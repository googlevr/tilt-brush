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

public class TutorialPointScript : MonoBehaviour {
  [SerializeField] private Transform m_BrushControllerAnchor;
  [SerializeField] private Transform m_WandControllerAnchor;
  [SerializeField] private Transform m_WandPanel;
  [SerializeField] private Renderer m_PointingLine;
  [SerializeField] private float m_RotationScalar = 30.0f;
  [SerializeField] private float m_TranslateScalar = 0.2f;
  [SerializeField] private float m_AnimationSpeed = 2.0f;
  [SerializeField] private float m_PanelScaleAmount = 1.25f;
  [SerializeField] private Vector2 m_PanelActivationRange;
  [SerializeField] private float m_PointingLineEmissionAmount = 5.0f;

  private float m_AnimationValue;
  private Vector3 m_TranslateBase;
  private Vector3 m_RotationBase;
  private Vector3 m_PanelScaleBase;

  void Start() {
    m_AnimationValue = 0.0f;
    m_TranslateBase = m_WandControllerAnchor.localPosition;
    m_RotationBase = m_BrushControllerAnchor.rotation.eulerAngles;
    m_PanelScaleBase = m_WandPanel.localScale;
    m_PointingLine.material.SetColor("_EmissionColor", Color.blue * m_PointingLineEmissionAmount);
  }

  void Update() {
    m_AnimationValue += Time.deltaTime * m_AnimationSpeed;
    float fSinValue = Mathf.Sin(m_AnimationValue);

    Vector3 vEulers = m_RotationBase;
    vEulers.x += fSinValue * m_RotationScalar;
    m_BrushControllerAnchor.rotation = Quaternion.Euler(vEulers);

    Vector3 vPosition = m_TranslateBase;
    vPosition.y += fSinValue * m_TranslateScalar;
    m_WandControllerAnchor.localPosition = vPosition;

    if (fSinValue > m_PanelActivationRange.x && fSinValue < m_PanelActivationRange.y) {
      m_WandPanel.localScale = m_PanelScaleBase * m_PanelScaleAmount;
      m_PointingLine.enabled = true;
    } else {
      m_WandPanel.localScale = m_PanelScaleBase;
      m_PointingLine.enabled = false;
    }
  }
}

} // namespace TiltBrush