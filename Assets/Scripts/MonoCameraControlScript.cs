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

public class MonoCameraControlScript : MonoBehaviour {
  private float m_xScale = 5f;
  private float m_yScale = 2.4f;
  private float m_yClamp = 85f;
  private Vector3 m_cameraRotation;

  void Update() {
    // Use mouse position to control camera rotation.
    if (InputManager.m_Instance.GetKeyboardShortcut(
            InputManager.KeyboardShortcut.PositionMonoCamera)) {
      // Mouse's x coordinate corresponds to camera's rotation around y axis.
      m_cameraRotation.y += Input.GetAxis("Mouse X") * m_xScale;
      if (m_cameraRotation.y <= -180) {
        m_cameraRotation.y += 360;
      } else if (m_cameraRotation.y > 180) {
        m_cameraRotation.y -= 360;
      }

      // Mouse's y coordinate corresponds to camera's rotation around x axis.
      m_cameraRotation.x -= Input.GetAxis("Mouse Y") * m_yScale;
      m_cameraRotation.x = Mathf.Clamp(m_cameraRotation.x, -m_yClamp, m_yClamp);
    }
    transform.localEulerAngles = m_cameraRotation;
  }
}
}  // namespace TiltBrush
