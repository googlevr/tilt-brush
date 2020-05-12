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

public class TutorialTossScript : MonoBehaviour {
  [SerializeField] private Transform m_ControllerAnchor;
  [SerializeField] private Transform m_GripGlowAnchor;
  [SerializeField] private Transform m_Panel;
  [SerializeField] private Vector3 m_RotationScalars;
  [SerializeField] private Vector3 m_TranslateScalars;
  [SerializeField] private float m_AnimationSpeed = 2.0f;
  [SerializeField] private float m_AnimationLength = 3.0f;
  [SerializeField] private float m_TranslatePoint = 1.0f;
  [SerializeField] private float m_ScalePoint = 1.5f;
  [SerializeField] private float m_ScaleEnd = 2.5f;

  private float m_AnimationValue;
  private Vector3 m_RotationBase;
  private Vector3 m_TranslateBase;
  private Vector3 m_PanelScaleBase;
  private Vector3 m_GripGlowScaleBase;

  void Start() {
    m_AnimationValue = 0.0f;
    m_RotationBase = m_ControllerAnchor.localRotation.eulerAngles;
    m_TranslateBase = m_Panel.localPosition;
    m_PanelScaleBase = m_Panel.localScale;
    m_GripGlowScaleBase = m_GripGlowAnchor.localScale;
  }

  void Update() {
    m_AnimationValue += Time.deltaTime * m_AnimationSpeed;
    if (m_AnimationValue > m_AnimationLength) {
      m_AnimationValue = 0.0f;
    }

    // Rotate the controller.
    float fRotateValue = Mathf.Min(m_AnimationValue, m_TranslatePoint);
    Vector3 vEulers = m_RotationBase + m_RotationScalars * fRotateValue;
    m_ControllerAnchor.localRotation = Quaternion.Euler(vEulers);

    // Toss the panel.
    float fTranslateValue = Mathf.Max(m_AnimationValue - m_TranslatePoint, 0.0f);
    Vector3 vTranslate = m_TranslateBase + m_TranslateScalars * fTranslateValue;
    m_Panel.localPosition = vTranslate;

    // Scale the panel.
    float fScaleValue = Mathf.Max(m_AnimationValue - m_ScalePoint, 0.0f);
    float fScaleRange = m_ScaleEnd - m_ScalePoint;
    float fScaleAmount = Mathf.Max(1.0f - (fScaleValue / fScaleRange), 0.0f);
    Vector3 vScale = m_PanelScaleBase * fScaleAmount;
    m_Panel.localScale = vScale;

    // Glow the grip while gripping.
    if (m_AnimationValue < m_TranslatePoint) {
      m_GripGlowAnchor.localScale = m_GripGlowScaleBase;
    } else {
      m_GripGlowAnchor.localScale = Vector3.zero;
    }
  }
}

} // namespace TiltBrush