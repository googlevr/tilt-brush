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

public class GripHintScript : MonoBehaviour {
  public Transform m_ArrowTransform;
  private Vector3 m_ArrowBasePosition;
  public float m_ArrowBobSpeed;
  public float m_ArrowBobScalar;
  private float m_ArrowBobValue;

  void Awake() {
    m_ArrowBasePosition = m_ArrowTransform.localPosition;
  }

  void Update() {
    m_ArrowBobValue += Time.deltaTime * m_ArrowBobSpeed;
    float fArrowOffset = Mathf.Sin(m_ArrowBobValue) * m_ArrowBobScalar;

    Vector3 vOffsetPosition = m_ArrowBasePosition;
    vOffsetPosition.x += fArrowOffset;
    m_ArrowTransform.localPosition = vOffsetPosition;
  }
}
}  // namespace TiltBrush
