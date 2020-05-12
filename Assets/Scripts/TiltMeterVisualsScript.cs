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

public class TiltMeterVisualsScript : MonoBehaviour {
  [SerializeField] private Transform m_MeterBarTransform;
  [SerializeField] private Renderer m_MeterBGRenderer;
  [SerializeField] private TextMesh m_CurrentPerformanceLevel;
  [SerializeField] private float m_UpdateInterval = 1.0f;
  private float m_UpdateTimer;

  void Update() {
    m_UpdateTimer -= Time.deltaTime;
    if (m_UpdateTimer <= 0.0f) {
      float meterRatio = 0.0f;
      Renderer meter = m_MeterBarTransform.GetComponent<Renderer>();

      if (App.Config.IsMobileHardware) {
        meterRatio = Mathf.Clamp01(SketchMemoryScript.m_Instance.MemoryExceededRatio);

        meter.material.color = TiltMeterScript.m_Instance.GetMeterColorAbsolute(meterRatio);
        m_MeterBGRenderer.material.color = Color.grey * 0.5f;

        m_CurrentPerformanceLevel.text = (meterRatio >= 1.0f) ?
            "Over Memory Limit!" : "Memory Usage";
      } else {
        meterRatio = TiltMeterScript.m_Instance.GetMeterFullRatio();

        // Update meter colors and text.
        meter.material.color = TiltMeterScript.m_Instance.GetMeterColor();
        m_MeterBGRenderer.material.color = TiltMeterScript.m_Instance.GetMeterBGColor();

        // Set the meter description from state.
        m_CurrentPerformanceLevel.text = TiltMeterScript.m_Instance.GetMeterText();
      }

      // Update meter scale and position.
      float fBaseScale = m_MeterBGRenderer.transform.localScale.z;
      float fScale = fBaseScale * meterRatio;

      Vector3 vMeterScale = m_MeterBarTransform.localScale;
      vMeterScale.z = fScale;
      m_MeterBarTransform.localScale = vMeterScale;

      float fLocalZPosition = fScale * 0.5f;
      Vector3 vMeterLocalPos = m_MeterBarTransform.localPosition;
      vMeterLocalPos.z = (fBaseScale * -0.5f) + fLocalZPosition;
      m_MeterBarTransform.localPosition = vMeterLocalPos;

      m_UpdateTimer += m_UpdateInterval;
    }
  }
}
}  // namespace TiltBrush
