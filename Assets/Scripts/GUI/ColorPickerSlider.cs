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

public class ColorPickerSlider : MonoBehaviour {
  private int m_Width;
  private int m_Height;
  private Texture2D m_sliderTex;
  private Color[] m_tempColors;

  [SerializeField]
  private Transform m_CurrentValueTransform;
  private Collider m_Collider;

  private ColorPickerMode m_LocalMode;

  public Collider GetCollider() { return m_Collider; }

  /// Range [0, 1]
  public float RawValue {
    get { return Mathf.Clamp01(m_CurrentValueTransform.localPosition.y + 0.5f); }
    set {
      m_CurrentValueTransform.localPosition = new Vector3(0, Mathf.Clamp01(value) - 0.5f, 0);
    }
  }

  public void SetLocalMode(ColorPickerMode mode) {
    m_LocalMode = mode;
  }

  void Awake() {
    m_Width = 1;
    m_Height = 128;
    m_tempColors = new Color[m_Width * m_Height];
    m_sliderTex = new Texture2D(m_Width, m_Height, TextureFormat.RGBAFloat, false);
    m_sliderTex.wrapMode = TextureWrapMode.Clamp;
    GetComponent<Renderer>().material.mainTexture = m_sliderTex;
    m_Collider = GetComponent<Collider>();
    CustomColorPaletteStorage.m_Instance.ModeChanged += OnModeChanged;
  }

  void OnDestroy() {
    CustomColorPaletteStorage.m_Instance.ModeChanged -= OnModeChanged;
  }

  public float GetValueFromHit(RaycastHit hit) {
    return Mathf.Clamp01(transform.InverseTransformPoint(hit.point).y + 0.5f);
  }

  void OnModeChanged() {
    switch (m_LocalMode) {
    case ColorPickerMode.SL_H_Triangle:
    case ColorPickerMode.SV_H_Rect:
      ColorPickerUtils.MakeRamp(m_LocalMode, m_Width, m_Height, m_tempColors);
      m_sliderTex.SetPixels(m_tempColors);
      m_sliderTex.Apply();
      break;
    case ColorPickerMode.HL_S_Polar:
    case ColorPickerMode.HS_L_Polar:
    case ColorPickerMode.HS_LogV_Polar:
      // Texture updates when color updates
      break;
    default:
      Debug.Assert(false);
      break;
    }
  }

  // Notification from sibling objects in the color picker UI that
  // the color has changed.
  // TODO: make this a no-op when color changes on the slider's axis?
  // Caller is responsible for guaranteeing that "mode+raw" maps to a valid color.
  public void OnColorChanged(ColorPickerMode mode, Vector3 raw) {
    ColorPickerUtils.MakeRamp(mode, m_Width, m_Height, m_tempColors, raw);
    m_sliderTex.SetPixels(m_tempColors);
    m_sliderTex.Apply();
  }

  public void SetTintColor(Color rColor) {
    GetComponent<Renderer>().material.SetColor("_Color", rColor);
    for (int i = 0; i < transform.childCount; ++i) {
      transform.GetChild(i).GetComponent<Renderer>().material.SetColor("_Color", rColor);
    }
  }
}
}  // namespace TiltBrush
