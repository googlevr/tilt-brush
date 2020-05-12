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

[System.Serializable]
public struct AdHocRigidAttachment {
  public Transform parent;
  public Transform child;
  public Vector3 localPosition;
  // public Quaternion rotation; No need for non-identity rotation at the moment

  public void Apply() {
    child.position = parent.position + parent.rotation * localPosition;
    child.rotation = parent.rotation;
  }
}

public class SaveIconTool : BaseTool {
  /// This is more complex than just a simple pos+rot because the
  /// camera auto-orient joint may have roll. This roll needs to
  /// be preserved when saving and restoring the rig state, because
  /// roll values of 90 and 270 use alternate aspect ratios.
  public struct CameraRigState {
    public Vector3 vRigPosition;
    public Quaternion qRigRotation;
    public Quaternion qAutoOrientJointLocalRotation;

    /// Get/SetLossy don't recreate portrait-vs-landscape, but they're
    /// only used for edge cases where we don't really care.
    public void SetLossyTransform(Vector3 pos, Quaternion rot) {
      vRigPosition = pos;
      qRigRotation = rot;
      qAutoOrientJointLocalRotation = Quaternion.identity;
    }

    public void SetLossyTransform(TrTransform transform) {
      vRigPosition = transform.translation;
      qRigRotation = transform.rotation;
      qAutoOrientJointLocalRotation = Quaternion.identity;
    }

    public void GetLossyTransform(out Vector3 pos, out Quaternion rot) {
      pos = vRigPosition;
      rot = qRigRotation * qAutoOrientJointLocalRotation;
    }

    /// Note that camera rig has no concept of scale, so scale is always 1.
    public TrTransform GetLossyTrTransform() {
      TrTransform trTransform = TrTransform.identity;
      GetLossyTransform(out trTransform.translation, out trTransform.rotation);
      return trTransform;
    }
  }

  /// So we can show/hide the screen
  [SerializeField] private GameObject m_CameraScreen;

  /// Don't use Unity to attach the camera into our hierarchy.
  /// There are issues with SteamVR moving the tool after
  /// UpdateTransformsFromControllers (causing jitter).
  /// Also we might want the rig's gameObject to stay enabled even after
  /// we become disabled.
  ///
  /// Both SaveIconTool and the ScreenshotManager want the camera rig to be:
  /// <rig root>
  ///   \_ "AutoOrientJoint" (identity transform)
  ///         \_ Camera (identity transform)
  [SerializeField] private AdHocRigidAttachment m_CameraRigAttachment;

  /// The ScreenshotManager, found somewhere in the camera rig's hierarchy
  [SerializeField] private ScreenshotManager m_SaveIconScreenshotManager;

  private bool m_LockToController;
  private Transform m_CameraController;

  [SerializeField]
  [Range(RenderWrapper.SSAA_MIN, RenderWrapper.SSAA_MAX)]
  private float m_superSampling = 1.0f;

  public enum State {
    Enter,
    WaitingForPicture,
    Off
  }
  private State m_CurrentState;
  private float m_EnterAmount;
  private Vector3 m_BaseScale;
  [SerializeField] private float m_EnterSpeed = 8.0f;

  [SerializeField] private int m_HapticCountTotal;
  [SerializeField] private float m_HapticInterval;
  private int m_HapticCounterWand;
  private float m_HapticTimerWand;
  private int m_HapticCounterBrush;
  private float m_HapticTimerBrush;
  private float m_HapticBuzzLength = 0.01f;

  private Transform m_CameraRigAutoOrientJoint;
  private CameraRigState m_LastSaveCameraRigState;

  // [SerializeField] private AudioClip m_IconAnimateComplete; was SaveIcon_Hit

  public ScreenshotManager ScreenshotManager {
    get {
      return m_SaveIconScreenshotManager;
    }
  }

  /// The camera rig is a root object that is safe to arbitrarily
  /// move and rotate.
  public Transform CameraRig {
    get {
      return m_CameraRigAttachment.child;
    }
  }

  public CameraRigState LastSaveCameraRigState {
    get {
      return m_LastSaveCameraRigState;
    }
  }

  /// Save and restore the position of the camera rig.
  /// See comments on CameraRigState.
  public CameraRigState CurrentCameraRigState {
    get {
      return new CameraRigState {
        vRigPosition = CameraRig.position,
        qRigRotation = CameraRig.rotation,
        qAutoOrientJointLocalRotation = m_CameraRigAutoOrientJoint.localRotation
      };
    }
    set {
      CameraRig.position = value.vRigPosition;
      CameraRig.rotation = value.qRigRotation;
      m_CameraRigAutoOrientJoint.localRotation = value.qAutoOrientJointLocalRotation;
    }
  }

  override public void Init() {
    base.Init();

    // This should be the joint right below the rig root
    m_CameraRigAutoOrientJoint = CameraRig.Find("AutoOrientJoint");
    Debug.Assert(m_CameraRigAutoOrientJoint != null);

    m_BaseScale = transform.localScale;
    m_CurrentState = State.Off;
    m_EnterAmount = 0.0f;
    m_HapticCounterWand = m_HapticCountTotal;
    m_HapticCounterBrush = m_HapticCountTotal;

    CameraRig.gameObject.SetActive(false);

    PromoManager.m_Instance.RequestPromo(PromoType.SaveIcon);
  }

  override public void HideTool(bool bHide) {
    base.HideTool(bHide);
    m_CameraScreen.SetActive(!bHide);
  }

  override public void EnableTool(bool bEnable) {
    base.EnableTool(bEnable);

    CameraRig.gameObject.SetActive(bEnable);

    //initialize to zeroed out
    m_EnterAmount = 0.0f;
    UpdateToolScale();

    SketchControlsScript.m_Instance.ForcePanelActivation(false);

    //eat up some input when we're enabled
    if (bEnable) {
      m_LockToController = m_SketchSurface.IsInFreePaintMode();
      if (m_LockToController) {
        m_CameraController = InputManager.Brush.Geometry.CameraAttachPoint;
      }
      EatInput();
      m_CurrentState = State.Enter;
    } else {
      m_CurrentState = State.Off;
    }

    //make sure our UI reticle isn't active
    SketchControlsScript.m_Instance.ForceShowUIReticle(false);
  }

  override protected void OnSwap() {
    if (m_LockToController) {
      m_CameraController = InputManager.Brush.Geometry.CameraAttachPoint;
    }
  }

  override public void AssignControllerMaterials(InputManager.ControllerName controller) {
    InputManager.Brush.Geometry.ToggleCancelOnly(enabled:true, enableFillTimer: false);
  }

  override public void UpdateTool() {
    base.UpdateTool();

    bool bCanTakePicture = m_CurrentState == State.WaitingForPicture;
    if (bCanTakePicture && !m_EatInput && !m_ToolHidden &&
        InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate)) {
      //snapshot the current scene and push it to the preview window
      RenderWrapper wrapper = m_SaveIconScreenshotManager.gameObject.GetComponent<RenderWrapper>();
      float ssaaRestore = wrapper.SuperSampling;
      wrapper.SuperSampling = m_superSampling;
      m_SaveIconScreenshotManager.RenderToTexture(
        SaveLoadScript.m_Instance.GetSaveIconRenderTexture());
      wrapper.SuperSampling = ssaaRestore;

      // save off camera transform from the position we took the snapshot
      m_LastSaveCameraRigState = CurrentCameraRigState;

      AudioManager.m_Instance.PlaySaveSound(InputManager.Brush.m_Position);
      SketchControlsScript.m_Instance.IssueGlobalCommand(SketchControlsScript.GlobalCommands.SaveNew);

      m_CurrentState = State.Off;
      m_RequestExit = true;
    } else if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.MenuContextClick)) {
      m_RequestExit = true;
    }

    //repeated buzz on wand controller to notify user the dialog has popped up
    if (m_HapticCounterWand < m_HapticCountTotal) {
      m_HapticTimerWand -= Time.deltaTime;
      if (m_HapticTimerWand <= 0.0f) {
        ++m_HapticCounterWand;

        m_HapticTimerWand = m_HapticInterval;
        InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Wand, m_HapticBuzzLength);
      }
    }
    if (m_HapticCounterBrush < m_HapticCountTotal) {
      m_HapticTimerBrush -= Time.deltaTime;
      if (m_HapticTimerBrush <= 0.0f) {
        ++m_HapticCounterBrush;

        m_HapticTimerBrush = m_HapticInterval;
        InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, m_HapticBuzzLength);
      }
    }
  }

  override public bool LockPointerToSketchSurface() {
    return false;
  }

  void Update() {
    switch (m_CurrentState) {
    case State.Enter:
      m_EnterAmount += (m_EnterSpeed * Time.deltaTime);
      if (m_EnterAmount >= 1.0f) {
        m_EnterAmount = 1.0f;
        m_CurrentState = State.WaitingForPicture;
        m_HapticCounterBrush = 0;
      }
      UpdateToolScale();
      break;
    default:
    case State.WaitingForPicture:
    case State.Off: break;
    }

    // If we're not locking to a controller, update our transforms now, instead of in LateUpdate.
    if (!m_LockToController) {
      UpdateTransformsFromControllers();
    }
  }

  override public void LateUpdateTool() {
    base.LateUpdateTool();
    UpdateTransformsFromControllers();
  }

  /// Called by code that captures save icons without user intervention.
  public void ProgrammaticCaptureSaveIcon(Vector3 pos, Quaternion rot) {
    bool wasActive = CameraRig.gameObject.activeSelf;
    CameraRig.gameObject.SetActive(true);
    var state = new SaveIconTool.CameraRigState();
    state.SetLossyTransform(pos, rot);

    var prev = CurrentCameraRigState;
    CurrentCameraRigState = state;

    // TODO XXX: Why is this Render() necessary?
    m_SaveIconScreenshotManager.LeftEye.Render();

    //snapshot the current scene and push it to the preview window
    RenderWrapper wrapper = m_SaveIconScreenshotManager.gameObject.GetComponent<RenderWrapper>();
    float ssaaRestore = wrapper.SuperSampling;
    wrapper.SuperSampling = m_superSampling;
    m_SaveIconScreenshotManager.RenderToTexture(
      SaveLoadScript.m_Instance.GetSaveIconRenderTexture());
    // save off camera transform from the position we took the snapshot
    wrapper.SuperSampling = ssaaRestore;
    m_LastSaveCameraRigState = CurrentCameraRigState;

    CurrentCameraRigState = prev;
    CameraRig.gameObject.SetActive(wasActive);
  }

  private void UpdateTransformsFromControllers() {
    // Lock tool to camera controller.
    if (m_LockToController) {
      transform.position = m_CameraController.position;
      transform.rotation = m_CameraController.rotation;
    } else {
      transform.position = SketchSurfacePanel.m_Instance.transform.position;
      transform.rotation = SketchSurfacePanel.m_Instance.transform.rotation;
    }

    m_CameraRigAttachment.Apply();
  }

  Vector3 TransformOffset(Transform rBase, Vector3 vOffset) {
    Vector3 vTransformedOffset = rBase.rotation * vOffset;
    return rBase.position + vTransformedOffset;
  }

  void UpdateToolScale() {
    Vector3 vScale = m_BaseScale;
    vScale.x *= m_EnterAmount;
    transform.localScale = vScale;
  }

  override public bool AllowsWidgetManipulation() {
    return false;
  }

  override public bool HidePanels() {
    return true;
  }

  override public bool InputBlocked() {
    return true;
  }

  override public bool AllowWorldTransformation() {
    return false;
  }

  override public bool AllowDefaultToolToggle() {
    return false;
  }
}
}  // namespace TiltBrush
