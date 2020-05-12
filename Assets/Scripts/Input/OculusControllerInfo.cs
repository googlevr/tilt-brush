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

using System.Collections;
using UnityEngine;

#if !OCULUS_SUPPORTED
  using OVRInput_Controller = System.Int32;
#else // !OCULUS_SUPPORTED
  using OVRInput_Controller = OVRInput.Controller;
#endif // OCULUS_SUPPORTED

namespace TiltBrush{

public class OculusControllerInfo : ControllerInfo {
  private bool m_IsValid = false;
  private Coroutine m_VibrationCoroutine = null;

  public override bool IsTrackedObjectValid { get { return m_IsValid; } set { m_IsValid = value; } }

  public OVRInput_Controller m_ControllerType = 0;

#if OCULUS_SUPPORTED
  public OculusControllerInfo(BaseControllerBehavior behavior, bool isLeftHand)
      : base(behavior) {
    m_ControllerType = isLeftHand ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
  }

  /// Updates IsTrackedObjectValid and Behavior.transform
  public void UpdatePosesAndValidity() {
    // OVRInput.Controller.Touch checks both (LTouch | RTouch)
    bool bothTouchControllersConnected =
        (OVRInput.GetConnectedControllers() & OVRInput.Controller.Touch)
        == OVRInput.Controller.Touch;
    OVRInput.Controller input = m_ControllerType;
    IsTrackedObjectValid = OVRInput.GetControllerOrientationTracked(input) ||
                           OVRInput.GetControllerPositionTracked(input) &&
                           bothTouchControllersConnected;
    Transform t = Behavior.transform;
    t.localRotation = OVRInput.GetLocalControllerRotation(input);
    t.localPosition = OVRInput.GetLocalControllerPosition(input);
  }

  // Was InputManager.GetTriggerRatio()
  public override float GetTriggerRatio() {
    return OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, m_ControllerType);
  }

  private OVRInput.Button MapVrInput(VrInput input) {
    switch (input) {
    case VrInput.Directional:
    case VrInput.Thumbstick:
    case VrInput.Touchpad:
      return OVRInput.Button.PrimaryThumbstick;

    case VrInput.Trigger:
      return OVRInput.Button.PrimaryIndexTrigger;

    case VrInput.Grip:
      return OVRInput.Button.PrimaryHandTrigger;

    case VrInput.Button04:
      return OVRInput.Button.One;

    case VrInput.Button01:
    case VrInput.Button06:
      // Pad_Left, Pad_Down, Full pad, (X,A)
      return OVRInput.Button.One;

    case VrInput.Button02:
    case VrInput.Button03:
    case VrInput.Button05:
      // Pad_Right, Pad_Up, Application button, (Y,B)
      return OVRInput.Button.Two;

    case VrInput.Any:
      return OVRInput.Button.One
           | OVRInput.Button.Two
           | OVRInput.Button.PrimaryThumbstick
           | OVRInput.Button.PrimaryIndexTrigger
           | OVRInput.Button.PrimaryHandTrigger
           ;
    }

    // Should never get here.
    return OVRInput.Button.None;
  }

  private OVRInput.Touch MapVrTouch(VrInput input) {
    switch (input) {
    case VrInput.Button01:
    case VrInput.Button04:
    case VrInput.Button06:
      return OVRInput.Touch.One;
    case VrInput.Button02:
    case VrInput.Button03:
    case VrInput.Button05:
      return OVRInput.Touch.Two;
    case VrInput.Directional:
    case VrInput.Thumbstick:
    case VrInput.Touchpad:
      return OVRInput.Touch.PrimaryThumbstick;
    case VrInput.Any:
      return OVRInput.Touch.One |
             OVRInput.Touch.Two |
             OVRInput.Touch.PrimaryThumbstick;
    default:
      Debug.Assert(false, string.Format("Invalid touch button enum: {0}", input.ToString()));
      return OVRInput.Touch.None;
    }
  }

  // Not filtered
  public override Vector2 GetPadValue() {
    return GetThumbStickValue();
  }

  // Not filtered
  public override Vector2 GetThumbStickValue() {
    return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, m_ControllerType);
  }

  public override Vector2 GetPadValueDelta() {
    return new Vector2(GetScrollXDelta(), GetScrollYDelta());
  }

  public override float GetGripValue() {
    // Raw value in [0, 1]
    return OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, m_ControllerType);
  }

  public override float GetTriggerValue() {
    return OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, m_ControllerType);
  }

  public override float GetScrollXDelta() {
    if (IsTrackedObjectValid) {
      return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, m_ControllerType).x;
    }
    return 0.0f;
  }

  public override float GetScrollYDelta() {
    if (IsTrackedObjectValid) {
      return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, m_ControllerType).y;
    }
    return 0.0f;
  }

  /// Returns the value of the specified button (level trigger).
  public override bool GetVrInput(VrInput input) {
    if (!m_IsValid) { return false; }

    switch (input) {
    case VrInput.Grip:
      // This is the old behavior of GetControllerGrip() which was merged into GetVrInput()
      return OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, m_ControllerType) >
             App.VrSdk.AnalogGripBinaryThreshold_Rift;
    default:
      return OVRInput.Get(MapVrInput(input), m_ControllerType);
    }
  }

  /// Returns true if the specified button was just pressed (rising-edge trigger).
  public override bool GetVrInputDown(VrInput input) {
    if (!m_IsValid) { return false; }
    return OVRInput.GetDown(MapVrInput(input), m_ControllerType);
  }

  public override bool GetVrInputTouch(VrInput input) {
    if (!m_IsValid) { return false; }
    return OVRInput.Get(MapVrTouch(input), m_ControllerType);
  }

  public override void TriggerControllerHaptics(float seconds) {
    if (m_VibrationCoroutine != null) {
      App.Instance.StopCoroutine(m_VibrationCoroutine);
    }

    App.Instance.StartCoroutine(DoVibration(m_ControllerType, seconds));
  }

  private IEnumerator DoVibration(OVRInput.Controller controller, float duration) {
    OVRInput.SetControllerVibration(1, App.VrSdk.VrControls.HapticsAmplitudeScale, controller);
    yield return new WaitForSeconds(App.VrSdk.VrControls.HapticsDurationScale * duration);
    OVRInput.SetControllerVibration(0, 0, controller);
  }

#else // OCULUS_SUPPORTED
  public OculusControllerInfo(BaseControllerBehavior behavior, bool isLeftHand)
      : base(behavior) {
  }
  public void UpdatePosesAndValidity() {
  }
  public override float GetTriggerRatio() {
    return 0;
  }
  public override Vector2 GetPadValue() {
    return GetThumbStickValue();
  }
  public override Vector2 GetThumbStickValue() {
    return Vector2.zero;
  }
  public override Vector2 GetPadValueDelta() {
    return new Vector2(GetScrollXDelta(), GetScrollYDelta());
  }
  public override float GetGripValue() {
    return 0;
  }
  public override float GetTriggerValue() {
    return 0;
  }
  public override float GetScrollXDelta() {
    return 0.0f;
  }
  public override float GetScrollYDelta() {
    return 0.0f;
  }
  public override bool GetVrInput(VrInput input) {
    return false;
  }
  public override bool GetVrInputDown(VrInput input) {
    return false;
  }
  public override bool GetVrInputTouch(VrInput input) {
    return false;
  }
  public override void TriggerControllerHaptics(float seconds) {
  }
  private IEnumerator DoVibration(OVRInput_Controller controller, float duration) {
    yield break;
  }
#endif // OCULUS_SUPPORTED

}

}  // namespace TiltBrush
