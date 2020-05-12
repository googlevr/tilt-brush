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
public class TutorialQuickToolScript : MonoBehaviour {
  [SerializeField] private GameObject m_QuickTool;
  [SerializeField] private GameObject m_Controller;
  [SerializeField] private GameObject m_PaintCircle;
  [SerializeField] private GameObject m_OtherCircle;
  [SerializeField] private float m_ShowHideSpeed;
  [SerializeField] private float m_MoveSpeed;
  [SerializeField] private float m_ToolPauseDuration;
  [SerializeField] private float m_ResetPauseDuration;
  [SerializeField] private Vector3 m_DefaultRotation;

  enum AnimationState {
    ShowQuickTool,
    PauseOnPaint,
    SwitchTool,
    PauseOnOther,
    SwitchBack,
    PauseBeforeClose,
    HideQuickTool,
    ResetPause
  }

  private AnimationState m_AnimState;
  private float m_Timer;
  private Vector3 m_SwitchPosition;
  private Quaternion m_SwitchRotation;

  void Awake() {
    m_SwitchPosition = m_Controller.transform.localPosition;
    m_SwitchRotation = m_Controller.transform.localRotation;
    m_OtherCircle.SetActive(false);
    m_Controller.transform.localPosition = Vector3.zero;
    m_Controller.transform.localRotation = Quaternion.Euler(m_DefaultRotation);
  }

  void Update() {
    switch (m_AnimState) {
    case AnimationState.ShowQuickTool:
      m_Timer += Time.deltaTime / m_ShowHideSpeed;
      m_QuickTool.transform.localScale = Mathf.Clamp01(m_Timer) * Vector3.one;
      break;
    case AnimationState.PauseOnPaint:
    case AnimationState.PauseBeforeClose:
      if (m_Timer == 0) {
        m_PaintCircle.SetActive(true);
        m_OtherCircle.SetActive(false);
      }
      m_Timer += Time.deltaTime / m_ToolPauseDuration;
      break;
    case AnimationState.SwitchTool:
      m_Timer += Time.deltaTime / m_MoveSpeed;
      m_Controller.transform.localPosition =
        Vector3.Lerp(Vector3.zero, m_SwitchPosition, Mathf.Clamp01(m_Timer));
      m_Controller.transform.localRotation = Quaternion.Lerp(
        Quaternion.Euler(m_DefaultRotation), m_SwitchRotation, Mathf.Clamp01(m_Timer));
      break;
    case AnimationState.PauseOnOther:
      if (m_Timer == 0) {
        m_PaintCircle.SetActive(false);
        m_OtherCircle.SetActive(true);
      }
      m_Timer += Time.deltaTime / m_ToolPauseDuration;
      break;
    case AnimationState.SwitchBack:
      m_Timer += Time.deltaTime / m_MoveSpeed;
      m_Controller.transform.localPosition =
        Vector3.Lerp(m_SwitchPosition, Vector3.zero, Mathf.Clamp01(m_Timer));
      m_Controller.transform.localRotation = Quaternion.Lerp(
        m_SwitchRotation, Quaternion.Euler(m_DefaultRotation), Mathf.Clamp01(m_Timer));
      break;
    case AnimationState.HideQuickTool:
      m_Timer += Time.deltaTime / m_ShowHideSpeed;
      m_QuickTool.transform.localScale = (1 - Mathf.Clamp01(m_Timer)) * Vector3.one;
      break;
    case AnimationState.ResetPause:
      m_Timer += Time.deltaTime / m_ResetPauseDuration;
      break;
    }

    if (m_Timer >= 1) {
      m_Timer = 0;
      if (m_AnimState == AnimationState.ResetPause) {
        m_AnimState = AnimationState.ShowQuickTool;
      } else {
        m_AnimState++;
      }
    }
  }
}
} // namespace TiltBrush
