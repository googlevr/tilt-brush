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

public class CameraPathCaptureRig : MonoBehaviour {
  [SerializeField] private GameObject m_Object;
  [SerializeField] private GameObject m_Camera;

  private CameraPathPreviewWidget m_Widget;
  private ScreenshotManager m_Manager;
  private UsdPathSerializer m_VideoUsdSerializer;
  private Camera m_CameraComponent;
  private Vector2 m_CameraClipPlanesBase;

  public bool Enabled => m_Object.activeSelf;

  public ScreenshotManager Manager => m_Manager;

  void Awake() {
    App.Switchboard.ToolChanged += OnToolChanged;
    App.Switchboard.CameraPathVisibilityChanged += RefreshVisibility;
    App.Switchboard.CameraPathDeleted += RefreshVisibility;
    App.Switchboard.AllWidgetsDestroyed += RefreshVisibility;
    App.Switchboard.CameraPathCreated += RefreshVisibility;
    App.Switchboard.CameraPathKnotChanged += RefreshVisibility;
    App.Switchboard.CurrentCameraPathChanged += RefreshVisibility;
    App.Scene.MainCanvas.PoseChanged += OnPoseChanged;
  }

  void OnDestroy() {
    App.Switchboard.ToolChanged -= OnToolChanged;
    App.Switchboard.CameraPathVisibilityChanged -= RefreshVisibility;
    App.Switchboard.CameraPathDeleted -= RefreshVisibility;
    App.Switchboard.AllWidgetsDestroyed -= RefreshVisibility;
    App.Switchboard.CameraPathCreated -= RefreshVisibility;
    App.Switchboard.CameraPathKnotChanged -= RefreshVisibility;
    App.Switchboard.CurrentCameraPathChanged -= RefreshVisibility;
    App.Scene.MainCanvas.PoseChanged -= OnPoseChanged;
  }

  public void Init() {
    // This is necessary to initialize the video renderer.
    m_Object.SetActive(true);
    m_Object.SetActive(false);

    m_VideoUsdSerializer = m_Camera.GetComponentInChildren<UsdPathSerializer>(true);
    m_Manager = m_Camera.GetComponentInChildren<ScreenshotManager>(true);
    m_CameraComponent = m_Manager.GetComponent<Camera>();
    m_CameraClipPlanesBase.x = m_CameraComponent.nearClipPlane;
    m_CameraClipPlanesBase.y = m_CameraComponent.farClipPlane;
    m_Widget = GetComponentInChildren<CameraPathPreviewWidget>();
    m_Widget.gameObject.SetActive(false);
  }

  public void SetPreviewWidgetCompletionPercent(float zeroToOne) {
    m_Widget.SetCompletionAlongPath(zeroToOne);
  }

  public void OverridePreviewWidgetPathT(PathT? t) {
    m_Widget.OverridePathT = t;
  }

  /// Returns "completion" as a float, [0:1].
  public float? GetCompletionOfCameraAlongPath() {
    return m_Widget.GetCompletionAlongPath();
  }

  public void UpdateCameraTransform(Transform xf) {
    m_Camera.transform.position = xf.position;
    m_Camera.transform.rotation = xf.rotation;
  }

  public void SetFov(float fov) {
    m_CameraComponent.fieldOfView = fov;
  }

  public void RecordPath() {
    // See README.md section # Video support and # Camera path support.
    /*
    m_Widget.ResetToPathStart();
    m_Widget.TintForRecording(true);
    UpdateCameraTransform(m_Widget.transform);
    WidgetManager.m_Instance.FollowingPath = true;
    // When we begin recording a camera path, switch to the CameraPathTool and set the
    // mode to recording.
    SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.CameraPathTool);
    App.Switchboard.TriggerCameraPathModeChanged(CameraPathTool.Mode.Recording);

    VideoRecorderUtils.StartVideoCapture(
        MultiCamTool.GetSaveName(MultiCamStyle.Video),
        m_Manager.GetComponent<VideoRecorder>(),
        m_VideoUsdSerializer);
    */
  }

  public void StopRecordingPath(bool saveCapture) {
    string message = saveCapture ? "Path Recorded!" : "Recording Canceled";
    OutputWindowScript.m_Instance.CreateInfoCardAtController(
        InputManager.ControllerName.Brush, message);
    if (saveCapture) {
      string filePath = null;
      if (VideoRecorderUtils.ActiveVideoRecording != null) {
        filePath = VideoRecorderUtils.ActiveVideoRecording.FilePath;
      }
      if (filePath != null) {
        ControllerConsoleScript.m_Instance.AddNewLine(filePath);
      }
    }
    VideoRecorderUtils.StopVideoCapture(saveCapture);
    WidgetManager.m_Instance.FollowingPath = false;
    m_Widget.ResetToPathStart();
    m_Widget.TintForRecording(false);

    // When we stop recording a camera path, make sure our CameraPathTool isn't in the
    // recording state.
    SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.CameraPathTool);
    App.Switchboard.TriggerCameraPathModeChanged(CameraPathTool.Mode.AddPositionKnot);
  }

  void RefreshVisibility() {
    m_Object.SetActive(WidgetManager.m_Instance.CameraPathsVisible);
    m_Widget.Show(WidgetManager.m_Instance.CanRecordCurrentCameraPath());
  }

  void OnPoseChanged(TrTransform prev, TrTransform current) {
    m_CameraComponent.nearClipPlane = m_CameraClipPlanesBase.x * current.scale;
    m_CameraComponent.farClipPlane = m_CameraClipPlanesBase.y * current.scale;
  }

  void OnToolChanged() {
    if (SketchSurfacePanel.m_Instance.GetCurrentToolType() == BaseTool.ToolType.MultiCamTool) {
      WidgetManager.m_Instance.FollowingPath = false;
    }
    RefreshVisibility();
  }
}
} // namespace TiltBrush