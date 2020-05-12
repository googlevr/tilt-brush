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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

using SymmetryMode = TiltBrush.PointerManager.SymmetryMode;

namespace TiltBrush {

public class SketchControlsScript : MonoBehaviour {
  public const string kRemoveHeadsetFyi = "Remove headset to view.";
  const string kTiltBrushGalleryUrl = "https://poly.google.com/tiltbrush";
  const string kBlocksGalleryUrl = "https://poly.google.com/blocks";
  const string kPolyMainPageUri = "https://poly.google.com";

  static public SketchControlsScript m_Instance;
  static bool sm_enableGrabHaptics = true;

  // ------------------------------------------------------------
  // Constants and types
  // ------------------------------------------------------------

  public enum GlobalCommands {
    Null,
    Save,
    SaveNew,
    Load,
    NewSketch,
    StraightEdge,
    AutoOrient,
    Undo,
    Redo,
    Tiltasaurus,
    LightingHdr,
    AudioVisualization,
    ResetAllPanels,
    SketchOrigin,
    SymmetryPlane,
    SymmetryFour,
    ViewOnly,
    SaveGallery,
    LightingLdr,
    ShowSketchFolder,
    About,
    LoadNamedFile, // iParam1 : (optional) - send through a LoadSpeed as int
    DropCam,
    CuratedGallery,
    Unused_UploadToCloud,
    AnalyticsEnabled_Deprecated,
    Credits,
    LogOutOfGenericCloud,
    DraftingVisibility,
    DeleteSketch,
    ShowWindowGUI,
    MorePanels,
    Cameras,
    FAQ,
    ExportRaw,
    IRC,
    YouTubeChat,
    CameraOptions,
    StencilsDisabled,
    AdvancedTools,
    FloatingPanelsMode,
    StraightEdgeMeterDisplay,
    Sketchbook,
    ExportAll,
    Lights,
    SaveAndUpload,
    StraightEdgeShape,
    SaveOptions,
    SketchbookMenu,
    Disco,
    ViewOnlineGallery,
    CancelUpload,
    AdvancedPanelsToggle,
    Music,
    Duplicate,
    ToggleGroupStrokesAndWidgets,
    SaveModel,
    ViewPolyPage,
    ViewPolyGallery,
    ExportListed,
    RenderCameraPath,
    ToggleProfiling,
    DoAutoProfile,
    DoAutoProfileAndQuit,
    ToggleSettings,
    SummonMirror,
    InvertSelection,
    SelectAll,
    FlipSelection,
    ToggleBrushLab,
    ReleaseNotes,
    ToggleCameraPostEffects,
    ToggleWatermark,
    AccountInfo,
    // LoadConfirmUnsaved -> LoadWaitOnDownload -> LoadConfirmComplex -> LoadComplexHigh ->  Load
    LoadConfirmUnsaved,
    LoadConfirmComplex,
    MemoryWarning,
    MemoryExceeded,
    ViewLastUpload,
    LoadConfirmComplexHigh,
    ShowTos,
    ShowPrivacy,
    ShowQuestSideLoading,
    AshleysSketch,
    UnloadReferenceImageCatalog,
    SaveOnLocalChanges,
    ToggleCameraPathVisuals,
    ToggleCameraPathPreview,
    DeleteCameraPath,
    RecordCameraPath,
    SelectCameraPath,
    ToggleAutosimplification,
    ShowGoogleDrive,
    GoogleDriveSync_Folder,  // iParam1: folder id as DriveSync.SyncedFolderType
    GoogleDriveSync,
    LoginToGenericCloud,  // iParam1: Cloud enum
    UploadToGenericCloud,  // iParam1: Cloud enum
    LoadWaitOnDownload,
    SignOutConfirm,
    ReadOnlyNotice,
  }

  public enum ControlsType {
    KeyboardMouse,
    SixDofControllers,
    ViewingOnly
  }

  public enum DraftingVisibilityOption {
    Visible,
    Transparent,
    Hidden
  }

  public enum InputState {
    Standard,
    Pan,
    Rotation,
    HeadLock,
    ControllerLock,
    PushPull,
    BrushSize,
    Save,
    Load,
    Num
  }

  public enum LoadSpeed {
    Normal = -1,
    Quick = 1,
  }

  const float kControlPointHistoryMaxTime = 0.1f;

  class GazeResult {
    public bool m_HitWithGaze;
    public bool m_HitWithController;
    // ReSharper disable once NotAccessedField.Local
    public bool m_WithinView;
    public float m_ControllerDistance;
    public Vector3 m_GazePosition;
    public Vector3 m_ControllerPosition;
    public InputManager.ControllerName m_ControllerName;
  }

  class GrabWidgetControllerInfo {
    public InputManager.ControllerName m_Name;
    /// Transform of controller at the time the grab started
    public TrTransform m_BaseControllerXf;
    /// "local" transform of widget (relative to controller), at the time the grab started.
    /// The widget isn't parented to the controller, but if it were, this would be its transform.
    public TrTransform m_BaseWidgetXf_LS;
  }

  struct GrabWidgetHoldPoint {
    // ReSharper disable once NotAccessedField.Local
    public InputManager.ControllerName m_Name;
    public float m_BirthTime;
    public Vector3 m_Pos; // where controller is holding the widget
    public Quaternion m_Rot;
  }

  class InputStateConfig {
    public bool m_AllowDrawing;
    public bool m_AllowMovement;
    public bool m_ShowGizmo;
  }

  enum FadeState {
    None,
    FadeOn,
    FadeOff
  }

  enum GrabWidgetState {
    None,
    OneHand,
    TwoHands
  }

  enum GrabWorldState {
    Normal,
    ResettingTransform,
    ResetDone
  }

  private enum WorldTransformResetState {
    Default,
    Requested,
    FadingToBlack,
    FadingToScene,
  }

  enum RotationType {
    All,
    RollOnly
  }

  enum GrabIntersectionState {
    RequestIntersections,
    ReadBrush,
    ReadWand
  }

  // ------------------------------------------------------------
  // Inspector data (read-only even if public)
  // ------------------------------------------------------------

  public GameObject m_SketchSurface;
  public SketchMemoryScript.PlaybackMode m_DefaultSketchPlaybackMode;
  public float m_GazeMaxAngleFromPointing = 85.0f;
  public float m_GazeMaxAngleFacingToForward = 80.0f;

  [SerializeField] bool m_AtlasIconTextures;

  [SerializeField] SaveIconTool m_SaveIconTool;
  [SerializeField] DropCamWidget m_DropCam;
  [SerializeField] string m_CreditsSketchFilename;
  [SerializeField] string m_AshleysSketchFilename;
  [SerializeField] float m_DefaultSketchLoadSpeed;
  [SerializeField] GameObject m_TransformGizmoPrefab;

  [SerializeField] GameObject m_RotationIconPrefab;
  [SerializeField] float m_GazeMaxAngleFromFacing = 70.0f;
  [SerializeField] float m_GazeMaxDistance = 10.0f;
  [SerializeField] float m_GazeControllerPointingDistance;
  [SerializeField] float m_GazePanelDectivationDelay = 0.25f;

  [SerializeField] GameObject m_UIReticle;
  [SerializeField] GameObject m_UIReticleMobile;
  [SerializeField] GameObject m_UIReticleSixDofController;

  [SerializeField] float m_DoubleTapWindow;
  [SerializeField] float m_PushPullScale;
  [SerializeField] RotationCursorScript m_RotationCursor;
  [SerializeField] float m_RotationMaxAngle;

  [SerializeField] float m_RotationScalar;
  [SerializeField] float m_RotationRollScalar;
  [SerializeField] float m_PanScalar;

  [SerializeField] float m_AdjustToolSizeScalar;

  [SerializeField] GameObject m_IRCChatPrefab;
  [SerializeField] GameObject m_YouTubeChatPrefab;
  [SerializeField] GameObject m_Decor;
  [SerializeField] BaseTool.ToolType m_InitialTool = BaseTool.ToolType.SketchSurface;
  [SerializeField] string m_ReleaseNotesURL;
  [SerializeField] string m_HelpCenterURL;
  [SerializeField] string m_ThirdPartyNoticesURL;
  [SerializeField] string m_TosURL;
  [SerializeField] string m_PrivacyURL;
  [SerializeField] string m_QuestSideLoadingHowToURL;

  [SerializeField] float m_WorldTransformMinScale = .1f;
  [SerializeField] float m_WorldTransformMaxScale = 10.0f;

  [Header("Undo/Redo Hold")]
  [SerializeField] float m_UndoRedoHold_DurationBeforeStart;
  [SerializeField] float m_UndoRedoHold_RepeatInterval;

  [Header("Pin Cushion")]
  [SerializeField] GameObject m_PinCushionPrefab;

  [Header("Grabbing and tossing")]
  [SerializeField] float m_GrabWorldFadeSpeed = 8.0f;
  [SerializeField] Color m_GrabWorldGridColor = new Color(0.0f, 1.0f, 1.0f, 0.2f);
  [SerializeField] ControllerGrabVisuals m_ControllerGrabVisuals;
  [SerializeField] float m_WidgetGpuIntersectionRadius;

  [Header("Saving")]
  [SerializeField] int m_NumStrokesForSaveIcon = 50;

  [NonSerialized] public Color m_GrabHighlightActiveColor;
  /// Throwing an object faster than this means it's a "toss". Units are m/s.
  public float m_TossThresholdMeters = 3f;
  /// Angular motion contributes more towards the toss velocity the larger the object is;
  /// or rather, the larger the distance between the grab point and the object's center.
  /// To prevent large objects from being too-easily-tossed, bound that distance.
  public float m_TossMaxPivotDistMeters = 0.33f;

  // ------------------------------------------------------------
  // Internal data
  // ------------------------------------------------------------

  private SketchSurfacePanel m_SketchSurfacePanel;
  private SketchMemoryScript.PlaybackMode m_SketchPlaybackMode;
  private GameObject m_TransformGizmo;
  private TransformGizmoScript m_TransformGizmoScript;
  private GameObject m_RotationIcon;
  private float m_MouseDeltaX;
  private float m_MouseDeltaY;
  private float m_MouseDeltaXScaled;
  private float m_MouseDeltaYScaled;
  private float m_PositionOffsetResetTapTime;
  private bool m_EatToolScaleInput;

  private PanelManager m_PanelManager;
  private WidgetManager m_WidgetManager;
  private PinCushion m_PinCushion;
  private bool m_EatPinCushionInput;

  // This is the gaze that was used to compute m_CurrentGazeHitPoint.
  // It is not a general substitute for ViewpointScript.Gaze.
  private Ray m_CurrentGazeRay;
  private Quaternion m_CurrentHeadOrientation;
  private GazeResult[] m_GazeResults;
  private int m_CurrentGazeObject;
  private bool m_EatInputGazeObject;
  private Vector3 m_CurrentGazeHitPoint;
  private Ray m_GazeControllerRay;
  private Ray m_GazeControllerRayActivePanel;
  private bool m_ForcePanelActivation = false;
  private float m_GazePanelDectivationCountdown;
  private bool m_PanelsVisibilityRequested;
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
  private bool m_HeadOffset;
#endif

  float m_UndoHold_Timer;
  float m_RedoHold_Timer;

  // Grab world member variables.
  struct GrabState {
    public InputManager.ControllerName name;
    public TrTransform grabTransform;
    public bool grabbingWorld;
    public bool grabbingGroup;
    public bool startedGrabInsideWidget;
    public bool eatInput;
    private GrabWidget lastWidgetIntersect;

    public void SetHadBestGrabAndTriggerHaptics(GrabWidgetData data) {
      bool dormant = WidgetManager.m_Instance.WidgetsDormant;
      if (data != null && !data.m_WidgetScript.AllowDormancy) {
        dormant = false;
      }
      GrabWidget newInsideWidget = (data != null && !dormant) ? data.m_WidgetScript : null;
      if (sm_enableGrabHaptics && newInsideWidget != lastWidgetIntersect) {
        // state changed
        if (newInsideWidget != null) {
          // transitioning in
          InputManager.m_Instance.TriggerHaptics(name, data.m_WidgetScript.HapticDuration);
        } else {
          // transitioning out
          InputManager.m_Instance.TriggerHaptics(name, 0.03f);
        }
      }
      lastWidgetIntersect = newInsideWidget;
    }

    public void ClearInsideWidget() {
      lastWidgetIntersect = null;
    }
  }
  private GrabState m_GrabBrush = new GrabState { name = InputManager.ControllerName.Brush };
  private GrabState m_GrabWand = new GrabState { name = InputManager.ControllerName.Wand };

  private WorldTransformResetState m_WorldTransformResetState = WorldTransformResetState.Default;
  private TrTransform m_WorldTransformResetXf = TrTransform.identity; // set when reset requested
  private GrabWorldState m_GrabWorldState = GrabWorldState.Normal;
  private float m_WorldTransformFadeAmount;
  private bool m_AllowWorldTransformLastFrame = false;
  private bool m_WorldBeingGrabbed;
  private TrTransform m_xfDropCamReset_RS;

  struct GpuIntersectionResult {
    public GpuIntersector.FutureModelResult result;
    public List<GpuIntersector.ModelResult> resultList;
  }
  private Queue<GpuIntersectionResult> m_BrushResults;
  private Queue<GpuIntersectionResult> m_WandResults;
  private int m_WidgetGpuIntersectionLayer;

  private GrabWidget m_CurrentGrabWidget;
  private GrabWidget m_MaybeDriftingGrabWidget; // use only to clear drift

  // References to widgets, cached in the UpdateGrab_None, to be used by helper functions
  // for the remainder of the frame.
  private GrabWidget m_PotentialGrabWidgetBrush;
  private GrabWidget m_PotentialGrabWidgetWand;

  // Flags for the explaining if the m_PotentialGrabWidget_x widgets are able to be interacted with.
  // Cached in the UpdateGrab_None, used for the remainder of the frame.
  private bool m_PotentialGrabWidgetBrushValid;
  private bool m_PotentialGrabWidgetWandValid;

  // References to widget metadata, cached in UpdateGrab_None, to be re-used on "off frames"
  // when the GPU intersector is not refreshing the nearest widget to the respective controller.
  private GrabWidgetData m_BackupBrushGrabData;
  private GrabWidgetData m_BackupWandGrabData;

  private GrabWidgetState m_GrabWidgetState;
  private GrabWidgetControllerInfo m_GrabWidgetOneHandInfo;
  private TrTransform m_GrabWidgetTwoHandBrushPrev;
  private TrTransform m_GrabWidgetTwoHandWandPrev;
  private Queue<GrabWidgetHoldPoint> m_GrabWidgetHoldHistory;

  private Quaternion m_RotationOrigin;
  private Vector2 m_RotationCursorOffset;

  private bool m_RotationRollActive;
  private float m_RotationResetTapTime;

  private RotationType m_CurrentRotationType;
  private bool m_AutoOrientAfterRotation;

  private Vector3 m_SurfaceForward;
  private Vector3 m_SurfaceRight;
  private Vector3 m_SurfaceUp;

  private Vector3 m_SurfaceLockOffset;
  private Vector3 m_SurfaceLockBaseSurfacePosition;
  private Vector3 m_SurfaceLockBaseControllerPosition;
  private Quaternion m_SurfaceLockBaseHeadRotation;
  private Quaternion m_SurfaceLockBaseControllerRotation;
  private Quaternion m_SurfaceLockBaseSurfaceRotation;
  private InputManager.ControllerName m_SurfaceLockActingController;
  private float m_SurfaceLockControllerBaseScalar;
  private float m_SurfaceLockControllerScalar;

  private bool m_PositioningPanelWithHead;
  private Quaternion m_PositioningPanelBaseHeadRotation;
  private Vector3 m_PositioningPanelOffset;
  private float m_PositioningTimer;
  private float m_PositioningSpeed;

  private DraftingVisibilityOption m_DraftingVisibility = DraftingVisibilityOption.Visible;

  private Vector3 m_SketchOrigin;

  private ControlsType m_ControlsType;
  private GrabWidget m_IRCChatWidget;
  private GrabWidget m_YouTubeChatWidget;
  private MultiCamCaptureRig m_MultiCamCaptureRig;
  private CameraPathCaptureRig m_CameraPathCaptureRig;

  private bool m_ViewOnly = false;

  private InputState m_CurrentInputState;
  private InputStateConfig[] m_InputStateConfigs;

  private GrabIntersectionState m_CurrentGrabIntersectionState;

  private float m_WorldTransformSpeedSmoothed;

  // ------------------------------------------------------------
  // Properties and events
  // ------------------------------------------------------------

  public MultiCamCaptureRig MultiCamCaptureRig {
    get { return m_MultiCamCaptureRig; }
  }

  public CameraPathCaptureRig CameraPathCaptureRig {
    get { return m_CameraPathCaptureRig; }
  }

  public ControllerGrabVisuals ControllerGrabVisuals {
    get { return m_ControllerGrabVisuals; }
  }

  public SketchMemoryScript.PlaybackMode SketchPlaybackMode {
    get { return m_SketchPlaybackMode; }
    set { m_SketchPlaybackMode = value; }
  }

  public Transform m_Canvas {
    get { return App.Instance.m_CanvasTransform; }
  }

  public ControlsType ActiveControlsType {
    get { return m_ControlsType; }
    set { m_ControlsType = value; }
  }

  public float WorldTransformMinScale {
    get { return App.UserConfig.Flags.UnlockScale ? m_WorldTransformMinScale * 0.01f :
        m_WorldTransformMinScale; }
  }

  public float WorldTransformMaxScale {
    get { return App.UserConfig.Flags.UnlockScale ? m_WorldTransformMaxScale * 10.0f :
        m_WorldTransformMaxScale; }
  }

  public void SetInitialTool(BaseTool.ToolType rType) {
    m_InitialTool = rType;
  }

  public void SetInFreePaintMode(bool bFreePaint) {
    m_SketchSurfacePanel.SetInFreePaintMode(bFreePaint);
  }

  public float GazeMaxDistance {
    get { return m_GazeMaxDistance; }
  }

  public InputManager.ControllerName OneHandGrabController {
    get {
      return m_CurrentGrabWidget != null ?
        m_GrabWidgetOneHandInfo.m_Name :
        InputManager.ControllerName.None;
      }
  }

  public InputManager.ControllerName PotentialOneHandGrabController(GrabWidget widget) {
    if (m_PotentialGrabWidgetBrush == widget) {
      return InputManager.ControllerName.Brush;
    } else if (m_PotentialGrabWidgetWand == widget) {
      return InputManager.ControllerName.Wand;
    }
    return OneHandGrabController;
  }

  public Vector3 GetSurfaceForward() { return m_SurfaceForward; }
  public Vector3 GetSurfaceUp() { return m_SurfaceUp; }
  public Vector3 GetSurfaceRight() { return m_SurfaceRight; }
  public Vector3 GetSketchOrigin() { return m_SketchOrigin; }
  public float GetDefaultSketchLoadSpeed() { return m_DefaultSketchLoadSpeed; }
  public Quaternion GetCurrentHeadOrientation() { return m_CurrentHeadOrientation; }
  public Vector3 GetUIReticlePos() { return m_UIReticle.transform.position; }
  public Vector3 GetSweetSpotPos() { return m_PanelManager.m_SweetSpot.transform.position; }
  public void SetSketchOrigin(Vector3 vOrigin) { m_SketchOrigin = vOrigin; }

  public void EatGazeObjectInput() {
    m_EatInputGazeObject = true;
    m_GazePanelDectivationCountdown = 0.0f;
    PointerManager.m_Instance.EatLineEnabledInput();
    SketchSurfacePanel.m_Instance.EatToolsInput();
  }
  public void EatToolScaleInput() { m_EatToolScaleInput = true; }
  public void EatGrabInput() { m_GrabWand.eatInput = true; m_GrabBrush.eatInput = true; }

  public bool ShouldRespondToPadInput(InputManager.ControllerName name) {
    if (name == InputManager.ControllerName.Brush && m_CurrentGazeObject != -1) {
      return m_PanelManager.GetPanel(m_CurrentGazeObject).BrushPadAnimatesOnHover();
    }
    return !m_EatToolScaleInput && SketchSurfacePanel.m_Instance.CanAdjustToolSize();
  }
  public void ForcePanelActivation(bool bForce) {
    m_ForcePanelActivation = bForce;
    if (m_ForcePanelActivation) {
      m_GazePanelDectivationCountdown = m_GazePanelDectivationDelay;
    }
  }
  public bool IsUserInteractingWithUI() {
    return (m_CurrentGazeObject != -1) || (m_GazePanelDectivationCountdown > 0.0f);
  }
  public bool IsUIBlockingUndoRedo() {
    if (m_CurrentGazeObject != -1) {
      return m_PanelManager.GetPanel(m_CurrentGazeObject).UndoRedoBlocked();
    }
    return false;
  }
  public bool IsUserAbleToInteractWithAnyWidget() {
    return IsUserInteractingWithAnyWidget() ||
        (m_PotentialGrabWidgetBrush != null && m_PotentialGrabWidgetBrushValid) ||
        (m_PotentialGrabWidgetWand != null && m_PotentialGrabWidgetWandValid);
  }
  public bool IsUserInteractingWithAnyWidget() { return m_CurrentGrabWidget != null; }
  public bool IsUserGrabbingAnyPanel() {
    return (m_CurrentGrabWidget != null && m_CurrentGrabWidget is PanelWidget);
  }
  public bool IsUsersBrushIntersectingWithSelectionWidget() {
    return (m_PotentialGrabWidgetBrush != null &&
        m_PotentialGrabWidgetBrushValid &&
        m_PotentialGrabWidgetBrush is SelectionWidget);
  }
  public bool IsUserIntersectingWithSelectionWidget() {
    return IsUsersBrushIntersectingWithSelectionWidget() ||
        (m_PotentialGrabWidgetWand != null &&
        m_PotentialGrabWidgetWandValid &&
        m_PotentialGrabWidgetWand is SelectionWidget);
  }
  public bool IsUserInteractingWithSelectionWidget() {
    return (m_CurrentGrabWidget != null && m_CurrentGrabWidget is SelectionWidget);
  }

  public bool IsUserGrabbingWorld() { return m_GrabWand.grabbingWorld || m_GrabBrush.grabbingWorld; }
  public bool IsUserGrabbingWorldWithBrushHand() { return m_GrabBrush.grabbingWorld; }
  public bool IsUserTransformingWorld() { return m_GrabWand.grabbingWorld && m_GrabBrush.grabbingWorld; }
  public float GetGazePanelActivationRatio() { return m_GazePanelDectivationCountdown / m_GazePanelDectivationDelay; }
  public bool IsCurrentGrabWidgetPinned() { return IsUserInteractingWithAnyWidget() && m_CurrentGrabWidget.Pinned; }
  public bool CanCurrentGrabWidgetBePinned() { return IsUserInteractingWithAnyWidget() && m_CurrentGrabWidget.AllowPinning; }
  public bool DidUserGrabWithBothInside() { return m_GrabBrush.startedGrabInsideWidget && m_GrabWand.startedGrabInsideWidget; }
  public bool IsUserGrabbingWidget(GrabWidget widget) { return widget == m_CurrentGrabWidget; }
  public bool IsUserTwoHandGrabbingWidget() { return m_GrabWidgetState == GrabWidgetState.TwoHands; }
  public bool IsPinCushionShowing() { return m_PinCushion.IsShowing(); }
  public bool IsUserLookingAtPanel(BasePanel panel) {
    return m_CurrentGazeObject > -1 &&
      m_PanelManager.GetAllPanels()[m_CurrentGazeObject].m_Panel == panel;
  }

  public SaveIconTool GetSaveIconTool() {
    return m_SaveIconTool;
  }

  public DropCamWidget GetDropCampWidget() {
    return m_DropCam;
  }

  public bool IsGrabWorldStateStable() {
    return m_GrabWorldState == GrabWorldState.Normal;
  }

  public bool InGrabCanvasMode {
    get {
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
      if (Config.IsExperimental) {
        if (App.Scene.ActiveCanvas != App.Scene.MainCanvas) {
          return true;
        }
      }
#endif
      return false;
    }
  }

  // Internal: modify Coords.ScenePose or Coords.CanvasPose depending on the
  // state of m_InTransformCanvasMode
  TrTransform GrabbedPose {
    get {
      return InGrabCanvasMode ? App.Scene.ActiveCanvas.Pose : App.Scene.Pose;
    }
    set {
      if (InGrabCanvasMode) {
        App.Scene.ActiveCanvas.Pose = value;
      } else {
        App.Scene.Pose = value;
      }
    }
  }

  public Transform GazeObjectTransform() {
    if (m_CurrentGazeObject != -1) {
      return m_PanelManager.GetPanel(m_CurrentGazeObject).transform;
    }
    return null;
  }

  public void ForceShowUIReticle(bool bVisible) {
    m_UIReticle.SetActive(bVisible);
  }

  public void SetUIReticleTransform(Vector3 vPos, Vector3 vForward) {
    m_UIReticle.transform.position = vPos;
    m_UIReticle.transform.forward = vForward;
  }

  public bool AtlasIconTextures {
    get { return m_AtlasIconTextures; }
  }

  public IconTextureAtlas IconTextureAtlas {
    get { return GetComponent<IconTextureAtlas>(); }
  }

  void DismissPopupOnCurrentGazeObject(bool force) {
    if (m_CurrentGazeObject != -1) {
      m_PanelManager.GetPanel(m_CurrentGazeObject).CloseActivePopUp(force);
    }
  }

  void Awake() {
    m_Instance = this;

    BrushController.m_Instance.BrushSetToDefault += OnBrushSetToDefault;

    IconTextureAtlas.Init();

    m_MultiCamCaptureRig = GetComponentInChildren<MultiCamCaptureRig>(true);
    m_MultiCamCaptureRig.Init();

    m_CameraPathCaptureRig = GetComponentInChildren<CameraPathCaptureRig>(true);
    m_CameraPathCaptureRig.Init();

    m_SketchSurfacePanel = m_SketchSurface.GetComponent<SketchSurfacePanel>();
    m_PanelManager = GetComponent<PanelManager>();
    m_PanelManager.Init();
    InitGazePanels();

    m_WidgetManager = GetComponent<WidgetManager>();
    m_WidgetManager.Init();

    m_InputStateConfigs = new InputStateConfig[(int)InputState.Num];
    for (int i = 0; i < (int)InputState.Num; ++i) {
      m_InputStateConfigs[i] = new InputStateConfig();
      m_InputStateConfigs[i].m_AllowDrawing = false;
      m_InputStateConfigs[i].m_AllowMovement = true;
      m_InputStateConfigs[i].m_ShowGizmo = false;
    }

    m_InputStateConfigs[(int)InputState.Standard].m_AllowDrawing = true;
    m_InputStateConfigs[(int)InputState.Pan].m_AllowDrawing = true;
    m_InputStateConfigs[(int)InputState.HeadLock].m_AllowDrawing = true;
    m_InputStateConfigs[(int)InputState.ControllerLock].m_AllowDrawing = true;
    m_InputStateConfigs[(int)InputState.PushPull].m_AllowDrawing = true;

    m_InputStateConfigs[(int)InputState.Pan].m_AllowMovement = false;
    m_InputStateConfigs[(int)InputState.Rotation].m_AllowMovement = false;
    m_InputStateConfigs[(int)InputState.ControllerLock].m_AllowMovement = false;
    m_InputStateConfigs[(int)InputState.PushPull].m_AllowMovement = false;
    m_InputStateConfigs[(int)InputState.BrushSize].m_AllowMovement = false;

    m_InputStateConfigs[(int)InputState.Pan].m_ShowGizmo = true;
    m_InputStateConfigs[(int)InputState.Rotation].m_ShowGizmo = true;
    m_InputStateConfigs[(int)InputState.HeadLock].m_ShowGizmo = true;
    m_InputStateConfigs[(int)InputState.PushPull].m_ShowGizmo = true;

    m_CurrentGazeRay = new Ray(Vector3.zero, Vector3.forward);
    m_GazeControllerRay = new Ray(Vector3.zero, Vector3.forward);
    m_GazeControllerRayActivePanel = new Ray(Vector3.zero, Vector3.forward);

    m_GrabWidgetHoldHistory = new Queue<GrabWidgetHoldPoint>();
    m_GrabWidgetOneHandInfo = new GrabWidgetControllerInfo();

    // Initialize world grip members.
    m_GrabBrush.grabTransform = TrTransform.identity;
    m_GrabWand.grabTransform = TrTransform.identity;

    m_BrushResults = new Queue<GpuIntersectionResult>();
    m_WandResults = new Queue<GpuIntersectionResult>();
    m_WidgetGpuIntersectionLayer = LayerMask.NameToLayer("GpuIntersection");
    m_CurrentGrabIntersectionState = GrabIntersectionState.RequestIntersections;
  }

  public void InitGazePanels() {
    // Find all gaze panels.
    int iNumGazePanels = m_PanelManager.GetAllPanels().Count;
    m_GazeResults = new GazeResult[iNumGazePanels];
    for (int i = 0; i < iNumGazePanels; ++i) {
      m_GazeResults[i] = new GazeResult();
      m_GazeResults[i].m_HitWithGaze = false;
      m_GazeResults[i].m_HitWithController = false;
      m_GazeResults[i].m_WithinView = false;
      m_GazeResults[i].m_GazePosition = new Vector3();
    }
  }

  public void OnEnable() {
    // This needs to run before other tools initialize, which is why it's running in OnEnable.
    // The sequence is Awake(), OnEnable(), Start().
    if (App.VrSdk.GetControllerDof() == VrSdk.DoF.Six) {
      SetInFreePaintMode(true);
      SetInitialTool(BaseTool.ToolType.FreePaintTool);
    }
  }

  void Start() {
    m_TransformGizmo = (GameObject)Instantiate(m_TransformGizmoPrefab);
    m_TransformGizmo.transform.parent = transform;
    m_TransformGizmoScript = m_TransformGizmo.GetComponent<TransformGizmoScript>();
    m_TransformGizmo.SetActive(false);

    m_RotationIcon = (GameObject)Instantiate(m_RotationIconPrefab);
    m_RotationIcon.transform.position = m_SketchSurface.transform.position;
    m_RotationIcon.transform.parent = m_SketchSurface.transform;
    m_RotationIcon.SetActive(false);

    GameObject pinCushionObj = (GameObject)Instantiate(m_PinCushionPrefab);
    m_PinCushion = pinCushionObj.GetComponent<PinCushion>();

    m_PositionOffsetResetTapTime = 0.0f;

    m_UndoHold_Timer = m_UndoRedoHold_DurationBeforeStart;
    m_RedoHold_Timer = m_UndoRedoHold_DurationBeforeStart;

    m_AutoOrientAfterRotation = true;
    m_RotationCursor.gameObject.SetActive(false);

    ResetGrabbedPose();
    m_SketchOrigin = m_SketchSurface.transform.position;

    m_PanelManager.InitPanels(m_ControlsType == ControlsType.SixDofControllers);

    m_UIReticleMobile.SetActive(m_ControlsType == ControlsType.ViewingOnly);
    m_UIReticleSixDofController.SetActive(m_ControlsType != ControlsType.ViewingOnly);

    m_PositioningPanelWithHead = false;
    m_PositioningSpeed = 16.0f;

    m_CurrentRotationType = RotationType.All;
    m_RotationResetTapTime = 0.0f;

    m_CurrentInputState = InputState.Standard;

    m_SketchSurfacePanel.EnableSpecificTool(m_InitialTool);
    m_SurfaceLockControllerBaseScalar = m_SketchSurfacePanel.m_PanelSensitivity;

    //after initializing, start with gaze objects hidden
    m_CurrentGazeObject = -1;
    m_EatInputGazeObject = false;

    int hidePanelsDelay = 1;
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      hidePanelsDelay = 0;
    }
#endif
    StartCoroutine(DelayedHidePanels(hidePanelsDelay));

    m_DropCam.Show(false);

    m_GrabWidgetState = GrabWidgetState.None;

    UpdateDraftingVisibility();
  }

  private IEnumerator<Timeslice> DelayedHidePanels(int frames) {
    int stall = frames;
    while (stall-- > 0) {
      yield return null;
    }

    m_PanelManager.HidePanelsForStartup();
    RequestPanelsVisibility(false);
  }

  void Update() {
    // TODO: we need to figure out what transform to pass in here!
    // Maybe best _just for now_ to use the scene transform?
    TrTransform scenePose = App.Scene.Pose;
    Shader.SetGlobalMatrix("xf_CS", scenePose.ToMatrix4x4());
    Shader.SetGlobalMatrix("xf_I_CS", scenePose.inverse.ToMatrix4x4());
  }

  void LateUpdate() {
    // Gracefully exits if we're not recording a video.
    VideoRecorderUtils.SerializerNewUsdFrame();
  }

  public void UpdateControls() {
    UnityEngine.Profiling.Profiler.BeginSample("SketchControlsScript.UpdateControls");
    m_SketchSurfacePanel.m_UpdatedToolThisFrame = false;

    // Verify controllers are available and prune state if they're not.
    if (App.VrSdk.GetControllerDof() == VrSdk.DoF.Six &&
        App.VrSdk.IsInitializingSteamVr) {
      m_PanelManager.SetVisible(false);
      PointerManager.m_Instance.RequestPointerRendering(false);
      return;
    }

    //mouse movement
    Vector2 mv = InputManager.m_Instance.GetMouseMoveDelta();
    m_MouseDeltaX = mv.x;
    m_MouseDeltaY = mv.y;

    UpdateGazeObjectsAnimationState();
    UpdateCurrentGazeRay();
    m_SketchSurfacePanel.SetBacksideActive(m_CurrentGazeRay.origin);
    m_PanelManager.UpdatePanels();

    m_MouseDeltaXScaled = m_MouseDeltaX * GetAppropriateMovementScalar();
    m_MouseDeltaYScaled = m_MouseDeltaY * GetAppropriateMovementScalar();

    //this is used for one-shot inputs that don't require state and do not change state
    UpdateBaseInput();

    UpdatePinCushionVisibility();

    //if the pointer manager is processing, we don't want to respond to input
    if (!PointerManager.m_Instance.IsMainPointerProcessingLine()) {

      //see if we're grabbing a widget
      UpdateGrab();

      //see if we're looking at a gaze object
      RefreshCurrentGazeObject();

      // Tools allowed when widgets aren't grabbed.
      bool bWidgetGrabOK = m_GrabWidgetState == GrabWidgetState.None;

      // If we don't have a widget held and we're not grabbing the world with the brush controller,
      // update tools.
      if (bWidgetGrabOK && !m_GrabBrush.grabbingWorld) {
        if (m_CurrentGazeObject != -1 && !m_WorldBeingGrabbed) {
          UpdateActiveGazeObject();

          // Allow for standard input (like Undo / Redo) even when gazing at a panel.
          if (m_CurrentInputState == InputState.Standard) {
            UpdateStandardInput();
          }
        } else {
          //standard input, no gaze object
          if (m_InputStateConfigs[(int)m_CurrentInputState].m_AllowMovement) {
            m_SketchSurfacePanel.UpdateReticleOffset(m_MouseDeltaX, m_MouseDeltaY);
          }

          switch (m_CurrentInputState) {
          case InputState.Standard: UpdateStandardInput(); break;
          case InputState.Pan: UpdatePanInput(); break;
          case InputState.Rotation: UpdateRotationInput(); break;
          case InputState.HeadLock: UpdateHeadLockInput(); break;
          case InputState.ControllerLock: UpdateControllerLock(); break;
          case InputState.PushPull: UpdatePushPullInput(); break;
          case InputState.Save: UpdateSaveInput(); break;
          case InputState.Load: UpdateLoadInput(); break;
          }

          //keep pointer locked in the right spot, even if it's hidden
          if (m_SketchSurfacePanel.ActiveTool.LockPointerToSketchSurface()) {
            Vector3 vPointerPos = Vector3.zero;
            Vector3 vPointerForward = Vector3.zero;
            m_SketchSurfacePanel.GetReticleTransform(out vPointerPos, out vPointerForward,
              (m_ControlsType == ControlsType.ViewingOnly));
            PointerManager.m_Instance.SetMainPointerPosition(vPointerPos);
            PointerManager.m_Instance.SetMainPointerForward(vPointerForward);
          }

          m_SketchSurfacePanel.AllowDrawing(m_InputStateConfigs[(int)m_CurrentInputState].m_AllowDrawing);
          m_SketchSurfacePanel.UpdateCurrentTool();

          PointerManager.m_Instance.AllowPointerPreviewLine(
              !m_PinCushion.IsShowing() &&
              !PointerManager.m_Instance.IsStraightEdgeProxyActive() &&
              !InputManager.m_Instance.ControllersAreSwapping() &&
              (m_SketchSurfacePanel.IsSketchSurfaceToolActive() ||
               (m_SketchSurfacePanel.GetCurrentToolType() == BaseTool.ToolType.FreePaintTool)));

          //keep transform gizmo at sketch surface pos
          m_TransformGizmo.transform.position = m_SketchSurface.transform.position;
          bool bGizmoActive = m_InputStateConfigs[(int)m_CurrentInputState].m_ShowGizmo && m_SketchSurfacePanel.ShouldShowTransformGizmo();
          m_TransformGizmo.SetActive(bGizmoActive);
        }
      }
    }

    // Update any transition to a scene transform reset.
    UpdateWorldTransformReset();

    //update our line after all input and tools have chimed in on the state of it
    PointerManager.m_Instance.UpdateLine();
    UnityEngine.Profiling.Profiler.EndSample();
  }

  public void UpdateControlsPostIntro() {
    m_PanelManager.UpdatePanels();
    UpdateCurrentGazeRay();
    UpdateGazeObjectsAnimationState();
    RefreshCurrentGazeObject();
    UpdateSwapControllers();
    if (m_CurrentGazeObject > -1) {
      UpdateActiveGazeObject();
    }
  }

  public void UpdateControlsForLoading() {
    UpdateCurrentGazeRay();
    m_PanelManager.UpdatePanels();
    UpdateGazeObjectsAnimationState();
    UpdateGrab();
    UpdateWorldTransformReset();

    if (m_GrabWidgetState == GrabWidgetState.None && m_CurrentGazeObject == -1 &&
        m_SketchSurfacePanel.ActiveTool.AvailableDuringLoading() &&
        !m_GrabBrush.grabbingWorld) {
      m_SketchSurfacePanel.UpdateCurrentTool();
    }
  }

  public void UpdateControlsForReset() {
    UpdateGrab();
    UpdateCurrentGazeRay();
    UpdatePinCushionVisibility();
    m_PanelManager.UpdatePanels();
    UpdateGazeObjectsAnimationState();
    PointerManager.m_Instance.UpdateLine();
  }

  public void UpdateControlsForUploading() {
    UpdateCurrentGazeRay();
    UpdatePinCushionVisibility();
    m_PanelManager.UpdatePanels();
    UpdateGazeObjectsAnimationState();
  }

  public void UpdateControlsForMemoryExceeded() {
    UpdateGrab();
    m_SketchSurfacePanel.m_UpdatedToolThisFrame = false;
    m_PanelManager.UpdatePanels();
    UpdateCurrentGazeRay();
    UpdateGazeObjectsAnimationState();
    RefreshCurrentGazeObject();
    if (m_CurrentGazeObject > -1) {
      UpdateActiveGazeObject();
    }
  }

  void UpdatePinCushionVisibility() {
    // If the pin cushion is showing and the user cancels, eat the input.
    if (m_PinCushion.IsShowing()) {
      if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate) ||
          InputManager.Brush.GetControllerGrip() ||
          InputManager.Wand.GetControllerGrip() ||
          IsUserInteractingWithAnyWidget() ||
          IsUserInteractingWithUI()) {
        m_EatPinCushionInput = true;
      }
    }

    // If our tool wants the input blocked, maintain the input eat state until
    // after the user has let off input.
    if (m_SketchSurfacePanel.ActiveTool.BlockPinCushion()) {
      m_EatPinCushionInput = true;
    }

    bool inputValid =
        InputManager.m_Instance.GetCommand(InputManager.SketchCommands.ShowPinCushion);
    bool show = inputValid && CanUsePinCushion();
    m_PinCushion.ShowPinCushion(show && !m_EatPinCushionInput);
    m_EatPinCushionInput = m_EatPinCushionInput && inputValid;
  }

  bool CanUsePinCushion() {
    return (m_ControlsType == ControlsType.SixDofControllers) &&
        m_PanelManager.AdvancedModeActive() &&
        !InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate) &&
        !InputManager.Brush.GetControllerGrip() &&
        !InputManager.Wand.GetControllerGrip() &&
        !IsUserInteractingWithAnyWidget() &&
        !IsUserInteractingWithUI() &&
        !m_SketchSurfacePanel.ActiveTool.BlockPinCushion() &&
        App.Instance.IsInStateThatAllowsPainting();
  }

  void UpdateCurrentGazeRay() {
    var head = ViewpointScript.Head;
    m_CurrentGazeRay = new Ray(head.position, head.forward);
    m_CurrentHeadOrientation = head.rotation;

    // We use the gaze ray for certain shader effects - like edge falloff.
    Shader.SetGlobalVector("_WorldSpaceRootCameraPosition", m_CurrentGazeRay.origin);
    bool hasController = m_ControlsType == ControlsType.SixDofControllers;
    if (hasController) {
      if (InputManager.Brush.IsTrackedObjectValid) {
        Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
        m_GazeControllerRay.direction = rAttachPoint.forward;
        m_GazeControllerRay.origin = rAttachPoint.position;
      } else {
        // If the brush controller isn't tracked, put our controller ray out of the way.
        float fBig = 9999999.0f;
        m_GazeControllerRay.direction = Vector3.one;
        m_GazeControllerRay.origin = new Vector3(fBig, fBig, fBig);
      }

      m_GazeControllerRayActivePanel.direction = m_GazeControllerRay.direction;
      m_GazeControllerRayActivePanel.origin = m_GazeControllerRay.origin;
      m_GazeControllerRayActivePanel.origin -= (m_GazeControllerRayActivePanel.direction * 0.5f);
    }
  }

  public void UpdateGazeObjectsAnimationState() {
    // Are the panels allowed to be visible?
    bool isSixDof = m_ControlsType == ControlsType.SixDofControllers;
    if ((!isSixDof) ||
        (InputManager.Wand.IsTrackedObjectValid &&
        !m_SketchSurfacePanel.ActiveTool.HidePanels() &&
        !App.Instance.IsLoading())) {
      // Transition panels according to requested visibility.
      m_PanelManager.SetVisible(m_PanelsVisibilityRequested);
    } else {
      // Transition out.
      m_PanelManager.SetVisible(false);
    }
  }

  void UpdateBaseInput() {
    UnityEngine.Profiling.Profiler.BeginSample("SketchControlScript.UpdateBaseInput");
    if (m_ControlsType == ControlsType.SixDofControllers) {
      m_PanelManager.UpdateWandOrientationControls();
    }

    //allow tool scaling if we're not drawing and our input device is active
    bool bScaleInputActive = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Scale);
    bool bScaleCommandActive =
         !InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate)
      && m_GrabBrush.grabbingWorld == false
      && bScaleInputActive
      && m_CurrentGazeObject == -1   // free up swipe for use by gaze object
      && ((m_ControlsType != ControlsType.SixDofControllers)
          || InputManager.Brush.IsTrackedObjectValid
          );

    if (m_EatToolScaleInput) {
      m_EatToolScaleInput = bScaleInputActive;
    }

    if (bScaleCommandActive && !m_EatToolScaleInput) {
      if (m_GrabWidgetState == GrabWidgetState.None) {
        //send scale command down to current tool
        m_SketchSurfacePanel.UpdateToolSize(
            m_AdjustToolSizeScalar * InputManager.m_Instance.GetAdjustedBrushScrollAmount());
      }

      //ugly, but brush size is becoming not an input state
      m_MouseDeltaX = 0.0f;
      m_MouseDeltaY = 0.0f;
    }

    UpdateSwapControllers();
    UnityEngine.Profiling.Profiler.EndSample();
  }

  void UpdateSwapControllers() {
    // Don't allow controller swap in first run intro.
    // Don't allow controller swap if we're grabbing a widget.
    // Don't allow controller swap if a Logitech pen is present.
    if (!TutorialManager.m_Instance.TutorialActive() &&
        m_GrabWidgetState == GrabWidgetState.None &&
        !App.VrSdk.VrControls.LogitechPenIsPresent()) {
      if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.SwapControls)) {
        InputManager.m_Instance.WandOnRight = !InputManager.m_Instance.WandOnRight;
        InputManager.m_Instance.GetControllerBehavior(InputManager.ControllerName.Brush)
            .DisplayControllerSwapAnimation();
        InputManager.m_Instance.GetControllerBehavior(InputManager.ControllerName.Wand)
            .DisplayControllerSwapAnimation();
        AudioManager.m_Instance.PlayControllerSwapSound(
            InputManager.m_Instance.GetControllerPosition(InputManager.ControllerName.Brush));
      }
    }
  }

  void UpdateStandardInput() {
    UnityEngine.Profiling.Profiler.BeginSample("SketchControlScript.UpdateStandardInput");
    //debug keys
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      var camTool = SketchSurfacePanel.m_Instance.ActiveTool as MultiCamTool;

      if (InputManager.m_Instance.GetKeyboardShortcutDown(InputManager.KeyboardShortcut.SaveNew)) {
        IssueGlobalCommand(GlobalCommands.SaveNew, 1);
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.ExportAll)) {
        IssueGlobalCommand(GlobalCommands.ExportAll);
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.SwitchCamera) && camTool != null) {
        camTool.ExternalObjectNextCameraStyle();  // For monoscopic mode
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.CycleCanvas)) {
        if (InputManager.m_Instance.GetAnyShift()) {
          // Create new layer if on main canvas,
          // otherwise squash current layer to main
          if (App.Scene.ActiveCanvas == App.Scene.MainCanvas) {
            App.Scene.Test_AddLayer();
          } else {
            App.Scene.Test_SquashCurrentLayer();
          }
        } else {
          App.Scene.Test_CycleCanvas();
        }
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.ViewOnly)) {
        IssueGlobalCommand(GlobalCommands.ViewOnly);
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.ToggleScreenMirroring)) {
        ViewpointScript.m_Instance.ToggleScreenMirroring();
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.PreviousTool)) {
        m_SketchSurfacePanel.PreviousTool();
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.NextTool)) {
        m_SketchSurfacePanel.NextTool();
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.CycleSymmetryMode)) {
        var cur = PointerManager.m_Instance.CurrentSymmetryMode;
        var next = (cur == SymmetryMode.None) ? SymmetryMode.SinglePlane
          : (cur == SymmetryMode.SinglePlane) ? SymmetryMode.DebugMultiple
          : (cur == SymmetryMode.DebugMultiple) ? SymmetryMode.FourAroundY
          : SymmetryMode.None;
        PointerManager.m_Instance.CurrentSymmetryMode = next;
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.Export)) {
        StartCoroutine(ExportCoroutine());
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
              InputManager.KeyboardShortcut.StoreHeadTransform) &&
          InputManager.m_Instance.GetAnyShift()) {
        Transform head = ViewpointScript.Head;
        PlayerPrefs.SetFloat("HeadOffset_localPositionX", head.localPosition.x);
        PlayerPrefs.SetFloat("HeadOffset_localPositionY", head.localPosition.y);
        PlayerPrefs.SetFloat("HeadOffset_localPositionZ", head.localPosition.z);
        PlayerPrefs.SetFloat("HeadOffset_localRotationX", head.localRotation.x);
        PlayerPrefs.SetFloat("HeadOffset_localRotationY", head.localRotation.y);
        PlayerPrefs.SetFloat("HeadOffset_localRotationZ", head.localRotation.z);
        PlayerPrefs.SetFloat("HeadOffset_localRotationW", head.localRotation.w);
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.RecallHeadTransform)) {
        Transform head = ViewpointScript.Head;
        // Toggle the head offset.
        if (m_HeadOffset) {
          // Remove the offset.
          Transform originalParent = head.parent;
          head.SetParent(head.parent.parent);
          GameObject.DestroyImmediate(originalParent.gameObject);
          m_HeadOffset = false;
        } else {
          // Add the offset.
          GameObject newParent = new GameObject();
          newParent.transform.SetParent(head.parent);
          newParent.transform.localPosition = Vector3.zero;
          newParent.transform.localRotation = Quaternion.identity;
          newParent.transform.localScale = Vector3.one;
          head.SetParent(newParent.transform);
          TrTransform offsetTransform = TrTransform.TR(
              new Vector3(
                  PlayerPrefs.GetFloat("HeadOffset_localPositionX", 0),
                  PlayerPrefs.GetFloat("HeadOffset_localPositionY", 1.5f),
                  PlayerPrefs.GetFloat("HeadOffset_localPositionZ", 0)),
              new Quaternion(
                  PlayerPrefs.GetFloat("HeadOffset_localRotationX", 0),
                  PlayerPrefs.GetFloat("HeadOffset_localRotationY", 0),
                  PlayerPrefs.GetFloat("HeadOffset_localRotationZ", 0),
                  PlayerPrefs.GetFloat("HeadOffset_localRotationW", 1)));
          TrTransform originalTransformInverse = TrTransform.FromLocalTransform(head).inverse;
          TrTransform newParentTransform = offsetTransform * originalTransformInverse;
          newParent.transform.localPosition = newParentTransform.translation;
          newParent.transform.localRotation = newParentTransform.rotation;
          m_HeadOffset = true;
        }
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.ToggleLightType)) {
        // Toggle between per-pixel & SH lighting on the secondary directional light
        Light secondaryLight = App.Scene.GetLight((1));
        if (LightRenderMode.ForceVertex == secondaryLight.renderMode) {
          secondaryLight.renderMode = LightRenderMode.ForcePixel;
        } else {
          secondaryLight.renderMode = LightRenderMode.ForceVertex;
        }
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.TossWidget)) {
        m_WidgetManager.TossNearestWidget();
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.Reset)) {
        App.Instance.SetDesiredState(App.AppState.LoadingBrushesAndLighting);
      } else if (App.Config.m_ToggleProfileOnAppButton &&
                 (InputManager.Wand.GetVrInputDown(VrInput.Button03) ||
                  InputManager.m_Instance.GetKeyboardShortcutDown(
                    InputManager.KeyboardShortcut.ToggleProfile))) {
        IssueGlobalCommand(GlobalCommands.ToggleProfiling);
      }
    }
#endif

#if DEBUG
    if (InputManager.m_Instance.GetKeyboardShortcutDown(
            InputManager.KeyboardShortcut.CheckStrokes)) {
      bool value = !SketchMemoryScript.m_Instance.m_SanityCheckStrokes;
      string feature = "Stroke determinism checking";
      SketchMemoryScript.m_Instance.m_SanityCheckStrokes = value;
      OutputWindowScript.m_Instance.CreateInfoCardAtController(
          InputManager.ControllerName.Brush,
          feature + (value ? ": On" : ": Off"));
    }
#endif

    bool hasController = m_ControlsType == ControlsType.SixDofControllers;

    // Toggle default tool.
    if (!m_PanelManager.AdvancedModeActive() &&
        InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.ToggleDefaultTool) &&
        !m_SketchSurfacePanel.IsDefaultToolEnabled() &&
        m_SketchSurfacePanel.ActiveTool.AllowDefaultToolToggle() &&
        // don't allow tool to change while pointing at panel because there is no visual indication
        m_CurrentGazeObject == -1) {
      m_SketchSurfacePanel.EnableDefaultTool();
      AudioManager.m_Instance.PlayPinCushionSound(true);
    }
    // Pan.
    else if (!hasController && Input.GetMouseButton(2)) {
      SwitchState(InputState.Pan);
    }
    // Controller lock (this must be before rotate/head lock!).
    else if (!hasController &&
            InputManager.m_Instance.GetCommand(InputManager.SketchCommands.LockToController)) {
      SwitchState(InputState.ControllerLock);
    }
    // Rotate.
    else if (!hasController &&
            InputManager.m_Instance.GetCommand(InputManager.SketchCommands.PivotRotation)) {
      SwitchState(InputState.Rotation);
    }
    // Head lock.
    else if (!hasController &&
            InputManager.m_Instance.GetCommand(InputManager.SketchCommands.LockToHead)) {
      SwitchState(InputState.HeadLock);
    }
    // Push pull.
    else if (!hasController &&
            InputManager.m_Instance.GetCommand(InputManager.SketchCommands.AltActivate)) {
      SwitchState(InputState.PushPull);
    }
    else if (!PointerManager.m_Instance.IsMainPointerCreatingStroke()) {
      // Reset surface.
      if (!hasController &&
          InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Reset)) {
        ResetGrabbedPose();
      }
      // Undo.
      else if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Undo) &&
          CanUndo()) {
        IssueGlobalCommand(GlobalCommands.Undo);
      }
      else if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Undo) &&
          CanUndo() && ShouldRepeatUndo()) {
        m_UndoHold_Timer = m_UndoRedoHold_RepeatInterval;
        IssueGlobalCommand(GlobalCommands.Undo);
      }
      // Redo.
      else if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Redo) &&
          CanRedo()) {
        IssueGlobalCommand(GlobalCommands.Redo);
      }
      else if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Redo) &&
          CanRedo() && ShouldRepeatRedo()) {
        m_RedoHold_Timer = m_UndoRedoHold_RepeatInterval;
        IssueGlobalCommand(GlobalCommands.Redo);
      }
      // Reset scene.
      else if (!hasController &&
          InputManager.m_Instance.GetKeyboardShortcutDown(
              InputManager.KeyboardShortcut.ResetScene)) {
        // TODO: Should thsi go away? Seems like the "sweetspot" may no longer be used.
        if (App.VrSdk.GetControllerDof() == VrSdk.DoF.Two) {
          m_PanelManager.SetSweetSpotPosition(m_CurrentGazeRay.origin);
          ResetGrabbedPose();
        }
      }
      // Straight edge.
      else if (!hasController &&
          InputManager.m_Instance.GetKeyboardShortcutDown(
              InputManager.KeyboardShortcut.StraightEdge)) {
        IssueGlobalCommand(GlobalCommands.StraightEdge);
      }
      // Always fall back on switching tools.
      else {
        m_SketchSurfacePanel.CheckForToolSelection();
      }
    }

    // Reset undo/redo hold timers.
    if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Undo)) {
      m_UndoHold_Timer = m_UndoRedoHold_DurationBeforeStart;
    }
    if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Redo)) {
      m_RedoHold_Timer = m_UndoRedoHold_DurationBeforeStart;
    }
    UnityEngine.Profiling.Profiler.EndSample();
  }

  bool CanUndo() {
    return SketchMemoryScript.m_Instance.CanUndo() &&
        !IsUIBlockingUndoRedo() &&
        m_PanelManager.GazePanelsAreVisible() &&
        !m_GrabWand.grabbingWorld &&
        !InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate) &&
        !SelectionManager.m_Instance.IsAnimatingTossFromGrabbingGroup;
  }

  bool CanRedo() {
    return SketchMemoryScript.m_Instance.CanRedo() &&
        !IsUIBlockingUndoRedo() &&
        m_PanelManager.GazePanelsAreVisible() &&
        !m_GrabBrush.grabbingWorld &&
        !InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate) &&
        !SelectionManager.m_Instance.IsAnimatingTossFromGrabbingGroup;
  }

  bool ShouldRepeatUndo() {
    m_UndoHold_Timer -= Time.deltaTime;
    return (m_UndoHold_Timer <= 0.0f);
  }

  bool ShouldRepeatRedo() {
    m_RedoHold_Timer -= Time.deltaTime;
    return (m_RedoHold_Timer <= 0.0f);
  }

  // Updates the global state:
  //   m_CurrentGrabWidget
  void UpdateGrab() {
    UnityEngine.Profiling.Profiler.BeginSample("SketchControlScript.UpdateGrab");
    if (m_ControlsType != ControlsType.SixDofControllers) {
      UnityEngine.Profiling.Profiler.EndSample();
      return;
    }

    GrabWidget rPrevGrabWidget = m_CurrentGrabWidget;
    GrabWidget rPrevPotentialBrush = m_PotentialGrabWidgetBrush;
    GrabWidget rPrevPotentialWand = m_PotentialGrabWidgetWand;
    if (m_CurrentGrabWidget) {
      m_CurrentGrabWidget.Activate(false);
    }
    if (m_PotentialGrabWidgetBrush) {
      m_PotentialGrabWidgetBrush.Activate(false);
    }
    if (m_PotentialGrabWidgetWand) {
      m_PotentialGrabWidgetWand.Activate(false);
    }
    m_CurrentGrabWidget = null;
    m_PotentialGrabWidgetBrush = null;
    m_PotentialGrabWidgetWand = null;
    m_PotentialGrabWidgetBrushValid = false;
    m_PotentialGrabWidgetWandValid = false;

    m_WidgetManager.RefreshNearestWidgetLists(m_CurrentGazeRay, m_CurrentGazeObject);

    if (m_GrabWidgetState == GrabWidgetState.None) {
      UpdateGrab_WasNone(rPrevPotentialBrush, rPrevPotentialWand);
    } else if (m_GrabWidgetState == GrabWidgetState.OneHand) {
      UpdateGrab_WasOneHand(rPrevGrabWidget);
    } else if (m_GrabWidgetState == GrabWidgetState.TwoHands) {
      UpdateGrab_WasTwoHands(rPrevGrabWidget);
    }

    // Update grab intersection state.
    switch (m_CurrentGrabIntersectionState) {
    case GrabIntersectionState.RequestIntersections:
      m_CurrentGrabIntersectionState = GrabIntersectionState.ReadBrush;
      break;
    case GrabIntersectionState.ReadBrush:
      m_CurrentGrabIntersectionState = GrabIntersectionState.ReadWand;
      break;
    case GrabIntersectionState.ReadWand:
      m_CurrentGrabIntersectionState = GrabIntersectionState.RequestIntersections;
      break;
    }

    if (!TutorialManager.m_Instance.TutorialActive() && m_CurrentGrabWidget == null) {
      UpdateGrab_World();
    }

    App.Instance.SelectionEffect.HighlightForGrab(
        m_GrabWidgetState != GrabWidgetState.None ||
        (m_PotentialGrabWidgetBrush != null && m_PotentialGrabWidgetBrushValid) ||
        (m_PotentialGrabWidgetWand != null && m_PotentialGrabWidgetWandValid));
    UnityEngine.Profiling.Profiler.EndSample();
  }

  void UpdateGrab_WasNone(GrabWidget rPrevPotentialBrush, GrabWidget rPrevPotentialWand) {
    // if a panel isn't in focus, allow for widget grab
    // We can grab a widget as long as we aren't trying to draw with that hand.
    bool bActiveInput =
      (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate) &&
       App.Instance.IsInStateThatAllowsPainting());

    //certain tools don't allow us to mess with widgets
    bool bWidgetManipOK = m_SketchSurfacePanel.DoesCurrentToolAllowWidgetManipulation() &&
      !m_GrabWand.grabbingWorld && !m_GrabBrush.grabbingWorld && IsGrabWorldStateStable() &&
      App.Instance.IsInStateThatAllowsAnyGrabbing();

    // Update EatInput flags if they're valid.
    if (m_GrabBrush.eatInput) {
      m_GrabBrush.eatInput = InputManager.Brush.GetControllerGrip();
    }
    if (m_GrabWand.eatInput) {
      m_GrabWand.eatInput = InputManager.Wand.GetControllerGrip();
    }

    bool bShouldClearWandInside = false;
    if (m_CurrentInputState == InputState.Standard && bWidgetManipOK) {
      // If we're in the intersection request state, fire off a new intersection request.  If
      // we're in the read brush state, update our brush grab data structure.
      List<GrabWidgetData> brushBests = m_WidgetManager.WidgetsNearBrush;
      if (m_CurrentGrabIntersectionState == GrabIntersectionState.RequestIntersections) {
        RequestWidgetIntersection(brushBests, InputManager.ControllerName.Brush);
      } else if (m_CurrentGrabIntersectionState == GrabIntersectionState.ReadBrush) {
        m_BackupBrushGrabData = GetBestWidget(brushBests, m_BrushResults);
      }

      if (m_BackupBrushGrabData != null) {
        m_PotentialGrabWidgetBrush = m_BackupBrushGrabData.m_WidgetScript;

        // Allow widget grab if we're not painting.
        if (!bActiveInput) {
          m_PotentialGrabWidgetBrush.Activate(true);
          m_PotentialGrabWidgetBrushValid = true;
          m_PotentialGrabWidgetBrush.VisualizePinState();

          if (!m_GrabBrush.eatInput && InputManager.Brush.GetControllerGrip()) {
            m_CurrentGrabWidget = m_PotentialGrabWidgetBrush;
            if (m_CurrentGrabWidget.Group != SketchGroupTag.None) {
              m_GrabBrush.grabbingGroup = true;
              m_CurrentGrabWidget =
                  SelectionManager.m_Instance.StartGrabbingGroupWithWidget(m_CurrentGrabWidget);
            }
            UpdateGrab_NoneToOne(InputManager.ControllerName.Brush);
            bShouldClearWandInside = true;
            m_GrabBrush.startedGrabInsideWidget = true;
          }
        }
      }
      m_GrabBrush.SetHadBestGrabAndTriggerHaptics(m_BackupBrushGrabData);
      m_ControllerGrabVisuals.BrushInWidgetRange = m_BackupBrushGrabData != null;

      // If we're in the intersection request state, fire off a new intersection request.  If
      // we're in the read wand state, update our wand grab data structure.
      List<GrabWidgetData> wandBests = m_WidgetManager.WidgetsNearWand;
      if (m_CurrentGrabIntersectionState == GrabIntersectionState.RequestIntersections) {
        RequestWidgetIntersection(wandBests, InputManager.ControllerName.Wand);
      } else if (m_CurrentGrabIntersectionState == GrabIntersectionState.ReadWand) {
        m_BackupWandGrabData = GetBestWidget(wandBests, m_WandResults);
      }

      if (m_BackupWandGrabData != null) {
        m_PotentialGrabWidgetWand = m_BackupWandGrabData.m_WidgetScript;
        // Allow wand widget grab if brush grab failed.
        bool bGrabAllowed = (m_GrabWidgetState == GrabWidgetState.None) && !bActiveInput;
        if (bGrabAllowed) {
          m_PotentialGrabWidgetWand.Activate(true);
          m_PotentialGrabWidgetWandValid = true;
          m_PotentialGrabWidgetWand.VisualizePinState();

          if (!m_GrabWand.eatInput && InputManager.Wand.GetControllerGrip()) {
            m_CurrentGrabWidget = m_PotentialGrabWidgetWand;
            if (m_CurrentGrabWidget.Group != SketchGroupTag.None) {
              m_GrabWand.grabbingGroup = true;
              m_CurrentGrabWidget =
                  SelectionManager.m_Instance.StartGrabbingGroupWithWidget(m_CurrentGrabWidget);
            }
            UpdateGrab_NoneToOne(InputManager.ControllerName.Wand);
            m_GrabBrush.ClearInsideWidget();
            m_GrabWand.startedGrabInsideWidget = true;
          }
        }
      }
      m_GrabWand.SetHadBestGrabAndTriggerHaptics(m_BackupWandGrabData);
      m_ControllerGrabVisuals.WandInWidgetRange = m_BackupWandGrabData != null;

      // Account for asymmetry in controller processing by clearing after wand has updated
      // GrabState.insideWidget according to bestWandGrab.
      if (bShouldClearWandInside) {
        m_GrabWand.ClearInsideWidget();
      }
    }

    // Update widget collisions if we've got a drifter.
    if (m_GrabWidgetState == GrabWidgetState.None) {
      if (m_WidgetManager.ShouldUpdateCollisions()) {
        m_PanelManager.DoCollisionSimulationForWidgetPanels();
      }
    }
  }

  void UpdateGrab_WasOneHand(GrabWidget rPrevGrabWidget) {
    var controller = InputManager.Controllers[(int)m_GrabWidgetOneHandInfo.m_Name];
    bool shouldRelease = !App.Instance.IsInStateThatAllowsAnyGrabbing();
    if (!InputManager.Controllers[(int)m_GrabWidgetOneHandInfo.m_Name].GetControllerGrip() ||
        shouldRelease) {
      if (shouldRelease) {
        EatGrabInput();
      }

      Vector3 vLinearVelocity;
      Vector3 vAngularVelocity;
      if (GetGrabWidgetHoldHistory(out vLinearVelocity, out vAngularVelocity)) {
        rPrevGrabWidget.SetVelocities(
            vLinearVelocity, vAngularVelocity,
            controller.Transform.position);
      }
      // One -> None
      UpdateGrab_ToNone(rPrevGrabWidget);
    } else {
      // Keep holding on to our widget.
      m_CurrentGrabWidget = rPrevGrabWidget;
      m_CurrentGrabWidget.Activate(true);
      m_CurrentGrabWidget.UserInteracting(true, m_GrabWidgetOneHandInfo.m_Name);

      if (!m_CurrentGrabWidget.Pinned) {
        var info = InputManager.Controllers[(int)m_GrabWidgetOneHandInfo.m_Name];
        var controllerXf = Coords.AsGlobal[info.Transform];
        var newWidgetXf = controllerXf * m_GrabWidgetOneHandInfo.m_BaseWidgetXf_LS;
        m_CurrentGrabWidget.RecordAndSetPosRot(newWidgetXf);

        UpdateGrabWidgetHoldHistory(m_GrabWidgetOneHandInfo.m_Name);
      }

      m_PanelManager.DoCollisionSimulationForWidgetPanels();

      // Check for widget pinning.
      if (m_CurrentGrabWidget.AllowPinning) {
        if (InputManager.Controllers[(int)m_GrabWidgetOneHandInfo.m_Name].GetCommandDown(
            InputManager.SketchCommands.PinWidget)) {
          // If the user initiates a pin action, buzz a bit.
          if (!m_CurrentGrabWidget.Pinned) {
            InputManager.m_Instance.TriggerHapticsPulse(
                m_GrabWidgetOneHandInfo.m_Name, 3, 0.10f, 0.07f);
          }
          m_CurrentGrabWidget.Pin(!m_CurrentGrabWidget.Pinned);
          SketchSurfacePanel.m_Instance.EatToolsInput();
          m_WidgetManager.RefreshPinAndUnpinLists();
        }
      }

      if (m_CurrentGrabWidget is SelectionWidget) {
        if (InputManager.m_Instance.GetCommandDown(
            InputManager.SketchCommands.DuplicateSelection)) {
          controller.LastHeldInput =
            controller.GetCommandHoldInput(InputManager.SketchCommands.DuplicateSelection);
        }

        if (controller.LastHeldInput != null &&
          InputManager.m_Instance.GetCommandHeld(InputManager.SketchCommands.DuplicateSelection)) {
          SketchControlsScript.m_Instance.IssueGlobalCommand(
            SketchControlsScript.GlobalCommands.Duplicate);
        }
      }

      InputManager.ControllerName otherName =
        (m_GrabWidgetOneHandInfo.m_Name == InputManager.ControllerName.Brush) ?
        InputManager.ControllerName.Wand : InputManager.ControllerName.Brush;
      bool otherInputEaten =
        (m_GrabWidgetOneHandInfo.m_Name == InputManager.ControllerName.Brush) ?
        m_GrabWand.eatInput : m_GrabBrush.eatInput;

      // See if the other controller decides to grab the widget (unless we're pinned).
      if (!m_CurrentGrabWidget.Pinned) {
        if (m_CurrentGrabWidget.AllowTwoHandGrab) {
          if (InputManager.Controllers[(int)otherName].GetControllerGrip()) {
            RequestPanelsVisibility(false);
            m_GrabWidgetState = GrabWidgetState.TwoHands;
            // Figure out if the new grab starts inside the widget.
            Vector3 vOtherGrabPos = TrTransform.FromTransform(
                InputManager.m_Instance.GetController(otherName)).translation;
            bool bOtherGrabInBounds = m_CurrentGrabWidget.GetActivationScore(
                vOtherGrabPos, otherName) >= 0;
            m_CurrentGrabWidget.SetUserTwoHandGrabbing(
                true, m_GrabWidgetOneHandInfo.m_Name, otherName, bOtherGrabInBounds);

            if (otherName == InputManager.ControllerName.Brush) {
              m_GrabBrush.startedGrabInsideWidget = bOtherGrabInBounds;
            } else {
              m_GrabWand.startedGrabInsideWidget = bOtherGrabInBounds;
            }

            m_GrabWidgetTwoHandBrushPrev = TrTransform.FromTransform(
                InputManager.m_Instance.GetController(InputManager.ControllerName.Brush));
            m_GrabWidgetTwoHandWandPrev = TrTransform.FromTransform(
                InputManager.m_Instance.GetController(InputManager.ControllerName.Wand));
          }
        }
      } else if (!otherInputEaten && InputManager.Controllers[(int)otherName].GetControllerGrip()) {
        // If it's a two hand grab but the current grab widget is pinned, grab the world.
        UpdateGrab_ToNone(m_CurrentGrabWidget);
        m_CurrentGrabWidget = null;
        m_ControllerGrabVisuals.SetDesiredVisualState(ControllerGrabVisuals.VisualState.Off);
      }
    }
  }

  // Previous frame was a two-handed grab.
  // Handles all the cases where this frame's grab is zero, one, or two hands.
  void UpdateGrab_WasTwoHands(GrabWidget rPrevGrabWidget) {
    //keep holding on to our widget
    m_CurrentGrabWidget = rPrevGrabWidget;
    m_CurrentGrabWidget.Activate(true);
    m_CurrentGrabWidget.UserInteracting(true, m_GrabWidgetOneHandInfo.m_Name);

    if (!App.Instance.IsInStateThatAllowsAnyGrabbing()) {
      m_CurrentGrabWidget.SetUserTwoHandGrabbing(false);
      UpdateGrab_ToNone(rPrevGrabWidget);
    } else if (!InputManager.Wand.GetControllerGrip()) { // Look for button release.
      m_CurrentGrabWidget.SetUserTwoHandGrabbing(false);
      // See if our Brush hand is still within grab range of the widget.
      if (m_GrabBrush.startedGrabInsideWidget ||
          IsControllerNearWidget(InputManager.ControllerName.Brush, m_CurrentGrabWidget)) {
        m_GrabWidgetOneHandInfo.m_Name = InputManager.ControllerName.Brush;
        RequestPanelsVisibility(true);
        InitializeGrabWidgetControllerInfo(m_GrabWidgetOneHandInfo);
        m_GrabWidgetState = GrabWidgetState.OneHand;
      } else {
        // If the Brush hand is beyond the widget, we're not holding it anymore.
        UpdateGrab_ToNone(rPrevGrabWidget);

        // Eat input on the brush grip until we release the button.
        m_GrabBrush.eatInput = true;
      }
    } else if (!InputManager.Brush.GetControllerGrip()) {
      m_CurrentGrabWidget.SetUserTwoHandGrabbing(false);
      if (m_GrabWand.startedGrabInsideWidget ||
          IsControllerNearWidget(InputManager.ControllerName.Wand, m_CurrentGrabWidget)) {
        m_GrabWidgetOneHandInfo.m_Name = InputManager.ControllerName.Wand;
        InitializeGrabWidgetControllerInfo(m_GrabWidgetOneHandInfo);
        m_GrabWidgetState = GrabWidgetState.OneHand;
      } else {
        UpdateGrab_ToNone(rPrevGrabWidget);
        m_GrabWand.eatInput = true;
      }
    } else {
      // Both hands still grabbing.
      // Check for pin, which forcibly releases one of the hands.
      if (m_CurrentGrabWidget.AllowPinning &&
          InputManager.Controllers[(int)m_GrabWidgetOneHandInfo.m_Name].GetCommandDown(
            InputManager.SketchCommands.PinWidget)) {
        // If the user initiates a pin action, buzz a bit.
        if (!m_CurrentGrabWidget.Pinned) {
          InputManager.m_Instance.TriggerHapticsPulse(
              m_GrabWidgetOneHandInfo.m_Name, 3, 0.10f, 0.07f);
        }

        m_CurrentGrabWidget.Pin(!m_CurrentGrabWidget.Pinned);
        SketchSurfacePanel.m_Instance.EatToolsInput();
        m_WidgetManager.RefreshPinAndUnpinLists();

        InitializeGrabWidgetControllerInfo(m_GrabWidgetOneHandInfo);
        m_GrabWidgetState = GrabWidgetState.OneHand;
        m_CurrentGrabWidget.SetUserTwoHandGrabbing(false);

        // Eat input on the off hand so we don't immediately jump in to world transform.
        if (m_GrabWidgetOneHandInfo.m_Name == InputManager.ControllerName.Brush) {
          RequestPanelsVisibility(true);
          m_GrabWand.eatInput = true;
        } else {
          m_GrabBrush.eatInput = true;
        }
      }

      if (!m_CurrentGrabWidget.Pinned) {
        UpdateGrab_ContinuesTwoHands();
      }
    }
    ClearGrabWidgetHoldHistory();
    m_PanelManager.DoCollisionSimulationForWidgetPanels();
  }

  // Common case for two-handed grab: both the previous and current frames are two-handed.
  private void UpdateGrab_ContinuesTwoHands() {
    //holding with two hands, transform accordingly
    TrTransform xfBrush = TrTransform.FromTransform(InputManager.Brush.Transform);
    TrTransform xfWand = TrTransform.FromTransform(InputManager.Wand.Transform);
    Vector2 vSizeRange = m_CurrentGrabWidget.GetWidgetSizeRange();

    GrabWidget.Axis axis = m_CurrentGrabWidget.GetScaleAxis(
        xfWand.translation, xfBrush.translation,
        out Vector3 axisDirection, out float axisExtent);

    TrTransform newWidgetXf;
    if (axis != GrabWidget.Axis.Invalid) {
      // Scale along a single axis
      float deltaScale;
      if (App.Config.m_AxisManipulationIsResize) {
        newWidgetXf = MathUtils.TwoPointObjectTransformationAxisResize(
            axisDirection, axisExtent,
            m_GrabWidgetTwoHandWandPrev, m_GrabWidgetTwoHandBrushPrev,
            xfWand, xfBrush,
            GetWorkingTransform(m_CurrentGrabWidget),
            out deltaScale,
            deltaScaleMin: vSizeRange.x / axisExtent,
            deltaScaleMax: vSizeRange.y / axisExtent);
      } else {
        newWidgetXf = MathUtils.TwoPointObjectTransformationNonUniformScale(
            axisDirection,
            m_GrabWidgetTwoHandWandPrev, m_GrabWidgetTwoHandBrushPrev,
            xfWand, xfBrush,
            GetWorkingTransform(m_CurrentGrabWidget),
            out deltaScale,
            finalScaleMin: vSizeRange.x,
            deltaScaleMin: vSizeRange.x / axisExtent,
            deltaScaleMax: vSizeRange.y / axisExtent);
      }

      // The above functions return undefined values in newWidgetXf.scale; but that's
      // okay because RecordAndSetPosRot ignores xf.scale.
      // TODO: do this more cleanly
      m_CurrentGrabWidget.RecordAndApplyScaleToAxis(deltaScale, axis);
    } else {
      // Uniform scaling
      TrTransform xfObject = GetWorkingTransform(m_CurrentGrabWidget);
      Vector3 extents = (m_CurrentGrabWidget is StencilWidget)
        ? (m_CurrentGrabWidget as StencilWidget).Extents
        : Vector3.one * Mathf.Abs(m_CurrentGrabWidget.GetSignedWidgetSize());

      // Delta-scale bounds should be based on the smallest/largest extent.
      // Irritatingly, the API wants absolute rather than relative scale bounds,
      // so they need even more conversion.
      float deltaScaleMin = vSizeRange.x / extents.Min();
      float deltaScaleMax = vSizeRange.y / extents.Max();
      if (m_GrabWand.startedGrabInsideWidget && m_GrabBrush.startedGrabInsideWidget) {
        newWidgetXf = MathUtils.TwoPointObjectTransformation(
            m_GrabWidgetTwoHandWandPrev, m_GrabWidgetTwoHandBrushPrev,
            xfWand, xfBrush,
            xfObject,
            deltaScaleMin: deltaScaleMin, deltaScaleMax: deltaScaleMax);
      } else if (m_GrabWand.startedGrabInsideWidget) {
        // keep the wand inside the object
        newWidgetXf = MathUtils.TwoPointObjectTransformation(
            m_GrabWidgetTwoHandWandPrev, m_GrabWidgetTwoHandBrushPrev,
            xfWand, xfBrush,
            xfObject,
            deltaScaleMin: deltaScaleMin, deltaScaleMax: deltaScaleMax,
            bUseLeftAsPivot: true);
      } else {
        // keep the brush inside the object (note the brush is the left hand)
        newWidgetXf = MathUtils.TwoPointObjectTransformation(
            m_GrabWidgetTwoHandBrushPrev, m_GrabWidgetTwoHandWandPrev,
            xfBrush, xfWand,
            xfObject,
            deltaScaleMin: deltaScaleMin, deltaScaleMax: deltaScaleMax,
            bUseLeftAsPivot: true);
      }

      // Must do separately becvause RecordAndSetPosRot ignores newWidgetXf.scale
      m_CurrentGrabWidget.RecordAndSetSize(newWidgetXf.scale);

      float currentSize = Mathf.Abs(m_CurrentGrabWidget.GetSignedWidgetSize());
      if (currentSize == vSizeRange.x || currentSize == vSizeRange.y) {
        InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.05f);
        InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Wand, 0.05f);
      }
    }

    // Ignores TrTransform.scale
    m_CurrentGrabWidget.RecordAndSetPosRot(newWidgetXf);

    m_GrabWidgetTwoHandBrushPrev = xfBrush;
    m_GrabWidgetTwoHandWandPrev = xfWand;
  }

  void UpdateGrab_NoneToOne(InputManager.ControllerName controllerName) {
    if (m_MaybeDriftingGrabWidget != null &&
        m_MaybeDriftingGrabWidget.IsMoving() &&
       !m_MaybeDriftingGrabWidget.IsSpinningFreely) {
      // If a new widget is grabbed but the previous one is still drifting, end the drift.
      // TODO: Simplify in the widget animation cleanup.
      if (m_MaybeDriftingGrabWidget == m_CurrentGrabWidget) {
        SketchMemoryScript.m_Instance.PerformAndRecordCommand(
         new MoveWidgetCommand(m_MaybeDriftingGrabWidget,
           m_MaybeDriftingGrabWidget.LocalTransform, m_MaybeDriftingGrabWidget.CustomDimension,
           final: true),
         discardIfNotMerged: true);
      }
      m_MaybeDriftingGrabWidget.ClearVelocities();
    }

    // UserInteracting should be the first thing that happens here so OnUserBeginInteracting can
    // be called before everything else.
    m_CurrentGrabWidget.UserInteracting(true, controllerName);
    m_CurrentGrabWidget.ClearVelocities();
    ClearGrabWidgetHoldHistory();

    //set our info names according to this controller's name
    m_GrabWidgetOneHandInfo.m_Name = controllerName;
    InitializeGrabWidgetControllerInfo(m_GrabWidgetOneHandInfo);

    PointerManager.m_Instance.AllowPointerPreviewLine(false);
    PointerManager.m_Instance.RequestPointerRendering(false);
    m_SketchSurfacePanel.RequestHideActiveTool(true);
    if (m_GrabWidgetOneHandInfo.m_Name == InputManager.ControllerName.Wand) {
      RequestPanelsVisibility(false);
    }

    // Notify visuals.
    ControllerGrabVisuals.VisualState visualState =
        m_GrabWidgetOneHandInfo.m_Name == InputManager.ControllerName.Brush ?
        ControllerGrabVisuals.VisualState.WidgetBrushGrip :
        ControllerGrabVisuals.VisualState.WidgetWandGrip;
    m_ControllerGrabVisuals.SetDesiredVisualState(visualState);
    m_ControllerGrabVisuals.SetHeldWidget(m_CurrentGrabWidget.transform);

    //if a gaze object had focus when we grabbed this widget, take focus off the object
    ResetActivePanel();
    m_UIReticle.SetActive(false);

    // Prep all other grab widgets for collision.
    m_PanelManager.PrimeCollisionSimForWidgets(m_CurrentGrabWidget);

    m_GrabWidgetState = GrabWidgetState.OneHand;
    m_WidgetManager.WidgetsDormant = false;
    PointerManager.m_Instance.EatLineEnabledInput();

    m_BackupWandGrabData = null;
    m_BackupBrushGrabData = null;
  }

  void UpdateGrab_ToNone(GrabWidget rPrevGrabWidget) {
    m_MaybeDriftingGrabWidget = rPrevGrabWidget;

    m_GrabWidgetState = GrabWidgetState.None;
    PointerManager.m_Instance.RequestPointerRendering(!App.Instance.IsLoading() &&
                                                      m_SketchSurfacePanel.ShouldShowPointer());
    RequestPanelsVisibility(true);
    m_SketchSurfacePanel.RequestHideActiveTool(false);
    rPrevGrabWidget.UserInteracting(false);

    // Disable grab visuals.
    m_ControllerGrabVisuals.SetDesiredVisualState(ControllerGrabVisuals.VisualState.Off);
    m_ControllerGrabVisuals.SetHeldWidget(null);

    if (m_GrabBrush.grabbingGroup || m_GrabWand.grabbingGroup) {
      SelectionManager.m_Instance.EndGrabbingGroupWithWidget();
      m_GrabBrush.grabbingGroup = false;
      m_GrabWand.grabbingGroup = false;
    }
  }

  void RequestWidgetIntersection(List<GrabWidgetData> candidates,
      InputManager.ControllerName controllerName) {
    // Get locals based off what controller we're using.
    Queue<GpuIntersectionResult> resultQueue = null;
    Vector3 controllerPos = Vector3.zero;
    if (controllerName == InputManager.ControllerName.Brush) {
      resultQueue = m_BrushResults;
      controllerPos = InputManager.m_Instance.GetBrushControllerAttachPoint().position;
    } else {
      resultQueue = m_WandResults;
      controllerPos = InputManager.m_Instance.GetWandControllerAttachPoint().position;
    }

    // If we don't have a candidate that has a GPU object, don't bother firing off a GPU request.
    bool requestGpuIntersection = false;

    // Fire off a new GPU intersection with all widgets that can use it.
    for (int i = 0; i < candidates.Count; ++i) {
      if (candidates[i].m_WidgetScript.HasGPUIntersectionObject()) {
        candidates[i].m_WidgetScript.SetGPUIntersectionObjectLayer(m_WidgetGpuIntersectionLayer);
        requestGpuIntersection = true;
      }
    }

    if (requestGpuIntersection) {
      GpuIntersectionResult newRequest = new GpuIntersectionResult();
      newRequest.resultList = new List<GpuIntersector.ModelResult>();
      newRequest.result = App.Instance.GpuIntersector.RequestModelIntersections(
          controllerPos, m_WidgetGpuIntersectionRadius, newRequest.resultList, 8,
          (1 << m_WidgetGpuIntersectionLayer));

      // The new result will only be null when the intersector is disabled.
      if (newRequest.result != null) {
        resultQueue.Enqueue(newRequest);
      }

      for (int i = 0; i < candidates.Count; ++i) {
        if (candidates[i].m_WidgetScript.HasGPUIntersectionObject()) {
          candidates[i].m_WidgetScript.RestoreGPUIntersectionObjectLayer();
        }
      }
    }
  }

  GrabWidgetData GetBestWidget(List<GrabWidgetData> candidates,
      Queue<GpuIntersectionResult> resultQueue) {
    // Discard futures that are too old.
    while (resultQueue.Count > 0) {
      if (Time.frameCount - resultQueue.Peek().result.StartFrame < 5) {
        break;
      }
      resultQueue.Dequeue();
    }

    // If the oldest future is ready, use its intersection result to update the candidates.
    GpuIntersectionResult finishedResult;
    if (resultQueue.Count > 0 && resultQueue.Peek().result.IsReady) {
      finishedResult = resultQueue.Dequeue();
    } else {
      finishedResult.resultList = new List<GpuIntersector.ModelResult>();
    }

    // TODO: Speed this up.
    for (int i = 0; i < candidates.Count; ++i) {
      if (candidates[i].m_WidgetScript.HasGPUIntersectionObject()) {
        // If a candidate can't find itself in the finished results list, it's not eligible.
        bool candidateValid = false;
        for (int j = 0; j < finishedResult.resultList.Count; ++j) {
          if (candidates[i].m_WidgetScript.Equals(finishedResult.resultList[j].widget)) {
            candidateValid = true;
            break;
          }
        }

        if (candidateValid) {
          // If a candidate has a GPU intersection object and we found it in this list,
          // not only is it valid, but it's as valid as it can be.
          candidates[i].m_ControllerScore = 1.0f;
        } else {
          candidates[i].m_NearController = false;
        }
      }
    }

    // Run through the candidates and pick
    GrabWidgetData best = null;
    for (int i = 0; i < candidates.Count; ++i) {
      if (candidates[i].m_NearController &&
          (best == null || candidates[i].m_ControllerScore > best.m_ControllerScore)) {
        best = candidates[i];
      }
    }
    return best;
  }

  void InitializeGrabWidgetControllerInfo(GrabWidgetControllerInfo info) {
    Transform controller = InputManager.Controllers[(int)info.m_Name].Transform;
    Transform widget = m_CurrentGrabWidget.GrabTransform_GS;
    TrTransform newWidgetXf = Coords.AsGlobal[widget];

    info.m_BaseControllerXf = Coords.AsGlobal[controller];
    info.m_BaseWidgetXf_LS = info.m_BaseControllerXf.inverse * newWidgetXf;
  }

  // returns the transform of the true widget (not the snapped one for those that can be)
  private TrTransform GetWorkingTransform(GrabWidget w) {
    TrTransform ret = w.GetGrabbedTrTransform();
    ret.scale = w.GetSignedWidgetSize();
    return ret;
  }

  // Initiate the world transform reset animation.
  public void RequestWorldTransformReset(bool toSavedXf = false) {
    if (WorldIsReset(toSavedXf)) {
      return;
    }

    m_WorldTransformResetXf =
      toSavedXf ? SketchMemoryScript.m_Instance.InitialSketchTransform : TrTransform.identity;
    m_WorldTransformResetState = WorldTransformResetState.Requested;
  }

  void UpdateWorldTransformReset() {
    switch (m_WorldTransformResetState) {
    case WorldTransformResetState.Requested:
      ViewpointScript.m_Instance.FadeToColor(Color.black, m_GrabWorldFadeSpeed);
      m_WorldTransformResetState = WorldTransformResetState.FadingToBlack;
      m_xfDropCamReset_RS = Coords.AsRoom[m_DropCam.transform];
      PointerManager.m_Instance.EatLineEnabledInput();
      PointerManager.m_Instance.AllowPointerPreviewLine(false);
      break;
    case WorldTransformResetState.FadingToBlack:
      m_WorldTransformFadeAmount += m_GrabWorldFadeSpeed * Time.deltaTime;
      if (m_WorldTransformFadeAmount >= 1.0f) {
        App.Scene.Pose = m_WorldTransformResetXf;
        m_WorldTransformFadeAmount = 1.0f;
        m_WorldTransformResetState = WorldTransformResetState.FadingToScene;
        ViewpointScript.m_Instance.FadeToScene(m_GrabWorldFadeSpeed);
        m_DropCam.transform.position = m_xfDropCamReset_RS.translation;
        m_DropCam.transform.rotation = m_xfDropCamReset_RS.rotation;
        PointerManager.m_Instance.AllowPointerPreviewLine(true);
      }
      break;
    case WorldTransformResetState.FadingToScene:
      m_WorldTransformFadeAmount -= m_GrabWorldFadeSpeed * Time.deltaTime;
      if (m_WorldTransformFadeAmount <= 0.0f) {
        m_WorldTransformFadeAmount = 0.0f;
        m_WorldTransformResetState = WorldTransformResetState.Default;
      }
      break;
    }
  }

  void UpdateGrab_World() {
    bool bAllowWorldTransform = m_SketchSurfacePanel.ActiveTool.AllowWorldTransformation() &&
        (m_GrabWorldState != GrabWorldState.ResetDone) &&
        (!PointerManager.m_Instance.IsMainPointerCreatingStroke() || App.Instance.IsLoading()) &&
        App.Instance.IsInStateThatAllowsAnyGrabbing();

    bool bWorldGrabWandPrev = m_GrabWand.grabbingWorld;
    bool bWorldGrabBrushPrev = m_GrabBrush.grabbingWorld;
    m_GrabWand.grabbingWorld = bAllowWorldTransform && !m_GrabWand.eatInput &&
        InputManager.Wand.GetControllerGrip();
    m_GrabBrush.grabbingWorld = bAllowWorldTransform && !m_GrabBrush.eatInput &&
        InputManager.Brush.GetControllerGrip() &&
        (m_CurrentGazeObject == -1);

    bool grabsChanged = (bWorldGrabWandPrev != m_GrabWand.grabbingWorld) ||
        (bWorldGrabBrushPrev != m_GrabBrush.grabbingWorld);
    bool bAllowWorldTransformChanged =
        bAllowWorldTransform != m_AllowWorldTransformLastFrame;
    int nGrabs = m_GrabWand.grabbingWorld ? 1 : 0;
    nGrabs += m_GrabBrush.grabbingWorld ? 1 : 0;

    // Allow grabbing again if grabs have changed and we're done resetting.
    if (m_GrabWorldState == GrabWorldState.ResetDone && grabsChanged) {
      m_GrabWorldState = GrabWorldState.Normal;
    }

    // Update panels visibility if brush grip has changed.
    if (bWorldGrabWandPrev != m_GrabWand.grabbingWorld) {
      RequestPanelsVisibility(!m_GrabWand.grabbingWorld);
    }

    // Update tool visibility if brush grip has changed.
    if (bWorldGrabBrushPrev != m_GrabBrush.grabbingWorld) {
      m_SketchSurfacePanel.RequestHideActiveTool(m_GrabBrush.grabbingWorld);
      PointerManager.m_Instance.AllowPointerPreviewLine(!m_GrabBrush.grabbingWorld);
      PointerManager.m_Instance.RequestPointerRendering(!m_GrabBrush.grabbingWorld
          && m_SketchSurfacePanel.ShouldShowPointer() && !App.Instance.IsLoading());
    }

    // Reset m_WorldBeingGrabbed and only set it when world is actually being grabbed.
    bool bWorldBeingGrabbedPrev = m_WorldBeingGrabbed;
    m_WorldBeingGrabbed = false;

    // Move the world if it has been grabbed.
    if (m_GrabWorldState == GrabWorldState.Normal && bAllowWorldTransform) {
      if (nGrabs == 2) {
        // Two-handed world movement.
        m_WorldBeingGrabbed = true;
        TrTransform grabXfWand = TrTransform.FromTransform(
            InputManager.m_Instance.GetController(InputManager.ControllerName.Wand));
        TrTransform grabXfBrush = TrTransform.FromTransform(
            InputManager.m_Instance.GetController(InputManager.ControllerName.Brush));

        // Offset the controller positions so that they're centered on the grips.
        Vector3 gripPos = InputManager.Controllers[(int)InputManager.ControllerName.Brush].
            Geometry.GripAttachPoint.localPosition;
        gripPos.x = 0.0f;
        grabXfWand.translation += grabXfWand.MultiplyVector(gripPos);
        grabXfBrush.translation += grabXfBrush.MultiplyVector(gripPos);

        // Are we initiating two hand transform this frame?
        if (!bWorldGrabWandPrev || !bWorldGrabBrushPrev) {
          PointerManager.m_Instance.EnableLine(false);
          PointerManager.m_Instance.AllowPointerPreviewLine(false);
          PointerManager.m_Instance.RequestPointerRendering(false);
          // Initiate audio loop
          m_WorldTransformSpeedSmoothed = 0.0f;
          AudioManager.m_Instance.WorldGrabLoop(true);
        } else {
          TrTransform xfOld = GrabbedPose;
          TrTransform xfNew;
          float deltaScaleMin = WorldTransformMinScale / xfOld.scale;
          float deltaScaleMax = WorldTransformMaxScale / xfOld.scale;
          // Constrain the transform depending on the mode.
          if (InGrabCanvasMode) {
            xfNew = MathUtils.TwoPointObjectTransformation(
                m_GrabBrush.grabTransform, m_GrabWand.grabTransform,
                grabXfBrush, grabXfWand,
                xfOld,
                deltaScaleMin: deltaScaleMin, deltaScaleMax: deltaScaleMax);
          } else {
            xfNew = MathUtils.TwoPointObjectTransformation(
                m_GrabBrush.grabTransform, m_GrabWand.grabTransform,
                grabXfBrush, grabXfWand,
                xfOld,
                rotationAxisConstraint:Vector3.up,
                deltaScaleMin: deltaScaleMin, deltaScaleMax: deltaScaleMax);
            float fCurrentWorldTransformSpeed =
                Mathf.Abs((xfNew.scale - xfOld.scale) / Time.deltaTime);
            m_WorldTransformSpeedSmoothed =
                Mathf.Lerp(m_WorldTransformSpeedSmoothed, fCurrentWorldTransformSpeed,
                AudioManager.m_Instance.m_WorldGrabLoopSmoothSpeed * Time.deltaTime);
            AudioManager.m_Instance.ChangeLoopVolume("WorldGrab",
                Mathf.Clamp(m_WorldTransformSpeedSmoothed /
                AudioManager.m_Instance.m_WorldGrabLoopAttenuation, 0f,
                AudioManager.m_Instance.m_WorldGrabLoopMaxVolume));
          }
          GrabbedPose = xfNew;
        }

        // Update last states.
        m_GrabBrush.grabTransform = grabXfBrush;
        m_GrabWand.grabTransform = grabXfWand;
      }
    } else if (m_GrabWorldState == GrabWorldState.ResettingTransform) {
      if (m_WorldTransformResetState == WorldTransformResetState.FadingToScene) {
        ResetGrabbedPose();
        PanelManager.m_Instance.ExecuteOnPanel<LightsPanel>(x => x.OnPanelMoved());

        // World can't be transformed right after a reset until grab states have changed.
        if (bAllowWorldTransform) {
          bAllowWorldTransform = false;
          bAllowWorldTransformChanged =
            bAllowWorldTransform != m_AllowWorldTransformLastFrame;
        }

        // Set the grab world state on exit.
        if (nGrabs == 0) {
          m_GrabWorldState = GrabWorldState.Normal;
        } else {
          m_GrabWorldState = GrabWorldState.ResetDone;
        }
      }
    }

    if (grabsChanged || bAllowWorldTransformChanged) {
      // Fade in grid when doing two handed spin.
      if (!InGrabCanvasMode) {
        if (nGrabs == 2 && !bAllowWorldTransformChanged) {
          ViewpointScript.m_Instance.FadeGroundPlaneIn(m_GrabWorldGridColor, m_GrabWorldFadeSpeed);
        } else {
          ViewpointScript.m_Instance.FadeGroundPlaneOut(m_GrabWorldFadeSpeed);
        }
      }
    }

    // Update visuals for world transform
    if (grabsChanged) {
      bool bDoubleGrip = m_GrabBrush.grabbingWorld && m_GrabWand.grabbingWorld;
      bool bSingleGrip = m_GrabBrush.grabbingWorld || m_GrabWand.grabbingWorld;
      Vector3 vControllersMidpoint =
          (InputManager.m_Instance.GetControllerPosition(InputManager.ControllerName.Brush) +
           InputManager.m_Instance.GetControllerPosition(InputManager.ControllerName.Wand)) * 0.5f;

      // Update transform line visuals
      if (bDoubleGrip) {
        m_ControllerGrabVisuals.SetDesiredVisualState(ControllerGrabVisuals.VisualState.WorldDoubleGrip);
        AudioManager.m_Instance.WorldGrabbed(vControllersMidpoint);
      } else if (bSingleGrip) {
        if (m_GrabWand.grabbingWorld) {
          m_ControllerGrabVisuals.SetDesiredVisualState(ControllerGrabVisuals.VisualState.WorldWandGrip);
        } else {
          m_ControllerGrabVisuals.SetDesiredVisualState(ControllerGrabVisuals.VisualState.WorldBrushGrip);
        }

        if (!bWorldGrabWandPrev && !bWorldGrabBrushPrev) {
          AudioManager.m_Instance.WorldGrabbed(vControllersMidpoint);
        } else {
          AudioManager.m_Instance.WorldGrabLoop(false);
        }
      } else {
        m_ControllerGrabVisuals.SetDesiredVisualState(ControllerGrabVisuals.VisualState.Off);
        AudioManager.m_Instance.WorldGrabLoop(false);
      }

      if (m_GrabWand.grabbingWorld || m_GrabBrush.grabbingWorld) {
        m_WidgetManager.WidgetsDormant = false;
        PointerManager.m_Instance.EatLineEnabledInput();
      }
    }

    // Reset scene transform if we're gripping and press the track pad.
    bool wandReset = m_GrabWand.grabbingWorld &&
        InputManager.Wand.GetCommandDown(InputManager.SketchCommands.WorldTransformReset);
    bool brushReset = m_GrabBrush.grabbingWorld &&
        InputManager.Brush.GetCommandDown(InputManager.SketchCommands.WorldTransformReset);
    if ((wandReset || brushReset) && !WorldIsReset(toSavedXf: false)) {
      m_GrabBrush.eatInput = true;
      m_GrabWand.eatInput = true;
      m_EatToolScaleInput = true;
      m_GrabWorldState = GrabWorldState.ResettingTransform;
      RequestWorldTransformReset();
      AudioManager.m_Instance.PlayTransformResetSound();
    }

    // Update the skybox rotation with the new scene rotation.
    if (RenderSettings.skybox) {
      Quaternion sceneQuaternion = App.Instance.m_SceneTransform.rotation;
      RenderSettings.skybox.SetVector(
          "_SkyboxRotation",
          new Vector4(sceneQuaternion.x, sceneQuaternion.y, sceneQuaternion.z, sceneQuaternion.w));
    }

    // Update last frame members.
    m_AllowWorldTransformLastFrame = bAllowWorldTransform;
  }

  /// If lhs and rhs are overlapping, return the smallest vector that would
  /// cause rhs to stop overlapping; otherwise, return 0.
  ///  lhs: an antisphere (solid outside, empty inside)
  ///  rhs: a sphere (empty outside, solid inside)
  private static Vector3 GetOverlap_Antisphere_Sphere(
      Vector3 lhsCenter, float lhsRadius,
      Vector3 rhsCenter, float rhsRadius) {
    // If anyone passes negative values, they are a bad person
    lhsRadius = Mathf.Abs(lhsRadius);
    rhsRadius = Mathf.Abs(rhsRadius);
    // Without loss of generality, can recenter on lhs
    rhsCenter -= lhsCenter;
    lhsCenter -= lhsCenter;

    float maxDistance = lhsRadius - rhsRadius;

    // Edge case: sphere does not fit in antisphere
    if (maxDistance <= 0) {
      return -rhsCenter;
    }

    float penetrationDistance = Mathf.Max(0, rhsCenter.magnitude - maxDistance);
    return -penetrationDistance * rhsCenter.normalized;
  }

  public static bool IsValidScenePose(TrTransform xf, float radialBounds) {
    // Simple and dumb implementation for now.
    return xf == MakeValidScenePose(xf, radialBounds);
  }

  /// This is like MakeValidScenePose, but it guarantees that:
  /// - The return value is a valid result of Lerp(scene0, scene1, t),
  ///   for some handwavy definition of "lerp"
  /// - The lerp "t" is in [0, 1]
  /// - IsValidScenePose(return value) is true, subject to the previous constraints.
  ///
  /// Think of it as doing a cast from scene0 to scene1.
  public static TrTransform MakeValidSceneMove(
      TrTransform scene0, TrTransform scene1, float radialBounds) {
    if (IsValidScenePose(scene1, radialBounds)) {
      return scene1;
    }
    if (!IsValidScenePose(scene0, radialBounds)) {
      Debug.LogError("Invalid scene cast start");
      return scene0;
    }

    // We don't support lerping either of these
    Debug.Assert(scene0.rotation == scene1.rotation);
    Debug.Assert(scene0.scale == scene1.scale);

    Vector3 vRoom0 = -scene0.translation;
    Vector3 vRoom1 = -scene1.translation;
    float radius = (scene0.scale
                    * radialBounds
                    * App.METERS_TO_UNITS) - App.Instance.RoomRadius;

    float t0, t1;
    bool success = MathUtils.RaySphereIntersection(
        vRoom0, vRoom1 - vRoom0,
        Vector3.zero, radius, out t0, out t1);
    if (!success) {
      // If this were more important, we could solve for the t of the closest approach
      return scene0;
    }

    // t0 is expected to be < 0 (room starts inside the fence)
    // t1 is expected to be in [0, 1] (room ends outside the fence)

    // Constraints:
    // - Lerp t must be in [0, 1]. (Do not move past the requested endpoint)
    // - Lerp t should be as high as possible but < t1. (Do not exit the sphere)
    float t = Mathf.Clamp(t1, 0, 1);

    TrTransform sceneT = TrTransform.TRS(
        Vector3.Lerp(scene0.translation, scene1.translation, t),
        scene0.rotation,
        scene0.scale);
    return MakeValidScenePose(sceneT, radialBounds);
  }

  /// Returns a new ScenePose TrTransform that does not cause the room
  /// to violate the hard scene bounds.
  ///
  ///   scenePose - The current, possibly invalid scene pose
  public static TrTransform MakeValidScenePose(TrTransform scenePose, float radialBounds) {
    scenePose.scale = Mathf.Clamp(
        scenePose.scale,
        SketchControlsScript.m_Instance.WorldTransformMinScale,
        SketchControlsScript.m_Instance.WorldTransformMaxScale);

    // Anything not explicitly qualified is in room space.

    float roomRadius = App.Instance.RoomRadius;
    Vector3 roomCenter = Vector3.zero;

    float fenceRadius = scenePose.scale * radialBounds
        * App.METERS_TO_UNITS;
    Vector3 fenceCenter = scenePose.translation;

    Vector3 moveRoom = GetOverlap_Antisphere_Sphere(
        fenceCenter, fenceRadius, roomCenter, roomRadius);
    Vector3 moveFence = -moveRoom;

    scenePose.translation += moveFence;
    return scenePose;
  }

  /// Clears data used by GetGrabWidgetHoldHistory()
  /// Should be called any time m_GrabWidgetOneHandInfo changes
  void ClearGrabWidgetHoldHistory() {
    m_GrabWidgetHoldHistory.Clear();
  }

  /// Collects data for use with GetGrabWidgetHoldHistory()
  void UpdateGrabWidgetHoldHistory(InputManager.ControllerName name) {
    float t = Time.realtimeSinceStartup;
    var info = InputManager.Controllers[(int)name];
    m_GrabWidgetHoldHistory.Enqueue(new GrabWidgetHoldPoint {
        m_Name = name,
        m_BirthTime = t,
        m_Pos = info.Transform.position,
        m_Rot = info.Transform.rotation
      });

    // Trim the fat off our widget history
    while (m_GrabWidgetHoldHistory.Count > 0 &&
           t - m_GrabWidgetHoldHistory.Peek().m_BirthTime >= kControlPointHistoryMaxTime) {
      m_GrabWidgetHoldHistory.Dequeue();
    }
  }

  /// Returns possibly-smoothed linear and angular velocities. May fail.
  /// Angular velocity is returned as an axial vector whose length() is degrees/second
  bool GetGrabWidgetHoldHistory(out Vector3 vLinearVelocity, out Vector3 vAngularVelocity) {
    vLinearVelocity = vAngularVelocity = Vector3.zero;
    if (m_GrabWidgetHoldHistory.Count < 2) {
      return false;
    }

    // We need pairs of elements, so a simple foreach() won't quite work.
    // Maybe using linq .First() and .Skip() would be okay.
    using (IEnumerator<GrabWidgetHoldPoint> enumerator = m_GrabWidgetHoldHistory.GetEnumerator()) {
      if (!enumerator.MoveNext()) {
        return false;
      }

      // Infinitesimal rotations commute, and scaled-axis-angle rotations commute
      // "better" than other rotation formats.
      Vector3 totalDeltaTheta = Vector3.zero;

      GrabWidgetHoldPoint first = enumerator.Current;
      GrabWidgetHoldPoint prev = first;
      GrabWidgetHoldPoint current = first;
      while (enumerator.MoveNext()) {
        current = enumerator.Current;

        // For our quaternion, find the difference, convert it to angle/axis, and sum it
        // Find delta such that  delta * prev = cur
        // left-multiply because we want it in world-space.
        // multiply vs prev since we want the delta that takes us forward in time
        // rather than backward in time.
        Quaternion dtheta = current.m_Rot * Quaternion.Inverse(prev.m_Rot);
        // Assume the rotation took the shorter path
        if (dtheta.w < 0) {
          dtheta.Set(-dtheta.x, -dtheta.y, -dtheta.z, -dtheta.w);
        }

        float degrees;
        Vector3 axis;
        dtheta.ToAngleAxis(out degrees, out axis);
        totalDeltaTheta += (axis * degrees);
        prev = current;
      }

      // Linear velocity calculation doesn't need to look at intermediate points
      Vector3 totalDeltaPosition = current.m_Pos - first.m_Pos;
      float totalDeltaTime = current.m_BirthTime - first.m_BirthTime;
      if (totalDeltaTime == 0) {
        return false;
      }

      vLinearVelocity = totalDeltaPosition / totalDeltaTime;
      vAngularVelocity = totalDeltaTheta / totalDeltaTime;
      return true;
    }
  }

  bool IsControllerNearWidget(InputManager.ControllerName name, GrabWidget widget) {
    Vector3 vControllerPos = InputManager.m_Instance.GetControllerAttachPointPosition(name);
    return widget.GetActivationScore(vControllerPos, name) >= 0.0f;
  }

  void RefreshCurrentGazeObject() {
    UnityEngine.Profiling.Profiler.BeginSample("SketchControlScript.RefreshCurrentGazeObject");
    int iPrevGazeObject = m_CurrentGazeObject;
    m_CurrentGazeObject = -1;
    bool bGazeAllowed = (m_CurrentInputState == InputState.Standard)
        && !InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate)
        && !m_SketchSurfacePanel.ActiveTool.InputBlocked()
        && (m_GrabWidgetState == GrabWidgetState.None)
        && !m_GrabBrush.grabbingWorld
        && !m_PinCushion.IsShowing();

    bool bGazeDeactivationOverrideWithInput = false;
    List<PanelManager.PanelData> aAllPanels = m_PanelManager.GetAllPanels();

    bool hasController = m_ControlsType == ControlsType.SixDofControllers;

    //if we're re-positioning a panel, keep it active
    if (m_PositioningPanelWithHead) {
      m_CurrentGazeObject = iPrevGazeObject;
    }
    // Only activate gaze objects if we're in standard input mode, and if we don't have the 'draw'
    // button held.
    else if ((bGazeAllowed || (iPrevGazeObject != -1))) {
      //reset hit flags
      for (int i = 0; i < m_GazeResults.Length; ++i) {
        m_GazeResults[i].m_HitWithGaze = false;
        m_GazeResults[i].m_HitWithController = false;
        m_GazeResults[i].m_WithinView = false;
      }

      // If we're in controller mode, find the nearest colliding widget that might get in our way.
      float fNearestWidget = 99999.0f;
      if (hasController) {
        fNearestWidget = m_WidgetManager.DistanceToNearestWidget(m_GazeControllerRay);
      }

      //check all panels for gaze hit
      bool bRequireVisibilityCheck = !hasController || (iPrevGazeObject == -1);
      if (m_PanelManager.PanelsAreStable()) {
        RaycastHit rHitInfo;
        bool bRayHit = false;
        int panelsHit = 0;
        for (int i = 0; i < aAllPanels.Count; ++i) {
          // Ignore fixed panels when they are not visible.
          if (!m_PanelManager.GazePanelsAreVisible() && aAllPanels[i].m_Panel.m_Fixed) {
            continue;
          }

          if (aAllPanels[i].m_Panel.gameObject.activeSelf && aAllPanels[i].m_Panel.IsAvailable()) {
            //make sure this b-snap is in view
            Vector3 vToPanel = aAllPanels[i].m_Panel.transform.position - m_CurrentGazeRay.origin;
            vToPanel.Normalize();
            if (!bRequireVisibilityCheck || Vector3.Angle(vToPanel, m_CurrentGazeRay.direction) < m_GazeMaxAngleFromFacing) {
              if (hasController) {
                if (aAllPanels[i].m_Panel.HasMeshCollider()) {
                  //make sure the angle between the pointer and the panel forward is below our max angle
                  if (Vector3.Angle(aAllPanels[i].m_Panel.transform.forward, m_GazeControllerRay.direction) < m_GazeMaxAngleFromPointing) {
                    //make sure the angle between the user-to-panel and the panel forward is reasonable
                    if (Vector3.Angle(aAllPanels[i].m_Panel.transform.forward, vToPanel) < m_GazeMaxAngleFacingToForward) {
                      m_GazeResults[i].m_WithinView = true;

                      bRayHit = false;
                      bRayHit = aAllPanels[i].m_Panel.RaycastAgainstMeshCollider(
                        m_GazeControllerRay, out rHitInfo, m_GazeControllerPointingDistance);

                      if (bRayHit) {
                        //if the ray starts inside the panel, we won't get a good hit point, it'll just be zero
                        if (rHitInfo.point.sqrMagnitude > 0.1f) {
                          if (rHitInfo.distance < fNearestWidget) {
                            m_GazeResults[i].m_ControllerDistance = rHitInfo.distance;
                            m_GazeResults[i].m_ControllerPosition = rHitInfo.point;
                            m_GazeResults[i].m_HitWithController = true;
                            panelsHit++;
                          }
                        }
                      }
                    }
                  }
                }
              } else {
                m_GazeResults[i].m_WithinView = true;
                if (aAllPanels[i].m_Panel.GetCollider().Raycast(m_CurrentGazeRay, out rHitInfo, m_GazeMaxDistance)) {
                  m_GazeResults[i].m_GazePosition = rHitInfo.point;
                  m_GazeResults[i].m_HitWithGaze = true;
                }
              }
            }
          }
        }

        // No panels hit within normal ray distance.
        // Check if previous panel still pointed to.
        if (panelsHit == 0) {
          if (iPrevGazeObject != -1) {
            // Don't allow any panel to hold focus if it's facing away from the user.
            Vector3 vToPanel = aAllPanels[iPrevGazeObject].m_Panel.transform.position -
                m_CurrentGazeRay.origin;
            vToPanel.Normalize();
            if (Vector3.Angle(aAllPanels[iPrevGazeObject].m_Panel.transform.forward, vToPanel) <
                m_GazeMaxAngleFacingToForward) {
              float fDist = m_GazeControllerPointingDistance * 1.5f;
              bRayHit = aAllPanels[iPrevGazeObject].m_Panel.RaycastAgainstMeshCollider(
                  m_GazeControllerRayActivePanel, out rHitInfo, fDist);
              if (bRayHit) {
                if (rHitInfo.point.sqrMagnitude > 0.1f) {
                  if (rHitInfo.distance < fNearestWidget) {
                    m_GazeResults[iPrevGazeObject].m_ControllerDistance = rHitInfo.distance;
                    m_GazeResults[iPrevGazeObject].m_ControllerPosition = rHitInfo.point;
                    m_GazeResults[iPrevGazeObject].m_HitWithController = true;
                  }
                }
              }
            }
          }
        }
      }

      //determine what panel we hit, take the one with the lowest controller distance
      float fControllerDist = 999.0f;
      int iControllerIndex = -1;
      if (hasController) {
        for (int i = 0; i < m_GazeResults.Length; ++i) {
          if (m_GazeResults[i].m_HitWithController) {
            if (m_GazeResults[i].m_ControllerDistance < fControllerDist) {
              iControllerIndex = i;
              fControllerDist = m_GazeResults[i].m_ControllerDistance;
            }
          }
        }
      }

       //if we found something near our controller, take it
      if (iControllerIndex != -1) {
        m_CurrentGazeObject = iControllerIndex;
        m_CurrentGazeHitPoint = m_GazeResults[iControllerIndex].m_ControllerPosition;

        // TODO: This should not be hardcoded once multiple pointers are allowed.
        m_GazeResults[m_CurrentGazeObject].m_ControllerName = InputManager.ControllerName.Brush;
        if (m_GazeResults[m_CurrentGazeObject].m_HitWithGaze) {
          //average with the gaze position if we hit that too
          m_CurrentGazeHitPoint += m_GazeResults[m_CurrentGazeObject].m_GazePosition;
          m_CurrentGazeHitPoint *= 0.5f;
        }
      } else {
        //nothing near the controller, see if we're looking at the previous
        if (iPrevGazeObject != -1 && m_GazeResults[iPrevGazeObject].m_HitWithGaze) {
          m_CurrentGazeObject = iPrevGazeObject;
          m_CurrentGazeHitPoint = m_GazeResults[m_CurrentGazeObject].m_GazePosition;
        } else {
          //controller and gaze not near panel, pick the first panel we're looking at
          for (int i = 0; i < m_GazeResults.Length; ++i) {
            if (m_GazeResults[i].m_HitWithGaze) {
              m_CurrentGazeObject = i;
              m_CurrentGazeHitPoint = m_GazeResults[i].m_GazePosition;
              break;
            }
          }
        }
      }

      //forcing users to look away from gaze panel
      if (m_EatInputGazeObject && m_CurrentGazeObject != -1) {
        m_CurrentGazeObject = -1;
      } else if (m_CurrentGazeObject == -1) {
        m_EatInputGazeObject = false;
      }
    }

    //if we're staring at a panel, keep our countdown fresh
    if (m_CurrentGazeObject != -1 || m_ForcePanelActivation) {
      m_GazePanelDectivationCountdown = m_GazePanelDectivationDelay;
    } else {
      if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate)) {
        bGazeDeactivationOverrideWithInput = true;
        m_GazePanelDectivationCountdown = 0.0f;
      } else {
        m_GazePanelDectivationCountdown -= Time.deltaTime;
      }
      if (m_GazePanelDectivationCountdown > 0.0f) {
        m_CurrentGazeObject = iPrevGazeObject;
      }
    }

    //update our positioning timer
    if (m_PositioningPanelWithHead) {
      m_PositioningTimer += m_PositioningSpeed * Time.deltaTime;
      m_PositioningTimer = Mathf.Min(m_PositioningTimer, 1.0f);
    } else {
      m_PositioningTimer -= m_PositioningSpeed * Time.deltaTime;
      m_PositioningTimer = Mathf.Max(m_PositioningTimer, 0.0f);
    }

    //prime objects if we change targets
    if (iPrevGazeObject != m_CurrentGazeObject) {
      //if we're switching panels, make sure the pointer doesn't streak
      PointerManager.m_Instance.DisablePointerPreviewLine();

      if (iPrevGazeObject != -1) {
        aAllPanels[iPrevGazeObject].m_Panel.PanelGazeActive(false);
        aAllPanels[iPrevGazeObject].m_Panel.SetPositioningPercent(0.0f);
      }
      if (m_CurrentGazeObject != -1) {
        //make sure our line is disabled
        if (m_GazeResults[m_CurrentGazeObject].m_ControllerName == InputManager.ControllerName.Brush) {
          PointerManager.m_Instance.EnableLine(false);
          PointerManager.m_Instance.AllowPointerPreviewLine(false);
        }

        aAllPanels[m_CurrentGazeObject].m_Panel.PanelGazeActive(true);
        aAllPanels[m_CurrentGazeObject].m_Panel.SetPositioningPercent(0.0f);

        if (m_GazeResults[m_CurrentGazeObject].m_ControllerName == InputManager.ControllerName.Brush) {
          m_SketchSurfacePanel.RequestHideActiveTool(true);
        }
      } else {
        //if we don't have a panel, we need to enable the pointer according to the current tool
        PointerManager.m_Instance.RefreshFreePaintPointerAngle();
        PointerManager.m_Instance.RequestPointerRendering(m_SketchSurfacePanel.ShouldShowPointer());
        m_UIReticle.SetActive(false);
        m_SketchSurfacePanel.RequestHideActiveTool(false);
        if (!bGazeDeactivationOverrideWithInput) {
          m_SketchSurfacePanel.EatToolsInput();
        }
      }

      m_PositioningPanelWithHead = false;
    }
    UnityEngine.Profiling.Profiler.EndSample();
  }

  void UpdateActiveGazeObject() {
    BasePanel currentPanel = m_PanelManager.GetPanel(m_CurrentGazeObject);
    currentPanel.SetPositioningPercent(m_PositioningTimer);
    bool hasController = m_ControlsType == ControlsType.SixDofControllers;
    // Update positioning behavior.
    if (m_PositioningPanelWithHead) {
      if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.LockToHead) &&
          !InputManager.m_Instance.GetCommand(InputManager.SketchCommands.LockToController)) {
        // No more positioning.
        m_PositioningPanelWithHead = false;
        m_PanelManager.m_SweetSpot.EnableBorderSphere(false, Vector3.zero, 0.0f);
        currentPanel.PanelHasStoppedMoving();
      } else {
        //lock the panel to the sweet spot bounds in the direction the user is looking
        Quaternion qDiff = m_CurrentHeadOrientation * Quaternion.Inverse(m_PositioningPanelBaseHeadRotation);
        Vector3 vAdjustedOffset = qDiff * m_PositioningPanelOffset;

        Vector3 vNewPos = m_PanelManager.m_SweetSpot.transform.position + vAdjustedOffset;
        currentPanel.transform.position = vNewPos;

        vAdjustedOffset.Normalize();
        currentPanel.transform.forward = vAdjustedOffset;

        float fHighlightRadius = currentPanel.m_BorderSphereHighlightRadius;
        m_PanelManager.m_SweetSpot.EnableBorderSphere(true, vNewPos, fHighlightRadius * m_PositioningTimer);

        //once we've moved this panel, run the simulation on the other panels to resolve collisions
        m_PanelManager.DoCollisionSimulationForKeyboardMouse(currentPanel);
      }
    } else {
      // It's possible that, on this frame, before this function was called, active gaze was pulled
      // from this panel.  In this case, we want to skip updating this frame.
      // This happens when a panel has gaze and world grab dismisses all panels, for example.
      if (currentPanel.IsActive()) {
        //orient to gaze
        if (hasController) {
          currentPanel.UpdatePanel(m_GazeControllerRay.direction, m_CurrentGazeHitPoint);
        } else {
          currentPanel.UpdatePanel(m_CurrentGazeRay.direction, m_CurrentGazeHitPoint);
        }
      }

      if (!hasController) {
        //lock to head if we're holding a lock button..
        bool bLockToHead = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.LockToHead) ||
            InputManager.m_Instance.GetCommand(InputManager.SketchCommands.LockToController);

        if (bLockToHead) {
          m_PositioningPanelWithHead = true;
          m_PositioningPanelBaseHeadRotation = m_CurrentHeadOrientation;
          m_PositioningPanelOffset = currentPanel.transform.position -
              m_PanelManager.m_SweetSpot.transform.position;

          currentPanel.ResetPanelFlair();

          //prime all other panels for movement
          m_PanelManager.PrimeCollisionSimForKeyboardMouse();
        }
      }

      PointerManager.m_Instance.RequestPointerRendering(false);
      currentPanel.UpdateReticleOffset(m_MouseDeltaX, m_MouseDeltaY);
    }

    // Keep reticle locked in the right spot.
    Vector3 reticlePos = Vector3.zero;
    Vector3 reticleForward = Vector3.zero;
    if (hasController) {
      currentPanel.GetReticleTransformFromPosDir(m_CurrentGazeHitPoint,
          m_GazeControllerRay.direction, out reticlePos, out reticleForward);
    } else {
      currentPanel.GetReticleTransform(out reticlePos, out reticleForward,
          (m_ControlsType == ControlsType.ViewingOnly));
    }

    SetUIReticleTransform(reticlePos, -reticleForward);
    m_UIReticle.SetActive(GetGazePanelActivationRatio() >= 1.0f);
  }

  public void ResetActivePanel() {
    m_PanelManager.ResetPanel(m_CurrentGazeObject);
    PointerManager.m_Instance.DisablePointerPreviewLine();
    m_PositioningPanelWithHead = false;
    m_CurrentGazeObject = -1;
  }

  void UpdatePanInput() {
    if (Input.GetMouseButton(2)) {
      Vector3 vPanDiff = Vector3.zero;
      vPanDiff += (Vector3.right * m_MouseDeltaXScaled);
      vPanDiff += (Vector3.up * m_MouseDeltaYScaled);
      Vector3 vSurfacePos = m_SketchSurface.transform.position;
      m_SketchSurface.transform.position = vSurfacePos + vPanDiff;
    } else {
      float fCurrentTime = Time.realtimeSinceStartup;
      if (fCurrentTime - m_PositionOffsetResetTapTime < m_DoubleTapWindow) {
        if (m_CurrentGazeObject == -1) {
          ResetGrabbedPose();
        }
      }
      m_PositionOffsetResetTapTime = fCurrentTime;

      SwitchState(InputState.Standard);
    }
  }

  void UpdateRotationInput() {
    if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.PivotRotation)) {
      bool bAltInputActive = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.AltActivate);
      bool bRollRotation = m_RotationRollActive || bAltInputActive || m_CurrentRotationType == RotationType.RollOnly;
      m_RotationIcon.SetActive(bRollRotation);
      if (bRollRotation) {
        m_RotationCursorOffset.x += m_MouseDeltaXScaled;
        float fRotationAmount = m_RotationCursorOffset.x * -m_RotationRollScalar;

        Quaternion qOffsetRotation = Quaternion.AngleAxis(fRotationAmount, m_SurfaceForward);
        Quaternion qNewRotation = qOffsetRotation * m_RotationOrigin;
        m_SketchSurface.transform.rotation = qNewRotation;

        m_RotationRollActive = true;
        m_RotationCursor.gameObject.SetActive(false);
      } else {
        //update offset with mouse movement
        m_RotationCursorOffset.x += m_MouseDeltaXScaled;
        m_RotationCursorOffset.y += m_MouseDeltaYScaled;

        //get offset in model space
        Vector3 vSurfaceBounds = m_SketchSurface.transform.localScale * 0.5f;
        m_RotationCursorOffset.x = Mathf.Clamp(m_RotationCursorOffset.x, -vSurfaceBounds.x, vSurfaceBounds.x);
        m_RotationCursorOffset.y = Mathf.Clamp(m_RotationCursorOffset.y, -vSurfaceBounds.y, vSurfaceBounds.y);
        float fCursorOffsetDist = m_RotationCursorOffset.magnitude;
        float fMaxCursorOffsetDist = vSurfaceBounds.x;

        //transform offset in to world space
        Vector3 vTransformedOffset = m_RotationOrigin * m_RotationCursorOffset;
        vTransformedOffset.Normalize();

        //get world space rotation axis
        Vector3 vSketchSurfaceRotationAxis = Vector3.Cross(vTransformedOffset, m_SurfaceForward);
        vSketchSurfaceRotationAxis.Normalize();

        //amount to rotate is determined by offset distance from origin
        float fSketchSurfaceRotationAngle = Mathf.Min(fCursorOffsetDist / fMaxCursorOffsetDist, 1.0f);
        fSketchSurfaceRotationAngle *= m_RotationMaxAngle;

        //set new surface rotation by combining base rotation with angle/axis rotation
        Quaternion qOffsetRotation = Quaternion.AngleAxis(fSketchSurfaceRotationAngle, vSketchSurfaceRotationAxis);
        Quaternion qNewRotation = qOffsetRotation * m_RotationOrigin;
        m_SketchSurface.transform.rotation = qNewRotation;

        //set position of rotation cursor
        Vector3 vNewTransformedOffset = qNewRotation * m_RotationCursorOffset;
        m_RotationCursor.transform.position = m_SketchSurface.transform.position + vNewTransformedOffset;
        m_RotationCursor.transform.rotation = qNewRotation;

        //set position of guide lines
        Vector2 vToCenter = m_RotationCursorOffset;
        vToCenter.Normalize();
        float fOffsetAngle = Vector2.Angle(vToCenter, Vector2.up);
        m_RotationCursor.PositionCursorLines(m_SketchSurface.transform.position, m_SketchSurface.transform.forward, fOffsetAngle, vSurfaceBounds.x * 2.0f);
      }
    } else {
      float fCurrentTime = Time.realtimeSinceStartup;
      if (fCurrentTime - m_RotationResetTapTime < m_DoubleTapWindow) {
        //reset drawing surface rotation
        m_SketchSurface.transform.rotation = Quaternion.identity;
      }
      m_RotationResetTapTime = fCurrentTime;

      m_SurfaceForward = m_SketchSurface.transform.forward;
      m_SurfaceRight = m_SketchSurface.transform.right;
      m_SurfaceUp = m_SketchSurface.transform.up;

      if (!m_RotationRollActive && m_AutoOrientAfterRotation && m_SketchSurfacePanel.IsSketchSurfaceToolActive()) {
        //get possible auto rotations
        Quaternion qQuatUp = OrientSketchSurfaceToUp();
        Quaternion qQuatForward = OrientSketchSurfaceToForward();

        //get the angle between our current and desired auto-rotation
        float toUpAngle = Quaternion.Angle(qQuatUp, m_SketchSurface.transform.rotation);
        float toForwardAngle = Quaternion.Angle(qQuatForward, m_SketchSurface.transform.rotation);

        //set our new rotation to be whichever autorotation is closeset
        Quaternion qNewRotation;
        if (Mathf.Abs(toUpAngle) < Mathf.Abs(toForwardAngle)) {
          qNewRotation = qQuatUp;
        } else {
          qNewRotation = qQuatForward;
        }

        //update the sketch surface
        m_SketchSurface.transform.rotation = qNewRotation;

        m_SurfaceForward = m_SketchSurface.transform.forward;
        m_SurfaceRight = m_SketchSurface.transform.right;
        m_SurfaceUp = m_SketchSurface.transform.up;
      }

      SwitchState(InputState.Standard);
    }
  }

  void UpdateHeadLockInput() {
    if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.LockToHead)) {
      //compute new position/orientation of sketch surface
      Vector3 vTransformedOffset = m_CurrentHeadOrientation * m_SurfaceLockOffset;
      Vector3 vSurfacePos = m_CurrentGazeRay.origin + vTransformedOffset;

      Quaternion qDiff = m_CurrentHeadOrientation * Quaternion.Inverse(m_SurfaceLockBaseHeadRotation);
      Quaternion qNewSurfaceRot = qDiff * m_SurfaceLockBaseSurfaceRotation;

      m_SketchSurface.transform.position = vSurfacePos;
      m_SketchSurface.transform.rotation = qNewSurfaceRot;
    } else {
      m_SurfaceForward = m_SketchSurface.transform.forward;
      m_SurfaceRight = m_SketchSurface.transform.right;
      m_SurfaceUp = m_SketchSurface.transform.up;

      SwitchState(InputState.Standard);
    }
  }

  void UpdateControllerLock() {
    if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.LockToController)) {
      //compute new position/orientation of sketch surface
      Vector3 vControllerDiff = InputManager.m_Instance.GetControllerPosition(m_SurfaceLockActingController) - m_SurfaceLockBaseControllerPosition;
      m_SketchSurface.transform.position = m_SurfaceLockBaseSurfacePosition + (vControllerDiff * m_SurfaceLockControllerScalar);

      Quaternion qDiff = InputManager.m_Instance.GetControllerRotation(m_SurfaceLockActingController) * Quaternion.Inverse(m_SurfaceLockBaseControllerRotation);
      m_SketchSurface.transform.rotation = qDiff * m_SurfaceLockBaseSurfaceRotation;
    } else {
      m_SurfaceForward = m_SketchSurface.transform.forward;
      m_SurfaceRight = m_SketchSurface.transform.right;
      m_SurfaceUp = m_SketchSurface.transform.up;

      SwitchState(InputState.Standard);
    }
  }

  void UpdatePushPullInput() {
    bool bRotationActive = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.PivotRotation);
    bool bInputActive = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate);
    bool bAltInputActive = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.AltActivate);

    if (bRotationActive && bInputActive) {
      SwitchState(InputState.Rotation);
    } else if (bAltInputActive) {
      Vector3 vPos = m_SketchSurface.transform.position;
      float fBigDiff = Mathf.Abs(m_MouseDeltaXScaled) > Mathf.Abs(m_MouseDeltaYScaled) ? -m_MouseDeltaXScaled : m_MouseDeltaYScaled;
      vPos += Vector3.forward * fBigDiff;

      m_SketchSurface.transform.position = vPos;
    } else {
      SwitchState(InputState.Standard);
    }
  }

  void UpdateSaveInput() {
    if (!InputManager.m_Instance.GetKeyboardShortcut(InputManager.KeyboardShortcut.Save)) {
      SwitchState(InputState.Standard);
    }
  }

  void UpdateLoadInput() {
    if (!InputManager.m_Instance.GetKeyboardShortcut(InputManager.KeyboardShortcut.Load)) {
      SwitchState(InputState.Standard);
    }
  }

  void OnBrushSetToDefault() {
    BrushDescriptor rDefaultBrush = BrushCatalog.m_Instance.DefaultBrush;
    PointerManager.m_Instance.SetBrushForAllPointers(rDefaultBrush);
    PointerManager.m_Instance.SetAllPointersBrushSize01(0.5f);
    PointerManager.m_Instance.MarkAllBrushSizeUsed();
  }

  public void AssignControllerMaterials(InputManager.ControllerName controller) {
    ControllerGeometry geometry = InputManager.GetControllerGeometry(controller);

    // Start from a clean state
    geometry.ResetAll();

    // If the tutorial is enabled, override all materials.
    if (TutorialManager.m_Instance.TutorialActive()) {
      InputManager.m_Instance
                  .GetControllerTutorial(controller)
                  ?.AssignControllerMaterials(controller);
      return;
    }

    // If we're grabbing the world, get the materials from the world transform panel.
    if (m_GrabBrush.grabbingWorld && controller == InputManager.ControllerName.Brush) {
      TrTransform scenePose = App.Scene.Pose;
      if (scenePose.scale != 1 || scenePose.translation != Vector3.zero
                               || scenePose.rotation != Quaternion.identity) {
        geometry.ShowWorldTransformReset();
      }
      return;
    } else if (m_GrabWand.grabbingWorld && controller == InputManager.ControllerName.Wand) {
      TrTransform scenePose = App.Scene.Pose;
      if (scenePose.scale != 1 || scenePose.translation != Vector3.zero
                               || scenePose.rotation != Quaternion.identity) {
        geometry.ShowWorldTransformReset();
      }
      return;
    }

    // Not grabbing the world, so see if we're grabbing a widget.
    if (m_GrabWidgetState != GrabWidgetState.None) {
      m_CurrentGrabWidget.AssignControllerMaterials(controller);
      return;
    }

    // See if we're highlighting a widget and if that matters.
    if (m_CurrentGrabWidget != null && m_CurrentGrabWidget.HasHoverInteractions()) {
      m_CurrentGrabWidget.AssignHoverControllerMaterials(controller);
      return;
    }

    // Not grabbing the world or a widget, see if we're interacting with a panel.
    if (controller == InputManager.ControllerName.Brush && m_CurrentGazeObject != -1) {
      BasePanel panel = m_PanelManager.GetPanel(m_CurrentGazeObject);
      panel.AssignControllerMaterials(controller);
      return;
    }

    // Defaults.
    if (controller == InputManager.ControllerName.Wand) {
      if (App.CurrentState != App.AppState.Standard || m_PanelManager.IntroSketchbookMode) {
        // If app is not in standard mode, the actions represented by subsequent material
        // assigments cannot be taken.
        return;
      }
      bool creatingStroke = PointerManager.m_Instance.IsMainPointerCreatingStroke();
      bool allowPainting = App.Instance.IsInStateThatAllowsPainting();

      InputManager.Wand.Geometry.ShowRotatePanels();
      InputManager.Wand.Geometry.ShowUndoRedo(CanUndo() && !creatingStroke && allowPainting,
                                              CanRedo() && !creatingStroke && allowPainting);
    }

    // Show the pin cushion icon on the button if it's available.
    if (controller == InputManager.ControllerName.Brush && CanUsePinCushion()) {
      InputManager.Brush.Geometry.ShowPinCushion();
    }

    // Finally, override with tools.
    m_SketchSurfacePanel.AssignControllerMaterials(controller);
  }

  public float GetControllerPadShaderRatio(
      InputManager.ControllerName controller, VrInput input) {
    // If we're interacting with a panel, get touch ratio from the panel.
    if (controller == InputManager.ControllerName.Brush && m_CurrentGazeObject != -1) {
      BasePanel panel = m_PanelManager.GetPanel(m_CurrentGazeObject);
      return panel.GetControllerPadShaderRatio(controller);
    }
    return SketchSurfacePanel.m_Instance.GetCurrentToolSizeRatio(controller, input);
  }

  void SwitchState(InputState rDesiredState) {
    //exit current state
    switch (m_CurrentInputState) {
    case InputState.Pan:
      m_TransformGizmoScript.ResetTransform();
      break;
    case InputState.PushPull:
      m_TransformGizmoScript.ResetTransform();
      break;
    case InputState.Rotation:
      m_RotationRollActive = false;
      m_RotationIcon.SetActive(false);
      m_RotationCursor.gameObject.SetActive(false);
      break;
    }

    bool bSketchSurfaceToolActive = m_SketchSurfacePanel.IsSketchSurfaceToolActive();

    //enter new state
    switch (rDesiredState) {
    case InputState.Pan:
      m_TransformGizmoScript.SetTransformForPan();
      break;
    case InputState.PushPull:
      m_TransformGizmoScript.SetTransformForPushPull();
      break;
    case InputState.Rotation:
      if (bSketchSurfaceToolActive) {
        m_SketchSurface.transform.position = PointerManager.m_Instance.MainPointer.transform.position;
        m_SketchSurfacePanel.ResetReticleOffset();
      }
      m_RotationOrigin = m_SketchSurface.transform.rotation;
      m_RotationCursorOffset = Vector2.zero;
      m_RotationCursor.transform.position = m_SketchSurface.transform.position;
      m_RotationCursor.transform.rotation = m_SketchSurface.transform.rotation;
      m_RotationCursor.ClearCursorLines(m_SketchSurface.transform.position);
      m_RotationCursor.gameObject.SetActive(bSketchSurfaceToolActive);
      break;
    case InputState.HeadLock:
      m_SurfaceLockBaseHeadRotation = m_CurrentHeadOrientation;
      m_SurfaceLockBaseSurfaceRotation = m_SketchSurface.transform.rotation;
      m_SurfaceLockOffset = m_SketchSurface.transform.position - m_CurrentGazeRay.origin;
      m_SurfaceLockOffset = Quaternion.Inverse(m_SurfaceLockBaseHeadRotation) * m_SurfaceLockOffset;
      break;
    case InputState.ControllerLock:
      if (bSketchSurfaceToolActive) {
        m_SketchSurface.transform.position = PointerManager.m_Instance.MainPointer.transform.position;
        m_SketchSurfacePanel.ResetReticleOffset();
      }
      m_SurfaceLockActingController = InputManager.m_Instance.GetDominantController(InputManager.SketchCommands.LockToController);
      m_SurfaceLockBaseSurfaceRotation = m_SketchSurface.transform.rotation;
      m_SurfaceLockBaseControllerRotation = InputManager.m_Instance.GetControllerRotation(m_SurfaceLockActingController);
      m_SurfaceLockBaseSurfacePosition = m_SketchSurface.transform.position;
      m_SurfaceLockBaseControllerPosition = InputManager.m_Instance.GetControllerPosition(m_SurfaceLockActingController);
      m_SurfaceLockControllerScalar = m_SketchSurfacePanel.m_PanelSensitivity / m_SurfaceLockControllerBaseScalar;
      break;
    case InputState.Save:
      IssueGlobalCommand(GlobalCommands.Save);
      break;
    case InputState.Load:
      IssueGlobalCommand(GlobalCommands.Load);
      break;
    }

    m_CurrentInputState = rDesiredState;
  }

  public void RequestPanelsVisibility(bool bVisible) {
    m_PanelsVisibilityRequested = bVisible;
  }

  Quaternion OrientSketchSurfaceToUp() {
    //project the world up vector on to the surface plane
    Vector3 vUpOnSurfacePlane = Vector3.up - (Vector3.Dot(Vector3.up, m_SurfaceForward) * m_SurfaceForward);
    vUpOnSurfacePlane.Normalize();

    //get the angle between the surface up and the projected world up
    float fUpOnSurfacePlaneAngle = Vector3.Angle(vUpOnSurfacePlane, m_SurfaceUp);
    Vector3 vUpCross = Vector3.Cross(vUpOnSurfacePlane, m_SurfaceUp);
    vUpCross.Normalize();
    if (Vector3.Dot(vUpCross, m_SurfaceForward) > 0.0f) {
      fUpOnSurfacePlaneAngle *= -1.0f;
    }

    //rotate around the surface foward by the angle diff
    Quaternion qOrientToUp = Quaternion.AngleAxis(fUpOnSurfacePlaneAngle, m_SurfaceForward);
    Quaternion qNewRotation = qOrientToUp * m_SketchSurface.transform.rotation;
    return qNewRotation;
  }

  Quaternion OrientSketchSurfaceToForward() {
    //project the world forward vector on to the surface plane
    Vector3 vForwardOnSurfacePlane = Vector3.forward - (Vector3.Dot(Vector3.forward, m_SurfaceForward) * m_SurfaceForward);
    vForwardOnSurfacePlane.Normalize();

    //get the angle between the surface up and the projected world forward
    float fForwardOnSurfacePlaneAngle = Vector3.Angle(vForwardOnSurfacePlane, m_SurfaceUp);
    Vector3 vUpCross = Vector3.Cross(vForwardOnSurfacePlane, m_SurfaceUp);
    vUpCross.Normalize();
    if (Vector3.Dot(vUpCross, m_SurfaceForward) > 0.0f) {
      fForwardOnSurfacePlaneAngle *= -1.0f;
    }

    //rotate around the surface foward by the angle diff
    Quaternion qOrientToForward = Quaternion.AngleAxis(fForwardOnSurfacePlaneAngle, m_SurfaceForward);
    Quaternion qNewRotation = qOrientToForward * m_SketchSurface.transform.rotation;
    return qNewRotation;
  }

  /// Reset the scene or the canvas, depending on the current mode
  void ResetGrabbedPose(bool everything=false) {
    //update sketch surface position with offset to sweet spot
    m_SketchSurface.transform.position = m_PanelManager.GetSketchSurfaceResetPos();
    if (everything) {
      App.Scene.Pose = TrTransform.identity;
      Coords.CanvasLocalPose = TrTransform.identity;
    } if (InGrabCanvasMode) {
      Coords.CanvasLocalPose = TrTransform.identity;
    } else {
      App.Scene.Pose = TrTransform.identity;
    }

    //reset orientation and pointer
    ResetSketchSurfaceOrientation();
    m_SketchSurfacePanel.ResetReticleOffset();
    PointerManager.m_Instance.DisablePointerPreviewLine();
    PointerManager.m_Instance.SetPointerPreviewLineDelayTimer();
  }

  public void ResetSketchSurfaceOrientation() {
    m_SketchSurface.transform.rotation = Quaternion.identity;
    m_SurfaceForward = m_SketchSurface.transform.forward;
    m_SurfaceRight = m_SketchSurface.transform.right;
    m_SurfaceUp = m_SketchSurface.transform.up;
  }

  float GetAppropriateMovementScalar() {
    switch (m_CurrentInputState) {
    case InputState.Pan: return m_PanScalar;
    case InputState.Rotation: return m_RotationScalar;
    case InputState.PushPull: return m_PushPullScale;
    }

    return 1.0f;
  }

  // TODO - it'd be great if we could disentangle this from the multicam.
  IEnumerator RenderPathAndQuit() {
#if USD_SUPPORTED
    App.Instance.SetDesiredState(App.AppState.OfflineRendering);
    SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.MultiCamTool);
    MultiCamTool multiCam = SketchSurfacePanel.m_Instance.ActiveTool as MultiCamTool;
    Debug.Assert(multiCam != null); // Something's gone wrong if we've been unable to find multicam!
    if (multiCam == null) {
      yield break;
    }
    multiCam.ExternalObjectForceCameraStyle(MultiCamStyle.Video);
    MultiCamCaptureRig.ForceClippingPlanes(MultiCamStyle.Video);
    // Give the video tool time to switch - TODO - be a little more graceful here
    yield return new WaitForSeconds(2);
    // Make sure the videos have had time to load, and set playing ones to start
    while (VideoCatalog.Instance.IsScanning) {
      yield return null;
    }
    foreach (var widget in WidgetManager.m_Instance.VideoWidgets) {
      if (widget.VideoController.Playing) {
        widget.VideoController.Position = 0;
      }
    }
    yield return null;
    var ssMgr = MultiCamCaptureRig.ManagerFromStyle(MultiCamStyle.Video);
    ssMgr.SetScreenshotResolution(App.UserConfig.Video.OfflineResolution);
    multiCam.StartVideoCapture(MultiCamTool.GetSaveName(MultiCamStyle.Video), offlineRender: true);
    App.Instance.FrameCountDisplay.gameObject.SetActive(true);
    App.Instance.FrameCountDisplay.SetFramesTotal(VideoRecorderUtils.NumFramesInUsdSerializer);
    while (VideoRecorderUtils.ActiveVideoRecording != null) {
      App.Instance.FrameCountDisplay.SetCurrentFrame(
          VideoRecorderUtils.ActiveVideoRecording.FrameCount);
      yield return null;
    }
    ssMgr.SetScreenshotResolution(App.UserConfig.Video.Resolution);
#else
    Debug.LogError("Render path requires USD support");
    yield return null;
#endif
    QuitApp();
  }

  IEnumerator<Null> ExportListAndQuit() {
    App.Config.m_ForceDeterministicBirthTimeForExport = true;
    List<string> filesToExport = new List<string>();
    foreach (string filePattern in App.Config.m_FilePatternsToExport) {
      bool absolute = Path.IsPathRooted(filePattern);
      string directory = absolute ? Path.GetDirectoryName(filePattern) : App.UserSketchPath();
      string filename = Path.GetFileName(filePattern);
      var tiltFiles = Directory.GetFiles(directory, filename);
      filesToExport.AddRange(tiltFiles);
      // Also look at .tilt files which have been unzipped into directory format
      var tiltDirs = Directory.GetDirectories(directory, filename)
          .Where(n => n.EndsWith(".tilt"));
      filesToExport.AddRange(tiltDirs);
    }

    using (var coroutine = LoadAndExportList(filesToExport)) {
      while (coroutine.MoveNext()) {
        yield return coroutine.Current;
      }
    }
    QuitApp();
  }

  void QuitApp() {
  // We're done! Quit!
#if UNITY_EDITOR
    UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
  }

  // This coroutine must be run to completion or disposed.
  IEnumerator<Null> LoadAndExportList(List<string> filenames) {
    foreach (var filename in filenames) {
      using (var coroutine = LoadAndExport(filename)) {
        while (coroutine.MoveNext()) {
          yield return coroutine.Current;
        }
      }
    }
  }

  // This coroutine must be run to completion or disposed.
  IEnumerator<Null> LoadAndExportAll() {
    SketchSet sketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.User);
    for (int i = 0; i < SketchCatalog.m_Instance.GetSet(SketchSetType.User).NumSketches; ++i) {
      SceneFileInfo rInfo = sketchSet.GetSketchSceneFileInfo(i);
      using (var coroutine = LoadAndExport(rInfo.FullPath)) {
        while (coroutine.MoveNext()) {
          yield return coroutine.Current;
        }
      }
    }
  }

  /// Loads a .tilt file completely.
  /// This may be slightly buggy; it's not currently used for production.
  /// This coroutine must be run to completion or disposed.
  public IEnumerable<Null> LoadTiltFile(string filename) {
    using (var unused = new SceneSettings.RequestInstantSceneSwitch()) {
      IssueGlobalCommand(
          GlobalCommands.LoadNamedFile,
         iParam1: (int)LoadSpeed.Quick, sParam: filename);
      yield return null;
      while (App.Instance.IsLoading()) {
        yield return null;
      }

      // I don't know why App.Instance.IsLoading() doesn't cover this, but it doesn't.
      while (m_WidgetManager.CreatingMediaWidgets) {
        yield return null;
      }
      while (WidgetManager.m_Instance.AreMediaWidgetsStillLoading()) {
        yield return null;
      }

      // This is kind of a hack.
      // Despite the RequestInstantSceneSwitch above, I think scene colors still require
      // a few frames to settle; also, GrabWidgets need to register themselves on the
      // first frame, etc.
      for (int i = 0; i < 10; ++i) {
        yield return null;
      }
    }
  }

  // This coroutine must be run to completion or disposed.
  IEnumerator<Null> LoadAndExport(string filename) {
    foreach (var val in LoadTiltFile(filename)) {
      yield return val;
    }
    using (var coroutine = ExportCoroutine()) {
      while (coroutine.MoveNext()) {
        yield return coroutine.Current;
      }
    }
  }

  IEnumerator<Null> ExportCoroutine() {
    return OverlayManager.m_Instance.RunInCompositor(
        OverlayType.Export, () => {
          // Sort of a kludge: put stuff back into the main canvas
          SelectionManager.m_Instance.ClearActiveSelection();
          Export.ExportScene();
        }, 0.25f, false, true);
  }

  private void SaveModel() {
#if USD_SUPPORTED && (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {

      var current = SaveLoadScript.m_Instance.SceneFile;
      string basename = (current.Valid)
                    ? Path.GetFileNameWithoutExtension(current.FullPath)
                    : "Untitled";
      string directoryName = FileUtils.GenerateNonexistentFilename(
          App.ModelLibraryPath(), basename, "");

      string usdname = Path.Combine(directoryName, basename + ".usd");
      // TODO: export selection only, though this is still only experimental. The blocking
      // issue to implement this is that the export collector needs to expose this as an option.
      //
      // SelectionManager.m_Instance.HasSelection
      //    ? SelectionManager.m_Instance.SelectedStrokes
      //    : null
      ExportUsd.ExportPayload(usdname);
      OutputWindowScript.m_Instance.CreateInfoCardAtController(
        InputManager.ControllerName.Brush, "Model created!");
    }
#endif
  }

  /// Generates a view from the previous thumbnail viewpoint.
  public void GenerateReplacementSaveIcon() {
    if (SaveLoadScript.m_Instance.LastThumbnail_SS.HasValue) {
      TrTransform thumbnailInGlobalSpace = App.Scene.Pose *
                                           SaveLoadScript.m_Instance.LastThumbnail_SS.Value;

      m_SaveIconTool.ProgrammaticCaptureSaveIcon(thumbnailInGlobalSpace.translation,
                                                 thumbnailInGlobalSpace.rotation);
    } else {
      GenerateBestGuessSaveIcon();
    }
  }

  public void GenerateBestGuessSaveIcon() {
    TrTransform camXform = GenerateBestGuessSaveIconTransform();
    m_SaveIconTool.ProgrammaticCaptureSaveIcon(camXform.translation, camXform.rotation);
  }

  /// This positions the save icon camera at the user's head position, and faces it towards
  /// the most recent strokes the user has created.
  /// If there are no strokes, it faces towards the 'most recent' models.
  /// Sadly we cannot really mix the two as we don't know when the models were instantiated.
  public TrTransform GenerateBestGuessSaveIconTransform(int itemsToEnumerate = 0) {
    if (itemsToEnumerate == 0) {
      itemsToEnumerate = m_NumStrokesForSaveIcon;
    }
    int startIndex = Mathf.Max(0, SketchMemoryScript.AllStrokesCount() - itemsToEnumerate);
    var lastFewStrokes = SketchMemoryScript.AllStrokes().Skip(startIndex).ToArray();

    Bounds bounds;
    if (lastFewStrokes.Length > 0) {
      bounds = new Bounds(lastFewStrokes.First().m_ControlPoints.First().m_Pos, Vector3.zero);
      foreach (var stroke in lastFewStrokes.Skip(1)) {
        bounds.Encapsulate(stroke.m_ControlPoints.First().m_Pos);
        bounds.Encapsulate(stroke.m_ControlPoints.Last().m_Pos);
      }
    } else {
      // If we have no strokes, just use the aggregates bounding boxes of the blocks models.
      var models = m_WidgetManager.ModelWidgets.ToArray();
      // we should always have models to get here, but just in case...
      if (models.Length > 0) {
        startIndex = Mathf.Max(0, models.Length - itemsToEnumerate);
        bounds = models[startIndex].WorldSpaceBounds;
        for (int i = startIndex + 1; i < models.Length; ++i) {
          bounds.Encapsulate(models[i].WorldSpaceBounds);
        }
      } else {
        bounds = new Bounds(new Vector3(0, 1, -100000), Vector3.one); // some point in the distance
      }
    }

    Vector3 camPos = ViewpointScript.Head.position;
    Vector3 worldPos = App.Scene.Pose.MultiplyPoint(bounds.center);
    Quaternion direction = Quaternion.LookRotation(worldPos - camPos);
    return TrTransform.TR(camPos, direction);
  }


  public void GenerateBoundingBoxSaveIcon() {
    Vector3 vNewCamPos;
    {
      Bounds rCanvasBounds = App.Scene.AllCanvases
          .Select(canvas => canvas.GetCanvasBoundingBox())
          .Aggregate( (b1, b2) => { b1.Encapsulate(b2); return b1; } );

      //position the camera at the center of the canvas bounds
      vNewCamPos = rCanvasBounds.center;

      //back the camera up, along -z until we can see the extent of the bounds
      float fCanvasWidth = rCanvasBounds.max.x - rCanvasBounds.min.x;
      float fCanvasHeight = rCanvasBounds.max.y - rCanvasBounds.min.y;
      float fLargerExtent = Mathf.Max(fCanvasHeight, fCanvasWidth);

      //half fov for camera
      float fHalfFOV = m_SaveIconTool.ScreenshotManager.LeftEye.fieldOfView * 0.5f;

      //TODO: find the real reason this isn't working as it should
      float fMagicNumber = 1.375f;

      //set new cam position and zero out orientation
      float fBackupDistance = (fLargerExtent * 0.5f)
        * Mathf.Tan(Mathf.Deg2Rad * fHalfFOV) * fMagicNumber;
      vNewCamPos.z = rCanvasBounds.min.z - fBackupDistance;
    }

    m_SaveIconTool.ProgrammaticCaptureSaveIcon(vNewCamPos, Quaternion.identity);
  }

  private void LoadSketch(SceneFileInfo fileInfo, bool quickload = false) {
    LightsControlScript.m_Instance.DiscoMode = false;
    m_WidgetManager.FollowingPath = false;
    m_WidgetManager.CameraPathsVisible = false;
    m_WidgetManager.DestroyAllWidgets();
    m_PanelManager.ToggleSketchbookPanels(isLoadingSketch: true);
    ResetGrabbedPose(everything: true);
    PointerManager.m_Instance.EnablePointerStrokeGeneration(true);
    if (SaveLoadScript.m_Instance.Load(fileInfo)) {
      SketchMemoryScript.m_Instance.SetPlaybackMode(m_SketchPlaybackMode, m_DefaultSketchLoadSpeed);
      SketchMemoryScript.m_Instance.BeginDrawingFromMemory(bDrawFromStart: true);
      // the order of these two lines are important as ExitIntroSketch is setting the
      // color of the pointer and we need the color to be set before we go to the Loading
      // state. App script's ShouldTintControllers allow the controller to be tinted only
      // when the app is in the standard mode. That was there to prevent the controller color
      // from flickering while in the intro mode.
      App.Instance.ExitIntroSketch();
      App.Instance.SetDesiredState(quickload ? App.AppState.QuickLoad : App.AppState.Loading);
    }
    QualityControls.m_Instance.ResetAutoQuality();
    m_WidgetManager.ValidateCurrentCameraPath();
  }

  public void IssueGlobalCommand(GlobalCommands rEnum, int iParam1 = -1,
                                  int iParam2 = -1, string sParam = null) {
    switch (rEnum) {

    // Keyboard command, for debugging and emergency use.
    case GlobalCommands.Save: {
      if (!FileUtils.CheckDiskSpaceWithError(App.UserSketchPath())) {
        return;
      }
      // Disable active selection before saving.
      // This looks fishy, here's what's going on: When an object is selected and it has moved, the
      // the user is observing the selection canvas in the HMD, but we will be saving the main canvas.
      // Because they haven't deselected yet, the selection canvas and the main canvas are out of sync
      // so the strokes that will be saved will not match what the user sees.
      //
      // Here we deselect to force the main canvas to sync with the selection canvas, which is more
      // correct from the user's perspective. Push the deselect operation onto the stack so the user
      // can undo it after save, if desired.
      SelectionManager.m_Instance.ClearActiveSelection();
      GenerateReplacementSaveIcon();
      if (iParam1 == -1) {
        if (iParam2 == 1) {
          // Do a save in Tiltasaurus mode, which creates a new filename prefixed with
          // "Tiltasaurus_" and the current prompt. Also, don't eat gaze input so that the
          // Tiltasaurus prompt stays open.
          StartCoroutine(SaveLoadScript.m_Instance.SaveOverwrite(tiltasaurusMode: true));
        } else {
          StartCoroutine(SaveLoadScript.m_Instance.SaveOverwrite());
          EatGazeObjectInput();
        }
      } else {
        StartCoroutine(SaveLoadScript.m_Instance.SaveMonoscopic(iParam1));
      }
      break;
    }
    case GlobalCommands.SaveNew: {
      if (!FileUtils.CheckDiskSpaceWithError(App.UserSketchPath())) {
        return;
      }
      if (iParam1 == 1) {
        GenerateBoundingBoxSaveIcon();
      }
      StartCoroutine(SaveLoadScript.m_Instance.SaveNewName());
      EatGazeObjectInput();
      break;
    }
    case GlobalCommands.SaveAndUpload: {
      if (!FileUtils.CheckDiskSpaceWithError(App.UserSketchPath())) {
        Debug.LogError("SaveAndUpload: Disk space error");
        return;
      }
      SelectionManager.m_Instance.ClearActiveSelection();
      m_PanelManager.GetPanel(m_CurrentGazeObject).CreatePopUp(
          GlobalCommands.UploadToGenericCloud, (int)Cloud.None, -1);
      EatGazeObjectInput();
      break;
    }
    case GlobalCommands.ExportAll: {
      StartCoroutine(LoadAndExportAll());
      break;
    }
      // Glen Keane request: a way to draw guidelines that can be toggled on and off
      // at runtime.
    case GlobalCommands.DraftingVisibility: {
      if (!Enum.IsDefined(typeof(DraftingVisibilityOption), iParam1)) {
        Debug.LogError("Unknown draft visibility value: " + iParam1);
        return;
      }
      DraftingVisibilityOption option = (DraftingVisibilityOption)iParam1;
      if (option != m_DraftingVisibility) {
        m_DraftingVisibility = option;
        UpdateDraftingVisibility();
      }
      break;
    }
    case GlobalCommands.Load: {
      var index = iParam1;
      var sketchSetType = (SketchSetType)iParam2;
      SketchSet sketchSet = SketchCatalog.m_Instance.GetSet(sketchSetType);
      SceneFileInfo rInfo = sketchSet.GetSketchSceneFileInfo(index);
      if (rInfo != null) {
        LoadSketch(rInfo);
        if (m_ControlsType != ControlsType.ViewingOnly) {
          EatGazeObjectInput();
        }
      }
      break;
    }
    case GlobalCommands.LoadNamedFile:
      var fileInfo = new DiskSceneFileInfo(sParam);
      fileInfo.ReadMetadata();
      if (SaveLoadScript.m_Instance.LastMetadataError != null) {
        ControllerConsoleScript.m_Instance.AddNewLine(
            string.Format("Error detected in sketch '{0}'.\nTry re-saving.",
                          fileInfo.HumanName));
        Debug.LogWarning(string.Format("Error reading metadata for {0}.\n{1}",
            fileInfo.FullPath, SaveLoadScript.m_Instance.LastMetadataError));
      }
      LoadSketch(fileInfo, iParam1 == (int)LoadSpeed.Quick);
      if (m_ControlsType != ControlsType.ViewingOnly) {
        EatGazeObjectInput();
      }
      break;
    case GlobalCommands.NewSketch:
      NewSketch(fade: true);
      Vector3 vTrashSoundPos = m_CurrentGazeRay.origin;
      if (App.VrSdk.GetControllerDof() == VrSdk.DoF.Six) {
        vTrashSoundPos = InputManager.m_Instance.GetControllerPosition(
            InputManager.ControllerName.Wand);
      }
      AudioManager.m_Instance.PlayTrashSound(vTrashSoundPos);
      PromoManager.m_Instance.RequestAdvancedPanelsPromo();
      break;
    case GlobalCommands.SymmetryPlane:
      if (PointerManager.m_Instance.CurrentSymmetryMode != SymmetryMode.SinglePlane) {
        PointerManager.m_Instance.SetSymmetryMode(SymmetryMode.SinglePlane);
        ControllerConsoleScript.m_Instance.AddNewLine("Mirror Enabled");
      } else {
        PointerManager.m_Instance.SetSymmetryMode(SymmetryMode.None);
        ControllerConsoleScript.m_Instance.AddNewLine("Mirror Off");
      }
      break;
    case GlobalCommands.SymmetryFour:
      if (PointerManager.m_Instance.CurrentSymmetryMode != SymmetryMode.FourAroundY) {
        PointerManager.m_Instance.SetSymmetryMode(SymmetryMode.FourAroundY);
        ControllerConsoleScript.m_Instance.AddNewLine("Symmetry Enabled");
      } else {
        PointerManager.m_Instance.SetSymmetryMode(SymmetryMode.None);
        ControllerConsoleScript.m_Instance.AddNewLine("Symmetry Off");
      }
      InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.1f);
      break;
    case GlobalCommands.StraightEdge:
      PointerManager.m_Instance.StraightEdgeModeEnabled = !PointerManager.m_Instance.StraightEdgeModeEnabled;
      if (PointerManager.m_Instance.StraightEdgeModeEnabled) {
        ControllerConsoleScript.m_Instance.AddNewLine("Straight Edge On");
      } else {
        ControllerConsoleScript.m_Instance.AddNewLine("Straight Edge Off");
      }
      break;
    case GlobalCommands.AutoOrient:
      m_AutoOrientAfterRotation = !m_AutoOrientAfterRotation;
      if (m_AutoOrientAfterRotation) {
        ControllerConsoleScript.m_Instance.AddNewLine("Auto-Orient On");
      } else {
        ControllerConsoleScript.m_Instance.AddNewLine("Auto-Orient Off");
      }
      break;
    case GlobalCommands.Undo:
      SketchMemoryScript.m_Instance.StepBack();
      break;
    case GlobalCommands.Redo:
      SketchMemoryScript.m_Instance.StepForward();
      break;
    case GlobalCommands.AudioVisualization: // Intentionally blank.
      break;
    case GlobalCommands.ResetAllPanels:
      m_PanelManager.ResetWandPanelsConfiguration();
      EatGazeObjectInput();
      break;
    case GlobalCommands.SketchOrigin:
      m_SketchSurfacePanel.EnableSpecificTool(BaseTool.ToolType.SketchOrigin);
      EatGazeObjectInput();
      break;
    case GlobalCommands.ViewOnly:
      m_ViewOnly = !m_ViewOnly;
      RequestPanelsVisibility(!m_ViewOnly);
      PointerManager.m_Instance.RequestPointerRendering(!m_ViewOnly);
      m_SketchSurface.SetActive(!m_ViewOnly);
      m_Decor.SetActive(!m_ViewOnly);
      break;
    case GlobalCommands.SaveGallery:
      m_SketchSurfacePanel.EnableSpecificTool(BaseTool.ToolType.SaveIconTool);
      break;
    case GlobalCommands.DropCam:
      // Want to enable this if in monoscopic or VR modes.
      // TODO: seems odd to tie this switch to the controller type, should be based on some
      // other build-time configuration setting.
      if (App.VrSdk.GetControllerDof() != VrSdk.DoF.None) {
        m_DropCam.Show(!m_DropCam.gameObject.activeSelf);
      }
      break;
    case GlobalCommands.AnalyticsEnabled_Deprecated:
      break;
    case GlobalCommands.ToggleAutosimplification:
      QualityControls.AutosimplifyEnabled = !QualityControls.AutosimplifyEnabled;
      break;
    case GlobalCommands.Credits:
      LoadSketch(new DiskSceneFileInfo(m_CreditsSketchFilename, embedded:true, readOnly:true));
      EatGazeObjectInput();
      break;
    case GlobalCommands.AshleysSketch:
      LoadSketch(new DiskSceneFileInfo(m_AshleysSketchFilename, embedded:true, readOnly:true));
      EatGazeObjectInput();
      break;
    case GlobalCommands.FAQ:
      //launch external window and tell the user we did so
      EatGazeObjectInput();
      if (!App.Config.IsMobileHardware) {
        OutputWindowScript.m_Instance.CreateInfoCardAtController(
            InputManager.ControllerName.Brush,
            kRemoveHeadsetFyi, fPopScalar: 0.5f);
      }
      App.OpenURL(m_HelpCenterURL);
      break;
    case GlobalCommands.ReleaseNotes:
      //launch external window and tell the user we did so
      EatGazeObjectInput();
      if (!App.Config.IsMobileHardware) {
        OutputWindowScript.m_Instance.CreateInfoCardAtController(
            InputManager.ControllerName.Brush,
            kRemoveHeadsetFyi, fPopScalar: 0.5f);
      }
      App.OpenURL(m_ReleaseNotesURL);
      break;
    case GlobalCommands.ExportRaw:
      if (!FileUtils.CheckDiskSpaceWithError(App.UserExportPath())) {
        return;
      }
      EatGazeObjectInput();
      StartCoroutine(ExportCoroutine());
      break;
    case GlobalCommands.IRC:
      if (m_IRCChatWidget == null) {
        GameObject widgetobject = (GameObject)Instantiate(m_IRCChatPrefab);
        widgetobject.transform.parent = App.Instance.m_RoomTransform;
        m_IRCChatWidget = widgetobject.GetComponent<GrabWidget>();
        m_IRCChatWidget.Show(true);
      } else {
        m_IRCChatWidget.Show(false);
        m_IRCChatWidget = null;
      }
      break;
    case GlobalCommands.YouTubeChat:
      if (m_YouTubeChatWidget == null) {
        GameObject widgetobject = (GameObject)Instantiate(m_YouTubeChatPrefab);
        widgetobject.transform.parent = App.Instance.m_RoomTransform;
        m_YouTubeChatWidget = widgetobject.GetComponent<GrabWidget>();
        m_YouTubeChatWidget.Show(true);
      } else {
        m_YouTubeChatWidget.Show(false);
        m_YouTubeChatWidget = null;
      }
      break;
    case GlobalCommands.CameraOptions:
      // If we're switching in to Camera mode, make sure Multicam is selected.
      if (!m_PanelManager.CameraActive()) {
        SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.MultiCamTool);
      }
      m_PanelManager.ToggleCameraPanels();
      PointerManager.m_Instance.EatLineEnabledInput();
      SketchSurfacePanel.m_Instance.EatToolsInput();
      break;
    case GlobalCommands.ShowSketchFolder: {
      var index = iParam1;
      var sketchSetType = (SketchSetType)iParam2;
      SketchSet sketchSet = SketchCatalog.m_Instance.GetSet(sketchSetType);
      SceneFileInfo rInfo = sketchSet.GetSketchSceneFileInfo(index);
      EatGazeObjectInput();
      //launch external window and tell the user we did so
      //this call is windows only
      if ((Application.platform == RuntimePlatform.WindowsPlayer) ||
          (Application.platform == RuntimePlatform.WindowsEditor)) {
        OutputWindowScript.m_Instance.CreateInfoCardAtController(
            InputManager.ControllerName.Brush,
            kRemoveHeadsetFyi, fPopScalar: 0.5f);
        System.Diagnostics.Process.Start("explorer.exe",
            "/select," + rInfo.FullPath);
      }
      break;
    }
    case GlobalCommands.About:
      EatGazeObjectInput();

      if (!App.Config.IsMobileHardware) {
        // Launch external window and tell the user we did so/
        OutputWindowScript.m_Instance.CreateInfoCardAtController(
            InputManager.ControllerName.Brush,
            kRemoveHeadsetFyi, fPopScalar: 0.5f);
      }

      // This call is Windows only.
      if ((Application.platform == RuntimePlatform.WindowsPlayer) ||
          (Application.platform == RuntimePlatform.WindowsEditor)) {
        if (!Application.isEditor) {
          System.Diagnostics.Process.Start("notepad.exe",
              Path.Combine(App.PlatformPath(), "NOTICE"));
        } else {
          System.Diagnostics.Process.Start("notepad.exe",
              Path.Combine(App.SupportPath(), "ThirdParty/GeneratedThirdPartyNotices.txt"));
        }
      } else if (App.Config.IsMobileHardware) {
        App.OpenURL(m_ThirdPartyNoticesURL);
      }
      break;
    case GlobalCommands.StencilsDisabled:
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(new StencilsVisibleCommand());
      break;
    case GlobalCommands.StraightEdgeMeterDisplay:
      PointerManager.m_Instance.StraightEdgeGuide.FlipMeter();
      break;
    case GlobalCommands.Sketchbook:
      m_PanelManager.ToggleSketchbookPanels();
      PointerManager.m_Instance.EatLineEnabledInput();
      SketchSurfacePanel.m_Instance.EatToolsInput();
      break;
    case GlobalCommands.StraightEdgeShape:
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
      if (Config.IsExperimental) {
        PointerManager.m_Instance.StraightEdgeGuide.SetTempShape(
            (StraightEdgeGuideScript.Shape)iParam1);
      }
#endif
      break;
    case GlobalCommands.DeleteSketch: {
      var sketchSetType = (SketchSetType)iParam2;
      SketchSet sketchSet = SketchCatalog.m_Instance.GetSet(sketchSetType);
      sketchSet.DeleteSketch(iParam1);
      DismissPopupOnCurrentGazeObject(false);
      break;
    }
    case GlobalCommands.ShowWindowGUI:
      break;
    case GlobalCommands.Disco:
      LightsControlScript.m_Instance.DiscoMode = !LightsControlScript.m_Instance.DiscoMode;
      break;
    case GlobalCommands.AccountInfo: break; // Intentionally blank.
    case GlobalCommands.LoginToGenericCloud: {
      var ident = App.GetIdentity((Cloud)iParam1);
      if (!ident.LoggedIn) { ident.LoginAsync(); }
      // iParam2 is being used as a UX flag.  If not set to the default, it will cause the UI
      // to lose focus.
      if (iParam2 != -1) { EatGazeObjectInput(); }
      break;
    }
    case GlobalCommands.LogOutOfGenericCloud: {
      var ident = App.GetIdentity((Cloud)iParam1);
      if (ident.LoggedIn) { ident.Logout(); }
      break;
    }
    case GlobalCommands.UploadToGenericCloud: {
      Cloud cloud = (Cloud)iParam1;
      var ident = App.GetIdentity(cloud);
      if (!ident.LoggedIn) {
        ident.LoginAsync();
        break;
      }
      SelectionManager.m_Instance.ClearActiveSelection();
      VrAssetService.m_Instance.UploadCurrentSketchAsync(cloud, isDemoUpload: false).AsAsyncVoid();
      EatGazeObjectInput();
      break;
    }
    case GlobalCommands.ViewOnlineGallery: {
      if (!App.Config.IsMobileHardware) {
        OutputWindowScript.m_Instance.CreateInfoCardAtController(
            InputManager.ControllerName.Brush,
            kRemoveHeadsetFyi, fPopScalar: 0.5f);
      }
      App.OpenURL(kTiltBrushGalleryUrl);
      EatGazeObjectInput();
      break;
      }
    case GlobalCommands.CancelUpload:
      VrAssetService.m_Instance.CancelUpload();
      break;
    case GlobalCommands.ViewLastUpload:
      if (VrAssetService.m_Instance.LastUploadCompleteUrl != null) {
        var url = VrAssetService.m_Instance.LastUploadCompleteUrl;
        App.OpenURL(url);

        // The upload flow is different on mobile and requires the user to manually accept
        // that they'll go to the browser for publishing.  In that case, we want to reset
        // state when the leave to publish.  This is automatically part of the
        // UploadPopUpWindow state flow on PC.
        if (App.Config.IsMobileHardware) {
          DismissPopupOnCurrentGazeObject(true);
        }
      }
      break;
    case GlobalCommands.ShowGoogleDrive:
      EatGazeObjectInput();
      if (!App.Config.IsMobileHardware) {
        OutputWindowScript.m_Instance.CreateInfoCardAtController(
            InputManager.ControllerName.Brush,
            kRemoveHeadsetFyi, fPopScalar: 0.5f);
      }
      string baseDriveUrl = "https://drive.google.com";
      string driveURL = !App.GoogleIdentity.LoggedIn ? baseDriveUrl :
          string.Format(
            "http://accounts.google.com/AccountChooser?Email={0}&continue={1}",
            App.GoogleIdentity.Profile.email, baseDriveUrl);
      App.OpenURL(driveURL);
      break;
    case GlobalCommands.GoogleDriveSync:
      App.DriveSync.SyncEnabled = !App.DriveSync.SyncEnabled;
      break;
    case GlobalCommands.GoogleDriveSync_Folder:
      App.DriveSync.ToggleSyncOnFolderOfType((DriveSync.SyncedFolderType)iParam1);
      break;
    case GlobalCommands.Duplicate: {
      int selectedVerts = SelectionManager.m_Instance.NumVertsInSelection;
      if (!SketchMemoryScript.m_Instance.MemoryWarningAccepted &&
          SketchMemoryScript.m_Instance.WillVertCountPutUsOverTheMemoryLimit(selectedVerts)) {
        AudioManager.m_Instance.PlayUploadCanceledSound(InputManager.Wand.Transform.position);
        if (!m_PanelManager.MemoryWarningActive()) {
          m_PanelManager.ToggleMemoryWarningMode();
        }
      } else {
        ClipboardManager.Instance.DuplicateSelection(
          offsetDuplicate: !IsUserInteractingWithSelectionWidget());
      }
      EatToolScaleInput();
      break;
      }
    case GlobalCommands.AdvancedPanelsToggle:
      m_PanelManager.ToggleAdvancedPanels();
      // If we're now in basic mode, ensure we don't have advanced abilities.
      if (!m_PanelManager.AdvancedModeActive()) {
        m_WidgetManager.StencilsDisabled = true;
        m_WidgetManager.CameraPathsVisible = false;
        App.Switchboard.TriggerStencilModeChanged();
        m_SketchSurfacePanel.EnsureUserHasBasicToolEnabled();
        if (PointerManager.m_Instance.CurrentSymmetryMode != SymmetryMode.None) {
          PointerManager.m_Instance.SetSymmetryMode(SymmetryMode.None, false);
        }
      }
      PromoManager.m_Instance.RecordCompletion(PromoType.AdvancedPanels);
      EatGazeObjectInput();
      break;
    case GlobalCommands.Music: break; // Intentionally blank.
    case GlobalCommands.ToggleGroupStrokesAndWidgets:
      SelectionManager.m_Instance.ToggleGroupSelectedStrokesAndWidgets();
      EatToolScaleInput();
      break;
    case GlobalCommands.SaveModel:
      SaveModel();
      break;
    case GlobalCommands.ViewPolyPage:
      if (!App.Config.IsMobileHardware) {
        OutputWindowScript.m_Instance.CreateInfoCardAtController(
            InputManager.ControllerName.Brush,
            kRemoveHeadsetFyi, fPopScalar: 0.5f);
      }
      App.OpenURL(kPolyMainPageUri);
      EatGazeObjectInput();
      break;
    case GlobalCommands.ViewPolyGallery:
      if (!App.Config.IsMobileHardware) {
        OutputWindowScript.m_Instance.CreateInfoCardAtController(
            InputManager.ControllerName.Brush,
            kRemoveHeadsetFyi, fPopScalar: 0.5f);
      }
      App.OpenURL(kBlocksGalleryUrl);
      EatGazeObjectInput();
      break;
    case GlobalCommands.ExportListed:
      StartCoroutine(ExportListAndQuit());
      break;
    case GlobalCommands.RenderCameraPath:
      StartCoroutine(RenderPathAndQuit());
      break;
    case GlobalCommands.ToggleProfiling:
      ToggleProfiling();
      break;
    case GlobalCommands.DoAutoProfile:
      DoAutoProfile();
      break;
    case GlobalCommands.DoAutoProfileAndQuit:
      DoAutoProfileAndQuit();
      break;
    case GlobalCommands.ToggleSettings:
      m_PanelManager.ToggleSettingsPanels();
      PointerManager.m_Instance.EatLineEnabledInput();
      SketchSurfacePanel.m_Instance.EatToolsInput();
      break;
    case GlobalCommands.SummonMirror:
      PointerManager.m_Instance.BringSymmetryToUser();
      break;
    case GlobalCommands.InvertSelection:
      SelectionManager.m_Instance.InvertSelection();
      break;
    case GlobalCommands.SelectAll:
      SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.SelectionTool);
      SelectionManager.m_Instance.SelectAll();
      EatGazeObjectInput();
      break;
    case GlobalCommands.FlipSelection:
      SelectionManager.m_Instance.FlipSelection();
      break;
    case GlobalCommands.ToggleBrushLab:
      m_PanelManager.ToggleBrushLabPanels();
      PointerManager.m_Instance.EatLineEnabledInput();
      SketchSurfacePanel.m_Instance.EatToolsInput();
      break;
    case GlobalCommands.ToggleCameraPostEffects:
      CameraConfig.PostEffects = !CameraConfig.PostEffects;
      break;
    case GlobalCommands.ToggleWatermark:
      CameraConfig.Watermark = !CameraConfig.Watermark;
      break;
    case GlobalCommands.LoadConfirmComplexHigh:
      IssueGlobalCommand(GlobalCommands.Load, iParam1, iParam2, null);
      break;
    case GlobalCommands.LoadConfirmComplex:
      {
        var index = iParam1;
        var sketchSetType = (SketchSetType)iParam2;
        bool loadSketch = true;

        // If the sketchbook is active, we may want to show a popup instead of load.
        if (m_PanelManager.SketchbookActive()) {
          BasePanel sketchBook = m_PanelManager.GetSketchBookPanel();
          if (sketchBook != null) {
            // Get triangle count from cloud scene file info.
            SketchSet sketchSet = SketchCatalog.m_Instance.GetSet(sketchSetType);
            SceneFileInfo sfi = sketchSet.GetSketchSceneFileInfo(index);
            int tris = sfi.TriangleCount ?? -1;

            // Show "this is bad" popup if we're over the triangle limit.
            if (tris > QualityControls.m_Instance.AppQualityLevels.MaxPolySketchTriangles) {
              loadSketch = false;
              sketchBook.CreatePopUp(GlobalCommands.LoadConfirmComplexHigh, iParam1, iParam2);
            } else if (tris >
                QualityControls.m_Instance.AppQualityLevels.WarningPolySketchTriangles) {
              // Show, "this could be bad" popup if we're over the warning limit.
              loadSketch = false;
              sketchBook.CreatePopUp(GlobalCommands.Load, iParam1, iParam2);
            }
          }
        }

        if (loadSketch) {
          IssueGlobalCommand(GlobalCommands.Load, iParam1, iParam2, null);
        }
      }
      break;
    case GlobalCommands.LoadConfirmUnsaved:
      {
        BasePanel sketchBook = m_PanelManager.GetSketchBookPanel();
        if ((sketchBook != null) && SketchMemoryScript.m_Instance.IsMemoryDirty()) {
          sketchBook.CreatePopUp(GlobalCommands.LoadWaitOnDownload, iParam1, iParam2, null);
        } else {
          IssueGlobalCommand(GlobalCommands.LoadWaitOnDownload, iParam1, iParam2, null);
        }
      }
      break;
    case GlobalCommands.LoadWaitOnDownload: {
        bool download = false;
        if (iParam2 == (int) SketchSetType.Drive) {
          BasePanel sketchBook = m_PanelManager.GetSketchBookPanel();
          var googleSketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Drive);
          if (sketchBook != null
              && googleSketchSet != null
              && googleSketchSet.IsSketchIndexValid(iParam1)
              && !googleSketchSet.GetSketchSceneFileInfo(iParam1).Available) {
            sketchBook.CreatePopUp(GlobalCommands.LoadConfirmComplex, iParam1, iParam2, null);
            download = true;
          }
        }
        if (!download) {
          IssueGlobalCommand(GlobalCommands.LoadConfirmComplex, iParam1, iParam2, null);
        }
      }
      break;
    case GlobalCommands.MemoryWarning:
      if (iParam1 > 0) {
        SketchMemoryScript.m_Instance.MemoryWarningAccepted = true;
      }
      m_PanelManager.ToggleMemoryWarningMode();
      break;
    case GlobalCommands.MemoryExceeded:
      // If we're in the memory exceeded app state, exit.
      if (App.CurrentState == App.AppState.MemoryExceeded) {
        App.Instance.SetDesiredState(App.AppState.Standard);
      } else {
        // If we're not in the full app state, just switch our panel mode.
        m_PanelManager.ToggleMemoryWarningMode();
      }
      break;
    case GlobalCommands.ShowTos:
      // Launch external window and tell the user we did so
      EatGazeObjectInput();
      if (!App.Config.IsMobileHardware) {
        OutputWindowScript.m_Instance.CreateInfoCardAtController(
            InputManager.ControllerName.Brush,
            kRemoveHeadsetFyi, fPopScalar: 0.5f);
      }
      App.OpenURL(m_TosURL);
      break;
    case GlobalCommands.ShowPrivacy:
      // Launch external window and tell the user we did so
      EatGazeObjectInput();
      if (!App.Config.IsMobileHardware) {
        OutputWindowScript.m_Instance.CreateInfoCardAtController(
            InputManager.ControllerName.Brush,
            kRemoveHeadsetFyi, fPopScalar: 0.5f);
      }
      App.OpenURL(m_PrivacyURL);
      break;
    case GlobalCommands.ShowQuestSideLoading:
      // Launch external window and tell the user we did so
      EatGazeObjectInput();
      if (!App.Config.IsMobileHardware) {
        OutputWindowScript.m_Instance.CreateInfoCardAtController(
            InputManager.ControllerName.Brush,
            kRemoveHeadsetFyi, fPopScalar: 0.5f);
      }
      App.OpenURL(m_QuestSideLoadingHowToURL);
      break;
    case GlobalCommands.UnloadReferenceImageCatalog:
      ReferenceImageCatalog.m_Instance.UnloadAllImages();
      break;
    case GlobalCommands.ToggleCameraPathVisuals:
      m_WidgetManager.CameraPathsVisible = !m_WidgetManager.CameraPathsVisible;
      break;
    case GlobalCommands.ToggleCameraPathPreview:
      m_WidgetManager.FollowingPath = !m_WidgetManager.FollowingPath;
      break;
    case GlobalCommands.DeleteCameraPath: {
        var cameraPath = m_WidgetManager.GetCurrentCameraPath();
        GrabWidget cameraPathWidget = cameraPath == null ? null : cameraPath.m_WidgetScript;
        m_WidgetManager.DeleteCameraPath(cameraPathWidget);
      }
      break;
    case GlobalCommands.RecordCameraPath:
      // Turn off MultiCam if we're going to record the camera path.
      if (m_SketchSurfacePanel.GetCurrentToolType() == BaseTool.ToolType.MultiCamTool) {
        m_SketchSurfacePanel.EnableDefaultTool();
      }
      CameraPathCaptureRig.RecordPath();
      EatGazeObjectInput();
      break;
    case GlobalCommands.Null: break; // Intentionally blank.
    default:
      Debug.LogError($"Unrecognized command {rEnum}");
      break;
    }
  }

  public bool IsCommandActive(GlobalCommands rEnum, int iParam = -1) {
    switch (rEnum) {
    case GlobalCommands.StraightEdge: return PointerManager.m_Instance.StraightEdgeModeEnabled;
    case GlobalCommands.StraightEdgeMeterDisplay: return PointerManager.m_Instance.StraightEdgeGuide.IsShowingMeter();
    case GlobalCommands.SymmetryPlane: return PointerManager.m_Instance.CurrentSymmetryMode == SymmetryMode.SinglePlane;
    case GlobalCommands.SymmetryFour: return PointerManager.m_Instance.CurrentSymmetryMode == SymmetryMode.FourAroundY;
    case GlobalCommands.AutoOrient: return m_AutoOrientAfterRotation;
    case GlobalCommands.AudioVisualization: return VisualizerManager.m_Instance.VisualsRequested;
    case GlobalCommands.AdvancedPanelsToggle: return m_PanelManager.AdvancedModeActive();
    case GlobalCommands.Music: return VisualizerManager.m_Instance.VisualsRequested;
    case GlobalCommands.DropCam: return m_DropCam.gameObject.activeSelf;
    case GlobalCommands.ToggleAutosimplification: return QualityControls.AutosimplifyEnabled;
    case GlobalCommands.DraftingVisibility: return m_DraftingVisibility == (DraftingVisibilityOption)iParam;
    case GlobalCommands.Cameras: return SketchSurfacePanel.m_Instance.GetCurrentToolType() == BaseTool.ToolType.AutoGif ||
      SketchSurfacePanel.m_Instance.GetCurrentToolType() == BaseTool.ToolType.ScreenshotTool;
    case GlobalCommands.IRC: return m_IRCChatWidget != null;
    case GlobalCommands.YouTubeChat: return m_YouTubeChatWidget != null;
    case GlobalCommands.StencilsDisabled: return m_WidgetManager.StencilsDisabled;
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    case GlobalCommands.StraightEdgeShape: return PointerManager.m_Instance.StraightEdgeGuide.TempShape == (StraightEdgeGuideScript.Shape)iParam ||
      (PointerManager.m_Instance.StraightEdgeGuide.TempShape == StraightEdgeGuideScript.Shape.None
      && PointerManager.m_Instance.StraightEdgeGuide.CurrentShape == (StraightEdgeGuideScript.Shape)iParam);
#endif
    case GlobalCommands.Disco: return LightsControlScript.m_Instance.DiscoMode;
    case GlobalCommands.ToggleGroupStrokesAndWidgets: return SelectionManager.m_Instance.SelectionIsInOneGroup;
    case GlobalCommands.ToggleProfiling: return UnityEngine.Profiling.Profiler.enabled;
    case GlobalCommands.ToggleCameraPostEffects: return CameraConfig.PostEffects;
    case GlobalCommands.ToggleWatermark: return CameraConfig.Watermark;
    case GlobalCommands.ToggleCameraPathVisuals: return m_WidgetManager.CameraPathsVisible;
    case GlobalCommands.ToggleCameraPathPreview: return m_WidgetManager.FollowingPath;
    case GlobalCommands.SelectCameraPath: return m_WidgetManager.IsCameraPathAtIndexCurrent(iParam) &&
        m_WidgetManager.CameraPathsVisible;
    case GlobalCommands.GoogleDriveSync_Folder:
        return App.DriveSync.IsFolderOfTypeSynced((DriveSync.SyncedFolderType)iParam);
    case GlobalCommands.GoogleDriveSync: return App.DriveSync.SyncEnabled;
    case GlobalCommands.RecordCameraPath: return VideoRecorderUtils.ActiveVideoRecording != null;
    }
    return false;
  }

  public void NewSketch(bool fade) {
    LightsControlScript.m_Instance.DiscoMode = false;
    m_WidgetManager.FollowingPath = false;
    SketchMemoryScript.m_Instance.ClearMemory();
    ControllerConsoleScript.m_Instance.AddNewLine("Sketch Cleared");
    ResetGrabbedPose(everything: true);
    QualityControls.m_Instance.ResetAutoQuality();
    InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.1f);
    SaveLoadScript.m_Instance.ResetLastFilename();
    SelectionManager.m_Instance.RemoveFromSelection(false);
    PointerManager.m_Instance.ResetSymmetryToHome();

    // If we've got the camera path tool active, switch back to the default tool.
    // I'm doing this because if we leave the camera path tool active, the camera path
    // panel shows the button highlighted, which affects the user's flow for being
    // invited to start a path.  It looks weird.
    if (m_SketchSurfacePanel.ActiveToolType == BaseTool.ToolType.CameraPathTool) {
      m_SketchSurfacePanel.EnableDefaultTool();
    }

    m_WidgetManager.DestroyAllWidgets();
    if (LightsControlScript.m_Instance.LightsChanged ||
        SceneSettings.m_Instance.EnvironmentChanged) {
      SceneSettings.m_Instance.RecordSkyColorsForFading();
      SceneSettings.m_Instance.SetDesiredPreset(
        SceneSettings.m_Instance.GetDesiredPreset(), skipFade: !fade);
    }
    // Blank the thumbnail position so that autosave won't save the thumbnail position to be
    // the one from the old sketch.
    SaveLoadScript.m_Instance.LastThumbnail_SS = null;

    // Re-set the quality level to reset simplification level
    QualityControls.m_Instance.QualityLevel = QualityControls.m_Instance.QualityLevel;

    App.PolyAssetCatalog.ClearLoadingQueue();
    App.PolyAssetCatalog.UnloadUnusedModels();
  }

  private bool WorldIsReset(bool toSavedXf) {
    return App.Scene.Pose ==
      (toSavedXf ? SketchMemoryScript.m_Instance.InitialSketchTransform : TrTransform.identity);
  }

  public bool IsCommandAvailable(GlobalCommands rEnum, int iParam = -1) {
    // TODO: hide gallery view / publish if there are no saved sketches
    switch (rEnum) {
    case GlobalCommands.Undo: return SketchMemoryScript.m_Instance.CanUndo();
    case GlobalCommands.Redo: return SketchMemoryScript.m_Instance.CanRedo();
    case GlobalCommands.Save:
      bool canSave =
          SaveLoadScript.m_Instance.SceneFile.Valid &&
          SaveLoadScript.m_Instance.IsSavingAllowed();
      return canSave && (!WorldIsReset(toSavedXf: true) ||
          (SketchHasChanges() && SketchMemoryScript.m_Instance.IsMemoryDirty()));
    case GlobalCommands.SaveOptions:
    case GlobalCommands.SaveNew:
    case GlobalCommands.SaveGallery:
      return SketchHasChanges();
    case GlobalCommands.SaveOnLocalChanges:
      if (!SaveLoadScript.m_Instance.SceneFile.Valid) {
        // No save file, but something has changed.
        return SketchHasChanges();
      } else {
        if (SaveLoadScript.m_Instance.CanOverwriteSource) {
          // Save file, and it's our file.  Whether we have changes is irrelevant.
          return true;
        }
        // Save file, but it's not our file.  Only make a copy if there are local changes.
        return SketchMemoryScript.m_Instance.IsMemoryDirty();
      }
    case GlobalCommands.UploadToGenericCloud:
      return SketchMemoryScript.m_Instance.HasVisibleObjects() ||
          m_WidgetManager.ExportableModelWidgets.Any(w => w.gameObject.activeSelf) ||
          m_WidgetManager.ImageWidgets.Any(w => w.gameObject.activeSelf) ||
          VrAssetService.m_Instance.UploadProgress >= 1.0f ||
          VrAssetService.m_Instance.LastUploadFailed;
    case GlobalCommands.SaveAndUpload:
      return App.GoogleIdentity.LoggedIn &&
          (VrAssetService.m_Instance.UploadProgress <= 0.0f) &&
          IsCommandAvailable(GlobalCommands.UploadToGenericCloud);
    case GlobalCommands.NewSketch:
      return SketchHasChanges();
    case GlobalCommands.Credits:
    case GlobalCommands.AshleysSketch:
      return !SketchHasChanges() && !SketchMemoryScript.m_Instance.IsMemoryDirty();
    case GlobalCommands.Tiltasaurus: return TiltBrush.Tiltasaurus.m_Instance.TiltasaurusAvailable();
    case GlobalCommands.ExportRaw:
      return SketchMemoryScript.m_Instance.HasVisibleObjects() ||
          m_WidgetManager.ModelWidgets.Any(w => w.gameObject.activeSelf) ||
          m_WidgetManager.ImageWidgets.Any(w => w.gameObject.activeSelf);
    case GlobalCommands.ResetAllPanels: return m_PanelManager.PanelsHaveBeenCustomized();
    case GlobalCommands.Duplicate: return ClipboardManager.Instance.CanCopy;
    case GlobalCommands.ToggleGroupStrokesAndWidgets: return SelectionManager.m_Instance.SelectionCanBeGrouped;
    case GlobalCommands.SaveModel: return SelectionManager.m_Instance.HasSelection;
    case GlobalCommands.SummonMirror: return PointerManager.m_Instance.CurrentSymmetryMode ==
        SymmetryMode.SinglePlane;
    case GlobalCommands.InvertSelection:
    case GlobalCommands.FlipSelection:
      return SelectionManager.m_Instance.HasSelection;
    case GlobalCommands.SelectAll: return SketchMemoryScript.m_Instance.HasVisibleObjects() ||
        m_WidgetManager.HasSelectableWidgets();
    case GlobalCommands.UnloadReferenceImageCatalog:
      return ReferenceImageCatalog.m_Instance.AnyImageValid();
    case GlobalCommands.ToggleCameraPathPreview:
      return m_WidgetManager.CanRecordCurrentCameraPath();
    case GlobalCommands.DeleteCameraPath:
      return CameraPathCaptureRig.Enabled && m_WidgetManager.AnyActivePathHasAKnot();
    case GlobalCommands.ToggleCameraPathVisuals:
      return m_WidgetManager.AnyActivePathHasAKnot();
    case GlobalCommands.GoogleDriveSync:
      return App.GoogleIdentity.LoggedIn;
    case GlobalCommands.RecordCameraPath: return m_WidgetManager.CameraPathsVisible;
    }
    return true;
  }

  public bool SketchHasChanges() {
    if (SceneSettings.m_Instance.IsTransitioning) { return false; }
    return SketchMemoryScript.m_Instance.HasVisibleObjects() ||
        SceneSettings.m_Instance.EnvironmentChanged ||
        LightsControlScript.m_Instance.LightsChanged ||
        m_WidgetManager.ModelWidgets.Any(w => w.gameObject.activeSelf) ||
        m_WidgetManager.StencilWidgets.Any(w => w.gameObject.activeSelf) ||
        m_WidgetManager.ImageWidgets.Any(w => w.gameObject.activeSelf) ||
        m_WidgetManager.VideoWidgets.Any(w => w.gameObject.activeSelf) ||
        m_WidgetManager.AnyCameraPathWidgetsActive;
  }

  public void OpenPanelOfType(BasePanel.PanelType type, TrTransform trSpawnXf) {
    m_PanelManager.OpenPanel(type, trSpawnXf);
    EatGazeObjectInput();
  }

  public void RestoreFloatingPanels() {
    if (!m_SketchSurfacePanel.ActiveTool.HidePanels()) {
      m_PanelManager.RestoreHiddenPanels();
    }
  }

  public void UpdateDraftingVisibility() {
    float value = 0;
    switch (m_DraftingVisibility) {
      case DraftingVisibilityOption.Visible:
        value = 1;
        break;
      case DraftingVisibilityOption.Transparent:
        value = .5f;
        break;
      case DraftingVisibilityOption.Hidden:
        value = 0;
        break;
    }
    Shader.SetGlobalFloat("_DraftingVisibility01", value);
  }

  private void ToggleProfiling() {
    if (Debug.isDebugBuild && ProfileDisplay.Instance != null) {
      ProfileDisplay.Instance.gameObject.SetActive(UnityEngine.Profiling.Profiler.enabled);
    }
    if (UnityEngine.Profiling.Profiler.enabled) {
      ProfilingManager.Instance.StopProfiling();
    } else {
      ProfilingManager.Instance.StartProfiling(App.UserConfig.Profiling.ProflingMode);
    }
  }

  private void DoAutoProfile() {
    StartCoroutine(DoProfiling());
  }

  private void DoAutoProfileAndQuit() {
    StartCoroutine(DoProfiling(andQuit: true));
  }

  private IEnumerator DoProfiling(bool andQuit = false) {
    TrTransform oldWandPose = TrTransform.FromTransform(InputManager.Wand.Geometry.transform);
    TrTransform oldBrushPose = TrTransform.FromTransform(InputManager.Brush.Geometry.transform);

    App.AppState oldState = App.CurrentState;
    App.Instance.SetDesiredState(App.AppState.AutoProfiling);
    while (App.CurrentState != App.AppState.AutoProfiling) {
      yield return null;
    }

    TrTransform camPose = App.Scene.Pose * SaveLoadScript.m_Instance.ReasonableThumbnail_SS;
    camPose.ToTransform(App.VrSdk.GetVrCamera().transform);
    float controllerDirection = App.UserConfig.Profiling.ShowControllers ? 1f : -1f;
    Vector3 roffset = Camera.main.transform.right * 2f;
    Vector3 fOffset = Camera.main.transform.forward * 4f * controllerDirection;
    InputManager.Brush.Geometry.transform.position = Camera.main.transform.position + roffset + fOffset;
    InputManager.Brush.Geometry.transform.rotation = Camera.main.transform.rotation;
    InputManager.Wand.Geometry.transform.position = Camera.main.transform.position - roffset + fOffset;
    InputManager.Wand.Geometry.transform.rotation = Camera.main.transform.rotation;
    m_PanelManager.LockPanelsToController();

    ProfilingManager.Instance.StartProfiling(App.UserConfig.Profiling.ProflingMode);
    yield return new WaitForSeconds(App.UserConfig.Profiling.Duration);
    ProfilingManager.Instance.StopProfiling();

    if (App.UserConfig.Profiling.TakeScreenshot) {
      GameObject camObj = new GameObject("ScreenShotter");
      Camera cam = camObj.AddComponent<Camera>();
      cam.CopyFrom(App.VrSdk.GetVrCamera());
      cam.stereoTargetEye = StereoTargetEyeMask.None;
      cam.clearFlags = CameraClearFlags.SolidColor;
      camPose.ToTransform(camObj.transform);
      int res = App.UserConfig.Profiling.ScreenshotResolution;
      RenderTexture renderTexture = RenderTexture.GetTemporary(res, res, 24);
      try {
        cam.targetTexture = renderTexture;
        cam.Render();
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = renderTexture;
        var texture = new Texture2D(res, res, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        RenderTexture.active = prev;
        byte[] jpegBytes = texture.EncodeToJPG();
        string filename =
          Path.GetFileNameWithoutExtension(SaveLoadScript.m_Instance.SceneFile.FullPath);
        File.WriteAllBytes(Path.Combine(App.UserPath(), filename + ".jpg"), jpegBytes);
      } finally {
        Destroy(camObj);
        RenderTexture.ReleaseTemporary(renderTexture);
      }
    }

    oldWandPose.ToTransform(InputManager.Wand.Geometry.transform);
    oldBrushPose.ToTransform(InputManager.Brush.Geometry.transform);
    App.Instance.SetDesiredState(oldState);

    if (andQuit) {
      QuitApp();
    }
  }
}

}  // namespace TiltBrush
