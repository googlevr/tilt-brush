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

using System;
using UnityEngine;

namespace TiltBrush {
// Color Controller is an unfortunate name for this class, as it refers to the MVC "Controller"
// for storing information about the app state regarding the current color.  It also
// maintains actions for objects to register with for status change notifications.
public class ColorController : MonoBehaviour {
  [SerializeField] protected Color m_DefaultColor;
  [SerializeField] protected bool m_Hdr;
  protected Color m_CurrentColor;

  public event Action<ColorPickerMode, Vector3> CurrentColorSet;

  public bool IsHdr { get { return m_Hdr; } }

  virtual public Color CurrentColor {
    get { return m_CurrentColor; }
    set {
      m_CurrentColor = value;
      var mode = ColorPickerUtils.GetActiveMode(m_Hdr);
      Vector3 raw = ColorPickerUtils.ColorToRawValue(mode, m_CurrentColor);
      TriggerCurrentColorSet(mode, raw);
    }
  }

  // This function is used by the ColorPicker to update our color without notifying those
  // that have registered with the CurrentColorSet action.  This is to prevent cyclical
  // behavior when the user is manipulating the ColorPickerSlider or ColorPickerSelector.
  virtual public void SetCurrentColorSilently(Color color) {
    m_CurrentColor = color;
  }

  protected void TriggerCurrentColorSet(ColorPickerMode mode, Vector3 rawColor) {
    if (CurrentColorSet != null) {
      CurrentColorSet(mode, rawColor);
    }
  }

  public void SetColorToDefault() {
    CurrentColor = m_DefaultColor;
  }
}

} // namespace TiltBrush