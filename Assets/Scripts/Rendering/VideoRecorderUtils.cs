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

using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TiltBrush {

static public class VideoRecorderUtils {
  static private float m_VideoCaptureResolutionScale = 1.0f;
  static private int m_DebugVideoCaptureQualityLevel = -1;
  static private int m_PreCaptureQualityLevel = -1;

  // [Range(RenderWrapper.SSAA_MIN, RenderWrapper.SSAA_MAX)]
  static private float m_SuperSampling = 2.0f;
  static private float m_PreCaptureSuperSampling = 1.0f;

#if USD_SUPPORTED
  static private UsdPathSerializer m_UsdPathSerializer;
  static private System.Diagnostics.Stopwatch m_RecordingStopwatch;
  static private string m_UsdPath;
#endif

  static private VideoRecorder m_ActiveVideoRecording;

  static public VideoRecorder ActiveVideoRecording {
    get { return m_ActiveVideoRecording; }
  }

  static public int NumFramesInUsdSerializer {
    get {
#if USD_SUPPORTED
      if (m_UsdPathSerializer != null && !m_UsdPathSerializer.IsRecording) {
        return Mathf.CeilToInt((float)m_UsdPathSerializer.Duration *
            (int)m_ActiveVideoRecording.FPS);
      }
#endif
      return 0;
    }
  }

  static public bool UsdPathSerializerIsBlocking {
    get {
#if USD_SUPPORTED
      return (m_UsdPathSerializer != null &&
          !m_UsdPathSerializer.IsRecording &&
          !m_UsdPathSerializer.IsFinished);
#else
      return false;
#endif
    }
  }

  static public bool UsdPathIsFinished {
    get {
#if USD_SUPPORTED
      return (m_UsdPathSerializer != null && m_UsdPathSerializer.IsFinished);
#else
      return true;
#endif
    }
  }

  static public Transform AdvanceAndDeserializeUsd() {
#if USD_SUPPORTED
    if (m_UsdPathSerializer != null) {
      m_UsdPathSerializer.Time += Time.deltaTime;
      m_UsdPathSerializer.Deserialize();
      return m_UsdPathSerializer.transform;
    }
#endif
    return null;
  }

  static public void SerializerNewUsdFrame() {
#if USD_SUPPORTED
    if (m_UsdPathSerializer != null && m_UsdPathSerializer.IsRecording) {
      m_UsdPathSerializer.Time = (float)m_RecordingStopwatch.Elapsed.TotalSeconds;
      m_UsdPathSerializer.Serialize();
    }
#endif
  }

  static public bool StartVideoCapture(string filePath, VideoRecorder recorder,
      UsdPathSerializer usdPathSerializer, bool offlineRender = false) {
    // Only one video at a time.
    if (m_ActiveVideoRecording != null) {
      return false;
    }

    // Don't start recording unless there is enough space left.
    if (!FileUtils.InitializeDirectoryWithUserError(
        Path.GetDirectoryName(filePath),
        "Failed to start video capture")) {
      return false;
    }

    // Vertical video is disabled.
    recorder.IsPortrait = false;

    // Start the capture first, which may fail, so do this before toggling any state.
    // While the state below is important for the actual frame capture, starting the capture process
    // does not require it.
    int sampleRate = 0;
    if (AudioCaptureManager.m_Instance.IsCapturingAudio) {
      sampleRate = AudioCaptureManager.m_Instance.SampleRate;
    }

    if (!recorder.StartCapture(filePath, sampleRate,
        AudioCaptureManager.m_Instance.IsCapturingAudio, offlineRender,
        offlineRender ? App.UserConfig.Video.OfflineFPS : App.UserConfig.Video.FPS)) {
      OutputWindowScript.ReportFileSaved("Failed to start capture!", null,
          OutputWindowScript.InfoCardSpawnPos.Brush);
      return false;
    }

    m_ActiveVideoRecording = recorder;

    // Perform any necessary VR camera rendering optimizations to reduce CPU & GPU workload

    // Debug reduce quality for capture.
    // XXX This should just be ADAPTIVE RENDERING
    if (m_DebugVideoCaptureQualityLevel != -1) {
      m_PreCaptureQualityLevel = QualityControls.m_Instance.QualityLevel;
      QualityControls.m_Instance.QualityLevel = m_DebugVideoCaptureQualityLevel;
    }

    App.VrSdk.SetHmdScalingFactor(m_VideoCaptureResolutionScale);

    // Setup SSAA
    RenderWrapper wrapper = recorder.gameObject.GetComponent<RenderWrapper>();
    m_PreCaptureSuperSampling = wrapper.SuperSampling;
    wrapper.SuperSampling = m_SuperSampling;

#if USD_SUPPORTED
    // Read from the Usd serializer if we're recording offline.  Write to it otherwise.
    m_UsdPathSerializer = usdPathSerializer;
    if (!offlineRender) {
      m_UsdPath = SaveLoadScript.m_Instance.SceneFile.Valid ?
                  Path.ChangeExtension(filePath, "usda") : null;
      m_RecordingStopwatch = new System.Diagnostics.Stopwatch();
      m_RecordingStopwatch.Start();
      if (!m_UsdPathSerializer.StartRecording()) {
        UnityEngine.Object.Destroy(m_UsdPathSerializer);
        m_UsdPathSerializer = null;
      }
    } else {
      recorder.SetCaptureFramerate(Mathf.RoundToInt(App.UserConfig.Video.OfflineFPS));
      m_UsdPath = null;
      if (m_UsdPathSerializer.Load(App.Config.m_VideoPathToRender)) {
        m_UsdPathSerializer.StartPlayback();
      } else {
        UnityEngine.Object.Destroy(m_UsdPathSerializer);
        m_UsdPathSerializer = null;
      }
    }
#endif

    return true;
  }

  static public void StopVideoCapture(bool saveCapture) {
    // Debug reset changes to quality settings.
    if (m_DebugVideoCaptureQualityLevel != -1) {
      QualityControls.m_Instance.QualityLevel = m_PreCaptureQualityLevel;
    }

    App.VrSdk.SetHmdScalingFactor(1.0f);

    // Stop capturing, reset colors
    m_ActiveVideoRecording.gameObject.GetComponent<RenderWrapper>().SuperSampling =
        m_PreCaptureSuperSampling;
    m_ActiveVideoRecording.StopCapture(save: saveCapture);

#if USD_SUPPORTED
    if (m_UsdPathSerializer != null) {
      bool wasRecording = m_UsdPathSerializer.IsRecording;
      m_UsdPathSerializer.Stop();
      if (wasRecording) {
        m_RecordingStopwatch.Stop();
        if (!string.IsNullOrEmpty(m_UsdPath)) {
          if (App.UserConfig.Video.SaveCameraPath && saveCapture) {
            m_UsdPathSerializer.Save(m_UsdPath);
            CreateOfflineRenderBatchFile(SaveLoadScript.m_Instance.SceneFile.FullPath, m_UsdPath);
          }
        }
      }
    }

    m_UsdPathSerializer = null;
    m_RecordingStopwatch = null;
#endif

    m_ActiveVideoRecording = null;
    App.Switchboard.TriggerVideoRecordingStopped();
  }

  /// Creates a batch file the user can execute to make a high quality re-render of the video that
  /// has just been recorded.
  static void CreateOfflineRenderBatchFile(string sketchFile, string usdaFile) {
    string batFile = Path.ChangeExtension(usdaFile, ".HQ_Render.bat");
    var pathSections = Application.dataPath.Split('/').ToArray();
    var exePath = String.Join("/", pathSections.Take(pathSections.Length - 1).ToArray());

    // For the reader:
    // In order for this function to generate a functional .bat file, this string needs to be
    // updated to reflect the path to the application. For example, if you've distributed your
    // app to Steam, this might be:
    //   "C:/Program Files (x86)/Steam/steamapps/common/Tilt Brush/TiltBrush.exe"
    // But if you're building locally, this should point to your standalone executable that you
    // created, something like:
    //   "C:/src/Builds/Windows_SteamVR_Release/TiltBrush.exe"
    string offlineRenderExePath = "Change me to the path to the .exe";

    string batText = string.Format(
        "@\"{0}/Support/bin/renderVideo.cmd\" ^\n\t\"{1}\" ^\n\t\"{2}\" ^\n\t\"{3}\"",
        exePath, sketchFile, usdaFile, offlineRenderExePath);
    File.WriteAllText(batFile, batText);
  }
}

} // namespace TiltBrush
