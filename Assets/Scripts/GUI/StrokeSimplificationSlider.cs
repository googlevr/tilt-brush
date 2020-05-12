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

public class StrokeSimplificationSlider : BaseSlider {
  [SerializeField] private GameObject[] m_HideOnChange;
  [SerializeField] private GameObject[] m_ShowOnChange;
  [SerializeField] private float [] m_SimplificationAmounts;

  public struct Step {
    public float sliderRatio;
    public float simplification;
  }
  private Step[] m_Steps;
  private float m_ProposedSimplificationLevel;

  override protected void Awake() {
    base.Awake();

    // Divide the slider in to steps.
    float interval = 1.0f / (m_SimplificationAmounts.Length - 1);
    m_Steps = new Step[m_SimplificationAmounts.Length];
    for (int i = 0; i < m_SimplificationAmounts.Length - 1; ++i) {
      m_Steps[i].sliderRatio = i * interval;
      m_Steps[i].simplification = m_SimplificationAmounts[i];
    }
    m_Steps[m_SimplificationAmounts.Length - 1].sliderRatio = 1f;

    // Default to all the way up.
    PositionNobAtSimplificationLevel(0.0f);
    SetDescriptionText(m_DescriptionText,
                       GetDescriptionExtraText(m_SimplificationAmounts.Length - 1));
  }

  void PositionNobAtSimplificationLevel(float level) {
    int iNearestIndex = GetStepFromLevel(level);
    m_ProposedSimplificationLevel = level;
    Vector3 vLocalPos = m_Nob.transform.localPosition;
    vLocalPos.x =
        Mathf.Clamp(m_Steps[iNearestIndex].sliderRatio - 0.5f, -0.5f, 0.5f) * m_MeshScale.x;
    m_Nob.transform.localPosition = vLocalPos;
  }

  int GetStepFromLevel(float level) {
    float nearestDistance = 999.0f;
    int nearestIndex = 0;
    for (int i = 0; i < m_Steps.Length; ++i) {
      float fAbsDiff = Mathf.Abs(level - m_Steps[i].simplification);
      if (fAbsDiff < nearestDistance) {
        nearestDistance = fAbsDiff;
        nearestIndex = i;
      }
    }
    return nearestIndex;
  }

  private void ShowConfirmation(bool show) {
    foreach (var toHide in m_HideOnChange) {
      toHide.SetActive(!show);
    }
    foreach (var toShow in m_ShowOnChange) {
      toShow.SetActive(show);
    }

    // If we're disabling the confirmation, reset our description.
    if (!show) {
      int step = GetStepFromLevel(QualityControls.m_Instance.SimplificationLevel);
      SetDescriptionText(m_DescriptionText, GetDescriptionExtraText(step));
    }
  }

  override public void UpdateValue(float fValue) {
    // Find nearest step to the value passed in.
    float nearestDistance = 999.0f;
    int nearestStep = 0;
    for (int i = 0; i < m_Steps.Length; ++i) {
      float fAbsDiff = Mathf.Abs(fValue - m_Steps[i].sliderRatio);
      if (fAbsDiff < nearestDistance) {
        nearestDistance = fAbsDiff;
        nearestStep = i;
      }
    }

    // Switch setting if needed.
    int currentStep = GetStepFromLevel(m_ProposedSimplificationLevel);
    if (nearestStep != currentStep) {
      // Only make one step at a time.
      if (nearestStep < currentStep) {
        nearestStep = currentStep - 1;
      } else {
        nearestStep = currentStep + 1;
      }

      SetDescriptionText(m_DescriptionText, GetDescriptionExtraText(nearestStep));
      AudioManager.m_Instance.PlaySliderSound(m_Nob.transform.position);
    }

    m_ProposedSimplificationLevel = m_Steps[nearestStep].simplification;
    PositionNobAtSimplificationLevel(m_ProposedSimplificationLevel);
    ShowConfirmation(nearestStep !=
        GetStepFromLevel(QualityControls.m_Instance.SimplificationLevel));
  }

  private string GetDescriptionExtraText(int step) {
    if (step == m_SimplificationAmounts.Length - 1) {
      return "Original";
    }
    if (step == 0) {
      return "Mangled";
    }
    return string.Format("Level {0}", m_SimplificationAmounts.Length - step - 1);
  }

  [System.Reflection.Obfuscation(Exclude=true)]
  public void ApplySettings() {
    QualityControls.m_Instance.SimplificationLevel = m_ProposedSimplificationLevel;
    ShowConfirmation(false);
    StartCoroutine(
        OverlayManager.m_Instance.RunInCompositorWithProgress(
            OverlayType.LoadGeneric,
            SketchMemoryScript.m_Instance.RepaintCoroutine(),
            0.25f));
  }

  [System.Reflection.Obfuscation(Exclude=true)]
  public void CancelSettings() {
    ShowConfirmation(false);
    PositionNobAtSimplificationLevel(QualityControls.m_Instance.SimplificationLevel);
  }

  protected override void OnDisable() {
    base.OnDisable();
    ShowConfirmation(false);
  }

  private void OnEnable() {
    float simplificationLevel = QualityControls.m_Instance.SimplificationLevel;
    PositionNobAtSimplificationLevel(simplificationLevel);
    int step = GetStepFromLevel(simplificationLevel);
    SetDescriptionText(m_DescriptionText, GetDescriptionExtraText(step));
  }
}
}  // namespace TiltBrush
