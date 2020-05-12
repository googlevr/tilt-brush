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
using UnityEngine.Serialization;

namespace TiltBrush {

public class VrControllers : MonoBehaviour {
  [SerializeField] private ControllerBehaviorWand m_Wand;
  [SerializeField] private ControllerBehaviorBrush m_Brush;

  // Monobehaviours that must be disabled to disable pose tracking
  [SerializeField] private MonoBehaviour[] m_TrackingComponents;

  [Header("SteamVR Haptics")]
  [FormerlySerializedAs("m_HapticsScaleAdjustment")]
  [SerializeField] private float m_HapticsDurationScale = 1.0f;
  [SerializeField] private float m_HapticAmplitudeScale = 1.0f;

  [Header("Input Zones")]
  [SerializeField] private Vector2 m_TriggerActivationRange = new Vector2(0.15f, .8f);
  [SerializeField] private Vector2 m_GripActivationRange = new Vector2(0.15f, .8f);
  [SerializeField] private Vector2 m_TouchpadActivationRange = new Vector2(-.8f, .8f);
  [SerializeField] private Vector2 m_LogitechPenActivationRange = new Vector2(0.0f, 1.0f);
  [SerializeField] private float m_WandRotateJoystickPercent = 0.7f;

  // VR headsets (e.g., Rift, Vive, Wmr) use different hardware for their controllers,
  // they require a scaled duration for the haptics to be felt in users hand.
  public float HapticsDurationScale {
    get { return m_HapticsDurationScale; }
  }

  public float HapticsAmplitudeScale {
    get { return m_HapticAmplitudeScale; }
  }

  public BaseControllerBehavior GetBehavior(InputManager.ControllerName name) {
    switch (name) {
    case InputManager.ControllerName.Brush:
      return m_Brush;
    case InputManager.ControllerName.Wand:
      return m_Wand;
    default:
      throw new System.ArgumentException(
          string.Format("Unknown controller behavior {0}", name));
    }
  }

  public BaseControllerBehavior[] GetBehaviors() {
    Debug.Assert((int)m_Wand.ControllerName == 0);
    Debug.Assert((int)m_Brush.ControllerName == 1);
    // The array is indexed by ControllerName, so the order here is important!
    return new BaseControllerBehavior[] { m_Wand, m_Brush };
  }

  /// Normally all controllers are assumed to be the same style.
  /// Logitech switches one of them to be a pen.
  /// This returns the style that both controllers used before one of them was switched to a pen.
  public ControllerStyle BaseControllerStyle {
    get {
      if (m_Wand.ControllerGeometry.Style != ControllerStyle.LogitechPen) {
        return m_Wand.ControllerGeometry.Style;
      } else {
        return m_Brush.ControllerGeometry.Style;
      }
    }
  }

  public Vector2 TouchpadActivationRange {
    get { return m_TouchpadActivationRange; }
  }

  public float WandRotateJoystickPercent {
    get { return m_WandRotateJoystickPercent; }
  }

  /// The usable range of the raw grip value.
  /// This is currently only used as the threshold for analog -> boolean conversion.
  public Vector2 GripActivationRange {
    get { return m_GripActivationRange; }
  }

  public ControllerBehaviorWand Wand {
    get { return m_Wand; }
  }

  public ControllerBehaviorBrush Brush {
    get { return m_Brush; }
  }

  public bool LogitechPenIsPresent() {
    return (m_Brush.ControllerGeometry.Style == ControllerStyle.LogitechPen) ||
           (m_Wand.ControllerGeometry.Style == ControllerStyle.LogitechPen);
  }

  public bool PrimaryScrollDirectionIsX(InputManager.ControllerName name) {
    var behavior = GetBehavior(name);
    if (behavior.ControllerGeometry.Style == ControllerStyle.LogitechPen) {
      return false;
    }
    return true;
  }

  public Vector2 TriggerActivationRange(InputManager.ControllerName name) {
    var behavior = GetBehavior(name);
    if (behavior.ControllerGeometry.Style == ControllerStyle.LogitechPen) {
      return m_LogitechPenActivationRange;
    }
    return m_TriggerActivationRange;
  }

  /// Enable or disable tracking
  public void EnablePoseTracking(bool enabled) {
    foreach (var comp in m_TrackingComponents) {
      comp.enabled = enabled;
    }
  }
}

}  // namespace TiltBrush
