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

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TiltBrush {

public class LoadingScene : MonoBehaviour {
  [SerializeField] private GvrOverlay m_Overlay;
  [SerializeField] private Camera m_VrCamera;
  [SerializeField] private float m_MaximumLoadingTime;
  // Amount of the progress bar taken up by the scene load
  [SerializeField] private float m_SceneLoadRatio;

  // We have a slightly faked loading position that will always increase
  // The fake loading rate is the minimum amount it will increase in one second, the reciprocal of
  // m_MaximumLoadingTime
  private float m_FakeLoadingRate;
  private float m_CurrentLoadingPosition;

  private IEnumerator Start() {
    m_FakeLoadingRate = 1f / m_MaximumLoadingTime;
    m_CurrentLoadingPosition = 0;

    // Position screen overlay in front of the camera.
    m_Overlay.transform.parent = m_VrCamera.transform;
    m_Overlay.transform.localPosition = Vector3.zero;
    m_Overlay.transform.localRotation = Quaternion.identity;
    float scale = 0.5f * m_VrCamera.farClipPlane / m_VrCamera.transform.lossyScale.z;
    m_Overlay.transform.localScale = Vector3.one * scale;

    // Reparent the overlay so that it doesn't move with the headset.
    m_Overlay.transform.parent = transform;

    // Reset the rotation so that it's level and centered on the horizon.
    Vector3 eulerAngles = m_Overlay.transform.localRotation.eulerAngles;
    m_Overlay.transform.localRotation = Quaternion.Euler(new Vector3(0, eulerAngles.y, 0));

    m_Overlay.gameObject.SetActive(true);

    UpdateProgress(0, 0, 0);

    DontDestroyOnLoad(gameObject);

    AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("Main");
    while (!asyncLoad.isDone) {
      UpdateProgress(0, m_SceneLoadRatio, asyncLoad.progress);
      yield return null;
    }

    // Skip a frame to allow app to get out of Standard and in to LoadingBrushesAndLighting state.
    yield return null;

    // We can't check against app state until main scene loading is done.
    while (App.CurrentState == App.AppState.LoadingBrushesAndLighting) {
      UpdateProgress(m_SceneLoadRatio, 1f - m_SceneLoadRatio, ShaderWarmup.Instance.Progress);
      yield return null;
    }

    yield return null;

    Destroy(gameObject);
  }

  // Updates the progress bar. Will always increment a little bit to ensure the user is always
  // told that things are progressing.
  // start and scale determine which part of the progress bar we are updating
  // progress should be from 0 - 1 for that section.
  private void UpdateProgress(float start, float scale, float progress) {
    m_CurrentLoadingPosition += Time.deltaTime * m_FakeLoadingRate;
    float position = start + scale * progress;
    m_CurrentLoadingPosition = Mathf.Max(m_CurrentLoadingPosition, position);
    m_Overlay.Progress = m_CurrentLoadingPosition;
  }
}
} // namespace TiltBrush