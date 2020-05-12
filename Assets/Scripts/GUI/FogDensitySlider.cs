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
using System;

namespace TiltBrush {
public class FogDensitySlider : BaseSlider {
  [SerializeField] private Renderer m_FogDensity;
  [SerializeField] private float m_MaxFogDensity = 0.04f;

  void OnEnable() {
    SceneSettings.m_Instance.FogDensityChanged += OnFogDensityChanged;
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    SceneSettings.m_Instance.FogDensityChanged -= OnFogDensityChanged;
  }

  override public void UpdateValue(float value) {
    base.UpdateValue(value);
    SetSliderPositionToReflectValue();
    m_FogDensity.material.SetFloat("_FogDensity", value);

    SetDescriptionText(m_DescriptionText, String.Format("{0:0}%", value * 100));
  }

  override protected void OnPositionSliderNobUpdated() {
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new ModifyFogCommand(SceneSettings.m_Instance.FogColor,
        GetCurrentValue() * m_MaxFogDensity));
  }

  override public void ButtonReleased() {
    base.ButtonReleased();
    EndModifyCommand();
  }

  override public void ResetState() {
    if (m_HadButtonPress) {
      EndModifyCommand();
    }
    base.ResetState();
  }

  void OnFogDensityChanged() {
    UpdateValue(SceneSettings.m_Instance.FogDensity / m_MaxFogDensity);
  }

  void EndModifyCommand() {
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new ModifyFogCommand(SceneSettings.m_Instance.FogColor,
        SceneSettings.m_Instance.FogDensity, final: true));
  }
}
}  // namespace TiltBrush
