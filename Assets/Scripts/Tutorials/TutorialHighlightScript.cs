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
public class TutorialHighlightScript : MonoBehaviour {

  enum HighlightState {
    NoGrab,
    MoveIn,
    Activate,
    MoveOut
  }

  [SerializeField] private float m_StateDuration = 1f;
  [SerializeField] private GameObject m_Highlight;
  [SerializeField] private Transform m_Controller;
  [SerializeField] private Vector3 m_StartPos;
  [SerializeField] private Vector3 m_EndPos;
  private HighlightState m_State;
  private float m_Timer;

  void Update() {
    m_Timer += Time.deltaTime;
    if (m_Timer > m_StateDuration) {
      switch (m_State) {
      case HighlightState.NoGrab:
        m_State = HighlightState.MoveIn;
        break;
      case HighlightState.MoveIn:
        m_Highlight.SetActive(true);
        m_Controller.localPosition = m_EndPos;
        m_State = HighlightState.Activate;
        break;
      case HighlightState.Activate:
        m_Highlight.SetActive(false);
        m_State = HighlightState.MoveOut;
        break;
      case HighlightState.MoveOut:
        m_Controller.localPosition = m_StartPos;
        m_State = HighlightState.NoGrab;
        break;
      }
      m_Timer = 0;
    }
    switch (m_State) {
    case HighlightState.MoveIn:
      m_Controller.localPosition = Vector3.Lerp(m_StartPos, m_EndPos, m_Timer / m_StateDuration);
      break;
    case HighlightState.MoveOut:
      m_Controller.localPosition = Vector3.Lerp(m_EndPos, m_StartPos, m_Timer / m_StateDuration);
      break;
    }
  }
}
} // namespace TiltBrush
