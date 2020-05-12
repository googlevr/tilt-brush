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

/// <summary>
/// This controls the models displayed in Tutorial for Rift or Vive controllers
/// </summary>
public class TutorialControllerSwitcherScript : MonoBehaviour {
  [SerializeField] private GameObject[] m_ViveControllers;
  [SerializeField] private GameObject[] m_RiftControllers;
  [SerializeField] private GameObject[] m_WmrControllers;
  [SerializeField] private GameObject[] m_QuestControllers;
  [SerializeField] private GameObject[] m_KnucklesControllers;

  void Awake() {
    // Dodge the logitech pen.  That's not supported.
    ControllerStyle style = App.VrSdk.VrControls.BaseControllerStyle;

    // Default to all off.
    ActivateControllers(m_RiftControllers, false);
    ActivateControllers(m_ViveControllers, false);
    ActivateControllers(m_WmrControllers, false);
    ActivateControllers(m_QuestControllers, false);
    ActivateControllers(m_KnucklesControllers, false);

    // Enable whatever style is active.
    switch (style) {
    case ControllerStyle.OculusTouch:
      if (App.Config.VrHardware == VrHardware.Rift) {
        ActivateControllers(m_RiftControllers, true);
      } else if (App.Config.VrHardware == VrHardware.Quest) {
        // TODO(b/135950527): rift-s also uses quest controllers.
        ActivateControllers(m_QuestControllers, true);
      }
      break;
    case ControllerStyle.Wmr:
      ActivateControllers(m_WmrControllers, true);
      break;
    case ControllerStyle.Knuckles:
      ActivateControllers(m_KnucklesControllers, true);
      break;
    case ControllerStyle.Vive:
    default:
      ActivateControllers(m_ViveControllers, true);
      break;
    }
  }

  private void ActivateControllers(GameObject[] gameObjects, bool isActive) {
    for (int i = 0; i < gameObjects.Length; i++) {
      gameObjects[i].SetActive(isActive);
    }
  }
}
} // namespace TiltBrush
