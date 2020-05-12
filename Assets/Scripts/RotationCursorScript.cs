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

public class RotationCursorScript : MonoBehaviour {
  public GameObject m_Mesh;
  public GameObject m_ToCursorLine;
  public GameObject m_CrossSectionLine;
  private Vector3 m_BaseToCursorScale;
  private Vector3 m_BaseCrossSectionScale;
  private Color m_BaseToCursorColor;
  private Color m_BaseCrossSectionColor;
  public float m_BeginFadePercent;
  public float m_EndFadePercent;

  void Awake() {
    m_BaseToCursorScale = m_ToCursorLine.transform.localScale;
    m_BaseCrossSectionScale = m_CrossSectionLine.transform.localScale;
    m_BaseToCursorColor = m_ToCursorLine.GetComponent<Renderer>().material.GetColor("_Color");
    m_BaseCrossSectionColor = m_CrossSectionLine.GetComponent<Renderer>().material.GetColor("_Color");
  }

  public void PositionCursorLines(Vector3 vSurfaceCenter, Vector3 vSurfaceNormal, float fOffsetAngle, float fSurfaceWidth) {
    //get the angle in a (0:45) range
    while (fOffsetAngle > 90.0f) { fOffsetAngle -= 90.0f; }
    if (fOffsetAngle > 45.0f) {
      fOffsetAngle = 90.0f - fOffsetAngle;
    }

    //position and orient the cursor line
    Vector3 vToCursor = transform.position - vSurfaceCenter;
    float fDistToCursor = vToCursor.magnitude;
    if (fDistToCursor < 0.0001f) {
      vToCursor = Vector3.forward;
    }
    vToCursor.Normalize();

    Vector3 vToCursorScale = m_BaseToCursorScale;
    vToCursorScale.z = fDistToCursor * 0.5f;

    Color rCursorColor = m_BaseToCursorColor;
    float fBeginFadeDist = m_BeginFadePercent * fSurfaceWidth * 0.5f;
    float fEndFadeDist = m_EndFadePercent * fSurfaceWidth * 0.5f;
    float fAlpha = Mathf.Clamp((fDistToCursor - fBeginFadeDist) / (fEndFadeDist - fBeginFadeDist), 0.0f, 1.0f);
    rCursorColor.a = fAlpha;

    m_ToCursorLine.transform.localScale = vToCursorScale;
    m_ToCursorLine.transform.rotation = Quaternion.LookRotation(vToCursor, vSurfaceNormal);
    m_ToCursorLine.transform.position = vSurfaceCenter + (vToCursor * fDistToCursor * 0.5f);
    m_ToCursorLine.GetComponent<Renderer>().material.SetColor("_Color", rCursorColor);

    //and now the cross section line
    Vector3 vCrossSectionScale = m_BaseCrossSectionScale;
    vCrossSectionScale.x = fSurfaceWidth / Mathf.Cos(fOffsetAngle * Mathf.Deg2Rad) * 0.5f;

    Color rCrossSectionColor = m_BaseCrossSectionColor;
    rCrossSectionColor.a = fAlpha;

    m_CrossSectionLine.transform.localScale = vCrossSectionScale;
    m_CrossSectionLine.transform.rotation = m_ToCursorLine.transform.rotation;
    m_CrossSectionLine.transform.position = vSurfaceCenter;
    m_CrossSectionLine.GetComponent<Renderer>().material.SetColor("_Color", rCrossSectionColor);
  }

  public void ClearCursorLines(Vector3 vSurfacePos) {
    m_ToCursorLine.transform.localScale = m_BaseToCursorScale;
    m_ToCursorLine.transform.position = vSurfacePos;

    m_CrossSectionLine.transform.localScale = m_BaseCrossSectionScale;
    m_CrossSectionLine.transform.position = vSurfacePos;
  }
}
}  // namespace TiltBrush
