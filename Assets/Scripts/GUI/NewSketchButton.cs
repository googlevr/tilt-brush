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
public class NewSketchButton : OptionButton {
  [SerializeField] private float m_AdjustDistanceAmount;
  [SerializeField] private Renderer m_NewSketchButtonBG;

  protected override void AdjustButtonPositionAndScale(
      float posAmount, float scaleAmount, float boxColliderGrowAmount) {
    SetMaterialFloat("_Distance", posAmount != 0 ? m_AdjustDistanceAmount : 0.0f);
    SetMaterialFloat("_Grayscale", posAmount != 0 ? 0.0f : 1.0f);
    m_NewSketchButtonBG.material.SetFloat("_Grayscale", posAmount != 0 ? 0.0f : 1.0f);
    base.AdjustButtonPositionAndScale(posAmount, scaleAmount, boxColliderGrowAmount);
  }

  protected override void OnButtonPressed() {
    if (!SketchControlsScript.m_Instance.SketchHasChanges()) {
      Vector3 vPos = PointerManager.m_Instance.MainPointer.transform.position;
      if (App.VrSdk.GetControllerDof() == VrSdk.DoF.Six) {
        vPos = InputManager.m_Instance.GetControllerPosition(
            InputManager.ControllerName.Wand);
      }
      AudioManager.m_Instance.PlayIntroTransitionSound(vPos);
    }

    PanelManager.m_Instance.ToggleSketchbookPanels();
    App.Instance.ExitIntroSketch();
    PromoManager.m_Instance.RequestAdvancedPanelsPromo();

    // Change the shown sketchset by simulating a press on the corresponding gallery button.
    SketchbookPanel panel = m_Manager.GetComponent<SketchbookPanel>();
    if (SketchCatalog.m_Instance.GetSet(SketchSetType.User).NumSketches == 0) {
      panel.ButtonPressed(GalleryButton.Type.Showcase);
    } else {
      panel.ButtonPressed(GalleryButton.Type.Local);
    }
  }
}
} // namespace TiltBrush
