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

public enum WatermarkStyle {
  Standard,
  Labs,
  None
}

// Handles displaying watermark.
public class TiltBrushWatermark : MonoBehaviour {
  [SerializeField] private GameObject m_Watermark;
  [SerializeField] private GameObject m_WatermarkLabs;

  private WatermarkStyle m_WatermarkStyle;

  void Awake() {
    CameraConfig.WatermarkChanged += Refresh;
    Refresh();
  }

  void Refresh() {
    m_WatermarkStyle = WatermarkStyle.None;
    if (App.VrSdk.GetHmdDof() != VrSdk.DoF.None) {
      if (CameraConfig.Watermark && !App.Config.IsMobileHardware) {
        m_WatermarkStyle = WatermarkStyle.Standard;
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
        if (Config.IsExperimental) {
          m_WatermarkStyle = WatermarkStyle.Labs;
        }
#endif
      }
    }

    m_Watermark.SetActive(m_WatermarkStyle == WatermarkStyle.Standard);
    m_WatermarkLabs.SetActive(m_WatermarkStyle == WatermarkStyle.Labs);
    if (!App.Config.OfflineRender) {
      Debug.Assert(transform.parent.GetComponent<Canvas>() != null);
      transform.parent.gameObject.SetActive(m_WatermarkStyle != WatermarkStyle.None);
    }
  }
}
} // namespace TiltBrush
