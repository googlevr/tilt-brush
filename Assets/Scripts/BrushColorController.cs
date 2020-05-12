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

public class BrushColorController : ColorController {
  private float m_BrushLuminanceMin;
  private float m_BrushSaturationMax;

  override public Color CurrentColor {
    get { return m_CurrentColor; }
    set { 
      m_CurrentColor = ColorPickerUtils.ClampLuminance(value, m_BrushLuminanceMin);
      PointerManager.m_Instance.PointerColor = CurrentColor;
      var mode = CustomColorPaletteStorage.m_Instance.Mode;
      Vector3 raw = ColorPickerUtils.ColorToRawValue(mode, m_CurrentColor);
      TriggerCurrentColorSet(mode, raw);
    }
  }

  public float BrushLuminanceMin { get { return m_BrushLuminanceMin; } }
  public float BrushSaturationMax { get { return m_BrushSaturationMax; } }

  override public void SetCurrentColorSilently(Color color) {
    base.SetCurrentColorSilently(color);
    PointerManager.m_Instance.PointerColor = CurrentColor;
  }

  void Start() {
    PointerManager.m_Instance.OnMainPointerBrushChange += OnMainPointerBrushChange;
  }

  void OnDestroy() {
    PointerManager.m_Instance.OnMainPointerBrushChange -= OnMainPointerBrushChange;
  }

  void OnMainPointerBrushChange(TiltBrush.BrushDescriptor brush) {
    // Clamp current color to constraints of selected brush and push result to pointer.
    m_BrushLuminanceMin = brush.m_ColorLuminanceMin;
    m_BrushSaturationMax = brush.m_ColorSaturationMax;

    // This only works for LDR colors. If we need HDR brush colors, we could do the
    // luminance clamping only for LDR (because if it's HDR then luminance is high).
    // The saturation validation can possibly be done using the "S" from HSV, which
    // does work properly in HDR, but that might involve re-tuning the brush values.
    HSLColor hsl = (HSLColor)CurrentColor;
    if (hsl.l < m_BrushLuminanceMin || hsl.s > m_BrushSaturationMax) {
      // Assign to ourselves to ensure correct bounds and trigger events.
      CurrentColor = m_CurrentColor;
    }
  }
}

}