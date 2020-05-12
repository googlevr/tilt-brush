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

namespace TiltBrush{
public class GvrControllerInfo : ControllerInfo {
  static int sm_numControllers = 0;
  int m_controllerIndex;
  Vector2 m_lastTouchPos = Vector2.zero;
  bool m_isLeftHand;

  public GvrControllerInfo(BaseControllerBehavior behavior, bool isLeftHand) : base(behavior) {
    m_controllerIndex = sm_numControllers++;
    m_isLeftHand = isLeftHand;
  }

  public override void Update() {
    base.Update();
    if (GvrControllerInput.State == GvrConnectionState.Error) {
      Debug.LogError(GvrControllerInput.ErrorDetails);
    }
  }

  public override void LateUpdate() {
    base.LateUpdate();
    m_lastTouchPos = GetPadValue();
  }

  // -------------------------------------------------------------------------------------------- //
  // Virtual API
  // See base class for API documentation.
  // -------------------------------------------------------------------------------------------- //

  public override bool IsTrackedObjectValid {
    get { return m_controllerIndex == 0 || GvrControllerInput.ApiStatus == GvrControllerApiStatus.Ok; }
    set { }
  }

  // -------------------------------------------------------------------------------------------- //
  // Grip
  // -------------------------------------------------------------------------------------------- //
  public override float GetGripValue() {
    return GetControllerGrip() ? 1.0f : 0.0f;
  }

  // -------------------------------------------------------------------------------------------- //
  // Trigger
  // -------------------------------------------------------------------------------------------- //
  public override float GetTriggerRatio() {
    return IsTrigger() ? 1.0f : 0.0f;
  }

  public override float GetTriggerValue() {
    return GetTriggerButton() ? 1.0f : 0.0f;
  }

  // -------------------------------------------------------------------------------------------- //
  // TouchPad
  // -------------------------------------------------------------------------------------------- //

  // Not filtered
  public override Vector2 GetPadValue() {
    return GetTouchPos();
  }

  public override Vector2 GetThumbStickValue() {
    return Vector2.zero;
  }

  // Filtered
  public override Vector2 GetPadValueDelta() {
    if (!GetIsTouching()) {
      return Vector2.zero;
    }
    // When the user starts touching the touch pad, reset the last position so scroll deltas are
    // correct.
    if (GetTouchDown()) {
      m_lastTouchPos = GetTouchPos();
    }
    return GetTouchPos() - m_lastTouchPos;
  }

  public override float GetScrollXDelta() {
    return GetPadValueDelta().x;
  }

  public override float GetScrollYDelta() {
    return GetPadValueDelta().y;
  }

  // -------------------------------------------------------------------------------------------- //
  // Generic VR Input
  // -------------------------------------------------------------------------------------------- //
  public override bool GetVrInput(VrInput input) {
    switch (input) {
    case VrInput.Button03:
      return GetAppButton();
    case VrInput.Button01: /*half_left*/
    case VrInput.Button02: /*half_right*/
    case VrInput.Button05: /*quad_up*/
    case VrInput.Button06: /*quad_down*/
      return GetClickButton() && IsInPosition(GetTouchPos(), input);

    case VrInput.Button04: /*full-pad button*/
      return GetClickButton();

    case VrInput.Trigger:
      return GetTriggerButton();

    case VrInput.Grip:
      return m_isLeftHand
          ? GvrControllerInput.ClickButtonLeft && GvrControllerInput.TriggerButtonLeft
          : GvrControllerInput.ClickButtonRight && GvrControllerInput.TriggerButtonRight;

    case VrInput.Any:
      return GetTriggerButton() || GetAppButton() || GetClickButton();
    }
    return false;
  }

  public override bool GetVrInputDown(VrInput input) {
    switch (input) {
    case VrInput.Button03:
      return GetAppButtonDown();
    case VrInput.Button01: /*half_left*/
    case VrInput.Button02: /*half_right*/
    case VrInput.Button05: /*quad_up*/
    case VrInput.Button06: /*quad_down*/
      return GetClickButtonDown() && IsInPosition(GetTouchPos(), input);

    case VrInput.Button04: /*full-pad button*/
      return GetClickButtonDown();

    case VrInput.Trigger:
      return GetTriggerButtonDown();

    case VrInput.Any:
      return GetTriggerButtonDown() || GetAppButtonDown() || GetClickButtonDown();
    }
    return false;
  }

  public override bool GetVrInputTouch(VrInput input) {
    switch (input) {
    case VrInput.Button01: /*half_left*/
    case VrInput.Button02: /*half_right*/
    case VrInput.Button05: /*quad_up*/
    case VrInput.Button06: /*quad_down*/
      return GetIsTouching() && IsInPosition(GetTouchPos(), input);

    case VrInput.Touchpad:
    case VrInput.Directional:
      return GetIsTouching();

    case VrInput.Any:
      return GetIsTouching();

    case VrInput.Thumbstick:
      return false;
    default:
      return false;
    }
  }

  // -------------------------------------------------------------------------------------------- //
  // Private helpers
  // -------------------------------------------------------------------------------------------- //
  public override void TriggerControllerHaptics(float seconds) {
  }

  private bool GetTriggerButton() {
    if (GetControllerGrip()) { return false; }
    return m_isLeftHand ? GvrControllerInput.TriggerButtonLeft
                        : GvrControllerInput.TriggerButtonRight;
  }

  private bool GetTriggerButtonDown() {
    if (GetControllerGrip()) { return false; }
    return m_isLeftHand ? GvrControllerInput.TriggerButtonDownLeft
                        : GvrControllerInput.TriggerButtonDownRight;
  }

  private bool GetIsTouching() {
    return m_isLeftHand ? GvrControllerInput.IsTouchingLeft
                        : GvrControllerInput.IsTouchingRight;
  }

  private bool GetTouchDown() {
    return m_isLeftHand ? GvrControllerInput.TouchDownLeft
                        : GvrControllerInput.TouchDownRight;
  }

  private Vector2 GetTouchPos() {
    var value = m_isLeftHand ? GvrControllerInput.TouchPosLeft
                        : GvrControllerInput.TouchPosRight;
    return value * 2f - Vector2.one;
  }

  private bool GetClickButton() {
    if (GetControllerGrip()) { return false; }
    return m_isLeftHand ? GvrControllerInput.ClickButtonLeft
                        : GvrControllerInput.ClickButtonRight;
  }

  private bool GetClickButtonDown() {
    if (GetControllerGrip()) { return false; }
    return m_isLeftHand ? GvrControllerInput.ClickButtonDownLeft
                        : GvrControllerInput.ClickButtonDownRight;
  }

  private bool GetAppButton() {
    return m_isLeftHand ? GvrControllerInput.AppButtonLeft
                        : GvrControllerInput.AppButtonRight;
  }

  private bool GetAppButtonDown() {
    return m_isLeftHand ? GvrControllerInput.AppButtonDownLeft
                        : GvrControllerInput.AppButtonDownRight;
  }
}
} // namespace TiltBrush
