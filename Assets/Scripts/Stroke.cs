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
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TiltBrush {

public class Stroke : StrokeData {
  public enum Type {
    /// Brush stroke has not been realized into geometry (or whatever else it turns into)
    /// so we don't yet know whether it is batched or unbatched
    NotCreated,
    /// Brush stroke geometry exists in the form of a GameObject
    BrushStroke,
    /// Brush stroke geoemtry exists in the form of a BatchSubset
    BatchedBrushStroke,
  }

  // Instance API

  /// How the geometry is contained (if there is any)
  public Type m_Type = Type.NotCreated;
  /// Valid only when type == NotCreated. May be null.
  public CanvasScript m_IntendedCanvas;
  /// Valid only when type == BrushStroke. Never null; will always have a BaseBrushScript.
  public GameObject m_Object;
  /// Valid only when type == BatchedBrushStroke.
  public BatchSubset m_BatchSubset;

  /// used by SketchMemoryScript.m_Instance.m_MemoryList (ordered by time)
  public LinkedListNode<Stroke> m_NodeByTime;
  /// used by one of the lists in ScenePlayback (ordered by time)
  public LinkedListNode<Stroke> m_PlaybackNode;

  /// A copy of the StrokeData part of the stroke.
  /// Used for the saving thread to serialize the sketch.
  private StrokeData m_CopyForSaveThread;

  /// The group this stroke is a part of. Cannot be null (as it is a struct).
  public SketchGroupTag Group {
    get => m_Group;
    set {
      var oldGroup = m_Group;
      m_Group = value;

      SelectionManager.m_Instance.OnStrokeRemovedFromGroup(this, oldGroup);
      SelectionManager.m_Instance.OnStrokeAddedToGroup(this);
    }
  }

  /// Which control points on the stroke should be dropped due to simplification
  public bool[] m_ControlPointsToDrop;

  /// The canvas this stroke is a part of.
  public CanvasScript Canvas {
    get {
      if (m_Type == Type.NotCreated) {
        return m_IntendedCanvas;
      } else if (m_Type == Type.BatchedBrushStroke) {
        return m_BatchSubset.Canvas;
      } else if (m_Type == Type.BrushStroke) {
        return m_Object.GetComponent<BaseBrushScript>().Canvas;
      } else {
        throw new InvalidOperationException();
      }
    }
  }

  /// True if stroke's geometry exists and is active.  E.g. prior to playback, strokes
  /// will not have a geometry representation in the scene.  Strokes which are erased
  /// but available for redo will have inactive geometry.
  public bool IsGeometryEnabled {
    get {
      return this.m_Object != null && this.m_Object.activeSelf ||
             m_Type == Type.BatchedBrushStroke &&
             m_BatchSubset.m_Active;
    }
  }

  /// True if this stroke should be displayed on playback (i.e. not an erased or undone stroke).
  /// TODO: the setter is never used -- is that a bug, or should we remove the field?
  public bool IsVisibleForPlayback { get; /*set;*/ } = true;

  public uint HeadTimestampMs {
    get { return this.m_ControlPoints[0].m_TimestampMs; }
  }

  public uint TailTimestampMs {
    get { return this.m_ControlPoints.Last().m_TimestampMs; }
  }

  public float SizeInLocalSpace {
    get {
      return m_BrushScale * m_BrushSize;
    }
  }

  public float SizeInRoomSpace {
    get {
      var localToRoom = Coords.AsRoom[this.StrokeTransform];
      return localToRoom.scale * SizeInLocalSpace;
    }
  }

  /// Could be a batch or an individual object
  public Transform StrokeTransform {
    get {
      if (m_Object != null) {
        return m_Object.transform;
      } else {
        return m_BatchSubset.m_ParentBatch.transform;
      }
    }
  }

  public Stroke() {
    m_NodeByTime = new LinkedListNode<Stroke>(this);
    m_PlaybackNode = new LinkedListNode<Stroke>(this);
  }

  /// Clones the passed stroke into a new NotCreated stroke.
  ///
  /// Group affiliation is copied, implying that the resulting stroke:
  /// - must be put into the same sketch as 'existing'
  /// - is selectable, meaning the caller is responsible for getting it out of NotCreated
  /// The caller is responsible for setting result.Group = SketchGroupTag.None if
  /// those things aren't both true.
  ///
  /// TODO: semantics are cleaner & safer if group affiliation is not copied;
  /// caller can do it explicitly if desired.
  public Stroke(Stroke existing) : base(existing) {
    m_ControlPointsToDrop = new bool[existing.m_ControlPointsToDrop.Length];
    Array.Copy(existing.m_ControlPointsToDrop, m_ControlPointsToDrop,
               existing.m_ControlPointsToDrop.Length);

    // Alas, we can't chain constructor to this() because we chain to base(existing).
    // And we can't use field initializers for the linked list creation.
    m_NodeByTime = new LinkedListNode<Stroke>(this);
    m_PlaybackNode = new LinkedListNode<Stroke>(this);
  }

  /// Makes a copy of stroke, if one has not already been made.
  /// Should only be called by the 'ThreadedSave' method of SaveLoadScript
  public StrokeData GetCopyForSaveThread() {
    if (m_CopyForSaveThread == null) {
      m_CopyForSaveThread = new StrokeData(this);
    }
    return m_CopyForSaveThread;
  }

  public void InvalidateCopy() {
    m_CopyForSaveThread = null;
  }

  /// Releases all resources owned by the stroke; consider the stroke unusable after this.
  ///
  /// It is important that this be called before trying to drop a Stroke
  /// into the garbage. Not doing so causes memory leaks:
  /// - Batch <-> BatchSubset link won't get torn down
  /// - BatchSubset <-> Stroke link won't get torn down
  /// - Batch will never become empty, and never get deallocated
  /// - Stroke will never become garbage because of the Batch -> Subset -> Stroke link
  public void DestroyStroke() {
    Uncreate();
    // The object is still in a valid state; we should probably purposely vandalize it,
    // but some code might still erroneously use Destroy() when they mean Uncreate()
    // TODO: Find and fix those places
    //   m_Type = StrokeType.Destroyed;
    //   m_ControlPoints = null;
    //   m_CopyForSaveThread = null;
  }

  /// Sets type to NotCreated, releasing render resources if applicable.
  /// This means the subset will be destroyed!
  public void Uncreate() {
    // Save off before we lose the object/batch that tells us what canvas we're in
    m_IntendedCanvas = Canvas;

    if (m_Object != null) {
      Object.Destroy(m_Object);
      m_Object = null;
    }

    // TODO: instead of destroying, reuse the previous batch space if possible
    // (but be careful because the brush may have changed). Excessive use of Recreate()
    // will swiss-cheese our batches.
    if (m_BatchSubset != null) {
      // Invalidates subset; tears down Batch <-> Subset link
      m_BatchSubset.m_ParentBatch.RemoveSubset(m_BatchSubset);
      // Tear down Subset <-> Stroke link. Removing the Subset <-- Stroke link is
      // more for cleanliness than correctness; the Subset should get GC'd soon.
      // Maybe one of these references should be weak?
      m_BatchSubset.m_Stroke = null;
      m_BatchSubset = null;
    }

    m_Type = Type.NotCreated;
  }

  /// Ensure there is geometry for this stroke, creating if necessary.
  /// Optionally also calls SetParent() or LeftTransformControlPoints() before creation.
  ///
  /// Assumes that any existing geometry is up-to-date with the data in the stroke;
  /// this assumption may be used for optimizations. Caller may therefore wish to
  /// call Uncreate() before calling Recreate().
  ///
  /// TODO: name is misleading because geo may be reused instead of recreated
  ///
  /// TODO: Consider moving the code from the "m_Type == StrokeType.BrushStroke"
  /// case of SetParentKeepWorldPosition() into here.
  public void Recreate(TrTransform? leftTransform = null, CanvasScript canvas = null) {
    // TODO: Try a fast-path that uses VertexLayout+GeometryPool to modify geo directly
    if (leftTransform != null || m_Type == Type.NotCreated) {
      // Uncreate first, or SetParent() will do a lot of needless work
      Uncreate();
      if (canvas != null) {
        SetParent(canvas);
      }
      if (leftTransform != null) {
        LeftTransformControlPoints(leftTransform.Value);
      }
      // PointerManager's pointer management is a complete mess.
      // "5" is the most-likely to be unused. It's terrible that this
      // needs to go through a pointer.
      var pointer = PointerManager.m_Instance.GetTransientPointer(5);
      pointer.RecreateLineFromMemory(this);
    } else if (canvas != null) {
      SetParent(canvas);
    } else {
      // It's already created, not being moved, not being reparented -- the only
      // reason the caller might have done this is they expected the geo to be destroyed
      // and recreated. They're not going to get that, so treat this as a logic error.
      // I expect this case will go away when the name/params get fixed to something
      // more reasonable
      throw new InvalidOperationException("Nothing to do");
    }
  }

  /// Similar to Recreate() but takes a fast path by copying geometry from another
  /// stroke, if possible.
  /// This only makes sense if the stroke has no geometry, so it's an error otherwise.
  public void CopyGeometry(CanvasScript targetCanvas, Stroke baseStroke) {
    if (m_Type != Type.NotCreated) {
      throw new InvalidOperationException("stroke must be NotCreated");
    }
    if (baseStroke.m_Type != Type.BatchedBrushStroke) {
      throw new InvalidOperationException("baseStroke must have batched geometry");
    }
    // Copy buffers directly.  Only works with batched brush stroke.
    m_BatchSubset = targetCanvas.BatchManager.CreateSubset(baseStroke.m_BatchSubset);
    m_BatchSubset.m_Stroke = this;
    m_Object = null;
    m_IntendedCanvas = null;
    m_Type = Type.BatchedBrushStroke;
  }

  // TODO: Possibly could optimize this in C++ for 11.5% of time in selection.
  private void LeftTransformControlPoints(TrTransform leftTransform) {
    for (int i = 0; i < m_ControlPoints.Length; i++) {
      var point = m_ControlPoints[i];
      var xfOld = TrTransform.TR(point.m_Pos, point.m_Orient);
      var xfNew = leftTransform * xfOld;
      point.m_Pos = xfNew.translation;
      point.m_Orient = xfNew.rotation;
      m_ControlPoints[i] = point;
    }

    m_BrushScale *= leftTransform.scale;
    InvalidateCopy();
  }

  /// Set the parent canvas of this stroke, preserving the _canvas_-relative position.
  /// There will be a pop if the previous and current canvases have different
  /// transforms.
  ///
  /// Directly analagous to Transform.SetParent, except strokes may not
  /// be parented to an arbitrary Transform, only to CanvasScript.
  public void SetParent(CanvasScript canvas) {
    CanvasScript prevCanvas = Canvas;
    if (prevCanvas == canvas) {
      return;
    }

    switch (m_Type) {
    case Type.BatchedBrushStroke: {
      // Shortcut: move geometry to new BatchManager rather than recreating from scratch
      BatchSubset newSubset = canvas.BatchManager.CreateSubset(m_BatchSubset);
      m_BatchSubset.m_ParentBatch.RemoveSubset(m_BatchSubset);
      m_BatchSubset = newSubset;
      m_BatchSubset.m_Stroke = this;
      break;
    }
    case Type.BrushStroke: {
      m_Object.transform.SetParent(canvas.transform, false);
      break;
    }
    case Type.NotCreated: {
      m_IntendedCanvas = canvas;
      break;
    }
    }
  }

  /// Set the parent canvas of this stroke, preserving the scene-relative position.
  ///
  /// Slower than the other SetParent(), since the stroke might be recreated from scratch or
  /// transformed.  There will *not* be a pop if the brush's geometry generation is not
  /// transform-invariant.  So production brushes need to be checked to be transform-invariant
  /// some other way.
  ///
  /// Directly analagous to Transform.SetParent, except strokes may not
  /// be parented to an arbitrary Transform, only to CanvasScript.
  public void SetParentKeepWorldPosition(CanvasScript canvas, TrTransform? leftTransform = null) {
    CanvasScript prevCanvas = Canvas;
    if (prevCanvas == canvas) {
      return;
    }

    // Invariant is:
    //   newCanvas.Pose * newCP = prevCanvas.Pose * prevCP
    // Solve for newCp:
    //   newCP = (newCanvas.Pose.inverse * prevCanvas.Pose) * prevCP
    TrTransform leftTransformValue = leftTransform ?? canvas.Pose.inverse * prevCanvas.Pose;
    bool bWasTransformed = leftTransform.HasValue &&
        !TrTransform.Approximately(App.ActiveCanvas.Pose, leftTransform.Value);
    if (m_Type == Type.NotCreated || !bWasTransformed) {
      SetParent(canvas);
      LeftTransformControlPoints(leftTransformValue);
    } else {
      if (m_Type == Type.BrushStroke) {
        Object.Destroy(m_Object);
        m_Object = null;

        m_Type = Type.NotCreated;
        m_IntendedCanvas = canvas;

        LeftTransformControlPoints(leftTransform.Value);
        // PointerManager's pointer management is a complete mess.
        // "5" is the most-likely to be unused. It's terrible that this
        // needs to go through a pointer.
        var pointer = PointerManager.m_Instance.GetTransientPointer(5);
        pointer.RecreateLineFromMemory(this);
      } else {
        Debug.Assert(m_Type == Type.BatchedBrushStroke);

        // Shortcut: modify existing geometry to new BatchManager rather than recreating from
        // scratch.
        BatchSubset newSubset = canvas.BatchManager.CreateSubset(m_BatchSubset, leftTransform);
        m_BatchSubset.m_ParentBatch.RemoveSubset(m_BatchSubset);
        m_BatchSubset = newSubset;
        m_BatchSubset.m_Stroke = this;
        LeftTransformControlPoints(leftTransform.Value);
      }
    }
  }
}
} // namespace TiltBrush