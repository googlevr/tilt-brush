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

  public class QualitySlider : BaseSlider {
    private float[] m_Steps;

    override protected void Awake() {
      base.Awake();

      //divide the slider in to steps
      int iNumQualitySettings = Mathf.Max(QualitySettings.names.Length, 2);
      float fStepInterval = 1.0f / (float)(iNumQualitySettings - 1);
      m_Steps = new float[iNumQualitySettings];
      for (int i = 0; i < iNumQualitySettings; ++i) {
        m_Steps[i] = i * fStepInterval;
      }

      //figure out where to initialize the position of the slider
      PositionNobAtCurrentQuality();
      SetDescriptionText(m_DescriptionText, GetDescriptionExtraText());
    }

    void PositionNobAtCurrentQuality() {
      int iCurrentQuality = QualitySettings.GetQualityLevel();
      QualityControls.m_Instance.QualityLevel = iCurrentQuality;
      Vector3 vLocalPos = m_Nob.transform.localPosition;
      vLocalPos.x = Mathf.Clamp(m_Steps[iCurrentQuality] - 0.5f, -0.5f, 0.5f) * m_MeshScale.x;
      m_Nob.transform.localPosition = vLocalPos;
    }

    override public void UpdateValue(float fValue) {
      //find nearest step
      float fNearestDistance = 999.0f;
      int iNearestIndex = 0;
      for (int i = 0; i < m_Steps.Length; ++i) {
        float fAbsDiff = Mathf.Abs(fValue - m_Steps[i]);
        if (fAbsDiff < fNearestDistance) {
          fNearestDistance = fAbsDiff;
          iNearestIndex = i;
        }
      }

      //switch quality setting if needed
      int iCurrentQuality = QualitySettings.GetQualityLevel();
      if (iNearestIndex != iCurrentQuality) {
        //only make one step at a time
        if (iNearestIndex < iCurrentQuality) {
          iNearestIndex = iCurrentQuality - 1;
        } else {
          iNearestIndex = iCurrentQuality + 1;
        }

        QualityControls.m_Instance.QualityLevel = iNearestIndex;
        SetDescriptionText(m_DescriptionText, GetDescriptionExtraText());
        AudioManager.m_Instance.PlaySliderSound(m_Nob.transform.position);
      }

      PositionNobAtCurrentQuality();
    }

    string GetDescriptionExtraText() {
      int iCurrentQuality = QualitySettings.GetQualityLevel();
      return QualitySettings.names[iCurrentQuality];
    }
  }
}  // namespace TiltBrush