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

/// This animates the Rift and Quest controllers.
/// It was originally used for SteamVR only; perhaps OVR used to have its own way of
/// animating grip and trigger that no longer exists (or works).
/// Thus this class is currently used for _both_ OVR and SteamVR.
public class AnimateOculusTouchSteam : MonoBehaviour {
  [SerializeField] private Animator m_animator = null;

  private ControllerGeometry m_geometry;

  public ControllerInfo Controller {
    get {
      if (m_geometry == null) { return null; }
      return m_geometry.ControllerInfo;
    }
  }

  private void Start() {
    // This is the base of our prefab and will never change
    m_geometry = GetComponentInParent<ControllerGeometry>();
    if (m_geometry == null) {
      Debug.LogWarning("Bad prefab: must be below a ControllerGeometry", this);
      enabled = false;
    }

    if (m_animator == null) {
      Debug.LogWarning("Bad prefab: must have an animator", this);
      enabled = false;
    }

    // Removed for b/134974904
    // TODO(for release 21): Remove this entirely
    // if (App.Config.m_SdkMode != SdkMode.SteamVR) {
    //   enabled = false;
    // }
  }

  private void Update() {
    if (m_animator != null) {  // animator should never be null any more
      ControllerInfo controller = Controller;

      // Animation
      m_animator.SetFloat("Button 1",
          controller.GetVrInput(VrInput.Button01) ? 1.0f : 0.0f);
      m_animator.SetFloat("Button 2",
          controller.GetVrInput(VrInput.Button02) ? 1.0f : 0.0f);
      Vector2 joyStick = controller.GetThumbStickValue();
      m_animator.SetFloat("Joy X", joyStick.x);
      m_animator.SetFloat("Joy Y", joyStick.y);
      m_animator.SetFloat("Grip", controller.GetGripValue());
      m_animator.SetFloat("Trigger", controller.GetTriggerValue());
    }
  }
}
} // namespace TiltBrush
