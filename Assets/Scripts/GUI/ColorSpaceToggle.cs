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

using System.Collections.Generic;

namespace TiltBrush {
public class ColorSpaceToggle : MultistateButton {
  override protected void Awake() {
    base.Awake();
    CustomColorPaletteStorage.m_Instance.ModeChanged += OnModeChanged;
  }

  override protected void OnStart() {
    base.OnStart();
    RefreshOptions();
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    CustomColorPaletteStorage.m_Instance.ModeChanged -= OnModeChanged;
  }

  public void RefreshOptions() {
    var pickers = App.Instance.GetComponent<CustomColorPaletteStorage>();
    List<Option> options = new List<Option>();
    foreach (var modeAndInfo in pickers.ModeToPickerInfo) {
      if (ColorPickerUtils.ModeIsValid(modeAndInfo.mode)) {
        options.Add(new Option {
            m_Description = modeAndInfo.mode.ToString(),
            m_Texture = modeAndInfo.info.icon
        });
      }
    }

    m_Options = options.ToArray();
    if (m_Options.Length < 2) {
      gameObject.SetActive(false);
    } else {
      CreateOptionSides();
      ColorPickerMode initialMode = CustomColorPaletteStorage.m_Instance.Mode;
      if (ColorPickerUtils.ModeIsValid(initialMode)) {
        ForceSelectedOption((int)initialMode);
      } else {
        ColorPickerUtils.GoToNextMode();
      }
    }
  }

  override protected void OnButtonPressed() {
    ColorPickerUtils.GoToNextMode();
  }

  void OnModeChanged() {
    ColorPickerInfo info =
        ColorPickerUtils.GetInfoForMode(CustomColorPaletteStorage.m_Instance.Mode);
    if (!m_AtlasTexture) {
      m_ButtonRenderer.material.mainTexture = info.icon;
    }
    SetSelectedOption((int)CustomColorPaletteStorage.m_Instance.Mode);
  }
}
}  // namespace TiltBrush
