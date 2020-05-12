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

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush {

public class SelectionWidget : GrabWidget {
  public event System.Action<TrTransform> SelectionTransformed;

  [SerializeField] private CanvasScript m_SelectionCanvas;

  private TrTransform m_xfOriginal_SS = TrTransform.identity;
  private Bounds? m_SelectionBounds_CS;

  private InputManager.ControllerName? m_CurrentIntersectionController;
  private InputManager.ControllerName? m_NextIntersectionController;
  private GpuIntersector.FutureBatchResult m_IntersectionFuture;
  private Dictionary<InputManager.ControllerName, float> m_LastIntersectionResult;
  private int m_IntersectionFrame;
  private Vector2 m_SizeRange;

  public override bool AllowDormancy {
    get {
      return InputManager.Brush.IsTrigger();
    }
  }

  public override float HapticDuration {
    get { return 0.05f; }
  }

  override protected void Awake() {
    base.Awake();
    m_LastIntersectionResult = new Dictionary<InputManager.ControllerName, float>();
    m_LastIntersectionResult[InputManager.ControllerName.Brush] = -1;
    m_LastIntersectionResult[InputManager.ControllerName.Wand] = -1;
    m_CustomShowHide = true;
    ResetSizeRange();
  }

  override protected void OnHide() {
    if (SelectionManager.m_Instance.HasSelection) {
      // Selection may already have been force-deleted in the case where a selection command was
      // issued before the selection widget was able to hide.
      SelectionManager.m_Instance.DeleteSelection();
    }
    gameObject.SetActive(true);
  }

  override protected void Start() {
    base.Start();
    App.Scene.PoseChanged += OnScenePoseChanged;
  }

  /// The transformation the user has performed on the widget box relative to the scene.
  public TrTransform SelectionTransform {
    get {
      return App.Scene.AsScene[transform] * m_xfOriginal_SS.inverse;
    }
    set {
      App.Scene.AsScene[transform] = value * m_xfOriginal_SS;
    }
  }

  public override void AssignControllerMaterials(InputManager.ControllerName controller) {
    if (!SketchControlsScript.m_Instance.IsUserTwoHandGrabbingWidget() &&
        SketchControlsScript.m_Instance.OneHandGrabController == controller) {
      InputManager.Controllers[(int)controller].Geometry.ShowDuplicateOption();
    }
  }

  public void SetSelectionBounds(Bounds bounds) {
    Debug.Assert(bounds.extents.magnitude > 0);

    m_SelectionBounds_CS = bounds;
    UpdateBoxCollider();
    gameObject.SetActive(true);
  }

  public void SelectionCleared() {
    m_SelectionBounds_CS = null;
    gameObject.SetActive(false);
    m_xfOriginal_SS = TrTransform.identity;
    App.Scene.AsScene[transform] = TrTransform.identity;
  }

  override public float GetSignedWidgetSize() {
    return transform.localScale.Max();
  }

  override protected void SetWidgetSizeInternal(float fSize) {
    // TODO: is this roundabout scheme necessary? Does the selection widget
    // really have non-uniform scale?
    transform.localScale = (transform.localScale / GetSignedWidgetSize()) * fSize;
    if (SelectionTransformed != null) {
      SelectionTransformed(SelectionTransform);
    }
  }

  public override Vector2 GetWidgetSizeRange() {
    return m_SizeRange;
  }

  /// Updates the size limits to be the intersection of the current and passed through range.
  public void UpdateSizeRange(Vector2 range) {
    m_SizeRange.x = Mathf.Max(m_SizeRange.x, range.x);
    m_SizeRange.y = Mathf.Min(m_SizeRange.y, range.y);
  }

  public void ResetSizeRange() {
    m_SizeRange = base.GetWidgetSizeRange();
  }

  // TODO: We're disabling "duplicate on hover" until we've had a chance to UER test
  // the basic functionality. Once that's been tested, we can experiment with what we do
  // when the selection tool hovers over an existing selection.
  //override public bool HasHoverInteractions() { return true; }
  //
  //override public void AssignHoverControllerMaterials(InputManager.ControllerName controller) {
  //  // If we're intersecting with the brush hand, allow additional actions.
  //  if (controller == InputManager.ControllerName.Brush) {
  //    InputManager.GetControllerGeometry(controller).ShowSelectionOptions();
  //  }
  //}

  override public float GetActivationScore(
      Vector3 vControllerPos_GS, InputManager.ControllerName name) {
    if (!PointInCollider(vControllerPos_GS)) {
      return -1;
    }

    // If the data we've got is old, delete it all.
    if ((Time.frameCount - m_IntersectionFrame) > 3) {
      m_CurrentIntersectionController = null;
      m_NextIntersectionController = null;
      m_IntersectionFuture = null;
    }

    // If we have an intersection in the pipe, set up the next one if it is of a different type.
    // This is so that if we're looking for both Wand and Brush, it will toggle between them.
    // If the results are ready, then store them off and clear the current intersection.
    if (m_CurrentIntersectionController.HasValue) {
      if (m_CurrentIntersectionController.Value != name) {
        m_NextIntersectionController = name;
      }
      if (m_IntersectionFuture.IsReady) {
        m_LastIntersectionResult[m_CurrentIntersectionController.Value] =
            m_IntersectionFuture.HasAnyIntersections() ? 1 : -1;
        m_CurrentIntersectionController = null;
        m_IntersectionFuture = null;
      }
    }

    // If we don't have a current intersection in the pipe, grab the next one if there is one,
    // or just start off the intersection we have been asked for.
    if (!m_CurrentIntersectionController.HasValue) {
      if (!m_NextIntersectionController.HasValue) {
        m_NextIntersectionController = name;
      }
      m_CurrentIntersectionController = m_NextIntersectionController.Value;
      m_NextIntersectionController = null;
      Debug.Assert(m_CurrentIntersectionController.Value == InputManager.ControllerName.Wand ||
          m_CurrentIntersectionController.Value == InputManager.ControllerName.Brush);
      // Because we may be requesting an intersection on another controller's behalf, don't use
      // the passed position, but instead the position respective of the enum.
      Vector3 pos = (m_CurrentIntersectionController.Value == InputManager.ControllerName.Brush) ?
          InputManager.Brush.Geometry.ToolAttachPoint.position :
          InputManager.Wand.Geometry.ToolAttachPoint.position;
      m_IntersectionFuture = App.Instance.GpuIntersector.RequestBatchIntersection(
          pos, m_CollisionRadius, (1 << m_SelectionCanvas.gameObject.layer));
      m_IntersectionFrame = Time.frameCount;
    }

    float result = -1;
    m_LastIntersectionResult.TryGetValue(name, out result);
    return result;
  }

  protected override void OnEndUpdateWithDesiredTransform() {
    base.OnEndUpdateWithDesiredTransform();
    if (SelectionTransformed != null) {
      SelectionTransformed(SelectionTransform);
    }
  }

  protected override void OnUpdate() {
    if (m_CurrentState == State.Tossed && SelectionTransformed != null) {
      SelectionTransformed(SelectionTransform);
    }
  }

  private void OnScenePoseChanged(TrTransform prev, TrTransform current) {
    UpdateBoxCollider();
  }

  private void UpdateBoxCollider() {
    if (!m_SelectionBounds_CS.HasValue) {
      return;
    }

    // Temporarily remember the user-made transformations on the selection
    // in scene-space. For example, when the user freshly selects strokes but has not moved
    // them, this will be Identity.
    TrTransform UserTransformations_SS = SelectionTransform;

    // Inflate bounding box so that we can still respect the collision
    // radius for parts of strokes that are at the outer edges of the
    // bounding box.
    Vector3 inflatedExtents_CS = m_SelectionBounds_CS.Value.extents;
    inflatedExtents_CS += Vector3.one * (m_CollisionRadius / m_SelectionCanvas.Pose.scale);

    // Position our widget within global space as if it's in canvas space
    // so that we can correctly set non-uniform scale. Even though we
    // override the non-uniform scale at the very end, we also do it here
    // because, even though TrTransform only considers uniform scale, it
    // gets its uniform scale from the longest extent on any non-uniform scale.
    transform.localPosition = m_SelectionBounds_CS.Value.center;
    transform.localScale = inflatedExtents_CS;
    transform.localRotation = Quaternion.identity;

    // Capture the scene-space transformation for the selected bounds
    // without considering how the user transformed the selection since
    // creating it.
    m_xfOriginal_SS = App.Scene.AsScene[transform];

    // Recombine the initial stroke transformation (the canvas-space bounds
    // within scene-space) with the transformations caused by the user manipulating
    // the selection (also in scene-space).
    App.Scene.AsScene[transform] = UserTransformations_SS * m_xfOriginal_SS;

    // Since TrTransform doesn't account for non-uniform scale, correct the
    // scale for the non-uniform bounds.
    transform.localScale = inflatedExtents_CS * UserTransformations_SS.scale;
  }

  public void PreventSelectionFromMoving(bool preventMoving) {
    // We're overriding pinning to prevent selections.
    m_Pinned = preventMoving;
  }

  override protected void OnUserBeginInteracting() {
    base.OnUserBeginInteracting();

    // Use pin visuals for preventing movement.
    WidgetManager.m_Instance.DestroyWidgetPin(m_Pin);
    m_Pin = WidgetManager.m_Instance.GetWidgetPin();
    InitPin();

    // Start disabled.
    m_Pin.gameObject.SetActive(false);

    // If we are pinned, jiggle the pin.
    if (m_Pinned) {
      m_Pin.gameObject.SetActive(true);
      m_Pin.WobblePin(m_InteractingController);
    }
  }
}

}  // namespace TiltBrush
