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
using System.Collections.Generic;
using System.Linq;
using Valve.VR;

#if !OCULUS_SUPPORTED
using OVROverlay = UnityEngine.MonoBehaviour;
#endif // !OCULUS_SUPPORTED

namespace TiltBrush {

// If these names are used in analytics etc, they must be protected from obfuscation.
// Do not change the names of any of them, unless they've never been released.
[Serializable]
public enum ControllerStyle {
  Unset,
  None,
  InitializingSteamVR,
  Vive,
  Knuckles,
  OculusTouch,
  Wmr,
  Gvr,
  LogitechPen,
  Cosmos,
}

//
// The VrSdk is an abstraction over the actual VR hardware and SDK. It is responsible for:
//
//   * Initializating the VR system, cameras, controllers and associated state.
//   * Providing hardware- and SDK-specific controls via a non-specific interface.
//   * Providing abstract access to events sent from the SDK.
//   * Exposing an interface to query Hardware and SDK capabilities.
//
// TODO: In its current form, the VrSdk is monolithic, though it should ultimately be
// broken out into hardware- and SDK-specific modules, which can be loaded and unloaded at startup
// or build time.
//
public class VrSdk : MonoBehaviour {
  [SerializeField] private float m_AnalogGripBinaryThreshold_Rift;
  [SerializeField] private SteamVR_Overlay m_SteamVROverlay;
  [SerializeField] private GvrOverlay m_GvrOverlayPrefab;
  [SerializeField] private float m_OverlayMaxAlpha = 1.0f;
  [SerializeField] private float m_OverlayMaxSize = 8;
  
  // VR  Data and Prefabs for specific VR systems
  [SerializeField] private GameObject m_VrSystem;
  [SerializeField] private GameObject m_SteamUninitializedControlsPrefab;
  [SerializeField] private GameObject m_SteamViveControlsPrefab;
  [SerializeField] private GameObject m_SteamRiftControlsPrefab;
  [SerializeField] private GameObject m_SteamQuestControlsPrefab;
  [SerializeField] private GameObject m_SteamWmrControlsPrefab;
  [SerializeField] private GameObject m_SteamKnucklesControlsPrefab;
  [SerializeField] private GameObject m_SteamCosmoControlsPrefab;
  // Prefab for the old-style Touch controllers, used only for Rift
  [SerializeField] private GameObject m_OculusRiftControlsPrefab;
  // Prefab for the new-style Touch controllers, used for Rift-S and Quest
  [SerializeField] private GameObject m_OculusQuestControlsPrefab;
  [SerializeField] private GameObject m_GvrPointerControlsPrefab;
  [SerializeField] private GameObject m_NonVrControlsPrefab;

  // This is the object "Camera (eye)"
  [SerializeField] private Camera m_VrCamera;

  // Runtime VR Spawned Controllers
  // This is the source of truth for controllers.  InputManager.m_ControllerInfos stores
  // links to some of these components, but may be out of date for a frame when
  // controllers change.
  private VrControllers m_VrControls;
  public VrControllers VrControls { get { return m_VrControls; } }

  private bool m_HasVrFocus = true;
  private OverlayMode m_OverlayMode;

  // Oculus Overlay
#if OCULUS_SUPPORTED
  private OVROverlay m_OVROverlay;
#endif // OCULUS_SUPPORTED

  // Mobile Overlay
  private bool m_MobileOverlayOn;
  private GvrOverlay m_MobileOverlay;

  private Bounds? m_RoomBoundsAabbCached;

  // Cached object to avoid interop overhead
  private Compositor_FrameTiming m_FrameTiming;

  private Action[] m_OldOnPoseApplied;

  private bool m_NeedsToAttachConsoleScript;
  private TrTransform? m_TrackingBackupXf;

  private enum OverlayMode {
    None,
    Steam,
    OVR,
    Mobile
  }

  // Degrees of Freedom.
  public enum DoF {
    None,
    Two,     // Mouse & Keyboard
    Six,     // Vive, Rift, etc
  }

  // -------------------------------------------------------------------------------------------- //
  // Public Events
  // -------------------------------------------------------------------------------------------- //

  // Called when new poses are ready.
  public event Action NewControllerPosesApplied;

  // -------------------------------------------------------------------------------------------- //
  // Public Controller Properties
  // -------------------------------------------------------------------------------------------- //

  public float AnalogGripBinaryThreshold_Rift {
    get { return m_AnalogGripBinaryThreshold_Rift; }
  }

  public bool OverlayIsOVR { get { return m_OverlayMode == OverlayMode.OVR; } }

  public bool IsInitializingSteamVr {
    get {
      return VrControls.Brush.ControllerGeometry.Style == ControllerStyle.InitializingSteamVR;
    }
  }

  // -------------------------------------------------------------------------------------------- //
  // Private Unity Component Events
  // -------------------------------------------------------------------------------------------- //

  void Awake() {
    if (App.Config.IsMobileHardware && m_GvrOverlayPrefab != null) {
      m_OverlayMode = OverlayMode.Mobile;
      m_MobileOverlay = Instantiate(m_GvrOverlayPrefab);
      m_MobileOverlay.gameObject.SetActive(false);
    } else if (App.Config.m_SdkMode == SdkMode.SteamVR && m_SteamVROverlay != null) {
      m_OverlayMode = OverlayMode.Steam;
    }
#if OCULUS_SUPPORTED
    else if (App.Config.m_SdkMode == SdkMode.Oculus) {
      m_OverlayMode = OverlayMode.OVR;
      var gobj = new GameObject("Oculus Overlay");
      gobj.transform.SetParent(m_VrSystem.transform, worldPositionStays: false);
      m_OVROverlay = gobj.AddComponent<OVROverlay>();
      m_OVROverlay.isDynamic = true;
      m_OVROverlay.compositionDepth = 0;
      m_OVROverlay.currentOverlayType = OVROverlay.OverlayType.Overlay;
      m_OVROverlay.currentOverlayShape = OVROverlay.OverlayShape.Quad;
      m_OVROverlay.noDepthBufferTesting = true;
      m_OVROverlay.enabled = false;
    }
#endif // OCULUS_SUPPORTED

    if (App.Config.m_SdkMode == SdkMode.Oculus) {
#if OCULUS_SUPPORTED
      // ---------------------------------------------------------------------------------------- //
      // OculusVR
      // ---------------------------------------------------------------------------------------- //
      OVRManager manager = gameObject.AddComponent<OVRManager>();
      manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
      manager.useRecommendedMSAALevel = false;

      SetControllerStyle(TiltBrush.ControllerStyle.OculusTouch);
      // adding components to the VR Camera needed for fading view and getting controller poses.
      m_VrCamera.gameObject.AddComponent<OculusCameraFade>();
      m_VrCamera.gameObject.AddComponent<OculusPreCullHook>();
#endif // OCULUS_SUPPORTED
    } else if (App.Config.m_SdkMode == SdkMode.SteamVR) {
      // ---------------------------------------------------------------------------------------- //
      // SteamVR
      // ---------------------------------------------------------------------------------------- //
      // SteamVR_Render needs to be instantiated from our version of the prefab before any other
      // SteamVR objects are instantiated because otherwise, those other objects will instantiate
      // their own version of SteamVR_Render, which won't have the same connections as our prefab.
      // Ideally, this instantiation would occur in a place that is guaranteed to happen first but
      // since we don't have an appropriate place for that now, it's being placed right before the
      // first call that would otherwise instantiate it.
      Instantiate(App.Config.m_SteamVrRenderPrefab);
      if (App.Config.VrHardware == VrHardware.Rift) {
        SetControllerStyle(TiltBrush.ControllerStyle.OculusTouch);
      } else if (App.Config.VrHardware == VrHardware.Wmr) {
        SetControllerStyle(TiltBrush.ControllerStyle.Wmr);
      } else {
        SetControllerStyle(TiltBrush.ControllerStyle.InitializingSteamVR);
      }
      m_VrCamera.gameObject.AddComponent<SteamVR_Camera>();
    } else if (App.Config.m_SdkMode == SdkMode.Gvr) {
      // ---------------------------------------------------------------------------------------- //
      // GoogleVR
      // ---------------------------------------------------------------------------------------- //
      SetControllerStyle(TiltBrush.ControllerStyle.Gvr);
      // Custom controls parenting for GVR.
      m_VrControls.transform.parent = null;

      // TODO: Why is this offset needed? This should also be in a prefab, not here.
      var pos = m_VrSystem.gameObject.transform.localPosition;
      pos.y += 15f;
      m_VrSystem.gameObject.transform.localPosition = pos;

      pos = m_VrControls.gameObject.transform.localPosition;
      pos.y += 15f;
      m_VrControls.gameObject.transform.localPosition = pos;

#if UNITY_EDITOR && false
      // Instant preview
      m_VrCamera.gameObject.AddComponent<InstantPreviewHelper>();
      var ip = m_VrCamera.gameObject.AddComponent<Gvr.Internal.InstantPreview>();
      ip.OutputResolution = Gvr.Internal.InstantPreview.Resolutions.Big;
      ip.MultisampleCount = Gvr.Internal.InstantPreview.MultisampleCounts.One;
      ip.BitRate = Gvr.Internal.InstantPreview.BitRates._16000;
#endif

      // Custom controls parenting for GVR.
      m_VrControls.transform.parent = m_VrCamera.transform.parent;
    } else if (App.Config.m_SdkMode == SdkMode.Monoscopic) {
      // ---------------------------------------------------------------------------------------- //
      // Monoscopic
      // ---------------------------------------------------------------------------------------- //
      m_VrCamera.gameObject.AddComponent<MonoCameraControlScript>();
      SetControllerStyle(TiltBrush.ControllerStyle.None);
      // Offset for head position, since camera height is set by the VR system.
      m_VrCamera.transform.localPosition = new Vector3(0f, 1.5f, 0f);
    } else {
      // ---------------------------------------------------------------------------------------- //
      // Non-VR
      // ---------------------------------------------------------------------------------------- //
      SetControllerStyle(TiltBrush.ControllerStyle.None);
      // Offset for head position, since camera height is set by the VR system.
      m_VrCamera.transform.localPosition = new Vector3(0f, 1.5f, 0f);
    }
    m_VrCamera.gameObject.SetActive(true);
    m_VrSystem.SetActive(m_VrCamera.gameObject.activeSelf);
  }

  void Start() {
    if (App.Config.m_SdkMode == SdkMode.SteamVR) {
      if (SteamVR.instance != null) {
        SteamVR_Events.InputFocus.Listen(OnInputFocusSteam);
        SteamVR_Events.NewPosesApplied.Listen(OnNewPoses);
      }
      m_FrameTiming = new Compositor_FrameTiming {
        m_nSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(
          typeof(Compositor_FrameTiming))
      };
    } else if (App.Config.m_SdkMode == SdkMode.Oculus) {
#if OCULUS_SUPPORTED
      OculusHandTrackingManager.NewPosesApplied += OnNewPoses;
      // We shouldn't call this frequently, hence the local cache and callbacks.
      OVRManager.VrFocusAcquired += () => { OnInputFocus(true); };
      OVRManager.VrFocusLost += () => { OnInputFocus(false); };
#endif // OCULUS_SUPPORTED
    } else if (App.Config.m_SdkMode == SdkMode.Gvr) {
      var brushGeom = InputManager.Brush.Geometry;
      GvrControllerInput.OnPostControllerInputUpdated += OnNewPoses;
    }

    if (m_NeedsToAttachConsoleScript && m_VrControls != null) {
      ControllerConsoleScript.m_Instance.AttachToController(
          m_VrControls.Brush);
      m_NeedsToAttachConsoleScript = false;
    }
  }

  void OnDestroy() {
    if (App.Config.m_SdkMode == SdkMode.SteamVR) {
      SteamVR_Events.InputFocus.Remove(OnInputFocusSteam);
      SteamVR_Events.NewPosesApplied.Remove(OnNewPoses);
    } else if (App.Config.m_SdkMode == SdkMode.Oculus) {
      OculusHandTrackingManager.NewPosesApplied -= OnNewPoses;
    }
  }

  // -------------------------------------------------------------------------------------------- //
  // Private VR SDK-Related Events
  // -------------------------------------------------------------------------------------------- //

  private void OnInputFocus(params object[] args) {
    InputManager.m_Instance.AllowVrControllers = (bool)args[0];
    m_HasVrFocus = (bool)args[0];
  }

  private void OnNewPoses() {
    if (NewControllerPosesApplied != null) {
      NewControllerPosesApplied();
    }
  }

  private void OnInputFocusSteam(bool arg) {
    OnInputFocus(arg);
  }

  // -------------------------------------------------------------------------------------------- //
  // Camera Methods
  // -------------------------------------------------------------------------------------------- //

  /// Returns a camera actually used for rendering. The associated transform
  /// may not be the transform of the head -- camera may have an eye offset.
  /// TODO: revisit callers and see if anything should be using GetHeadTransform() instead.
  ///
  /// XXX: Why do we have this instead of Camera.main? Also, due to our current setup,
  /// Camera.main is currently broken in monoscope mode (and maybe oculus?) due ot the fact that the
  /// camera is not tagged as "MainCamera".
  public Camera GetVrCamera() {
    return m_VrCamera;
  }

  public void SetScreenMirroring(bool enabled) {
    if (App.Config.m_SdkMode == SdkMode.SteamVR) {
      // Get the camera mask if this is the first use of mirroring
      if (enabled) {
        Screen.SetResolution(1920, 1080, false);
        SetHmdScalingFactor(1.875f);
      } else {
        Screen.SetResolution(1024, 768, false);
        SetHmdScalingFactor(1.0f);
      }
    }
  }

  // -------------------------------------------------------------------------------------------- //
  // Profiling / VR Utility Methods
  // -------------------------------------------------------------------------------------------- //

  // Returns a string representing the user's hardware and SDK configuration.
  public string GetDisplayIdentifier() {
    return string.Format("{0}; {1}", App.Config.m_SdkMode, App.Config.VrHardware);
  }

  // Returns the time of the most recent number of dropped frames, null on failure.
  public int? GetDroppedFrames() {
    if (App.Config.m_SdkMode == SdkMode.SteamVR) {
      SteamVR vr = SteamVR.instance;
      if (vr != null) {
        if (vr.compositor.GetFrameTiming(ref m_FrameTiming, 0 /* most recent frame */)) {
          return (int)m_FrameTiming.m_nNumDroppedFrames;
        }
      }
    } else if (App.Config.m_SdkMode == SdkMode.Oculus) {
#if OCULUS_SUPPORTED
      OVRPlugin.AppPerfStats perfStats = OVRPlugin.GetAppPerfStats();
      if (perfStats.FrameStatsCount > 0) {
        return perfStats.FrameStats[0].AppDroppedFrameCount;
      }
      return 0;
#endif // OCULUS_SUPPORTED
    }

    return null;
  }

  public void ResetPerfStats() {
    if (App.Config.m_SdkMode == SdkMode.Oculus) {
#if OCULUS_SUPPORTED
      OVRPlugin.ResetAppPerfStats();
#endif // OCULUS_SUPPORTED
    }
  }

  // -------------------------------------------------------------------------------------------- //
  // Room Bounds / Chaperone Methods
  // -------------------------------------------------------------------------------------------- //

  // Returns true if GetRoomBounds() will return a non-zero volume.
  public bool HasRoomBounds() {
    return GetRoomBoundsAabb().extents != Vector3.zero;
  }

  // Returns the extents of the room bounds, which is the half-vector of the axis aligned bounds.
  // This value is returned in Tilt Brush room coordinates.
  // Extents are non-negative.
  public Vector3 GetRoomExtents() {
    return GetRoomBoundsAabb().extents;
  }

  /// Returns room bounds as an AABB in Tilt Brush room coordinates.
  public Bounds GetRoomBoundsAabb() {
    if (m_RoomBoundsAabbCached == null) {
      RefreshRoomBoundsCache();
    }
    return m_RoomBoundsAabbCached.Value;
  }

  // re-calculate m_RoomBoundsPointsCached and m_RoomBoundsAabbCached
  private void RefreshRoomBoundsCache() {
    Vector3[] points_RS = null;

    if (App.Config.m_SdkMode == SdkMode.Oculus) {
#if OCULUS_SUPPORTED
      // N points, clockwise winding (but axis is undocumented), undocumented convexity
      // In practice, it's clockwise looking along Y-
      points_RS = OVRManager.boundary.GetGeometry(OVRBoundary.BoundaryType.OuterBoundary)
          .Select(v => UnityFromOculus(v)).ToArray();
#endif // OCULUS_SUPPORTED
    } else if (App.Config.m_SdkMode == SdkMode.SteamVR) {
      var chaperone = OpenVR.Chaperone;
      if (chaperone != null) {
        HmdQuad_t rect = new HmdQuad_t();
        // 4 points, undocumented winding, undocumented convexity
        // Undocumented if it's an AABB
        // In practice, seems to always be an axis-aligned clockwise box.
        chaperone.GetPlayAreaRect(ref rect);
        var steamPoints = new[] {
          rect.vCorners0, rect.vCorners1, rect.vCorners2, rect.vCorners3
        };
        points_RS = steamPoints.Select(v => UnityFromSteamVr(v)).ToArray();
      }
    }

    if (points_RS == null) {
      points_RS = new Vector3[0];
    }

    // We could use points_RS to expose a raw-points-based API, and currently
    // we can offer the guarantee that the points are clockwise (looking along Y-),
    // and convex. So far, nobody needs it.
    // Debug.Assert(IsClockwiseConvex(points_RS));
    // m_RoomBoundsPointsCached = points_RS.

    m_RoomBoundsAabbCached = FromPoints(points_RS);
  }

  /// If points is empty, returns the default (empty) Bounds
  static private Bounds FromPoints(IEnumerable<Vector3> points) {
    using (var e = points.GetEnumerator()) {
      if (!e.MoveNext()) {
        return new Bounds();
      }
      Bounds bounds = new Bounds(e.Current, Vector3.zero);
      while (e.MoveNext()) {
        bounds.Encapsulate(e.Current);
      }
      return bounds;
    }
  }

  static private bool IsClockwiseConvex(Vector3[] points) {
    for (int i = 0; i < points.Length; ++i) {
      Vector3 a = points[i];
      Vector3 b = points[(i+1) % points.Length];
      Vector3 c = points[(i+2) % points.Length];
      Vector3 ab = b - a;
      Vector3 bc = c - b;
      if (Vector3.Dot(Vector3.Cross(ab, bc), Vector3.up) < 0) {
        return false;
      }
    }
    return true;
  }

  /// Converts from SteamVR axis conventions and units to Unity
  static private Vector3 UnityFromSteamVr(HmdVector3_t v) {
    return new Vector3(v.v0, v.v1, v.v2) * App.METERS_TO_UNITS;
  }

  /// Converts from Oculus axis conventions and units to Unity
  static private Vector3 UnityFromOculus(Vector3 v) {
    return v * App.METERS_TO_UNITS;
  }

  // -------------------------------------------------------------------------------------------- //
  // Controller Methods
  // -------------------------------------------------------------------------------------------- //
  // A scaling factor for when adjusting the brush size.
  // The thumbstick 0..1 value moves too fast.
  public float SwipeScaleAdjustment(InputManager.ControllerName name) {
    return AnalogIsStick(name) ? 0.025f : 1.0f;
  }

  public bool AnalogIsStick(InputManager.ControllerName name) {
    var style = VrControls.GetBehavior(name).ControllerGeometry.Style;
    return style == ControllerStyle.Wmr ||
           style == ControllerStyle.OculusTouch ||
           style == ControllerStyle.Knuckles ||
           style == ControllerStyle.Cosmos;
  }

  // Destroy and recreate the ControllerBehavior and ControllerGeometry objects.
  // This is mostly useful if you want different geometry.
  //
  // TODO: this will always give the wand left-hand geometry and the brush right-hand geometry,
  // so InputManager.WandOnRight should probably be reset to false after this? Or maybe
  // SetControllerStyle should be smart enough to figure that out.
  public void SetControllerStyle(ControllerStyle style) {
    // Clear console parent in case we're switching controllers.
    if (ControllerConsoleScript.m_Instance != null) {
      ControllerConsoleScript.m_Instance.transform.parent = null;
    }

    // Clean up existing controllers.
    // Note that we are explicitly not transferring state.  This is because, in practice,
    // we only change controller style when we're initializing SteamVR, and the temporary
    // controllers are largely disabled.  Any bugs that occur will be trivial and cosmetic.
    // If we add the ability to dynamically change controllers or my above comment about
    // trivial bugs is not true, state transfer should occur here.
    //
    // In practice, the only style transitions we should see are:
    // - None -> correct style                   During VrSdk.Awake()
    // - None -> InitializingSteamVr             During VrSdk.Awake()
    //   InitializingSteamVr -> correct style    Many frames after VrSdk.Awake()
    if (m_VrControls != null) {
      Destroy(m_VrControls.gameObject);
      m_VrControls = null;
    }

    m_NeedsToAttachConsoleScript = true;

    GameObject controlsPrefab;
    switch (style) {
    case ControllerStyle.Vive:
      controlsPrefab = m_SteamViveControlsPrefab;
      break;
    case ControllerStyle.Knuckles:
      controlsPrefab = m_SteamKnucklesControlsPrefab;
      break;
    case ControllerStyle.Cosmos:
      controlsPrefab = m_SteamCosmoControlsPrefab;
      break;
    case ControllerStyle.OculusTouch: {
      // This will probably not work once new headsets are released.
      // Maybe something like this instead?
      //   isQuest = (UnityEngine.XR.XRDevice.model != "Oculus Rift CV1");
      bool isQuestController = (UnityEngine.XR.XRDevice.refreshRate < 81f) ||
          (App.Config.VrHardware == VrHardware.Quest);
      if (App.Config.m_SdkMode == SdkMode.Oculus) {
        controlsPrefab = isQuestController ? m_OculusQuestControlsPrefab : m_OculusRiftControlsPrefab;
      } else /* Assume SteamVR */ {
        controlsPrefab = isQuestController ? m_SteamQuestControlsPrefab : m_SteamRiftControlsPrefab;
      }
      break;
    }
    case ControllerStyle.Wmr:
      controlsPrefab = m_SteamWmrControlsPrefab;
      break;
    case ControllerStyle.Gvr:
      controlsPrefab = m_GvrPointerControlsPrefab;
      break;
    case ControllerStyle.None:
      controlsPrefab = m_NonVrControlsPrefab;
      m_NeedsToAttachConsoleScript = false;
      break;
    case ControllerStyle.InitializingSteamVR:
      controlsPrefab = m_SteamUninitializedControlsPrefab;
      m_NeedsToAttachConsoleScript = false;
      break;
    case ControllerStyle.Unset:
    default:
      controlsPrefab = null;
      m_NeedsToAttachConsoleScript = false;
      break;
    }

#if UNITY_EDITOR
    // This is _just_ robust enough to be able to switch between the Rift and Touch
    // controllers. To force (for example) a Wmr controller when using a Touch will
    // probably require being able to specify an override style as well, because TB
    // might act funny if we spawn a Wmr prefab with style OculusTouch.
    // Additionally, the Logitech Pen override happens after this, so there's no way
    // to override it.

    // Wait for the "real" SetControllerStyle to come through.
    if (style != ControllerStyle.InitializingSteamVR) {
      GameObject overridePrefab = null;
      switch (App.Config.m_SdkMode) {
      case SdkMode.Oculus:  overridePrefab = App.Config.m_ControlsPrefabOverrideOvr; break;
      case SdkMode.SteamVR: overridePrefab = App.Config.m_ControlsPrefabOverrideSteamVr; break;
      }
      if (overridePrefab != null) {
        Debug.LogWarning("Overriding Vr controls with {0}", overridePrefab);
        controlsPrefab = overridePrefab;
      }
    }
#endif

    if (controlsPrefab != null) {
      Debug.Assert(m_VrControls == null);
      GameObject controlsObject = Instantiate(controlsPrefab);
      m_VrControls = controlsObject.GetComponent<VrControllers>();
      if (m_VrControls == null) {
        throw new InvalidOperationException(
            string.Format("Bad prefab for {0} {1}", style, controlsPrefab));
      }
      m_VrControls.transform.parent = m_VrSystem.transform;
    }

    if (m_VrControls != null) {
      if (m_NeedsToAttachConsoleScript && ControllerConsoleScript.m_Instance) {
        ControllerConsoleScript.m_Instance.AttachToController(
            m_VrControls.Brush);
        m_NeedsToAttachConsoleScript = false;
      }

      // TODO: the only case where this is necessary is when using empty geometry
      // for ControllerStyle.InitializingSteamVR. Can we keep track of "initializing"
      // some other way?
      m_VrControls.Brush.ControllerGeometry.TempWritableStyle = style;
      m_VrControls.Wand.ControllerGeometry.TempWritableStyle = style;
    }
  }

  // Stitches together these things:
  // - Behavior, which encapsulates Wand and Brush
  // - Geometry, which encapsulates phsyical controller appearance (Touch, Knuckles, ...)
  // - Info, which encapsulates VR APIs (OVR, SteamVR, GVR, ...)
  public ControllerInfo CreateControllerInfo(BaseControllerBehavior behavior, bool isLeftHand) {
    if (App.Config.m_SdkMode == SdkMode.SteamVR) {
      return new SteamControllerInfo(behavior);
    } else if (App.Config.m_SdkMode == SdkMode.Oculus) {
      return new OculusControllerInfo(behavior, isLeftHand);
    } else if (App.Config.m_SdkMode == SdkMode.Gvr) {
      return new GvrControllerInfo(behavior, isLeftHand);
    } else {
      return new NonVrControllerInfo(behavior);
    }
  }

  // Swap the hand that each ControllerInfo is associated with
  // TODO: if the tracking were associated with the Geometry rather than the Info+Behavior,
  // we wouldn't have to do any swapping. So rather than putting Behaviour_Pose on the Behavior,
  // we should dynamically add it when creating the Geometry. This might make the Behavior
  // prefabs VRAPI-agnostic, too.
  public bool TrySwapLeftRightTracking() {
    bool leftRightSwapped = true;
    if (App.Config.m_SdkMode == SdkMode.Oculus) {
      VrControls.GetComponent<OculusHandTrackingManager>().SwapLeftRight();
    } else if (App.Config.m_SdkMode == SdkMode.SteamVR) {
      // Don't swap controller input sources while we're initializing because it screws up
      // the actions when the proper controllers are instantiated.
      // TODO : Figure out why this screws up and fix it.  Note that this is
      // unnecessary unless we support hot-swapping of controller types.
      if (! IsInitializingSteamVr) {
        BaseControllerBehavior[] behaviors = VrControls.GetBehaviors();
        for (int i = 0; i < behaviors.Length; ++i) {
          SteamVR_Behaviour_Pose pose = behaviors[i].GetComponent<SteamVR_Behaviour_Pose>();
          switch (pose.inputSource) {
          case SteamVR_Input_Sources.LeftHand:
            pose.inputSource = SteamVR_Input_Sources.RightHand;
            break;
          case SteamVR_Input_Sources.RightHand:
            pose.inputSource = SteamVR_Input_Sources.LeftHand;
            break;
          default:
            Debug.LogWarningFormat(
                "Controller is configured as {0}.  Should be LeftHand or RightHand.",
                pose.inputSource);
            break;
          }
        }
      } else {
        // Don't commit to swapping controller styles.
        leftRightSwapped = false;
      }
    } else if (App.Config.m_SdkMode == SdkMode.Gvr) {
      var tmp = InputManager.Controllers[0];
      InputManager.Controllers[0] = InputManager.Controllers[1];
      InputManager.Controllers[1] = tmp;
    }

    return leftRightSwapped;
  }

  // Returns the Degrees of Freedom for the VR system controllers.
  public DoF GetControllerDof() {
    switch (App.Config.m_SdkMode) {
    case SdkMode.Oculus:
    case SdkMode.SteamVR:
      return DoF.Six;

    case SdkMode.Gvr:
      return DoF.Six;

    case SdkMode.Monoscopic:
      return DoF.Two;

    default:
      return DoF.None;
    }
  }

  // -------------------------------------------------------------------------------------------- //
  // Overlay Methods
  // (These should only be accessed via OverlayManager.)
  // -------------------------------------------------------------------------------------------- //
  public void SetOverlayAlpha(float ratio) {
    switch (m_OverlayMode) {
    case OverlayMode.Steam:
      m_SteamVROverlay.alpha = ratio * m_OverlayMaxAlpha;
      OverlayEnabled = ratio > 0.0f;
      break;
    case OverlayMode.OVR:
      OverlayEnabled = ratio == 1;
      break;
    case OverlayMode.Mobile:
      if (!OverlayEnabled && ratio > 0.0f) {
        // Position screen overlay in front of the camera.
        m_MobileOverlay.transform.parent = GetVrCamera().transform;
        m_MobileOverlay.transform.localPosition = Vector3.zero;
        m_MobileOverlay.transform.localRotation = Quaternion.identity;
        float scale = 0.5f * GetVrCamera().farClipPlane / GetVrCamera().transform.lossyScale.z;
        m_MobileOverlay.transform.localScale = Vector3.one * scale;

        // Reparent the overlay so that it doesn't move with the headset.
        m_MobileOverlay.transform.parent = null;

        // Reset the rotation so that it's level and centered on the horizon.
        Vector3 eulerAngles = m_MobileOverlay.transform.localRotation.eulerAngles;
        m_MobileOverlay.transform.localRotation = Quaternion.Euler(new Vector3(0, eulerAngles.y, 0));

        m_MobileOverlay.gameObject.SetActive(true);
        OverlayEnabled = true;
      } else if (OverlayEnabled && ratio == 0.0f) {
        m_MobileOverlay.gameObject.SetActive(false);
        OverlayEnabled = false;
      }
      break;
    }
  }

  public bool OverlayEnabled {
    get {
      switch (m_OverlayMode) {
      case OverlayMode.Steam:
        return m_SteamVROverlay.gameObject.activeSelf;
      case OverlayMode.OVR:
#if OCULUS_SUPPORTED
        return m_OVROverlay.enabled;
#else
        return false;
#endif // OCULUS_SUPPORTED
      case OverlayMode.Mobile:
        return m_MobileOverlayOn;
      default:
        return false;
      }
    }
    set {
      switch (m_OverlayMode) {
      case OverlayMode.Steam:
        m_SteamVROverlay.gameObject.SetActive(value);
        break;
      case OverlayMode.OVR:
#if OCULUS_SUPPORTED
        m_OVROverlay.enabled = value;
#endif // OCULUS_SUPPORTED
        break;
      case OverlayMode.Mobile:
        m_MobileOverlayOn = value;
        break;
      }
    }
  }

  public void SetOverlayTexture(Texture tex) {
    switch (m_OverlayMode) {
    case OverlayMode.Steam:
      m_SteamVROverlay.texture = tex;
      m_SteamVROverlay.UpdateOverlay();
      break;
    case OverlayMode.OVR:
#if OCULUS_SUPPORTED 
      m_OVROverlay.textures = new[] { tex };
#endif // OCULUS_SUPPORTED
      break;
    }
  }

  public void PositionOverlay(float distance, float height) {
    //place overlay in front of the player a distance out
    Vector3 vOverlayPosition = ViewpointScript.Head.position;
    Vector3 vOverlayDirection = ViewpointScript.Head.forward;
    vOverlayDirection.y = 0.0f;
    vOverlayDirection.Normalize();

    switch (m_OverlayMode) {
    case OverlayMode.Steam:
      vOverlayPosition += (vOverlayDirection * distance);
      vOverlayPosition.y = height;
      m_SteamVROverlay.transform.position = vOverlayPosition;
      m_SteamVROverlay.transform.forward = vOverlayDirection;
      break;
    case OverlayMode.OVR:
#if OCULUS_SUPPORTED
      vOverlayPosition += (vOverlayDirection * distance / 10);
      m_OVROverlay.transform.position = vOverlayPosition;
      m_OVROverlay.transform.forward = vOverlayDirection;
#endif // OCULUS_SUPPORTED
      break;
    }
  }

  // Fades to the compositor world (if available) or black.
  public void FadeToCompositor(float fadeTime) {
    FadeToCompositor(fadeTime, fadeToCompositor:true);
  }

  // Fades from the compositor world (if available) or black.
  public void FadeFromCompositor(float fadeTime) {
    FadeToCompositor(fadeTime, fadeToCompositor:false);
  }

  private void FadeToCompositor(float fadeTime, bool fadeToCompositor) {
    switch (m_OverlayMode) {
    case OverlayMode.Steam:
      SteamVR rVR = SteamVR.instance;
      if (rVR != null && rVR.compositor != null) {
        rVR.compositor.FadeGrid(fadeTime, fadeToCompositor);
      }
      break;
    case OverlayMode.OVR:
      FadeBlack(fadeTime, fadeToCompositor);
      break;
    }
  }

  public void PauseRendering(bool bPause) {
    switch (m_OverlayMode) {
    case OverlayMode.Steam:
      SteamVR_Render.pauseRendering = bPause;
      break;
    case OverlayMode.OVR:
      // :(
      break;
    }
  }

  // Fades to solid black.
  public void FadeToBlack(float fadeTime) {
    FadeBlack(fadeTime, fadeToBlack:true);
  }

  // Fade from solid black.
  public void FadeFromBlack(float fadeTime) {
    FadeBlack(fadeTime, fadeToBlack:false);
  }

  private void FadeBlack(float fadeTime, bool fadeToBlack) {
    switch (App.Config.m_SdkMode) {
    case SdkMode.SteamVR:
      SteamVR_Fade.Start(fadeToBlack ? Color.black : Color.clear, fadeTime);
      break;
    case SdkMode.Oculus:
      // TODO: using Viewpoint here is pretty gross, dependencies should not go from VrSdk
      // to other Tilt Brush components.

      // Currently ViewpointScript.FadeToColor takes 1/time as a parameter, which we should fix to
      // make consistent, but for now just convert the incoming parameter.
      float speed = 1 / Mathf.Max(fadeTime, 0.00001f);
      if (fadeToBlack) {
        ViewpointScript.m_Instance.FadeToColor(Color.black, speed);
      } else {
        ViewpointScript.m_Instance.FadeToScene(speed);
      }
      break;
    }
  }

  // -------------------------------------------------------------------------------------------- //
  // HMD Related Methods
  // -------------------------------------------------------------------------------------------- //

  // Returns false if SDK Mode uses an HMD, but it is not initialized.
  // Retruns true if SDK does not have an HMD or if it is correctly initialized.
  public bool IsHmdInitialized() {
    if (App.Config.m_SdkMode == SdkMode.SteamVR && SteamVR.instance == null) {
      return false;
    } else if (App.Config.m_SdkMode == SdkMode.Gvr) {
      // We used to be able to check the GvrViewer state, but this has been moved internal to Unity.
      // Now just return true and hope for the best.
      return true;
    }
#if OCULUS_SUPPORTED
    else if (App.Config.m_SdkMode == SdkMode.Oculus && !OVRManager.isHmdPresent) {
      return false;
    }
#endif // OCULUS_SUPPORTED
    /* else if (App.Config.m_SdkMode == SdkMode.Wmr  && somehow check for Wmr headset ) {
      return false;
    } */
    return true;
  }

  // Returns the native frame rate of the HMD (or screen) in frames per second.
  public int GetHmdTargetFrameRate() {
    switch (App.Config.m_SdkMode) {
    case SdkMode.Oculus:
      return 90;
    case SdkMode.SteamVR:
      return SteamVR.instance != null ? (int)SteamVR.instance.hmd_DisplayFrequency : 60;
    case SdkMode.Gvr:
      return 75;
    case SdkMode.Monoscopic:
      return 60;
    case SdkMode.Ods:
      // TODO: 30 would be correct, buf feels too slow.
      return 60;
    default:
      throw new NotImplementedException("Unknown VR SDK Mode");
    }
  }

  // Returns the Degrees of Freedom for the VR system headset.
  public DoF GetHmdDof() {
    switch (App.Config.m_SdkMode) {
    case SdkMode.Oculus:
    case SdkMode.SteamVR:
      return DoF.Six;
    case SdkMode.Gvr:
      return DoF.Six;
    default:
      return DoF.None;
    }
  }

  // If the SDK is blocking the user's view of the application, return true.
  public bool IsAppFocusBlocked() {
    return !m_HasVrFocus;
  }

  // Scales the rendered image that the user sees by \p scale.
  // Scale is clamped to [0.1, 2].
  public void SetHmdScalingFactor(float scale) {
    scale = Mathf.Clamp(scale, 0.1f, 2f);
    if (App.Config.m_SdkMode == SdkMode.SteamVR) {
      SteamVR_Camera.sceneResolutionScale = scale;
    }
  }

  // -------------------------------------------------------------------------------------------- //
  // Tracking Methods
  // -------------------------------------------------------------------------------------------- //

  /// Clears the callbacks that get called when a new pose is received. The callbacks are saved
  /// So that they can be restored later with RestorePoseTracking.
  public void DisablePoseTracking() {
    m_TrackingBackupXf = TrTransform.FromTransform(GetVrCamera().transform);
    m_OldOnPoseApplied = NewControllerPosesApplied.GetInvocationList().Cast<Action>().ToArray();
    NewControllerPosesApplied = null;
  }

  /// Restores the pose recieved callbacks that were saved off with DisablePoseTracking. Will merge
  /// any callbacks currently on OnControllerNewPoses.
  public void RestorePoseTracking() {
    if (m_OldOnPoseApplied != null) {
      if (NewControllerPosesApplied != null) {
        var list = m_OldOnPoseApplied.Concat(NewControllerPosesApplied.GetInvocationList())
          .Distinct().Cast<Action>();
        NewControllerPosesApplied = null;
        foreach (var handler in list) {
          NewControllerPosesApplied += handler;
        }
      }
    }

    // Restore camera xf.
    if (m_TrackingBackupXf != null) {
      Transform camXf = GetVrCamera().transform;
      camXf.position = m_TrackingBackupXf.Value.translation;
      camXf.rotation = m_TrackingBackupXf.Value.rotation;
      camXf.localScale = Vector3.one;
      m_TrackingBackupXf = null;
    }
  }

  // -------------------------------------------------------------------------------------------- //
  // Performance Methods
  // -------------------------------------------------------------------------------------------- //
#if OCULUS_SUPPORTED
  public void SetFixedFoveation(int level) {
    Debug.Assert(level >= 0 && level <= 3);
    if (App.Config.IsMobileHardware && !SpoofMobileHardware.MobileHardware
                                    && App.Config.m_SdkMode == SdkMode.Oculus) {
      OVRManager.tiledMultiResLevel = (OVRManager.TiledMultiResLevel) level;
    }
  }

  /// Gets GPU utilization 0 .. 1 if supported, otherwise returns 0.
  public float GetGpuUtilization() {
    if (App.Config.m_SdkMode == SdkMode.Oculus && OVRManager.gpuUtilSupported) {
      return OVRManager.gpuUtilLevel;
    }
    return 0;
  }

  public void SetGpuClockLevel(int level) {
    if (App.Config.m_SdkMode == SdkMode.Oculus && App.Config.IsMobileHardware) {
      OVRManager.gpuLevel = level;
    }
  }
#else // OCULUS_SUPPORTED
  public void SetFixedFoveation(int level) {
  }
  
  public float GetGpuUtilization() {
    return 0;
  }

  public void SetGpuClockLevel(int level) {
  }
#endif // OCULUS_SUPPORTED
}
}
