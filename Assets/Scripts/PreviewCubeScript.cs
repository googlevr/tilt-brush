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

public class PreviewCubeScript : UIComponentDescription {
  [Header ("PreviewCube-specific")]
  public GameObject m_SampleQuad;
  private Vector3 m_QuadAnimatingValue;
  public Vector3 m_QuadAnimatingSpeed;
  public Vector3 m_QuadAnimatingAmount;

  // Legacy variables
  // TODO:
  // Can be deleted once all brush buttons have been moved to using new button descriptions.
  public Texture m_StandardTexture;
  public Texture m_SelectedTexture;
  public Renderer m_BoundingCube;

  void Update() {
    //update quad rotation angle
    m_QuadAnimatingValue.x += Time.deltaTime * m_QuadAnimatingSpeed.x;
    m_QuadAnimatingValue.y += Time.deltaTime * m_QuadAnimatingSpeed.y;
    m_QuadAnimatingValue.z += Time.deltaTime * m_QuadAnimatingSpeed.z;

    Vector3 vEulers = Vector3.zero;
    vEulers.x = Mathf.Cos(m_QuadAnimatingValue.x) * m_QuadAnimatingAmount.x;
    vEulers.y = Mathf.Cos(m_QuadAnimatingValue.y) * m_QuadAnimatingAmount.y;
    vEulers.z = Mathf.Cos(m_QuadAnimatingValue.z) * m_QuadAnimatingAmount.z;

    Quaternion qOrient = Quaternion.Euler(vEulers);
    m_SampleQuad.transform.rotation = transform.rotation * qOrient;
  }

  // Legacy function
  // TODO:
  // Can be deleted once all brush buttons have been moved to using new button descriptions.
  public void SetSelected(bool bSelected) {
    if (m_BoundingCube) {
      if (bSelected) {
        m_BoundingCube.material.mainTexture = m_SelectedTexture;
      } else {
        m_BoundingCube.material.mainTexture = m_StandardTexture;
      }
    }
  }

  public void SetSampleQuadTexture(Texture2D tex) {
    m_SampleQuad.GetComponent<Renderer>().material.mainTexture = tex;
  }
}
}  // namespace TiltBrush
