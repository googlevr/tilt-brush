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

public class ControllerTutorialScript : MonoBehaviour {
  [SerializeField] private float m_HapticBuzzLength = 1.0f;
  [SerializeField] private int m_HapticPulses = 5;

  [SerializeField] private Material m_MeshMaterialInactive;
  [SerializeField] private Material m_TriggerMaterialInactive;

  private Material m_OriginalMeshMaterial;
  private Material m_OriginalTriggerMaterial;
  private BaseControllerBehavior m_BaseBehavior;
  private Renderer m_Mesh;
  private Renderer m_Trigger;

  void Awake() {
    InitCachedComponents();
  }

  void Start() {
    m_OriginalMeshMaterial = m_Mesh.material;
    m_OriginalTriggerMaterial = m_Trigger.material;
  }

  void InitCachedComponents() {
    if (m_BaseBehavior == null) {
      m_BaseBehavior = GetComponent<BaseControllerBehavior>();
      var controllerGeometry = m_BaseBehavior.ControllerGeometry;
      m_Mesh = controllerGeometry.MainMesh;
      m_Trigger = controllerGeometry.TriggerMesh;
    }
  }

  public void AssignControllerMaterials(InputManager.ControllerName controller) {
    InputManager.GetControllerGeometry(controller).ShowTutorialMode();
    InputManager.GetControllerGeometry(controller).PadEnabled = false;

    // Once the user needs to swipe, make sure the "rotate panels" icon is always present.
    switch (TutorialManager.m_Instance.IntroState) {
    case IntroTutorialState.WaitForSwipe:
    case IntroTutorialState.SwipeToUnlockPanels:
    case IntroTutorialState.ActivatePanels:
    case IntroTutorialState.DelayForPointToPanelHint:
    case IntroTutorialState.WaitForPanelInteract:
      if (controller == InputManager.ControllerName.Wand) {
        InputManager.Wand.Geometry.ShowRotatePanels();
      }
      break;
    }
  }

  public void Activate(bool bActivate) {
    if (bActivate) {
      //switch controller materials/ meshes to active
      if (m_Mesh != null) {
        m_Mesh.material = m_OriginalMeshMaterial;
      }
      if (m_Trigger != null) {
        m_Trigger.material = m_OriginalTriggerMaterial;
      }

      m_BaseBehavior.BuzzAndGlow(m_HapticBuzzLength, m_HapticPulses, m_HapticBuzzLength);

      //show tutorial
      m_BaseBehavior.ActivateHint(true);
    } else {
      //switch controller materials/ meshes to inactive
      if (m_Mesh != null) {
        m_Mesh.material = m_MeshMaterialInactive;
      }
      if (m_Trigger != null) {
        m_Trigger.material = m_TriggerMaterialInactive;
      }

      InitCachedComponents();

      //hide tutorial
      m_BaseBehavior.ActivateHint(false);
    }
  }

  public void DisableTutorialObject() {
    m_BaseBehavior.ActivateHint(false);
  }
}
}  // namespace TiltBrush
