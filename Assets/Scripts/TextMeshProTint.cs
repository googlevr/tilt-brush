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
using TMPro;

namespace TiltBrush {

// This class is useful for tinting TextMeshPro objects without having to create a new material.
public class TextMeshProTint : MonoBehaviour {
  [SerializeField] private Color m_Color;
  private TextMeshPro m_SiblingText;

  void Awake() {
    m_SiblingText = GetComponent<TextMeshPro>();
    Debug.Assert(m_SiblingText != null);
    m_SiblingText.color = m_Color;
  }
}

}  // namespace TiltBrush