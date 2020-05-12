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
using UnityEngine;
using Valve.VR;

using KeyMap = System.Collections.Generic.Dictionary<
  int,
  UnityEngine.KeyCode[]>;

namespace TiltBrush {

// Ordering:
// - Viewpoint must come before InputManager (InputManager uses Viewpoint)
// - InputManager must come before scripts that use it, specifically SketchControls
public class InputManager : MonoBehaviour {
  const string PLAYER_PREF_WAND_ON_RIGHT = "WandOnRight";

  /// touchpad vector must be at least this magnitude to register as directional press
  const float kSteamTrackpadButtonSqrMagnitudeThreshold = 0.1f * 0.1f;

  // Controller-swap gesture tunables
  const float kSwapDistMeters = 0.04f;
  const float kSwapResetDistMeters = 0.16f;
  const float kSwapForwardAngle = 130f;  // degrees
  const float kSwapVelocityAngle = 150f;  // degrees
  const float kSwapAcceleration = 10f;  // decimeters / second^2

  public enum ControllerName {
    Wand = 0,
    Brush,
    Num,
    None
  }

  /// WARNING: do not arbitrarily rename these enum values.
  /// There is magic in MapCommandToKeyboard that relies on SketchCommand values having
  /// the same names as KeyboardShortcut values.
  public enum SketchCommands {
    Activate,
    AltActivate,
    PanelShowHide,
    LockToHead,
    PivotRotation,
    WandRotation,
    LockToController,
    Scale,
    Sensitivity,
    Reset,
    Undo,
    Redo,
    Delete,
    Abort,
    Panic,
    RewindTimeline,
    AdvanceTimeline,
    TimelineHome,
    TimelineEnd,
    MultiCamSelection,
    WorldTransformReset,
    Teleport,
    ShowPinCushion,
    ToggleDefaultTool,
    RespawnPanels,
    SwapControls,
    MenuContextClick,
    PinWidget,
    ToggleSelection,
    GroupStrokes,
    DuplicateSelection,
    Confirm,
    Cancel,
    Trash,
    Share
  }

  /// WARNING: do not arbitrarily rename these enum values.
  /// There is magic in MapCommandToKeyboard that relies on SketchCommand values having
  /// the same names as KeyboardShortcut values.
  public enum KeyboardShortcut {
    LockToHead,
    PivotRotation,
    Scale,

    RewindTimeline,
    AdvanceTimeline,
    TimelineHome,
    TimelineEnd,
    Reset,
    Undo,
    Redo,
    Delete,
    Abort,

    SaveNew,
    ExportAll,
    SwitchCamera,
    _Unused_1,
    CycleCanvas,
    ViewOnly,
    ToggleScreenMirroring,
    PreviousTool,
    NextTool,
    _Unused_2,
    _Unused_3,
    CycleSymmetryMode,
    Export,
    StoreHeadTransform,
    RecallHeadTransform,
    ToggleLightType,

    CheckStrokes,

    ResetScene,
    StraightEdge,

    Save,
    Load,

    Forward,
    Backward,

    PositionMonoCamera,

    ToggleHeadStationaryOrWobble,
    ToggleHeadStationaryOrFollow,

    DecreaseSlowFollowSmoothing,
    IncreaseSlowFollowSmoothing,

    ToggleGVRAudio,

    ResetEverything,
    GotoInitialPosition,
    ExtendDemoTimer,
    InstantUpload,

    TossWidget,

    ToggleProfile,
  }

  // Standard mapping of keyboard shortcut to actual keyboard keys.
  // The keycodes are an "or", not an "and". Just one of the keycodes
  // in the keycode collections has to be registered for the shortcut to be
  // active.
  private static readonly KeyMap m_KeyMap = new KeyMap {
    { (int)KeyboardShortcut.LockToHead,                   new[] { KeyCode.LeftShift } },
    { (int)KeyboardShortcut.PivotRotation,                new[] { KeyCode.LeftControl } },
    { (int)KeyboardShortcut.Scale,                        new[] { KeyCode.Tab } },

    { (int)KeyboardShortcut.RewindTimeline,               new[] { KeyCode.Minus } },
    { (int)KeyboardShortcut.AdvanceTimeline,              new[] { KeyCode.Plus } },
    { (int)KeyboardShortcut.TimelineHome,                 new[] { KeyCode.Home } },
    { (int)KeyboardShortcut.TimelineEnd,                  new[] { KeyCode.End } },
    { (int)KeyboardShortcut.Reset,                        new[] { KeyCode.Space } },
    { (int)KeyboardShortcut.Undo,                         new[] { KeyCode.Z } },
    { (int)KeyboardShortcut.Redo,                         new[] { KeyCode.X } },
    { (int)KeyboardShortcut.Delete,                       new[] { KeyCode.Delete } },
    { (int)KeyboardShortcut.Abort,                        new[] { KeyCode.Escape } },

    { (int)KeyboardShortcut.SaveNew,                      new[] { KeyCode.S } },
    { (int)KeyboardShortcut.ExportAll,                    new[] { KeyCode.A } },
    { (int)KeyboardShortcut.ToggleProfile,                new[] { KeyCode.K } },
    // Context-dependent
    { (int)KeyboardShortcut.SwitchCamera,                 new[] { KeyCode.C } },
    { (int)KeyboardShortcut.CycleCanvas,                  new[] { KeyCode.C } },
    { (int)KeyboardShortcut.ViewOnly,                     new[] { KeyCode.H } },
    { (int)KeyboardShortcut.ToggleScreenMirroring,        new[] { KeyCode.M } },
    { (int)KeyboardShortcut.PreviousTool,                 new[] { KeyCode.LeftArrow } },
    { (int)KeyboardShortcut.NextTool,                     new[] { KeyCode.RightArrow } },
    { (int)KeyboardShortcut.CycleSymmetryMode,            new[] { KeyCode.F2 } },
    { (int)KeyboardShortcut.Export,                       new[] { KeyCode.E } },
    { (int)KeyboardShortcut.StoreHeadTransform,           new[] { KeyCode.O } }, // Also checks for shift
    { (int)KeyboardShortcut.RecallHeadTransform,          new[] { KeyCode.O } },
    { (int)KeyboardShortcut.ToggleLightType,              new[] { KeyCode.P } },

    { (int)KeyboardShortcut.CheckStrokes,                 new[] { KeyCode.V } },

    { (int)KeyboardShortcut.ResetScene,                   new[] { KeyCode.Return } },
    { (int)KeyboardShortcut.StraightEdge,                 new[] { KeyCode.CapsLock } },

    { (int)KeyboardShortcut.Save,                         new[] { KeyCode.S } },
    { (int)KeyboardShortcut.Load,                         new[] { KeyCode.L } },

    { (int)KeyboardShortcut.Forward,                      new[] { KeyCode.N } },
    { (int)KeyboardShortcut.Backward,                     new[] { KeyCode.M } },

    { (int)KeyboardShortcut.PositionMonoCamera,           new[] { KeyCode.LeftAlt, KeyCode.RightAlt } },

    { (int)KeyboardShortcut.ToggleHeadStationaryOrWobble, new[] { KeyCode.Q } },
    { (int)KeyboardShortcut.ToggleHeadStationaryOrFollow, new[] { KeyCode.W } },

    { (int)KeyboardShortcut.DecreaseSlowFollowSmoothing,  new[] { KeyCode.E } },
    { (int)KeyboardShortcut.IncreaseSlowFollowSmoothing,  new[] { KeyCode.R } },

    { (int)KeyboardShortcut.ToggleGVRAudio,               new[] { KeyCode.BackQuote } },

    { (int)KeyboardShortcut.TossWidget,                   new[] { KeyCode.Y } },
  };

  // Separate keymap for when demo mode is enabled.
  // Determined by DemoManager.m_Instance.DemoModeEnabled == true
  private static readonly KeyMap m_DemoKeyMap = new KeyMap {
    { (int)KeyboardShortcut.ResetEverything, new KeyCode[] { KeyCode.Delete, KeyCode.Backspace } },
    { (int)KeyboardShortcut.GotoInitialPosition, new KeyCode[] { KeyCode.P } },
    { (int)KeyboardShortcut.ExtendDemoTimer, new KeyCode[] { KeyCode.E } },
    { (int)KeyboardShortcut.InstantUpload, new KeyCode[] { KeyCode.U } },
  };

  private KeyMap ActiveKeyMap {
    get {
      if (DemoManager.m_Instance.DemoModeEnabled) {
        return m_DemoKeyMap;
      } else {
        return m_KeyMap;
      }
    }
  }

  [System.Serializable]
  public struct HmdInfo {
    public MeshRenderer m_Renderer;
  }

  public struct TouchInput {
    public bool m_Valid;
    public Vector2 m_Pos;
  }

  //
  // Static API
  //

  public static InputManager m_Instance;
  // This is indexed by enum ControllerName
  // Note that m_ControllerInfos is not the source of truth for controllers.  That's located
  // in VrSdk.m_VrControls.  These are potentially out of date for a frame when controllers
  // change.
  public static ControllerInfo[] Controllers { get { return m_Instance.m_ControllerInfos; } }
  public static ControllerInfo Wand { get { return Controllers[(int)ControllerName.Wand]; } }
  public static ControllerInfo Brush { get { return Controllers[(int)ControllerName.Brush]; } }
  static public event Action OnSwapControllers;

  //
  // Inspector configurables
  //
  [SerializeField] Transform m_SwapEffect;
  [SerializeField] HmdInfo m_HmdInfo;

  //
  // Internal data
  //

  private bool m_AllowVrControllers = true;
  // This is indexed by enum ControllerName
  private ControllerInfo[] m_ControllerInfos;

  private bool m_InhibitControllerSwap = false;

  private float m_InputThreshold = 0.0001f;

  private TouchInput m_Touch;
  private bool m_WandOnRight;

  private Dictionary<int, KeyboardShortcut?> m_SketchToKeyboardCommandMap =
      new Dictionary<int, KeyboardShortcut?>();

  //
  // Public properties
  //

  static public void ControllersHaveChanged() {
    OnSwapControllers();
  }

  public event Action ControllerPosesApplied;

  public bool AllowVrControllers {
    get { return m_AllowVrControllers; }
    set {
      m_AllowVrControllers = value;
      EnableVrControllers(value);
    }
  }


  public void EnableVrControllers(bool bEnable) {
    for (int i = 0; i < m_ControllerInfos.Length; ++i) {
      m_ControllerInfos[i].Transform.gameObject.SetActive(bEnable);
    }
  }

  public bool WandOnRight {
    get {
      return m_WandOnRight;
    }

    set {
      if (m_WandOnRight == value) { return; }

      // ControllerInfo has an immutable reference to ControllerBehavior and vice versa.
      // ControllerBehavior has a mutable reference to ControllerGeometry and vice versa.
      // ControllerGeometry is immutably associated with left or right (even in cases where
      // the geometry for each hand is identical).
      // Each ControllerInfo is also associated with the left or right hand, since Info is
      // responsible for extracting poses from the platform VR API.
      //
      // After hand-swapping, these things must be true:
      // - The Geometry's pose remains the same
      // - The Geometry is associated with a different Behavior (ie, GeometryLeftTouch
      //   switches from WandBehavior to BrushBehavior)
      // - The Info's handedness matches Info.Behavior.Geometry's handedness.
      
      bool canSwap = App.VrSdk.TrySwapLeftRightTracking();
      if (!canSwap) {
        Debug.LogWarning("VR SDK failed to swap controllers");
        return;
      }

      m_WandOnRight = value;
      PlayerPrefs.SetInt(PLAYER_PREF_WAND_ON_RIGHT, WandOnRight ? 1 : 0);

      var vrControllers = App.VrSdk.VrControls;
      BaseControllerBehavior.SwapBehaviors(vrControllers.Wand, vrControllers.Brush);

      if (OnSwapControllers != null) {
        OnSwapControllers();
      }
    }
  }

  public void EnablePoseTracking(bool enabled) {
    UnityEngine.XR.XRDevice.DisableAutoXRCameraTracking(App.VrSdk.GetVrCamera(), !enabled);
    if (enabled) {
      App.VrSdk.RestorePoseTracking();
    } else {
      App.VrSdk.DisablePoseTracking();
    }
    App.VrSdk.VrControls.EnablePoseTracking(enabled);
    UnityEngine.XR.InputTracking.disablePositionalTracking = !enabled;
  }

  void Awake() {
    m_Instance = this;

    // Instantiate so we can mutate without modifying a global asset
    // (the assumption is that m_HmdInfo never changes)
    if (m_HmdInfo.m_Renderer) {
      m_HmdInfo.m_Renderer.sharedMaterial =
          Instantiate(m_HmdInfo.m_Renderer.sharedMaterial);
    }
  }

  void OnEnable() {
    CreateControllerInfos();
    ShowControllers(false);
  }

  void CreateControllerInfos() {
    VrControllers vrControllers = App.VrSdk.VrControls;
    if (vrControllers != null) {
      BaseControllerBehavior[] behaviors = vrControllers.GetBehaviors();
      if (behaviors.Length != (int)ControllerName.Num) {
        Debug.LogErrorFormat("Expected {0} controllers, have {1}",
                             (int)ControllerName.Num,
                             behaviors.Length);
      }
      m_ControllerInfos = new ControllerInfo[behaviors.Length];

      for (int i = 0; i < m_ControllerInfos.Length; ++i) {
        bool isLeft = i == 0;
        m_ControllerInfos[i] = App.VrSdk.CreateControllerInfo(behaviors[i], isLeft);
      }
    }
  }

  bool SetSteamControllerStyle(SteamControllerInfo steamInfo, out string style) {
    SteamVR steamVR = SteamVR.instance;
    style = steamVR.GetStringProperty(ETrackedDeviceProperty.Prop_ControllerType_String,
        (uint)steamInfo.TrackedPose.GetDeviceIndex());
    if (style == "oculus_touch") {
      App.VrSdk.SetControllerStyle(ControllerStyle.OculusTouch);
    } else if (style == "knuckles") {
      App.VrSdk.SetControllerStyle(ControllerStyle.Knuckles);
    } else if (style == "vive_controller" || style == "vive_pro") {
      App.VrSdk.SetControllerStyle(ControllerStyle.Vive);
    } else if (style == "vive_cosmos_controller") {
      App.VrSdk.SetControllerStyle(ControllerStyle.OculusTouch);
    } else if (style == "wmr") {
      App.VrSdk.SetControllerStyle(ControllerStyle.Wmr);
    } else {
      // Not recognized.  This is not necessarily bad.
      return false;
    }
    return true;
  }

  void Start() {
    App.VrSdk.NewControllerPosesApplied += OnControllerPosesApplied;
    // If we're initializing SteamVR, defer this call until our controller type is determined.
    if (! App.VrSdk.IsInitializingSteamVr) {
      WandOnRight = (PlayerPrefs.GetInt(PLAYER_PREF_WAND_ON_RIGHT, 0) != 0);
    }
  }

  public void ShowControllers(bool show) {
    for (int i = 0; i < m_ControllerInfos.Length; ++i) {
      m_ControllerInfos[i].ShowController(m_ControllerInfos[i].IsTrackedObjectValid && show);
    }
  }

  void OnDestroy() {
    App.VrSdk.NewControllerPosesApplied -= OnControllerPosesApplied;
  }

  void Update() {
    // If we're initializing our controllers, continue to look for them.
    if (App.VrSdk.IsInitializingSteamVr) {
      if (m_ControllerInfos[0].IsTrackedObjectValid && m_ControllerInfos[1].IsTrackedObjectValid) {
        SteamVR steamVR = SteamVR.instance;

        // Determine controllers from the 0 controller.  If that isn't recognized,
        // try the 1 controller.  If *that* isn't recognized, default to Vive.
        string controllerStyle0 = "";
        SteamControllerInfo steamInfo0 = m_ControllerInfos[0] as SteamControllerInfo;
        if (!SetSteamControllerStyle(steamInfo0, out controllerStyle0)) {
          string controllerStyle1 = "";
          SteamControllerInfo steamInfo1 = m_ControllerInfos[1] as SteamControllerInfo;
          if (!SetSteamControllerStyle(steamInfo1, out controllerStyle1)) {
            Debug.LogWarningFormat(
                "Controller styles {0} and {1} not recognized.  Defaulting to Vive.",
                controllerStyle0, controllerStyle1);
            App.VrSdk.SetControllerStyle(ControllerStyle.Vive);
          }
        }

        // Null out controller infos to start fresh.
        for (int i = 0; i < m_ControllerInfos.Length; ++i) {
          m_ControllerInfos[i] = null;
        }

        // Create new one controller infos.
        CreateControllerInfos();

        // Swap geometry if any of our controllers is a logipen.
        bool foundLogipen = false;
        for (int i = 0; i < m_ControllerInfos.Length; ++i) {
          SteamControllerInfo info = m_ControllerInfos[i] as SteamControllerInfo;
          DetectLogitechVrPen pen = info.Behavior.GetComponent<DetectLogitechVrPen>();
          if (pen != null) {
            pen.Initialize(info.TrackedPose.GetDeviceIndex());
            foundLogipen = foundLogipen || pen.IsPen;
          }
        }

        // Initialize handedness.
        // The logitech pen stomps handedness because it is a handed controller, so don't
        // respect this if we've got a pen.
        if (!foundLogipen) {
          WandOnRight = (PlayerPrefs.GetInt(PLAYER_PREF_WAND_ON_RIGHT, 0) != 0);
        }

        // Refresh pointer angle and rendering.
        PointerManager.m_Instance.RefreshFreePaintPointerAngle();
        PointerManager.m_Instance.RequestPointerRendering(true);
      }
    } else {
      // Update controller infos.
      for (int i = 0; i < m_ControllerInfos.Length; ++i) {
        m_ControllerInfos[i].Update();
      }

      //cache touch inputs so we can control their usage
      m_Touch.m_Valid = (Input.touchCount > 0) && (Input.GetTouch(0).phase == TouchPhase.Began);
      if (m_Touch.m_Valid) {
        m_Touch.m_Pos = Input.GetTouch(0).position;
      }

      // Update touch locators.
      // Controller pad touch locator should be active if thumb is on the pad.
      // For the brush controller, tools can override this, unless we're in the intro tutorial.
      Brush.Behavior.SetTouchLocatorActive(Brush.GetPadTouch() &&
          (SketchSurfacePanel.m_Instance.ActiveTool.ShouldShowTouch() ||
          TutorialManager.m_Instance.TutorialActive()));
      Wand.Behavior.SetTouchLocatorActive(Wand.GetPadTouch());
      Wand.Behavior.SetTouchLocatorPosition(Wand.GetPadValue());
      Brush.Behavior.SetTouchLocatorPosition(Brush.GetPadValue());
    }
  }

  void LateUpdate() {
    // Late update controller infos.
    for (int i = 0; i < m_ControllerInfos.Length; ++i) {
      m_ControllerInfos[i].LateUpdate();
    }
  }

  void OnControllerPosesApplied() {
    for (int i = 0; i < Controllers.Length; ++i) {
      ControllerInfo info = Controllers[i];

      // Update velocity and acceleration.
      Vector3 currPosition = info.Transform.position;
      // TODO: should this take velocity straight from the controller?
      // Might be more accurate
      Vector3 currVelocity = (currPosition - info.m_Position) / Time.deltaTime;
      info.m_Acceleration =
          (currVelocity - info.m_Velocity) / Time.deltaTime;
      info.m_Velocity = currVelocity;
      info.m_Position = currPosition;
      if (info.m_WasTracked != info.IsTrackedObjectValid) {
        info.ShowController(info.IsTrackedObjectValid && App.Instance.ShowControllers);
      }
      info.m_WasTracked = info.IsTrackedObjectValid;
    }

    if (ControllerPosesApplied != null) {
      ControllerPosesApplied();
    }
  }

  public bool GetKeyboardShortcut(KeyboardShortcut shortcut) {
    KeyCode[] codes;
    if (!ActiveKeyMap.TryGetValue((int)shortcut, out codes)) {
      return false;
    }
    for (int i = 0; i < codes.Length; ++i) {
      KeyCode code = codes[i];
      if (Input.GetKey(code)) {
        return true;
      }
    }
    return false;
  }

  public bool GetKeyboardShortcutDown(KeyboardShortcut shortcut) {
    KeyCode[] codes;
    if (!ActiveKeyMap.TryGetValue((int)shortcut, out codes)) {
      return false;
    }
    for (int i = 0; i < codes.Length; ++i) {
      KeyCode code = codes[i];
      if (Input.GetKeyDown(code)) {
        return true;
      }
    }
    return false;
  }

  public bool GetAnyShift() {
    return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
  }

  private KeyboardShortcut? MapCommandToKeyboard(SketchCommands rCommand) {
    // We could cache this into a map, if needed.
    KeyboardShortcut? value;
    if (!m_SketchToKeyboardCommandMap.TryGetValue((int)rCommand, out value)) {
      try {
        value = (KeyboardShortcut)Enum.Parse(typeof(KeyboardShortcut),
                                             rCommand.ToString(),
                                             ignoreCase: false);
      } catch (ArgumentException) {
        value = null;
      }
      m_SketchToKeyboardCommandMap.Add((int)rCommand, value);
    }
    return value;
  }

  public bool GetCommand(SketchCommands rCommand) {
    // Here you can limit the given command to a specific scope, e.g. only allowing it on the Wand
    // or Brush, but the the controller info is responsible for how that command is mapped to the
    // hardware.

    KeyboardShortcut? shortcut = MapCommandToKeyboard(rCommand);
    bool isDemoMode = DemoManager.m_Instance.DemoModeEnabled;

    switch (rCommand) {
    case SketchCommands.Activate:
      return Brush.GetCommand(rCommand) || (!isDemoMode && GetMouseButton(0));
    case SketchCommands.AltActivate:
      return GetMouseButton(1) || Wand.GetCommand(rCommand);
    case SketchCommands.LockToHead:
      return GetKeyboardShortcut(shortcut.Value);
    case SketchCommands.PivotRotation:
      return GetKeyboardShortcut(shortcut.Value);
    case SketchCommands.WandRotation:
      return Wand.GetCommand(rCommand);
    case SketchCommands.LockToController:
      return Wand.GetCommand(rCommand) || Brush.GetCommand(rCommand);
    case SketchCommands.Scale:
      return GetKeyboardShortcut(shortcut.Value) || Brush.GetCommand(rCommand);
    case SketchCommands.Sensitivity:
      return Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > m_InputThreshold;
    case SketchCommands.Panic:
      return GetMouseButton(1) || Wand.GetCommand(rCommand);
    case SketchCommands.MultiCamSelection:
      return Brush.GetCommand(rCommand);
    case SketchCommands.ShowPinCushion:
      return Brush.GetCommand(rCommand);
    case SketchCommands.DuplicateSelection:
      return Brush.GetCommand(rCommand);
    case SketchCommands.Undo:
    case SketchCommands.Redo:
      return Wand.GetCommand(rCommand);
    }

    return false;
  }

  public bool GetCommandHeld(SketchCommands rCommand) {
    // Here you can limit the given command to a specific scope, e.g. only allowing it on the Wand
    // or Brush, but the the controller info is responsible for how that command is mapped to the
    // hardware.

    switch (rCommand) {
    case SketchCommands.Confirm:
    case SketchCommands.Cancel:
    case SketchCommands.Share:
    case SketchCommands.Trash:
      return Brush.GetCommandHeld(rCommand);
    case SketchCommands.DuplicateSelection:
      if (SketchControlsScript.m_Instance.OneHandGrabController != InputManager.ControllerName.None) {
        return Controllers[(int)SketchControlsScript.m_Instance.OneHandGrabController]
          .GetCommandHeld(rCommand);
      } else {
        return Brush.GetCommandHeld(rCommand);
      }
    }

    return false;
  }

  public bool GetCommandDown(SketchCommands rCommand) {
    // Here you can limit the given command to a specific scope, e.g. only allowing it on the Wand
    // or Brush, but the the controller info is responsible for how that command is mapped to the
    // hardware.

    KeyboardShortcut? shortcut = MapCommandToKeyboard(rCommand);

    switch (rCommand) {
    case SketchCommands.Activate:
        return GetMouseButtonDown(0) || Brush.GetCommandDown(rCommand);
    case SketchCommands.RewindTimeline:
    case SketchCommands.AdvanceTimeline:
    case SketchCommands.TimelineHome:
    case SketchCommands.TimelineEnd:
    case SketchCommands.Reset:
    case SketchCommands.Undo:
    case SketchCommands.Redo:
      return GetKeyboardShortcutDown(shortcut.Value) || Wand.GetCommandDown(rCommand);

    case SketchCommands.DuplicateSelection:
      return (SketchControlsScript.m_Instance.OneHandGrabController != ControllerName.None &&
        Controllers[(int)SketchControlsScript.m_Instance.OneHandGrabController]
          .GetCommandDown(rCommand)) ||
        Brush.GetCommandDown(rCommand);

    // Keyboard only:
    case SketchCommands.Delete:
    case SketchCommands.Abort:
      return GetKeyboardShortcutDown(shortcut.Value);

    // Brush only
    case SketchCommands.Teleport:
    case SketchCommands.ToggleDefaultTool:
    case SketchCommands.MenuContextClick:
    case SketchCommands.ToggleSelection:
      return Brush.GetCommandDown(rCommand);

    // Misc
    case SketchCommands.SwapControls:
      return HasSwapGestureCompleted();
    case SketchCommands.AltActivate:
      return GetMouseButtonDown(1);
    }

    return false;
  }

  private bool HasSwapGestureCompleted() {
    TrTransform base1 = TrTransform.FromTransform(Brush.Geometry.BaseAttachPoint);
    TrTransform base2 = TrTransform.FromTransform(Wand.Geometry.BaseAttachPoint);
    float meters = Vector3.Distance(base1.translation, base2.translation) * App.UNITS_TO_METERS;

    if (m_InhibitControllerSwap) {
      if (meters > kSwapResetDistMeters) {
        m_InhibitControllerSwap = false;
      }
      return false;
    } else {
      // Controllers should only swap when there is no active input.
      bool bActiveInput = GetCommand(SketchCommands.Activate) &&
          App.Instance.IsInStateThatAllowsPainting();
      bool forwardsOpposed = Vector3.Angle(base1.forward, base2.forward) > kSwapForwardAngle;
      bool velocitiesOpposed =
          Vector3.Angle(Wand.m_Velocity, Brush.m_Velocity) > kSwapVelocityAngle;
      bool minControllerAccelerationReached =
          (Brush.m_Acceleration.magnitude > kSwapAcceleration ||
           Wand.m_Acceleration.magnitude > kSwapAcceleration);
      bool closeEnough = (meters < kSwapDistMeters);
      bool shouldSwap = !bActiveInput
                        && forwardsOpposed && velocitiesOpposed
                        && minControllerAccelerationReached
                        && closeEnough;
      if (shouldSwap) {
        m_InhibitControllerSwap = true;
        var fxPos = Vector3.Lerp(base1.translation, base2.translation, 0.5f);
        Instantiate(m_SwapEffect, fxPos, Quaternion.identity);
        PointerManager.m_Instance.DisablePointerPreviewLine();
      }
      return shouldSwap;
    }
  }

  public bool ControllersAreSwapping() {
    return m_InhibitControllerSwap;
  }

  public Vector2 GetMouseMoveDelta() {
    Vector2 mv = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    return new Vector2(Mathf.Abs(mv.x) > m_InputThreshold ? mv.x : 0f,
                       Mathf.Abs(mv.y) > m_InputThreshold ? mv.y : 0f);
  }

  public float GetMouseWheel() {
    return Input.GetAxis("Mouse ScrollWheel");
  }

  /// Mouse input is ignored on mobile platform because the Oculus Quest seems to emulate mouse
  /// presses when you fiddle with the joystick.
  public bool GetMouseButton(int button) {
    return !App.Config.IsMobileHardware && Input.GetMouseButton(button);
  }

  /// Mouse input is ignored on mobile platform because the Oculus Quest seems to emulate mouse
  /// presses when you fiddle with the joystick.
  public bool GetMouseButtonDown(int button) {
    return !App.Config.IsMobileHardware && Input.GetMouseButtonDown(button);
  }

  public bool IsBrushScrollActive() {
    return Brush.GetPadTouch() || Brush.GetThumbStickTouch();
  }

  public float GetBrushScrollAmount() {
    // Check mouse first.
    if (!App.Config.IsMobileHardware) {
      float fMouse = Input.GetAxis("Mouse X");
      if (Mathf.Abs(fMouse) > m_InputThreshold) {
        return fMouse;
      }
    }

    // Check controller scroll direction.
    if (App.VrSdk.VrControls.PrimaryScrollDirectionIsX(ControllerName.Brush)) {
      return Brush.GetScrollXDelta();
    }
    return Brush.GetScrollYDelta(); 
  }

  // Scroll amount is adjusted if the analog input for this controller is a stick.
  public float GetAdjustedBrushScrollAmount() {
    return GetBrushScrollAmount() * App.VrSdk.SwipeScaleAdjustment(ControllerName.Brush);
  }

  public float GetWandScrollAmount() {
    // Check controller scroll direction.
    if (App.VrSdk.VrControls.PrimaryScrollDirectionIsX(ControllerName.Wand)) {
      return Wand.GetScrollXDelta();
    }
    return Wand.GetScrollYDelta();
  }

  // Scroll amount is adjusted if the analog input for this controller is a stick.
  public float GetAdjustedWandScrollAmount() {
    return GetWandScrollAmount() * App.VrSdk.SwipeScaleAdjustment(ControllerName.Wand);
  }

  public float GetToolSelection() {
    float fScrollWheel = Input.GetAxis("Mouse ScrollWheel");
    if (Mathf.Abs(fScrollWheel) > m_InputThreshold) {
      return fScrollWheel;
    }

    return Wand.GetScrollYDelta();
  }

  public bool GetTouchPosition(out Vector2 touchPos) {
    touchPos = m_Touch.m_Pos;
    return m_Touch.m_Valid;
  }

  public void ClearTouchPosition() {
    m_Touch.m_Valid = false;
  }

  public void TriggerHaptics(ControllerName eName, float durationInSeconds) {
    m_ControllerInfos[(int)eName].TriggerControllerHaptics(durationInSeconds);
  }

  public void TriggerHaptics(ControllerName eName, float durationInSeconds,
      float minTimeBetweenPulses) {
    int iMappedIndex = (int)eName;
    if (m_ControllerInfos[iMappedIndex].m_TimeSinceHapticTrigger > minTimeBetweenPulses) {
      m_ControllerInfos[iMappedIndex].TriggerControllerHaptics(durationInSeconds);
      m_ControllerInfos[iMappedIndex].m_TimeSinceHapticTrigger = 0.0f;
    }
  }

  public void TriggerHapticsPulse(ControllerName eName, int iNumPulses, float fInterval,
      float durationInSeconds) {
    int iIndex = (int)eName;
    m_ControllerInfos[iIndex].m_HapticPulseCount = iNumPulses;
    m_ControllerInfos[iIndex].m_HapticInterval = fInterval;
    m_ControllerInfos[iIndex].m_HapticPulseLength = durationInSeconds;
  }

  public BaseControllerBehavior GetControllerBehavior(ControllerName eName) {
    return m_ControllerInfos[(int)eName].Behavior;
  }

  public ControllerTutorialScript GetControllerTutorial(ControllerName eName) {
    return m_ControllerInfos[(int)eName].Tutorial;
  }

  public void TintControllersAndHMD(Color rTintColor, float fBaseIntensity, float fGlowIntensity) {
    if (!App.Instance.ShouldTintControllers()) { return; }
    for (int i = 0; i < (int)ControllerName.Num; ++i) {
      m_ControllerInfos[i].Behavior.SetTint(rTintColor, fBaseIntensity, fGlowIntensity);
    }

    if (m_HmdInfo.m_Renderer != null) {
      Color rTintedColor = rTintColor * (fBaseIntensity + fGlowIntensity);
      m_HmdInfo.m_Renderer.sharedMaterial.SetColor("_EmissionColor", rTintedColor);
    }

    SketchControlsScript.m_Instance.m_GrabHighlightActiveColor = rTintColor;
    Shader.SetGlobalColor("_GrabHighlightActiveColor", rTintColor);
  }

  public Transform GetPinCushionSpawn() {
    int iMappedIndex = (int)ControllerName.Brush;
    BaseControllerBehavior behavior = m_ControllerInfos[iMappedIndex].Behavior;
    if (behavior) {
      return behavior.PinCushionSpawn;
    }
    return null;
  }

  public Transform GetBrushControllerAttachPoint() {
    int iMappedIndex = (int)ControllerName.Brush;
    BaseControllerBehavior behavior = m_ControllerInfos[iMappedIndex].Behavior;
    if (behavior) {
      return behavior.PointerAttachPoint;
    }
    return null;
  }

  public Transform GetWandControllerAttachPoint() {
    int iMappedIndex = (int)ControllerName.Wand;
    BaseControllerBehavior behavior = m_ControllerInfos[iMappedIndex].Behavior;
    if (behavior) {
      return behavior.PointerAttachPoint;
    }
    return null;
  }

  public void SetControllersAttachAngle(float fAngle) {
    // If either of our controllers is a logitech pen, zero out the attach angle.
    // It's not supported by the pen.
    if (App.VrSdk.VrControls.LogitechPenIsPresent()) {
      fAngle = 0.0f;
    }

    int iMappedBrushIndex = (int)ControllerName.Brush;
    BaseControllerBehavior rBrushScript = m_ControllerInfos[iMappedBrushIndex].Behavior;
    if (rBrushScript) {
      Vector3 vRotation = rBrushScript.PointerAttachAnchor.localRotation.eulerAngles;
      vRotation.x = fAngle;
      rBrushScript.PointerAttachAnchor.localRotation = Quaternion.Euler(vRotation);
      rBrushScript.ToolAttachAnchor.localRotation = Quaternion.Euler(vRotation);
    }

    int iMappedWandIndex = (int)ControllerName.Wand;
    BaseControllerBehavior rWandScript = m_ControllerInfos[iMappedWandIndex].Behavior;
    if (rWandScript) {
      Vector3 vRotation = rWandScript.PointerAttachAnchor.localRotation.eulerAngles;
      vRotation.x = fAngle;
      rWandScript.PointerAttachAnchor.localRotation = Quaternion.Euler(vRotation);
      rWandScript.ToolAttachAnchor.localRotation = Quaternion.Euler(vRotation);
    }
  }

  public Transform GetController(ControllerName eName) {
    return m_ControllerInfos[(int)eName].Transform;
  }

  public static ControllerGeometry GetControllerGeometry(ControllerName eName) {
    return m_Instance.m_ControllerInfos[(int)eName].Geometry;
  }

  public Quaternion GetControllerRotation(ControllerName eName) {
    return m_ControllerInfos[(int)eName].Transform.rotation;
  }

  public Vector3 GetControllerPosition(ControllerName eName) {
    return m_ControllerInfos[(int)eName].Transform.position;
  }

  public Vector3 GetControllerAttachPointPosition(ControllerName eName) {
    if (eName == ControllerName.Brush) {
      return GetBrushControllerAttachPoint().position;
    } else if (eName == ControllerName.Wand) {
      return GetWandControllerAttachPoint().position;
    }
    return Vector3.zero;
  }

  public ControllerName GetDominantController(SketchCommands rCommand) {
    switch (rCommand) {
    case SketchCommands.Activate:
      for (int i = 0; i < (int)ControllerName.Num; ++i) {
        if (m_ControllerInfos[i].GetVrInput(VrInput.Trigger)) {
          return (ControllerName)i;
        }
      }
      break;
    case SketchCommands.LockToController:
      for (int i = 0; i < (int)ControllerName.Num; ++i) {
        var controller = (ControllerName)i;
        if (m_ControllerInfos[i].GetControllerGrip()) {
          return controller;
        }
      }
      break;
    }

    return ControllerName.Num;
  }
}

}  // namespace TiltBrush
