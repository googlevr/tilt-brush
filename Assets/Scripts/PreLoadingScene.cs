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
using UnityEngine.SceneManagement;

namespace TiltBrush {

public class PreLoadingScene : MonoBehaviour {
  [SerializeField] private Transform m_Logo;
  [SerializeField] private Camera m_Camera;

  void Start() {
    // Position screen overlay in front of the camera.
    m_Logo.parent = m_Camera.transform;
    m_Logo.position = m_Camera.transform.position + m_Camera.transform.forward * 25.0f;
    m_Logo.localRotation = Quaternion.identity;
    m_Logo.parent = null;

    SceneManager.LoadSceneAsync("Loading");
  }
}
} // namespace TiltBrush