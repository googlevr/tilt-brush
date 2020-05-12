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

public class SaveIconCaptureScript : MonoBehaviour {
  [SerializeField] private float m_GifCaptureOffset;
  [SerializeField] private float m_GifTotalAngle;

  private float[] m_GifAngles;

  void Start() {
    //calculate angles for gif icon
    m_GifAngles = new float[SaveLoadScript.m_Instance.m_SaveGifTextureCount];
    float fAutoGifCaptureHalfAngle = m_GifTotalAngle * 0.5f;
    for (int i = 0; i < m_GifAngles.Length; ++i) {
      //put T in the range [0 : Pi]
      float fT = ((float)i / (float)(m_GifAngles.Length - 1)) * Mathf.PI;

      //this is in the range [-1 : 1]
      float fCosT = Mathf.Cos(fT);

      m_GifAngles[i] = fCosT * fAutoGifCaptureHalfAngle;
    }

    Debug.Assert(transform.parent.gameObject.name == "AutoOrientJoint");
  }

  public void SetSaveIconTransformForGifFrame(Vector3 basePos, Quaternion baseRot, int iFrame) {
    //determine the point we're going to be rotating around
    Vector3 vTargetOffset = (baseRot * Vector3.forward) * m_GifCaptureOffset;
    Vector3 vTarget = basePos + vTargetOffset;

    //determine our rotation vector according to our frame angle
    float fAngle = m_GifAngles[iFrame];
    Quaternion qRotate = Quaternion.AngleAxis(fAngle, (baseRot * Vector3.up));
    Vector3 vTransformedOffset = qRotate * vTargetOffset;

    //set position as [rotated] offset from target
    transform.position = vTarget - vTransformedOffset;

    //point rig toward target
    transform.rotation = qRotate * baseRot;
  }
}
}  // namespace TiltBrush
