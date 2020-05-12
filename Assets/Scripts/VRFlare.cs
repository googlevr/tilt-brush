//// Copyright 2020 The Tilt Brush Authors
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

[System.Serializable]
public class VRFlareData {
  public Transform m_Flare;
  public float m_DepthFactor;
  public float m_CrossFactor;
}

public class VRFlare : MonoBehaviour {

  [SerializeField]
  private VRFlareData[] m_Flares;

  private Transform m_CameraTransform;

	// Use this for initialization
	void Start () {
      m_CameraTransform = App.VrSdk.GetVrCamera().transform;
	}

	// Update is called once per frame
	void Update () {
      Transform camXf = m_CameraTransform;
      Vector3 headPos = transform.position;
      Vector3 dirToCamera = headPos - camXf.position;
      Vector3 cameraForward = camXf.forward;
      float cameraDot = Vector3.Dot(cameraForward, dirToCamera.normalized);

      // cameraCross is used to shift flares in screenspace
      Vector3 cameraCross = Vector3.Cross(cameraForward, dirToCamera.normalized);
      cameraCross = Vector3.Cross(cameraCross, dirToCamera.normalized);

      // Use scene scale to modulate the cross factor, which is the flare displacement.
      float scale = App.Scene.Pose.scale;

      for (int i = 0; i < m_Flares.Length; ++i) {
        m_Flares[i].m_Flare.position = headPos
                                     - m_Flares[i].m_DepthFactor * dirToCamera
                                     - m_Flares[i].m_CrossFactor * cameraCross * cameraDot * scale;
      }
	}
}
}  // namespace TiltBrush
