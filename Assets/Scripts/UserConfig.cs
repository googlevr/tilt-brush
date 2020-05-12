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
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TiltBrush {

// Use "struct" instead of "class" to prohibit the use of default values,
// which won't work the way you want. Use Nullable<> instead.
[Serializable]
public class UserConfig {
  [Serializable] public struct YouTubeConfig {
    public string ChannelID;
  }
  public YouTubeConfig YouTube;

  [Serializable] public struct FlagsConfig {
    public bool DisableAudio;
    public bool DisableAutosave;
    public bool DisablePoly;
    public bool UnlockScale;
    public bool GuideToggleVisiblityOnly;
    public bool HighResolutionSnapshots; // Deprecated
    public bool ShowDroppedFrames;

    bool? m_ShowDangerousBrushes;
    public bool ShowDangerousBrushes {
      get {
#if UNITY_EDITOR
        return m_ShowDangerousBrushes ?? true;
#else
        return m_ShowDangerousBrushes ?? false;
#endif
      }
      set { m_ShowDangerousBrushes = value; }
    }

    bool? m_PostEffectsOnCapture;
    public bool PostEffectsOnCaptureValid { get { return m_PostEffectsOnCapture != null; } }
    public bool PostEffectsOnCapture {
      get { return m_PostEffectsOnCapture ?? true; }
      set { m_PostEffectsOnCapture = value; }
    }

    // Spectator-cam watermark doesn't update on the fly
    // The snapshot/video watermark does, though
    bool? m_ShowWatermark;
    public bool ShowWatermarkValid { get { return m_ShowWatermark != null; } }
    public bool ShowWatermark {
      get { return m_ShowWatermark ?? true; }
      set { m_ShowWatermark = value; }
    }

    // This doesn't update on the fly
    bool? m_ShowHeadset;
    public bool ShowHeadset {
      get { return m_ShowHeadset ?? true; }
      set { m_ShowHeadset = value; }
    }

    // This doesn't update on the fly
    bool? m_ShowControllers;
    public bool ShowControllers {
      get { return m_ShowControllers ?? true; }
      set { m_ShowControllers = value; }
    }

    int? m_SnapshotHeight;
    public int SnapshotHeight {
      get { return m_SnapshotHeight ?? -1; }
      set {
        int max = App.Config.PlatformConfig.MaxSnapshotDimension;
        if (value > max) {
          OutputWindowScript.Error(
              $"Snapshot height of {value} is not supported. Set to {max} pixels.");
          m_SnapshotHeight = max;
        } else {
          m_SnapshotHeight = value;
        }
      }
    }

    int? m_SnapshotWidth;
    public int SnapshotWidth {
      get { return m_SnapshotWidth ?? -1; }
      set {
        int max = App.Config.PlatformConfig.MaxSnapshotDimension;
        if (value > max) {
          OutputWindowScript.Error(
              $"Snapshot width of {value} is not supported. Set to {max} pixels.");
          m_SnapshotWidth = max;
        } else {
          m_SnapshotWidth = value;
        }
      }
    }

    float? m_Fov;
    public bool FovValid { get { return m_Fov != null; } }
    public float Fov {
      get { return m_Fov ?? CameraConfig.kFovDefault; }
      set {
        m_Fov = UnityEngine.Mathf.Clamp(value, CameraConfig.kFovMin, CameraConfig.kFovMax);
        if (m_Fov != value) {
          OutputWindowScript.Error(string.Format("FOV of '{0}' not supported.", value),
              string.Format("FOV must be between {0} and {1}.\nFOV set to {2}.",
              CameraConfig.kFovMin, CameraConfig.kFovMax, m_Fov));
        }
      }
    }

    private bool? m_PolyModelPreload;
    public bool PolyModelPreloadValid => m_PolyModelPreload.HasValue;
    public bool PolyModelPreload {
      get {
        return  m_PolyModelPreload ?? App.PlatformConfig.EnablePolyPreload;
      }
      set { m_PolyModelPreload = value; }
    }
  }

  public FlagsConfig Flags;

  [Serializable] public struct DemoConfig {
    public bool Enabled;
    public uint? Duration;
    public bool PublishAutomatically;

    private string m_PublishTitle;
    public string PublishTitle {
      get { return m_PublishTitle ?? $"Sketch from {App.kAppDisplayName} demo"; }
      set { m_PublishTitle = value; }
    }

    private string m_PublishDescription;
    public string PublishDescription {
      get { return m_PublishDescription ?? ""; }
      set { m_PublishDescription = value; }
    }
  }
  public DemoConfig Demo;

  [Serializable] public struct ExportConfig {
    bool? m_ExportBinaryFbx;
    public bool ExportBinaryFbx {
      get { return m_ExportBinaryFbx ?? true; }
      set { m_ExportBinaryFbx = value; }
    }

    string m_ExportFbxVersion;
    public string ExportFbxVersion {
      get { return m_ExportFbxVersion ?? "FBX201400"; }
      set { m_ExportFbxVersion = value; }
    }

    bool? m_ExportStrokeTimestamp;
    public bool ExportStrokeTimestamp {
      get { return m_ExportStrokeTimestamp ?? true; }
      set { m_ExportStrokeTimestamp = value; }
    }
  }
  public ExportConfig Export;

  [Serializable] public struct SharingConfig {
    // For Poly testing allow us to use a different API host and landing page URL.
    [JsonProperty("VrAssetServiceHost")] public string VrAssetServiceHostOverride;
    [JsonProperty("VrAssetServiceUrl")] public string VrAssetServiceUrlOverride;
  }
  public SharingConfig Sharing;

  [Serializable] public struct IdentityConfig {
    public string Author;
  }
  public IdentityConfig User;

  [Serializable] public struct VideoConfig {
    // Default values and limits.
    private const float kDefaultFps = 30f;
    private const float kDefaultOfflineFps = 60f;
    private const float kMinFps = 1f;
    private const float kMaxFps = 60f;
    private const float kMaxOfflineFps = 1000f;
    private const int kDefaultRes = 1280;
    private const int kDefaultOfflineRes = 1920;
    private const int kMinRes = 640;
    private const int kMaxRes = 2560;
    private const int kMaxOfflineRes = 8000;
    private const string kDefaultContainer = "mp4";
    private static readonly List<string> kSupportedContainers = new List<string> {
      "mp4", "mov", "avi", "mpeg", "ogv", "ogx",
    };

    private const string kDefaultVideoEncoder = "h.264";
    private static readonly List<string> kSupportedVideoEncoders = new List<string> {
      "h.264", "h.265",
    };

    private const float kDefaultSmoothing = 0.98f;
    private const float kDefaultOdsPoleCollapsing = 1.0f;

    float? m_FPS;
    public float FPS {
      get { return m_FPS ?? kDefaultFps; }
      set {
        m_FPS = UnityEngine.Mathf.Clamp(value, kMinFps, kMaxFps);
        if (m_FPS != value) {
          OutputWindowScript.Error(string.Format("Video FPS of '{0}' not supported.", value),
              string.Format("FPS must be between {0} and {1}.\nFPS set to {2}.",
                  UnityEngine.Mathf.RoundToInt(kMinFps),
                  UnityEngine.Mathf.RoundToInt(kMaxFps), m_FPS));
        }
      }
    }

    float? m_OfflineFps;
    public float OfflineFPS {
      get { return m_OfflineFps ?? kDefaultOfflineFps; }
      set {
        m_OfflineFps = UnityEngine.Mathf.Clamp(value, kMinFps, kMaxOfflineFps);
        if (OfflineFPS != value) {
          OutputWindowScript.Error(string.Format("Offline Video FPS of '{0}' not supported.", value),
            string.Format("FPS must be between {0} and {1}.\nFPS set to {2}.",
              UnityEngine.Mathf.RoundToInt(kMinFps),
              UnityEngine.Mathf.RoundToInt(kMaxOfflineFps), OfflineFPS));
        }
      }
    }

    float? m_Fov;
    public bool FovValid { get { return m_Fov != null; } }
    public float Fov {
      get { return m_Fov ?? CameraConfig.kFovDefault; }
      set {
        m_Fov = UnityEngine.Mathf.Clamp(value, CameraConfig.kFovMin, CameraConfig.kFovMax);
        if (m_Fov != value) {
          OutputWindowScript.Error(string.Format("FOV of '{0}' not supported.", value),
              string.Format("FOV must be between {0} and {1}.\nFOV set to {2}.",
              CameraConfig.kFovMin, CameraConfig.kFovMax, m_Fov));
        }
      }
    }

    int? m_Resolution;
    public int Resolution {
      get { return m_Resolution ?? kDefaultRes; }
      set {
        m_Resolution = UnityEngine.Mathf.Clamp(value, kMinRes, kMaxRes);
        if (m_Resolution != value) {
          OutputWindowScript.Error(string.Format("Video Resolution of '{0}' not supported.", value),
            string.Format("Resolution must be between {0} and {1}.\nResolution set to {2}.",
                          kMinRes, kMaxRes, m_Resolution));
        }
      }
    }

    int? m_OfflineResolution;
    public int OfflineResolution {
      get { return m_OfflineResolution ?? kDefaultOfflineRes; }
      set {
        m_OfflineResolution = UnityEngine.Mathf.Clamp(value, 640, kMaxOfflineRes);
        if (m_OfflineResolution != value) {
          OutputWindowScript.Error(string.Format("Video Resolution of '{0}' not supported.", value),
              string.Format("Resolution must be between {0} and {1}.\nResolution set to {2}.",
                             kMinRes, kMaxOfflineRes, m_OfflineResolution));
        }
      }
    }

    private bool? m_SaveCameraPath;
    public bool SaveCameraPath {
      get { return m_SaveCameraPath ?? true; }
      set {
        m_SaveCameraPath = value;
      }
    }

    string m_VideoEncoder;
    public string Encoder {
      get { return m_VideoEncoder ?? kDefaultVideoEncoder; }
      set {
        string lowered = value.ToLowerInvariant();
        if (kSupportedVideoEncoders.Contains(lowered)) {
          m_VideoEncoder = lowered;
        } else {
          m_VideoEncoder = null;
          OutputWindowScript.Error(
              $"VideoEncoder '{lowered}' not supported in {App.kConfigFileName}",
              string.Format("Supported: {0}.\nContainer type set to {1}.",
                            string.Join(", ", kSupportedVideoEncoders.ToArray()),
                                              kDefaultVideoEncoder));
        }
      }
    }

    string m_ContainerType;
    public string ContainerType {
      get { return m_ContainerType ?? kDefaultContainer; }
      set {
        string lowered = value.ToLowerInvariant();
        if ( kSupportedContainers.Contains(lowered)) {
          m_ContainerType = lowered;
        } else {
          m_ContainerType = null;
          OutputWindowScript.Error(
              $"ContainerType '{lowered}' not supported in {App.kConfigFileName}",
              string.Format("Supported: {0}.\nContainer type set to {1}.",
                            string.Join(", ", kSupportedContainers.ToArray()), kDefaultContainer));
        }
      }
    }

    float? m_CameraSmoothing;
    public bool CameraSmoothingValid { get { return m_CameraSmoothing != null; } }
    public float CameraSmoothing {
      get { return m_CameraSmoothing ?? kDefaultSmoothing; }
      set {
        m_CameraSmoothing = UnityEngine.Mathf.Clamp01(value);
        if (m_CameraSmoothing != value) {
          OutputWindowScript.Error(string.Format("Camera smoothing of '{0}' not supported.", value),
              string.Format("Smoothing must be between 0 and 1.\nSmoothing set to {0}.",
                            m_CameraSmoothing));
        }
      }
    }

    float? m_OdsPoleCollapsing;
    public float OdsPoleCollapsing {
      get { return m_OdsPoleCollapsing ?? kDefaultOdsPoleCollapsing; }
      set {
        m_OdsPoleCollapsing = UnityEngine.Mathf.Clamp01(value);
        if (m_OdsPoleCollapsing != value) {
          OutputWindowScript.Error(string.Format("Pole Collapsing of '{0}' not supported.", value),
            string.Format("Smoothing must be between 0 and 1.\nPole Collapsing set to {0}.",
              m_OdsPoleCollapsing));
        }
      }
    }
  }

  public VideoConfig Video;

  // Settings for the QA testing panel.
  [Serializable] public struct TestingConfig {
    public Dictionary<Guid, Guid> BrushReplacementMap {
      get {
        Dictionary<Guid, Guid> results = new Dictionary<Guid, Guid>();
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
        if (Config.IsExperimental) {
          if (string.IsNullOrEmpty(BrushReplacements)) {
            return results;
          }
          var replacements = BrushReplacements.Split(',');
          foreach (string replacement in replacements) {
            string[] pair = replacement.Split('=');
            if (pair.Length == 2) {
              if (pair[0] == "*") {
                Guid guid = new Guid(pair[1]);
                foreach (var brush in App.Instance.m_Manifest.Brushes) {
                  results.Add(brush.m_Guid, guid);
                }
              } else {
                results.Add(new Guid(pair[0]), new Guid(pair[1]));
              }
            } else {
              OutputWindowScript.Error("BrushReplacement should be of the form:\n" +
                                       "brushguidA=brushguidB,brushguidC=brushguidD");
            }
          }
        }
#endif
        return results;
      }
    }

    public bool Enabled;
    public string InputFile;
    public string OutputFile;
    public bool ResetPromos;
    public bool FirstRun;
    public string BrushReplacements;
  }
  public TestingConfig Testing;

  // Profiling Settings
  [Serializable]
  public struct ProfilingConfig {
    public const int kDefaultScreenshotResolution = 1000;
    public string[] ProfilingFunctions { get; private set; }
    public ProfilingManager.Mode ProflingMode { get; private set; }

    public string Mode {
      set {
        try {
          ProflingMode = (ProfilingManager.Mode)Enum.Parse(typeof(ProfilingManager.Mode), value);
        } catch (ArgumentException) {
          OutputWindowScript.Error(string.Format("'{0}' is not a valid profiling mode.", value));
        }
      }
    }

    public string Functions {
      set { ProfilingFunctions = value.Split(','); }
    }

    public string ProfileName;
    public string ProfileFilename;
    public string SketchToLoad;
    public bool AutoProfile;
    public bool ShowControllers;

    public float Duration {
      get { return m_Duration.HasValue ? m_Duration.Value : 5f; }
      set { m_Duration = value; }
    }
    private float? m_Duration;

    public bool Csv;

    // Any invalid quality level is ignored.
    private int? m_QualityLevel;
    public int QualityLevel {
      get { return m_QualityLevel.HasValue ? m_QualityLevel.Value : -1; }
      set { m_QualityLevel = value; }
    }

    public float ViewportScaling { get; set; }
    public float EyeTextureScaling { get; set; }
    public int GlobalMaximumLOD { get; set; }
    public int MsaaLevel { get; set; }
    public bool TakeScreenshot { get; set; }
    private int? m_screenshotResolution;

    public int ScreenshotResolution {
      get {
        return m_screenshotResolution.HasValue ?
               m_screenshotResolution.Value : kDefaultScreenshotResolution;
      }
      set { m_screenshotResolution = value; }
    }
    public bool PerfgateOutput { get; set; }

    private float? m_StrokeSimplification;

    public float StrokeSimplification {
      get { return m_StrokeSimplification.HasValue ? m_StrokeSimplification.Value : 0f; }
      set { m_StrokeSimplification = value; }
    }

    public bool HasStrokeSimplification {
      get { return m_StrokeSimplification.HasValue; }
    }
  }
  public ProfilingConfig Profiling;
}

} // namespace TiltBrush
