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
public class TutorialSnapScript : MonoBehaviour {
  [SerializeField] GameObject m_Unsnapped;
  [SerializeField] GameObject m_Snapped;
  [SerializeField] float m_SwapTime;

  private float m_Time;
  private bool m_ShowingSnapped;

  void Update() {
    m_Time += Time.deltaTime;
    if (m_Time > m_SwapTime) {
      m_ShowingSnapped = !m_ShowingSnapped;
      if (m_ShowingSnapped) {
        m_Unsnapped.SetActive(false);
        m_Snapped.SetActive(true);
      } else {
        m_Unsnapped.SetActive(true);
        m_Snapped.SetActive(false);
      }
      m_Time = 0;
    }
  }
}
} // namespace TiltBrush
