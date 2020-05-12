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

namespace TiltBrush {

public class BaseTool : MonoBehaviour {
  public enum ToolType {
    SketchSurface,
    Selection,
    ColorPicker,
    BrushPicker,
    BrushAndColorPicker,
    SketchOrigin,
    AutoGif,
    CanvasTool,
    TransformTool,
    StampTool,
    FreePaintTool,
    EraserTool,
    ScreenshotTool,
    DropperTool,
    SaveIconTool,
    ThreeDofViewingTool,
    MultiCamTool,
    TeleportTool,
    RepaintTool,
    RecolorTool,
    RebrushTool,
    SelectionTool,
    PinTool,
    EmptyTool,
    CameraPathTool,
  }
  public ToolType m_Type;

  public bool m_ShowTransformGizmo = false;
  [SerializeField] protected bool m_ExitOnAbortCommand = true;
  [SerializeField] protected bool m_ScalingSupported = false;

  public virtual float ButtonHoldDuration { get { return 1.0f; } }

  protected Transform m_Parent;
  protected SketchSurfacePanel m_SketchSurface;
  protected Vector3 m_ParentBaseScale;
  private Vector3 m_ParentScale;

  protected bool m_RequestExit;
  protected bool m_EatInput;
  protected bool m_AllowDrawing;
  protected bool m_ToolHidden;

  public bool IsEatingInput { get { return m_EatInput; } }

  public bool ExitRequested() { return m_RequestExit; }
  public bool ToolHidden() { return m_ToolHidden; }
  public void EatInput() { m_EatInput = true; }
  public void AllowDrawing(bool bAllow) { m_AllowDrawing = bAllow; }
  public virtual bool ShouldShowPointer() { return false; }

  virtual public void Init() {
    m_Parent = transform.parent;
    if (m_Parent != null) {
      m_ParentBaseScale = m_Parent.localScale;
      m_SketchSurface = m_Parent.GetComponent<SketchSurfacePanel>();
    }
  }

  virtual protected void Awake() {
    // Some tools attach things to controllers (like the camera to the brush in SaveIconTool)
    // and these need to be swapped when the controllers are swapped.
    InputManager.OnSwapControllers += OnSwap;
  }

  virtual protected void OnDestroy() {
    InputManager.OnSwapControllers -= OnSwap;
  }

  virtual protected void OnSwap() { }

  virtual public void HideTool(bool bHide) {
    m_ToolHidden = bHide;
  }

  virtual public bool ShouldShowTouch() {
    return true;
  }

  virtual public bool CanShowPromosWhileInUse() {
    return true;
  }

  virtual public void EnableTool(bool bEnable) {
    m_RequestExit = false;
    gameObject.SetActive(bEnable);

    if (bEnable) {
      if (m_Parent != null) {
        m_ParentScale = m_Parent.localScale;
        m_Parent.localScale = m_ParentBaseScale;
      }
      PointerManager.m_Instance.RequestPointerRendering(ShouldShowPointer());
    } else {
      if (m_Parent != null) {
        m_Parent.localScale = m_ParentScale;
      }
      m_EatInput = false;
      m_AllowDrawing = false;
    }
  }

  // Called only on frames that UpdateTool() has been called.
  // Guaranteed to be called after new poses have been received from OpenVR.
  virtual public void LateUpdateTool() {}

  virtual public void UpdateTool() {
    if (m_EatInput) {
      if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate)) {
        m_EatInput = false;
      }
    }
    if (m_ExitOnAbortCommand && InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Abort)) {
      m_RequestExit = true;
    }
  }

  // Called to notify the tool that it should assign controller materials.
  public virtual void AssignControllerMaterials(InputManager.ControllerName controller) {
  }

  public bool ScalingSupported() {
    return m_ScalingSupported;
  }

  // Modifies the size by some amount determined by the implementation.
  // _usually_ this will have the effect that GetSize() changes by fAdjustAmount,
  // but not necessarily (see FreePaintTool).
  virtual public void UpdateSize(float fAdjustAmount) { }

  virtual public void Monitor() { }

  virtual public float GetSizeRatio(
      InputManager.ControllerName controller, VrInput input) {
    if (controller == InputManager.ControllerName.Brush) {
      return GetSize01();
    }
    return 0.0f;
  }

  virtual public float GetSize() {
    return 0.0f;
  }

  // Returns a number in [0,1]
  virtual public float GetSize01() {
    return 0.0f;
  }

  virtual public void SetColor(Color rColor) {
  }

  virtual public void SetExtraText(string sExtra) {
  }

  virtual public void SetToolProgress(float fProgress) {
  }

  virtual public void BacksideActive(bool bActive) {
  }

  virtual public bool LockPointerToSketchSurface() {
    return true;
  }

  virtual public bool AllowsWidgetManipulation() {
    return true;
  }

  // Overridden by classes to set when tool sizing interaction should be disabled.
  virtual public bool CanAdjustSize() {
    return true;
  }

  // Overridden by classes to set when gaze and widget interaction should be disabled.
  virtual public bool InputBlocked() {
    return false;
  }

  virtual public bool AllowWorldTransformation() {
    return true;
  }

  // True if this tool can be used while a sketch is loading.
  virtual public bool AvailableDuringLoading() {
    return false;
  }

  // If this is true, the tool will tell the panels to hide.
  virtual public bool HidePanels() {
    return false;
  }

  // If this is true, the tool will disallow the pin cushion from spawning.
  virtual public bool BlockPinCushion() {
    return false;
  }

  // If this is true, the user can use the default tool toggle.
  virtual public bool AllowDefaultToolToggle() {
    return !PointerManager.m_Instance.IsMainPointerCreatingStroke();
  }

  virtual public void EnableRenderer(bool enable) {
    gameObject.SetActive(enable);
  }

  protected bool PointInTriangle(ref Vector3 rPoint, ref Vector3 rA, ref Vector3 rB, ref Vector3 rC) {
    if (SameSide(ref rPoint, ref rA, ref rB, ref rC) &&
       SameSide(ref rPoint, ref rB, ref rA, ref rC) &&
       SameSide(ref rPoint, ref rC, ref rA, ref rB)) {
      return true;
    }
    return false;
  }

  protected bool SameSide(ref Vector3 rPoint1, ref Vector3 rPoint2, ref Vector3 rA, ref Vector3 rB) {
    Vector3 vCross1 = Vector3.Cross(rB - rA, rPoint1 - rA);
    Vector3 vCross2 = Vector3.Cross(rB - rA, rPoint2 - rA);
    return (Vector3.Dot(vCross1, vCross2) >= 0);
  }

  protected bool SegmentSphereIntersection(Vector3 vSegA, Vector3 vSegB, Vector3 vSphereCenter, float fSphereRadSq) {
    //check segment start to sphere
    Vector3 vStartToSphere = vSphereCenter - vSegA;
    if (vStartToSphere.sqrMagnitude < fSphereRadSq) {
      return true;
    }

    //check to see if our ray is pointing in the right direction
    Vector3 vSegment = vSegB - vSegA;
    Ray segmentRay = new Ray(vSegA, vSegment.normalized);
    float fDistToCenterProj = Vector3.Dot(vStartToSphere, segmentRay.direction);
    if (fDistToCenterProj < 0.0f) {
      return false;
    }

    //if the distance to our projection is within the segment bounds, we're on the right track
    if (fDistToCenterProj * fDistToCenterProj > vSegment.sqrMagnitude) {
      return false;
    }

    //see if this projected point is within the sphere
    Vector3 vProjectedPoint = segmentRay.GetPoint(fDistToCenterProj);
    Vector3 vToProjectedPoint = vProjectedPoint - vSphereCenter;
    return vToProjectedPoint.sqrMagnitude <= fSphereRadSq;
  }
}
}  // namespace TiltBrush
