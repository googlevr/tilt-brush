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

using UnityEngine;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TiltBrush {

public abstract class StencilWidget : GrabWidget {
  [SerializeField] protected float m_MinSize_CS;
  [SerializeField] protected float m_MaxSize_CS;
  [SerializeField] private Color m_TintColor;
  [SerializeField] protected float m_StencilGrabDistance = 1.0f;
  [SerializeField] protected float m_PointerLiftSlope;

  protected Collider m_Collider;
  protected float m_Size = 1.0f;
  protected StencilType m_Type;
  private bool m_SkipIntroAnim;
  private float m_PreviousShowRatio;
  protected Vector3 m_KahanSummationC;
  protected bool m_StickyTransformEnabled;
  protected TrTransform m_StickyTransformBreakDelta;

  // null means not locked. Invalid means "locked, but no axis"
  protected Axis? m_LockedManipulationAxis;

  /// The full extent along each axis, to support non-uniform scale.
  /// Some subclasses (eg spheres) may not support assignment of non-uniform extent.
  public abstract Vector3 Extents {
    get; set;
  }

  // Currently used for:
  // - undo/redo (which treats it as opaque)
  // - scale (which treats it as an extent in order to calculate delta-size range)
  // Previously used for:
  // - save/load (which treats it as opaque but also requires that the meaning not change)
  //
  // It should really only be used for undo/redo
  public override Vector3 CustomDimension {
    get { return Vector3.one; }
    set { }
  }

  /// Data that is saved to the .tilt file.
  /// Be very careful when changing this, because it affects the save file format.
  /// This does not really need to be virtual, except to implement the temporary
  /// backwards-compatibility code.
  public Guides.State GetSaveState(GroupIdMapping groupIdMapping) {
    return new Guides.State {
        Transform = TrTransform.TRS(transform.localPosition, transform.localRotation, 0),
        Extents = Extents,
        Pinned = m_Pinned,
        GroupId = groupIdMapping.GetId(Group)
    };
  }
  public Guides.State SaveState {
    set {
      transform.localPosition = value.Transform.translation;
      transform.localRotation = value.Transform.rotation;
      Extents = value.Extents;
      if (value.Pinned) {
        PinFromSave();
      }
      Group = App.GroupManager.GetGroupFromId(value.GroupId);
    }
  }

  /// Returns the axis the user probably means to modify.
  /// Subclasses are free to return any Axis value, including Invalid (no preferred axis)
  /// Pass:
  ///   primaryHand - the hand that first grabbed the object. Guaranteed to be inside.
  ///   secondaryHand - the other hand grabbing the object. Not guaranteed to be inside.
  protected abstract Axis GetInferredManipulationAxis(
      Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInside);

  /// Implementation must handle any axes returned by GetInferredManipulationAxis(),
  /// except for Invalid which is handled by StencilWidget.RegisterHighlight
  protected abstract void RegisterHighlightForSpecificAxis(Axis highlightAxis);

  /// All StencilWidgets are expected to comply with axis locking,
  /// so the base GrabWidget implementation is inappropriate.
  ///
  /// Implementations should ignore handA and handB, and return results
  /// for m_LockedManipulationAxis, which is guaranteed to be non-null.
  public abstract override Axis GetScaleAxis(
      Vector3 handA, Vector3 handB,
      out Vector3 axisVec, out float extent);

  override protected void Awake() {
    base.Awake();
    // Normalize for size.
    // Use transform.localScale.x because prefabs have scales != Vector3.one.
    m_Size = transform.localScale.x / Coords.CanvasPose.scale;

    // Manually apply Canvas scale because Awake() is called before the transform is parented.
    var sizeRange = GetWidgetSizeRange();
    if (m_Size < sizeRange.x) {
      m_Size = sizeRange.x;
      transform.localScale = m_Size * Vector3.one * Coords.CanvasPose.scale;
    }
    if (m_Size > sizeRange.y) {
      m_Size = sizeRange.y;
      transform.localScale = m_Size * Vector3.one * Coords.CanvasPose.scale;
    }

    m_Collider = GetComponentInChildren<Collider>();
    InitSnapGhost(m_Collider.transform, transform);

    // Pull tintable meshes from collider and reuse them for the highlight meshes.
    m_HighlightMeshFilters = m_TintableMeshes.Select(x => x.GetComponent<MeshFilter>()).ToArray();

    // Custom pin scalar for stencils.
    m_PinScalar = 0.5f;

    // Set a new batchId on this image so it can be picked up in GPU intersections.
    m_BatchId = GpuIntersector.GetNextBatchId();
    WidgetManager.m_Instance.AddWidgetToBatchMap(this, m_BatchId);
    HierarchyUtils.RecursivelySetMaterialBatchID(transform, m_BatchId);
    RestoreGameObjectLayer(App.Scene.MainCanvas.gameObject.layer);
  }

  override public GrabWidget Clone() {
    StencilWidget clone = Instantiate(WidgetManager.m_Instance.GetStencilPrefab(this.Type));
    clone.transform.position = transform.position;
    clone.transform.rotation = transform.rotation;
    clone.m_SkipIntroAnim = true;
    // We want to lie about our intro transition amount.
    clone.m_ShowTimer = clone.m_ShowDuration;
    clone.transform.parent = transform.parent;
    clone.Show(true, false);
    clone.SetSignedWidgetSize(this.m_Size);
    clone.Extents = this.Extents;
    HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);

    CanvasScript canvas = transform.parent.GetComponent<CanvasScript>();
    if (canvas != null) {
      var materials = clone.GetComponentsInChildren<Renderer>().SelectMany(x => x.materials);
      foreach (var material in materials) {
        foreach (string keyword in canvas.BatchManager.MaterialKeywords) {
          material.EnableKeyword(keyword);
        }
      }
    }

    return clone;
  }

  // Given a pos, find the closest position and surface normal of the stencil widget's collider.
  //   - surfacePos is the closest position on the surface, but in the case of ambiguity, will
  //     return a position most appropriate for the user experience.
  //   - surfaceNorm is always outward facing, and in cases of ambiguity, will return a vector
  //     most appropriate for the user experience.
  public virtual void FindClosestPointOnSurface(Vector3 pos,
      out Vector3 surfacePos, out Vector3 surfaceNorm) {
    surfacePos = transform.position;
    surfaceNorm = transform.forward;
  }

  override public Vector2 GetWidgetSizeRange() {
    return new Vector2(m_MinSize_CS, m_MaxSize_CS);
  }

  override protected void OnUserBeginTwoHandGrab(
      Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInObject) {
    base.OnUserBeginTwoHandGrab(primaryHand, secondaryHand, secondaryHandInObject);
    m_LockedManipulationAxis = GetInferredManipulationAxis(
        primaryHand, secondaryHand, secondaryHandInObject);
  }

  override protected void OnUserEndTwoHandGrab() {
    base.OnUserEndTwoHandGrab();
    m_LockedManipulationAxis = null;
  }

  override protected void OnShow() {
    base.OnShow();

    if (!m_SkipIntroAnim) {
      m_IntroAnimState = IntroAnimState.In;
      Debug.Assert(!IsMoving(), "Shouldn't have velocity!");
      ClearVelocities();
      m_IntroAnimValue = 0.0f;
      UpdateIntroAnim();
    } else {
      m_IntroAnimState = IntroAnimState.On;
    }

    // Refresh visibility with current state of stencil interaction.
    RefreshVisibility(WidgetManager.m_Instance.StencilsDisabled);
    UpdateMaterialScale();
    SpoofScaleForShowAnim(GetShowRatio());
  }

  public override void RestoreFromToss() {
    m_SkipIntroAnim = true;
    base.RestoreFromToss();
  }

  virtual public void SetInUse(bool bInUse) {
    if (m_TintableMeshes != null) {
      Color rMatColor = bInUse && !WidgetManager.m_Instance.WidgetsDormant ?
          m_TintColor : GrabWidget.m_InactiveGrey;
      for (int i = 0; i < m_TintableMeshes.Length; ++i) {
        m_TintableMeshes[i].material.color = rMatColor;
      }
    }
  }

  public void RefreshVisibility(bool bStencilDisabled) {
    if (m_TintableMeshes != null) {
      for (int i = 0; i < m_TintableMeshes.Length; ++i) {
        m_TintableMeshes[i].enabled = !bStencilDisabled;
      }
    }
  }

  // As the user paints on a stencil, the lift offset should grow at a steady rate to allow layers
  // to build up.
  [MethodImpl(MethodImplOptions.NoOptimization)]
  public void AdjustLift(float fDistance_CS) {
    // Kahan sum algorithm, https://en.wikipedia.org/wiki/Kahan_summation_algorithm
    // Keep track of the "leftover" that doesn't get applied (as a result of precision issues)
    // and apply it the next time around.
    //   y = input[i] - c
    //   tmp = sum + y
    //   c = (tmp - sum) - y
    //   sum = tmp
    Vector3 liftAmount_CS = m_PointerLiftSlope * fDistance_CS * Vector3.one;
    liftAmount_CS -= m_KahanSummationC;
    Vector3 tmp = Extents + liftAmount_CS;
    // compiler must be prevented from "optimizing" this to zero
    m_KahanSummationC = (tmp - Extents) - liftAmount_CS;
    Extents = tmp;
  }

  // Maintain the invariant that localScale == m_Size, and that aspect ratio
  // (if supported) and m_Size satisfy the invariants:
  //   extent = aspectRatio * size
  //   aspectRatio.max() == 1
  //
  // Should be called after touching m_Size (or better yet, why not just call SetWidgetSize?)
  protected virtual void UpdateScale() {
    transform.localScale = m_Size * Vector3.one;
    UpdateMaterialScale();
  }

  override public float GetSignedWidgetSize() {
    return m_Size;
  }

  override protected void SetWidgetSizeInternal(float fScale) {
    // Allow stencil sizes to go beyond range due to pointer lift.
    m_Size = fScale;
    UpdateScale();
  }

  public StencilType Type {
    get { return m_Type; }
  }

  public static void FromGuideIndex(Guides guide) {
    StencilType stencilType = guide.Type;

    foreach (var state in guide.States) {
      StencilWidget stencil = Instantiate(
          WidgetManager.m_Instance.GetStencilPrefab(stencilType));

      stencil.m_SkipIntroAnim = true;
      stencil.transform.parent = App.Instance.m_CanvasTransform;
      try {
        stencil.SaveState = state;
      } catch (ArgumentException e) {
        Debug.LogException(e, stencil);
      }
      stencil.Show(true, false);
    }
  }

  protected void UpdateMaterialScale() {
    // Because I hate the name Vector3.Scale.
    Vector3 Mul(Vector3 a, Vector3 b) => Vector3.Scale(a, b);

    // Update visuals
    if (m_TintableMeshes != null) {
      // Parent (if there is one) will be a Canvas, and never has nonuniform scale
      Vector3 parentScale = (transform.parent == null) ? Vector3.one : transform.parent.localScale;
      parentScale.x = 1;

      foreach (Renderer r in m_TintableMeshes) {
        r.material.SetVector("_LocalScale",
                             Mul(parentScale, Mul(transform.localScale, r.transform.localScale)));
      }
    }
  }

  override protected void OnUpdate() {
    float showRatio = GetShowRatio();
    if (m_PreviousShowRatio != showRatio) {
      SpoofScaleForShowAnim(showRatio);
      m_PreviousShowRatio = showRatio;
    }
  }

  virtual protected void SpoofScaleForShowAnim(float showRatio) {
    transform.localScale = m_Size * showRatio * Vector3.one;
  }

  override protected void OnUserBeginInteracting() {
    base.OnUserBeginInteracting();
    m_LockedManipulationAxis = null;
    if (m_TintableMeshes != null) {
      Shader.SetGlobalFloat("_UserIsInteractingWithStencilWidget", 1.0f);
    }
  }

  override protected void OnUserEndInteracting() {
    base.OnUserEndInteracting();
    if (m_TintableMeshes != null) {
      Shader.SetGlobalFloat("_UserIsInteractingWithStencilWidget", 0.0f);
    }
  }

  public override void RegisterHighlight() {
    if (m_Pinned || !m_UserInteracting || App.Config.IsMobileHardware) {
      if (!WidgetManager.m_Instance.WidgetsDormant) {
        base.RegisterHighlight();
      }
      return;
    }

    var primaryName = m_InteractingController;
    // Guess at what the other manipulating controller is
    var secondaryName = (m_InteractingController == InputManager.ControllerName.Brush)
        ? InputManager.ControllerName.Wand
        : InputManager.ControllerName.Brush;
    var primary = InputManager.Controllers[(int)primaryName].Transform.position;
    var secondary = InputManager.Controllers[(int)secondaryName].Transform.position;
    bool secondaryInside = GetActivationScore(secondary, secondaryName) >= 0;

    var highlightAxis = m_LockedManipulationAxis
        ?? GetInferredManipulationAxis(primary, secondary, secondaryInside);
    if (highlightAxis == Axis.Invalid) {
      base.RegisterHighlight();
    } else {
      RegisterHighlightForSpecificAxis(highlightAxis);
    }
  }

  override public void RestoreGameObjectLayer(int layer) {
    HierarchyUtils.RecursivelySetLayer(transform, layer);

    int layerIndex = Pinned ? WidgetManager.m_Instance.PinnedStencilLayerIndex :
                              WidgetManager.m_Instance.StencilLayerIndex;

    // The stencil collider object has to stay in the stencil layer so it can be picked
    // up by physics checks.
    m_Collider.gameObject.layer = WidgetManager.m_Instance.StencilLayerIndex;
    for (int i = 0; i < m_TintableMeshes.Length; ++i) {
      m_TintableMeshes[i].gameObject.layer = layerIndex;
    }
  }

  protected override void InitPin() {
    base.InitPin();

    int layerIndex = Pinned ? WidgetManager.m_Instance.PinnedStencilLayerIndex :
                              WidgetManager.m_Instance.StencilLayerIndex;

    for (int i = 0; i < m_TintableMeshes.Length; ++i) {
      m_TintableMeshes[i].gameObject.layer = layerIndex;
    }
  }
}
}  // namespace TiltBrush
