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
public class BackdropButton : BaseButton {
  [SerializeField] private ColorMode m_ColorMode;
  [SerializeField] private GameObject [] m_ObjectsToHideBehindPopups;

  public enum ColorMode {
    SkyColorA,
    SkyColorB,
    Fog
  }

  void OnEnable() {
    if (m_ColorMode == ColorMode.Fog) {
      SceneSettings.m_Instance.FogColorChanged += OnColorChanged;
    } else {
      SceneSettings.m_Instance.SkyboxChanged += OnColorChanged;
    }
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    if (m_ColorMode == ColorMode.Fog) {
      SceneSettings.m_Instance.FogColorChanged -= OnColorChanged;
    } else {
      SceneSettings.m_Instance.SkyboxChanged -= OnColorChanged;
    }
  }

  override protected void OnButtonPressed() {
    for (int i = 0; i < m_ObjectsToHideBehindPopups.Length; ++i) {
      m_ObjectsToHideBehindPopups[i].SetActive(false);
    }

    var popupText = m_ColorMode == ColorMode.Fog ? "Fog" : "Skybox Color";
    BasePanel panel = m_Manager.GetPanelForPopUps();
    panel.CreatePopUp(SketchControlsScript.GlobalCommands.LightingLdr, -1, -1, popupText, OnPopUpClose);

    var popup = (panel.PanelPopUp as ColorPickerPopUpWindow);
    switch (m_ColorMode) {
    case ColorMode.SkyColorA:
      // Init must be called after all popup.ColorPicked actions have been assigned.
      popup.ColorPicker.ColorPicked += OnSkyAColorPicked;
      popup.ColorPicker.Controller.CurrentColor = SceneSettings.m_Instance.SkyColorA;
      popup.ColorPicker.ColorFinalized += SkyColorFinalized;
      popup.CustomColorPalette.ColorPicked += OnSkyColorAPickedAsFinal;
      break;
    case ColorMode.SkyColorB:
      popup.ColorPicker.ColorPicked += OnSkyBColorPicked;
      popup.ColorPicker.Controller.CurrentColor = SceneSettings.m_Instance.SkyColorB;
      popup.ColorPicker.ColorFinalized += SkyColorFinalized;
      popup.CustomColorPalette.ColorPicked += OnSkyColorBPickedAsFinal;
      break;
    case ColorMode.Fog:
      popup.ColorPicker.ColorPicked += OnFogColorPicked;
      popup.ColorPicker.Controller.CurrentColor = SceneSettings.m_Instance.FogColor;
      popup.ColorPicker.ColorFinalized += FogColorFinalized;
      popup.CustomColorPalette.ColorPicked += OnFogColorPickedAsFinal;
      break;
    }
  }

  void OnColorChanged() {
    BasePanel panel = m_Manager.GetPanelForPopUps();
    if (panel != null) {
      SetColor(panel.GetGazeColorFromActiveGazePercent());
    }
  }

  public override void SetColor(Color color) {
    Color modulation = SceneSettings.m_Instance.GetColor(m_ColorMode);
    color.r *= modulation.r;
    color.g *= modulation.g;
    color.b *= modulation.b;
    color.a *= modulation.a;
    base.SetColor(color);
  }

  override public void GazeRatioChanged(float gazeRatio) {
    GetComponent<Renderer>().material.SetFloat("_Distance", gazeRatio);
  }

  void OnPopUpClose() {
    for (int i = 0; i < m_ObjectsToHideBehindPopups.Length; ++i) {
      m_ObjectsToHideBehindPopups[i].SetActive(true);
    }
  }

  void OnSkyAColorPicked(Color color) {
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ModifySkyboxCommand(
        color, SceneSettings.m_Instance.SkyColorB, SceneSettings.m_Instance.GradientOrientation));
  }

  void OnSkyBColorPicked(Color color) {
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ModifySkyboxCommand(
        SceneSettings.m_Instance.SkyColorA, color, SceneSettings.m_Instance.GradientOrientation));
  }

  void SkyColorFinalized() {
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ModifySkyboxCommand(
        SceneSettings.m_Instance.SkyColorA, SceneSettings.m_Instance.SkyColorB,
        SceneSettings.m_Instance.GradientOrientation, final: true));
  }

  void OnSkyColorAPickedAsFinal(Color color) {
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ModifySkyboxCommand(
        color, SceneSettings.m_Instance.SkyColorB, SceneSettings.m_Instance.GradientOrientation,
        final: true));
  }

  void OnSkyColorBPickedAsFinal(Color color) {
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ModifySkyboxCommand(
        SceneSettings.m_Instance.SkyColorA, color, SceneSettings.m_Instance.GradientOrientation,
        final: true));
  }

  void OnFogColorPicked(Color color) {
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new ModifyFogCommand(color, SceneSettings.m_Instance.FogDensity));
  }

  void FogColorFinalized() {
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ModifyFogCommand(
        SceneSettings.m_Instance.FogColor, SceneSettings.m_Instance.FogDensity, final: true));
  }

  void OnFogColorPickedAsFinal(Color color) {
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new ModifyFogCommand(color, SceneSettings.m_Instance.FogDensity, final: true));
  }
}
} // namespace TiltBrush
