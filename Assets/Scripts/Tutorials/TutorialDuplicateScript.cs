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
public class TutorialDuplicateScript : MonoBehaviour {
  [SerializeField] private MeshFilter m_Stroke;
  [SerializeField] private MeshFilter m_StrokeDuplicate;
  [SerializeField] private float m_SelectDuration;
  [SerializeField] private float m_DuplicateDuration;

  enum AnimationState {
    Selected,
    Duplicated,
  }

  private AnimationState m_AnimState;
  private float m_Timer;

  void Update() {
    switch (m_AnimState) {
    case AnimationState.Selected:
      m_StrokeDuplicate.gameObject.SetActive(false);
      App.Instance.SelectionEffect.RegisterMesh(m_Stroke);
      m_Timer += Time.deltaTime / m_SelectDuration;
      break;
    case AnimationState.Duplicated:
      m_StrokeDuplicate.gameObject.SetActive(true);
      App.Instance.SelectionEffect.RegisterMesh(m_StrokeDuplicate);
      m_Timer += Time.deltaTime / m_DuplicateDuration;
      break;
    }

    if (m_Timer >= 1) {
      m_Timer = 0;
      if (m_AnimState == AnimationState.Duplicated) {
        m_AnimState = AnimationState.Selected;
      } else {
        m_AnimState++;
      }
    }
  }
}
} // namespace TiltBrush
