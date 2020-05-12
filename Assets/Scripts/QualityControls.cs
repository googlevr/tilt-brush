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
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace TiltBrush {

using BloomMode = AppQualitySettingLevels.BloomMode;

public class QualityControls : MonoBehaviour {
  static public QualityControls m_Instance;
  private const string kAutoSimplificationEnabled = "Autosimplification Enabled";

  private List<Camera> m_Cameras;
  private List<SENaturalBloomAndDirtyLens> m_Bloom;
  private List<FXAA> m_Fxaa;
  private List<MobileBloom> m_MobileBloom;
  private float m_MobileBloomAmount;

  public event Action<int> OnQualityLevelChange;

  public RdpStrokeSimplifier StrokeSimplifier { get; private set; }
  public RdpStrokeSimplifier UserStrokeSimplifier { get; private set; }

  /// Non-active cameras need to be explicitly registered by the user.
  /// These special case cameras are ignored on mobile hardware.
  [SerializeField] private List<Camera> m_OptInCamerasForPC;

  /// Cameras can explicitly opt out of this manipulation.
  /// These are ignored on mobile hardware.
  [SerializeField] private List<Camera> m_OptOutCamerasForPC;

  [SerializeField] private bool m_enableHdr = true;
  [SerializeField] private int m_msaaLevel = 1;

  [SerializeField] private AppQualitySettingLevels m_QualityLevels;
  [UsedImplicitly] // on Android
  [SerializeField] private AppQualitySettingLevels m_MobileQualityLevels;
  [SerializeField] private GpuTextRender m_DebugText;

  /// Used to track when quality level actually changes.
  private int m_lastQualityLevel = -1;

  private int m_targetMaxControlPoints = 500000;
  private float m_maxLoadingSimplification = 150f;

  private Queue<double> m_FrameTimeStamps;
  private double m_TimeSinceStart;
  private int m_FramesInLastSecond;

  private int m_NumFramesFpsTooLow;
  private int m_NumFramesFpsHighEnough;
  private int m_NumFramesGpuTooHigh;
  private int m_NumFramesGpuLowEnough;
  private float m_DesiredBloom;

  /// A number from 0 (mobile, lowest) to 3 (future, highest)
  public int QualityLevel {
    get { return QualitySettings.GetQualityLevel(); }
    set { SetQualityLevel(value); }
  }

  public static bool AutosimplifyEnabled {
    get { return PlayerPrefs.GetInt(kAutoSimplificationEnabled, 1) == 1; }
    set { PlayerPrefs.SetInt(kAutoSimplificationEnabled, value ? 1 : 0); }
  }

  public float SimplificationLevel {
    get { return (StrokeSimplifier == null) ? 0.0f : StrokeSimplifier.Level; }
    set {
      float level = value;
      if (App.UserConfig.Profiling.HasStrokeSimplification) {
        level = App.UserConfig.Profiling.StrokeSimplification;
        Debug.LogFormat("Simplification overridden to be: {0}.", level);
      }
      StrokeSimplifier = new RdpStrokeSimplifier(level);
      UserStrokeSimplifier = new RdpStrokeSimplifier(
          Mathf.Min(level, AppQualityLevels[QualityLevel].MaxSimplificationUserStrokes));
    }
  }

  public int MSAALevel {
    get { return m_msaaLevel; }
  }

  public int FramesInLastSecond => m_FramesInLastSecond;

  public RenderTextureFormat FramebufferFormat {
    get {
      return m_enableHdr ? RenderTextureFormat.DefaultHDR
                         : RenderTextureFormat.ARGB32;
    }
  }
  public AppQualitySettingLevels AppQualityLevels
  {
    get
    {
#if UNITY_ANDROID
      return m_MobileQualityLevels;
#else
      return m_QualityLevels;
#endif
    }
  }

  public AppQualitySettingLevels.AppQualitySettings AppQualitySettings {
    get { return AppQualityLevels[QualityLevel];  }
  }

  void Awake() {
    m_Instance = this;

    m_Cameras = new List<Camera>();
    m_Bloom = new List<SENaturalBloomAndDirtyLens>();
    m_Fxaa = new List<FXAA>();
    m_MobileBloom = new List<MobileBloom>();

    // Simple desktop vs. mobile quality for now.  May need more control if e.g.
    // we need to set this differently for Win vs. Linux, or mobile level fragments
    // into bloom and non-bloom variants.
    int newLevel = App.Config.IsMobileHardware ? AppQualityLevels.Length - 1 : 2;

    // Override from user config, if valid.
    int configQuality = App.UserConfig.Profiling.QualityLevel;
    if (configQuality >= 0 && configQuality <= AppQualityLevels.Length) {
      newLevel = configQuality;
    }

    // Apply the quality level.
    QualityLevel = newLevel;
    SimplificationLevel = 0.0f;
  }

  /// Should be called after correct cameras are enabled
  public void Init() {
    m_Bloom.Clear();
    m_Fxaa.Clear();
    m_MobileBloom.Clear();

    var cameras = new HashSet<Camera>(
        FindObjectsOfType<Camera>(), new ReferenceComparer<Camera>());
    if (!App.Config.IsMobileHardware) {
      cameras.UnionWith(m_OptInCamerasForPC);
      cameras.ExceptWith(m_OptOutCamerasForPC);
    }
    m_Cameras = cameras.Where(x => x.tag != "Ignore").ToList();

    foreach (var camera in m_Cameras) {
      var rBloom = camera.GetComponent<SENaturalBloomAndDirtyLens>();
      if (rBloom) {
        m_Bloom.Add(rBloom);
      }
      var rFxaa = camera.GetComponent<FXAA>();
      if (rFxaa) {
        m_Fxaa.Add(rFxaa);
      }
      var mobileBloom = camera.GetComponent<MobileBloom>();
      if (mobileBloom) {
        m_MobileBloom.Add(mobileBloom);
      }
    }

    m_FrameTimeStamps = new Queue<double>();

    m_MobileBloomAmount = 0;
    m_DesiredBloom = 1;

    // Set up the OVR overlay for the dynamic quality debug readout.
#if OCULUS_SUPPORTED
    if (m_DebugText && m_DebugText.gameObject.activeInHierarchy
                    && App.Config.m_SdkMode == SdkMode.Oculus) {
      OVROverlay overlay = m_DebugText.gameObject.AddComponent<OVROverlay>();
      overlay.textures = new Texture[] { m_DebugText.RenderedTexture };
      overlay.isDynamic = true;
    }
#endif // OCULUS_SUPPORTED

    // Push current level to camera settings.
    SetQualityLevel(QualityLevel);
  }

  void Update() {
    if (!App.Config.IsMobileHardware) {
      return;
    }

    // Count actual frames in the last second
    m_TimeSinceStart += Time.deltaTime;
    m_FrameTimeStamps.Enqueue(m_TimeSinceStart);
    m_FramesInLastSecond++;
    double oneSecondAgo = m_TimeSinceStart - 1;
    while (m_FrameTimeStamps.Peek() <= oneSecondAgo) {
      m_FrameTimeStamps.Dequeue();
      m_FramesInLastSecond--;
    }

    // Update the counts for fps / gpu levels high or low
    int fps = m_FramesInLastSecond;
    if (fps <= AppQualityLevels.LowerQualityFpsTrigger) {
      m_NumFramesFpsTooLow++;
    } else {
      m_NumFramesFpsTooLow = 0;
    }

    if (fps >= AppQualityLevels.HigherQualityFpsTrigger) {
      m_NumFramesFpsHighEnough++;
    } else {
      m_NumFramesFpsHighEnough = 0;
    }

    float gpuUtilization = App.VrSdk.GetGpuUtilization() * 100f;
    if (gpuUtilization >= AppQualityLevels.LowerQualityGpuTrigger) {
      m_NumFramesGpuTooHigh++;
    } else {
      m_NumFramesGpuTooHigh = 0;
    }

    if (gpuUtilization <= AppQualityLevels.HigherQualityGpuTrigger) {
      m_NumFramesGpuLowEnough++;
    } else {
      m_NumFramesGpuLowEnough = 0;
    }

    // Update quality level if needed
    int limit = AppQualityLevels.FramesForLowerQuality;
    if (m_NumFramesFpsTooLow >= limit) {
      if (QualityLevel > 0) {
        QualityLevel--;
      }
      m_NumFramesFpsTooLow = 0;
    }

    if (m_NumFramesGpuTooHigh >= limit) {
      if (QualityLevel > 0) {
        QualityLevel--;
      }
      m_NumFramesGpuTooHigh = 0;
    }

    limit = AppQualityLevels.FramesForHigherQuality;
    if (m_NumFramesGpuLowEnough >= limit &&  m_NumFramesFpsHighEnough >= limit) {
      if (QualityLevel < AppQualityLevels.Length - 1) {
        QualityLevel++;
      }
      m_NumFramesGpuLowEnough = 0;
      m_NumFramesFpsHighEnough = 0;
    }

    // Update the mobile bloom level for fade in / out
    if (m_MobileBloomAmount != m_DesiredBloom) {
      float change = (Mathf.Sign(m_DesiredBloom - m_MobileBloomAmount) * Time.deltaTime)
                     / AppQualityLevels.BloomFadeTime;
      m_MobileBloomAmount = Mathf.Clamp01(m_MobileBloomAmount + change);
      foreach (var bloom in m_MobileBloom) {
        bloom.enabled = true;
        bloom.BloomAmount = m_MobileBloomAmount;
      }
      if (m_MobileBloomAmount == m_DesiredBloom) {
        foreach (var bloom in m_MobileBloom) {
          bloom.enabled = m_DesiredBloom == 1;
        }
      }
    }

    if (m_DebugText != null && m_DebugText.gameObject.activeInHierarchy
                            && App.Config.m_SdkMode == SdkMode.Oculus) {
      m_DebugText.SetData(0, fps);
      m_DebugText.SetData(1, gpuUtilization);
      m_DebugText.SetData(2, QualityLevel);
      m_DebugText.SetData(3, m_NumFramesFpsHighEnough);
      m_DebugText.SetData(4, AppQualityLevels.HigherQualityFpsTrigger);
      m_DebugText.SetData(5, m_NumFramesGpuLowEnough);
      m_DebugText.SetData(6, AppQualityLevels.HigherQualityGpuTrigger);
      m_DebugText.SetData(7, m_NumFramesFpsTooLow);
      m_DebugText.SetData(8, AppQualityLevels.LowerQualityFpsTrigger);
      m_DebugText.SetData(9, m_NumFramesGpuTooHigh);
      m_DebugText.SetData(10, AppQualityLevels.LowerQualityGpuTrigger);
    }
  }

  void SetQualityLevel(int value) {
    AppQualitySettingLevels settingLevels = AppQualityLevels;
    var settings = new AppQualitySettingLevels.AppQualitySettings();
    if (settingLevels == null) {
      Debug.LogError("Main -> App -> QualityControl -> QualityLevels object not set.");
    } else {
      settings = settingLevels[value];
    }

    SetBloomMode(settings.Bloom);
    EnableHDR(settings.Hdr);
    EnableFxaa(settings.Fxaa);
    Shader.globalMaximumLOD = settings.MaxLod;
    m_msaaLevel = settings.MsaaLevel;
    QualitySettings.anisotropicFiltering = settings.Anisotropic;
    SimplificationLevel = settings.StrokeSimplification;
    m_targetMaxControlPoints = settings.TargetMaxControlPoints;
    m_maxLoadingSimplification = settings.MaxSimplification;

    float viewportScale = App.UserConfig.Profiling.ViewportScaling > 0 ?
        App.UserConfig.Profiling.ViewportScaling :
        settings.ViewportScale;

    float eyeScale = App.UserConfig.Profiling.EyeTextureScaling > 0 ?
        App.UserConfig.Profiling.EyeTextureScaling :
        settings.EyeTextureScale;

    if (App.UserConfig.Profiling.GlobalMaximumLOD > 0) {
      Shader.globalMaximumLOD = App.UserConfig.Profiling.GlobalMaximumLOD;
    }

    if (App.UserConfig.Profiling.MsaaLevel > 0) {
      m_msaaLevel = App.UserConfig.Profiling.MsaaLevel;
    }

    UnityEngine.XR.XRSettings.renderViewportScale = viewportScale;
    UnityEngine.XR.XRSettings.eyeTextureResolutionScale = eyeScale;

    if (value != m_lastQualityLevel && Debug.isDebugBuild && App.UserConfig.Profiling.AutoProfile) {
      Debug.Log("Profile: Quality Level: " + value
        + " renderViewportScale: " + viewportScale
        + " eyeTexture scale: " + eyeScale
        + " MSAA: " + m_msaaLevel
        + " GlobalMaximumLOD: " + Shader.globalMaximumLOD);
      m_lastQualityLevel = value;
    }

    App.VrSdk.SetGpuClockLevel(AppQualitySettings.GpuLevel);
    App.VrSdk.SetFixedFoveation(AppQualitySettings.FixedFoveationLevel);

    QualitySettings.SetQualityLevel(value, applyExpensiveChanges: !App.Config.IsMobileHardware);

    if (OnQualityLevelChange != null) {
      OnQualityLevelChange(value);
    }
  }

  void SetBloomMode(BloomMode rMode) {
    for (int i = 0; i < m_Bloom.Count; ++i) {
      m_Bloom[i].enabled = (rMode == BloomMode.Full || rMode == BloomMode.Fast);
    }

    m_DesiredBloom = rMode == BloomMode.None ? 0 : 1;
  }

  void EnableFxaa(bool bEnable) {
    foreach (var fxaa in m_Fxaa) {
      fxaa.enabled = bEnable;
    }
  }

  void EnableHDR(bool bEnable) {
    m_enableHdr = bEnable;
    foreach (var camera in m_Cameras) {
      if (camera.gameObject.activeSelf) {
        camera.allowHDR = bEnable;
      }
    }

    App.VrSdk.GetVrCamera().allowHDR = bEnable;
  }

  public void ResetAutoQuality() {
    /* a no-op for now, since we don't do any auto quality scaling */
  }

  /// Counts the number of control points in a sketch and estimates a simplification level in an
  /// attempt to keep the framerate high.
  public void AutoAdjustSimplifierLevel(List<Stroke> strokes, Guid[] brushes) {
    if (App.UserConfig.Profiling.HasStrokeSimplification) {
      Debug.LogFormat("Simplification overridden to be: {0}.",
                      App.UserConfig.Profiling.StrokeSimplification);
      return;
    }

    Dictionary<Guid, int> controlPointCount = brushes.Distinct().ToDictionary(x => x, x => 0);
    foreach (var stroke in strokes) {
      controlPointCount[stroke.m_BrushGuid] += stroke.m_ControlPoints.Length;
    }
    float total = 0;
    foreach (var pair in controlPointCount) {
      total += pair.Value * AppQualityLevels.GetWeightForBrush(pair.Key);
    }
    if (total < m_targetMaxControlPoints) {
      Debug.LogFormat("Complexity ({0}) is less than {1}. No extra simplification required.",
                      total, m_targetMaxControlPoints);
      return;
    }
    float reduction = m_targetMaxControlPoints / total;
    float level = Mathf.Max(Mathf.Min(RdpStrokeSimplifier.CalculateLevelForReduction(reduction),
                                      m_maxLoadingSimplification), StrokeSimplifier.Level);
    if (AutosimplifyEnabled) {
     Debug.LogFormat(
         "Complexity ({0}) is greater than {1}. Reduction of {2} using level {3} simplification.",
                    total, m_targetMaxControlPoints, reduction, level);
      SimplificationLevel = level;
    }
  }
}
}  // namespace TiltBrush
