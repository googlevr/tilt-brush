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
public class LightButton : BaseButton {
  public LightMode m_ButtonType;

  override protected void OnButtonPressed() {
    LightsPanel lightsParent = m_Manager.GetComponent<LightsPanel>();
    if (lightsParent) {
      lightsParent.ButtonPressed(m_ButtonType);
    }
  }

  override public void SetColor(Color rColor) {
    var color = Color.white;
    switch (m_ButtonType) {
    case LightMode.Ambient:
      color = RenderSettings.ambientLight;
      break;
    case LightMode.Shadow:
      color = App.Scene.GetLight((int)LightMode.Shadow).color;
      break;
      case LightMode.NoShadow:
      color = App.Scene.GetLight((int)LightMode.NoShadow).color;
      break;
    }

    Color mainColor = new Color(color.r * rColor.r, color.g * rColor.g,
        color.b * rColor.b, color.a * rColor.a);
    if (m_ButtonType == LightMode.Shadow || m_ButtonType == LightMode.NoShadow) {
      // If button is HDR, convert to LDR and set the LDR color as secondary color
      Color ldrColor = ColorPickerUtils.ClampColorIntensityToLdr(color);
      Color secondaryColor = new Color(ldrColor.r * rColor.r, ldrColor.g * rColor.g,
          ldrColor.b * rColor.b, ldrColor.a * rColor.a);
      SetHDRButtonColor(mainColor, secondaryColor);
    } else {
      base.SetColor(mainColor);
    }
  }
}
} // namespace TiltBrush
