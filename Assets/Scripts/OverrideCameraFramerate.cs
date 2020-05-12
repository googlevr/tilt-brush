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

public class OverrideCameraFramerate : MonoBehaviour {

  // Set frames to skip to "2" in order to render at 30FPS in VR (which is 90FPS)
  public int m_FramesToSkip = 0;
  private Camera m_Camera;
  private int frameCount = 0;
  // Use this for initialization
  void Start () {
    m_Camera = GetComponent<Camera>();
    m_Camera.enabled = false;
  }

  // Update is called once per frame
  void Update () {
    if (frameCount > m_FramesToSkip) {
      m_Camera.Render();
      frameCount = 0;
    }
    frameCount++;
  }
}
}  // namespace TiltBrush
