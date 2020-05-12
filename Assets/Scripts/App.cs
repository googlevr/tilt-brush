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
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;

using Newtonsoft.Json;

#if USE_DOTNETZIP
using ZipSubfileReader = ZipSubfileReader_DotNetZip;
using ZipLibrary = Ionic.Zip;
#else
using ZipSubfileReader = TiltBrush.ZipSubfileReader_SharpZipLib;
using ZipLibrary = ICSharpCode.SharpZipLibUnityPort.Zip;
#endif

[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
namespace TiltBrush {
public class App : MonoBehaviour {
  // ------------------------------------------------------------
  // Constants and types
  // ------------------------------------------------------------

  public const float METERS_TO_UNITS = 10f;
  public const float UNITS_TO_METERS = .1f;

  // These control the naming of various things related to the app. If you distribute your own
  // build, you need to call is something other that 'Tilt Brush', as that if a Google trademark -
  // see the BRANDING file for details.
  // As a minimum, you should change kAppDisplayName.
  
  // This is the name of the app, as displayed to the users running it.
  public const string kAppDisplayName = "Open Source Tilt Brush";
  // The vendor name - used for naming android builds - shouldn't have spaces.
  public const string kVendorName = "SomeCompany";
  // The vendor name - used for the company name in builds and fbx output. Can have spaces.
  public const string kDisplayVendorName = "Some Company";
  // This is the App name used when speaking to Google services
  public const string kGoogleServicesAppName = kAppDisplayName;
  // The name of the configuration file. You may want to change this if you think your users may
  // want to have a different config file for your edition of the app.
  public const string kConfigFileName = "Tilt Brush.cfg";
  // The name of the App folder (In the user's Documents folder) - you may want to share this with
  // the original Tilt Brush, or not.
  public const string kAppFolderName = "Tilt Brush";
  // The data folder used on Google Drive.
  public const string kDriveFolderName = kAppDisplayName;
  // Executable Base
  public const string kGuiBuildExecutableName = "OpenSourceTiltBrush";
  // Windows Executable
  public const string kGuiBuildWindowsExecutableName = kGuiBuildExecutableName + ".exe";
  // OSX Executable
  public const string kGuiBuildOSXExecutableName = kGuiBuildExecutableName + ".app";
  // Android Executable
  public const string kGuiBuildAndroidExecutableName =
      "com." + kVendorName + "." + kGuiBuildExecutableName + ".apk";

  public const string kPlayerPrefHasPlayedBefore = "Has played before";
  public const string kReferenceImagesSeeded = "Reference Images seeded";

  private const string kDefaultConfigPath = "DefaultConfig";

  private const int kHttpListenerPort = 40074;
  private const string kProtocolHandlerPrefix = "tiltbrush://remix/";
  private const string kFileMoveFilename = "WhereHaveMyFilesGone.txt";

  private const string kFileMoveContents = 
      "All your " + kAppDisplayName + " files have been moved to\n" + 
      "/sdcard/" + kAppFolderName + ".\n";

  public enum AppState {
    Error,
    LoadingBrushesAndLighting,
    FadeFromBlack,
    FirstRunIntro,
    Intro,
    Loading,
    QuickLoad,
    Standard,
    MemoryExceeded,
    Saving,
    Reset,
    Uploading,
    AutoProfiling,
    OfflineRendering,
  }

  // ------------------------------------------------------------
  // Static API
  // ------------------------------------------------------------

  private static App m_Instance;

  // Accessible at all times after config is initialized.
  public static Config Config {
    get { return Config.m_SingletonState; }
  }

  public static UserConfig UserConfig {
    get { return m_Instance.m_UserConfig; }
  }

  public static PlatformConfig PlatformConfig {
    get { return Config.PlatformConfig; }
  }

  public static VrSdk VrSdk {
    get { return m_Instance.m_VrSdk; }
  }

  public static SceneScript Scene {
    get { return m_Instance.m_SceneScript; }
  }

  public static CanvasScript ActiveCanvas {
    get { return Scene.ActiveCanvas; }
  }

  public static PolyAssetCatalog PolyAssetCatalog {
    get { return m_Instance.m_PolyAssetCatalog; }
  }

  public static Switchboard Switchboard {
    get { return m_Instance.m_Switchboard; }
  }

  public static BrushColorController BrushColor {
    get { return m_Instance.m_BrushColorController; }
  }

  public static GroupManager GroupManager {
    get { return m_Instance.m_GroupManager; }
  }

  public static HttpServer HttpServer => m_Instance.m_HttpServer;

  public static DriveAccess DriveAccess => m_Instance.m_DriveAccess;
  public static DriveSync DriveSync => m_Instance.m_DriveSync;

  public static OAuth2Identity GoogleIdentity => m_Instance.m_GoogleIdentity;
  public static OAuth2Identity SketchfabIdentity => m_Instance.m_SketchfabIdentity;

  public static GoogleUserSettings GoogleUserSettings => m_Instance.m_GoogleUserSettings;

  /// Returns the App instance, or null if the app has not been initialized
  /// with Awake().  Note that the App may not have had Start() called yet.
  ///
  /// Do not modify the script execution order if you only need inspector
  /// data from App.Instance. Put the inspector data in App.Config instead.
  public static App Instance {
    get { return m_Instance; }
#if UNITY_EDITOR
    // Bleh. Needed by BuildTiltBrush.cs
    set { m_Instance = value; }
#endif
  }

  public static AppState CurrentState {
    get {
      return m_Instance == null ? AppState.Loading : m_Instance.m_CurrentAppState;
    }
  }

  public static OAuth2Identity GetIdentity(Cloud cloud) {
    switch (cloud) {
      case Cloud.Poly: return GoogleIdentity;
      case Cloud.Sketchfab: return SketchfabIdentity;
      default: throw new InvalidOperationException($"No identity for {cloud}");
    }
  }

  // ------------------------------------------------------------
  // Events
  // ------------------------------------------------------------

  public event Action<AppState, AppState> StateChanged;

  // ------------------------------------------------------------
  // Inspector data
  // ------------------------------------------------------------
  // Unless otherwise stated, intended to be read-only even if public

  [Header("External References")]
  [SerializeField] VrSdk m_VrSdk;
  [SerializeField] SceneScript m_SceneScript;

  [Header("General inspector")]
  [SerializeField] float m_FadeFromBlackDuration;
  [SerializeField] float m_QuickLoadHintDelay = 2f;

  [SerializeField] GpuIntersector m_GpuIntersector;

  public TiltBrushManifest m_Manifest;
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
  [SerializeField] private TiltBrushManifest m_ManifestExperimental;
#endif

  [SerializeField] private SelectionEffect m_SelectionEffect;

  /// The root object for the "Room" coordinate system
  public Transform m_RoomTransform { get { return transform; } }
  /// The root object for the "Scene" coordinate system ("/SceneParent")
  public Transform m_SceneTransform;
  /// The root object for the "Canvas" coordinate system ("/SceneParent/Canvas")
  /// TODO: remove, in favor of .ActiveCanvas.transform
  public Transform m_CanvasTransform;
  /// The object "/SceneParent/EnvironmentParent"
  public Transform m_EnvironmentTransform;
  [SerializeField] GameObject m_SketchSurface;
  [SerializeField] GameObject m_ErrorDialog;
  [SerializeField] GameObject m_OdsPrefab;
  GameObject m_OdsPivot;

  [Header("Intro")]
  [SerializeField] float m_IntroSketchFadeInDuration = 5.0f;
  [SerializeField] float m_IntroSketchFadeOutDuration = 1.5f;
  [SerializeField] float m_IntroSketchMobileFadeInDuration = 3.0f;
  [SerializeField] float m_IntroSketchMobileFadeOutDuration = 1.5f;

  [SerializeField] FrameCountDisplay m_FrameCountDisplay;

  [SerializeField] private GameObject m_ShaderWarmup;

  [Header("Identities")]
  [SerializeField] private OAuth2Identity m_GoogleIdentity;
  [SerializeField] private OAuth2Identity m_SketchfabIdentity;

  // ------------------------------------------------------------
  // Private data
  // ------------------------------------------------------------

  /// Use C# event in preference to Unity callbacks because
  /// Unity doesn't send callbacks to disabled objects
  public event Action AppExit;

  private Queue m_RequestedTiltFileQueue = Queue.Synchronized(new Queue());
  private HttpServer m_HttpServer;

  private SketchSurfacePanel m_SketchSurfacePanel;
  private UserConfig m_UserConfig;
  private string m_UserPath;
  private string m_OldUserPath;

  private PolyAssetCatalog m_PolyAssetCatalog;
  private Switchboard m_Switchboard;
  private BrushColorController m_BrushColorController;
  private GroupManager m_GroupManager;

  /// Time origin of sketch in seconds for case when drawing is not sync'd to media.
  private double m_sketchTimeBase = 0;
  private float m_AppStateCountdown;
  private float m_QuickLoadHintCountdown;
  private bool m_QuickLoadInputWasValid;
  private bool m_QuickLoadEatInput;
  private AppState m_CurrentAppState;
  // Temporary: to narrow down b/37256058
  private AppState m_DesiredAppState_;
  private AppState m_DesiredAppState {
    get { return m_DesiredAppState_; }
    set {
      if (m_DesiredAppState_ != value) {
        Console.WriteLine("State <- {0}", value);
      }
      m_DesiredAppState_ = value;
    }
  }
  private int m_TargetFrameRate;
  private float m_RoomRadius;
  private bool m_AutosaveRestoreFileExists;
  private bool m_ShowAutosaveHint = false;
  private bool? m_ShowControllers;
  private int m_QuickloadStallFrames;

  private GameObject m_IntroSketch;
  private Renderer[] m_IntroSketchRenderers;
  private float m_IntroFadeTimer;

  private bool m_FirstRunExperience;
  private bool m_RequestingAudioReactiveMode;
  private DriveAccess m_DriveAccess;
  private DriveSync m_DriveSync;
  private GoogleUserSettings m_GoogleUserSettings;

  // ------------------------------------------------------------
  // Properties
  // ------------------------------------------------------------

  /// Time spent in current sketch, in seconds.
  /// On load, this is restored to the timestamp of the last stroke.
  /// Updated per-frame.
  public double CurrentSketchTime {
    // Unity's Time.time has useful precision probably <= 1ms, and unknown
    // drift/accuracy. It is a single (but is a double, internally), so its
    // raw precision drops to ~2ms after ~4 hours and so on.
    // Time.timeSinceLevelLoad is also an option.
    //
    // C#'s DateTime API has low-ish precision (10+ ms depending on OS)
    // but likely the highest accuracy with respect to wallclock, since
    // it's reading from an RTC.
    //
    // High-precision timers are the opposite: high precision, but are
    // subject to drift.
    //
    // For realtime sync, Time.time is probably the best thing to use.
    // For postproduction sync, probably C# DateTime.
    get {
      // If you change this, also modify SketchTimeToLevelLoadTime
      return Time.timeSinceLevelLoad - m_sketchTimeBase;
    }
    set {
      if (value < 0) { throw new ArgumentException("negative"); }
      m_sketchTimeBase = Time.timeSinceLevelLoad - value;
    }
  }

  public float RoomRadius {
    get { return m_RoomRadius; }
  }

  public SelectionEffect SelectionEffect {
    get { return m_SelectionEffect; }
  }
  public bool IsFirstRunExperience { get { return m_FirstRunExperience; } }
  public bool HasPlayedBefore {
    get;
    private set;
  }

  public bool StartupError { get; set; }

  public bool ShowControllers {
    get { return m_ShowControllers.GetValueOrDefault(true); }
    set {
      InputManager.m_Instance.ShowControllers(value);
      m_ShowControllers = value;
    }
  }

  public bool AutosaveRestoreFileExists {
    get { return m_AutosaveRestoreFileExists; }
    set {
      if (value != m_AutosaveRestoreFileExists) {
        try {
          string filePath = AutosaveRestoreFilePath();
          if (value) {
            var autosaveFile = File.Create(filePath);
            autosaveFile.Close();
          } else {
            if (File.Exists(filePath)) {
              File.Delete(filePath);
            }
          }
        }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }

        m_AutosaveRestoreFileExists = value;
      }
    }
  }

  public GpuIntersector GpuIntersector {
    get { return m_GpuIntersector; }
  }

  public TrTransform OdsHeadPrimary { get; set; }
  public TrTransform OdsScenePrimary { get; set; }

  public TrTransform OdsHeadSecondary { get; set; }
  public TrTransform OdsSceneSecondary { get; set; }

  public FrameCountDisplay FrameCountDisplay {
    get { return m_FrameCountDisplay; }
  }

  // ------------------------------------------------------------
  // Implementation
  // ------------------------------------------------------------

  public bool RequestingAudioReactiveMode {
    get { return m_RequestingAudioReactiveMode; }
  }

  public void ToggleAudioReactiveModeRequest() {
    m_RequestingAudioReactiveMode ^= true;
  }

  public void ToggleAudioReactiveBrushesRequest() {
    ToggleAudioReactiveModeRequest();
    AudioCaptureManager.m_Instance.CaptureAudio(m_RequestingAudioReactiveMode);
    VisualizerManager.m_Instance.EnableVisuals(m_RequestingAudioReactiveMode);
    Switchboard.TriggerAudioReactiveStateChanged();
  }

  public double SketchTimeToLevelLoadTime(double sketchTime) {
    return sketchTime + m_sketchTimeBase;
  }

  public void SetOdsCameraTransforms(TrTransform headXf, TrTransform sceneXf) {
    if (Config.m_SdkMode != SdkMode.Ods) { return; }
    OdsScenePrimary = sceneXf;
    OdsHeadPrimary = headXf;

    // To simplify down-stream code, copy primary into secondary.
    if (OdsHeadSecondary == new TrTransform()) {
      OdsHeadSecondary = headXf;
      OdsSceneSecondary = sceneXf;
    }
  }

  // Tilt Brush code assumes the current directory is next to the Support/
  // folder. Enforce that assumption
  static void SetCurrentDirectoryToApplication() {
    // dataPath is:
    //   Editor  - <project folder>/Assets
    //   Windows - TiltBrush_Data/
    //   Linux   - TiltBrush_Data/
    //   OSX     - TiltBrush.app/Contents/
#if UNITY_STANDALONE_WIN
    string oldDir = Directory.GetCurrentDirectory();
    string dataDir = UnityEngine.Application.dataPath;
    string appDir = Path.GetDirectoryName(dataDir);
    try {
      Directory.SetCurrentDirectory(appDir);
    } catch (Exception e) {
      Debug.LogErrorFormat("Couldn't set dir to {0}: {1}", appDir, e);
    }
    string curDir = Directory.GetCurrentDirectory();
    Debug.LogFormat("Dir {0} -> {1}", oldDir, curDir);
#endif
  }

  void CreateIntroSketch() {
    m_IntroSketch = Instantiate(PlatformConfig.IntroSketchPrefab);
    m_IntroSketchRenderers = m_IntroSketch.GetComponentsInChildren<Renderer>();
    for (int i = 0; i < m_IntroSketchRenderers.Length; ++i) {
      m_IntroSketchRenderers[i].material.SetFloat("_IntroDissolve", 1);
      m_IntroSketchRenderers[i].material.SetFloat("_GreyScale", 0);
    }
  }

  void DestroyIntroSketch() {
    Destroy(m_IntroSketch);
    m_IntroSketchRenderers = null;

    // Eject the (rather large) intro sketch from memory.
    // TODO: The Unity Way would be to put these prefab references and instantiations
    // in an additive scene.
    // Don't do this in the editor, because it mutates the asset on disk!
#if !UNITY_EDITOR
    PlatformConfig.IntroSketchPrefab = null;
#endif
    Resources.UnloadUnusedAssets();
  }

  static string GetStartupString() {
    string stamp = Config.m_BuildStamp;
#if UNITY_ANDROID
    stamp += string.Format(" code {0}", AndroidUtils.GetVersionCode());
#endif
#if DEBUG
    stamp += string.Format(" platcfg {0}", PlatformConfig.name);
#endif
    return $"{App.kAppDisplayName} {Config.m_VersionNumber}\nBuild {stamp}";
  }

  void Awake() {
    m_Instance = this;
    Debug.Log(GetStartupString());

    // Begone, physics! You were using 0.3 - 1.3ms per frame on Quest!
    Physics.autoSimulation = false;

    // See if this is the first time
    HasPlayedBefore = PlayerPrefs.GetInt(kPlayerPrefHasPlayedBefore, 0) == 1;

    // Copy files into Support directory
    CopySupportFiles();

    InitUserPath();
    SetCurrentDirectoryToApplication();
    Coords.Init(this);
    Scene.Init();
    CreateDefaultConfig();
    RefreshUserConfig();
    CameraConfig.Init();
    if (!string.IsNullOrEmpty(m_UserConfig.Profiling.SketchToLoad)) {
      Config.m_SketchFiles = new string[] { m_UserConfig.Profiling.SketchToLoad };
    }

    if (m_UserConfig.Testing.FirstRun) {
      PlayerPrefs.DeleteKey(kPlayerPrefHasPlayedBefore);
      PlayerPrefs.DeleteKey(kReferenceImagesSeeded);
      PlayerPrefs.DeleteKey(PanelManager.kPlayerPrefAdvancedMode);
      AdvancedPanelLayouts.ClearPlayerPrefs();
      PointerManager.ClearPlayerPrefs();
      HasPlayedBefore = false;
    }
    // Cache this variable for the length of the play session.  HasPlayedBefore will be updated,
    // but m_FirstRunExperience should not.
    m_FirstRunExperience = !HasPlayedBefore;

    m_Switchboard = new Switchboard();
    m_GroupManager = new GroupManager();

    m_PolyAssetCatalog = GetComponent<PolyAssetCatalog>();
    m_PolyAssetCatalog.Init();

    m_BrushColorController = GetComponent<BrushColorController>();

    // Tested on Windows. I hope they don't change the names of these preferences.
    PlayerPrefs.DeleteKey("Screenmanager Is Fullscreen mode");
    PlayerPrefs.DeleteKey("Screenmanager Resolution Height");
    PlayerPrefs.DeleteKey("Screenmanager Resolution Width");

    if (DevOptions.I.UseAutoProfiler) {
      gameObject.AddComponent<AutoProfiler>();
    }

    m_Manifest = GetMergedManifest(consultUserConfig: true);

    m_HttpServer = GetComponentInChildren<HttpServer>();
    if (!Config.IsMobileHardware) {
      HttpServer.AddHttpHandler("/load", HttpLoadSketchCallback);
    }

    m_AutosaveRestoreFileExists = File.Exists(AutosaveRestoreFilePath());

    m_GoogleUserSettings = new GoogleUserSettings(m_GoogleIdentity);
    m_DriveAccess = new DriveAccess(m_GoogleIdentity, m_GoogleUserSettings);
    m_DriveSync = new DriveSync(m_DriveAccess, m_GoogleIdentity);
  }

  // TODO: should add OnDestroy to other scripts that create background tasks
  void OnDestroy() {
    if (!Config.IsMobileHardware) {
      HttpServer.RemoveHttpHandler("/load");
    }

    if (m_DriveSync != null) {
      m_DriveSync.UninitializeAsync().AsAsyncVoid();
    }
    if (m_DriveAccess != null) {
      m_DriveAccess.UninitializeAsync().AsAsyncVoid();
    }
  }

  // Called from HttpListener thread.  Supported requests:
  //     /load?<SKETCH_PATH>
  //         Loads sketch given path on local filesystem.  Any pending load is canceled.
  //         Response body:  none
  string HttpLoadSketchCallback(HttpListenerRequest request) {
    var urlPath = request.Url.LocalPath;
    var query = Uri.UnescapeDataString(request.Url.Query);
    if (urlPath == "/load" && query.Length > 1) {
      var filePath = query.Substring(1);
      m_RequestedTiltFileQueue.Enqueue(filePath);
    }
    return "";
  }

  void Start() {
    // Use of ControllerConsoleScript must wait until Start()
    ControllerConsoleScript.m_Instance.AddNewLine(GetStartupString());

    if (!VrSdk.IsHmdInitialized()) {
      Debug.Log("VR HMD was not initialized on startup.");
      StartupError = true;
      CreateErrorDialog();
    } else {
      Debug.LogFormat("Sdk mode: {0} XRDevice.model: {1}",
                      App.Config.m_SdkMode, UnityEngine.XR.XRDevice.model);
    }

    m_TargetFrameRate = VrSdk.GetHmdTargetFrameRate();
    if (VrSdk.GetHmdDof() == TiltBrush.VrSdk.DoF.None) {
      Application.targetFrameRate = m_TargetFrameRate;
    }

    if (VrSdk.HasRoomBounds()) {
      Vector3 extents = VrSdk.GetRoomExtents();
      m_RoomRadius = Mathf.Min(Mathf.Abs(extents.x), Mathf.Abs(extents.z));
    }

    // Load the Usd Plugins
    InitializeUsd();

    foreach (string s in Config.m_SketchFiles) {
      // Assume all relative paths are relative to the Sketches directory.
      string sketch = s;
      if (!System.IO.Path.IsPathRooted(sketch)) {
        sketch = System.IO.Path.Combine(App.UserSketchPath(), sketch);
      }
      m_RequestedTiltFileQueue.Enqueue(sketch);
      if (Config.m_SdkMode == SdkMode.Ods || Config.OfflineRender) {
        // We only load one sketch for ODS rendering & offline rendering.
        break;
      }
    }

    if (Config.m_AutosaveRestoreEnabled && AutosaveRestoreFileExists) {
      string lastAutosave = SaveLoadScript.m_Instance.MostRecentAutosaveFile();
      if (lastAutosave != null) {
        string newPath = SaveLoadScript.m_Instance.GenerateNewUntitledFilename(
            UserSketchPath(), SaveLoadScript.TILT_SUFFIX);
        if (newPath != null) {
          File.Copy(lastAutosave, newPath);
          m_ShowAutosaveHint = true;
        }
      }
      AutosaveRestoreFileExists = false;
    }

    if (Config.m_SdkMode == SdkMode.Ods) {
      m_OdsPivot = (GameObject)Instantiate(m_OdsPrefab);

      OdsDriver driver = m_OdsPivot.GetComponent<OdsDriver>();
      driver.FramesToCapture = Config.m_OdsNumFrames;
      driver.m_fps = Config.m_OdsFps;
      driver.TurnTableRotation = Config.m_OdsTurnTableDegrees;
      driver.OutputFolder = Config.m_OdsOutputPath;
      driver.OutputBasename = Config.m_OdsOutputPrefix;
      if (!string.IsNullOrEmpty(App.Config.m_VideoPathToRender)) {
        driver.CameraPath = App.Config.m_VideoPathToRender;
      }

      ODS.HybridCamera cam = driver.OdsCamera;
      cam.CollapseIpd = Config.m_OdsCollapseIpd;
      cam.imageWidth /= Config.m_OdsPreview ? 4 : 1;
      Debug.LogFormat("Configuring ODS:{0}" +
                      "Frames: {1}{0}" +
                      "FPS: {8}{0}" +
                      "TurnTable: {2}{0}" +
                      "Output: {3}{0}" +
                      "Basename: {4}{0}" +
                      "QuickLoad: {5}{0}" +
                      "CollapseIPD: {6}{0}" +
                      "ImageWidth: {7}{0}",
                      System.Environment.NewLine,
                      driver.FramesToCapture,
                      driver.TurnTableRotation,
                      driver.OutputFolder,
                      driver.OutputBasename,
                      Config.m_QuickLoad,
                      cam.CollapseIpd,
                      cam.imageWidth,
                      driver.m_fps);
    }

    //these guys don't need to be alive just yet
    PointerManager.m_Instance.EnablePointerStrokeGeneration(false);

    Console.WriteLine("RenderODS: {0}, numFrames: {1}",
                      m_OdsPivot != null,
                      m_OdsPivot ? m_OdsPivot.GetComponent<OdsDriver>().FramesToCapture
                                  : 0);

    if (!AppAllowsCreation()) {
      TutorialManager.m_Instance.IntroState = IntroTutorialState.InitializeForNoCreation;
    } else {
      TutorialManager.m_Instance.IntroState = IntroTutorialState.Done;
    }
    if (m_RequestedTiltFileQueue.Count == 0) {
      TutorialManager.m_Instance.ActivateControllerTutorial(InputManager.ControllerName.Brush, false);
      TutorialManager.m_Instance.ActivateControllerTutorial(InputManager.ControllerName.Wand, false);
    }

    ViewpointScript.m_Instance.Init();
    QualityControls.m_Instance.Init();
    bool bVR = VrSdk.GetHmdDof() != TiltBrush.VrSdk.DoF.None;
    InputManager.m_Instance.AllowVrControllers = bVR;
    PointerManager.m_Instance.UseSymmetryWidget(bVR);

    switch (VrSdk.GetControllerDof()) {
    case TiltBrush.VrSdk.DoF.Six:
      // Vive, Rift + Touch
      SketchControlsScript.m_Instance.ActiveControlsType =
                                        SketchControlsScript.ControlsType.SixDofControllers;
      break;
    case TiltBrush.VrSdk.DoF.None:
      SketchControlsScript.m_Instance.ActiveControlsType =
                                        SketchControlsScript.ControlsType.ViewingOnly;
      break;
    case TiltBrush.VrSdk.DoF.Two:
      // Monoscopic
      SketchControlsScript.m_Instance.ActiveControlsType =
                                        SketchControlsScript.ControlsType.KeyboardMouse;
      break;
    }

    m_CurrentAppState = AppState.Standard;
    m_DesiredAppState = AppState.LoadingBrushesAndLighting;
    if (StartupError) {
      m_DesiredAppState = AppState.Error;
    }

    m_SketchSurfacePanel = m_SketchSurface.GetComponent<SketchSurfacePanel>();

    ViewpointScript.m_Instance.SetHeadMeshVisible(App.UserConfig.Flags.ShowHeadset);
    ShowControllers = App.UserConfig.Flags.ShowControllers;

    SwitchState();

#if USD_SUPPORTED && (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental && !string.IsNullOrEmpty(Config.m_IntroSketchUsdFilename)) {
      var gobject = ImportUsd.ImportWithAnim(Config.m_IntroSketchUsdFilename);

      gobject.transform.SetParent(App.Scene.transform, false);
    }
#endif

    if (Config.m_AutoProfile || m_UserConfig.Profiling.AutoProfile) {
      StateChanged += AutoProfileOnStartAndQuit;
    }

  }

  private void AutoProfileOnStartAndQuit(AppState oldState, AppState newState) {
    if (newState == AppState.Standard) {
      Invoke("AutoProfileAndQuit", Config.m_AutoProfileWaitTime);
      StateChanged -= AutoProfileOnStartAndQuit;
    }
  }

  private void AutoProfileAndQuit() {
    SketchControlsScript.m_Instance.IssueGlobalCommand(
        SketchControlsScript.GlobalCommands.DoAutoProfileAndQuit);
  }

  public void SetDesiredState(AppState rDesiredState) {
    m_DesiredAppState = rDesiredState;
  }

  void Update() {
#if UNITY_EDITOR
    // All changes to Scene transform must go through Coords.cs
    if (m_SceneTransform.hasChanged) {
      Debug.LogError("Detected unsanctioned change to Scene transform");
      m_SceneTransform.hasChanged = false;
    }
#endif

    //look for state change
    if (m_CurrentAppState != m_DesiredAppState) {
      SwitchState();
    }

    if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate)) {
      //kinda heavy-handed, but whatevs
      InitCursor();
    }

    // Wait for the environment transition to complete before capturing.
    if (m_OdsPivot
        && !m_OdsPivot.activeInHierarchy
        && !SceneSettings.m_Instance.IsTransitioning
        && ((m_CurrentAppState == AppState.Loading && !Config.m_QuickLoad)
            || m_CurrentAppState == AppState.Standard)) {
      try {
        OdsDriver driver = m_OdsPivot.GetComponent<OdsDriver>();

        // Load the secondary transform, if a second sketch was specified.
        if (Config.m_SketchFiles.Length > 1) {
          string sketch = Config.m_SketchFiles[1];
          // Assume relative paths are relative to the sketches directory.
          if (!System.IO.Path.IsPathRooted(sketch)) {
            sketch = System.IO.Path.Combine(App.UserSketchPath(), sketch);
          }
          var head = TrTransform.identity;
          var scene = TrTransform.identity;
          if (SaveLoadScript.m_Instance.LoadTransformsForOds(new DiskSceneFileInfo(sketch),
            ref head,
            ref scene)) {
            OdsHeadSecondary = head;
            OdsSceneSecondary = scene;
          } else {
            Debug.LogErrorFormat("Failed to load secondary sketch for ODS: {0}", sketch);
          }
        }

        if (driver.OutputBasename == null || driver.OutputBasename == "") {
          driver.OutputBasename =
            FileUtils.SanitizeFilename(SaveLoadScript.m_Instance.SceneFile.HumanName);
          if (driver.OutputBasename == null || driver.OutputBasename == "") {
            if (Config.m_SketchFiles.Length > 0) {
              driver.OutputBasename = System.IO.Path.GetFileNameWithoutExtension(
                Config.m_SketchFiles[0]);
            } else {
              driver.OutputBasename = "Untitled";
            }
          }
        }

        if (driver.OutputFolder == null || driver.OutputFolder == "") {
          driver.OutputFolder = App.VrVideosPath();
          FileUtils.InitializeDirectoryWithUserError(driver.OutputFolder);
        }

        InputManager.m_Instance.EnablePoseTracking(false);

        driver.BeginRender();
      } catch (System.Exception ex) {
        Debug.LogException(ex);
        Application.Quit();
        Debug.Break();
      }
    }

    m_PolyAssetCatalog.UpdateCatalog();

    //update state
    switch (m_CurrentAppState) {
    case AppState.LoadingBrushesAndLighting: {
      if (!BrushCatalog.m_Instance.IsLoading
          && !EnvironmentCatalog.m_Instance.IsLoading
          && !m_ShaderWarmup.activeInHierarchy) {
        if (AppAllowsCreation()) {
          BrushController.m_Instance.SetBrushToDefault();
          BrushColor.SetColorToDefault();
        } else {
          PointerManager.m_Instance.SetBrushForAllPointers(BrushCatalog.m_Instance.DefaultBrush);
        }

        AudioManager.Enabled = true;
        SceneSettings.m_Instance.SetDesiredPreset(EnvironmentCatalog.m_Instance.DefaultEnvironment);

        bool skipStandardIntro = true;
        if (HandleExternalTiltOpenRequest()) {
          // tilt requested on command line was loaded
        } else if (Config.m_FilePatternsToExport != null) {
          m_DesiredAppState = AppState.Standard;
          SketchControlsScript.m_Instance.IssueGlobalCommand(
            SketchControlsScript.GlobalCommands.ExportListed);
        } else if (Config.OfflineRender) {
          m_DesiredAppState = AppState.Standard;
        } else if (DemoManager.m_Instance.DemoModeEnabled) {
          OnIntroComplete();
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
        } else if (Config.IsExperimental) {
          OnIntroComplete();
          PanelManager.m_Instance.ReviveFloatingPanelsForStartup();
#endif
        } else {
          if (Config.m_SdkMode == SdkMode.Ods) {
            // Skip the fade from black when we're rendering ODS.
            m_DesiredAppState = AppState.Standard;
          } else {
            m_DesiredAppState = AppState.FadeFromBlack;
            skipStandardIntro = false;
          }
        }

        if (skipStandardIntro) {
          DestroyIntroSketch();
          ViewpointScript.m_Instance.FadeToScene(float.MaxValue);
        }
      }
      break;
    }
    case AppState.FadeFromBlack: {
      // On the Oculus platform, the Health and Safety warning may be visible, blocking the
      // user's view.  If this is the case, hold black until the warning is dismissed.
      if (!VrSdk.IsAppFocusBlocked() || Config.m_SdkMode == SdkMode.Ods) {
        m_AppStateCountdown -= Time.deltaTime;
      }

      if (m_AppStateCountdown <= 0.0f) {
        PointerManager.m_Instance.EnablePointerStrokeGeneration(true);
        m_AppStateCountdown = 0;
        if (!HasPlayedBefore) {
          m_DesiredAppState = AppState.FirstRunIntro;
        } else {
          m_DesiredAppState = AppState.Intro;
        }
      }
      break;
    }
    case AppState.FirstRunIntro: {
      if (UpdateIntroFadeIsFinished()) {
          PointerManager.m_Instance.EnablePointerStrokeGeneration(true);
          SaveLoadScript.m_Instance.NewAutosaveFile();
          m_DesiredAppState = AppState.Standard;
        }
        break;
      }
    case AppState.Intro: {
      if (UpdateIntroFadeIsFinished()) {
        if (!Config.IsMobileHardware) {
          InputManager.Brush.Behavior.BuzzAndGlow(1.0f, 7, .1f);
          InputManager.Wand.Behavior.BuzzAndGlow(1.0f, 7, .1f);
          AudioManager.m_Instance.PlayMagicControllerSound();
        }
        PanelManager.m_Instance.ShowIntroSketchbookPanels();
        PointerManager.m_Instance.IndicateBrushSize = false;
        PromoManager.m_Instance.RequestPromo(PromoType.InteractIntroPanel);
        OnIntroComplete();
      }
      break;
      }
    case AppState.Loading: {
        HandleExternalTiltOpenRequest();
        SketchControlsScript.m_Instance.UpdateControlsForLoading();

        if (WidgetManager.m_Instance.CreatingMediaWidgets) {
          break;
        }

        //trigger our tutorial a little bit after we started loading so it doesn't show up immediately
        if (!m_QuickLoadEatInput) {
          float fPrevTutorialValue = m_QuickLoadHintCountdown;
          m_QuickLoadHintCountdown -= Time.deltaTime;
          if (fPrevTutorialValue > 0.0f && m_QuickLoadHintCountdown <= 0.0f) {
            TutorialManager.m_Instance.EnableQuickLoadTutorial(true);
          }
        }

        if (OverlayManager.m_Instance.CanDisplayQuickloadOverlay) {
          // Watch for speed up button presses and keep on loadin'
          // Don't allow for quickloading yet if we are fading out the overlay from loading media.
          UpdateQuickLoadLogic();
        }
        if ((m_OdsPivot && Config.m_QuickLoad) ||
            (Config.OfflineRender) || !string.IsNullOrEmpty(m_UserConfig.Profiling.SketchToLoad)) {
          m_DesiredAppState = AppState.QuickLoad;
        }

        // Call ContinueDrawingFromMemory() unless we are rendering ODS, in which case we don't want
        // to animate the strokes until the renderer has actually started.
        bool bContinueDrawing = true;
        if (Config.m_SdkMode != SdkMode.Ods || m_OdsPivot.GetComponent<OdsDriver>().IsRendering) {
          bContinueDrawing = SketchMemoryScript.m_Instance.ContinueDrawingFromMemory();
        }
        if (!bContinueDrawing) {
          FinishLoading();
          InputManager.m_Instance.TriggerHapticsPulse(
            InputManager.ControllerName.Brush, 4, 0.15f, 0.1f);
          InputManager.m_Instance.TriggerHapticsPulse(
            InputManager.ControllerName.Wand, 4, 0.15f, 0.1f);
        }
        break;
      }
    case AppState.QuickLoad: {
        // Allow extra frames to complete fade to black.
        // Required for OVR to position the overlay because it only does so once the transition
        // is complete.
        if (m_QuickloadStallFrames-- < 0) {
          bool bContinueDrawing = SketchMemoryScript.m_Instance.ContinueDrawingFromMemory();
          if (!bContinueDrawing) {
            FinishLoading();
          }
        }
        break;
      }
    case AppState.Uploading:
      SketchControlsScript.m_Instance.UpdateControlsForUploading();
      break;
    case AppState.MemoryExceeded:
      SketchControlsScript.m_Instance.UpdateControlsForMemoryExceeded();
      break;
    case AppState.Standard:
      // Logic for fading out intro sketches.
      if (m_IntroFadeTimer > 0 &&
          !PanelManager.m_Instance.IntroSketchbookMode &&
          !TutorialManager.m_Instance.TutorialActive()) {
        if (UpdateIntroFadeIsFinished()) {
          PanelManager.m_Instance.ReviveFloatingPanelsForStartup();
        }
      }

      // If the app doesn't have focus, don't update.
      if (VrSdk.IsAppFocusBlocked() && Config.m_SdkMode != SdkMode.Ods) {
        break;
      }

      // Intro tutorial state machine.
      TutorialManager.m_Instance.UpdateIntroTutorial();

      // Continue edit-time playback, if any.
      SketchMemoryScript.m_Instance.ContinueDrawingFromMemory();
      if (PanelManager.m_Instance.SketchbookActiveIncludingTransitions() &&
          PanelManager.m_Instance.IntroSketchbookMode) {
        // Limit controls if the user hasn't exited from the sketchbook post intro.
        SketchControlsScript.m_Instance.UpdateControlsPostIntro();
      } else {
        SketchControlsScript.m_Instance.UpdateControls();
      }

      // This should happen after SMS.ContinueDrawingFromMemory, so we're not loading and
      // continuing in one frame.
      HandleExternalTiltOpenRequest();
      break;
    case AppState.Reset:
      SketchControlsScript.m_Instance.UpdateControls();
      if (!PointerManager.m_Instance.IsMainPointerCreatingStroke() &&
          !PointerManager.m_Instance.IsMainPointerProcessingLine()) {
        StartReset();
      }
      break;
    }
  }

  public void ExitIntroSketch() {
    PanelManager.m_Instance.SetInIntroSketchbookMode(false);
    PointerManager.m_Instance.IndicateBrushSize = true;
    PointerManager.m_Instance.PointerColor = PointerManager.m_Instance.PointerColor;
    PromoManager.m_Instance.RequestPromo(PromoType.BrushSize);
  }

  private void StartReset() {
    // Switch to paint tool if not already there.
    SketchSurfacePanel.m_Instance.EnableDefaultTool();

    // Disable preview line.
    PointerManager.m_Instance.AllowPointerPreviewLine(false);

    // Switch to the default brush type and size.
    BrushController.m_Instance.SetBrushToDefault();

    // Disable audio reactive mode.
    if (m_RequestingAudioReactiveMode) {
      ToggleAudioReactiveModeRequest();
    }

    // Reset to the default brush color.
    BrushColor.SetColorToDefault();

    // Clear saved colors.
    CustomColorPaletteStorage.m_Instance.ClearAllColors();

    // Turn off straightedge
    PointerManager.m_Instance.StraightEdgeModeEnabled = false;

    // Turn off straightedge ruler.
    if (PointerManager.m_Instance.StraightEdgeGuide.IsShowingMeter()) {
      PointerManager.m_Instance.StraightEdgeGuide.FlipMeter();
    }

    // Close any panel menus that might be open (e.g. Sketchbook)
    if (PanelManager.m_Instance.SketchbookActive()) {
      PanelManager.m_Instance.ToggleSketchbookPanels();
    } else if (PanelManager.m_Instance.SettingsActive()) {
      PanelManager.m_Instance.ToggleSettingsPanels();
    } else if (PanelManager.m_Instance.MemoryWarningActive()) {
      PanelManager.m_Instance.ToggleMemoryWarningMode();
    } else if (PanelManager.m_Instance.BrushLabActive()) {
      PanelManager.m_Instance.ToggleBrushLabPanels();
    }

    // Hide all panels.
    SketchControlsScript.m_Instance.RequestPanelsVisibility(false);

    // Reset all panels.
    SketchControlsScript.m_Instance.IssueGlobalCommand(
        SketchControlsScript.GlobalCommands.ResetAllPanels);

    // Rotate want panels to default orientation (color picker).
    PanelManager.m_Instance.ResetWandPanelRotation();

    // Close Twitch widget.
    if (SketchControlsScript.m_Instance.IsCommandActive(SketchControlsScript.GlobalCommands.IRC)) {
      SketchControlsScript.m_Instance.IssueGlobalCommand(SketchControlsScript.GlobalCommands.IRC);
    }

    // Close Youtube Chat widget.
    if (SketchControlsScript.m_Instance.IsCommandActive(
        SketchControlsScript.GlobalCommands.YouTubeChat)) {
      SketchControlsScript.m_Instance.IssueGlobalCommand(
          SketchControlsScript.GlobalCommands.YouTubeChat);
    }

    // Hide the pointer and reticle.
    PointerManager.m_Instance.RequestPointerRendering(false);
    SketchControlsScript.m_Instance.ForceShowUIReticle(false);
  }

  private void FinishReset() {
    // Switch to the default environment.
    SceneSettings.m_Instance.SetDesiredPreset(EnvironmentCatalog.m_Instance.DefaultEnvironment);

    // Clear the sketch and reset the scene transform.
    SketchControlsScript.m_Instance.NewSketch(fade: false);

    // Disable mirror.
    PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.None);

    // Reset mirror position.
    PointerManager.m_Instance.ResetSymmetryToHome();

    // Show the wand panels.
    SketchControlsScript.m_Instance.RequestPanelsVisibility(true);

    // Show the pointer.
    PointerManager.m_Instance.RequestPointerRendering(true);
    PointerManager.m_Instance.EnablePointerStrokeGeneration(true);

    // Forget command history.
    SketchMemoryScript.m_Instance.ClearMemory();
  }

  void FinishLoading() {
    // Force progress to be full before exiting (for small scenes)
    OverlayManager.m_Instance.UpdateProgress(1);
    OverlayManager.m_Instance.HideOverlay();
    //if we just released the button, kick a fade out
    if (m_QuickLoadInputWasValid) {
      App.VrSdk.PauseRendering(false);
      App.VrSdk.FadeFromCompositor(0);
    }

    m_DesiredAppState = AppState.Standard;
    if (VrSdk.GetControllerDof() == TiltBrush.VrSdk.DoF.Six) {
      float holdDelay = (m_CurrentAppState == AppState.QuickLoad) ? 1.0f : 0.0f;
      StartCoroutine(DelayedSketchLoadedCard(holdDelay));
    } else {
      OutputWindowScript.m_Instance.AddNewLine(
        OutputWindowScript.LineType.Special, "Sketch Loaded!");
    }

    OnPlaybackComplete();
    m_SketchSurfacePanel.EnableRenderer(true);

    //turn off quick load tutorial
    TutorialManager.m_Instance.EnableQuickLoadTutorial(false);

    AudioManager.m_Instance.PlaySketchLoadedSound(
      InputManager.m_Instance.GetControllerPosition(InputManager.ControllerName.Brush));

    SketchControlsScript.m_Instance.RequestPanelsVisibility(true);
    if (VideoRecorderUtils.ActiveVideoRecording == null) {
      SketchSurfacePanel.m_Instance.EatToolsInput();
    }
    SketchSurfacePanel.m_Instance.RequestHideActiveTool(false);
    SketchControlsScript.m_Instance.RestoreFloatingPanels();
    PointerManager.m_Instance.RequestPointerRendering(
        SketchSurfacePanel.m_Instance.ShouldShowPointer());
    PointerManager.m_Instance.RestoreBrushInfo();
    WidgetManager.m_Instance.LoadingState(false);
    WidgetManager.m_Instance.WidgetsDormant = true;
    SketchControlsScript.m_Instance.EatGrabInput();
    SaveLoadScript.m_Instance.MarkAsAutosaveDone();
    if (SaveLoadScript.m_Instance.SceneFile.InfoType == FileInfoType.Disk) {
      PromoManager.m_Instance.RequestAdvancedPanelsPromo();
    }
    SketchMemoryScript.m_Instance.SanitizeMemoryList();

    if (Config.OfflineRender) {
      SketchControlsScript.m_Instance.IssueGlobalCommand(
        SketchControlsScript.GlobalCommands.RenderCameraPath);
    }
  }

  private IEnumerator<Timeslice> DelayedSketchLoadedCard(float delay) {
    float stall = delay;
    while (stall >= 0.0f) {
      stall -= Time.deltaTime;
      yield return null;
    }

    OutputWindowScript.m_Instance.CreateInfoCardAtController(
        InputManager.ControllerName.Brush, "Sketch Loaded!");
  }

  void SwitchState() {
    switch (m_CurrentAppState) {
    case AppState.LoadingBrushesAndLighting:
      if (VrSdk.GetControllerDof() == VrSdk.DoF.Two) {
        // Sketch surface tool is not properly loaded because
        // it is the default tool.
        SketchSurfacePanel.m_Instance.ActiveTool.EnableTool(false);
        SketchSurfacePanel.m_Instance.ActiveTool.EnableTool(true);
      }
      break;
    case AppState.Reset:
      // Demos should reset to the standard state only.
      Debug.Assert(m_DesiredAppState == AppState.Standard);
      FinishReset();
      break;
    case AppState.AutoProfiling:
    case AppState.OfflineRendering:
      InputManager.m_Instance.EnablePoseTracking(true);
      break;
    case AppState.MemoryExceeded:
      SketchSurfacePanel.m_Instance.EnableDefaultTool();
      PanelManager.m_Instance.ToggleMemoryWarningMode();
      PointerManager.m_Instance.RequestPointerRendering(
          SketchSurfacePanel.m_Instance.ShouldShowPointer());
      break;
    }

    switch (m_DesiredAppState) {
    case AppState.LoadingBrushesAndLighting:
      BrushCatalog.m_Instance.BeginReload();
      EnvironmentCatalog.m_Instance.BeginReload();
      CreateIntroSketch();
      break;
    case AppState.FadeFromBlack:
      ViewpointScript.m_Instance.FadeToScene(1.0f / m_FadeFromBlackDuration);
      m_AppStateCountdown = m_FadeFromBlackDuration;
      break;
    case AppState.FirstRunIntro:
      AudioManager.m_Instance.PlayFirstRunMusic(AudioManager.FirstRunMusic.IntroAmbient);
      m_SketchSurfacePanel.EnableRenderer(false);
      TutorialManager.m_Instance.IntroState = IntroTutorialState.ActivateBrush;
      m_IntroFadeTimer = 0;
      break;
    case AppState.Intro:
      AudioManager.m_Instance.PlayFirstRunMusic(AudioManager.FirstRunMusic.IntroAmbient);
      m_SketchSurfacePanel.EnableRenderer(false);
      m_IntroFadeTimer = 0;
      break;
    case AppState.Loading:
      if (m_IntroFadeTimer > 0) {
        AudioManager.m_Instance.SetMusicVolume(0.0f);
        m_IntroFadeTimer = 0;
        DestroyIntroSketch();
        PanelManager.m_Instance.ReviveFloatingPanelsForStartup();
      }
      PointerManager.m_Instance.StoreBrushInfo();
      m_QuickLoadHintCountdown = m_QuickLoadHintDelay;
      m_QuickLoadEatInput = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Panic);
      m_QuickLoadInputWasValid = false;

      // Don't disable tools if we've got a valid load tool active.
      bool bToolsAllowed = SketchSurfacePanel.m_Instance.ActiveTool.AvailableDuringLoading();
      if (!bToolsAllowed) {
        m_SketchSurfacePanel.EnableRenderer(false);
      } else {
        m_SketchSurfacePanel.RequestHideActiveTool(false);
      }

      if (!bToolsAllowed) {
        SketchSurfacePanel.m_Instance.EnableDefaultTool();
      }
      PointerManager.m_Instance.RequestPointerRendering(false);
      SketchControlsScript.m_Instance.RequestPanelsVisibility(false);
      SketchControlsScript.m_Instance.ResetActivePanel();
      PanelManager.m_Instance.HideAllPanels();
      SketchControlsScript.m_Instance.ForceShowUIReticle(false);
      PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.None, false);
      WidgetManager.m_Instance.LoadingState(true);
      WidgetManager.m_Instance.StencilsDisabled = true;
      break;
    case AppState.QuickLoad:
      SketchMemoryScript.m_Instance.QuickLoadDrawingMemory();
      break;
    case AppState.MemoryExceeded:
      if (!PanelManager.m_Instance.MemoryWarningActive()) {
        PanelManager.m_Instance.ToggleMemoryWarningMode();
      }
      SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.EmptyTool);
      AudioManager.m_Instance.PlayUploadCanceledSound(InputManager.Wand.Transform.position);
      break;
    case AppState.Standard:
      PointerManager.m_Instance.DisablePointerPreviewLine();
      // Refresh the tinting on the controllers
      PointerManager.m_Instance.PointerColor = PointerManager.m_Instance.PointerColor;

      if (m_ShowAutosaveHint) {
        OutputWindowScript.m_Instance.CreateInfoCardAtController(InputManager.ControllerName.Wand,
            "Abnormal program termination detected!\n" +
            "The last autosave has been copied into your sketchbook.");
        m_ShowAutosaveHint = false;
      }
      break;
    case AppState.Reset:
      PointerManager.m_Instance.EnablePointerStrokeGeneration(false);
      PointerManager.m_Instance.AllowPointerPreviewLine(false);
      PointerManager.m_Instance.EatLineEnabledInput();
      PointerManager.m_Instance.EnableLine(false);
      break;
    case AppState.AutoProfiling:
    case AppState.OfflineRendering:
      InputManager.m_Instance.EnablePoseTracking(false);
      break;
    }

    var oldState = m_CurrentAppState;
    m_CurrentAppState = m_DesiredAppState;
    if (StateChanged != null) {
      StateChanged(oldState, m_CurrentAppState);
    }
  }

  /// Load one requested sketch, if any, returning true if pending request was processed.
  private bool HandleExternalTiltOpenRequest() {
    // Early out if we're in the intro tutorial.
    if (TutorialManager.m_Instance.TutorialActive() || m_RequestedTiltFileQueue.Count == 0) {
      return false;
    }

    string path;
    try {
      path = (string)m_RequestedTiltFileQueue.Dequeue();
    } catch (InvalidOperationException) {
      return false;
    }
    Debug.LogFormat("Received external request to load {0}", path);

    if (path.StartsWith(kProtocolHandlerPrefix)) {
      return HandlePolyRequest(path);
    }

    // Copy to sketch folder in order to discourage the user from explicitly saving
    // to gallery for future access, which would (by design) strip attribution.
    // Crypto hash suffix is added to the filename for (deterministic) uniqueness.
    try {
      string dstFilename = Path.GetFileName(path);
      if (Path.GetFullPath(Path.GetDirectoryName(path)) != Path.GetFullPath(UserSketchPath()) &&
        SaveLoadScript.Md5Suffix(dstFilename) == null) {
        dstFilename = SaveLoadScript.AddMd5Suffix(dstFilename,
          SaveLoadScript.GetMd5(path));
      }
      string dstPath = Path.Combine(UserSketchPath(), dstFilename);
      if (!File.Exists(dstPath)) {
        File.Copy(path, dstPath);
      }
      SketchControlsScript.m_Instance.IssueGlobalCommand(
        SketchControlsScript.GlobalCommands.LoadNamedFile, sParam: dstPath);
    } catch (FileNotFoundException) {
      OutputWindowScript.Error(String.Format("Couldn't open {0}", path));
      return false;
    }
    return true;
  }

  private bool HandlePolyRequest(string request) {
    string id = request.Substring(kProtocolHandlerPrefix.Length); // Strip prefix to get asset id
    StartCoroutine(VrAssetService.m_Instance.LoadTiltFile(id));
    return true;
  }

  public bool ShouldTintControllers() {
    return m_DesiredAppState == AppState.Standard && !PanelManager.m_Instance.IntroSketchbookMode;
  }

  public bool IsInStateThatAllowsPainting() {
    return !TutorialManager.m_Instance.TutorialActive() &&
        CurrentState == AppState.Standard &&
        !PanelManager.m_Instance.IntroSketchbookMode;
  }

  public bool IsInStateThatAllowsAnyGrabbing() {
    return !TutorialManager.m_Instance.TutorialActive() &&
        !PanelManager.m_Instance.IntroSketchbookMode &&
        (CurrentState == AppState.Standard || CurrentState == AppState.Loading) &&
        !SelectionManager.m_Instance.IsAnimatingTossFromGrabbingGroup;
  }

  public bool IsLoading() {
    return CurrentState == AppState.Loading || CurrentState == AppState.QuickLoad;
  }

  void UpdateQuickLoadLogic() {
    if (CurrentState == AppState.Loading && AppAllowsCreation()) {
      //require the user to stop holding the trigger before pulling it again to speed load
      if (m_QuickLoadEatInput) {
        if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Panic)) {
          m_QuickLoadEatInput = false;
        }
      } else {
        if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Panic) &&
            !SketchControlsScript.m_Instance.IsUserInteractingWithAnyWidget() &&
            !SketchControlsScript.m_Instance.IsUserGrabbingWorld() &&
            (VideoRecorderUtils.ActiveVideoRecording == null) &&
            (!VrSdk.IsAppFocusBlocked() || Config.m_SdkMode == SdkMode.Ods)) {
          OverlayManager.m_Instance.SetOverlayFromType(OverlayType.LoadSketch);
          //if we just pressed the button, kick a fade in
          if (!m_QuickLoadInputWasValid) {
            // b/69060780: This workaround is due to the ViewpointScript.Update() also messing
            // with the overlay fade, and causing state conflicts in OVR.
            if (!App.VrSdk.OverlayIsOVR || ViewpointScript.m_Instance.AllowsFading) {
              App.VrSdk.FadeToCompositor(0);
            } else {
              ViewpointScript.m_Instance.SetOverlayToBlack();
            }
            App.VrSdk.PauseRendering(true);
            InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Wand, 0.05f);
          }

          m_QuickLoadInputWasValid = true;
          if (m_CurrentAppState != AppState.QuickLoad) {
            OverlayManager.m_Instance.SetOverlayTransitionRatio(1.0f);
            m_QuickloadStallFrames = 1;
            m_DesiredAppState = AppState.QuickLoad;
            m_SketchSurfacePanel.EnableRenderer(false);
            InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Wand, 0.1f);
          }
        } else {
          //if we just released the button, kick a fade out
          if (m_QuickLoadInputWasValid) {
            App.VrSdk.PauseRendering(false);
            App.VrSdk.FadeFromCompositor(0);
          }
          m_QuickLoadInputWasValid = false;
        }
      }
    }
  }

  void OnIntroComplete() {
    SaveLoadScript.m_Instance.NewAutosaveFile();
    PointerManager.m_Instance.EnablePointerStrokeGeneration(true);
    SketchControlsScript.m_Instance.RequestPanelsVisibility(true);

    // If the user chooses to skip the intro, assume they've done the tutorial before.
    PlayerPrefs.SetInt(App.kPlayerPrefHasPlayedBefore, 1);

    m_DesiredAppState = AppState.Standard;
  }

  // Updates the intro fade (both in and out) and returns true when finished.
  // Has a special case for Mobile, so that the scene fades out as soon as it has faded in.
  //
  // For desktop:
  //   FFFFFFFFFFFFT               FFFFFFFFFFFFFFFFFT
  //   ^ Start     ^ Faded in      ^ Standard Mode  ^ Faded out
  //
  // For mobile:
  //   FFFFFFFFFFFFFFFFFFFFFFFFFFFT              T
  //   ^ Start     ^ Faded in     ^ Faded out    ^ Standard Mode
  //
  // (F = returns false, T = returns true)

  bool UpdateIntroFadeIsFinished() {
    if (m_IntroSketchRenderers == null) {
      m_IntroFadeTimer = 0;
      // This code path gets triggered when running on mobile. At this point the fade out has
      // already happened, so we just return true so that the 'once-fadeout-has-finished' code gets
      // triggered.
      return true;
    }

    bool isMobile = Config.IsMobileHardware;
    bool isFadingIn = m_IntroFadeTimer < 1f;
    float fadeMax = isFadingIn ? 1f : 2f;
    float fadeDuration;
    if (isMobile) {
      fadeDuration = isFadingIn
          ? m_IntroSketchMobileFadeInDuration
          : m_IntroSketchMobileFadeOutDuration;
    } else {
      fadeDuration = isFadingIn ? m_IntroSketchFadeInDuration : m_IntroSketchFadeOutDuration;
    }

    m_IntroFadeTimer += Time.deltaTime / fadeDuration;
    if (m_IntroFadeTimer > fadeMax) {
      m_IntroFadeTimer = fadeMax;
    }

    for (int i = 0; i < m_IntroSketchRenderers.Length; ++i) {
      m_IntroSketchRenderers[i].material.SetFloat("_IntroDissolve",
          Mathf.SmoothStep(0, 1, Math.Abs(1 - m_IntroFadeTimer)));
    }

    if (m_IntroFadeTimer == fadeMax) {
      if (isFadingIn) {
        // With Mobile, we fade in then out, so the fade isn't complete at fade-in.
        return !isMobile;
      } else {
        DestroyIntroSketch();
        m_IntroSketchRenderers = null;
        return true;
      }
    }

    return false;
  }

  void InitCursor() {
    if (StartupError) {
      return;
    }
    if (VrSdk.GetHmdDof() == TiltBrush.VrSdk.DoF.None) {
      Cursor.visible = false;
      Cursor.lockState = CursorLockMode.Locked;
    }
  }

  public static T DeserializeObjectWithWarning<T>(string text, out string warning) {
    // Try twice, once to catch "unknown key" warnings, once to actually get a result.
    warning = null;
    try {
      return JsonConvert.DeserializeObject<T>(text, new JsonSerializerSettings {
          MissingMemberHandling = MissingMemberHandling.Error
        });
    } catch (JsonSerializationException e) {
      warning = e.Message;
      return JsonConvert.DeserializeObject<T>(text);
    }
  }

  void CreateDefaultConfig() {
    // If we don't have a .cfg in our Tilt Brush directory, drop a default one.
    string tiltBrushFolder = UserPath();
    if (!Directory.Exists(tiltBrushFolder)) {
      return;
    }

    string configPath = Path.Combine(tiltBrushFolder, kConfigFileName);
    if (!File.Exists(configPath)) {
      FileUtils.WriteTextFromResources(kDefaultConfigPath, configPath);
    }
  }

  public void RefreshUserConfig() {
    m_UserConfig = new UserConfig();

    try {
      string sConfigPath = App.ConfigPath();
      if (!File.Exists(sConfigPath)) {
        return;
      }

      string text;
      try {
        text = File.ReadAllText(sConfigPath, System.Text.Encoding.UTF8);
      } catch (Exception e) {
        // UnauthorizedAccessException, IOException
        OutputWindowScript.Error($"Error reading {kConfigFileName}", e.Message);
        return;
      }

      try {
        string warning;
        m_UserConfig = DeserializeObjectWithWarning<UserConfig>(text, out warning);
        if (warning != null) {
          OutputWindowScript.Error($"Warning reading {kConfigFileName}", warning);
        }
      } catch (Exception e) {
        OutputWindowScript.Error($"Error reading {kConfigFileName}", e.Message);
        return;
      }
    } finally {
      // Apply any overrides sent through via the command line, even if reading from Tilt Brush.cfg
      // goes horribly wrong.
      Config.ApplyUserConfigOverrides(m_UserConfig);
    }
  }

  public void CreateErrorDialog(string msg=null) {
    GameObject dialog = Instantiate(m_ErrorDialog);
    var textXf = dialog.transform.Find("Text");
    var textMesh = textXf.GetComponent<TextMesh>();
    if (msg == null) {
      msg = "Failed to detect VR";
    }
    textMesh.text = string.Format(@"        Tiltasaurus says...
                   {0}", msg);
  }

  static public bool AppAllowsCreation() {
    // TODO: this feels like it should be an explicit part of Config,
    // not something based on VR hardware...
    return App.VrSdk.GetControllerDof() != TiltBrush.VrSdk.DoF.None;
  }

  static public string PlatformPath() {
    if (!Application.isEditor && Application.platform == RuntimePlatform.OSXPlayer) {
      return System.IO.Directory.GetParent(Application.dataPath).Parent.ToString();
    } else if (Application.platform == RuntimePlatform.Android) {
      return Application.persistentDataPath;
    }

    return System.IO.Directory.GetParent(Application.dataPath).ToString();
  }

  static public string SupportPath() {
    return Path.Combine(PlatformPath(), "Support");
  }

  /// Returns a parent of UserPath; used to figure out how much path
  /// is necessary to display to the user when giving feedback. We
  /// assume this is the "boring" portion of the path that they can infer.
  public static string DocumentsPath() {
    switch (Application.platform) {
    case RuntimePlatform.WindowsPlayer:
    case RuntimePlatform.WindowsEditor:
    case RuntimePlatform.OSXPlayer:
    case RuntimePlatform.OSXEditor:
    case RuntimePlatform.LinuxPlayer:
      return System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
    case RuntimePlatform.Android:
    case RuntimePlatform.IPhonePlayer:
    default:
      return Application.persistentDataPath;
    }
  }

  void InitUserPath() {
    switch (Application.platform) {
    case RuntimePlatform.WindowsPlayer:
    case RuntimePlatform.WindowsEditor:
      // user Documents folder
      m_UserPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);

      // GetFolderPath() can fail, returning an empty string.
      if (m_UserPath == "") {
        // If that happens, try a bunch of other folders.
        m_UserPath = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.MyDocuments);
        if (m_UserPath == "") {
          m_UserPath = System.Environment.GetFolderPath(
              System.Environment.SpecialFolder.DesktopDirectory);
        }
      }
      break;
    case RuntimePlatform.OSXPlayer:
    case RuntimePlatform.OSXEditor:
    case RuntimePlatform.LinuxPlayer:
      // user Documents folder
      m_UserPath = Path.Combine(System.Environment.GetFolderPath(
                              System.Environment.SpecialFolder.Personal),
                          "Documents");
      break;
    case RuntimePlatform.Android:
      m_UserPath = "/sdcard/";
      m_OldUserPath = Application.persistentDataPath;
      break;
    case RuntimePlatform.IPhonePlayer:
    default:
      m_UserPath = Application.persistentDataPath;
      break;
    }

    m_UserPath = Path.Combine(m_UserPath, App.kAppFolderName);

    // In the case that we have changed the location of the user data, move the user data from the
    // old location to the new one.
    if (!string.IsNullOrEmpty(m_OldUserPath)) {
      MoveUserDataFromOldLocation();
    }

    if (!Path.IsPathRooted(m_UserPath)) {
      StartupError = true;
      CreateErrorDialog("Failed to find Documents folder.\nIn Windows, try modifying your Controlled Folder Access settings.");
    }
  }

  private void MoveUserDataFromOldLocation() {
    m_OldUserPath = Path.Combine(m_OldUserPath, App.kAppFolderName);

    if (!Directory.Exists(m_OldUserPath)) {
      return;
    }

    if (Directory.Exists(m_UserPath)) {
      return;
    }

    try {
      Directory.Move(m_OldUserPath, m_UserPath);
      // Recreate the old directory and put a message in there so a user used to looking in the old
      // location can find out where to get their files.
      Directory.CreateDirectory(m_OldUserPath);
      string moveMessageFilename = Path.Combine(m_OldUserPath, kFileMoveFilename);
      File.WriteAllText(moveMessageFilename, kFileMoveContents);
    } catch (Exception ex) {
      Debug.LogException(ex);
    }
  }

  // Return path of root directory for storing user sketches, snapshots, etc.
  public static string UserPath() {
    return App.m_Instance.m_UserPath;
  }

  public static bool InitDirectoryAtPath(string path) {
    if (Directory.Exists(path)) {
      return true;
    }
    if (!FileUtils.InitializeDirectoryWithUserError(path)) {
      return false;
    }
    return true;
  }

  public static string ShortenForDescriptionText(string desc) {
    desc = desc.Split('\n')[0];
    if (desc.Length > 33) {
      desc = desc.Substring(0, 30) + "...";
    }
    return desc;
  }

  /// Creates the Media Library directory if it does not already exist.
  /// Returns true if the directory already exists or if it is created successfully, false if the
  /// directory could not be created.
  public static bool InitMediaLibraryPath() {
    string mediaLibraryPath = MediaLibraryPath();
    if (!InitDirectoryAtPath(mediaLibraryPath)) { return false; }
    string readmeFile = Path.Combine(mediaLibraryPath, Config.m_MediaLibraryReadme);
    FileUtils.WriteTextFromResources(Config.m_MediaLibraryReadme,
                                     Path.ChangeExtension(readmeFile, ".txt"));
    return true;
  }

  /// Creates the Model Catalog directory and copies in the provided default models.
  /// Returns true if the directory already exists or if it is created successfully, false if the
  /// directory could not be created.
  public static bool InitModelLibraryPath(string[] defaultModels) {
    string modelsDirectory = ModelLibraryPath();
    if (Directory.Exists(modelsDirectory)) { return true; }
    if (!InitDirectoryAtPath(modelsDirectory)) { return false; }
    foreach (string fileName in defaultModels) {
      string[] path = fileName.Split(
        new[] { '\\', '/' }, 3, StringSplitOptions.RemoveEmptyEntries);
      string newModel = Path.Combine(modelsDirectory, path[1]);
      if (!Directory.Exists(newModel)) {
        Directory.CreateDirectory(newModel);
      }
      if (Path.GetExtension(fileName) == ".png" ||
        Path.GetExtension(fileName) == ".jpeg" ||
        Path.GetExtension(fileName) == ".jpg") {
        FileUtils.WriteTextureFromResources(fileName, Path.Combine(newModel, path[2]));
      } else {
        FileUtils.WriteTextFromResources(fileName, Path.Combine(newModel, path[2]));
      }
    }
    return true;
  }

  /// Creates the Reference Images directory and copies in the provided default images.
  /// Returns true if the directory already exists or if it is created successfully, false if the
  /// directory could not be created.
  public static bool InitReferenceImagePath(string[] defaultImages) {
    string path = ReferenceImagePath();
    if (!Directory.Exists(path)) {
      if (!FileUtils.InitializeDirectoryWithUserError(path)) {
        return false;
      }
    }

    // Poplulate the reference images folder exactly once.
    int seeded = PlayerPrefs.GetInt(kReferenceImagesSeeded);
    if (seeded == 0) {
      foreach (string fileName in defaultImages) {
        FileUtils.WriteTextureFromResources(fileName,
                                            Path.Combine(path, Path.GetFileName(fileName)));
      }
      PlayerPrefs.SetInt(kReferenceImagesSeeded, 1);
    }
    return true;
  }

  public static bool InitVideoLibraryPath(string[] defaultVideos) {
    string videosDirectory = VideoLibraryPath();
    if (Directory.Exists(videosDirectory)) {
      return true;
    }
    if (!InitDirectoryAtPath(videosDirectory)) {
      return false;
    }
    foreach (var video in defaultVideos) {
      string destFilename = Path.GetFileName(video);
      FileUtils.WriteBytesFromResources(video, Path.Combine(videosDirectory, destFilename));
    }

    return true;
  }

  public static string MediaLibraryPath() {
    return Path.Combine(UserPath(), "Media Library");
  }

  public static string ModelLibraryPath() {
    return Path.Combine(MediaLibraryPath(), "Models");
  }

  public static string ReferenceImagePath() {
    return Path.Combine(MediaLibraryPath(), "Images");
  }

  public static string VideoLibraryPath() {
    return Path.Combine(MediaLibraryPath(), "Videos");
  }

  static public string UserSketchPath() {
    return Path.Combine(UserPath(), "Sketches");
  }

  static public string AutosavePath() {
    return Path.Combine(UserPath(), "Sketches/Autosave");
  }

  static public string ConfigPath() {
    return Path.Combine(UserPath(), kConfigFileName);
  }

  static public string UserExportPath() {
    return App.Config.m_ExportPath ?? Path.Combine(UserPath(), "Exports");
  }

  static public string AutosaveRestoreFilePath() {
    return Path.Combine(UserPath(), "Sketches/Autosave/AutosaveRestore");
  }

  static public string SnapshotPath() {
    return Path.Combine(UserPath(), "Snapshots");
  }

  static public string VideosPath() {
    return Path.Combine(UserPath(), "Videos");
  }

  static public string VrVideosPath() {
    return Path.Combine(UserPath(), "VRVideos");
  }

  void OnApplicationQuit() {
    if (AppExit != null) {
      AppExit();
    }

    AutosaveRestoreFileExists = false;
  }

  void OnPlaybackComplete() {
    SaveLoadScript.m_Instance.SignalPlaybackCompletion();
    if (SketchControlsScript.m_Instance.SketchPlaybackMode !=
        SketchMemoryScript.PlaybackMode.Timestamps) {

      // For non-timestamp playback mode, adjust current time to last stroke in drawing.
      try {
        this.CurrentSketchTime = SketchMemoryScript.m_Instance.GetApproximateLatestTimestamp();
      } catch (InvalidOperationException) {
        // Can happen as an edge case, eg if we try to load a file that doesn't exist.
        this.CurrentSketchTime = 0;
      }
    }
  }

  public TiltBrushManifest GetMergedManifest(bool consultUserConfig) {
    var manifest = m_Manifest;
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      // At build time, we don't want the user config to affect the build output.
      if (consultUserConfig
          && m_UserConfig.Flags.ShowDangerousBrushes
          && m_ManifestExperimental != null) {
        manifest = Instantiate(m_Manifest);
        manifest.AppendFrom(m_ManifestExperimental);
      }
    }
#endif
    return manifest;
  }

  public static bool InitializeUsd() {
    if (!UsdUtils.InitializeUsd()) {
      return false;
    }
    return true;
  }

#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
  public bool IsBrushExperimental(BrushDescriptor brush) {
    return m_ManifestExperimental.Brushes.Contains(brush);
  }
#endif

  DateTime GetLinkerTime(Assembly assembly, TimeZoneInfo target = null) {
#if !UNITY_ANDROID
    var filePath = assembly.Location;
    const int c_PeHeaderOffset = 60;
    const int c_LinkerTimestampOffset = 8;

    var buffer = new byte[2048];

    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
      stream.Read(buffer, 0, 2048);

    var offset = BitConverter.ToInt32(buffer, c_PeHeaderOffset);
    var secondsSince1970 = BitConverter.ToInt32(buffer, offset + c_LinkerTimestampOffset);
    var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    var linkTimeUtc = epoch.AddSeconds(secondsSince1970);
    return linkTimeUtc.ToLocalTime();
#else
    return DateTime.Now;
#endif
  }

  // By executing the URL directly windows will open it without making the browser a child
  // process of Tilt Brush.  If this fails or throws an exception we fall back to Unity's
  // OpenURL().
  public static void OpenURL(string url) {
    var isPolyUrl = (url.Contains("poly.google.com/") || url.Contains("vr.google.com"));
    if (isPolyUrl && GoogleIdentity.LoggedIn) {
      var email = GoogleIdentity.Profile.email;
      url = $"https://accounts.google.com/AccountChooser?Email={email}&continue={url}";
    }
#if UNITY_STANDALONE_WINDOWS
    var startInfo = new System.Diagnostics.ProcessStartInfo(url);
    startInfo.UseShellExecute = true;
    try {
      if (System.Diagnostics.Process.Start(startInfo) == null) {
        Application.OpenURL(url);
      }
    } catch (Exception) {
      Application.OpenURL(url);
    }
#else
    // Something about the url makes OpenURL() not work on OSX, so use a workaround
    if (Application.platform == RuntimePlatform.OSXEditor ||
        Application.platform == RuntimePlatform.OSXPlayer) {
      System.Diagnostics.Process.Start(url);
    } else {
      Application.OpenURL(url);
    }
#endif
  }

  /// This copies the support files from inside the Streaming Assets folder to the support folder.
  /// This only happens on Android. The files have to be extracted directly from the .apk.
  private static void CopySupportFiles() {
    if (Application.platform != RuntimePlatform.Android) {
      return;
    }
    if (!Directory.Exists(SupportPath())) {
      Directory.CreateDirectory(SupportPath());
    }

    Func<string, int> GetIndexOfEnd = (s) => Application.streamingAssetsPath.IndexOf(s) + s.Length;

    // Find the apk file
    int apkIndex = GetIndexOfEnd("file://");
    int fileIndex = Application.streamingAssetsPath.IndexOf("!/");
    string apkFilename = Application.streamingAssetsPath.Substring(apkIndex, fileIndex - apkIndex);

    const string supportBeginning = "assets/Support/";

    try {
      using (Stream zipFile = File.Open(apkFilename, FileMode.Open, FileAccess.Read)) {
        ZipLibrary.ZipFile zip = new ZipLibrary.ZipFile(zipFile);
        foreach (ZipLibrary.ZipEntry entry in zip) {
          if (entry.IsFile && entry.Name.StartsWith(supportBeginning)) {
            // Create the directory if needed.
            string fullPath = Path.Combine(App.SupportPath(), 
                                           entry.Name.Substring(supportBeginning.Length));
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory)) {
              Directory.CreateDirectory(directory);
            }

            // Copy the data over to a file.
            using (Stream entryStream = zip.GetInputStream(entry)) {
              using (FileStream fileStream = File.Create(fullPath)) {
                byte[] buffer = new byte[16 * 1024]; // Do it in 16k chunks
                while (true) {
                  int size = entryStream.Read(buffer, 0, buffer.Length);
                  if (size > 0) {
                    fileStream.Write(buffer, 0, size);
                  } else {
                    break;
                  }
                }
              }
            }

          }
        }
        zip.Close();
      }
    } catch (Exception ex) {
      Debug.LogException(ex);
    }
  }

}  // class App
}  // namespace TiltBrush
