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

public class TransformGizmoScript : MonoBehaviour {
  public Color[] m_AxesColors;
  public GameObject[] m_Axes;
  public Color m_ActiveAxisColor;
  public Color m_InactiveAxisColor;

  void Awake() {
    ResetTransform();
  }

  public void SetTransformForPushPull() {
    m_Axes[0].GetComponent<Renderer>().material.SetColor("_Color", m_InactiveAxisColor);
    m_Axes[1].GetComponent<Renderer>().material.SetColor("_Color", m_InactiveAxisColor);
    m_Axes[2].GetComponent<Renderer>().material.SetColor("_Color", m_ActiveAxisColor);
  }

  public void SetTransformForPan() {
    m_Axes[0].GetComponent<Renderer>().material.SetColor("_Color", m_ActiveAxisColor);
    m_Axes[1].GetComponent<Renderer>().material.SetColor("_Color", m_ActiveAxisColor);
    m_Axes[2].GetComponent<Renderer>().material.SetColor("_Color", m_InactiveAxisColor);
  }

  public void ResetTransform() {
    for (int i = 0; i < Mathf.Min(m_Axes.Length, m_AxesColors.Length); ++i) {
      m_Axes[i].GetComponent<Renderer>().material.SetColor("_Color", m_AxesColors[i]);
    }
  }
}
}  // namespace TiltBrush
