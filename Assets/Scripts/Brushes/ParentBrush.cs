// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;

using UnityEngine;

namespace TiltBrush {

/*
  Design questions raised:

  - How should strokes be saved?
    1. Save one stroke, with the control points of the parent-most stroke.
       At load time, reconstruct the entire tree of strokes.
    2. "Bake" out all strokes with geometry; ignore parent strokes.
       At load time this looks like a normal sketch.

    Tradeoffs:
    #1 makes sketches more fragile; more logic is run at load time, and if that
    logic changes, the sketch changes. Also, #1 requires a hacked Tilt Brush to
    load. #2 can be loaded by a regular build.

  - How should #1 be implemented? We currently assume one stroke <-> one batch.
    Generalize it to one stroke <-> many batches?

    When line is ended, currently Pointer is in charge of finalization, then
    it passes the line (and maybe subset) to SketchMemory.
    Instead, pass the line only to SketchMemory, and have it do the finalization
    (if necessary) and saving of the strokes.

  - How should #2 be implemented?
    We'd have to remove control-point storage out of PointerScript (a good idea anyway).
    Then we could call MemorizeBrushStroke on each of the strokes in the tree.

 */

/// ParentBrush is an experimental brush which does not have any geometry itself;
/// instead, it has child brushes which may either create geometry, or be ParentBrushes
/// themselves(!).
public abstract class ParentBrush : BaseBrushScript {
  /// Enumerates the coordinate frames associated with knots that are useful for attachments.
  [Serializable]
  public enum AttachFrame {
    /// The pose of the input device (or Pointer) when the knot was placed.
    Pointer,

    /// Assumes the Knots describe a line.  Forward is aligned to the tangent; right and up are
    /// normal and binormal.  Parallel transport is used to determine how right and up change
    /// from knot to knot, so this framing is all bend, no twist.
    LineTangent,
    // Assumes Knots describe a ribbon.  Forward is the main tangent (the one that's also the
    // line tangent); ??? is the bitangent (ie the other surface tangent); and ??? is the
    // surface normal.  Currently unused.
    RibbonTangent,
  }

  /// Analagous to GeometryBrush's knots, but less info is needed.
  protected class PbKnot {
    // Pose of the pointer, in Canvas space.
    public TrTransform m_pointer;
    // Just a quat because T and S are assumed to be the same as m_pointer.
    public Quaternion? m_tangentFrame;
    // Total distance from knot 0.
    public float m_distance;
    // Stroke BaseSize, modulated by pressure.
    public float m_pressuredSize;

    public TrTransform CanvasFromTool {
      get {
        return TrTransform.TR(m_pointer.translation, m_pointer.rotation);
      }
    }

    public TrTransform CanvasFromTangent {
      get {
        if (m_tangentFrame == null) {
          throw new InvalidOperationException("Frame not defined on this knot");
        }
        return TrTransform.TR(m_pointer.translation, m_tangentFrame.Value);
      }
    }

    public TrTransform GetFrame(AttachFrame frame) {
      switch (frame) {
      case AttachFrame.Pointer: return CanvasFromTool;
      case AttachFrame.LineTangent: return CanvasFromTangent;
      default: throw new NotImplementedException();
      }
    }

    public PbKnot Clone() {
      return new PbKnot {
        m_pointer = m_pointer,
        m_tangentFrame = m_tangentFrame,
        m_distance = m_distance,
        m_pressuredSize = m_pressuredSize
      };
    }
  }

  //
  // PbChild classes
  //

  /// This is the main abstraction that determines the movement/behavior of child brushes.
  protected abstract class PbChild {
    public BaseBrushScript m_brush;

    /// Returns a pointer position for the child brush.
    public TrTransform CalculateChildXfFixedScale(List<PbKnot> parentKnots) {
      // Brushes interpret .scale to mean "size of the person/pointer drawing me".
      // They also assume that .scale is identical for all knots in the stroke, since
      // users can't draw and scale themselves at the same time.
      // Thus, for now, keep enforcing that assumption.
      // TODO: revisit this when we understand more.
      var ret = CalculateChildXf(parentKnots);
      return TrTransform.TRS(ret.translation, ret.rotation, parentKnots[0].m_pointer.scale);
    }

    // Subclasses should return a canvas-space position and orientation for the child pointer.
    // The parent pointer's position can be inferred from the transform of the most-recent knot.
    // At the moment, .scale on the return value is ignored.
    protected abstract TrTransform CalculateChildXf(List<PbKnot> parentKnots);
  }

  /// The simplest possible implementation -- doesn't modify pointer transform at all.
  protected class PbChildIdentityXf : PbChild {
    protected override TrTransform CalculateChildXf(List<PbKnot> parentKnots) {
      var cur = parentKnots[parentKnots.Count-1];
      return cur.m_pointer;
    }
  }

  /// A PbChild that need some sort of knot-based reference frame.
  protected abstract class PbChildWithKnotBasedFrame : PbChild {
    public readonly int m_frameKnot;
    public readonly AttachFrame m_frame;

    /// If frameKnot is < 0, it is relative to a knot at the current end of the stroke.
    public PbChildWithKnotBasedFrame(int frameKnot, AttachFrame frame) {
      m_frameKnot = frameKnot;
      m_frame = frame;
    }

    /// Returns the attach point for this child, as a knot.
    /// See also GetAttachTransform().
    public PbKnot GetAttachKnot(List<PbKnot> parentKnots) {
      int knotIndex = (m_frameKnot >= 0) ? m_frameKnot : parentKnots.Count + m_frameKnot;
      return parentKnots[knotIndex];
    }

    /// Returns the attach point for this child, as a TrTransform.
    public TrTransform GetAttachTransform(List<PbKnot> parentKnots) {
      return GetAttachKnot(parentKnots).GetFrame(m_frame);
    }
  }

  /// Applies a transform, specified relative to a knot's tangent or pointer frame.
  ///
  /// The knot may be specified relative to the start of the stroke for things like tree
  /// branches, or relative to the current end of the stroke for things like spirals.
  ///
  /// There is an optional distance-based twist about frame-forward.
  protected class PbChildWithOffset : PbChildWithKnotBasedFrame {
    public TrTransform m_offset;
    public float m_twist;
    public bool m_pressureAffectsOffset;

    /// Pass:
    ///   frameKnot - the index of a knot; negative to index from the end.
    ///   offset - specified relative to the knot+frame, as a %age of brush size.
    ///   twist - in degrees-per-meter; rotates based on distance from initial knot
    ///   pressureAffectsOffset - true if the offset should be modulated by pressure, too.
    ///     For example, the rungs of a double-helix should be closer to the parent
    ///     when pressure makes the brush smaller; but maybe a tickertape should remain
    ///     constant-size and pressure only affects the stuff written on the tape.
    public PbChildWithOffset(int frameKnot, AttachFrame frame, TrTransform offset, float twist,
                             bool pressureAffectsOffset = true)
        : base(frameKnot, frame) {
      m_offset = offset;
      m_twist = twist;
      m_pressureAffectsOffset = pressureAffectsOffset;
    }

    protected override TrTransform CalculateChildXf(List<PbKnot> parentKnots) {
      TrTransform actionInCanvasSpace; {
        PbKnot knot = GetAttachKnot(parentKnots);
        float rotationDegrees = knot.m_distance * App.UNITS_TO_METERS * m_twist;
        TrTransform offset = m_offset;

        // It's cleaner to tack a TrTransform.S(size) onto the action, but that
        // would modify .scale, which is currently interpreted as "pointer size"
        // (affecting control point density) and which is also assumed by most
        // brushes to be constant.
        if (m_pressureAffectsOffset) {
          offset.translation *= knot.m_pressuredSize;
        } else {
          offset.translation *= m_brush.BaseSize_LS;
        }

        TrTransform action =
            TrTransform.R(Quaternion.AngleAxis(rotationDegrees, Vector3.forward))
            * offset;
        actionInCanvasSpace = action.TransformBy(knot.GetFrame(m_frame));
      }

      var cur = parentKnots[parentKnots.Count-1];
      return actionInCanvasSpace * cur.m_pointer;
    }
  }

  //
  // Instance api
  //

  const float kSolidAspectRatio = 0.2f;

  protected List<PbKnot> m_knots = new List<PbKnot>();
  protected List<PbChild> m_children = new List<PbChild>();
  protected int m_recursionLevel = 0;

  //
  // ParentBrush stuff
  //

  public ParentBrush() : base(bCanBatch : false) { }

  // Finishes initializing the passed PbChild, and adds to children array
  protected void InitializeAndAddChild(
      PbChild child,
      BrushDescriptor desc, Color color, float relativeSize = 1) {
    Debug.Assert(child.m_brush == null);
    TrTransform childXf = child.CalculateChildXfFixedScale(m_knots);
    BaseBrushScript brush = Create(
        transform.parent, childXf, desc, color, m_BaseSize_PS * relativeSize);
    ParentBrush pb = brush as ParentBrush;

    string originalName = brush.gameObject.name;
    string newName;
    if (pb != null) {
      newName = string.Format("{0}.{1}", gameObject.name, m_children.Count);
    } else {
      newName = string.Format(
          "{0}.{1} (Leaf {2})", gameObject.name, m_children.Count, originalName);
    }
    brush.gameObject.name = newName;

    if (pb != null) {
      pb.m_recursionLevel = m_recursionLevel + 1;
    }

    child.m_brush = brush;
    m_children.Add(child);
  }

  private void MaybeCreateChildren() {
    // Wait until we have a tangent frame
    if (m_knots[1].m_tangentFrame == null) { return; }
    // Wait until we have non-zero size, because creating children with size 0 is terrible.
    // The preview line is evil and changes our size between control points.
    if (m_BaseSize_PS == 0) { return; }

    MaybeCreateChildrenImpl();
  }

  // Looks through children to find ones that are attached to a knot.
  // Returns the distance to the most recent knot with a child.
  // If there are no such children, returns distance to the start of stroke.
  protected float DistanceSinceLastKnotBasedChild() {
    PbKnot cur = m_knots[m_knots.Count-1];
    float minDistance = cur.m_distance;
    foreach (var child_ in m_children) {
      var child = child_ as PbChildWithOffset;
      if (child != null) {
        float distanceFromChildToTip = cur.m_distance - child.GetAttachKnot(m_knots).m_distance;
        minDistance = Mathf.Min(minDistance, distanceFromChildToTip);
      }
    }
    return minDistance;
  }

  void OnDestroy() {
    foreach (var child in m_children) {
      BaseBrushScript bbs = child.m_brush;
      bbs.DestroyMesh();
      Destroy(bbs.gameObject);
    }
  }

  /// Subclasses should fill this in.
  /// Callee can assume the size is valid and at least one tangent frame exists.
  protected abstract void MaybeCreateChildrenImpl();

  //
  // BaseBrushScript api
  //

  protected override bool UpdatePositionImpl(
      Vector3 translation, Quaternion rotation, float pressure) {
    TrTransform parentXf = TrTransform.TR(translation, rotation);

    // Update m_knots
    {
      Debug.Assert(m_knots.Count > 1, "There should always be at least 2 knots");
      PbKnot cur = m_knots[m_knots.Count-1];
      PbKnot prev = m_knots[m_knots.Count-2];
      Vector3 move = parentXf.translation - prev.m_pointer.translation;

      cur.m_pointer = parentXf;
      float moveLen = move.magnitude;
      cur.m_tangentFrame = (moveLen > 1e-5f)
          ? MathUtils.ComputeMinimalRotationFrame(
              move / moveLen, prev.m_tangentFrame, cur.m_pointer.rotation)
          : prev.m_tangentFrame;
      cur.m_distance = prev.m_distance + moveLen;
      cur.m_pressuredSize = PressuredSize(pressure);
    }

    MaybeCreateChildren();

    bool createdControlPoint = false;
    for (int i = 0; i < m_children.Count; ++i) {
      PbChild child = m_children[i];
      var childXf = child.CalculateChildXfFixedScale(m_knots);
      if (child.m_brush.UpdatePosition_LS(childXf, pressure)) {
        // Need to save off any control point which is applicable to any of our children.
        // This does mean that if we have a giant tree of children, we might be saving
        // off every control point.
        // TODO: maybe there's a way for the parent to impose some order on this;
        // like it doesn't always send positions to its children? But that would make
        // interactive drawing less pretty.
        createdControlPoint = true;
      }
    }

    if (createdControlPoint) {
      m_knots.Add(m_knots[m_knots.Count - 1].Clone());
    }

    return createdControlPoint;
  }

  void CommonInit(TrTransform localPointerXf) {
    for (int i = 0; i < 2; ++i) {
      m_knots.Add(new PbKnot {
          m_pointer = localPointerXf,
          m_pressuredSize = PressuredSize(1)
        });
    }
  }

  protected override void InitBrush(BrushDescriptor desc, TrTransform localPointerXf) {
    base.InitBrush(desc, localPointerXf);
    CommonInit(localPointerXf);
  }

  public override void ResetBrushForPreview(TrTransform localPointerXf) {
    base.ResetBrushForPreview(localPointerXf);
    m_knots.Clear();

    foreach (var child in m_children) {
      if (child != null) {
        Destroy(child.m_brush.gameObject);
      }
    }
    m_children.Clear();

    CommonInit(localPointerXf);
  }

  public override bool AlwaysRebuildPreviewBrush() {
    return true;
  }

  public override GeometryPool.VertexLayout GetVertexLayout(BrushDescriptor desc) {
    return new GeometryPool.VertexLayout {
      bUseColors = false,
      bUseNormals = false,
      bUseTangents = false,
      uv0Size = 2,
      uv0Semantic = GeometryPool.Semantic.XyIsUv,
      uv1Size = 0,
      uv1Semantic = GeometryPool.Semantic.Unspecified
    };
  }

  public override void ApplyChangesToVisuals() {
    foreach (var child in m_children) {
      child.m_brush.ApplyChangesToVisuals();
    }
  }

  public override int GetNumUsedVerts() {
    // TODO: can this be zero? I seem to remember bad things happening
    // with brushes that return 0, should check.
    int count = 1;
    foreach (var child in m_children) {
      count += child.m_brush.GetNumUsedVerts();
    }
    return count;
  }

  public override float GetSpawnInterval(float pressure01) {
    return PressuredSize(pressure01) * kSolidAspectRatio;
  }

  protected override void InitUndoClone(GameObject clone) {
    // TODO: CloneAsUndoObject() won't clone any of our children (because they're
    // not in transform.children). And even if it could, we'd have difficulties mapping
    // m_children to transform.children. I guess we could assume they're parallel arrays.
    //
    // I think the correct thing to do here is to move CloneAsUndoObject() into BaseBrushScript.
  }

  public override void FinalizeSolitaryBrush() {
    foreach (var child in m_children) {
      child.m_brush.FinalizeSolitaryBrush();
    }
  }

  public override BatchSubset FinalizeBatchedBrush() {
    throw new NotImplementedException();
  }
}

} // namespace TiltBrush
