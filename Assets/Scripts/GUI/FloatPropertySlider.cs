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
public class FloatPropertySlider : BaseSlider {

  [SerializeField] private SerializedPropertyReferenceFloat m_Property;
  [Vec2AsRange] [SerializeField] private Vector2 m_Range = Vector2.up;
  [Tooltip("Values < 1 will bias the low end - Values > 1 will bias the high end.")]
  [SerializeField] private float m_PowerCurve = 1;

  public override void UpdateValue(float value) {
    if (m_Property.HasValue) {
      float iLerped = Mathf.InverseLerp(m_Range.x, m_Range.y, value);
      m_Property.Value = Mathf.Pow(iLerped, 1f / m_PowerCurve);
    }
  }

  protected virtual void Update() {
    if (m_Property.HasValue) {
      m_CurrentValue = Mathf.Lerp(m_Range.x, m_Range.y, Mathf.Pow(m_Property.Value, m_PowerCurve));
      SetSliderPositionToReflectValue();
    }
  }
}
} // namespace TiltBrush