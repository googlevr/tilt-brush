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
public class UnlockSkyboxButton : BaseButton {
  [SerializeField] private GameObject[] m_HideOnPress;
  [SerializeField] private GameObject[] m_ShowOnPress;
  [SerializeField] private float m_TransitionDuration = 0.1f;
  [SerializeField] private GameObject m_Title;
  private bool m_TransitionToUnlockedSkybox;
  private float m_TransitionTimer;
  private float[] m_HideStartSizes;
  private float[] m_ShowEndSizes;

  override protected void Awake() {
    base.Awake();
    SceneSettings.m_Instance.GradientActiveChanged += OnGradientActiveChanged;

    m_TransitionToUnlockedSkybox = SceneSettings.m_Instance.InGradient;
    if (m_TransitionToUnlockedSkybox) {
      m_TransitionTimer = m_TransitionDuration;
    } else {
      m_TransitionTimer = 0.0f;
    }

    // Cache sizes for animation.
    m_HideStartSizes = new float[m_HideOnPress.Length];
    m_ShowEndSizes = new float[m_ShowOnPress.Length];
    for (int i = 0; i < m_HideOnPress.Length; i++) {
      m_HideStartSizes[i] = m_HideOnPress[i].transform.localScale.x;
    }
    for (int i = 0; i < m_ShowOnPress.Length; i++) {
      m_ShowEndSizes[i] = m_ShowOnPress[i].transform.localScale.x;
    }
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    SceneSettings.m_Instance.GradientActiveChanged -= OnGradientActiveChanged;
  }

  void Update() {
    if (m_TransitionToUnlockedSkybox) {
      // Shrink 'hide on press' objects while we're transitioning to a gradient.
      if (m_TransitionTimer < m_TransitionDuration) {
        m_TransitionTimer += Time.deltaTime;
        m_TransitionTimer = Mathf.Min(m_TransitionTimer, m_TransitionDuration);
        Vector3 transitionRatio = Vector3.one * (m_TransitionTimer / m_TransitionDuration);
        Vector3 invTransitionRatio =
            Vector3.one * (1.0f - (m_TransitionTimer / m_TransitionDuration));

        for (int i = 0; i < m_HideOnPress.Length; ++i) {
          m_HideOnPress[i].transform.localScale = invTransitionRatio * m_HideStartSizes[i];
        }
        for (int i = 0; i < m_ShowOnPress.Length; ++i) {
          m_ShowOnPress[i].transform.localScale = transitionRatio * m_ShowEndSizes[i];
        }

        // If we hit our transition point, turn off the hide objects.
        if (m_TransitionTimer >= m_TransitionDuration) {
          SetSkyboxUnlocked();
        }
      }
    } else {
      // Grow 'hide on press' objects while we're transitioning from a gradient.
      if (m_TransitionTimer > 0.0f) {
        m_TransitionTimer -= Time.deltaTime;
        m_TransitionTimer = Mathf.Max(m_TransitionTimer, 0.0f);
        Vector3 transitionRatio = Vector3.one * (m_TransitionTimer / m_TransitionDuration);
        Vector3 invTransitionRatio =
            Vector3.one * (1.0f - (m_TransitionTimer / m_TransitionDuration));

        for (int i = 0; i < m_HideOnPress.Length; ++i) {
          m_HideOnPress[i].transform.localScale = invTransitionRatio * m_HideStartSizes[i];
        }
        for (int i = 0; i < m_ShowOnPress.Length; ++i) {
          m_ShowOnPress[i].transform.localScale = transitionRatio * m_ShowEndSizes[i];
        }

        // If we hit our transition point, turn off the hide objects.
        if (m_TransitionTimer <= 0.0f) {
          SetSkyboxLocked();
        }
      }
    }
  }

  protected override void OnButtonPressed() {
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(new UnlockSkyboxCommand());
  }

  void OnGradientActiveChanged() {
    m_TransitionToUnlockedSkybox = SceneSettings.m_Instance.InGradient;

    // If we transitioned to a state that we're already in, ensure our visuals are correct.
    if (m_TransitionToUnlockedSkybox && m_TransitionTimer >= m_TransitionDuration) {
      SetSkyboxUnlocked();
    } else if (!m_TransitionToUnlockedSkybox && m_TransitionTimer <= 0.0f) {
      SetSkyboxLocked();
    } else {
      // For transitions to new states, and transitions during transitions, enable
      // everything so it can visibly scale.
      for (int i = 0; i < m_HideOnPress.Length; ++i) {
        m_HideOnPress[i].SetActive(true);
      }
      for (int i = 0; i < m_ShowOnPress.Length; ++i) {
        m_ShowOnPress[i].SetActive(true);
      }

      // Enable almost everything.  We turn off the title during transitions because it
      // doesn't animate and doesn't add anything.  It'll be enabled when needed.
      m_Title.SetActive(false);
    }
  }

  void SetSkyboxUnlocked() {
    for (int i = 0; i < m_HideOnPress.Length; ++i) {
      m_HideOnPress[i].SetActive(false);
    }
    for (int i = 0; i < m_ShowOnPress.Length; ++i) {
      m_ShowOnPress[i].transform.localScale = m_ShowEndSizes[i] * Vector3.one;
    }
    m_Title.SetActive(true);
  }

  void SetSkyboxLocked() {
    for (int i = 0; i < m_HideOnPress.Length; ++i) {
      m_HideOnPress[i].transform.localScale = m_HideStartSizes[i] * Vector3.one;
    }
    for (int i = 0; i < m_ShowOnPress.Length; ++i) {
      m_ShowOnPress[i].SetActive(false);
    }
    m_Title.SetActive(false);
  }
}
} // namespace TiltBrush
