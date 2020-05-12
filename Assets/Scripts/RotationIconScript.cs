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

public class RotationIconScript : MonoBehaviour {
  public Texture m_Standard;
  public Texture m_AngleLocked;

  void Start() {
    GetComponent<Renderer>().material.mainTexture = m_Standard;
  }

  public void SetLocked(bool bLocked) {
    if (bLocked) {
      GetComponent<Renderer>().material.mainTexture = m_AngleLocked;
    } else {
      GetComponent<Renderer>().material.mainTexture = m_Standard;
    }
  }
}
}  // namespace TiltBrush
