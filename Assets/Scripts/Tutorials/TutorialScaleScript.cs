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
public class TutorialScaleScript : MonoBehaviour {
  [SerializeField] private Transform m_LeftController;
  [SerializeField] private Transform m_RightController;
  [SerializeField] private Transform m_Mesh;
  [SerializeField] private float m_StateDuration;
  private bool m_AnimatingIn;
  private float m_Timer;
  private Vector3 m_StartPos;
  private float m_StartSize;

  void Awake() {
    m_StartPos = m_RightController.transform.localPosition;
    m_StartSize = m_Mesh.transform.localScale.x;
  }

  void Update() {
    m_Timer += Time.deltaTime;
    if (m_Timer > m_StateDuration) {
      m_Timer = 0;
      m_AnimatingIn = !m_AnimatingIn;
    }
    if (m_AnimatingIn) {
      m_LeftController.localPosition =
        new Vector3(-1 * (m_StartPos.x + m_StartPos.x * (1 - m_Timer / m_StateDuration)),
          m_StartPos.y, m_StartPos.z);
      m_RightController.localPosition =
        new Vector3(m_StartPos.x + m_StartPos.x * (1 - m_Timer / m_StateDuration),
          m_StartPos.y, m_StartPos.z);
      m_Mesh.transform.localScale = (2 - m_Timer / m_StateDuration) * m_StartSize * Vector3.one;
    } else {
      m_LeftController.localPosition =
        new Vector3(-1 * (m_StartPos.x + m_StartPos.x * m_Timer / m_StateDuration),
          m_StartPos.y, m_StartPos.z);
      m_RightController.localPosition =
        new Vector3(m_StartPos.x + m_StartPos.x * m_Timer / m_StateDuration,
          m_StartPos.y, m_StartPos.z);
      m_Mesh.transform.localScale = (1 + m_Timer / m_StateDuration) * m_StartSize * Vector3.one;
    }
  }
}
} // namespace TiltBrush
