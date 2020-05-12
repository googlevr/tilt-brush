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
using System.Collections.Generic;
using System.IO;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using TMPro;

namespace TiltBrush {

[System.Serializable]
public class MultiCamAttach {
  public Transform m_JointTransform;
  public Transform m_OffsetTransform;
  // Where on this screen rig to attach the camera rig.
  // This should not have any roll, relative to the parent tool.
  public Transform m_AttachPoint;
  [NonSerialized] public Vector3 m_OffsetBaseScale;
}

public class MultiCamTool : BaseTool {
  public enum GifMovementStyle {
    HorizontalArc,
    HorizontalLine,
    VerticalCircle
  }

  public enum State {
    Enter,
    Standard,
    Exit,
    Off
  }

  enum GifCreationState {
    Ready,
    Capturing,
    Building
  }

  enum VideoState {
    Ready,
    Capturing,
    Processing,
    ReadyToShare,
    Previewing
  }

  [System.Serializable]
  public class GifPreset {
    public string name;
    public GifMovementStyle style;
    [Range(0, 5)]
    public float focalPoint;
    [Range(0, .5f)]
    public float movementRadius;
    public int frames;
    public float frameDelay;
    public bool advancesTime;
  }

  const float kProgressRingGrowRate = 8.0f;
  private Vector3 kProgressRingBaseScale; // effectively readonly

  [Header("Objects")]
  [SerializeField] private MultiCamAttach[] m_Cameras;
  [SerializeField] private HintObjectScript m_SwipeHintObject;
  [SerializeField] private Renderer[] m_GifProgressRings;
  [SerializeField] private float m_SwipeHintDelay = 3.0f;
  [SerializeField] private float m_FrameGapChangeSpeed = 2f;

  [Header("Screenshot Processing")]
  [SerializeField] private float m_MinTimeBetweenShots = 1.0f;
  [SerializeField] private int m_ScreenshotWidth = 1920;
  [SerializeField] private int m_ScreenshotHeight = 1080;

  [Header("AutoGif Processing")]
  // For flat sinusoidal styles, this should be an even number;
  // otherwise there won't be a shot at theta=180
  [SerializeField] private int m_AutoGifWidth = 350;
  [SerializeField] private int m_AutoGifHeight = 350;
  [SerializeField] private GifPreset[] m_AutoGifPresets;

  [Header("TimeGif Processing")]
  [SerializeField] private Transform m_TimeGifTimeBar;
  [SerializeField] private TextMeshPro m_TimeGifTimeText;
  [SerializeField] private Renderer m_TimeGifMesh;
  [SerializeField] private int m_TimeGifWidth = 500;
  [SerializeField] private int m_TimeGifHeight = 500;
  [SerializeField] private float m_TimeGifDuration = 5.0f;
  [SerializeField] private int m_TimeGifFPS = 10;

  [Header("Quality")]
  [SerializeField]
  [Range(RenderWrapper.SSAA_MIN, RenderWrapper.SSAA_MAX)]
  private float m_superSampling = 1.0f;

  [Header("Video Processing")]
  [SerializeField] private TextMeshPro m_VideoRecordTimer;
  [SerializeField] private GameObject m_VideoSavingRoot;
  [SerializeField] private string m_VideoSavingText = "Loading Video...";
  [SerializeField] private string m_VideoPlaybackText = "Video Preview";
  [SerializeField] private string m_VideoPreviewToolText;
  [SerializeField] private string m_VideoReadyToolText;
  [SerializeField] private int m_VideoPlaybackNumLoops = 3;
  [SerializeField] private GameObject m_VideoPlaybackRoot;
  [SerializeField] private GameObject m_VideoPlaybackProgressLeftAlign;
  [SerializeField] private GameObject m_VideoPlaybackProgressMesh;
  [SerializeField] private GameObject m_VideoRecordIcon;
  [SerializeField] private GameObject m_UploadingIcon;
  [SerializeField] private TextMeshPro m_UploadingHeader;
  [SerializeField] private Texture m_UploadIconTexture;
  [SerializeField] private Texture m_ProfileIconTexture;
  [SerializeField] private Renderer m_VideoRecordingIndicator;
  [SerializeField] private Color    m_VideoRecordingIndicatorColor1;
  [SerializeField] private Color    m_VideoRecordingIndicatorColor2;
  [SerializeField] private Color    m_VideoPreviewingIndicatorColor1;
  [SerializeField] private Color    m_VideoPreviewingIndicatorColor2;
  [SerializeField] private float    m_VideoCaptureMinDuration;
  [SerializeField] private TextMeshPro m_VideoRecordAudioHeader;
  [SerializeField] private TextMeshPro m_VideoRecordAudioDesc;
  [SerializeField] private GameObject m_VideoRecorderAudioSearchVisuals;
  [SerializeField] private Renderer m_VideoRecorderAudioSearchIcon;
  [SerializeField] private Texture2D m_VideoRecorderAudioSearchIconGood;
  [SerializeField] private Texture2D m_VideoRecorderAudioSearchIconBad;
  [SerializeField] private Color m_VideoRecordAudioDescColor;
  [SerializeField] private string m_AudioLookingText;
  [SerializeField] private string m_AudioFoundText;
  [SerializeField] private string m_AudioNoneText;
  [SerializeField] private float m_AudioFoundShowDuration;
  [SerializeField] private TextMeshPro m_VideoToolText;
  [SerializeField] private string m_UploadingText;
  [SerializeField] private string m_AuthNeededText;

#if false
  [SerializeField] private GifMovementStyle m_GifStyle = GifMovementStyle.HorizontalArc;
  [Range(0, 5)]
  [SerializeField] private float m_GifFocalPointMeters = .6f;
  [Range(0, .5f)]
  [SerializeField] private float m_GifMovementRadiusMeters = .1f;
  [SerializeField] private int m_GifFrames = 20;
  [SerializeField] private float m_FrameDelay = 0.05f;
  [SerializeField] private bool m_GifAdvancesTime = true;
#else
  private GifPreset CurrentPreset { get { return m_AutoGifPresets[m_iGifPreset]; } }
  private GifMovementStyle m_GifStyle { get { return CurrentPreset.style; }}
  private float m_GifFocalPointMeters { get { return CurrentPreset.focalPoint; } }
  private float m_GifMovementRadiusMeters { get { return CurrentPreset.movementRadius; } }
  private int m_GifFrames { get { return CurrentPreset.frames; } }
  private float m_FrameDelay { get { return CurrentPreset.frameDelay; } }
  private bool m_GifAdvancesTime { get { return CurrentPreset.advancesTime; } }
#endif

  [Header("Animation")]
  [SerializeField] private float m_EnterSpeed = 16.0f;
  [SerializeField] private float m_CameraChangeTriggerT = 0.25f;
  [SerializeField] private float m_CameraChangeSpeed = 8.0f;
  [SerializeField] private float m_CameraChangeJoystickTDelay;

  [Header("Misc")]
  [SerializeField] private Vector3 m_SketchSurfaceAdditionalRotation;

  [Header("YouTube")]
  [SerializeField] private BaseButton m_YouTubeIcon;
  [SerializeField] private TextMeshPro m_YouTubeName;
  // ReSharper disable once NotAccessedField.Local
  [SerializeField] private TextMeshPro m_YouTubeEmail;

  private bool m_LockToController;

  private float m_ShotTimer;
  private bool m_EatPadInput = false;

  private State m_CurrentState;
  private float m_EnterAmount;

  static private string m_SnapshotDirectory;
  static private string m_VideoDirectory;

  private int m_CurrentCameraIndex = 0;
  private int m_DesiredCameraIndex = 0;
  private bool m_CameraChangePositive;
  private float m_CameraChangeT = 0.0f;
  private float m_CameraChangeJoystickTimer;
  private int m_iGifPreset = 0;

  private float m_TimeGifCaptureTimer;
  private float m_TimeGifCaptureInterval;
  private float m_TimeGifCaptureIntervalTimer;
  private float m_TimedGifTimeBarBaseScaleY;
  private string m_TimedGifSaveName;

  private GifCreationState m_AutoGifCreationState;
  private GifCreationState m_TimeGifCreationState;

  private VideoState m_CurrentVideoState;

  private bool m_LookingForAudio;
  private bool m_AudioHasBeenRequested;
  private float m_AudioFoundCountdown;

  // Only valid during "Capturing"
  private List<Color32[]> m_Captures;

  // Only valid during "Building"
  private float m_ProgressRingSize;
  private GifEncodeTask m_Task;

  private bool m_SwipeBlinkRequested;
  private bool m_SwipeBlinkShowing;
  private float m_SwipeHintCountdown;

  private string m_VideoCaptureFile;
  private IEnumerator m_UploadIconBlinker;
  private bool m_WaitingForAuth = false;

  private List<int> m_ActiveCameras;

  private bool m_OauthMonitored;

  private int m_RenderGap;
  private float m_CurrentGap = 1f;

  MultiCamStyle CurrentCameraStyle {
    get { return (MultiCamStyle)m_CurrentCameraIndex; }
  }

  private float GifProgressRingsSize {
    get { return m_ProgressRingSize; }
    set {
      m_ProgressRingSize = value;
      for (int i = 0; i < m_GifProgressRings.Length; ++i) {
        m_GifProgressRings[i].transform.localScale = kProgressRingBaseScale * m_ProgressRingSize;
      }
    }
  }

  private bool GifProgressRingsEnabled {
    get { return m_GifProgressRings[0].enabled; }
    set {
      for (int i = 0; i < m_GifProgressRings.Length; ++i) {
        m_GifProgressRings[i].enabled = value;
      }
    }
  }

  private float GifProgressRingsProgress {
    set {
      for (int i = 0; i < m_GifProgressRings.Length; ++i) {
        m_GifProgressRings[i].material.SetFloat("_Progress", value);
      }
    }
  }

  void SetCameraActive(int cameraIndex, bool active) {
    m_Cameras[cameraIndex].m_JointTransform.gameObject.SetActive(active);
    SketchControlsScript.m_Instance.MultiCamCaptureRig.EnableCaptureObject(
        (MultiCamStyle)cameraIndex, active);
  }

  VideoRecorder GetVideoRecorder(int cameraIndex) {
    // Early out because asking for a component that isn't there is slow.
    if ((MultiCamStyle)cameraIndex != MultiCamStyle.Video) {
      return null;
    }
    return SketchControlsScript.m_Instance.MultiCamCaptureRig.
        ManagerFromStyle(MultiCamStyle.Video).GetComponent<VideoRecorder>();
  }

  public override bool CanShowPromosWhileInUse() {
    return false;
  }

  override public void Init() {
    base.Init();

    m_ActiveCameras = new List<int>();
    for (int i = 0; i < m_Cameras.Length; ++i) {
      var style = (MultiCamStyle) i;
      if (App.PlatformConfig.EnabledMulticamStyles.Contains(style)) {
        m_ActiveCameras.Add(i);
      } else {
        m_Cameras[i].m_JointTransform.gameObject.SetActive(false);
      }
    }

    float numCams = (float)m_Cameras.Length;

    ControllerMaterialCatalog.m_Instance.Multicam.SetFloat("_IconCount", numCams);
    ControllerMaterialCatalog.m_Instance.MulticamActive.SetFloat("_IconCount", numCams);
    ControllerMaterialCatalog.m_Instance.Multicam.SetFloat("_UsedIconCount", numCams);
    ControllerMaterialCatalog.m_Instance.MulticamActive.SetFloat("_UsedIconCount", numCams);

    for (int i = 0; i < m_Cameras.Length; ++i) {
      m_Cameras[i].m_OffsetBaseScale = m_Cameras[i].m_OffsetTransform.localScale;
    }

    m_CurrentState = State.Off;
    m_EnterAmount = 0.0f;
    SetCurrentCameraToDesired();

    kProgressRingBaseScale = m_GifProgressRings[0].transform.localScale;
    m_TimeGifCaptureInterval = 1.0f / (float)m_TimeGifFPS;
    m_TimedGifTimeBarBaseScaleY = m_TimeGifTimeBar.localScale.y;

    m_SwipeBlinkRequested = true;

    m_LockToController = m_SketchSurface.IsInFreePaintMode();

    UpdateScale();
    InitSavePaths();
    // Update the gif camera label
    AdjustGifPreset(0);

    m_VideoRecordAudioDesc.color = m_VideoRecordAudioDescColor;

    // Normally, we would add our OnProfileUpdated() to the Oath2Identity.OnProfileUpdated event
    // here but our function updates a button renderer which has not been initialized by this time
    // so add our OnProfileUpdate() once during EnableTool() instead.

    // If no viewfinder preview is shown, then we need to adjust the time between shots to allow
    // for the flash animation.
    if (!App.PlatformConfig.EnableMulticamPreview) {
      m_MinTimeBetweenShots =
          SketchControlsScript.m_Instance.MultiCamCaptureRig.SnapshotFlashDuration;
    }
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    if (m_OauthMonitored) {
      OAuth2Identity.ProfileUpdated -= OnProfileUpdated;
    }
  }

  void InitSavePaths() {
    m_SnapshotDirectory = App.SnapshotPath();
    m_VideoDirectory = App.VideosPath();
    FileUtils.InitializeDirectoryWithUserError(m_SnapshotDirectory);
    FileUtils.InitializeDirectoryWithUserError(m_VideoDirectory);
  }

  override public void HideTool(bool bHide) {
    base.HideTool(bHide);

    // Always stop searching for audio (note that this does not disable audio capture).
    StopAudioSearch();

    if (bHide) {
      m_SwipeHintObject.Activate(false);

      // If the video camera is recording, stop it when hidden.
      if ((MultiCamStyle)m_CurrentCameraIndex == MultiCamStyle.Video) {
        if (m_CurrentVideoState == VideoState.Capturing) {
          StopVideoCapture(false);
        }
        VideoRecorder recorder = GetVideoRecorder(m_CurrentCameraIndex);
        if (recorder != null && recorder.IsPlayingBack) {
          recorder.StopPlayback();
        }
        if (m_CurrentVideoState == VideoState.Previewing) {
          m_CurrentVideoState = VideoState.Ready;
        }
      }

      m_CurrentState = State.Exit;
    } else {
      TurnOn();

      m_VideoRecordIcon.gameObject.SetActive(false);
      m_VideoRecordTimer.gameObject.SetActive(false);

      m_SwipeHintObject.gameObject.SetActive(true);
    }

    // Always reset the time gif mesh color.
    m_TimeGifMesh.material.color = Color.white;
  }

  override public void EnableTool(bool bEnable) {
    base.EnableTool(bEnable);

    if (bEnable) {
      SetTimeBar(m_TimeGifCaptureTimer);

      TurnOn();
      EatInput();

      // In case we used to be monitoring the tool.
      SketchSurfacePanel.m_Instance.StopMonitoringTool(this);

      // Normally, we would add our OnProfileUpdated() to the Oath2Identity.OnProfileUpdated event
      // in Init() but our function updates a button renderer which has not been initialized by that
      // time so add our OnProfileUpdate() once here instead.
      if (!m_OauthMonitored) {
        OAuth2Identity.ProfileUpdated += OnProfileUpdated;
        RefreshYouTubeIcon();
        m_OauthMonitored = true;
      }
    } else {
      TurnOff();
      UpdateScale();

      if (m_CurrentVideoState == VideoState.Capturing) {
        StopVideoCapture(false);
      }

      // Monitor the tool so our messages show up at the right time.
      if (m_Task != null) {
        SketchSurfacePanel.m_Instance.BeginMonitoringTool(this);
      }
    }
    SketchControlsScript.m_Instance.MultiCamCaptureRig.gameObject.SetActive(bEnable);
    m_SwipeHintObject.gameObject.SetActive(bEnable);
  }

  override public void Monitor() {
    // Continue to poll the task for completion.
    bool bDoneMonitoring = false;
    if (m_Task == null) {
      bDoneMonitoring = true;
    } else if (m_Task.IsDone) {
      ReportGifTaskDone();
      bDoneMonitoring = true;
    }

    if (bDoneMonitoring) {
      SketchSurfacePanel.m_Instance.StopMonitoringTool(this);
    }
  }

  bool CanCapture() {
    bool bStyleOK = true;
    switch ((MultiCamStyle)m_CurrentCameraIndex) {
    case MultiCamStyle.AutoGif:
    case MultiCamStyle.TimeGif:
      bStyleOK = (m_AutoGifCreationState == GifCreationState.Ready) &&
          (m_TimeGifCreationState == GifCreationState.Ready);
      break;
    }

    return m_ShotTimer <= 0.0f && bStyleOK;
  }

  public void ExternalObjectNextCameraStyle() {
    // Monoscopic mode camera change command.
    if (m_LockToController || !CanSwitchCameras()) {
      return;
    }

    int iNewIndex = m_CurrentCameraIndex + 1;
    iNewIndex %= m_ActiveCameras.Count;
    SetDesiredCameraIndex(iNewIndex);
    SetCameraActive(m_DesiredCameraIndex, true);
    m_CameraChangeT = 0.0f;
  }

  public void ExternalObjectForceCameraStyle(MultiCamStyle style) {
    m_DesiredCameraIndex = (int)style;
    SetCameraActive(m_DesiredCameraIndex, true);
    m_CameraChangeT = 0.0f;
  }

  bool CanSwitchCameras() {
    switch (CurrentCameraStyle) {
    case MultiCamStyle.AutoGif: return m_AutoGifCreationState != GifCreationState.Capturing;
    case MultiCamStyle.TimeGif: return m_Captures == null;
    case MultiCamStyle.Video:
      VideoRecorder recorder = GetVideoRecorder(m_CurrentCameraIndex);
      bool recording = recorder != null && recorder.IsCapturing;
      return (m_CurrentVideoState == VideoState.Ready) ||
          (m_CurrentVideoState == VideoState.ReadyToShare) ||
          (m_CurrentVideoState == VideoState.Capturing && !recording);
    }
    return true;
  }

  /// Are the buttons (Oculus) or trackpad (Vive) usable for input in the current state?
  bool PadSelectionAvailable() {
    switch (CurrentCameraStyle) {
    case MultiCamStyle.TimeGif: return m_Captures != null;
    case MultiCamStyle.Video:
      VideoRecorder recorder = GetVideoRecorder(m_CurrentCameraIndex);
      bool recording = recorder != null && recorder.IsCapturing;
      return (m_CurrentVideoState == VideoState.Capturing && !recording)
        || m_CurrentVideoState == VideoState.ReadyToShare
        || m_CurrentVideoState == VideoState.Previewing;
    }
    return false;
  }

  void SetDesiredCameraIndex(int i) {
    m_DesiredCameraIndex = m_ActiveCameras[i];
    m_SwipeBlinkShowing = false;
    m_SwipeBlinkRequested = false;
    AudioManager.m_Instance.PlaySliderSound(transform.position);
  }

  void SetCurrentCameraToDesired() {
    m_CurrentCameraIndex = m_DesiredCameraIndex;

    MultiCamStyle newStyle = CurrentCameraStyle;
    SketchControlsScript.m_Instance.MultiCamCaptureRig.m_ActiveStyle = newStyle;
    SketchControlsScript.m_Instance.MultiCamCaptureRig.ScaleVisuals(newStyle,
        m_Cameras[m_CurrentCameraIndex].m_OffsetBaseScale);
  }

  void ClearTimeGifCapture() {
    // Clear any active gif captures and set the time bar to reflect current state.
    if (m_TimeGifCreationState == GifCreationState.Capturing) {
      m_Captures = null;
      m_TimeGifCaptureTimer = 0.0f;
      m_TimeGifCreationState = GifCreationState.Ready;
      m_SwipeHintObject.Activate(true);
      AudioManager.m_Instance.PlayTrashSoftSound(
          SketchControlsScript.m_Instance.MultiCamCaptureRig.transform.position);
      OutputWindowScript.ReportFileSaved("GIF trashed!", null,
          OutputWindowScript.InfoCardSpawnPos.Brush);
      SetTimeBar(m_TimeGifCaptureTimer);
    }
  }

  void ReportGifTaskDone() {
    if (m_Task != null) {
      string err = m_Task.Error;
      if (err != null) {
        OutputWindowScript.Error("Failed to save gif", err);
      } else {
        OutputWindowScript.ReportFileSaved("Gif Written!", m_Task.GifName);
      }
      m_Task = null;
    }

    m_TimeGifCreationState = GifCreationState.Ready;
    m_AutoGifCreationState = GifCreationState.Ready;

    m_TimeGifCaptureTimer = 0.0f;
    SetTimeBar(m_TimeGifCaptureTimer);
  }

  void UpdateMultiCamTransform() {
    // Force pointer to controller if we're in controller mode.
    if (m_LockToController) {
      transform.position = InputManager.Brush.Geometry.CameraAttachPoint.position;
      transform.rotation = InputManager.Brush.Geometry.CameraAttachPoint.rotation;

      // Does the viewfinder need to face the user?
      if (!App.PlatformConfig.EnableMulticamPreview) {
        var camXform = App.VrSdk.GetVrCamera().transform;
        // Calculate the up and forward vectors so that the up is taken from the orientation of the
        // controller and the forward points directly from the head to the viewfinder.
        Vector3 center = m_Cameras[m_CurrentCameraIndex].m_AttachPoint.position;
        Vector3 forward = (center - camXform.position).normalized;
        Vector3 up = Vector3.Cross(forward, transform.right);

        m_Cameras[m_CurrentCameraIndex].m_AttachPoint.rotation =
            Quaternion.LookRotation(forward, up);
      }

    } else {
      transform.position = SketchSurfacePanel.m_Instance.transform.position;
      transform.rotation = SketchSurfacePanel.m_Instance.transform.rotation *
        Quaternion.Euler(m_SketchSurfaceAdditionalRotation);
    }
  }

  void UpdateCameraVisualTransforms() {
    if (VideoRecorderUtils.UsdPathSerializerIsBlocking) {
      return;
    }

    UpdateMultiCamTransform();

    // Update mesh joint transforms according to t value.
    MultiCamStyle currentStyle = CurrentCameraStyle;
    float angleT = m_CameraChangeT * 180.0f;
    Quaternion qCurrent = Quaternion.Euler(0.0f, 0.0f, angleT);
    m_Cameras[m_CurrentCameraIndex].m_JointTransform.localRotation = qCurrent;

    // Keep the rig locked to the current camera, or the one nearest full transition.
    Transform attach = m_Cameras[m_CurrentCameraIndex].m_AttachPoint;
    SketchControlsScript.m_Instance.MultiCamCaptureRig.UpdateObjectVisualsTransform(
        currentStyle, attach);

    if (m_CurrentCameraIndex != m_DesiredCameraIndex) {
      MultiCamStyle desiredStyle = (MultiCamStyle)m_DesiredCameraIndex;
      float flipSide = angleT + 180.0f;
      Quaternion qNext = Quaternion.Euler(0.0f, 0.0f, flipSide);
      m_Cameras[m_DesiredCameraIndex].m_JointTransform.localRotation = qNext;

      attach = m_Cameras[m_DesiredCameraIndex].m_AttachPoint;
      SketchControlsScript.m_Instance.MultiCamCaptureRig.UpdateObjectVisualsTransform(
          desiredStyle, attach);

      // Scale cameras up and down.
      Vector3 currentScale = m_Cameras[m_CurrentCameraIndex].m_OffsetBaseScale;
      currentScale *= Mathf.Max(1.0f - Mathf.Abs(m_CameraChangeT), 0.001f);
      SketchControlsScript.m_Instance.MultiCamCaptureRig.ScaleVisuals(currentStyle, currentScale);

      Vector3 desiredScale = m_Cameras[m_DesiredCameraIndex].m_OffsetBaseScale;
      desiredScale *= Mathf.Max(Mathf.Abs(m_CameraChangeT), 0.001f);
      SketchControlsScript.m_Instance.MultiCamCaptureRig.ScaleVisuals(desiredStyle, desiredScale);
    }
  }

  void UpdateCameraManagerTransforms() {
    if (VideoRecorderUtils.UsdPathSerializerIsBlocking) {
      return;
    }

    UpdateMultiCamTransform();

    // Keep the rig locked to the current camera, or the one nearest full transition.
    Transform attach = m_Cameras[m_CurrentCameraIndex].m_AttachPoint;
    MultiCamStyle currentStyle = CurrentCameraStyle;
    float t = (currentStyle == MultiCamStyle.Video) ? 1.0f - CameraConfig.Smoothing : 1.0f;
    SketchControlsScript.m_Instance.MultiCamCaptureRig.UpdateObjectCameraTransform(
        currentStyle, attach, t);

    if (m_CurrentCameraIndex != m_DesiredCameraIndex) {
      attach = m_Cameras[m_DesiredCameraIndex].m_AttachPoint;
      MultiCamStyle desiredStyle = (MultiCamStyle)m_DesiredCameraIndex;
      t = (currentStyle == MultiCamStyle.Video) ? 1.0f - CameraConfig.Smoothing : 1.0f;
      SketchControlsScript.m_Instance.MultiCamCaptureRig.UpdateObjectCameraTransform(
          desiredStyle, attach, t);
    }
  }

  override public void UpdateTool() {
    base.UpdateTool();

    // Update video capture recording state
    UpdateVideoCaptureState();
    UpdateCameraManagerTransforms();
    m_ShotTimer -= Time.deltaTime;
    bool isChangingCameras = m_CurrentCameraIndex != m_DesiredCameraIndex;
    if (!m_EatInput && !m_ToolHidden && !isChangingCameras) {
      if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate) &&
          CanCapture()) {
        // Depending on our camera style, snap some footage a certain way.
        // Only call GetSaveName() once, it can be expensive.
        string saveName = GetSaveName(CurrentCameraStyle);
        switch (CurrentCameraStyle) {
        case MultiCamStyle.Snapshot:
          if (FileUtils.CheckDiskSpaceWithError(saveName)) {
            StartCoroutine(TakeScreenshotAsync(saveName));
          }
          break;
        case MultiCamStyle.AutoGif:
          if (FileUtils.CheckDiskSpaceWithError(saveName)) {
            AutoGifTransitionReadyToCapturing();
          }
          break;
        case MultiCamStyle.TimeGif:
          if (FileUtils.CheckDiskSpaceWithError(saveName)) {
            TimeGifTransitionReadyToCapturing(saveName);
            m_SwipeHintObject.Activate(false);
          }
          break;
        case MultiCamStyle.Video:
          if (m_CurrentVideoState == VideoState.Ready) {
            if (FileUtils.CheckDiskSpaceWithError(saveName)) {
              if (VideoRecorderUtils.ActiveVideoRecording == null) {
                // Disable audio searching visuals, but don't actually stop looking for audio.
                m_VideoRecordAudioHeader.gameObject.SetActive(false);
                m_VideoRecordAudioDesc.gameObject.SetActive(false);
                m_VideoRecorderAudioSearchIcon.gameObject.SetActive(false);
                m_VideoRecorderAudioSearchVisuals.SetActive(false);

                StartVideoCapture(saveName);
              }
            }
          } else if (m_CurrentVideoState == VideoState.Capturing) {
            VideoRecorder recorder = VideoRecorderUtils.ActiveVideoRecording;
            if (recorder != null) {
              StopVideoCapture(true);
            }
          } else if (m_CurrentVideoState == VideoState.ReadyToShare) {
            if (VideoRecorderUtils.ActiveVideoRecording == null) {
              m_VideoRecordAudioDesc.gameObject.SetActive(false);
              m_VideoRecorderAudioSearchIcon.gameObject.SetActive(false);
              StartVideoCapture(saveName);
            }
          }

          m_SwipeHintObject.Activate(m_CurrentVideoState == VideoState.Ready);
          break;
        }

        m_SwipeBlinkShowing = false;
      } else {
        // Take action if selection is full.
        switch (CurrentCameraStyle) {
        case MultiCamStyle.TimeGif:
          // Trash gif.
          if (InputManager.m_Instance.GetCommandHeld(InputManager.SketchCommands.Trash)) {
            ClearTimeGifCapture();
          }
          break;
        case MultiCamStyle.Video:
          if (m_EatPadInput) {
            break;
          }
          // Timer exceeded - do something.
          if (m_CurrentVideoState == VideoState.ReadyToShare) {
            if (InputManager.m_Instance.GetCommandHeld(InputManager.SketchCommands.Share)) {
              m_CurrentVideoState = VideoState.Previewing;
              m_EatPadInput = true;
              VideoRecorder recorder = GetVideoRecorder(m_CurrentCameraIndex);
              if (recorder != null) {
                recorder.RequestPlayback();
                m_VideoRecorderAudioSearchIcon.gameObject.SetActive(false);
              }
            }
          } else if (m_CurrentVideoState == VideoState.Previewing) {
            // Share or new
            if (InputManager.m_Instance.GetCommandHeld(InputManager.SketchCommands.Confirm)) {
              App.Instance.StartCoroutine(YouTube.m_Instance.ShareVideo(m_VideoCaptureFile));
              m_UploadingIcon.SetActive(true);
              StartCoroutine(m_UploadIconBlinker = Blink(m_UploadingIcon, 0.5f));
            } else if (InputManager.m_Instance.GetCommandHeld(InputManager.SketchCommands.Cancel)) {
              AudioManager.m_Instance.PlaySliderSound(transform.position);
            } else {
              // No button confirmation yet.
              break;
            }

            m_CurrentVideoState = VideoState.Ready;
            VideoRecorder recorder = GetVideoRecorder(m_CurrentCameraIndex);
            if (recorder != null && recorder.IsPlayingBack) {
              recorder.StopPlayback();
            }

            InitAudioSearch();
            m_VideoRecordAudioDesc.gameObject.SetActive(true);
            m_VideoRecorderAudioSearchIcon.gameObject.SetActive(true);

            m_SwipeHintObject.Activate(true);
            m_VideoToolText.text = m_VideoReadyToolText;
          }
          break;
        case MultiCamStyle.Snapshot:
        case MultiCamStyle.AutoGif:
          break;
        }

        // Increment our selection timer.
        if (m_EatPadInput) {
          if (!InputManager.Brush.GetVrInput(VrInput.Any)) {
            m_EatPadInput = false;
          }
        }
      }
    }

    // Hint logic.
    if (m_SwipeHintCountdown > 0.0f && CanSwitchCameras()) {
      m_SwipeHintCountdown -= Time.deltaTime;
      if (m_SwipeHintCountdown <= 0.0f) {
        if (m_SwipeBlinkRequested) {
          m_SwipeBlinkShowing = true;
        }
        m_SwipeHintObject.Activate(true);
      }
    }

    if (m_UploadingIcon.activeSelf) {
      bool authNeeded = YouTube.m_Instance.AuthNeeded;
      if (m_WaitingForAuth != authNeeded) {
        if (authNeeded) {
          m_UploadingHeader.text = m_AuthNeededText;
          m_UploadingIcon.GetComponent<Renderer>().material.mainTexture = m_ProfileIconTexture;
        } else {
          m_UploadingHeader.text = m_UploadingText;
          m_UploadingIcon.GetComponent<Renderer>().material.mainTexture = m_UploadIconTexture;
        }
        m_WaitingForAuth = authNeeded;
      }
      if (!YouTube.m_Instance.InProgress) {
        // All done
        m_UploadingIcon.SetActive(false);
        if (m_UploadIconBlinker != null) {
          StopCoroutine(m_UploadIconBlinker);
        }
        m_UploadIconBlinker = null;
      }
    }
  }

  void Update() {
    switch (m_CurrentState) {
    case State.Enter:
      m_EnterAmount += (m_EnterSpeed * Time.deltaTime);
      if (m_EnterAmount >= 1.0f) {
        m_EnterAmount = 1.0f;
        m_CurrentState = State.Standard;
      }
      UpdateScale();
      break;
    case State.Exit:
      m_EnterAmount -= (m_EnterSpeed * Time.deltaTime);
      if (m_EnterAmount <= 0.0f) {
        TurnOff();
      }
      UpdateScale();
      break;
    }

    //if we're changing modes
    if (m_CurrentCameraIndex != m_DesiredCameraIndex) {
      if (m_CameraChangePositive) {
        m_CameraChangeT += m_CameraChangeSpeed * Time.deltaTime;
      } else {
        m_CameraChangeT -= m_CameraChangeSpeed * Time.deltaTime;
      }

      if (Mathf.Abs(m_CameraChangeT) >= 1.0f) {
        // Change state of video camera if we're switching away.
        if (CurrentCameraStyle == MultiCamStyle.Video) {
          if (m_CurrentVideoState == VideoState.Capturing) {
            VideoRecorder recorder = GetVideoRecorder(m_CurrentCameraIndex);
            if (recorder != null) {
              StopVideoCapture(true);
            }
          } else if (m_CurrentVideoState == VideoState.ReadyToShare) {
            m_CurrentVideoState = VideoState.Ready;
          }
        }

        // This is where the actual camera switch occurs.
        MultiCamStyle previousCamera = CurrentCameraStyle;
        SetCameraActive(m_CurrentCameraIndex, false);
        SetCurrentCameraToDesired();
        m_CameraChangeT = 0.0f;

        if (CurrentCameraStyle == MultiCamStyle.Video) {
          InitAudioSearch();
        } else if (previousCamera == MultiCamStyle.Video) {
          // Hide the upload status
          m_UploadingIcon.SetActive(false);
          if (m_UploadIconBlinker != null) {
            StopCoroutine(m_UploadIconBlinker);
          }
          m_UploadIconBlinker = null;
        }
      }
    } else {
      // If we're allowed to switch cameras, look for analog mode manipulation.
      if (!m_ToolHidden && CanSwitchCameras() && InputManager.m_Instance.IsBrushScrollActive()) {
        if (App.VrSdk.AnalogIsStick(InputManager.ControllerName.Brush)) {
          // When using a thumbstick, we use the absolute displacement rather than an accumulated
          // value, since this feels much better.  However, when an a switch action takes place,
          // we want to pause for a breath before we switch again.
          m_CameraChangeJoystickTimer -= Time.deltaTime;
          if (m_CameraChangeJoystickTimer <= 0.0f) {
            m_CameraChangeT = InputManager.m_Instance.GetBrushScrollAmount();
          }
        } else if (m_CurrentCameraIndex == m_DesiredCameraIndex) {
          m_CameraChangeT -= InputManager.m_Instance.GetBrushScrollAmount();
        }

        // If our camera change delta is beyond our trigger threshold, switch to the next camera.
        if (Mathf.Abs(m_CameraChangeT) > m_CameraChangeTriggerT) {
          // We're switching cameras, stop listening for audio.
          StopAudioSearch();

          // Clamp T to trigger value so animation is consistent, no matter what value comes in.
          if (App.VrSdk.AnalogIsStick(InputManager.ControllerName.Brush)) {
            m_CameraChangeT = m_CameraChangeTriggerT * Mathf.Sign(m_CameraChangeT);
          }

          // Set auto-rotate direction.
          m_CameraChangePositive = m_CameraChangeT > 0.0f;

          int iOffset = m_CameraChangePositive ? 1 : -1;
          int activeIndex = m_ActiveCameras.FindIndex(x => x == m_CurrentCameraIndex);
          int iNewIndex = (activeIndex != -1) ? activeIndex + iOffset : 0;
          if (iNewIndex >= m_ActiveCameras.Count) {
            SetDesiredCameraIndex(0);
            AdjustGifPreset(1);
          } else if (iNewIndex < 0) {
            SetDesiredCameraIndex(m_ActiveCameras.Count - 1);
            AdjustGifPreset(-1);
          } else {
            SetDesiredCameraIndex(iNewIndex);
          }

          // Enable the camera that's coming in.
          SetCameraActive(m_DesiredCameraIndex, true);
          InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.1f);

          m_CameraChangeJoystickTimer = m_CameraChangeJoystickTDelay;
        }
      } else {
        m_CameraChangeT = 0.0f;
        m_CameraChangeJoystickTimer = 0.0f;
      }
    }

    // Update gif task progress displays.
    if (m_Task != null) {
      GifProgressRingsProgress = m_Task.CreationPercent;

      if (m_AutoGifCreationState == GifCreationState.Building ||
          m_TimeGifCreationState == GifCreationState.Building) {
        float delta = kProgressRingGrowRate * Time.deltaTime;
        GifProgressRingsSize = Mathf.Min(m_ProgressRingSize + delta, 1.0f);
      }

      if (m_Task.IsDone) {
        // Task is done, reset all gif states.
        GifProgressRingsSize = 0.0f;
        GifProgressRingsEnabled = false;

        ReportGifTaskDone();
      }
    }

    // AutoGif creation states.
    if (CurrentCameraStyle == MultiCamStyle.AutoGif) {
      switch (m_AutoGifCreationState) {
      case GifCreationState.Ready:
        break;

      case GifCreationState.Capturing:
        if (m_Captures.Count == m_GifFrames) {
          AutoGifTransitionCapturingToBuilding(GetSaveName(CurrentCameraStyle));
        }
        break;

      case GifCreationState.Building: break;
      }

      //force our camera to a preview position if we're not snapping pics
      if ((m_AutoGifCreationState != GifCreationState.Capturing) &&
          (m_CurrentCameraIndex == m_DesiredCameraIndex)) {
        float period = m_FrameDelay * m_GifFrames;
        float t01 = (Time.time / period) % 1;
        t01 = Mathf.Round(t01 * m_GifFrames) / m_GifFrames;
        TrTransform xf_LS = GetGifTransform(t01);
        SketchControlsScript.m_Instance.MultiCamCaptureRig.UpdateObjectCameraLocalTransform(
            MultiCamStyle.AutoGif, xf_LS);
      }
    }

    // Time Gif.
    if (CurrentCameraStyle == MultiCamStyle.TimeGif) {
      switch (m_TimeGifCreationState) {
      case GifCreationState.Ready:
        break;

      case GifCreationState.Capturing:
        if (!m_EatInput && !m_ToolHidden &&
            InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate)) {
          // Tick our capture timer and capture a new shot if requested.
          m_TimeGifCaptureIntervalTimer += Time.deltaTime;
          if (m_TimeGifCaptureIntervalTimer >= 0.0f) {
            m_TimeGifCaptureIntervalTimer -= m_TimeGifCaptureInterval;
            TimeGifCapture();
          }

          // Tick our overall timer and bake the gif if we're out of time.
          m_TimeGifCaptureTimer += Time.deltaTime;
          if (m_TimeGifCaptureTimer >= m_TimeGifDuration) {
            TimeGifTransitionCapturingToBuilding();
            m_TimeGifCaptureTimer = m_TimeGifDuration;
            SetTimeBar(m_TimeGifDuration);
          } else {
            // Prevent time text from rounding up to "full capture".
            SetTimeBar(Mathf.Min(m_TimeGifCaptureTimer, m_TimeGifDuration - 0.1f));
            m_TimeGifMesh.material.color = m_VideoRecordingIndicatorColor2;
          }
        } else {
          // Set time gif mesh color according to button pressed state.
          m_TimeGifMesh.material.color = Color.white;
        }
        break;

      case GifCreationState.Building: break;
      }
    }

    // Video.
    if (CurrentCameraStyle == MultiCamStyle.Video) {
      VideoRecorder recorder = GetVideoRecorder(m_CurrentCameraIndex);
      switch (m_CurrentVideoState) {
      case VideoState.Ready:
        m_VideoRecordingIndicator.material.color = Color.white;
        UpdateAudioSearch();
        break;

      case VideoState.Capturing:
        if (!m_EatInput && !m_ToolHidden) {
          float fSeconds = recorder.FrameCount / (float)recorder.FPS;
          int iMinutes = (int)(fSeconds / 60.0f);
          int iSeconds = (int)(fSeconds % 60.0f);
          int iFrames = (int)(recorder.FrameCount % recorder.FPS);

          // Proper SMPTE timecode is: hour:minute:second:frame
          // Here only minute:second:frame is shown, since we don't expect/support hours of video.
          m_VideoRecordTimer.text = iMinutes
                                  + ":" + iSeconds.ToString("D2")
                                  + ":" + iFrames.ToString("D2");

          // Notify the user we are recording
          Color recordingColor;
          if (recorder.IsCapturing) {
            recordingColor = Color.Lerp(m_VideoRecordingIndicatorColor1,
                                        m_VideoRecordingIndicatorColor2,
                                        Mathf.Abs(Mathf.Sin((float)recorder.FrameCount / 3.0f)));
          } else {
            recordingColor = m_VideoRecordingIndicatorColor1;
          }

          m_VideoRecordingIndicator.material.color = recordingColor;
        } else {
          m_VideoRecordingIndicator.material.color = Color.white;
        }

        if (VideoRecorderUtils.UsdPathSerializerIsBlocking) {
          Transform xf = VideoRecorderUtils.AdvanceAndDeserializeUsd();
          if (xf != null) {
            TrTransform newXform = TrTransform.FromTransform(xf);

            // Intentionally not smoothed here, since we're already playing back a smoothed path.
            newXform.ToTransform(xf);

            // Also copy to the head transform, so we can see what's going on
            newXform.ToTransform(App.VrSdk.GetVrCamera().transform);
          }

          if (xf == null || VideoRecorderUtils.UsdPathIsFinished) {
            StopVideoCapture(false);
          }
        }

        break;

        case VideoState.Previewing:
          Color c = Color.Lerp(
            m_VideoPreviewingIndicatorColor1,
            m_VideoPreviewingIndicatorColor2,
            Mathf.Abs(Mathf.Sin((float)recorder.PlaybackFrameCount / 3.0f)));
          m_VideoRecordingIndicator.material.color = c;
          break;
      }
    }

    // If we're not rendering the preview every frame
    if (App.Config.PlatformConfig.FrameRateToPreviewRenderGap.length != 0) {
      SketchControlsScript.m_Instance.MultiCamCaptureRig.EnableCameraRender(m_RenderGap == 0);
      int gap = (int) Mathf.Clamp(App.Config.PlatformConfig.FrameRateToPreviewRenderGap
          .Evaluate(QualityControls.m_Instance.FramesInLastSecond), 1f, 20f);
      m_CurrentGap = Mathf.MoveTowards(m_CurrentGap, gap, Time.deltaTime * m_FrameGapChangeSpeed);
      m_RenderGap = (m_RenderGap + 1) % (int)m_CurrentGap;
    }
  }

  override public void LateUpdateTool() {
    base.LateUpdateTool();
    UpdateCameraVisualTransforms();
  }

  static public string GetSaveName(MultiCamStyle style) {
    string ext = "";
    switch (style) {
    case MultiCamStyle.AutoGif: ext = ".gif"; break;
    case MultiCamStyle.Snapshot: ext = ".png"; break;
    case MultiCamStyle.TimeGif: ext = ".gif"; break;
    case MultiCamStyle.Video: ext = "." + App.UserConfig.Video.ContainerType; break;
    }

    var basename = FileUtils.SanitizeFilename(SaveLoadScript.m_Instance.GetLastFileHumanName())
                 + "_{0:00}";

    try {
      // Basename is good, tack on the extension.
      basename += ext;
    } catch (ArgumentException) {
      // Basename had invalid characters.
      basename = "Unnamed_{0:00}" + ext;
    }

    if (style == MultiCamStyle.Video) {
      basename = Path.Combine(m_VideoDirectory, basename);
    } else {
      basename = Path.Combine(m_SnapshotDirectory, basename);
    }

    string fullpath;
    int lower = 0;
    int upper = 1024;
    for (int i = 1; ; i *= 2) {
      upper = i;
      fullpath = string.Format(basename, upper - 1);
      if (!File.Exists(fullpath)) {
        break;
      }
    }

    // lower == 0, upper == [1,N], some unused index.
    // Now binary search to find the smallest unused index in [0, upper].

    while (lower < upper) {
      int middle = lower + (upper - lower) / 2;
      fullpath = string.Format(basename, middle);
      if (!File.Exists(fullpath)) {
        upper = middle;
      } else {
        lower = middle + 1;
      }
    }

    return string.Format(basename, upper);
  }

  ScreenshotManager GetScreenshotManager(MultiCamStyle style) {
    return SketchControlsScript.m_Instance.MultiCamCaptureRig.ManagerFromStyle(style);
  }

  void UpdateScale() {
    for (int i = 0; i < m_Cameras.Length; ++i) {
      Vector3 vScale = m_Cameras[i].m_OffsetBaseScale;
      vScale.x *= Mathf.Max(m_EnterAmount, 0.001f);
      m_Cameras[i].m_OffsetTransform.localScale = vScale;
      SketchControlsScript.m_Instance.MultiCamCaptureRig.ScaleVisuals((MultiCamStyle)i, vScale);
    }
  }

  void TurnOff() {
    StopAudioSearch();
    m_EnterAmount = 0.0f;
    for (int i = 0; i < m_Cameras.Length; ++i) {
      m_Cameras[i].m_OffsetTransform.gameObject.SetActive(false);
    }
    SketchControlsScript.m_Instance.MultiCamCaptureRig.EnableAllVisuals(false);
    m_CurrentState = State.Off;
  }

  void TurnOn() {
    for (int i = 0; i < m_Cameras.Length; ++i) {
      m_Cameras[i].m_OffsetTransform.gameObject.SetActive(true);
    }
    SketchControlsScript.m_Instance.MultiCamCaptureRig.EnableAllVisuals(true);

    // Only enable the active camera.
    for (int i = 0; i < m_Cameras.Length; ++i) {
      SetCameraActive(i, i == m_CurrentCameraIndex);
    }
    m_DesiredCameraIndex = m_CurrentCameraIndex;
    m_CameraChangeT = 0f;
    UpdateCameraVisualTransforms();

    // Init the gif progress rings
    GifProgressRingsEnabled = ((m_AutoGifCreationState == GifCreationState.Building) ||
        (m_TimeGifCreationState == GifCreationState.Building));
    GifProgressRingsSize = 0.0f;

    // Ensure we're in the ready state and init audio if we're on Video.
    if (CurrentCameraStyle == MultiCamStyle.Video) {
      m_CurrentVideoState = VideoState.Ready;
      InitAudioSearch();
      m_VideoToolText.text = m_VideoReadyToolText;
    }

    m_SwipeHintCountdown = m_SwipeHintDelay;
    m_CurrentState = State.Enter;
    SketchControlsScript.m_Instance.MultiCamCaptureRig.EnableScreen(App.PlatformConfig.EnableMulticamPreview);
    SketchControlsScript.m_Instance.MultiCamCaptureRig.EnableCamera(App.PlatformConfig.EnableMulticamPreview);
  }

  override public void AssignControllerMaterials(InputManager.ControllerName controller) {
    InputManager.Brush.Geometry.ShowMulticamSwipe(m_SwipeBlinkShowing);

    switch (CurrentCameraStyle) {
    case MultiCamStyle.TimeGif:
      // If we've got standing frames on the timed gif, show the trash icon.
      InputManager.Brush.Geometry.ToggleTrash(m_Captures != null);
      break;

    case MultiCamStyle.Video:
      switch (m_CurrentVideoState) {
      case VideoState.Capturing:
        InputManager.Brush.Geometry.ShowCapturingVideo();
        break;
      case VideoState.ReadyToShare:
        InputManager.Brush.Geometry.ShowShareVideo();
        break;
      case VideoState.Processing:
        InputManager.Brush.Geometry.ResetAll();
        break;
      case VideoState.Previewing:
        InputManager.Brush.Geometry.ShowShareOrCancel();
        break;
      }
      break;
    }
  }

  override public float GetSizeRatio(InputManager.ControllerName controller,
                                     VrInput input) {
    VrInput? lastHeld = InputManager.Brush.GetLastHeldInput();

    if (((!lastHeld.HasValue || input != lastHeld.Value)
            && input != VrInput.Directional)
        || controller != InputManager.ControllerName.Brush) {
      return 0f;
    }

    // If the user has a pad selection available, return the hold timer.
    if (PadSelectionAvailable() && lastHeld.HasValue && input == lastHeld.Value) {
      return InputManager.Brush.GetCommandHoldProgress();
    } else if (m_CurrentVideoState == VideoState.Processing) {
      // If we're in the processing state, just hold at 1.0.
      return 1.0f;
    }

    // Return the swipe amount through all cameras.
    float fCamAmount = 1.0f / (float)m_Cameras.Length;
    float fIndexPercent = (float)m_CurrentCameraIndex * fCamAmount;
    float fTouchAdjust = (fCamAmount * m_CameraChangeT);
    return fIndexPercent + fTouchAdjust;
  }

  override public bool CanAdjustSize() {
    return m_CurrentVideoState != VideoState.Capturing;
  }

  override public bool AvailableDuringLoading() {
    return true;
  }

  override public bool AllowWorldTransformation() {
    return (VideoRecorderUtils.ActiveVideoRecording == null) &&
        m_CurrentVideoState != VideoState.Processing &&
        m_CurrentVideoState != VideoState.Previewing;
  }

  override public bool AllowsWidgetManipulation() {
    return (VideoRecorderUtils.ActiveVideoRecording == null) &&
        m_CurrentVideoState != VideoState.Processing &&
        m_CurrentVideoState != VideoState.Previewing;
  }

  override public bool InputBlocked() {
    if (CurrentCameraStyle != MultiCamStyle.Video) {
      return false;
    }

    VideoRecorder recorder = GetVideoRecorder(m_CurrentCameraIndex);
    return m_CurrentVideoState == VideoState.Capturing ||
        m_CurrentVideoState == VideoState.Processing ||
        m_CurrentVideoState == VideoState.Previewing;
  }

  override public bool BlockPinCushion() {
    if (CurrentCameraStyle != MultiCamStyle.Video) {
      return false;
    }

    return m_CurrentVideoState == VideoState.Previewing;
  }

  override public bool AllowDefaultToolToggle() {
    if (CurrentCameraStyle != MultiCamStyle.Video) {
      return base.AllowDefaultToolToggle();
    }
    return base.AllowDefaultToolToggle() &&
        (m_CurrentVideoState == VideoState.Ready ||
        m_CurrentVideoState == VideoState.ReadyToShare);
  }

  //
  // Video
  //

  void InitAudioSearch() {
    VideoRecorder recorder = GetVideoRecorder(m_CurrentCameraIndex);
    bool playbackActive = recorder != null && recorder.IsPlayingBack;
    m_LookingForAudio = !AudioCaptureManager.m_Instance.IsCapturingAudio
                     && !playbackActive;

    if (m_LookingForAudio) {
      if (!m_AudioHasBeenRequested) {
        AudioCaptureManager.m_Instance.CaptureAudio(true);
        m_AudioHasBeenRequested = true;
      }

      // UpdateAudioSearch() will keep these lines fresh, just blank them out for now.
      m_VideoRecordAudioHeader.text = "";
      m_VideoRecordAudioDesc.text = "";
      m_VideoRecorderAudioSearchVisuals.SetActive(true);
      m_VideoRecordAudioHeader.gameObject.SetActive(true);
      m_VideoRecorderAudioSearchIcon.material.mainTexture = m_VideoRecorderAudioSearchIconGood;
    } else {
      bool bCapturingAudio = AudioCaptureManager.m_Instance.IsCapturingAudio;
      RefreshAudioDescText(bCapturingAudio);
      m_VideoRecordAudioHeader.gameObject.SetActive(false);
      m_VideoRecorderAudioSearchVisuals.SetActive(false);
    }
    m_AudioFoundCountdown = -1.0f;

    // Always show the description of what source we're latched on to.
    m_VideoRecordAudioDesc.gameObject.SetActive(true);
    m_VideoRecorderAudioSearchIcon.gameObject.SetActive(true);
  }

  void UpdateAudioSearch() {
    if (m_LookingForAudio) {
      if (AudioCaptureManager.m_Instance.IsCapturingAudio) {
        m_VideoRecordAudioHeader.text = m_AudioFoundText;
        m_AudioFoundCountdown = m_AudioFoundShowDuration;
        m_VideoRecordAudioDesc.gameObject.SetActive(true);
        m_VideoRecorderAudioSearchIcon.gameObject.SetActive(true);
        m_VideoRecorderAudioSearchVisuals.SetActive(true);
        m_VideoRecordAudioDesc.text = AudioCaptureManager.m_Instance.GetCaptureStatusMessage();
        m_LookingForAudio = false;
      } else {
        m_VideoRecordAudioHeader.text = m_AudioLookingText;
        m_VideoRecordAudioDesc.text = "Play some sound or music on your computer";
      }
    } else if (m_AudioFoundCountdown > 0.0f) {
      m_AudioFoundCountdown -= Time.deltaTime;
      if (m_AudioFoundCountdown <= 0.0f) {
        m_VideoRecordAudioHeader.gameObject.SetActive(false);
        m_VideoRecorderAudioSearchVisuals.SetActive(false);
      }
    }
  }

  // Stop *searching* for audio.  If audio is already found, this does nothing.
  void StopAudioSearch() {
    m_LookingForAudio = false;
    m_AudioFoundCountdown = -1.0f;
    m_VideoRecordAudioHeader.gameObject.SetActive(false);
    m_VideoRecordAudioDesc.gameObject.SetActive(false);
    m_VideoRecorderAudioSearchIcon.gameObject.SetActive(false);
    m_VideoRecorderAudioSearchVisuals.SetActive(false);

    if (!AudioCaptureManager.m_Instance.IsCapturingAudio && m_AudioHasBeenRequested) {
      AudioCaptureManager.m_Instance.CaptureAudio(false);
      m_AudioHasBeenRequested = false;
    }
  }

  void RefreshAudioDescText(bool bCapturing) {
    if (bCapturing) {
      m_VideoRecordAudioDesc.text = AudioCaptureManager.m_Instance.GetCaptureStatusMessage();
      m_VideoRecorderAudioSearchIcon.material.mainTexture = m_VideoRecorderAudioSearchIconGood;
    } else {
      m_VideoRecordAudioDesc.text = m_AudioNoneText;
      m_VideoRecorderAudioSearchIcon.material.mainTexture = m_VideoRecorderAudioSearchIconBad;
    }
  }

  public void StartVideoCapture(string filePath, bool offlineRender = false) {
    if (!VideoRecorderUtils.StartVideoCapture(filePath,
        GetVideoRecorder(m_CurrentCameraIndex),
        SketchControlsScript.m_Instance.MultiCamCaptureRig.UsdPathSerializer,
        offlineRender)) {
      return;
    }

    m_CurrentVideoState = VideoState.Capturing;
    m_VideoRecordTimer.gameObject.SetActive(true);
    m_VideoRecordTimer.text = "0:00:00";
    m_VideoRecordIcon.gameObject.SetActive(true);
    m_VideoCaptureFile = filePath;
    m_UploadingIcon.gameObject.SetActive(false);
    if (m_UploadIconBlinker != null) {
      StopCoroutine(m_UploadIconBlinker);
    }
  }

  void StopVideoCapture(bool showInfoCard) {
    VideoRecorder recorder = VideoRecorderUtils.ActiveVideoRecording;
    if (recorder == null) {
      return;
    }

    float currentVideoLength = (float)recorder.FrameCount / (float)recorder.FPS;
    bool validVideoLength = currentVideoLength >= m_VideoCaptureMinDuration;

    string filePath = null;
    if (VideoRecorderUtils.ActiveVideoRecording != null) {
      filePath = VideoRecorderUtils.ActiveVideoRecording.FilePath;
    }
    VideoRecorderUtils.StopVideoCapture(validVideoLength);

    m_CurrentVideoState = validVideoLength ? VideoState.Processing : VideoState.Ready;
    m_VideoRecordTimer.text = "0:00:00";
    m_VideoRecordTimer.gameObject.SetActive(false);
    m_VideoRecordIcon.gameObject.SetActive(false);
    m_SwipeHintObject.Activate(true);

    RefreshAudioDescText(AudioCaptureManager.m_Instance.IsCapturingAudio);
    m_VideoRecordAudioDesc.gameObject.SetActive(true);
    m_VideoRecorderAudioSearchIcon.gameObject.SetActive(true);

    m_VideoRecordingIndicator.material.color = Color.white;

    if (validVideoLength) {
      AudioManager.m_Instance.PlaySliderSound(transform.position);
      if (showInfoCard) {
        OutputWindowScript.m_Instance.CreateInfoCardAtController(
            InputManager.ControllerName.Brush, "Video Captured!");
        if (filePath != null) {
          ControllerConsoleScript.m_Instance.AddNewLine(filePath);
        }
      }
    }

    // Do not put away the camera.
    m_RequestExit = false;
  }

  void UpdateVideoCaptureState() {
    VideoRecorder recorder = GetVideoRecorder(m_CurrentCameraIndex);
    if (recorder != null) {
      recorder.PlaybackNumLoops = m_VideoPlaybackNumLoops;
    }

    m_VideoPlaybackRoot.SetActive(recorder != null
                               && recorder.IsPlayingBack
                               && m_CurrentVideoState != VideoState.Capturing);

    m_VideoSavingRoot.SetActive(recorder != null
                               && recorder.IsSaving
                               && m_CurrentVideoState != VideoState.Capturing);

    if (m_CurrentVideoState == VideoState.Processing && !recorder.IsSaving) {
      if (App.GoogleIdentity.LoggedIn) {
        m_CurrentVideoState = VideoState.ReadyToShare;
      } else {
        m_CurrentVideoState = VideoState.Ready;
      }
    } else if (m_CurrentVideoState == VideoState.Previewing) {
      m_VideoToolText.text = m_VideoPreviewToolText;
    }

    if (m_CurrentVideoState != VideoState.Capturing) {
      if (recorder != null) {
        if (recorder.IsPlayingBack) {
          // Make a shortened version of the file path for display.
          string sanitizedPath = recorder.FilePath;
          int start = sanitizedPath.IndexOf($"\\{App.kAppFolderName}");
          if (start > -1) {
            int len = sanitizedPath.Length - start;
            sanitizedPath = sanitizedPath.Substring(start, len).Replace("\\", "/");
          } else {
            start = Math.Max(sanitizedPath.Length - 45, 0);
            int len = sanitizedPath.Length - start;
            sanitizedPath = sanitizedPath.Substring(start, len);
          }
          m_VideoRecordAudioDesc.text = sanitizedPath;
          m_VideoRecordAudioHeader.gameObject.SetActive(true);
          m_VideoRecordAudioDesc.gameObject.SetActive(true);
        }

        // Disabled until sharing lands.
        if (recorder.IsSaving) {
          m_VideoRecordAudioHeader.text = m_VideoSavingText;
        } else if (recorder.IsPlayingBack) {
          m_VideoRecordAudioHeader.text = m_VideoPlaybackText;
          Vector3 scale = m_VideoPlaybackProgressMesh.transform.localScale;
          scale.x = recorder.PlaybackPercent;
          m_VideoPlaybackProgressMesh.transform.localScale = scale;
          Vector3 align = new Vector3(scale.x / 2.0f - 0.5f, 0.0f, 0.0f);
          m_VideoPlaybackProgressLeftAlign.transform.localPosition = align;
        } else {
          Vector3 scale = m_VideoPlaybackProgressMesh.transform.localScale;
          scale.x = 1.0f;
          m_VideoPlaybackProgressMesh.transform.localScale = scale;
        }
      }
      return;
    }

    //
    // State == Capturing
    //

    // If we're running out of disk space, stop recording.
    if (!FileUtils.HasFreeSpace(recorder.FilePath)) {
      StopVideoCapture(false);
    }

    m_VideoRecordIcon.gameObject.SetActive(true);
  }

  //
  // Snapshot
  //

  IEnumerator TakeScreenshotAsync(string saveName) {
    // There are multiple expensive bits here, the most expensive of which
    // is the png conversion. Eventually we might want to run that on some other
    // thread, but it'll require a 3rd party library to do the rgb32->png encode.

    // Cheap way of preventing multiple TakeScreenshotAsync() coroutines from running
    // at once. Assumes coroutine never takes longer than m_MinTimeBetweenShots.
    m_ShotTimer = m_MinTimeBetweenShots;

    // Min SuperSampling factor (for high res snapshots)
    const float MIN_SS = 1.0f;

    AudioManager.m_Instance.PlayScreenshotSound(transform.position);

    if (!App.Config.PlatformConfig.EnableMulticamPreview) {
      SketchControlsScript.m_Instance.MultiCamCaptureRig.EnableCamera(true);
      yield return null;
    }

    ScreenshotManager rMgr = GetScreenshotManager(MultiCamStyle.Snapshot);
    if (rMgr != null) {
      // Default to the multicam values, and overwrite with user config values.
      int snapshotWidth = (App.UserConfig.Flags.SnapshotWidth > 0) ?
          App.UserConfig.Flags.SnapshotWidth :
          m_ScreenshotWidth;
      int snapshotHeight = (App.UserConfig.Flags.SnapshotHeight > 0) ?
          App.UserConfig.Flags.SnapshotHeight :
          m_ScreenshotHeight;

      RenderTexture tmp = rMgr.CreateTemporaryTargetForSave(
          snapshotWidth, snapshotHeight);

      try {
        RenderWrapper wrapper = rMgr.gameObject.GetComponent<RenderWrapper>();
        float ssaaRestore = wrapper.SuperSampling;
        // If we're beyond our multicam defaults, use low super samplin'.
        if (snapshotWidth > m_ScreenshotWidth || snapshotHeight > m_ScreenshotHeight) {
          wrapper.SuperSampling = MIN_SS;
        } else {
          wrapper.SuperSampling = m_superSampling;
        }
        rMgr.RenderToTexture(tmp);
        wrapper.SuperSampling = ssaaRestore;
        yield return null;
        SketchControlsScript.m_Instance.MultiCamCaptureRig.
            EnableCamera(App.PlatformConfig.EnableMulticamPreview);

        string fullPath = Path.GetFullPath(saveName);
        System.Object err = null;
        try {
          Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
          using (var fs = new FileStream(fullPath, FileMode.Create)) {
            ScreenshotManager.Save(fs, tmp, bSaveAsPng: true);
          }
        }
        catch (IOException e) { err = e.Message; }
        catch (UnauthorizedAccessException e) { err = e.Message; }

        if (err != null) {
          OutputWindowScript.Error("Failed to save snapshot", err);
          yield break;
        }

        App.DriveSync.SyncLocalFilesAsync().AsAsyncVoid();
        OutputWindowScript.ReportFileSaved("Snapshot Saved!", saveName,
            OutputWindowScript.InfoCardSpawnPos.Brush);

        if (!App.PlatformConfig.EnableMulticamPreview) {
          var multiCam = SketchControlsScript.m_Instance.MultiCamCaptureRig;
          yield return multiCam.SnapshotFlashAnimation(m_CurrentCameraIndex, tmp);
        }
      } finally {
        // Do not put away the camera.
        m_RequestExit = false;
        RenderTexture.ReleaseTemporary(tmp);
      }
    }
  }

  //
  // TimeGif
  //

  void SetTimeBar(float fTime) {
    // Figure how far through our duration we are.
    float fCompletePercent = fTime / m_TimeGifDuration;

    // Clamp our complete percent to something small because scale 0 looks bad.
    fCompletePercent = Mathf.Max(fCompletePercent, 0.025f);

    // Scale and position time bar.
    Vector3 vTimeBarScale = m_TimeGifTimeBar.localScale;
    vTimeBarScale.y = m_TimedGifTimeBarBaseScaleY * fCompletePercent;
    m_TimeGifTimeBar.localScale = vTimeBarScale;

    Vector3 vTimeBarLocalPos = m_TimeGifTimeBar.localPosition;
    vTimeBarLocalPos.y = -m_TimedGifTimeBarBaseScaleY + vTimeBarScale.y;
    m_TimeGifTimeBar.localPosition = vTimeBarLocalPos;

    // Set and position time text.
    m_TimeGifTimeText.text = fTime.ToString("0.0");
    Vector3 vTimeBarTextLocalPos = m_TimeGifTimeText.transform.localPosition;
    vTimeBarTextLocalPos.y = -m_TimedGifTimeBarBaseScaleY + (vTimeBarScale.y * 2.0f);
    m_TimeGifTimeText.transform.localPosition = vTimeBarTextLocalPos;
  }

  void TimeGifTransitionReadyToCapturing(string saveName) {
    Debug.Assert(m_TimeGifCreationState == GifCreationState.Ready);
    m_TimeGifCreationState = GifCreationState.Capturing;
    m_Captures = new List<Color32[]>((int)((float)m_TimeGifFPS * m_TimeGifDuration));
    m_TimeGifCaptureTimer = 0.0f;

    // Cache this early so if the user switches sketches, we retain the initial sketch for naming.
    m_TimedGifSaveName = saveName;
  }

  void TimeGifCapture() {
    Texture2D tempTex = new Texture2D(m_TimeGifWidth, m_TimeGifHeight, TextureFormat.RGB24, false);

    ScreenshotManager rMgr = GetScreenshotManager(MultiCamStyle.TimeGif);
    var tempTarget = rMgr.CreateTemporaryTargetForSave(tempTex.width, tempTex.height);
    rMgr.RenderToTexture(tempTarget);

    RenderTexture.active = tempTarget;
    tempTex.ReadPixels(new Rect(0, 0, tempTex.width, tempTex.height), 0, 0, false);
    tempTex.Apply();
    RenderTexture.active = null;
    RenderTexture.ReleaseTemporary(tempTarget);

    m_Captures.Add(tempTex.GetPixels32());
    Destroy(tempTex);
  }

  void TimeGifTransitionCapturingToBuilding() {
    Debug.Assert(m_TimeGifCreationState == GifCreationState.Capturing);
    m_TimeGifCreationState = GifCreationState.Building;
    m_SwipeHintObject.Activate(true);

    GifProgressRingsSize = 0.0f;
    GifProgressRingsEnabled = true;
    m_TimeGifMesh.material.color = Color.white;

    int delayMs = (int)(m_TimeGifCaptureInterval * 1000);

    // Time Gif should be palette per frame because the nature of the mechanic encourages
    // single frames with colors that could be very different from all other frames.
    m_Task = new GifEncodeTask(
        m_Captures, delayMs,
        m_TimeGifWidth, m_TimeGifHeight, m_TimedGifSaveName, 1f / 8, true);
    m_Captures = null;
    m_Task.Start();

    AudioManager.m_Instance.PlayScreenshotSound(transform.position);

    // Give immediate feedback; but we won't really know if it succeeded or failed until later.
    OutputWindowScript.ReportFileSaved("Gif Captured!", null,
        OutputWindowScript.InfoCardSpawnPos.Brush);

    // Do not put away the camera.
    m_RequestExit = false;
  }

  //
  // AutoGif
  //

  void AutoGifTransitionReadyToCapturing() {
    Debug.Assert(m_AutoGifCreationState == GifCreationState.Ready);
    m_AutoGifCreationState = GifCreationState.Capturing;

    m_Captures = new List<Color32[]>(m_GifFrames);
    StartCoroutine(AutoGifStateCapturing());
  }

  IEnumerator AutoGifStateCapturing() {
    // Don't think this is necessary, because we're rendering from scratch
    // (not copying from the backbuffer)
    // yield return new WaitForEndOfFrame();
    Texture2D tempTex = new Texture2D(m_AutoGifWidth, m_AutoGifHeight, TextureFormat.RGB24, false);

    ScreenshotManager rMgr = GetScreenshotManager(MultiCamStyle.AutoGif);
    if (rMgr == null) { yield break; }
    Transform camera = rMgr.LeftEye.transform;

    // Reset any tweaks the gif preview may have made
    Coords.AsLocal[camera] = TrTransform.identity;
    TrTransform baseXf = TrTransform.FromTransform(camera);

    // If symmetric, we can rely on these identities:
    // - frame(t) == frame(1-t)
    // - m_Captures[i] == m_Captures[NumFrames - i]

    int nCapture = GifMovementIsSymmetric() ? m_GifFrames/2 + 1 : m_GifFrames;
    while (m_Captures.Count < nCapture) {
      // TODO: try supersampling
      var tempTarget = rMgr.CreateTemporaryTargetForSave(tempTex.width, tempTex.height);
      float t = (float)m_Captures.Count / m_GifFrames;

      TrTransform prevXf = TrTransform.FromLocalTransform(camera);
      TrTransform offsetXf = GetGifTransform(t);
      var tmp = (baseXf * offsetXf);  // Work around 2018.3.x Mono parse bug
      tmp.ToTransform(camera);
      rMgr.RenderToTexture(tempTarget);
      prevXf.ToLocalTransform(camera);

      RenderTexture.active = tempTarget;
      tempTex.ReadPixels(new Rect(0, 0, tempTex.width, tempTex.height),
                         0, 0, false);
      tempTex.Apply();
      RenderTexture.active = null;
      RenderTexture.ReleaseTemporary(tempTarget);

      if (m_GifAdvancesTime) {
        yield return null;
      }

      m_Captures.Add(tempTex.GetPixels32());
    }

    while (m_Captures.Count < m_GifFrames) {
      // Take advantage of the symmetry identity (see above)
      m_Captures.Add(m_Captures[m_GifFrames - m_Captures.Count]);
    }

    Destroy(tempTex);
  }

  void AutoGifTransitionCapturingToBuilding(string saveName) {
    Debug.Assert(m_AutoGifCreationState == GifCreationState.Capturing);
    m_AutoGifCreationState = GifCreationState.Building;

    GifProgressRingsSize = 0.0f;
    GifProgressRingsEnabled = true;

    int delayMs = (int)(m_FrameDelay * 1000);

    m_Task = new GifEncodeTask(
        m_Captures, delayMs,
        m_AutoGifWidth, m_AutoGifHeight, saveName);
    m_Captures = null;
    m_Task.Start();

    AudioManager.m_Instance.PlayScreenshotSound(transform.position);

    // Give immediate feedback; but we won't really know if it succeeded or failed until later.
    OutputWindowScript.ReportFileSaved("Gif Captured!", null,
        OutputWindowScript.InfoCardSpawnPos.Brush);

    // Do not put away the camera.
    m_RequestExit = false;
  }

  //
  // Gif movement
  //

  // Return true if give movement is symmetric about t = .5
  // IOW, if frame(t) == frame(1-t)
  bool GifMovementIsSymmetric() {
    return ((m_GifStyle == GifMovementStyle.HorizontalArc) ||
            (m_GifStyle == GifMovementStyle.HorizontalLine));
  }

  TrTransform GetGifTransform(float t) {
    switch (m_GifStyle) {
    case GifMovementStyle.HorizontalArc:
      return GetGifTransform_HorizontalArc(t);
    case GifMovementStyle.HorizontalLine:
      return GetGifTransform_HorizontalLine(t);
    case GifMovementStyle.VerticalCircle:
      return GetGifTransform_VerticalCircle(t);
    default:
      return TrTransform.identity;
    }
  }

  void AdjustGifPreset(int i) {
    if (m_AutoGifPresets.Length == 0) {
      return;
    }
    // Limit shipping version to a single gif preset
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      m_iGifPreset = (m_iGifPreset + i + m_AutoGifPresets.Length) % m_AutoGifPresets.Length;

      var preset = m_AutoGifPresets[m_iGifPreset];

      // Apply the preset
      var text = m_Cameras[1].m_OffsetTransform.Find("Text");
      if (text != null) {
        var tmpro = text.GetComponent<TMPro.TextMeshPro>();
        if (tmpro != null) {
          tmpro.text = preset.name;
        }
      }
    }
#endif
  }

  // This version rotates in an arc around the subject.
  // Uses m_GifMovementRadiusMeters, m_GifFocalPointMeters
  TrTransform GetGifTransform_HorizontalArc(float t) {
    float M2U = App.METERS_TO_UNITS;
    float theta = t * (2 * Mathf.PI);
    float cos = Mathf.Cos(theta);

    if (m_GifFocalPointMeters > 0) {
      // moving in an arc about the focal point, so the arc radius is
      // m_GifFocalPointMeters.  Convert circumferential distance to degrees
      float movementDeg = m_GifMovementRadiusMeters / m_GifFocalPointMeters * Mathf.Rad2Deg;

      Vector3 localTarget = Vector3.forward * m_GifFocalPointMeters * M2U;
      Quaternion localRot = Quaternion.AngleAxis(movementDeg * cos, Vector3.up);

      Vector3 localPos = localTarget - localRot * localTarget;

      return TrTransform.TR(localPos, localRot);
    } else {
      // Ends up being the same thing as _HorizontalLine
      Vector3 localPos = (m_GifMovementRadiusMeters * M2U) *
        (-cos * Vector3.right);
      return TrTransform.T(localPos);
    }
  }

  // Horizontal line, pointing at subject
  // Uses m_GifMovementRadiusMeters, m_GifFocalPointMeters
  TrTransform GetGifTransform_HorizontalLine(float t) {
    float M2U = App.METERS_TO_UNITS;
    float theta = t * (2 * Mathf.PI);
    float cos = Mathf.Cos(theta);

    Vector3 localPos = (m_GifMovementRadiusMeters * M2U) *
      (-cos * Vector3.right);

    Quaternion localRot;
    if (m_GifFocalPointMeters > 0) {
      Vector3 localTarget = Vector3.forward * m_GifFocalPointMeters * M2U;
      localRot = Quaternion.LookRotation(localTarget - localPos, Vector3.up);
    } else {
      localRot = Quaternion.identity;
    }

    return TrTransform.TR(localPos, localRot);
  }

  // Uses m_GifMovementRadiusMeters, m_GifFocalPointMeters
  TrTransform GetGifTransform_VerticalCircle(float t) {
    float M2U = App.METERS_TO_UNITS;
    float theta = t * (2 * Mathf.PI);
    float sin = Mathf.Sin(theta);
    float cos = Mathf.Cos(theta);

    Vector3 localPos = (m_GifMovementRadiusMeters * M2U) *
      (-cos * Vector3.right + sin * Vector3.up);

    Quaternion localRot;
    if (m_GifFocalPointMeters > 0) {
      Vector3 localTarget = Vector3.forward * m_GifFocalPointMeters * M2U;
      localRot = Quaternion.LookRotation(localTarget - localPos, Vector3.up);
    } else {
      localRot = Quaternion.identity;
    }

    return TrTransform.TR(localPos, localRot);
  }

  [SuppressMessage("ReSharper", "IteratorNeverReturns")]  // Intentional infinite loop
  IEnumerator Blink(GameObject obj, float seconds = 1f) {
    while (true) {
      obj.SetActive(true);
      yield return new WaitForSeconds(seconds);
      obj.SetActive(false);
      yield return new WaitForSeconds(seconds);
    }
  }

  void RefreshYouTubeIcon() {
    // Update UI with current YouTube channel info
    OAuth2Identity.UserInfo profile = App.GoogleIdentity.Profile;
    if (profile != null) {
      m_YouTubeName.text = profile.name;
      //m_YouTubeEmail.text = profile.email;
      if (profile.icon == null) {
        m_YouTubeIcon.gameObject.SetActive(false);
      } else {
        m_YouTubeIcon.SetButtonTexture(profile.icon);
        m_YouTubeIcon.gameObject.SetActive(true);
      }
    } else {
      m_YouTubeIcon.gameObject.SetActive(false);
    }
  }

  private void OnProfileUpdated(OAuth2Identity _) {
    RefreshYouTubeIcon();
  }
}
}  // namespace TiltBrush
