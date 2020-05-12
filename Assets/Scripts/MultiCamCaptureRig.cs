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

using System.Collections;
using UnityEngine;

namespace TiltBrush {

public enum MultiCamStyle {
  Snapshot,
  AutoGif,
  TimeGif,
  Video,
  Num
}

[System.Serializable]
public struct MultiCamCaptureObject {
  public GameObject m_Object;
  public GameObject m_Visuals;
  public GameObject m_Camera;
  public GameObject m_Screen;
  [System.NonSerialized] public ScreenshotManager m_Manager;
  [System.NonSerialized] public Camera m_CameraComponent;
  [System.NonSerialized] public Vector2 m_CameraClipPlanesBase;
}

public class MultiCamCaptureRig : MonoBehaviour {
  [SerializeField] private MultiCamCaptureObject[] m_CaptureObjects;
  [SerializeField] private AnimationCurve m_SnapshotFlashAnimation;
  [SerializeField] private AnimationCurve m_SnapshotAlphaAnimation;

  // This value should only be set by the MultiCam.  It mirrors state of the Multicam to allow
  // other objects easier access to understanding the active style.
  // This value is flimsy and error-prone.  A TODO would be moving more state off MultiCam and
  // in to the capture rig so this is the one source of truth.
  [System.NonSerialized] public MultiCamStyle m_ActiveStyle;

  private UsdPathSerializer m_VideoUsdSerializer;

  public UsdPathSerializer UsdPathSerializer {
    get { return m_VideoUsdSerializer; }
  }

  public void Init() {
    Debug.AssertFormat(m_CaptureObjects.Length == (int)MultiCamStyle.Num,
        "There needs to be exactly the number of capture objects as MultiCam styles.");

    // This is necessary to initialize the video renderer.
    gameObject.SetActive(true);
    gameObject.SetActive(false);

    for (int i = 0; i < m_CaptureObjects.Length; ++i) {
      m_CaptureObjects[i].m_Manager =
          m_CaptureObjects[i].m_Camera.GetComponentInChildren<ScreenshotManager>(true);
      m_CaptureObjects[i].m_CameraComponent = 
          m_CaptureObjects[i].m_Camera.GetComponentInChildren<Camera>();
      m_CaptureObjects[i].m_CameraClipPlanesBase.x =
          m_CaptureObjects[i].m_CameraComponent.nearClipPlane;
      m_CaptureObjects[i].m_CameraClipPlanesBase.y =
          m_CaptureObjects[i].m_CameraComponent.farClipPlane;
    }

    m_VideoUsdSerializer = m_CaptureObjects[(int)MultiCamStyle.Video].m_Camera.
        GetComponentInChildren<UsdPathSerializer>(true);

    CameraConfig.FovChanged += UpdateFovs;
    UpdateFovs();
  }

  public void UpdateAllObjectVisualsTransform(Transform xf) {
    for (int i = 0; i < m_CaptureObjects.Length; ++i) {
      m_CaptureObjects[i].m_Visuals.transform.position = xf.position;
      m_CaptureObjects[i].m_Visuals.transform.rotation = xf.rotation;
    }
  }

  public void UpdateObjectVisualsTransform(MultiCamStyle style, Transform xf) {
    m_CaptureObjects[(int)style].m_Visuals.transform.position = xf.position;
    m_CaptureObjects[(int)style].m_Visuals.transform.rotation = xf.rotation;
  }

  public void UpdateAllObjectCameraTransform(Transform xf, float videoCameraLerpT) {
    for (int i = 0; i < m_CaptureObjects.Length; ++i) {
      MultiCamCaptureObject obj = m_CaptureObjects[i];
      if ((MultiCamStyle)i == MultiCamStyle.Video) {
        UpdateObjectCameraTransform(MultiCamStyle.Video, xf, videoCameraLerpT);
      } else {
        obj.m_Camera.transform.position = xf.position;
        obj.m_Camera.transform.rotation = xf.rotation;
      }
    }
  }

  public void UpdateObjectCameraTransform(MultiCamStyle style, Transform xf, float cameraLerpT) {
    MultiCamCaptureObject obj = m_CaptureObjects[(int)style];

    // For the mobile version we need to adjust the camera fov so that the frustum exactly matches
    // the view through the viewfinder. The camera is placed at the vr camera position.
    if (!App.PlatformConfig.EnableMulticamPreview) {
      obj.m_Camera.transform.position = App.VrSdk.GetVrCamera().transform.position;
      obj.m_Camera.transform.rotation = xf.rotation;
      float distance = (xf.position - obj.m_Camera.transform.position).magnitude;
      float height = obj.m_Screen.transform.localScale.y * 0.5f;
      float fov = 2f * Mathf.Atan2(height, distance) * Mathf.Rad2Deg;
      obj.m_CameraComponent.fieldOfView = fov;
      obj.m_CameraComponent.nearClipPlane = distance;

    } else {
      obj.m_Camera.transform.position =
          Vector3.Lerp(obj.m_Camera.transform.position, xf.position, cameraLerpT);
      obj.m_Camera.transform.rotation =
          Quaternion.Slerp(obj.m_Camera.transform.rotation, xf.rotation, cameraLerpT);
    }
  }

  public void UpdateObjectCameraLocalTransform(MultiCamStyle style, TrTransform xf_LS) {
    m_CaptureObjects[(int)style].m_Manager.transform.localPosition = xf_LS.translation;
    m_CaptureObjects[(int)style].m_Manager.transform.localRotation = xf_LS.rotation;
  }

  public void ForceClippingPlanes(MultiCamStyle style) {
    MultiCamCaptureObject obj = m_CaptureObjects[(int)style];
    TrTransform xf = TrTransform.FromTransform(obj.m_CameraComponent.transform);
    float scale = App.ActiveCanvas.Pose.scale;
    obj.m_CameraComponent.nearClipPlane = obj.m_CameraClipPlanesBase.x * scale;
    obj.m_CameraComponent.farClipPlane = obj.m_CameraClipPlanesBase.y * scale;
      Debug.Log($"machk: forcing clip: {obj.m_CameraClipPlanesBase.x}->{obj.m_CameraClipPlanesBase.y} x{scale}");
  }

    public ScreenshotManager ManagerFromStyle(MultiCamStyle style) {
    return m_CaptureObjects[(int)style].m_Manager;
  }

  public void EnableCaptureObject(MultiCamStyle style, bool enable) {
    m_CaptureObjects[(int)style].m_Object.SetActive(enable);
  }

  public void ScaleVisuals(MultiCamStyle style, Vector3 scale) {
    m_CaptureObjects[(int)style].m_Visuals.transform.localScale = scale;
  }

  public void EnableAllVisuals(bool enable) {
    for (int i = 0; i < m_CaptureObjects.Length; ++i) {
      m_CaptureObjects[i].m_Visuals.SetActive(enable);
    }
  }

  public void UpdateFovs() {
    for (int i = 0; i < m_CaptureObjects.Length; ++i) {
      m_CaptureObjects[i].m_CameraComponent.fieldOfView = CameraConfig.Fov;
    }
  }

  public void EnableScreen(bool enable) {
    for (int i = 0; i < m_CaptureObjects.Length; ++i) {
      m_CaptureObjects[i].m_Screen.SetActive(enable);
    }
  }

  public void EnableCamera(bool enable) {
    for (int i = 0; i < m_CaptureObjects.Length; ++i) {
      m_CaptureObjects[i].m_Camera.SetActive(enable);
    }
  }

  public void EnableCameraRender(bool enable) {
    foreach (var capture in m_CaptureObjects) {
      capture.m_CameraComponent.enabled = enable;
    }
  }

  public float SnapshotFlashDuration {
    get {
      return m_SnapshotAlphaAnimation.keys[m_SnapshotAlphaAnimation.length - 1].time;
    }
  }

  /// Animates a 'flash and fade' on the specified camera screen object, with the given
  /// texture
  public IEnumerator SnapshotFlashAnimation(int camera, Texture texture) {
    float maxTime = SnapshotFlashDuration;
    var screen = m_CaptureObjects[camera].m_Screen;
    screen.SetActive(true);
    Material screenMaterial = screen.GetComponent<Renderer>().material;
    screenMaterial.mainTexture = texture;
    for (float time = 0; time <= maxTime; time += Time.deltaTime) {
      screenMaterial.SetFloat("_Alpha", m_SnapshotAlphaAnimation.Evaluate(time));
      screenMaterial.SetFloat("_FlashMix", m_SnapshotFlashAnimation.Evaluate(time));
      yield return null;
    }
    screenMaterial.SetFloat("_Alpha", 1);
    screenMaterial.SetFloat("_FlashMix", 0);
    screenMaterial.mainTexture = null;
    screen.SetActive(false);
  }
}

} // namespace TiltBrush