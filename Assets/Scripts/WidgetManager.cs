//// Copyright 2020 The Tilt Brush Authors
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
using System.Linq;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace TiltBrush {

// These names are used in our save format, so they must be protected from obfuscation
// Do not change the names of any of them, unless they've never been released.
[Serializable]
public enum StencilType {
  Plane,
  Cube,
  Sphere,
  Capsule,
  Cone,
  Cylinder,
  InteriorDome,
  Pyramid,
  Ellipsoid
}

[Serializable]
public struct StencilMapKey {
  public StencilType m_Type;
  public StencilWidget m_StencilPrefab;
}

public struct StencilContactInfo {
  public StencilWidget widget;
  public Vector3 pos;
  public Vector3 normal;
}

public class GrabWidgetData {
  public readonly GameObject m_WidgetObject;
  public readonly GrabWidget m_WidgetScript;

  // These fields are only valid during a call to GetNearestGrabWidget,
  // and are undefined afterwards. Do not use them.

  public bool m_NearController;
  // only valid if m_NearController == true
  public float m_ControllerScore;

  public GrabWidgetData(GrabWidget widget) {
    m_WidgetScript = widget;
    m_WidgetObject = widget.gameObject;
  }
  // Could maybe get by without this since all users of Clone() don't care if they
  // receive a plain old GrabWidgetData or not.
  public virtual GrabWidgetData Clone() {
    return new GrabWidgetData(m_WidgetScript) {
        m_NearController = m_NearController,
        m_ControllerScore = m_ControllerScore
    };
  }
}

public class TypedWidgetData<T> : GrabWidgetData where T : GrabWidget {
  private readonly T m_typedWidget;
  public new T WidgetScript => m_typedWidget;
  public TypedWidgetData(T widget) : base(widget) {
    m_typedWidget = widget;
  }
  public override GrabWidgetData Clone() {
    return new TypedWidgetData<T>(m_typedWidget) {
        m_NearController = m_NearController,
        m_ControllerScore = m_ControllerScore
    };
  }
}

public class WidgetManager : MonoBehaviour {
  static public WidgetManager m_Instance;

  [SerializeField] ModelWidget m_ModelWidgetPrefab;
  [SerializeField] GameObject m_WidgetPinPrefab;
  [SerializeField] ImageWidget m_ImageWidgetPrefab;
  [SerializeField] VideoWidget m_VideoWidgetPrefab;
  [SerializeField] CameraPathWidget m_CameraPathWidgetPrefab;
  [SerializeField] private GameObject m_CameraPathPositionKnotPrefab;
  [SerializeField] private GameObject m_CameraPathRotationKnotPrefab;
  [SerializeField] private GameObject m_CameraPathSpeedKnotPrefab;
  [SerializeField] private GameObject m_CameraPathFovKnotPrefab;
  [SerializeField] private GameObject m_CameraPathKnotSegmentPrefab;
  [SerializeField] private GrabWidgetHome m_Home;
  [SerializeField] private GameObject m_HomeHintLinePrefab;
  [SerializeField] float m_WidgetSnapAngle = 15.0f;
  [SerializeField] float m_GazeMaxAngleFromFacing = 70.0f;
  [SerializeField] private float m_PanelFocusActivationScore;
  [SerializeField] private float m_ModelVertCountScalar = 1.0f;

  [Header("Stencils")]
  [SerializeField] StencilMapKey [] m_StencilMap;
  [SerializeField] private float m_StencilAttractDist = 0.5f;
  [SerializeField] private float m_StencilAttachHysteresis = 0.1f;
  [SerializeField] private string m_StencilLayerName;
  [SerializeField] private string m_PinnedStencilLayerName;

  private bool m_WidgetsDormant;
  private bool m_InhibitGrabWhileLoading;

  private GameObject m_HomeHintLine;
  private MeshFilter m_HomeHintLineMeshFilter;
  private Vector3 m_HomeHintLineBaseScale;

  private StencilContactInfo[] m_StencilContactInfos;
  private const int m_StencilBucketSize = 16;
  private StencilWidget m_ActiveStencil;
  private bool m_StencilsDisabled;

  // All grabbable widgets should be in exactly one of these lists.
  // Widgets will be in the most specific list.
  private List<GrabWidgetData> m_GrabWidgets;
  private List<TypedWidgetData<ModelWidget>> m_ModelWidgets;
  private List<TypedWidgetData<StencilWidget>> m_StencilWidgets;
  private List<TypedWidgetData<ImageWidget>> m_ImageWidgets;
  private List<TypedWidgetData<VideoWidget>> m_VideoWidgets;
  private List<TypedWidgetData<CameraPathWidget>> m_CameraPathWidgets;

  // These lists are used by the PinTool.  They're kept in sync by the
  // widget manager, but the PinTool is responsible for their coherency.
  private List<GrabWidget> m_CanBePinnedWidgets;
  private List<GrabWidget> m_CanBeUnpinnedWidgets;
  public event Action RefreshPinAndUnpinAction;

  private TiltModels75[] m_loadingTiltModels75;
  private TiltImages75[] m_loadingTiltImages75;
  private TiltVideo[] m_loadingTiltVideos;

  private List<GrabWidgetData> m_WidgetsNearBrush;
  private List<GrabWidgetData> m_WidgetsNearWand;

  // This value is used by SketchMemoryScript to check the sketch against memory limits.
  // It's incremented when a model is registered, and decremented when a model is
  // unregistered.
  private int m_ModelVertCount;
  // Similar to above, this value is used to check against memory limits.  Images are always
  // the same number of verts, however, so this number is scaled by texture size.  It's a
  // hand-wavey calculation.
  private int m_ImageVertCount;

  // Camera path.
  [NonSerialized] public bool FollowingPath;
  private TypedWidgetData<CameraPathWidget> m_CurrentCameraPath;
  private bool m_CameraPathsVisible;
  private CameraPathTinter m_CameraPathTinter;

  static private Dictionary<ushort, GrabWidget> sm_BatchMap = new Dictionary<ushort, GrabWidget>();

  public StencilWidget ActiveStencil {
    get { return m_ActiveStencil; }
  }

  public void ResetActiveStencil() {
    m_ActiveStencil = null;
  }

  public int StencilLayerIndex {
    get { return LayerMask.NameToLayer(m_StencilLayerName); }
  }

  public int PinnedStencilLayerIndex {
    get { return LayerMask.NameToLayer(m_PinnedStencilLayerName); }
  }

  public LayerMask PinnedStencilLayerMask {
    get { return LayerMask.GetMask(m_PinnedStencilLayerName); }
  }

  public LayerMask StencilLayerMask {
    get { return LayerMask.GetMask(m_StencilLayerName); }
  }

  public List<GrabWidgetData> WidgetsNearBrush {
    get { return m_WidgetsNearBrush; }
  }

  public List<GrabWidgetData> WidgetsNearWand {
    get { return m_WidgetsNearWand; }
  }

  public bool AnyWidgetsToPin {
    get { return m_CanBePinnedWidgets.Count > 0; }
  }

  public bool AnyWidgetsToUnpin {
    get { return m_CanBeUnpinnedWidgets.Count > 0; }
  }

  public float ModelVertCountScalar {
    get { return m_ModelVertCountScalar; }
  }

  public int ImageVertCount {
    get { return m_ImageVertCount; }
  }

  public void AdjustModelVertCount(int amount) {
    m_ModelVertCount += amount;
  }

  public void AdjustImageVertCount(int amount) {
    m_ImageVertCount += amount;
  }

  public int WidgetsVertCount {
    get { return m_ModelVertCount + m_ImageVertCount; }
  }

  public bool AnyVideoWidgetActive => m_VideoWidgets.Any(x => x.m_WidgetObject.activeSelf);

  public bool AnyCameraPathWidgetsActive =>
      m_CameraPathWidgets.Any(x => x.m_WidgetObject.activeSelf);

  public CameraPathTinter PathTinter { get => m_CameraPathTinter; }

  // Returns the associated widget for the given batchId.
  // Returns null if key doesn't exist.
  public GrabWidget GetBatch(ushort batchId) {
    if (sm_BatchMap.ContainsKey(batchId)) {
      return sm_BatchMap[batchId];
    }
    return null;
  }

  public void AddWidgetToBatchMap(GrabWidget widget, ushort batchId) {
    Debug.Assert(!sm_BatchMap.ContainsKey(batchId));
    sm_BatchMap.Add(batchId, widget);
  }

  void Awake() {
    m_Instance = this;
  }

  public void Init() {
    m_CameraPathTinter = gameObject.AddComponent<CameraPathTinter>();

    m_GrabWidgets = new List<GrabWidgetData>();
    m_ModelWidgets = new List<TypedWidgetData<ModelWidget>>();
    m_StencilWidgets = new List<TypedWidgetData<StencilWidget>>();
    m_ImageWidgets = new List<TypedWidgetData<ImageWidget>>();
    m_VideoWidgets = new List<TypedWidgetData<VideoWidget>>();
    m_CameraPathWidgets = new List<TypedWidgetData<CameraPathWidget>>();

    m_CanBePinnedWidgets = new List<GrabWidget>();
    m_CanBeUnpinnedWidgets = new List<GrabWidget>();

    m_WidgetsNearBrush = new List<GrabWidgetData>();
    m_WidgetsNearWand = new List<GrabWidgetData>();

    m_Home.Init();
    m_Home.SetFixedPosition(Vector3.zero);
    m_HomeHintLine = (GameObject)Instantiate(m_HomeHintLinePrefab);
    m_HomeHintLineMeshFilter = m_HomeHintLine.GetComponent<MeshFilter>();
    m_HomeHintLineBaseScale = m_HomeHintLine.transform.localScale;
    m_HomeHintLine.transform.parent = transform;
    m_HomeHintLine.SetActive(false);

    m_StencilContactInfos = new StencilContactInfo[m_StencilBucketSize];
    m_StencilsDisabled = false;

    FollowingPath = false;
    m_CameraPathsVisible = false;
  }

  public ModelWidget ModelWidgetPrefab { get { return m_ModelWidgetPrefab; } }
  public ImageWidget ImageWidgetPrefab { get { return m_ImageWidgetPrefab; } }
  public VideoWidget VideoWidgetPrefab { get { return m_VideoWidgetPrefab; } }
  public CameraPathWidget CameraPathWidgetPrefab { get { return m_CameraPathWidgetPrefab; } }
  public GameObject CameraPathPositionKnotPrefab { get { return m_CameraPathPositionKnotPrefab; } }
  public GameObject CameraPathRotationKnotPrefab { get { return m_CameraPathRotationKnotPrefab; } }
  public GameObject CameraPathSpeedKnotPrefab { get { return m_CameraPathSpeedKnotPrefab; } }
  public GameObject CameraPathFovKnotPrefab { get { return m_CameraPathFovKnotPrefab; } }
  public GameObject CameraPathKnotSegmentPrefab { get { return m_CameraPathKnotSegmentPrefab; } }

  public IEnumerable<GrabWidgetData> ActiveGrabWidgets {
    get {
      if (m_InhibitGrabWhileLoading) {
        // Returns only widgets that are not part of the sketch
        return m_GrabWidgets.Where(x => x.m_WidgetObject.activeSelf);
      }
      return GetAllActiveGrabWidgets();
    }
  }

  private IEnumerable<GrabWidgetData> GetAllActiveGrabWidgets() {
    for (int i = 0; i < m_GrabWidgets.Count; ++i) {
      if (m_GrabWidgets[i].m_WidgetObject.activeSelf) {
        yield return m_GrabWidgets[i];
      }
    }
    for (int i = 0; i < m_ModelWidgets.Count; ++i) {
      if (m_ModelWidgets[i].m_WidgetObject.activeSelf) {
        yield return m_ModelWidgets[i];
      }
    }
    for (int i = 0; i < m_StencilWidgets.Count; ++i) {
      if (m_StencilWidgets[i].m_WidgetObject.activeSelf) {
        yield return m_StencilWidgets[i];
      }
    }
    for (int i = 0; i < m_ImageWidgets.Count; ++i) {
      if (m_ImageWidgets[i].m_WidgetObject.activeSelf) {
        yield return m_ImageWidgets[i];
      }
    }
    for (int i = 0; i < m_VideoWidgets.Count; ++i) {
      if (m_VideoWidgets[i].m_WidgetObject.activeSelf) {
        yield return m_VideoWidgets[i];
      }
    }
    for (int i = 0; i < m_CameraPathWidgets.Count; ++i) {
      if (m_CameraPathWidgets[i].m_WidgetObject.activeSelf) {
        yield return m_CameraPathWidgets[i];
      }
    }
  }

  public IEnumerable<GrabWidgetData> MediaWidgets {
    get {
      IEnumerable<GrabWidgetData> ret = m_ModelWidgets;
      return ret.Concat(m_ImageWidgets).Concat(m_VideoWidgets);
    }
  }

  public IEnumerable<TypedWidgetData<CameraPathWidget>> CameraPathWidgets =>
      m_CameraPathWidgets.Where(x => x.m_WidgetObject.activeSelf);

  public TypedWidgetData<CameraPathWidget> GetCurrentCameraPath() => m_CurrentCameraPath;

  public void SetCurrentCameraPath(CameraPathWidget path) {
    // Early out if we're trying to set the path to the already current path.
    if (m_CurrentCameraPath != null && m_CurrentCameraPath.WidgetScript == path) {
      return;
    }

    for (int i = 0; i < m_CameraPathWidgets.Count; ++i) {
      if (m_CameraPathWidgets[i].m_WidgetScript == path) {
        FollowingPath = false;
        SetCurrentCameraPath_Internal(m_CameraPathWidgets[i]);
        App.Switchboard.TriggerCurrentCameraPathChanged();
        return;
      }
    }
  }

  public void ValidateCurrentCameraPath() {
    if (m_CurrentCameraPath == null ||
        m_CurrentCameraPath.WidgetScript == null ||
        !m_CurrentCameraPath.m_WidgetObject.activeSelf) {
      var prevPath = m_CurrentCameraPath;
      for (int i = 0; i < m_CameraPathWidgets.Count; ++i) {
        if (m_CameraPathWidgets[i] != prevPath &&
            m_CameraPathWidgets[i].m_WidgetObject.activeSelf) {
          SetCurrentCameraPath_Internal(m_CameraPathWidgets[i]);
          return;
        }
      }
    }
  }

  void SetCurrentCameraPath_Internal(TypedWidgetData<CameraPathWidget> cp) {
    if (m_CurrentCameraPath != null && m_CurrentCameraPath.WidgetScript != null) {
      m_CurrentCameraPath.WidgetScript.SetAsActivePath(false);
    }
    m_CurrentCameraPath = cp;
    if (m_CurrentCameraPath != null && m_CurrentCameraPath.WidgetScript != null) {
      m_CurrentCameraPath.WidgetScript.SetAsActivePath(true);
    }
  }

  public bool CanRecordCurrentCameraPath() {
    if (SketchSurfacePanel.m_Instance.GetCurrentToolType() != BaseTool.ToolType.MultiCamTool) {
      if (m_CameraPathsVisible && m_CurrentCameraPath != null) {
        CameraPathWidget cpw = m_CurrentCameraPath.WidgetScript;
        return (cpw != null) && cpw.gameObject.activeSelf && cpw.Path.NumPositionKnots > 1;
      }
    }
    return false;
  }

  // The following methods use indexes to reference camera paths.  Because the list of
  // camera path widgets can have inactive paths (if they're deleted), these lists may
  // have holes.  Use caution when using these methods.
  // The reason we need these methods is because our UI buttons work with SketchControls
  // global commands, which can be modified with generic integer parameters.  In those
  // cases, we can't pass a CameraPathWidget object.
  public CameraPathWidget GetNthActiveCameraPath(int nth) {
    var activeCameraPathWidgets = m_CameraPathWidgets.Where(x => x.m_WidgetObject.activeSelf);
    foreach(var cpw in activeCameraPathWidgets) {
      if (nth == 0) {
        return cpw.WidgetScript;
      }
      --nth;
    }
    return null;
  }

  public bool IsCameraPathAtIndexCurrent(int pathIndex) {
    CameraPathWidget cpw = (m_CurrentCameraPath != null) ? cpw = m_CurrentCameraPath.WidgetScript :
        null;
    if (cpw == null) { return false; }
    return GetNthActiveCameraPath(pathIndex) == m_CurrentCameraPath.WidgetScript;
  }

  public int? GetIndexOfCameraPath(CameraPathWidget path) {
    int index = 0;
    for (int i = 0; i < m_CameraPathWidgets.Count; ++i) {
      if (m_CameraPathWidgets[i].m_WidgetObject.activeSelf) {
        if (m_CameraPathWidgets[i].WidgetScript == path) {
          return index;
        }
        ++index;
      }
    }
    return null;
  }

  public CameraPathWidget CreatePathWidget() {
    CreateWidgetCommand command =
        new CreateWidgetCommand(m_CameraPathWidgetPrefab, TrTransform.identity);
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
    return m_CameraPathWidgets.Last().WidgetScript;
  }

  public bool AnyActivePathHasAKnot() {
    var datas = CameraPathWidgets;
    foreach (TypedWidgetData<CameraPathWidget> data in datas) {
      if (data.WidgetScript.Path.NumPositionKnots > 0) {
        return true;
      }
    }
    return false;
  }

  public void DeleteCameraPath(GrabWidget cameraPathWidgetScript) {
    if (cameraPathWidgetScript != null) {
      // We don't *really* delete it, we just hide it.
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(
          new HideWidgetCommand(cameraPathWidgetScript));
      FollowingPath = false;
    }

    // If our current path is null or inactive, it means we have no camera paths.  In that
    // instance, if the camera path tool is active, reset back to our default tool.
    // I'm doing this because if we leave the camera path tool active, the camera path
    // panel shows the button highlighted, which affects the user's flow for being
    // invited to start a path.  It looks weird.
    if (m_CurrentCameraPath == null || !m_CurrentCameraPath.WidgetScript.gameObject.activeSelf) {
      if (SketchSurfacePanel.m_Instance.ActiveToolType == BaseTool.ToolType.CameraPathTool) {
        SketchSurfacePanel.m_Instance.EnableDefaultTool();
      }
    }
  }

  public bool CameraPathsVisible {
    get { return m_CameraPathsVisible; }
    set {
      if (value != m_CameraPathsVisible) {
        m_CameraPathsVisible = value;

        // Camera paths.
        for (int i = 0; i < m_CameraPathWidgets.Count; ++i) {
          CameraPathWidget cpw = m_CameraPathWidgets[i].m_WidgetScript as CameraPathWidget;
          if (cpw.gameObject.activeSelf) {
            cpw.Path.SetKnotsActive(m_CameraPathsVisible);
          }
        }

        if (!m_CameraPathsVisible) {
          // Flip back to default tool if we turned off paths.
          if (SketchSurfacePanel.m_Instance.ActiveToolType == BaseTool.ToolType.CameraPathTool) {
            SketchSurfacePanel.m_Instance.EnableDefaultTool();
          }
          FollowingPath = false;
        }

        App.Switchboard.TriggerCameraPathVisibilityChanged();
      }
    }
  }

  public bool HasSelectableWidgets() {
    return (m_ModelWidgets.Count > 0) || (m_ImageWidgets.Count > 0) || (m_VideoWidgets.Count > 0) ||
        (!m_StencilsDisabled && m_StencilWidgets.Count > 0);
  }

  public bool HasExportableContent(Cloud cloud) {
    switch(cloud) {
      case Cloud.Sketchfab:
        return SketchMemoryScript.m_Instance.HasVisibleObjects() ||
            ExportableModelWidgets.Any(w => w.gameObject.activeSelf) ||
            ImageWidgets.Any(w => w.gameObject.activeSelf);
      default:
        return SketchMemoryScript.m_Instance.HasVisibleObjects() ||
            App.Config.m_EnableReferenceModelExport &&
            ExportableModelWidgets.Any(
                w => w.gameObject.activeSelf &&
                     w.Model.GetLocation().GetLocationType() == Model.Location.Type.PolyAssetId);
    }
  }

  public bool HasNonExportableContent(Cloud cloud) {
    switch(cloud) {
    case Cloud.Sketchfab:
      return VideoWidgets.Any(w => w.gameObject.activeSelf);
    default:
      return NonExportableModelWidgets.Any(w => w.gameObject.activeSelf) ||
          ImageWidgets.Any(w => w.gameObject.activeSelf) ||
          VideoWidgets.Any(w => w.gameObject.activeSelf);
    }
  }

  public bool StencilsDisabled {
    get { return m_StencilsDisabled; }
    set {
      if (value != m_StencilsDisabled) {
        // Flip flag and visuals for all stencils.
        for (int i = 0; i < m_StencilWidgets.Count; ++i) {
          StencilWidget sw = m_StencilWidgets[i].WidgetScript;
          if (sw) {
            sw.RefreshVisibility(value);
          }
        }
        m_ActiveStencil = null;
      }
      m_StencilsDisabled = value;
      RefreshPinAndUnpinLists();
    }
  }

  private static string CanonicalizeForCompare(string path) {
    return path.ToLower().Replace("\\","/");
  }

  // Shortens full file paths to "Media Library/[Models | Images]/thing"
  // Input: absolute path
  // Returns: path starting from Media Library/ (e.g. "Media Library/[Models | Images]/thing")
  //          or null if path does not lead to Media Library
  // Throws: ArgumentException if path is not full
  public static string GetPathRootedAtMedia(string path) {
    if (!System.IO.Path.IsPathRooted(path)) {
      throw new ArgumentException("Path is not rooted");
    }
    var media = App.MediaLibraryPath();
    if (CanonicalizeForCompare(path).StartsWith(CanonicalizeForCompare(media))) {
      return "Media Library" + path.Substring(media.Length);
    }
    return null;
  }

  // Returns path after Media Library/Models for models only
  // Input: absolute path
  // Returns: path starting after Models/ or null if the path is not to the Models directory
  public static string GetModelSubpath(string fullPath) {
    string media = GetPathRootedAtMedia(fullPath);
    string modelPath = "Media Library/Models/";
    if (media == null || !media.StartsWith(modelPath)) {
      return null;
    }
    return media.Substring(modelPath.Length);
  }

  // Used only at .tilt-loading time
  public void SetDataFromTilt(TiltModels75[] value) {
    m_loadingTiltModels75 = value;
  }

  // Used only at .tilt-loading time
  public void SetDataFromTilt(TiltImages75[] value) {
    m_loadingTiltImages75 = value;
  }

  public void SetDataFromTilt(CameraPathMetadata[] cameraPaths) {
    for (int i = 0; i < cameraPaths.Length; ++i) {
      CameraPathWidget.CreateFromSaveData(cameraPaths[i]);
    }
  }

  public void SetDataFromTilt(TiltVideo[] value) {
    m_loadingTiltVideos = value;
  }

  public WidgetPinScript GetWidgetPin() {
    GameObject pinObj = Instantiate(m_WidgetPinPrefab);
    pinObj.transform.parent = transform;
    return pinObj.GetComponent<WidgetPinScript>();
  }

  public void DestroyWidgetPin(WidgetPinScript pin) {
    if (pin != null) {
      Destroy(pin.gameObject);
    }
  }

  // Set the position that widgets can snap to in the current environment
  public void SetHomePosition(Vector3 position) {
    m_Home.SetFixedPosition(position);
  }

  // Dormant models are still grabbable but visuals/haptics are disabled
  public bool WidgetsDormant {
    get { return m_WidgetsDormant; }
    set {
      m_WidgetsDormant = value;
      Shader.SetGlobalFloat("_WidgetsDormant", value ? 0 : 1);
    }
  }

  public float WidgetSnapAngle {
    get { return m_WidgetSnapAngle; }
  }

  public bool IsOriginHomeWithinSnapRange(Vector3 pos) {
    return m_Home.WithinRange(pos);
  }

  public Transform GetHomeXf() {
    return m_Home.transform;
  }

  public void SetHomeOwner(Transform owner) {
    m_Home.SetOwner(owner);
    m_Home.Reset();
  }

  public void ClearHomeOwner() {
    m_Home.SetOwner(null);
    m_Home.gameObject.SetActive(false);
    m_HomeHintLine.SetActive(false);
  }

  public void EnableHome(bool bEnable) {
    m_Home.gameObject.SetActive(bEnable);
    if (!bEnable) {
      m_HomeHintLine.SetActive(false);
    }
  }

  public void LoadingState(bool bEnter) {
    m_InhibitGrabWhileLoading = bEnter;
  }

  public void UpdateHomeHintLine(Vector3 vModelSnapPos) {
    if (!m_Home.WithinRange(vModelSnapPos) && m_Home.WithinHintRange(vModelSnapPos)) {
      // Enable, position, and scale hint line.
      m_HomeHintLine.SetActive(true);
      Vector3 vHomeToModel = vModelSnapPos - m_Home.transform.position;
      m_HomeHintLine.transform.position = m_Home.transform.position +
          (vHomeToModel * 0.5f);
      m_HomeHintLine.transform.up = vHomeToModel.normalized;

      Vector3 vScale = m_HomeHintLineBaseScale;
      vScale.y = vHomeToModel.magnitude * 0.5f;
      m_HomeHintLine.transform.localScale = vScale;

      App.Instance.SelectionEffect.RegisterMesh(m_HomeHintLineMeshFilter);
      m_Home.RenderHighlight();
    } else {
      // Disable the line.
      m_HomeHintLine.SetActive(false);
    }
  }

  public void MagnetizeToStencils(ref Vector3 pos, ref Quaternion rot) {
    // Early out if stencils are disabled.
    if (m_StencilsDisabled && !App.UserConfig.Flags.GuideToggleVisiblityOnly) {
      return;
    }

    Vector3 samplePos = pos;

    // If we're painting, we have a different path for magnetization that relies on the
    // previous frame.
    if (PointerManager.m_Instance.IsLineEnabled()) {
      // If we don't have an active stencil, we're done here.
      if (m_ActiveStencil == null) {
        return;
      }

      // Using the 0 index of m_StencilContactInfos as a shortcut.
      m_StencilContactInfos[0].widget = m_ActiveStencil;
      FindClosestPointOnWidgetSurface(pos, ref m_StencilContactInfos[0]);

      m_ActiveStencil.SetInUse(true);
      pos = m_StencilContactInfos[0].pos;
      rot = Quaternion.LookRotation(m_StencilContactInfos[0].normal);
    } else {
      StencilWidget prevStencil = m_ActiveStencil;
      float fPrevScore = -m_StencilAttachHysteresis;
      int iPrevIndex = -1;
      m_ActiveStencil = null;

      // Run through the overlap list and find the best stencil to stick to.
      int iPrimaryIndex = -1;
      float fBestScore = 0;
      int sIndex = 0;
      foreach (var stencil in m_StencilWidgets) {
        StencilWidget sw = stencil.WidgetScript;
        Debug.Assert(sw != null);

        // Reset tint
        sw.SetInUse(false);

        // Does a rough check to see if the stencil might overlap. OverlapSphereNonAlloc is
        // shockingly slow, which is why we don't use it.
        Collider collider = stencil.m_WidgetScript.GrabCollider;
        float centerDist = (collider.bounds.center - samplePos).sqrMagnitude;
        if (centerDist >
            (m_StencilAttractDist * m_StencilAttractDist + collider.bounds.extents.sqrMagnitude)) {
          continue;
        }
        m_StencilContactInfos[sIndex].widget = sw;

        FindClosestPointOnWidgetSurface(samplePos, ref m_StencilContactInfos[sIndex]);

        // Find out how far we are from this point and save it as a score.
        float distToSurfactPoint = (m_StencilContactInfos[sIndex].pos - samplePos).magnitude;
        float score = 1.0f - (distToSurfactPoint / m_StencilAttractDist);
        if (score > fBestScore) {
          iPrimaryIndex = sIndex;
          fBestScore = score;
          m_ActiveStencil = m_StencilContactInfos[sIndex].widget;
        }

        if (m_StencilContactInfos[sIndex].widget == prevStencil) {
          fPrevScore = score;
          iPrevIndex = sIndex;
        }

        if (++sIndex == m_StencilBucketSize) {
          break;
        }
      }

      // If we are switching between stencils, check to see if we're switching "enough".
      if (iPrevIndex != -1 && m_ActiveStencil != null && prevStencil != m_ActiveStencil) {
        if (fPrevScore + m_StencilAttachHysteresis > fBestScore) {
          m_ActiveStencil = prevStencil;
          iPrimaryIndex = iPrevIndex;
        }
      }

      // If we found a good stencil, return the surface collision transform.
      if (m_ActiveStencil != null) {
        m_ActiveStencil.SetInUse(true);
        pos = m_StencilContactInfos[iPrimaryIndex].pos;
        rot = Quaternion.LookRotation(m_StencilContactInfos[iPrimaryIndex].normal);
      }

      if (prevStencil != m_ActiveStencil) {
        PointerManager.m_Instance.DisablePointerPreviewLine();
      }
    }

    return;
  }

  bool FindClosestPointOnCollider(
      Ray rRay, Collider collider, out RaycastHit rHitInfo, float fDist) {
    rHitInfo = new RaycastHit();
    return collider.Raycast(rRay, out rHitInfo, fDist);
  }

  void FindClosestPointOnWidgetSurface(Vector3 pos, ref StencilContactInfo info) {
    info.widget.FindClosestPointOnSurface(pos, out info.pos, out info.normal);
  }

  public bool ShouldUpdateCollisions() {
    return ActiveGrabWidgets.Any(elt => elt.m_WidgetScript.IsCollisionEnabled());
  }

  public IEnumerable<ModelWidget> ModelWidgets {
    get {
      return m_ModelWidgets
        .Select(w => w == null ? null : w.WidgetScript)
        .Where(w => w != null);
    }
  }

  public IEnumerable<VideoWidget> VideoWidgets {
    get {
      return m_VideoWidgets
          .Select(w => w == null ? null : w.WidgetScript)
          .Where(w => w != null);
    }
  }

  public IEnumerable<ModelWidget> NonExportableModelWidgets {
    get {
      return m_ModelWidgets
        .Select(w => w == null ? null : w.WidgetScript)
        .Where(w => w != null).Where(w => !w.Model.AllowExport);
    }
  }

  public IEnumerable<ModelWidget> ExportableModelWidgets {
    get {
      return m_ModelWidgets
        .Select(w => w == null ? null : w.WidgetScript)
        .Where(w => w != null).Where(w => w.Model.AllowExport);
    }
  }

  public IEnumerable<StencilWidget> StencilWidgets {
    get {
      return m_StencilWidgets
        .Select(d => d == null ? null : d.WidgetScript)
        .Where(w => w != null);
    }
  }

  public StencilWidget GetStencilPrefab(StencilType type) {
    for (int i = 0; i < m_StencilMap.Length; ++i) {
      if (m_StencilMap[i].m_Type == type) {
        return m_StencilMap[i].m_StencilPrefab;
      }
    }
    throw new ArgumentException(type.ToString());
  }

  public IEnumerable<ImageWidget> ImageWidgets {
    get {
      return m_ImageWidgets
        .Select(d => d == null ? null : d.m_WidgetScript as ImageWidget)
        .Where(w => w != null);
    }
  }

  public List<GrabWidget> GetAllUnselectedActiveWidgets() {
    List<GrabWidget> widgets = new List<GrabWidget>();
    GetUnselectedActiveWidgetsInList(m_ModelWidgets);
    GetUnselectedActiveWidgetsInList(m_ImageWidgets);
    GetUnselectedActiveWidgetsInList(m_VideoWidgets);
    if (!m_StencilsDisabled) {
      GetUnselectedActiveWidgetsInList(m_StencilWidgets);
    }
    return widgets;

    void GetUnselectedActiveWidgetsInList<T>(List<TypedWidgetData<T>> list) where T : GrabWidget {
      for (int i = 0; i < list.Count; ++i) {
        GrabWidget w = list[i].m_WidgetScript;
        if (!w.Pinned && w.transform.parent == App.Scene.MainCanvas.transform &&
            w.gameObject.activeSelf) {
          widgets.Add(w);
        }
      }
    }
  }

  public void RefreshPinAndUnpinLists() {
    if (RefreshPinAndUnpinAction != null) {
      m_CanBePinnedWidgets.Clear();
      m_CanBeUnpinnedWidgets.Clear();

      RefreshPinUnpinWidgetList(m_ModelWidgets);
      RefreshPinUnpinWidgetList(m_ImageWidgets);
      RefreshPinUnpinWidgetList(m_VideoWidgets);
      RefreshPinUnpinWidgetList(m_StencilWidgets);

      RefreshPinAndUnpinAction();
    }

    // New in C# 7 - local functions!
    void RefreshPinUnpinWidgetList<T>(List<TypedWidgetData<T>> widgetList) where T : GrabWidget {
      foreach (var widgetData in widgetList) {
        var widget = widgetData.WidgetScript;
        if (widget.gameObject.activeSelf && widget.AllowPinning) {
          if (widget.Pinned) {
            m_CanBeUnpinnedWidgets.Add(widget);
          } else {
            m_CanBePinnedWidgets.Add(widget);
          }
        }
      }
    }
  }

  public void RegisterHighlightsForPinnableWidgets(bool pinnable) {
    List<GrabWidget> widgets = pinnable ? m_CanBePinnedWidgets : m_CanBeUnpinnedWidgets;
    for (int i = 0; i < widgets.Count; ++i) {
      GrabWidget w = widgets[i];
      // If stencils are disabled, don't highlight them, cause we can interact with 'em.
      if (WidgetManager.m_Instance.StencilsDisabled) {
        if (w is StencilWidget) {
          continue;
        }
      }
      w.RegisterHighlight();
    }
  }

  public void RegisterGrabWidget(GameObject rWidget) {
    // Find b/29514616
    if (ReferenceEquals(rWidget, null)) {
      throw new ArgumentNullException("rWidget");
    } else if (rWidget == null) {
      throw new ArgumentNullException("rWidget(2)");
    }
    GrabWidget generic = rWidget.GetComponent<GrabWidget>();
    if (generic == null) {
      throw new InvalidOperationException($"Object {rWidget.name} is not a GrabWidget");
    }

    if (generic is ModelWidget mw) {
      m_ModelWidgets.Add(new TypedWidgetData<ModelWidget>(mw));
    } else if (generic is StencilWidget stencil) {
      m_StencilWidgets.Add(new TypedWidgetData<StencilWidget>(stencil));
    } else if (generic is ImageWidget image) {
      m_ImageWidgets.Add(new TypedWidgetData<ImageWidget>(image));
    } else if (generic is VideoWidget video) {
      m_VideoWidgets.Add(new TypedWidgetData<VideoWidget>(video));
    } else if (generic is CameraPathWidget cpw) {
      m_CameraPathWidgets.Add(new TypedWidgetData<CameraPathWidget>(cpw));
    } else {
      m_GrabWidgets.Add(new GrabWidgetData(generic));
    }

    RefreshPinAndUnpinLists();
  }

  // Returns true if a widget was removed
  static bool RemoveFrom<T>(List<T> list, GameObject rWidget)
      where T : GrabWidgetData {
    int idx = list.FindIndex((data) => data.m_WidgetObject == rWidget);
    if (idx != -1) {
      list.RemoveAt(idx);
      return true;
    }
    return false;
  }

  public void UnregisterGrabWidget(GameObject rWidget) {
    // Get this widget's batchId out of the map.
    sm_BatchMap.Remove(rWidget.GetComponent<GrabWidget>().BatchId);

    // Pull out of pin tool lists.
    RefreshPinAndUnpinLists();

    // Decrement model vert count if we're a model widget.
    ModelWidget mw = rWidget.GetComponent<GrabWidget>() as ModelWidget;
    if (mw != null) {
      m_ModelVertCount -= mw.NumVertsTrackedByWidgetManager;
    }
    // Same with image widget.
    ImageWidget iw = rWidget.GetComponent<GrabWidget>() as ImageWidget;
    if (iw != null) {
      m_ImageVertCount -= iw.NumVertsTrackedByWidgetManager;
    }

    if (RemoveFrom(m_ModelWidgets, rWidget)) { return; }
    if (RemoveFrom(m_StencilWidgets, rWidget)) { return; }
    if (RemoveFrom(m_ImageWidgets, rWidget)) { return; }
    if (RemoveFrom(m_VideoWidgets, rWidget)) { return; }
    if (RemoveFrom(m_CameraPathWidgets, rWidget)) { return; }
    RemoveFrom(m_GrabWidgets, rWidget);
  }

  public ImageWidget GetNearestImage(Vector3 pos, float maxDepth, ref Vector3 sampleLoc) {
    ImageWidget bestImage = null;
    float leastDistance = float.MaxValue;
    foreach (var im in ImageWidgets.Where(i => i.gameObject.activeSelf)) {
      Vector3 dropper_QS;
      Vector3 dropper_GS = pos;
      Matrix4x4 xfQuadFromGlobal = im.m_ImageQuad.transform.worldToLocalMatrix;
      dropper_QS = xfQuadFromGlobal.MultiplyPoint3x4(dropper_GS);
      if (Mathf.Abs(dropper_QS.z) < leastDistance
          && Mathf.Abs(dropper_QS.x) <= 0.5f
          && Mathf.Abs(dropper_QS.y) <= 0.5f
          && Mathf.Abs(dropper_QS.z) <= maxDepth / Mathf.Abs(im.GetSignedWidgetSize())) {
        bestImage = im;
        leastDistance = Mathf.Abs(dropper_QS.z);
        sampleLoc = dropper_QS;
      }
    }
    return bestImage;
  }

  public void RefreshNearestWidgetLists(Ray currentGazeRay, int currentGazeObject) {
    m_WidgetsNearBrush.Clear();
    UpdateNearestGrabsFor(InputManager.ControllerName.Brush, currentGazeRay, currentGazeObject);
    foreach (GrabWidgetData widget in ActiveGrabWidgets) {
      if (widget.m_NearController) {
        // Deep copy.
        m_WidgetsNearBrush.Add(widget.Clone());
      }
    }

    m_WidgetsNearWand.Clear();
    UpdateNearestGrabsFor(InputManager.ControllerName.Wand, currentGazeRay, currentGazeObject);
    foreach (GrabWidgetData widget in ActiveGrabWidgets) {
      if (widget.m_NearController) {
        m_WidgetsNearWand.Add(widget.Clone());
      }
    }
  }

  // Helper for RefreshNearestWidgetLists
  void UpdateNearestGrabsFor(
      InputManager.ControllerName name, Ray currentGazeRay, int currentGazeObject) {
    // Reset hit flags.
    foreach (var elt in ActiveGrabWidgets) {
      elt.m_NearController = false;
      elt.m_ControllerScore = -1.0f;
    }

    Vector3 controllerPos = Vector3.zero;
    if (name == InputManager.ControllerName.Brush) {
      controllerPos = InputManager.m_Instance.GetBrushControllerAttachPoint().position;
    } else if (name == InputManager.ControllerName.Wand) {
      controllerPos = InputManager.m_Instance.GetWandControllerAttachPoint().position;
    } else {
      Debug.LogError("UpdateNearestGrabsFor() only supports Brush and Wand controller types.");
    }

    // Figure out if controller is in view frustum.  If it isn't, don't allow widget grabs.
    Vector3 vToController = controllerPos - currentGazeRay.origin;
    vToController.Normalize();
    if (Vector3.Angle(vToController, currentGazeRay.direction) > m_GazeMaxAngleFromFacing) {
      return;
    }

    BasePanel gazePanel = null;
    if (currentGazeObject > -1) {
      gazePanel = PanelManager.m_Instance.GetPanel(currentGazeObject);
    }

    foreach (var data in ActiveGrabWidgets) {
      if (!data.m_WidgetScript.enabled) {
        continue;
      }
      if (m_StencilsDisabled && data.m_WidgetScript is StencilWidget) {
        continue;
      }
      if (SelectionManager.m_Instance.ShouldRemoveFromSelection() &&
          !data.m_WidgetScript.CanGrabDuringDeselection()) {
        continue;
      }
      if (SelectionManager.m_Instance.IsWidgetSelected(data.m_WidgetScript)) {
        continue;
      }
      float score = data.m_WidgetScript.GetActivationScore(controllerPos, name);
      if (score < m_PanelFocusActivationScore && name == InputManager.ControllerName.Brush &&
          gazePanel && data.m_WidgetObject == gazePanel.gameObject) {
        // If the brush is pointing at a panel, make sure that the panel will be the widget grabbed
        score = m_PanelFocusActivationScore;
      }
      if (score < 0) {
        continue;
      }

      data.m_NearController = true;
      data.m_ControllerScore = score;
    }
  }

  public float DistanceToNearestWidget(Ray ray) {
    // If we're in controller mode, find the nearest colliding widget that might get in our way.
    float fNearestWidget = 99999.0f;
    foreach (var elt in ActiveGrabWidgets) {
      float fWidgetDist = 0.0f;
      if (elt.m_WidgetScript.DistanceToCollider(ray, out fWidgetDist)) {
        fNearestWidget = Mathf.Min(fNearestWidget, fWidgetDist);
      }
    }
    return fNearestWidget;
  }

  public void DestroyAllWidgets() {
    DestroyWidgetList(m_ModelWidgets);
    DestroyWidgetList(m_ImageWidgets);
    DestroyWidgetList(m_VideoWidgets);
    DestroyWidgetList(m_StencilWidgets);
    DestroyWidgetList(m_CameraPathWidgets, false);
    SetCurrentCameraPath_Internal(null);
    App.Switchboard.TriggerAllWidgetsDestroyed();

    void DestroyWidgetList<T>(List<TypedWidgetData<T>> widgetList,
        bool hideBeforeDestroy = true) where T : GrabWidget {
      while (widgetList.Count > 0) {
        GrabWidget widget = widgetList[0].m_WidgetScript;
        GameObject obj = widgetList[0].m_WidgetObject;
        if (hideBeforeDestroy) { widget.Hide(); }
        widget.OnPreDestroy();
        UnregisterGrabWidget(obj);
        Destroy(obj);
      }
    }
  }

  /// Upon return:
  /// - ImageWidgets have at least an icon-sized Texture2D; it will be mutated with
  ///   the fullres texture data some time later.
  /// - ModelWidgets may have a dummy, invisible Model instead of the actual Model they want;
  ///   the Model will be automatically replaced with the loaded Model some time later.
  public IEnumerator<Null> CreateMediaWidgetsFromLoadDataCoroutine() {
    if (m_loadingTiltModels75 != null) {
      OverlayManager.m_Instance.RefuseProgressBarChanges(true);

      if (App.Config.kModelWidgetsWaitForLoad) {
        var assetIds = m_loadingTiltModels75
            .Select(tm => tm.AssetId).Where(aid => aid != null).ToArray();
        // Kick off a bunch of loads...
        foreach (var assetId in assetIds) {
          if (App.PolyAssetCatalog.GetAssetLoadState(assetId)
              != PolyAssetCatalog.AssetLoadState.Loaded) {
            App.PolyAssetCatalog.RequestModelLoad(assetId, "tiltload");
          }
        }
        // ... and wait for them to complete
        // No widgets have been created yet, so we can't use AreMediaWidgetsStillLoading.
        bool IsLoading(string assetId) {
          var state = App.PolyAssetCatalog.GetAssetLoadState(assetId);
          return (state == PolyAssetCatalog.AssetLoadState.Downloading ||
                  state == PolyAssetCatalog.AssetLoadState.Loading);
        }
        while (assetIds.Any(IsLoading)) {
          yield return null;
        }
      }

      for (int i = 0; i < m_loadingTiltModels75.Length; i++) {
        ModelWidget.CreateFromSaveData(m_loadingTiltModels75[i]);
        OverlayManager.m_Instance.UpdateProgress(
          (float)(i + 1) / m_loadingTiltModels75.Length, true);
      }
      OverlayManager.m_Instance.RefuseProgressBarChanges(false);
      m_loadingTiltModels75 = null;
    }
    ModelCatalog.m_Instance.PrintMissingModelWarnings();
    if (m_loadingTiltImages75 != null) {
      foreach (TiltImages75 import in m_loadingTiltImages75) {
        // TODO: FromTiltImage should take advantage of being called by a coroutine
        // so it can avoid calling ReferenceImage.SynchronousLoad()
        ImageWidget.FromTiltImage(import);
      }
      m_loadingTiltImages75 = null;
    }
    if (m_loadingTiltVideos != null) {
      foreach (var video in m_loadingTiltVideos) {
        VideoWidget.FromTiltVideo(video);
      }
      m_loadingTiltVideos = null;
    }
    yield break;
  }

  /// Returns true when all media widgets have finished getting created.
  /// Note that:
  /// - ImageWidgets may have low-res textures
  /// - ModelWidgets may not have a model yet (depending on Config.kModelWidgetsWaitForLoad)
  public bool CreatingMediaWidgets =>
      m_loadingTiltModels75 != null ||
      m_loadingTiltImages75 != null ||
      m_loadingTiltVideos != null;

  /// Returns true if any widgets are still waiting for their data.
  public bool AreMediaWidgetsStillLoading() {
    if (CreatingMediaWidgets) { return true; }
    // Widgets have been created, but some may not have their data yet
    PolyAssetCatalog pac = App.PolyAssetCatalog;
    foreach (var gwd in m_ModelWidgets) {
      Model.Location loc = gwd.WidgetScript.Model.GetLocation();
      if (loc.GetLocationType() == Model.Location.Type.PolyAssetId) {
        switch (pac.GetAssetLoadState(loc.AssetId)) {
          case PolyAssetCatalog.AssetLoadState.Downloading:
          case PolyAssetCatalog.AssetLoadState.Loading:
            return true;
        }
      }
    }
    return false;
  }

  // Smaller is better. Invalid objects get negative values.
  static float ScoreByAngleAndDistance(GrabWidgetData data) {
    if (!data.m_WidgetScript.Showing) { return -1; }
    Transform source = ViewpointScript.Head;
    Transform dest = data.m_WidgetObject.transform;
    Vector3 delta = (dest.position - source.position);
    float dist = delta.magnitude;
    return dist / Vector3.Dot(delta.normalized, source.forward);
  }

  // Simulate a grab and toss. Use for debugging in monoscopic only.
  public void TossNearestWidget() {
    var widget = MediaWidgets.Where(w => w.m_WidgetScript.Showing)
        .Select(w => new { score = ScoreByAngleAndDistance(w), widget = w.m_WidgetScript })
        .Where(data => data.score > 0)
        .OrderBy(data => data.score)
        .FirstOrDefault().widget;

    if (widget != null) {
      if (widget.Pinned) {
        SketchMemoryScript.m_Instance.PerformAndRecordCommand(new PinWidgetCommand(widget, false));
      }
      // All media widgets record their movements.
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new MoveWidgetCommand(widget, widget.LocalTransform, widget.CustomDimension));
      float speed = (App.METERS_TO_UNITS * SketchControlsScript.m_Instance.m_TossThresholdMeters);
      Vector3 vLinVel = ViewpointScript.Head.forward * speed;
      Vector3 vAngVel = Quaternion.AngleAxis(UnityEngine.Random.Range(0, 360), Vector3.forward)
          * ViewpointScript.Head.up * 500;
      widget.SetVelocities(vLinVel, vAngVel, widget.transform.position);
    } else {
      Debug.Log("No media in sketch");
    }
  }
}
}
