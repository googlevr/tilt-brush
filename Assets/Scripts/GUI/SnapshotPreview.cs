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

public class SnapshotPreview : UIComponent {
  [SerializeField] private GameObject m_Mesh;
  [SerializeField] private Transform m_CameraAttach;
  private MultiCamCaptureRig m_SketchControlsCaptureRig;

  protected override void Awake() {
    base.Awake();
    m_Mesh.SetActive(false);
    m_SketchControlsCaptureRig = SketchControlsScript.m_Instance.MultiCamCaptureRig;
  }

  override public bool UpdateStateWithInput(bool inputValid, Ray inputRay,
      GameObject parentActiveObject, Collider parentCollider) {
    // If this function is being ticked, it means we're active.  In that case, keep the capture
    // rig locked to our attach point.
    float t = (m_SketchControlsCaptureRig.m_ActiveStyle == MultiCamStyle.Video) ?
        (1.0f - CameraConfig.Smoothing) : 1.0f;
    m_SketchControlsCaptureRig.UpdateObjectCameraTransform(
        m_SketchControlsCaptureRig.m_ActiveStyle, m_CameraAttach, t);

    return base.UpdateStateWithInput(inputValid, inputRay, parentActiveObject, parentCollider);
  }

  override public void GazeRatioChanged(float gazeRatio) {
    bool activateMesh = gazeRatio > 0.0f;
    // If we're turning on, grab our render texture from the active multicam camera.
    if (!m_Mesh.activeSelf && activateMesh) {
      MultiCamStyle activeStyle = m_SketchControlsCaptureRig.m_ActiveStyle;
      ScreenshotManager ssMgr = m_SketchControlsCaptureRig.ManagerFromStyle(activeStyle);
      m_Mesh.GetComponent<Renderer>().material = ssMgr.LeftEyeMaterial;
    }
    m_Mesh.SetActive(activateMesh);
  }
}

}  // namespace TiltBrush
