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

public class AmbientGrid : MonoBehaviour {
  [SerializeField] private GameObject m_GridBig;
  [SerializeField] private GameObject m_GridSmall;
  [SerializeField] private Color m_GridBaseColor = new Color(.0235f, .0235f, .0235f);
  [SerializeField] private float m_GridSpacing = 10;

  private Transform m_CameraTransform;
  private Material m_MaterialBig;
  private Material m_MaterialSmall;

  void Start () {
    m_CameraTransform = App.VrSdk.GetVrCamera().transform;
    m_MaterialBig = m_GridBig.GetComponentInChildren<MeshRenderer>().material;
    m_MaterialSmall = m_GridSmall.GetComponentInChildren<MeshRenderer>().material;
  }

  void Update () {
    // Figure out the nearest power of two scales above and below the scene scale.
    float sceneScale = App.Scene.Pose.scale;
    float sceneScaleLogBaseTwo = Mathf.Log(sceneScale) / Mathf.Log(2);
    float scaleFraction = sceneScaleLogBaseTwo - Mathf.Floor(sceneScaleLogBaseTwo);
    float sceneScaleBig = Mathf.Pow(.5f, Mathf.Floor(sceneScaleLogBaseTwo));
    float sceneScaleSmall = 0.5f * sceneScaleBig;

    // Offset the grid by the grid spacing closest to the camera.
    TrTransform xfCamera_SS = App.Scene.AsScene[m_CameraTransform];
    Vector3 vCameraPosition = xfCamera_SS.translation;
    float cameraNearestX = Mathf.Floor(vCameraPosition.x / m_GridSpacing / sceneScaleBig) * m_GridSpacing * sceneScaleBig;
    float cameraNearestY = Mathf.Floor(vCameraPosition.y / m_GridSpacing / sceneScaleBig) * m_GridSpacing * sceneScaleBig;
    float cameraNearestZ = Mathf.Floor(vCameraPosition.z / m_GridSpacing / sceneScaleBig) * m_GridSpacing * sceneScaleBig;
    Vector3 vCameraOffset = new Vector3(cameraNearestX, cameraNearestY, cameraNearestZ);
    m_GridBig.transform.localPosition = vCameraOffset;
    m_GridSmall.transform.localPosition = vCameraOffset;

    m_GridBig.transform.localScale = new Vector3(sceneScaleBig, sceneScaleBig, sceneScaleBig);
    m_GridSmall.transform.localScale = new Vector3(sceneScaleSmall, sceneScaleSmall, sceneScaleSmall);
    m_MaterialBig.SetColor("_Color", m_GridBaseColor * (1 - scaleFraction));
    m_MaterialSmall.SetColor("_Color", m_GridBaseColor * scaleFraction);
  }
}
}  // namespace TiltBrush
