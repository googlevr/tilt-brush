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
using Valve.VR;

namespace TiltBrush {

public class SteamControllerInfo : ControllerInfo {
  // Helper that binds to a particular frame (current or last) and a particular controller,
  // then lets us look up that input.
  private struct GetVrInputHelper {
    private readonly SteamVR_Input_Sources m_index;
    private readonly bool m_currentFrame;

    public GetVrInputHelper(SteamControllerInfo owner, bool currentFrame) {
      m_index = owner.TrackedPose.inputSource;
      m_currentFrame = currentFrame;
    }
    public bool Active<SourceMap, SourceElement>(SteamVR_Action<SourceMap, SourceElement> action)
        where  SourceMap : SteamVR_Action_Source_Map<SourceElement>, new()
        where SourceElement : SteamVR_Action_Source, new() {
      return action[m_index].active;
    }
    public bool State(SteamVR_Action_Boolean action) {
      return m_currentFrame ? action[m_index].state : action[m_index].lastState;
    }
    public float Axis(SteamVR_Action_Single action) {
      return m_currentFrame ? action[m_index].axis : action[m_index].lastAxis;
    }
    public Vector2 Axis(SteamVR_Action_Vector2 action) {
      return m_currentFrame ? action[m_index].axis : action[m_index].lastAxis;
    }
  }

  private const float kInputScrollScalar = 0.5f;
  private const float kWmrThumbstickDeadzone = 0.1f;
  private readonly float kThumbstickDeadzone = 0.075f;

  private float m_HapticsDurationScale;
  private float m_HapticsAmplitudeScale;

  public SteamVR_Behaviour_Pose TrackedPose { get; protected set; }

  public override bool IsTrackedObjectValid {
    get {
      if (TrackedPose != null) {
        return TrackedPose.isValid;
      }
      return false;
    }
    set {
      if (TrackedPose != null) {
        Debug.Assert(value == TrackedPose.isValid);
      }
    }
  }

  private int Index {
    get {
      if (TrackedPose != null) {
        return TrackedPose.GetDeviceIndex();
      }
      return (int)SteamVR_TrackedObject.EIndex.None;
    }
  }

  public SteamControllerInfo(BaseControllerBehavior behavior)
    : base(behavior) {
    TrackedPose = behavior.GetComponent<SteamVR_Behaviour_Pose>();
    m_HapticsDurationScale = App.VrSdk.VrControls.HapticsDurationScale;
    m_HapticsAmplitudeScale = App.VrSdk.VrControls.HapticsAmplitudeScale;
  }

  private bool IndexIsValid() {
    Debug.Assert(Index != (int)SteamVR_TrackedObject.EIndex.Hmd);
    return Index != (int)SteamVR_TrackedObject.EIndex.None;
  }

  public override float GetTriggerRatio() {
    if (!IndexIsValid()) { return 0; }
    Vector2 range = App.VrSdk.VrControls.TriggerActivationRange(Behavior.ControllerName);
    float value = SteamVR_Actions.TiltBrush.RI_Trigger[TrackedPose.inputSource].axis;
    return Mathf.Clamp01(Mathf.InverseLerp(range.x, range.y, value));
  }

  // Not filtered
  public override Vector2 GetPadValue() {
    if (!IndexIsValid()) { return Vector2.zero; }
    return SteamVR_Actions.TiltBrush.RI_PadDirectional[TrackedPose.inputSource].axis;
  }

  public override Vector2 GetThumbStickValue() {
    if (!IndexIsValid()) { return Vector2.zero; }
    Vector2 axis = SteamVR_Actions.TiltBrush.RI_Thumbstick[TrackedPose.inputSource].axis;
    return new Vector2(DeadStickMap(axis.x), DeadStickMap(axis.y));
  }

  public override Vector2 GetPadValueDelta() {
    if (!IndexIsValid()) { return Vector2.zero; }
    return new Vector2(GetScrollXDelta(), GetScrollYDelta());
  }

  public override float GetGripValue() {
    if (!IndexIsValid()) { return 0; }
    return SteamVR_Actions.TiltBrush.RI_GripAnalog[TrackedPose.inputSource].axis;
  }

  public override float GetTriggerValue() {
    if (!IndexIsValid()) { return 0; }
    Vector2 range = App.VrSdk.VrControls.TriggerActivationRange(Behavior.ControllerName);
    float value = SteamVR_Actions.TiltBrush.RI_Trigger[TrackedPose.inputSource].axis;
    return Mathf.Clamp01(Mathf.InverseLerp(range.x, range.y, value));
  }

  public override float GetScrollXDelta() {
    if (!IndexIsValid()) { return 0; }

    SteamVR_Input_ActionSet_TiltBrush tb = SteamVR_Actions.TiltBrush;
    SteamVR_Input_Sources index = TrackedPose.inputSource;

    // If thumbstick is bound, use that.  Otherwise, look at pad movement.
    // NOTE: Not all thumbsticks detect touch, which is why we don't reference it here.
    if (tb.RI_Thumbstick[index].active) {
      float value = tb.RI_Thumbstick[index].axis.x;
      if (Mathf.Abs(value) < kThumbstickDeadzone) {
        return 0f;
      } else {
        return value;
      }
    }

    if (tb.RI_PadDirectional[index].active && tb.RI_PadTouch[index].active) {
      // If we weren't active last frame, return 0 and set ourselves up for success next frame.
      if (!tb.RI_PadTouch[index].lastState) {
        return 0.0f;
      }

      // This clamping alleviates bounce-back effect as finger slides off the capacitive touch pad.
      Vector2 range = App.VrSdk.VrControls.TouchpadActivationRange;
      float xCurr = tb.RI_PadDirectional[index].axis.x;
      float xPrev = tb.RI_PadDirectional[index].lastAxis.x;
      xCurr = Mathf.Clamp(xCurr, range.x, range.y);
      xPrev = Mathf.Clamp(xPrev, range.x, range.y);
      return kInputScrollScalar * (xCurr - xPrev);
    }
    return 0.0f;
  }

  public override float GetScrollYDelta() {
    if (!IndexIsValid()) { return 0; }

    SteamVR_Input_ActionSet_TiltBrush tb = SteamVR_Actions.TiltBrush;
    SteamVR_Input_Sources index = TrackedPose.inputSource;

    // If thumbstick is bound, use that.  Otherwise, look at pad movement.
    if (tb.RI_Thumbstick[index].active && tb.RI_ThumbstickTouch[index].active) {
      if (tb.RI_ThumbstickTouch[index].state) {
        Vector2 range = App.VrSdk.VrControls.TouchpadActivationRange;
        return Mathf.Clamp(tb.RI_Thumbstick[index].axis.y,
            range.x,
            range.y);
      }
    }

    if (tb.RI_PadDirectional[index].active && tb.RI_PadTouch[index].active) {
      // If we weren't active last frame, return 0 and set ourselves up for success next frame.
      if (!tb.RI_PadTouch[index].lastState) {
        return 0.0f;
      }

      // This clamping alleviates bounce-back effect as finger slides off the capacitive touch pad.
      Vector2 range = App.VrSdk.VrControls.TouchpadActivationRange;
      float yCurr = tb.RI_PadDirectional[index].axis.y;
      float yPrev = tb.RI_PadDirectional[index].lastAxis.y;
      yCurr = Mathf.Clamp(yCurr, range.x, range.y);
      yPrev = Mathf.Clamp(yPrev, range.x, range.y);
      return kInputScrollScalar * (yCurr - yPrev);
    }
    return 0.0f;
  }

  public override bool GetVrInputTouch(VrInput input) {
    if (!IndexIsValid()) { return false; }
    var tb = SteamVR_Actions.TiltBrush;
    var index = TrackedPose.inputSource;

    switch (input) {
    case VrInput.Button01: // A,X aka Secondary
      return tb.RI_SecondaryButtonTouch[index].state;

    case VrInput.Button02: // B,Y aka Primary
      return tb.RI_PrimaryButtonTouch[index].state;

    // case VrInput.Button03 through Button06: TODO

    case VrInput.Directional:
      if (tb.RI_Thumbstick[index].active) {
        goto case VrInput.Thumbstick;
      } else {
        goto case VrInput.Touchpad;
      }

    case VrInput.Thumbstick:
      if (tb.RI_ThumbstickTouch[index].active) {
        return tb.RI_ThumbstickTouch[index].state;
      } else {
        return tb.RI_Thumbstick[index].axis.sqrMagnitude > kThumbstickDeadzone;
      }

    case VrInput.Touchpad:
      return tb.RI_PadTouch[index].state;

    default:
      return false;
    }
  }

  // WMR controller seems to have a value greater than 0f while in resting position.
  private float DeadStickMap(float x) {
    float deadZone = kWmrThumbstickDeadzone;
    float sign = Mathf.Sign(x);
    x = Mathf.Clamp01(Mathf.Abs(x) - deadZone);
    x /= (1f - deadZone);
    return x * sign;
  }

  private float PrimaryAxis(InputManager.ControllerName name, GetVrInputHelper helper,
      SteamVR_Action_Vector2 action) {
    bool scrollX = App.VrSdk.VrControls.PrimaryScrollDirectionIsX(Behavior.ControllerName);
    Vector2 axis = helper.Axis(action);
    return scrollX ? axis.x : axis.y;
  }

  private bool GetVrInputForFrame(VrInput input, bool currentFrame) {
    SteamVR_Input_ActionSet_TiltBrush tb = SteamVR_Actions.TiltBrush;
    var h = new GetVrInputHelper(this, currentFrame);

    switch (input) {
    case VrInput.Button01: {
        return (h.State(tb.RI_PadClick) &&
                PrimaryAxis(Behavior.ControllerName, h, tb.RI_PadDirectional) < 0.0f)
               || h.State(tb.RI_SecondaryButton);
      }
    case VrInput.Button02: {
        return (h.State(tb.RI_PadClick) &&
                PrimaryAxis(Behavior.ControllerName, h, tb.RI_PadDirectional) > 0.0f)
               || h.State(tb.RI_PrimaryButton); 
      }
    case VrInput.Button03:
      return (h.State(tb.RI_MenuButton) ||
              h.State(tb.RI_PrimaryButton));
    case VrInput.Button04:
      return (h.State(tb.RI_PadClick) ||
              h.State(tb.RI_SecondaryButton));
    case VrInput.Button05:
      return (h.State(tb.RI_PadClick) && h.Axis(tb.RI_PadDirectional).y > 0.0f)
             || h.State(tb.RI_PrimaryButton);
    case VrInput.Button06:
      return (h.State(tb.RI_PadClick) && h.Axis(tb.RI_PadDirectional).y < 0.0f)
             || h.State(tb.RI_SecondaryButton);
    case VrInput.Trigger: {
      Vector2 triggerRange = App.VrSdk.VrControls.TriggerActivationRange(Behavior.ControllerName);
      return h.Axis(tb.RI_Trigger) > triggerRange.x;
    }
    case VrInput.Grip:
      if (h.Active(tb.RI_GripBinary)) {
        return h.State(tb.RI_GripBinary);
      } else {
        // Fallback in case nobody's bound RI_GripBinary. RI_GripBinary is preferred,
        // since it handles hysteresis, is tunable by end-users, etc.
        // GripActivationRange.x is not a great threshold since it's defined as the minimum
        // useful value and therefore will be set rather light.
        Vector2 gripRange = App.VrSdk.VrControls.GripActivationRange;
        return h.Axis(tb.RI_GripAnalog) > gripRange.x;
      }
    case VrInput.Any:
      return false; // TODO
    case VrInput.Directional:
      if (h.Active(tb.RI_Thumbstick)) {
        goto case VrInput.Thumbstick;
      } else {
        goto case VrInput.Touchpad;
      }
    case VrInput.Thumbstick:
      return h.Axis(tb.RI_Thumbstick).sqrMagnitude > 0.0f;
    case VrInput.Touchpad:
      return h.State(tb.RI_PadTouch);
    }
    return false;
  }

  /// Returns the value of the specified button (level trigger).
  public override bool GetVrInput(VrInput input) {
    if (!IndexIsValid()) { return false; }
    return GetVrInputForFrame(input, currentFrame: true);
  }

  /// Returns true if the specified button was just pressed (rising-edge trigger).
  public override bool GetVrInputDown(VrInput input) {
    if (!IndexIsValid()) { return false; }
    bool last = GetVrInputForFrame(input, currentFrame: false);
    bool current = GetVrInputForFrame(input, currentFrame: true);
    return (!last && current);
  }

  public override void TriggerControllerHaptics(float seconds) {
    if (!IndexIsValid()) { return; }
    SteamVR_Input_ActionSet_TiltBrush tb = SteamVR_Actions.TiltBrush;
    SteamVR_Input_Sources index = TrackedPose.inputSource;
    float durationSeconds = seconds * m_HapticsDurationScale;
    tb.Haptic[index].Execute(0.0f, durationSeconds, 1.0f, m_HapticsAmplitudeScale);
  }
}

}  // namespace TiltBrush
