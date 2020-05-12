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
#if USD_SUPPORTED
using USD.NET;
using USD.NET.Unity;
#endif

namespace TiltBrush {
/// Used for writing the path a transform takes to a path, in USD format.
/// Relative to the scene transform
public class UsdPathSerializer : MonoBehaviour {
#if USD_SUPPORTED
  [System.Serializable]
  class UsdCameraSample : CameraSample {
    [UsdVariability(Variability.Uniform)]
    public float eyeScale = 1;

    public UsdCameraSample() : base() {
    }

    public UsdCameraSample(Camera cam)
      : base(cam) {
    }
  }

  [System.Serializable]
  [UsdSchema("Camera")]
  class UsdCameraXformSample : XformSample {
    public float fov = -1;
  }

  private USD.NET.Scene m_Scene;
  private string m_xformName;
  private UsdCameraXformSample m_UsdCamera;
  private UsdCameraSample m_UsdCameraInfo;
  private float m_Smoothing;
  private bool m_IsRecording;
  private Camera m_RecordingCamera;
  private Camera[] m_PlaybackCameras;
  private bool m_UnitsInMeters = true;

  public bool IsRecording { get { return m_IsRecording; } }
  public bool IsFinished { get { return m_Scene.Time.Value >= m_Scene.EndTime; } }
  public double Duration { get { return m_Scene.EndTime - m_Scene.StartTime; } }

  public double Time {
    get { return m_Scene.Time.Value; }
    set {
      if (value >= m_Scene.Time) {
        m_Scene.Time = value;
      } else {
        Debug.LogErrorFormat("ERROR : out-of-sequence time passed to UsdPathSerializer. Current Time {0}, new time {1}",
          m_Scene.Time, value);
      }
    }
  }

  /// Starts recording the transform to a named transform in a new usd file given by the path.
  public bool StartRecording(string sketchName = "/Sketch", string xformName = "/VideoCamera") {
    m_xformName = sketchName + xformName;
    if (!App.InitializeUsd()) {
      return false;
    }

    // Find the active camera.
    m_RecordingCamera = null;
    foreach (var c in GetComponentsInChildren<Camera>()) {
      if (c.gameObject.activeInHierarchy && c.isActiveAndEnabled) {
        m_RecordingCamera = c;
      }
    }
    if (m_RecordingCamera == null) {
      return false;
    }

    var sketchRoot = ExportUsd.CreateSketchRoot();
    m_UsdCamera = new UsdCameraXformSample();
    m_Scene = USD.NET.Scene.Create();

    m_Scene.Write(sketchName, sketchRoot);

    // The time code of the recording is in seconds.
    m_Scene.Stage.SetTimeCodesPerSecond(1);

    // CameraSample constructor converts the Unity Camera to USD.
    // Write the fallback camera parameters.
    var cameraSample = new UsdCameraSample(m_RecordingCamera);

    // Convert camera params to meters.
    cameraSample.clippingRange *= App.UNITS_TO_METERS;

    m_Scene.Write(m_xformName, cameraSample);

    m_Scene.Time = 0;
    m_Scene.StartTime = 0;
    m_Scene.EndTime = 0;

    m_IsRecording = true;
    return true;
  }

  /// Stops recording the transform.
  public void Stop() {
    if (m_IsRecording) {
      m_Scene.EndTime = m_Scene.Time.Value;
      m_Scene.Save();
      m_IsRecording = false;
      m_RecordingCamera = null;
    }
  }

  public void Save(string path) {
    m_Scene.SaveAs(path);
  }

  public bool Load(string path) {
    if (!App.InitializeUsd()) {
      return false;
    }
    m_Scene = Scene.Open(path);
    return m_Scene != null;
  }

  /// Plays back a named transform onto the current transform from a usd path.
  /// The transform can optionally be smoothed using exponential smoothing.
  /// Smoothing will be clamped between 0 - 1.
  public void StartPlayback(string sketchName = "/Sketch", string xformName = "/VideoCamera",
                            float smoothing = 0) {
    m_xformName = xformName;
    m_Smoothing = Mathf.Clamp01(smoothing);
    m_IsRecording = false;

    // Older versions of Tilt Brush exported usda camera paths in decimeters. We now
    // export in meters to match USD geometry export. Older versions also didn't export any sketch 
    // data so we check here for the presence of sketch data to decide how to treat the camera
    // path units.
    bool hasSketchRoot = m_Scene.Stage.GetPrimAtPath(new pxr.SdfPath(sketchName));
    m_xformName = hasSketchRoot ? sketchName + xformName : xformName;
    float scale = hasSketchRoot ? App.UNITS_TO_METERS : 1;
    m_UnitsInMeters = hasSketchRoot;

    m_Scene.Time = null;
    m_UsdCameraInfo = new UsdCameraSample();
    m_UsdCameraInfo.shutter = new USD.NET.Unity.CameraSample.Shutter();
    m_Scene.Read(m_xformName, m_UsdCameraInfo);

    m_UsdCamera = new UsdCameraXformSample();
    m_Scene.Time = 0;

    m_Scene.Read(m_xformName, m_UsdCamera);
    var basisMat = AxisConvention.GetFromUnity(AxisConvention.kUsd)
                   * Matrix4x4.Scale(Vector3.one * scale);
    m_UsdCamera.transform = ExportUtils.ChangeBasis(m_UsdCamera.transform, basisMat, basisMat.inverse);

    TrTransform xf_WS = UsdXformToWorldSpaceXform(m_UsdCamera);
    xf_WS.ToTransform(transform);

    m_PlaybackCameras = FindObjectsOfType<Camera>();
  }

  private TrTransform UsdXformToWorldSpaceXform(USD.NET.Unity.XformSample usdXform) {
    TrTransform xf_CS = TrTransform.FromMatrix4x4(usdXform.transform);
    return TrTransform.FromTransform(App.Scene.ActiveCanvas.transform) * xf_CS;
  }

  public void Deserialize() {
    m_Scene.Read(m_xformName, m_UsdCamera);

    var basisMat = Matrix4x4.identity;
    if (m_UnitsInMeters) {
      basisMat[0, 0] *= App.METERS_TO_UNITS;
      basisMat[1, 1] *= App.METERS_TO_UNITS;
      basisMat[2, 2] *= -1 * App.METERS_TO_UNITS;
    }
    m_UsdCamera.transform = ExportUtils.ChangeBasis(m_UsdCamera.transform, basisMat, basisMat.inverse);

    TrTransform xf_WS = UsdXformToWorldSpaceXform(m_UsdCamera);
    TrTransform old_WS = TrTransform.FromTransform(transform);
    TrTransform new_WS = TrTransform.Lerp(old_WS, xf_WS, 1 - m_Smoothing);
    new_WS.scale = m_UsdCameraInfo.eyeScale != 0 ? m_UsdCameraInfo.eyeScale : 1;
    new_WS.ToTransform(transform);

    // Pre-M23.3, .usd files won't have fov defined, so this value will be negative.
    if (m_UsdCamera.fov > 0) {
      // A bit brute force, but we're running through all cameras in the scene to make
      // sure the preview shows the modified fov.
      for (int i = 0; i < m_PlaybackCameras.Length; ++i) {
        if (m_PlaybackCameras[i].gameObject.activeInHierarchy &&
            m_PlaybackCameras[i].isActiveAndEnabled) {
          m_PlaybackCameras[i].fieldOfView = m_UsdCamera.fov;
        }
      }
    }
  }

  public void Serialize() {
    // Get transform in canvas space, and write to scene.
    TrTransform xf_CS = Coords.AsCanvas[transform];
    var basisMat = Matrix4x4.identity;
    if (m_UnitsInMeters) {
      basisMat[0, 0] *= App.UNITS_TO_METERS;
      basisMat[1, 1] *= App.UNITS_TO_METERS;
      basisMat[2, 2] *= -1 * App.UNITS_TO_METERS;
    }
    m_UsdCamera.transform = ExportUtils.ChangeBasis(xf_CS.ToMatrix4x4(), basisMat, basisMat.inverse);
    m_UsdCamera.fov = m_RecordingCamera.fieldOfView;
    m_Scene.Write(m_xformName, m_UsdCamera);
  }
#endif
}
} // namespace TiltBrush
