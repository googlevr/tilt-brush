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

public class CustomColorPaletteStorage : MonoBehaviour {
  static public CustomColorPaletteStorage m_Instance;

  public struct StoredColor {
    public Color color;
    public bool valid;
  }

  [SerializeField] private ModeAndPickerInfo[] m_ModeToPickerInfo;
  [SerializeField] private int m_NumColors = 7;

  private StoredColor[] m_StoredColors;
  private ColorPickerMode m_Mode;

  public event Action StoredColorsChanged;

  /// Global change in ColorPickerMode
  /// User if responsbile for making sure this action triggers all IColorPickerOwner.ModeChanged
  /// actions. HDR color pickers should not send out ModeChanged events.
  public event Action ModeChanged;

  public ModeAndPickerInfo[] ModeToPickerInfo {
    get { return m_ModeToPickerInfo; }
  }

  public ColorPickerMode Mode {
    get { return m_Mode; }
    set {
      var info = ColorPickerUtils.GetInfoForMode(value);
      if (info != null && !info.hdr) {
        m_Mode = value;
        PlayerPrefs.SetInt("ColorMode", (int)m_Mode);
        if (ModeChanged != null) {
          ModeChanged();
        }
      }
    }
  }

  void Awake() {
    m_Instance = this;
    m_StoredColors = new StoredColor[m_NumColors];
    m_Mode = ColorPickerMode.HS_L_Polar;
  }

  void Start() {
    if (PlayerPrefs.HasKey("ColorMode")) {
      int mode = PlayerPrefs.GetInt("ColorMode");
      if (mode < (int)ColorPickerMode.NUM_MODES) {
        Mode = (ColorPickerMode)mode;
      }
    }
  }

  // This function has undefined results if RefreshStoredColors() hasn't been called.
  public int GetNumValidColors() {
    for (int i = 0; i < m_StoredColors.Length; ++i) {
      if (!m_StoredColors[i].valid) {
        return i;
      }
    }
    return m_StoredColors.Length;
  }

  public Color GetColor(int index) {
    Debug.Assert(index >= 0 && index < m_StoredColors.Length);
    return m_StoredColors[index].color;
  }

  public void SetColor(int index, Color col, bool colorsChanged = false) {
    Debug.Assert(index >= 0 && index < m_StoredColors.Length);
    m_StoredColors[index].color = col;
    m_StoredColors[index].valid = true;
    if (colorsChanged && StoredColorsChanged != null) {
      StoredColorsChanged();
    }
    LightsControlScript.m_Instance.AddColor(col);
  }

  public void ClearAllColors() {
    for (int i = 0; i < m_StoredColors.Length; i++) {
      m_StoredColors[i].valid = false;
    }
    if (StoredColorsChanged != null) {
      StoredColorsChanged();
    }
  }

  public void ClearColor(int index) {
    Debug.Assert(index >= 0 && index < m_StoredColors.Length);
    m_StoredColors[index].valid = false;
    if (StoredColorsChanged != null) {
      StoredColorsChanged();
    }
  }

  public Palette GetPaletteForSaving() {
    // Find the last valid color.
    int lastValid = -1;
    for (int i = 0; i < m_StoredColors.Length; ++i) {
      if (m_StoredColors[i].valid) {
        lastValid = i;
      }
    }

    if (lastValid < 0) {
      return null;
    }

    // Return a Palette that only includes valid colors.
    Palette pal = new Palette();
    pal.Colors = new Color32[lastValid + 1];
    for (int i = 0; i <= lastValid; ++i) {
      pal.Colors[i] = m_StoredColors[i].color;
    }

    return pal;
  }

  public void SetColorsFromPalette(Palette palette) {
    if (palette == null) { palette = new Palette(); }
    if (palette.Colors == null) { palette.Colors = new Color32[0]; }

    for (int i = 0; i < m_StoredColors.Length; ++i) {
      if (i < palette.Colors.Length) {
        m_StoredColors[i].color = palette.Colors[i];
        m_StoredColors[i].valid = true;
        LightsControlScript.m_Instance.AddColor(palette.Colors[i]);
      } else {
        m_StoredColors[i].valid = false;
      }
    }
    if (StoredColorsChanged != null) {
      StoredColorsChanged();
    }
  }

  public void RefreshStoredColors() {
    // Shift our valid colors down.
    for (int i = 0; i < m_StoredColors.Length; ++i) {
      if (!m_StoredColors[i].valid) {
        // Find the next set color and copy its guts in to us.
        int otherIndex = -1;
        for (int j = i + 1; j < m_StoredColors.Length; ++j) {
          if (m_StoredColors[j].valid) {
            otherIndex = j;
            break;
          }
        }

        if (otherIndex != -1) {
          m_StoredColors[i].color = m_StoredColors[otherIndex].color;
          m_StoredColors[i].valid = m_StoredColors[otherIndex].valid;

          // Turn off the other.
          m_StoredColors[otherIndex].valid = false;
        } else {
          // If we didn't find a valid future index, there's nothing left to do.
          break;
        }
      }
    }
  }
}

} // namespace TiltBrush
