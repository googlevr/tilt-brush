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
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush {

[System.Serializable]
public struct PanelMapKey {
  public GameObject m_PanelPrefab;
  public bool m_ModeVr;
  public bool m_ModeVrExperimental;
  public bool m_ModeMono;
  public bool m_ModeQuest;
  public bool m_ModeGvr;
  public bool m_Basic;
  public bool m_Advanced;

  public bool IsValidForSdkMode(SdkMode mode) {
    switch (mode) {
    case SdkMode.SteamVR:
    case SdkMode.Oculus:
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
      if (Config.IsExperimental) {
        return m_ModeVrExperimental;
      }
#endif
      if (App.Config.IsMobileHardware) {
        return m_ModeQuest;
      }
      return m_ModeVr;
    case SdkMode.Gvr:
      return m_ModeGvr;
    case SdkMode.Monoscopic:
      return m_ModeMono;
    default:
      if (App.VrSdk.GetControllerDof() != VrSdk.DoF.None) {
        return m_ModeMono;
      }
      break;
    }
    return false;
  }
}

public class WandPane {
  public List<BasePanel> orderedPanelList;
  public float angleOffset;
}

public class PanelManager : MonoBehaviour {
  static public PanelManager m_Instance;

  public const string kPlayerPrefAdvancedMode = "AdvancedMode";

  [SerializeField] protected GameObject m_UxExplorationPrefab;

  [SerializeField] protected PanelMapKey [] m_PanelMap;

  [SerializeField] float m_WandPanelsRotationFeedbackInterval = 20.0f;
  [SerializeField] float m_WandPanelsRotationScalar = 200.0f;
  [SerializeField] float m_WandPanelsRotationVelocityScalar = 7500.0f;
  [SerializeField] float m_WandPanelsRotationDecay = 10.0f;
  [SerializeField] float m_RotateSnapRestTime = 0.1f;
  [SerializeField] float m_WandRadius = 1.0f;
  [SerializeField] float m_AdminPanelWandRadius = 1.0f;
  [SerializeField] float m_TransitionSpeed = 8.0f;
  [SerializeField] Vector3 m_SketchbookOffset;
  [SerializeField] Vector3 m_SketchbookRotation;
  [SerializeField] float m_WandPanelsInitialSketchLoadOriginAngle;
  [SerializeField] float m_WandPanelsDefaultOriginAngle;

  [SerializeField] float m_AltModeSwipeThreshold = 0.5f;
  [SerializeField] float m_AltModeSwipeTeaseAmount = 30.0f;

  [SerializeField] float m_AdvancedModeRevealSpinSpeed;
  [SerializeField] GameObject m_AdvancedModeRevealParticlePrefab;

  [SerializeField] float m_WandSnapDistance = 1.0f;
  [SerializeField] float m_WandRadiusManipulationAdjust = 0.25f;
  [SerializeField] float m_WandPanelSnapStepDistance;
  [SerializeField] float m_WandPanelSnapStickyPercent;
  [SerializeField] float m_WandPaneHeightOffset;
  [SerializeField] float m_WandPaneHalfWidth;
  [SerializeField] float m_WandPaneMaxY;
  [SerializeField] float m_WandPaneMinY;
  [SerializeField] float m_WandPaneVisualOffsetRadius;
  [SerializeField] float m_WandPaneAttachMaxFacingAngle = 70.0f;
  [SerializeField] float m_WandPaneHapticDelay = 0.1f;

  [SerializeField] GameObject m_WandPaneVisualsPrefab;
  [SerializeField] Color m_WandPaneVisualsColorOK;
  [SerializeField] Color m_WandPaneVisualsColorBad;
  [SerializeField] float m_WandPaneVisualsShowSpeed;
  [SerializeField] private float m_PanelMipmapBias = 0;

  [SerializeField] private Color m_PanelHighlightActiveColor;
  [SerializeField] private Color m_PanelHighlightInactiveColor;
  [SerializeField] private Color m_PanelBorderMeshBaseColor = Color.white;
  [SerializeField] private Color m_PanelBorderMeshOutlineColor = Color.black;

  private GameObject m_WandPaneVisuals;
  private Renderer m_WandPaneVisualsMeshRenderer;
  private float m_WandPaneVisualsShowValue;
  private PaneVisualsState m_WandPaneVisualsState;
  private WandPane[] m_WandPanes;
  private int? m_WandPanePanelSnapPreviousStep;
  private bool m_WandPanePanelWasAttached;
  private bool m_PanelsCustomized;

  private float m_AltModeSwipeAmount;
  private bool m_AltModeSwipeEatStickInput;

  private float m_AdvancedModeRevealSpinValue;
  private float m_AdvancedModeRevealSpinTarget;
  private float m_AdvancedModeRevealSpinFinalAngle;
  private bool m_AdvancedModeRevealActive;

  public PanelSweetSpotScript m_SweetSpot;
  private GrabWidget m_ImmovableWidget;

  enum RotateSnapState {
    Done,
    Snapping,
    Jiggling,
    Resting,
  }

  enum PanelsState {
    Visible,
    Exiting,
    Hidden,
    Entering
  }

  public enum PanelMode {
    Standard,
    Sketchbook,
    Settings,
    MemoryWarning,
    Camera,
    BrushLab,

    StandardToSketchbook,
    SketchbookToStandard,

    StandardToSettings,
    SettingsToStandard,

    StandardToCamera,
    CameraToStandard,

    StandardToBrushLab,
    BrushLabToStandard,

    // StandardToMemoryWarning - Transition to Memory Warning can come from any state.
    MemoryWarningToStandard,
  }

  public enum PaneVisualsState {
    Hidden,
    HiddenToShowing,
    Showing,
    ShowingToHidden,
  }

  public class RevealParticle {
    public Transform xf;
    public ParticleSystem.EmissionModule emission;
    public ParticleSystem.ShapeModule shape;
    public float baseRate;
    public Transform parentXf;
    public Vector3 bounds;

    public void UpdateTransform() {
      xf.position = parentXf.position;
      xf.rotation = parentXf.rotation;
      Vector3 scale = shape.scale;
      scale.x = parentXf.localScale.x * bounds.x;
      scale.y = parentXf.localScale.y * bounds.y;
      shape.scale = scale;
    }
    public void StartEmitting() {
      emission.rateOverTimeMultiplier = baseRate;
    }
    public void StopEmitting() {
      emission.rateOverTimeMultiplier = 0.0f;
    }
  }
  private GameObject m_RevealParticleParent;

  public class PanelData {
    public BasePanel m_Panel;
    public PanelWidget m_Widget;
    public bool m_RestoreFlag;
    public RevealParticle m_RevealParticles;

    public bool AvailableInCurrentMode {
      get {
        // Admin panel is always available.
        return (PanelManager.m_Instance.IsAdminPanel(m_Panel.Type) ||
            (PanelManager.m_Instance.AdvancedModeActive() ==
            m_Panel.AdvancedModePanel));
      }
    }
  }
  private List<PanelData> m_AllPanels;
  private List<BasePanel> m_SketchbookPanels;
  private List<BasePanel> m_SettingsPanels;
  private List<BasePanel> m_MemoryWarningPanels;
  private List<BasePanel> m_CameraPanels;
  private List<BasePanel> m_BrushLabPanels;
  private BasePanel m_AdminPanel;

  private AdvancedPanelLayouts m_CachedPanelLayouts;

  // The current rotation of the panels, accumulated from frame to frame
  private float m_WandPanelsOriginAngle;
  // An offset added to the current rotation, reset every frame
  private float m_WandPanelsOriginAngleOffset;
  // Parameter to control the snap animation, see use below for details.
  private float m_RotateSnapAnimTime = 0;
  // Direction which the snap rotation is animating
  private float m_RotateSnapAnimDir = 0;
  private RotateSnapState m_RotateSnapState = RotateSnapState.Done;
  private float m_WandPanelsSketchbookOriginAngle;
  private float m_WandPanelsRotationLastFeedbackAngle;
  private float[] m_WandPanelsRotationDiffHistory;
  private int m_WandPanelsRotationDiffIndex;
  private int m_WandPanelsRotationDiffCount;
  private float m_WandPanelsRotationVelocity;

  private float m_MasterScale;
  private float m_StandardScale;
  private float m_SketchbookScale;
  private float m_SettingsScale;
  private float m_MemoryWarningScale;
  private float m_CameraScale;
  private float m_BrushLabScale;

  private PanelsState m_PanelsState;
  private PanelMode m_PanelsMode;

  private GameObject m_UxExploration;

  private int m_LastOpenedPanelIndex = -1;
  private BasePanel m_LastPanelInteractedWith;

  private bool m_IntroSketchbookMode;
  private bool m_FirstSketchLoad = true;
  private bool m_AdvancedPanels;

  public Color PanelHighlightActiveColor {
    get { return m_PanelHighlightActiveColor; }
  }
  public Color PanelHighlightInactiveColor {
    get { return m_PanelHighlightInactiveColor; }
  }
  public Color PanelBorderMeshBaseColor {
    get { return m_PanelBorderMeshBaseColor; }
  }
  public Color PanelBorderMeshOutlineColor {
    get { return m_PanelBorderMeshOutlineColor; }
  }

  // Proxy for whether the viewer is in the intro sketch (second run and onward).
  // TODO: Move this property into App.cs where it makes more sense for it to be.
  public bool IntroSketchbookMode { get { return m_IntroSketchbookMode; } }

  public float GetSnapStepDistance() { return m_WandPanelSnapStepDistance; }

  public bool PanelsAreStable() {
    return StandardActive() || SketchbookActive() || SettingsActive() || MemoryWarningActive() ||
        CameraActive() || BrushLabActive();
  }
  public bool StandardActive() { return m_PanelsMode == PanelMode.Standard; }
  public bool SketchbookActive() { return m_PanelsMode == PanelMode.Sketchbook; }
  public bool SettingsActive() { return m_PanelsMode == PanelMode.Settings; }
  public bool CameraActive() { return m_PanelsMode == PanelMode.Camera; }
  public bool MemoryWarningActive() { return m_PanelsMode == PanelMode.MemoryWarning; }
  public bool BrushLabActive() { return m_PanelsMode == PanelMode.BrushLab; }
  public bool PanelsHaveBeenCustomized() { return m_PanelsCustomized; }
  public bool AdvancedModeActive() { return m_AdvancedPanels; }
  public bool SketchbookActiveIncludingTransitions() {
    return SketchbookActive() || m_PanelsMode == PanelMode.StandardToSketchbook;
  }

  // TODO: Remove this function.
  // This is a dangerous function, as it'll return the first panel of this type in the
  // list.  This functionality is undefined for multiple panels of the same type.
  public BasePanel GetPanelByType(BasePanel.PanelType type) {
    for (int i = 0; i < m_AllPanels.Count; ++i) {
      if (m_AllPanels[i].m_Panel.Type == type) {
        return m_AllPanels[i].m_Panel;
      }
    }
    return null;
  }
  public BasePanel GetActivePanelByType(BasePanel.PanelType type) {
    return m_AllPanels.Select(x => x.m_Panel).
        FirstOrDefault(x => x.gameObject.activeInHierarchy && (x.Type == type));
  }
  public BasePanel GetSketchBookPanel() {
    BasePanel.PanelType sketchbookPanelType = App.Config.IsMobileHardware
        ? BasePanel.PanelType.SketchbookMobile
        : BasePanel.PanelType.Sketchbook;
    return GetActivePanelByType(sketchbookPanelType);
  }
  public BasePanel GetPanel(int index) { return m_AllPanels[index].m_Panel; }
  public BasePanel LastOpenedPanel() {
    return m_LastOpenedPanelIndex > -1 ? m_AllPanels[m_LastOpenedPanelIndex].m_Panel : null;
  }
  public bool GazePanelsAreVisible() { return m_PanelsState == PanelsState.Visible; }
  public List<PanelData> GetAllPanels() { return m_AllPanels; }
  public BasePanel GetAdminPanel() { return m_AdminPanel; }

  public BasePanel LastPanelInteractedWith {
    get { return m_LastPanelInteractedWith; }
    set { m_LastPanelInteractedWith = value; }
  }

  public bool IsAdminPanel(BasePanel.PanelType type) {
    return type == BasePanel.PanelType.AdminPanel || type == BasePanel.PanelType.AdminPanelMobile;
  }

  // Unique panels do not change when toggling basic/advanced mode.
  // A unique panel is one that is guaranteed to not have a basic/advanced counterpart.  That
  // is, it's guaranteed that <=1 exists.
  public bool IsPanelUnique(BasePanel.PanelType type) {
    return IsAdminPanel(type) ||
        type == BasePanel.PanelType.AppSettings || type == BasePanel.PanelType.AppSettingsMobile ||
        type == BasePanel.PanelType.Sketchbook || type == BasePanel.PanelType.SketchbookMobile ||
        type == BasePanel.PanelType.Camera || type == BasePanel.PanelType.MemoryWarning;
  }

  // Core panels are those that exist in the basic mode experience.  Practically, those that
  // cannot be spawned by a button and cannot be dimissed [by throwing].
  public bool IsPanelCore(BasePanel.PanelType type) {
    return type == BasePanel.PanelType.Color || type == BasePanel.PanelType.Brush ||
        type == BasePanel.PanelType.BrushExperimental || type == BasePanel.PanelType.BrushMobile ||
        type == BasePanel.PanelType.ToolsAdvanced || type == BasePanel.PanelType.ToolsBasic ||
        type == BasePanel.PanelType.Experimental || type == BasePanel.PanelType.ExtraPanel ||
        type == BasePanel.PanelType.ExtraMobile || type == BasePanel.PanelType.ToolsAdvancedMobile;
  }

  void Awake() {
    m_Instance = this;
  }

  public void Init() {
    m_AllPanels = new List<PanelData>();
    m_SketchbookPanels = new List<BasePanel>();
    m_SettingsPanels = new List<BasePanel>();
    m_MemoryWarningPanels = new List<BasePanel>();
    m_CameraPanels = new List<BasePanel>();
    m_BrushLabPanels = new List<BasePanel>();

    m_RevealParticleParent = new GameObject("ParticlesParent");
    m_RevealParticleParent.transform.parent = transform;
    m_PanelsState = PanelsState.Hidden;
    m_PanelsMode = PanelMode.Standard;
    m_MasterScale = 0.0f;
    m_StandardScale = 1.0f;
    m_SketchbookScale = 0.0f;

    // Start with advanced panels off.
    m_AdvancedPanels = PlayerPrefs.GetInt(kPlayerPrefAdvancedMode, 0) == 1;

    // Cache any advanced panel layout we can pull from disk.
    m_CachedPanelLayouts = new AdvancedPanelLayouts();
    m_CachedPanelLayouts.PopulateFromPlayerPrefs();

    // Run through our panel map and create each panel, if it is valid for this SDK Mode.
    for (int i = 0; i < m_PanelMap.Length; ++i) {
      // Don't bother with the sketch surface, it's in the Main scene already.
      BasePanel.PanelType type = (BasePanel.PanelType)i;
      if (type == BasePanel.PanelType.SketchSurface) {
        continue;
      }

      if (!App.Instance.StartupError && m_PanelMap[i].IsValidForSdkMode(App.Config.m_SdkMode)) {
        // Only create one of our unique panels
        if (IsPanelUnique(type)) {
          CreatePanel(m_PanelMap[i], false);
        } else {
          // Create a panel for each mode.
          if (m_PanelMap[i].m_Basic) {
            CreatePanel(m_PanelMap[i], false);
          }
          if (m_PanelMap[i].m_Advanced) {
            CreatePanel(m_PanelMap[i], true);
          }
        }
      }
    }

    // Init rotation.
    m_WandPanelsRotationDiffCount = 4;
    m_WandPanelsRotationDiffHistory = new float[m_WandPanelsRotationDiffCount];
    m_WandPanelsRotationDiffIndex = 0;
    m_WandPanelsOriginAngle = m_WandPanelsDefaultOriginAngle;
    m_WandPanelsRotationLastFeedbackAngle = 0.0f;

    // Init panes.
    m_WandPanes = new WandPane[3];
    for (int i = 0; i < 3; ++i) {
      m_WandPanes[i] = new WandPane();
      m_WandPanes[i].angleOffset = i * 120.0f;
      m_WandPanes[i].orderedPanelList = new List<BasePanel>();
    }

    m_WandPaneVisuals = Instantiate(m_WandPaneVisualsPrefab);
    m_WandPaneVisuals.SetActive(false);
    m_WandPaneVisualsMeshRenderer =
        m_WandPaneVisuals.transform.GetChild(0).GetComponent<Renderer>();
    m_WandPaneVisualsState = PaneVisualsState.Hidden;

    Debug.AssertFormat((App.Config.m_SdkMode == SdkMode.Ods) || (m_AdminPanel != null),
        "Admin Panel required.");

    m_PanelsCustomized = false;
    m_AdvancedModeRevealActive = false;

#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      // If we've got a UX exploration prefab, instantiate it here.
      if (m_UxExplorationPrefab != null) {
        m_UxExploration = Instantiate(m_UxExplorationPrefab);
      }
    }
#endif

    TintWandPaneVisuals(true);

    // Set the MipMap bias for mobile builds (as there is more compression on the textures)
    if (App.Config.IsMobileHardware) {
      Shader.SetGlobalFloat("_PanelMipmapBias", m_PanelMipmapBias);
    }
  }

  void CreatePanel(PanelMapKey key, bool advancedPanel) {
    GameObject obj = Instantiate(key.m_PanelPrefab);
    obj.name += advancedPanel ? "_Advanced" : "_Basic";

    BasePanel p = obj.GetComponent<BasePanel>();
    obj.transform.position = p.m_InitialSpawnPos;
    obj.transform.rotation = Quaternion.Euler(p.m_InitialSpawnRotEulers);
    obj.transform.parent = transform;

    if (p.ShouldRegister) {
      // We have to turn on each panel so the Awake() function runs.
      p.gameObject.SetActive(true);
      p.InitAdvancedFlag(advancedPanel);

      // See if this panel has a cached attributes.
      bool attributesFromDisk = false;
      if (advancedPanel) {
        attributesFromDisk = m_CachedPanelLayouts.ApplyLayoutToPanel(p);
      }
      if (!attributesFromDisk) {
        p.m_Fixed = p.BeginFixed;
        // Use defaults for the rest of the attributes.
      }

      // Package this panel up with metadata.
      PanelData newData = new PanelData();
      newData.m_Panel = p;
      newData.m_Widget = p.GetComponent<PanelWidget>();
      newData.m_RestoreFlag = false;

      // Create particle systems for reveal.
      if (!IsPanelUnique(p.Type)) {
        if (p.ParticleBounds != Vector3.zero) {
          GameObject go = (GameObject)Instantiate(m_AdvancedModeRevealParticlePrefab);
          go.transform.parent = m_RevealParticleParent.transform;
          newData.m_RevealParticles = new RevealParticle();
          newData.m_RevealParticles.parentXf = p.transform;
          newData.m_RevealParticles.xf = go.transform;
          newData.m_RevealParticles.emission = go.GetComponent<ParticleSystem>().emission;
          newData.m_RevealParticles.shape = go.GetComponent<ParticleSystem>().shape;
          newData.m_RevealParticles.bounds = p.ParticleBounds;
          newData.m_RevealParticles.baseRate =
              newData.m_RevealParticles.emission.rateOverTimeMultiplier;
          newData.m_RevealParticles.StopEmitting();
        }
      }

      // Add to general list.
      m_AllPanels.Add(newData);

      // Add to custom list.
      if (p.Type == BasePanel.PanelType.Sketchbook ||
          p.Type == BasePanel.PanelType.SketchbookMobile) {
        m_SketchbookPanels.Add(p);
      } else if (p.Type == BasePanel.PanelType.AppSettings ||
          p.Type == BasePanel.PanelType.AppSettingsMobile) {
        m_SettingsPanels.Add(p);
      } else if (p.Type == BasePanel.PanelType.MemoryWarning) {
        m_MemoryWarningPanels.Add(p);
      } else if (p.Type == BasePanel.PanelType.Camera) {
        m_CameraPanels.Add(p);
      } else if (p.Type == BasePanel.PanelType.BrushLab) {
        m_BrushLabPanels.Add(p);
      } else if (IsAdminPanel(p.Type)) {
        Debug.Assert(m_AdminPanel == null, "Multiple Admin Panels are being created.");
        m_AdminPanel = p;
      }

      PanelWidget grabWidget = p.GetComponent<PanelWidget>();
      if (grabWidget != null) {
        grabWidget.enabled = true;
      }
    }
  }

  public void ResetPanel(int index) {
    if (index != -1) {
      m_AllPanels[index].m_Panel.PanelGazeActive(false);
      m_AllPanels[index].m_Panel.SetPositioningPercent(0.0f);
    }
  }

  public void SetInIntroSketchbookMode(bool inIntro) {
    m_IntroSketchbookMode = inIntro;
    foreach (var panel in m_SketchbookPanels) {
      panel.SetInIntroMode(inIntro);
    }
  }

  public void ResetWandPanelRotation() {
    m_WandPanelsOriginAngle = 0;
    m_RotateSnapState = RotateSnapState.Done;
  }

  public void RefreshConfiguredFlag() {
    m_PanelsCustomized = false;

    // Run through the panels and look for anything fishy.
    for (int i = 0; i < m_AllPanels.Count; ++i) {
      // Ignore unique panels-- they can't be configured.
      if (IsPanelUnique(m_AllPanels[i].m_Panel.Type)) {
        continue;
      }

      if (m_AllPanels[i].m_Panel.BeginFixed) {
        // If a panel started fixed, but isn't, we're customized.
        if (!m_AllPanels[i].m_Panel.m_Fixed) {
          m_PanelsCustomized = true;
          break;
        }

        // If a panel that started fixed isn't in the default position, we're customized.
        if (!m_AllPanels[i].m_Panel.IsInInitialPosition()) {
          m_PanelsCustomized = true;
          break;
        }
      } else {
        // If a panel didn't start fixed and is active, we're customized.
        if (m_AllPanels[i].m_Widget) {
          if (m_AllPanels[i].m_Widget.Showing) {
            m_PanelsCustomized = true;
            break;
          }
        } else if (m_AllPanels[i].m_Panel.gameObject.activeSelf) {
          m_PanelsCustomized = true;
          break;
        }
      }
    }
  }

  public void InitPanels(bool bWandPanels) {
    for (int i = 0; i < m_AllPanels.Count; ++i) {
      // Initialize the sweet spot offset distance for Monoscopic mode.
      m_AllPanels[i].m_Panel.m_SweetSpotDistance = m_SweetSpot.m_PanelAttachRadius;
      m_AllPanels[i].m_Panel.InitPanel();
    }

    // Config fixed panels for wand rotation if requested.
    if (bWandPanels) {
      for (int i = 0; i < m_AllPanels.Count; ++i) {
        m_AllPanels[i].m_Panel.m_MaxGazeRotation = 0.0f;
      }
    }

    // Disable panels that aren't available in this mode.
    for (int i = 0; i < m_AllPanels.Count; ++i) {
      BasePanel panel = m_AllPanels[i].m_Panel;
      if (!IsPanelUnique(panel.Type)) {
        PanelWidget widget = m_AllPanels[i].m_Widget;
        if (m_AdvancedPanels != panel.AdvancedModePanel) {
          widget.ForceInvisibleForInit();
        } else if (panel.m_Fixed || IsPanelCore(panel.Type)) { 
          // Prime panels that begin fixed.
          widget.ForceVisibleForInit();
        }
      }
    }

    SetSweetSpotPosition(m_SweetSpot.transform.position);
    RefreshConfiguredFlag();
  }

  // This method is only used on startup to initialize all panels to off.
  public void HidePanelsForStartup() {
    m_MasterScale = 0.0f;
    foreach (PanelData p in GetFixedPanels()) {
      if (p.AvailableInCurrentMode) {
        p.m_Panel.SetScale(0.0f);
      }
    }

    for (int i = 0; i < m_AllPanels.Count; ++i) {
      m_AllPanels[i].m_Panel.gameObject.SetActive(false);
    }

    m_PanelsState = PanelsState.Hidden;
  }

  // This method is used to revive any advanced panels that were floating (not fixed to the
  // wand panes) when the user shut down the last session.  This is required because after
  // the initialization of all panels, we use HidePanelsForStartup() to shrink them all,
  // starting fresh.  Fixed panels are revived when m_PanelManager.SetVisible is called,
  // but floating panels never get revived.
  public void ReviveFloatingPanelsForStartup() {
    if (m_AdvancedPanels) {
      for (int i = 0; i < m_AllPanels.Count; ++i) {
        BasePanel p = m_AllPanels[i].m_Panel;
        if (!IsPanelUnique(p.Type) && p.AdvancedModePanel && !p.m_Fixed) {
          m_CachedPanelLayouts.ReviveFloatingPanelWithLayout(p);
        }
      }
    }
  }

  public void ToggleAdvancedPanels() {
    m_AdvancedPanels ^= true;

    // If we've been in advanced panels before, just spin around once.
    m_AdvancedModeRevealSpinValue = 0.0f;
    m_AdvancedModeRevealSpinTarget = 360.0f;
    m_AdvancedModeRevealSpinFinalAngle = m_WandPanelsOriginAngle;

    // If we haven't been in advanced panels mode before, spin to the tools panel to highlight all
    // the cool stuff that's been unlocked.
    if (m_AdvancedPanels &&
        !PromoManager.m_Instance.HasPromoBeenCompleted(PromoType.AdvancedPanels)) {
      // Find the tools panel.
      for (int i = 0; i < m_AllPanels.Count; ++i) {
        BasePanel panel = m_AllPanels[i].m_Panel;
        if (panel.Type == BasePanel.PanelType.ToolsBasic) {
          // Figure out how far we need to spin to get to the tools panel.
          float currentToAttachAngle = 360.0f - (panel.m_WandAttachAngle + m_WandPanelsOriginAngle);
          if (currentToAttachAngle >= 360.0f) {
            currentToAttachAngle -= 360.0f;
          } else if (currentToAttachAngle < 0.0f) {
            currentToAttachAngle += 360.0f;
          }
          m_AdvancedModeRevealSpinTarget += currentToAttachAngle;

          // Our final angle should be exactly where the tools panel is.
          m_AdvancedModeRevealSpinFinalAngle = m_WandPanelsOriginAngle + currentToAttachAngle;
        }
      }
    }
    m_AdvancedModeRevealActive = true;
    PlayerPrefs.SetInt(kPlayerPrefAdvancedMode, m_AdvancedPanels ? 1 : 0);
    AudioManager.m_Instance.AdvancedModeSwitch(m_AdvancedPanels);
    App.Switchboard.TriggerAdvancedPanelsChanged();

    if (m_AdvancedPanels) {
      for (int i = 0; i < m_AllPanels.Count; ++i) {
        BasePanel panel = m_AllPanels[i].m_Panel;
        if (IsPanelUnique(panel.Type)) {
          continue;
        }
        // Turn on panel if it is an advanced panel.
        if (panel.AdvancedModePanel && panel.CurrentlyVisibleInAdvancedMode) {
          _RestorePanelInternal(i);
          panel.ForceUpdatePanelVisuals();
        }
        // Turn off panel if it is a basic panel.
        if (!panel.AdvancedModePanel) {
          _DismissPanelInternal(i, false);
        }
      }
    } else {
      for (int i = 0; i < m_AllPanels.Count; ++i) {
        BasePanel panel = m_AllPanels[i].m_Panel;
        if (IsPanelUnique(panel.Type)) {
          continue;
        }
        // Turn on panel basic mode panels.
        if (!panel.AdvancedModePanel) {
          _RestorePanelInternal(i);
          panel.ForceUpdatePanelVisuals();
        }
        // Turn off advanced mode panels.
        if (panel.AdvancedModePanel) {
          _DismissPanelInternal(i, false);
        }
      }
    }

    // Always update the admin panel's visuals on swap.
    m_AdminPanel.ForceUpdatePanelVisuals();

    // Enable emitters on fixed panels that are showing up.
    foreach (PanelData p in GetFixedPanels()) {
      if (!p.AvailableInCurrentMode) {
        continue;
      }
      if (p.m_RevealParticles != null) {
        p.m_RevealParticles.UpdateTransform();
        p.m_RevealParticles.StartEmitting();
      }
    }
  }

  IEnumerable<PanelData> GetFixedPanels() {
    return m_AllPanels.Where(p => p.m_Panel.m_Fixed);
  }

  public void SetSweetSpotPosition(Vector3 vSweetSpot) {
    // Run through each fixed panel and set their new position.
    Vector3 vPreviousSweetSpot = m_SweetSpot.transform.position;
    foreach (PanelData p in GetFixedPanels()) {
      // Get previous offset vector.
      Vector3 vPreviousOffset = p.m_Panel.transform.position - vPreviousSweetSpot;
      vPreviousOffset.Normalize();

      // Set as normal.
      p.m_Panel.transform.forward = vPreviousOffset;

      // Extend to radius of sweet spot and set new position.
      vPreviousOffset *= p.m_Panel.m_SweetSpotDistance;
      p.m_Panel.transform.position = vSweetSpot + vPreviousOffset;
    }

    // Set sweet spot to new position.
    m_SweetSpot.transform.position = vSweetSpot;
    OutputWindowScript.m_Instance.UpdateBasePositionHeight(vSweetSpot.y);
  }

  public Vector3 GetSketchSurfaceResetPos() {
    return m_SweetSpot.transform.position + (Vector3.forward * m_SweetSpot.m_PanelAttachRadius) +
        Vector3.down;
  }

  public void UpdateWandOrientationControls() {
    // Bail early if we're in a fancy animation.
    if (m_AdvancedModeRevealActive) {
      return;
    }

    bool bWandRot = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.WandRotation);
    float wandScrollXDelta = InputManager.m_Instance.GetWandScrollAmount();

    // If we're in non-standard panel mode, blank out wand rotation if we're flagged to eat input.
    bool inVisibleAltMode = GazePanelsAreVisible() &&
        (m_PanelsMode == PanelMode.Sketchbook ||
          m_PanelsMode == PanelMode.Settings ||
          m_PanelsMode == PanelMode.MemoryWarning ||
          m_PanelsMode == PanelMode.Camera);
    if (inVisibleAltMode && App.VrSdk.AnalogIsStick(InputManager.ControllerName.Wand)) {
      if (m_AltModeSwipeEatStickInput) {
        m_AltModeSwipeEatStickInput = Mathf.Abs(wandScrollXDelta) > 0.0f;
        bWandRot = false;
      }
    }

    if (!App.VrSdk.AnalogIsStick(InputManager.ControllerName.Wand)) {
      UpdateSwipeRotate(bWandRot, wandScrollXDelta);
    } else {
      UpdateSnapRotate(bWandRot, wandScrollXDelta);
    }

    // Look for swipe to exit an alt mode. Don't exit if gallery is in intro mode.
    if (inVisibleAltMode) {
      if (bWandRot) {
        // Don't allow swipe to dismiss for memory warning panels.
        if (m_PanelsMode != PanelMode.MemoryWarning) {
          m_AltModeSwipeAmount += wandScrollXDelta;
          if (Mathf.Abs(m_AltModeSwipeAmount) > m_AltModeSwipeThreshold) {
            // Get back to standard.
            if (m_PanelsMode == PanelMode.Settings) {
              ToggleSettingsPanels();
            } else if (m_PanelsMode == PanelMode.Sketchbook) {
              ToggleSketchbookPanels();
            } else if (m_PanelsMode == PanelMode.Camera) {
              ToggleCameraPanels();
            }
          }
        }
      } else {
        m_AltModeSwipeAmount = 0.0f;
      }
    }
  }

  private void UpdateSnapRotate(bool bWandRot, float wandScrollXDelta) {
    const float kSnapStickMultiplier = 0.25f;

    float wandSnapPercent = App.VrSdk.VrControls.WandRotateJoystickPercent;
    bool bWandSnap = Mathf.Abs(wandScrollXDelta) > wandSnapPercent;

    float offsetAngle = 0;

    switch (m_RotateSnapState) {
    case RotateSnapState.Snapping:
      // How fast it snaps to next position.
      const float kSnapSpeed = 10f;
      // How far to turn to get to the next pannel.
      const float kSnapRotationAngle = 360f / 3f;

      // Play audio on enter.
      if (m_RotateSnapAnimTime == 1.0f) {
        AudioManager.m_Instance.PanelFlip(
          InputManager.m_Instance.GetControllerPosition(InputManager.ControllerName.Wand));
      }

      // Compensate for the initial rotation before the snap occured.
      float init = Mathf.Abs(wandScrollXDelta * m_WandPanelsRotationScalar * kSnapStickMultiplier);

      // Get the animated target angle, compensated for initial rotation.
      offsetAngle = init + (kSnapRotationAngle - init) * (1f - m_RotateSnapAnimTime);
      // Increment time.
      m_RotateSnapAnimTime -= Time.deltaTime * kSnapSpeed;
      // Apply the angle to the temproary offset, the final accumulated rotation is only applied
      // once the animation is done.
      UpdateWandPanelsOriginAngle(0, m_RotateSnapAnimDir * offsetAngle);

      if (m_RotateSnapAnimTime <= 0f) {
        // Apply the accumulated rotation, remove the offset.
        UpdateWandPanelsOriginAngle(m_RotateSnapAnimDir * kSnapRotationAngle);
        // Start phase 2 jiggle.
        m_RotateSnapAnimTime = 1.0f;
        m_RotateSnapState = RotateSnapState.Jiggling;
      }
      return;

    case RotateSnapState.Jiggling:
      const float kJiggleDecay = 6f; // How fast the overall effect falls off.
      const float kJiggleFreq = 60f; // How fast the jiggle travels / oscillates while animating.
      const float kJiggleAmt = 2f;   // How far the oscillation travels / max displacement.

      // RotateSnapAnimTime goes from 1.0 to 0.0
      float falloff = m_RotateSnapAnimTime;

      offsetAngle = Mathf.Sin(Time.time * kJiggleFreq) * kJiggleAmt * falloff * m_RotateSnapAnimDir;

      UpdateWandPanelsOriginAngle(0, offsetAngle);

      // Dampen the jiggle over time according to how much time is left and decay.
      m_RotateSnapAnimTime -= Time.deltaTime * kJiggleDecay;

      if (m_RotateSnapAnimTime <= 0f) {
        m_RotateSnapAnimTime = m_RotateSnapRestTime;
        m_RotateSnapState = RotateSnapState.Resting;
      }
      return;

    case RotateSnapState.Resting:
      // Explicit delay until next rotation
      m_RotateSnapAnimTime -= Time.deltaTime;
      if (m_RotateSnapAnimTime <= 0f) {
        m_RotateSnapState = RotateSnapState.Done;
      }
      return;

    case RotateSnapState.Done:
      if (App.VrSdk.AnalogIsStick(InputManager.ControllerName.Wand)
            && bWandSnap
            && bWandRot
            && GazePanelsAreVisible()
            && (m_PanelsMode == PanelMode.Standard)) {
        // Threshold hit, apply snap rotation.
        m_RotateSnapAnimTime = 1.0f;
        m_RotateSnapAnimDir = Mathf.Sign(-wandScrollXDelta);
        m_RotateSnapState = RotateSnapState.Snapping;
      } else {
        // Partially rotate the sketchbook, until the threshold is hit.
        offsetAngle = -wandScrollXDelta *
                m_WandPanelsRotationScalar *
                kSnapStickMultiplier;
        UpdateWandPanelsOriginAngle(0, offsetAngle);
      }
      return;
    }
  }

  private void UpdateSwipeRotate(bool bWandRot, float wandScrollXDelta) {
    if (bWandRot && GazePanelsAreVisible() && (m_PanelsMode == PanelMode.Standard)) {
      for (int i = 1; i < m_WandPanelsRotationDiffCount; ++i) {
        m_WandPanelsRotationDiffHistory[i - 1] = m_WandPanelsRotationDiffHistory[i];
      }

      m_WandPanelsRotationDiffHistory[m_WandPanelsRotationDiffIndex] = wandScrollXDelta;
      float fOriginAngleAdjust = m_WandPanelsRotationDiffHistory[m_WandPanelsRotationDiffIndex] * m_WandPanelsRotationScalar;

      UpdateWandPanelsOriginAngle(-fOriginAngleAdjust);
      m_WandPanelsRotationVelocity = 0.0f;

      ++m_WandPanelsRotationDiffIndex;
      m_WandPanelsRotationDiffIndex = Mathf.Min(m_WandPanelsRotationDiffIndex, m_WandPanelsRotationDiffCount - 1);
    } else if (m_WandPanelsRotationDiffIndex > 0) {
      float fAvgDiff = 0.0f;
      for (int i = 0; i < m_WandPanelsRotationDiffIndex; ++i) {
        fAvgDiff += m_WandPanelsRotationDiffHistory[i];
      }
      if (m_WandPanelsRotationDiffIndex > 0) {
        fAvgDiff /= (float)m_WandPanelsRotationDiffIndex;
      }

      m_WandPanelsRotationVelocity = -fAvgDiff * m_WandPanelsRotationVelocityScalar;
      m_WandPanelsRotationDiffIndex = 0;
    }
  }

  void UpdateWandPanelsOriginAngle(float fDiff, float fOffset = 0) {
    m_WandPanelsOriginAngleOffset = fOffset;
    m_WandPanelsOriginAngle = (m_WandPanelsOriginAngle + fDiff) % 360.0f;

    // Get periodic difference of last feedback angle and current origin angle.
    float fAngleDelta = (m_WandPanelsRotationLastFeedbackAngle - m_WandPanelsOriginAngle) % 360.0f;
    if (fAngleDelta < 0) { fAngleDelta += 360.0f; }
    if (fAngleDelta >= 180.0f) { fAngleDelta -= 360.0f; }

    if (Mathf.Abs(fAngleDelta) > m_WandPanelsRotationFeedbackInterval) {
      if (!App.VrSdk.AnalogIsStick(InputManager.ControllerName.Wand)) {
        InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Wand, 0.05f);
      }
      m_WandPanelsRotationLastFeedbackAngle = m_WandPanelsOriginAngle;
    }
  }

  public void UpdatePanels() {
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      if (m_UxExploration != null) {
        LockUxExplorationToController();
        return;
      }
    }
#endif

    UnityEngine.Profiling.Profiler.BeginSample("PanelManager.UpdatePanels");
    // Lock panels to the controller if we've got 6dof controls.
    if (SketchControlsScript.m_Instance.ActiveControlsType ==
        SketchControlsScript.ControlsType.SixDofControllers) {
      LockPanelsToController();
    }
    UnityEngine.Profiling.Profiler.EndSample();
  }

  float GetWandCircumference() {
    float fCircumference = 0.0f;
    foreach (PanelData p in GetFixedPanels()) {
      fCircumference += p.m_Panel.GetBounds().x;
    }
    fCircumference *= 2.5f;
    return fCircumference;
  }

  void PanelMovedHaptics() {
    InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Wand, 0.04f);
  }

  public void InitPanesForPanelAttach(bool bPanelStartedFixed) {
    m_WandPanePanelSnapPreviousStep = null;
    m_WandPanePanelWasAttached = bPanelStartedFixed;
  }

  public void ResetWandPanelsConfiguration() {
    for (int i = 0; i < m_AllPanels.Count; ++i) {
      if (m_AllPanels[i].m_Panel.gameObject.activeSelf) {
        m_AllPanels[i].m_Panel.ResetPanel();

        // If the panel begins fixed, restore.
        if (m_AllPanels[i].m_Panel.BeginFixed) {
          m_AllPanels[i].m_Panel.ResetPanelToInitialPosition();
        } else {
          // Otherwise, hide.
          m_AllPanels[i].m_Panel.m_Fixed = false;
          if (m_AllPanels[i].m_Widget != null) {
            m_AllPanels[i].m_Widget.Show(false);
          }
        }
      }
    }
    m_PanelsCustomized = false;
    AdvancedPanelLayouts.ClearPlayerPrefs();
    // We may not be dismissing a panel here, but we want our panel buttons to refresh, so fake it.
    App.Switchboard.TriggerPanelDismissed();
  }

  public void SetFixedPanelsToStableOffsets() {
    foreach (PanelData p in GetFixedPanels()) {
      if (!IsAdminPanel(p.m_Panel.Type)) {
        p.m_Panel.m_WandAttachYOffset_Target = p.m_Panel.m_WandAttachYOffset_Stable;
      }
    }
  }

  public void AttachHeldPanelToWand(BasePanel panel) {
    // Early out if we're not in a mode that supports customization.
    if (m_PanelsMode == PanelMode.Sketchbook ||
        m_PanelsMode == PanelMode.Settings ||
        m_PanelsMode == PanelMode.MemoryWarning ||
        m_PanelsMode == PanelMode.Camera) {
      return;
    }

    // Reset all fixed panels.
    SetFixedPanelsToStableOffsets();

    Transform wandXf = InputManager.Wand.Geometry.MainAxisAttachPoint;
    // Put panel position in wand local space.
    Vector3 panelPos_LS = wandXf.InverseTransformPoint(panel.transform.position);
    float panelHeight = panel.m_WandAttachHalfHeight;
    bool wandAttached = false;
    int nearestPaneIndex = -1;
    float nearestPaneAttachOffset = 0.0f;
    float nearestPaneDist = m_WandSnapDistance;
    float paneHalfWidth = m_WandPaneHalfWidth;
    if (m_WandPanePanelWasAttached) {
      nearestPaneDist += (m_WandSnapDistance * m_WandPanelSnapStickyPercent);
      paneHalfWidth += (m_WandPaneHalfWidth * m_WandPanelSnapStickyPercent);
    }

    if (panel.CanBeDetached) {
      // Figure out what pane we're nearest to.
      for (int i = 0; i < m_WandPanes.Length; ++i) {
        // Ignore a pane if it's facing away from us.
        float fPaneAngle = m_WandPanes[i].angleOffset + m_WandPanelsOriginAngleOffset;
        float fPaneAngleWithAdjust = m_WandPanelsOriginAngle + fPaneAngle;
        Quaternion qRotation = Quaternion.AngleAxis(fPaneAngleWithAdjust, wandXf.forward);
        Quaternion paneOrient = qRotation * wandXf.rotation;
        Vector3 paneForward = paneOrient * Vector3.up;
        if (Vector3.Angle(-paneForward.normalized, ViewpointScript.Gaze.direction) >
            m_WandPaneAttachMaxFacingAngle) {
          continue;
        }

        // Compute pane position.
        Quaternion qRot_LS = Quaternion.AngleAxis(fPaneAngleWithAdjust, Vector3.forward);
        Vector3 vRotatedForward = qRot_LS * Vector3.up;
        Vector3 vPaneCenter_LS = vRotatedForward * m_WandRadius;

        // Project panel position on to pane.
        Vector3 paneToPanel = panelPos_LS - vPaneCenter_LS;
        Vector3 projectedPaneToPanel = Vector3.ProjectOnPlane(paneToPanel, vRotatedForward);

        // If we're projecting too high or low on the pane, disregard.
        if (projectedPaneToPanel.z < m_WandPaneMaxY + m_WandPaneHeightOffset &&
            projectedPaneToPanel.z > m_WandPaneMinY + m_WandPaneHeightOffset) {
          if (Mathf.Abs(projectedPaneToPanel.x) < paneHalfWidth) {
            // Clamp z.
            projectedPaneToPanel.z = Mathf.Clamp(projectedPaneToPanel.z,
                m_WandPaneMinY + m_WandPaneHeightOffset + panelHeight,
                m_WandPaneMaxY + m_WandPaneHeightOffset - panelHeight);

            Vector3 projectedPoint_LS = vPaneCenter_LS + projectedPaneToPanel;
            float distanceToProjectedPoint = Vector3.Distance(panelPos_LS, projectedPoint_LS);
            if (distanceToProjectedPoint < nearestPaneDist) {
              nearestPaneIndex = i;
              nearestPaneDist = distanceToProjectedPoint;
              nearestPaneAttachOffset = projectedPaneToPanel.z;
            }
          }
        }
      }
    } else {
      // If we can't be detached, just lie and say the pane we're on is best.
      for (int i = 0; i < m_WandPanes.Length; ++i) {
        if (panel.m_WandAttachAngle == m_WandPanes[i].angleOffset) {
          // Compute panel position on pane.
          float fPaneAngle = m_WandPanes[i].angleOffset + m_WandPanelsOriginAngleOffset;
          float fPaneAngleWithAdjust = m_WandPanelsOriginAngle + fPaneAngle;
          Quaternion qRotation = Quaternion.AngleAxis(fPaneAngleWithAdjust, Vector3.forward);
          Vector3 vRotatedForward = qRotation * Vector3.up;
          Vector3 vPaneCenter_LS = vRotatedForward * m_WandRadius;
          Vector3 paneToPanel = panelPos_LS - vPaneCenter_LS;
          Vector3 projectedPaneToPanel = Vector3.ProjectOnPlane(paneToPanel, vRotatedForward);

          projectedPaneToPanel.z = Mathf.Clamp(projectedPaneToPanel.z,
              m_WandPaneMinY + m_WandPaneHeightOffset + panelHeight,
              m_WandPaneMaxY + m_WandPaneHeightOffset - panelHeight);

          nearestPaneIndex = i;
          nearestPaneAttachOffset = projectedPaneToPanel.z;
          break;
        }
      }
    }

    if (nearestPaneIndex >= 0) {
      // Snap to nearest step.
      if (m_WandPanelSnapStepDistance > 0.0f) {
        // Hysteresis for snapping.
        float previousOffset = panel.m_WandAttachYOffset;
        float stickySnapAmount = m_WandPanelSnapStickyPercent * m_WandPanelSnapStepDistance;
        float offsetDiffFromLastFrame = nearestPaneAttachOffset - previousOffset;
        if (Mathf.Abs(nearestPaneAttachOffset - previousOffset) < stickySnapAmount) {
          nearestPaneAttachOffset = previousOffset;
        } else {
          nearestPaneAttachOffset -= stickySnapAmount * Mathf.Sign(offsetDiffFromLastFrame);
        }

        // Normalize our offset for a nice, snappy feel.
        float normalizedOffset = nearestPaneAttachOffset / m_WandPanelSnapStepDistance;
        int iNumSteps = Mathf.RoundToInt(normalizedOffset);
        nearestPaneAttachOffset = iNumSteps * m_WandPanelSnapStepDistance;

        // Audio for sliding around on pane.
        if (m_WandPanePanelWasAttached && m_WandPanePanelSnapPreviousStep != null &&
            m_WandPanePanelSnapPreviousStep != iNumSteps) {
          AudioManager.m_Instance.PlayPanelPaneMoveSound(panel.transform.position);
          Invoke("PanelMovedHaptics", m_WandPaneHapticDelay);
        }
        m_WandPanePanelSnapPreviousStep = iNumSteps;
      }

      // Scoot this panel into the pane.
      bool bRoomAvailable = true;
      bool pureOverlap = false;
      WandPane pane = m_WandPanes[nearestPaneIndex];
      if (pane.orderedPanelList.Count > 0) {
        float panelBottom = nearestPaneAttachOffset - panelHeight;
        float bottomLimit = m_WandPaneMinY + m_WandPaneHeightOffset;

        int iInsertIndex = pane.orderedPanelList.Count;
        for (int i = 0; i < pane.orderedPanelList.Count; ++i) {
          BasePanel other = pane.orderedPanelList[i];

          // A "pure overlap" occurs when the panel we're trying to attach is directly overtop
          // another panel.  This leads to special case inserting logic.
          pureOverlap = pureOverlap ||
              nearestPaneAttachOffset == other.m_WandAttachYOffset_Stable;

          // A "fake pure overlap" occurs when the panel is at the far low extreme, and another
          // is also at the far low extreme.  In this case, we want to skip the insert check to
          // spoof our location a bit higher.
          float otherBottom = other.m_WandAttachYOffset_Target - other.m_WandAttachHalfHeight;
          bool fakePureOverlap = (otherBottom == bottomLimit) && (panelBottom == bottomLimit);

          if (!fakePureOverlap && nearestPaneAttachOffset > other.m_WandAttachYOffset_Stable) {
            iInsertIndex = i;
            break;
          }
        }

        // Try scooting panels above our insert position up, and panels below our insert position
        // down.  If either fails, flag that there's no room available.
        bool bUpValid = true;
        bool bDownValid = true;

        // In the event of a "pure" overlap, deciding whether we should scoot the overlapped
        // panel up or down is ambiguous, so try scooting it up first.
        if (pureOverlap) {
          bUpValid = MovePanePanelsUpToFit(pane, panelHeight, nearestPaneAttachOffset, iInsertIndex);

          // If scooting up immediately fails, reset state and try scooting again with our insert
          // position adjusted.
          if (!bUpValid) {
            SetFixedPanelsToStableOffsets();
            iInsertIndex = Mathf.Max(iInsertIndex - 1, 0);
            bUpValid = MovePanePanelsUpToFit(pane, panelHeight, nearestPaneAttachOffset, iInsertIndex);
          }
          // Scoot the panels below us down.
          bDownValid = MovePanePanelsDownToFit(pane, panelHeight, nearestPaneAttachOffset, iInsertIndex);
        } else {
          bUpValid = MovePanePanelsUpToFit(pane, panelHeight, nearestPaneAttachOffset, iInsertIndex);
          bDownValid = MovePanePanelsDownToFit(pane, panelHeight, nearestPaneAttachOffset, iInsertIndex);
        }

        bRoomAvailable = bUpValid && bDownValid;
      }

      // Snap to this pane.
      panel.m_WandAttachAngle = m_WandPanes[nearestPaneIndex].angleOffset;
      panel.m_WandAttachYOffset = nearestPaneAttachOffset;
      panel.m_WandAttachYOffset_Target = nearestPaneAttachOffset;
      panel.m_WandAttachRadiusAdjust = m_WandRadiusManipulationAdjust;
      SetPanelXfFromWand(panel, wandXf, m_WandPanelsOriginAngle,
          m_WandPanelsOriginAngleOffset, m_WandRadius);
      panel.m_WandPrimedForAttach = bRoomAvailable;
      panel.WidgetSibling.SetActiveTintToShowError(!bRoomAvailable);
      wandAttached = true;

      // Show pane visuals and set it in position.
      if (m_WandPaneVisualsState != PaneVisualsState.Showing) {
        m_WandPaneVisualsState = PaneVisualsState.HiddenToShowing;
      }

      TintWandPaneVisuals(bRoomAvailable);

      Vector3 vBaseOffset = wandXf.up * m_WandPaneVisualOffsetRadius;
      vBaseOffset += wandXf.forward * m_WandPaneHeightOffset;
      float fAngle = panel.m_WandAttachAngle + m_WandPanelsOriginAngleOffset;
      Quaternion qRotation = Quaternion.AngleAxis(m_WandPanelsOriginAngle + fAngle, wandXf.forward);
      Vector3 vRotatedOffset = qRotation * vBaseOffset;
      m_WandPaneVisuals.transform.position = wandXf.position + vRotatedOffset;
      m_WandPaneVisuals.transform.rotation = qRotation * wandXf.rotation;
    } else {
      // Flag as not attached, so when the user stops interaction, we know to leave this alone.
      panel.m_WandPrimedForAttach = false;
      panel.m_WandAttachRadiusAdjust = 0.0f;
      panel.WidgetSibling.SetActiveTintToShowError(false);

      TintWandPaneVisuals(true);

      ResetPaneVisuals();
      ClosePanePanelGaps();
    }

    // Audio for attaching/detaching.
    if (wandAttached != m_WandPanePanelWasAttached) {
      AudioManager.m_Instance.PlayPanelPaneAttachSound(panel.transform.position);
      Invoke("PanelMovedHaptics", m_WandPaneHapticDelay);
      InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.1f);
    }
    m_WandPanePanelWasAttached = wandAttached;
  }

  bool MovePanePanelsUpToFit(WandPane pane, float panelHeight, float attachOffset, int insertIndex) {
    // Work backward from our insert index and scoot panels up.
    bool bRoomAvailable = true;
    float scootAmount = panelHeight;
    float pusherOffset = attachOffset;
    for (int i = insertIndex - 1; i >= 0; --i) {
      BasePanel scootPanel = pane.orderedPanelList[i];

      // Add the height of the panel we're moving.
      scootAmount += scootPanel.m_WandAttachHalfHeight;

      // Subtract the difference in positions.
      scootAmount -= (scootPanel.m_WandAttachYOffset_Stable - pusherOffset);

      if (scootAmount <= 0.0f) {
        break;
      } else {
        // Pay it forward.
        scootPanel.m_WandAttachYOffset_Target += scootAmount;

        // Check to see if this panel has been scooted too far.
        float newTop = scootPanel.m_WandAttachYOffset_Target + scootPanel.m_WandAttachHalfHeight;
        float topLimit = 2.0f * m_WandPaneMaxY + m_WandPaneHeightOffset;
        if (i == 0 && newTop > topLimit) {
          // If it has, rewind it back to bump up against the top smoothly.
          scootPanel.m_WandAttachYOffset_Target = topLimit - scootPanel.m_WandAttachHalfHeight;
          bRoomAvailable = false;

          // We moved too far.  We have to go tell everyone that.
          float spillover = newTop - topLimit;
          for (int j = i + 1; j < insertIndex; ++j) {
            BasePanel unscootPanel = pane.orderedPanelList[j];
            unscootPanel.m_WandAttachYOffset_Target -= spillover;
          }
        }

        pusherOffset = scootPanel.m_WandAttachYOffset_Target;
        scootAmount = scootPanel.m_WandAttachHalfHeight;
      }
    }
    return bRoomAvailable;
  }

  bool MovePanePanelsDownToFit(WandPane pane, float panelHeight, float attachOffset, int insertIndex) {
    // Work forward from our insert index and scoot panels down.
    bool bRoomAvailable = true;
    float scootAmount = panelHeight;
    float pusherOffset = attachOffset;
    for (int i = insertIndex; i < pane.orderedPanelList.Count; ++i) {
      BasePanel scootPanel = pane.orderedPanelList[i];

      // Add the height of the panel we're moving.
      scootAmount += scootPanel.m_WandAttachHalfHeight;

      // Subtract the difference in positions.
      scootAmount -= (pusherOffset - scootPanel.m_WandAttachYOffset_Stable);

      if (scootAmount <= 0.0f) {
        break;
      } else {
        // Pay it forward.
        scootPanel.m_WandAttachYOffset_Target -= scootAmount;

        // Check to see if this panel has been scooted too far.
        float newBottom = scootPanel.m_WandAttachYOffset_Target - scootPanel.m_WandAttachHalfHeight;
        float bottomLimit = m_WandPaneMinY + m_WandPaneHeightOffset;
        if (i == pane.orderedPanelList.Count - 1 && newBottom < bottomLimit) {
          // If it has, rewind it back to bump up against the bottom smoothly.
          scootPanel.m_WandAttachYOffset_Target = bottomLimit + scootPanel.m_WandAttachHalfHeight;
          bRoomAvailable = false;

          // We moved too far.  We have to go tell everyone that.
          float spillover = bottomLimit - newBottom;
          for (int j = i - 1; j >= insertIndex; --j) {
            BasePanel unscootPanel = pane.orderedPanelList[j];
            unscootPanel.m_WandAttachYOffset_Target += spillover;
          }
        }
        pusherOffset = scootPanel.m_WandAttachYOffset_Target;
        scootAmount = scootPanel.m_WandAttachHalfHeight;
      }
    }
    return bRoomAvailable;
  }


  public void ClosePanePanelGaps() {
    OrderPanes();
    foreach (WandPane pane in m_WandPanes) {
      if (pane.orderedPanelList.Count == 0) { continue;}

      // See if we exceed any limits.
      BasePanel topPanel = pane.orderedPanelList[0];
      BasePanel bottomPanel = pane.orderedPanelList[pane.orderedPanelList.Count - 1];
      float topLimit = m_WandPaneMaxY + m_WandPaneHeightOffset;
      float bottomLimit = m_WandPaneMinY + m_WandPaneHeightOffset;
      float top = topPanel.m_WandAttachYOffset_Target + topPanel.m_WandAttachHalfHeight;
      float bottom = bottomPanel.m_WandAttachYOffset_Target - bottomPanel.m_WandAttachHalfHeight;
      if (top <= topLimit && bottom >= bottomLimit) { continue;}

      // Add up the amount of gap between all the panels.
      float gapAmount = 0;
      float lastBottom = topPanel.m_WandAttachYOffset_Target - topPanel.m_WandAttachHalfHeight;
      for (int i = 1; i < pane.orderedPanelList.Count; ++i) {
        var panel = pane.orderedPanelList[i];
        float thisTop = panel.m_WandAttachYOffset_Target + panel.m_WandAttachHalfHeight;
        gapAmount += lastBottom - thisTop;
        lastBottom = panel.m_WandAttachYOffset_Target - panel.m_WandAttachHalfHeight;
      }
      if (bottom > bottomLimit) {
        gapAmount += bottom - bottomLimit;
      }
      if (top < topLimit) {
        gapAmount += topLimit - top;
      }

      // Work from the bottom and scoot panels up.
      if (bottom < bottomLimit) {
        float nextBottom = bottom + gapAmount;
        if (nextBottom > bottomLimit) {
          // The full gap isn't required to fit the panels up.
          float extraGap = nextBottom - bottomLimit;
          gapAmount = extraGap;
          nextBottom -= extraGap;
        } else {
          gapAmount = 0;
        }

        for (int i = pane.orderedPanelList.Count - 1; i >= 0; --i) {
          var panel = pane.orderedPanelList[i];
          float newTarget = nextBottom + panel.m_WandAttachHalfHeight;
          if (newTarget > panel.m_WandAttachYOffset_Target) {
            // New target is higher, so scoot up.
            panel.m_WandAttachYOffset_Target = newTarget;
          } else {
            // New target isn't higher, so we don't have to scoot any more.
            break;
          }
          nextBottom += 2 * panel.m_WandAttachHalfHeight;
        }
      }

      // Work from the top and scoot panels down.
      if (top > topLimit) {
        float nextTop = top - gapAmount;
        if (nextTop < topLimit) {
          // The full gap isn't required to fit the panels up.
          float extraGap = topLimit - nextTop;
          gapAmount = extraGap;
          nextTop += extraGap;
        } else {
          gapAmount = 0;
        }

        for (int i = 0; i < pane.orderedPanelList.Count; ++i) {
          var panel = pane.orderedPanelList[i];
          float newTarget = nextTop - panel.m_WandAttachHalfHeight;
          if (newTarget < panel.m_WandAttachYOffset_Target) {
            // New target is lower, so scoot down.
            panel.m_WandAttachYOffset_Target = newTarget;
          } else {
            // New target isn't lower, so we don't have to scoot any more.
            break;
          }
          nextTop -= 2 * panel.m_WandAttachHalfHeight;
        }
      }
    }

    // After closing the gaps, register these new panel positions as the stable positions.
    for (int i = 0; i < m_AllPanels.Count; ++i) {
      if (m_AllPanels[i].m_Widget != null) {
        m_AllPanels[i].m_Panel.SetPanelStableToTarget();
      }
    }

    // Write this config to disk.
    if (m_AdvancedPanels) {
      m_CachedPanelLayouts.WriteToDisk(m_AllPanels);
    }
  }

  void TintWandPaneVisuals(bool ok) {
    m_WandPaneVisualsMeshRenderer.material.color = ok ?
        m_WandPaneVisualsColorOK : m_WandPaneVisualsColorBad;
  }

  public void ResetPaneVisuals() {
    if (m_WandPaneVisualsState != PaneVisualsState.Hidden) {
      m_WandPaneVisualsState = PaneVisualsState.ShowingToHidden;
    }
  }

  public void LockUxExplorationToController() {
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      Transform baseTransform = InputManager.Wand.Geometry.MainAxisAttachPoint;
      m_UxExploration.transform.position = baseTransform.position;
      m_UxExploration.transform.rotation = baseTransform.rotation;
    }
#endif
  }

  public void LockPanelsToController() {
    Transform rBaseTransform = InputManager.Wand.Geometry.MainAxisAttachPoint;

    float fStep = m_WandPanelsRotationVelocity * Time.deltaTime;
    if (Mathf.Abs(fStep) > 0.0001f) {
      m_WandPanelsRotationVelocity *= Mathf.Max(1.0f - (m_WandPanelsRotationDecay * Time.deltaTime), 0.0f);
      UpdateWandPanelsOriginAngle(fStep);
    } else {
      m_WandPanelsRotationVelocity = 0.0f;
    }

    foreach (PanelData p in GetFixedPanels()) {
      SetPanelXfFromWand(p.m_Panel, rBaseTransform, m_WandPanelsOriginAngle,
          m_WandPanelsOriginAngleOffset, m_WandRadius);
    }

    // Keep alt panels locked in place if we're not in standard mode.
    // No point in paying for them if we're not using them.
    if (m_PanelsMode != PanelMode.Standard) {
      for (int i = 0; i < m_SketchbookPanels.Count; ++i) {
        SetAltPanelXfFromWand(m_SketchbookPanels[i], rBaseTransform);
      }

      for (int i = 0; i < m_SettingsPanels.Count; ++i) {
        SetAltPanelXfFromWand(m_SettingsPanels[i], rBaseTransform);
      }

      for (int i = 0; i < m_MemoryWarningPanels.Count; ++i) {
        SetAltPanelXfFromWand(m_MemoryWarningPanels[i], rBaseTransform);
      }

      for (int i = 0; i < m_CameraPanels.Count; ++i) {
        SetAltPanelXfFromWand(m_CameraPanels[i], rBaseTransform);
      }

      for (int i = 0; i < m_BrushLabPanels.Count; ++i) {
        SetAltPanelXfFromWand(m_BrushLabPanels[i], rBaseTransform);
      }
    }

    // Keep admin panel locked.
    SetPanelXfFromWand(m_AdminPanel, rBaseTransform, 0.0f, 0.0f,
        m_AdminPanelWandRadius, true);
  }

  public void SetPanelXfFromWand(BasePanel panel, Transform wandTransform,
      float originAngle, float originOffset, float radius, bool ignoreReveal = false) {
    Vector3 vBaseOffset = wandTransform.up * (radius + panel.m_WandAttachRadiusAdjust);
    float revealAngle = ignoreReveal ? 0.0f : m_AdvancedModeRevealSpinValue;
    float fAngle = panel.m_WandAttachAngle + revealAngle + originOffset;
    Quaternion qRotation = Quaternion.AngleAxis(originAngle + fAngle, wandTransform.forward);
    Vector3 vPanelOffset = vBaseOffset + wandTransform.forward * panel.m_WandAttachYOffset;
    Vector3 vRotatedOffset = qRotation * vPanelOffset;
    panel.transform.position = wandTransform.position + vRotatedOffset;

    Quaternion qPanelOrient = qRotation * wandTransform.rotation;
    Quaternion qPanelAdjust = Quaternion.Euler(90.0f, 0.0f, 0.0f);
    panel.transform.rotation = qPanelOrient * qPanelAdjust;
  }

  void SetAltPanelXfFromWand(BasePanel panel, Transform wandTransform) {
    float fTeaseAngle = (m_AltModeSwipeAmount / m_AltModeSwipeThreshold) * m_AltModeSwipeTeaseAmount;
    float fTotalAngle = m_WandPanelsSketchbookOriginAngle - fTeaseAngle;
    Quaternion qRotation = Quaternion.AngleAxis(fTotalAngle, wandTransform.forward);

    Vector3 sketchbookBaseOffset = wandTransform.up * m_SketchbookOffset.z;
    Vector3 vPanelOffset = sketchbookBaseOffset +
        (wandTransform.forward * m_SketchbookOffset.y);
    Vector3 vRotatedOffset = qRotation * vPanelOffset;
    panel.transform.position = wandTransform.position + vRotatedOffset;

    Quaternion qPanelOrient = qRotation * wandTransform.rotation;
    Quaternion qPanelAdjust = Quaternion.Euler(m_SketchbookRotation);
    panel.transform.rotation = qPanelOrient * qPanelAdjust;
  }

  public void UpdateWandTransitionXf(BasePanel panel, TrTransform target, float percent) {
    Transform rBaseTransform = InputManager.Wand.Geometry.MainAxisAttachPoint;
    SetPanelXfFromWand(panel, rBaseTransform, m_WandPanelsOriginAngle,
        m_WandPanelsOriginAngleOffset, m_WandRadius);
    panel.transform.position =
        Vector3.Lerp(panel.transform.position, target.translation, percent);
    panel.transform.rotation =
        Quaternion.Slerp(panel.transform.rotation, target.rotation, percent);
  }

  public Vector3 GetFixedPanelPosClosestToPoint(Vector3 vPos) {
    int iBestIndex = -1;
    float fBestDistance = 99999.0f;
    List<PanelData> fixedPanels = GetFixedPanels().ToList();
    for (int i = 0; i < fixedPanels.Count; ++i) {
      Vector3 vToPanel = vPos - fixedPanels[i].m_Panel.transform.position;
      float fDist = vToPanel.sqrMagnitude;
      if (fDist < fBestDistance) {
        fBestDistance = fDist;
        iBestIndex = i;
      }
    }

    if (iBestIndex != -1) {
      return fixedPanels[iBestIndex].m_Panel.transform.position;
    }
    return Vector3.zero;
  }

  public void SetVisible(bool bVisible) {
    if (bVisible) {
      // Enter if we're disabled.
      if (m_PanelsState == PanelsState.Exiting || m_PanelsState == PanelsState.Hidden) {
        m_PanelsState = PanelsState.Entering;
      }
    } else {
      // Prep panels for exit if they're enabled.
      if (m_PanelsState == PanelsState.Entering || m_PanelsState == PanelsState.Visible) {
        foreach (PanelData p in GetFixedPanels()) {
          p.m_Panel.ResetPanel();
        }
        for (int i = 0; i < m_SketchbookPanels.Count; ++i) {
          m_SketchbookPanels[i].ResetPanel();
        }
        for (int i = 0; i < m_SettingsPanels.Count; ++i) {
          m_SettingsPanels[i].ResetPanel();
        }
        for (int i = 0; i < m_MemoryWarningPanels.Count; ++i) {
          m_MemoryWarningPanels[i].ResetPanel();
        }
        for (int i = 0; i < m_CameraPanels.Count; ++i) {
          m_CameraPanels[i].ResetPanel();
        }
        for (int i = 0; i < m_BrushLabPanels.Count; ++i) {
          m_BrushLabPanels[i].ResetPanel();
        }

        m_PanelsState = PanelsState.Exiting;
      }
    }
  }

  public void ShowIntroSketchbookPanels() {
    m_PanelsMode = PanelMode.StandardToSketchbook;
    m_WandPanelsSketchbookOriginAngle = 0;
    m_AltModeSwipeAmount = 0.0f;
    for (int i = 0; i < m_SketchbookPanels.Count; ++i) {
      m_SketchbookPanels[i].ResetPanel();
    }
    SetInIntroSketchbookMode(true);
  }

  public void ToggleSketchbookPanels(bool isLoadingSketch = false) {
    // We only want to default to the color picker if you load a sketch directly from the gallery
    // panel when you first start the program.
    if (isLoadingSketch && m_FirstSketchLoad) {
      m_WandPanelsOriginAngle = m_WandPanelsInitialSketchLoadOriginAngle;
    }
    // Always set this to false as we count a new sketch as the first sketch load.
    m_FirstSketchLoad = false;
    ToggleMode(m_SketchbookPanels, PanelMode.Sketchbook, PanelMode.StandardToSketchbook, PanelMode.SketchbookToStandard);
  }

  public void ToggleSettingsPanels() {
    ToggleMode(m_SettingsPanels, PanelMode.Settings, PanelMode.StandardToSettings,
        PanelMode.SettingsToStandard);
  }

  public void ToggleCameraPanels() {
    ToggleMode(m_CameraPanels, PanelMode.Camera, PanelMode.StandardToCamera,
        PanelMode.CameraToStandard);
  }

  public void ToggleBrushLabPanels() {
    ToggleMode(m_BrushLabPanels, PanelMode.BrushLab, PanelMode.StandardToBrushLab,
        PanelMode.BrushLabToStandard);
  }

  public void ToggleMemoryWarningMode() {
    if (m_PanelsMode == PanelMode.MemoryWarning) {
      ToggleMode(m_MemoryWarningPanels, PanelMode.MemoryWarning, PanelMode.MemoryWarning,
          PanelMode.MemoryWarningToStandard);
    } else {
      // Transitioning to Warning Mode can come from anywhere, so hard slam the state.
      ForceModeScale(PanelMode.MemoryWarning);
      RefreshPanelsForAnimations();
      m_AltModeSwipeEatStickInput = true;
      m_PanelsMode = PanelMode.MemoryWarning;
    }
  }

  // This function toggles between the 'mode' parameter and PanelMode.Standard.  Currently,
  // transitions from a non-Standard mode to another non-Standard mode are not allowed.
  // toMode and fromMode define the transition modes to mode.
  void ToggleMode(List<BasePanel> panels, PanelMode mode, PanelMode toMode, PanelMode fromMode) {
    if (panels.Count > 0) {
      // In "standard" mode, transition to target mode.
      if (m_PanelsMode == PanelMode.Standard ||
          m_PanelsMode == PanelMode.SketchbookToStandard ||
          m_PanelsMode == PanelMode.SettingsToStandard ||
          m_PanelsMode == PanelMode.CameraToStandard ||
          m_PanelsMode == PanelMode.BrushLabToStandard) {
        // If we're in full standard mode, reset the panels before we shrink 'em down.
        if (m_PanelsMode == PanelMode.Standard) {
          foreach (PanelData p in GetFixedPanels()) {
            p.m_Panel.ResetPanel();
          }
        }

        // If we're hidden, jump to the new state.  If not, transition.
        if (m_PanelsState == PanelsState.Hidden) {
          ForceModeScale(mode);
          RefreshPanelsForAnimations();
          m_AltModeSwipeEatStickInput = true;
          m_PanelsMode = mode;
        } else {
          m_PanelsMode = toMode;
        }

        m_AltModeSwipeAmount = 0.0f;
        ComputeWandPanelSketchbookOriginAngleFromHead();
      } else if (m_PanelsMode == mode || m_PanelsMode == toMode) {
        // If we're in full target mode, reset the panels before we shrink 'em down.
        if (m_PanelsMode == mode) {
          for (int i = 0; i < panels.Count; ++i) {
            panels[i].ResetPanel();
          }
        }

        // In target mode, transition to standard.
        if (m_PanelsState == PanelsState.Hidden) {
          ForceModeScale(PanelMode.Standard);
          RefreshPanelsForAnimations();
          m_PanelsMode = PanelMode.Standard;
        } else {
          m_PanelsMode = fromMode;
        }
      } else {
        // We're trying to transition to/from target when we're in an unallowed mode.
        Debug.LogErrorFormat("Trying to toggle {0} mode while in unallowed mode {1}",
            mode.ToString(), m_PanelsMode.ToString());
      }
    }
  }

  void ForceModeScale(PanelMode mode) {
    m_SketchbookScale = 0.0f;
    m_SettingsScale = 0.0f;
    m_CameraScale = 0.0f;
    m_StandardScale = 0.0f;
    m_BrushLabScale = 0.0f;
    m_MemoryWarningScale = 0.0f;

    switch (mode) {
    case PanelMode.Standard: m_StandardScale = 1.0f; break;
    case PanelMode.Sketchbook: m_SketchbookScale = 1.0f; break;
    case PanelMode.Settings: m_SettingsScale = 1.0f; break;
    case PanelMode.MemoryWarning: m_MemoryWarningScale = 1.0f; break;
    case PanelMode.Camera: m_CameraScale = 1.0f; break;
    case PanelMode.BrushLab: m_BrushLabScale = 1.0f; break;
    default: Debug.LogError("PanelManager.ForceModeScale() called with unsupported mode.");
      break;
    }
  }

  void ComputeWandPanelSketchbookOriginAngleFromHead() {
    Transform baseTransform = InputManager.Wand.Geometry.MainAxisAttachPoint;
    Vector3 vToHead = ViewpointScript.Head.position - baseTransform.position;
    Vector3 vProjected = Vector3.ProjectOnPlane(vToHead.normalized, baseTransform.forward);
    Vector3 vCross = Vector3.Cross(vProjected, baseTransform.up);

    m_WandPanelsSketchbookOriginAngle = Vector3.Angle(vProjected, baseTransform.up);
    if (vCross.y > 0.0f) {
      m_WandPanelsSketchbookOriginAngle *= -1.0f;
    }
  }

  void RefreshPanelsForAnimations() {
    float fStandardScale = m_MasterScale * m_StandardScale;
    bool bStandardActive = fStandardScale > 0.0f;
    foreach (PanelData p in GetFixedPanels()) {
      if (p.AvailableInCurrentMode) {
        p.m_Panel.SetScale(fStandardScale);
        p.m_Panel.gameObject.SetActive(bStandardActive);
      }
    }

    SetPanelListScaleAndActive(m_SketchbookPanels, m_SketchbookScale);
    SetPanelListScaleAndActive(m_SettingsPanels, m_SettingsScale);
    SetPanelListScaleAndActive(m_MemoryWarningPanels, m_MemoryWarningScale);
    SetPanelListScaleAndActive(m_CameraPanels, m_CameraScale);
    SetPanelListScaleAndActive(m_BrushLabPanels, m_BrushLabScale);
  }

  void SetPanelListScaleAndActive(List<BasePanel> panels, float scale) {
    float masterScaled = m_MasterScale * scale;
    bool panelsActive = masterScaled > 0.0f;
    for (int i = 0; i < panels.Count; ++i) {
      panels[i].SetScale(masterScaled);
      panels[i].gameObject.SetActive(panelsActive);
    }
  }

  void Update() {
    // Update animations for mode changes.
    switch (m_PanelsMode) {
    case PanelMode.Standard: break;
    case PanelMode.Sketchbook: break;
    case PanelMode.Settings: break;
    case PanelMode.MemoryWarning: break;
    case PanelMode.BrushLab: break;
    case PanelMode.StandardToSketchbook:
      AnimateScaleToMode(ref m_StandardScale, ref m_SketchbookScale, PanelMode.Sketchbook);
      break;
    case PanelMode.SketchbookToStandard:
      AnimateScaleToMode(ref m_SketchbookScale, ref m_StandardScale, PanelMode.Standard);
      break;
    case PanelMode.StandardToSettings:
      AnimateScaleToMode(ref m_StandardScale, ref m_SettingsScale, PanelMode.Settings);
      break;
    case PanelMode.SettingsToStandard:
      AnimateScaleToMode(ref m_SettingsScale, ref m_StandardScale, PanelMode.Standard);
      break;
    case PanelMode.StandardToCamera:
      AnimateScaleToMode(ref m_StandardScale, ref m_CameraScale, PanelMode.Camera);
      break;
    case PanelMode.CameraToStandard:
      AnimateScaleToMode(ref m_CameraScale, ref m_StandardScale, PanelMode.Standard);
      break;
    case PanelMode.StandardToBrushLab:
      AnimateScaleToMode(ref m_StandardScale, ref m_BrushLabScale, PanelMode.BrushLab);
      break;
    case PanelMode.BrushLabToStandard:
      AnimateScaleToMode(ref m_BrushLabScale, ref m_StandardScale, PanelMode.Standard);
      break;
    case PanelMode.MemoryWarningToStandard:
      AnimateScaleToMode(ref m_MemoryWarningScale, ref m_StandardScale, PanelMode.Standard);
      break;
    }

    // Update animations for state changes.
    switch (m_PanelsState) {
    case PanelsState.Entering:
      m_MasterScale = Mathf.Min(m_MasterScale + (m_TransitionSpeed * Time.deltaTime), 1.0f);
      if (m_MasterScale >= 1.0f) {
        m_PanelsState = PanelsState.Visible;
      }
      RefreshPanelsForAnimations();
      break;
    case PanelsState.Exiting:
      m_MasterScale = Mathf.Max(m_MasterScale - (m_TransitionSpeed * Time.deltaTime), 0.0f);
      if (m_MasterScale <= 0.0f) {
        m_PanelsState = PanelsState.Hidden;
      }
      RefreshPanelsForAnimations();
      break;
    case PanelsState.Hidden: break;
    case PanelsState.Visible: break;
    }

    // Update advanced/basic transitions.
    if (m_AdvancedModeRevealActive) {
      // Keep particles attached to parents.
      foreach (PanelData p in GetFixedPanels()) {
        if (p.m_RevealParticles != null) {
          p.m_RevealParticles.UpdateTransform();
        }
      }

      m_AdvancedModeRevealSpinValue += m_AdvancedModeRevealSpinSpeed * Time.deltaTime;
      if (m_AdvancedModeRevealSpinValue >= m_AdvancedModeRevealSpinTarget) {
        // Once we've completed our spin, fudge our origin value and reset the spin value.
        m_WandPanelsOriginAngle = m_AdvancedModeRevealSpinFinalAngle;
        m_AdvancedModeRevealSpinValue = 0.0f;

        foreach (PanelData p in GetFixedPanels()) {
          if (p.m_RevealParticles != null) {
            p.m_RevealParticles.StopEmitting();
          }
        }
        m_AdvancedModeRevealActive = false;
      }
    }

    // Update animation for pane visuals.
    float fShowStep = m_WandPaneVisualsShowSpeed * Time.deltaTime;
    Vector3 vVisualsScale = Vector3.one;
    switch (m_WandPaneVisualsState) {
    case PaneVisualsState.HiddenToShowing:
      m_WandPaneVisuals.SetActive(true);
      m_WandPaneVisualsShowValue += fShowStep;
      if (m_WandPaneVisualsShowValue >= 1.0f) {
        m_WandPaneVisualsShowValue = 1.0f;
        m_WandPaneVisualsState = PaneVisualsState.Showing;
      }
      vVisualsScale.x = m_WandPaneVisualsShowValue;
      m_WandPaneVisuals.transform.localScale = vVisualsScale;
      break;
    case PaneVisualsState.ShowingToHidden:
      m_WandPaneVisualsShowValue -= fShowStep;
      if (m_WandPaneVisualsShowValue <= 0.0f) {
        m_WandPaneVisualsShowValue = 0.0f;
        m_WandPaneVisualsState = PaneVisualsState.Hidden;
        m_WandPaneVisuals.SetActive(false);
      }
      vVisualsScale.x = m_WandPaneVisualsShowValue;
      m_WandPaneVisuals.transform.localScale = vVisualsScale;
      break;
    case PaneVisualsState.Hidden:
    case PaneVisualsState.Showing:
      break;
    }
  }

  void AnimateScaleToMode(ref float from, ref float to, PanelMode targetMode) {
    to = Mathf.Min(to + (m_TransitionSpeed * Time.deltaTime), 1.0f);
    from = 1.0f - to;
    RefreshPanelsForAnimations();

    if (to >= 1.0f) {
      m_AltModeSwipeEatStickInput = true;
      m_PanelsMode = targetMode;
    }
  }

  public void PrimeCollisionSimForKeyboardMouse() {
    foreach (PanelData p in GetFixedPanels()) {
      p.m_Panel.InitForPanelMovement();
    }
  }

  public void DoCollisionSimulationForKeyboardMouse(BasePanel movingPanel) {
    // Prep for collisions.
    List<PanelData> fixedPanels = GetFixedPanels().ToList();
    for (int i = 0; i < fixedPanels.Count; ++i) {
      fixedPanels[i].m_Panel.InitForCollisionDetection();
    }

    // Calculate depenetration from each one against each other one.
    for (int i = 0; i < fixedPanels.Count; ++i) {
      if (fixedPanels[i].m_Panel != movingPanel) {
        for (int j = 0; j < fixedPanels.Count; ++j) {
          if (i != j) {
            fixedPanels[i].m_Panel.CalculateDepenetration(fixedPanels[j].m_Panel);
          }
        }
      }
    }

    // Apply forces.
    for (int i = 0; i < fixedPanels.Count; ++i) {
      // Controlled panel doesn't get forces.
      if (fixedPanels[i].m_Panel != movingPanel) {
        fixedPanels[i].m_Panel.UpdatePositioningForces();
      }
    }

    // Lock to sweet spot sphere.
    Vector3 vSweetSpot = m_SweetSpot.transform.position;
    for (int i = 0; i < fixedPanels.Count; ++i) {
      if (fixedPanels[i].m_Panel != movingPanel) {
        Vector3 vOffset = fixedPanels[i].m_Panel.transform.position - vSweetSpot;
        vOffset.Normalize();

        // Set normal.
        fixedPanels[i].m_Panel.transform.forward = vOffset;

        // Extend to radius of sweet spot and set new position.
        vOffset *= m_SweetSpot.m_PanelAttachRadius;
        fixedPanels[i].m_Panel.transform.position = vSweetSpot + vOffset;
      }
    }
  }

  public void PrimeCollisionSimForWidgets(GrabWidget immovableWidget) {
    for (int i = 0; i < m_AllPanels.Count; ++i) {
      if (m_AllPanels[i].m_Widget != null && m_AllPanels[i].AvailableInCurrentMode) {
        m_AllPanels[i].m_Panel.InitForPanelMovement();
      }
    }
    OrderPanes();
    m_ImmovableWidget = immovableWidget;
  }

  void OrderPanes() {
    // Order panes.
    for (int i = 0; i < m_WandPanes.Length; ++i) {
      m_WandPanes[i].orderedPanelList.Clear();
    }
    // Determine what pane each panel is associated with.
    foreach (PanelData p in GetFixedPanels()) {
      // Don't need to sort and position the admin panel.
      if (IsAdminPanel(p.m_Panel.Type)) {
        continue;
      }

      // Don't need to sort panels that aren't available in this mode.
      if (!p.AvailableInCurrentMode) {
        continue;
      }

      int iPane = -1;
      for (int j = 0; j < m_WandPanes.Length; ++j) {
        if (p.m_Panel.m_WandAttachAngle == m_WandPanes[j].angleOffset) {
          iPane = j;
          break;
        }
      }

      if (iPane != -1) {
        m_WandPanes[iPane].orderedPanelList.Add(p.m_Panel);
      }
    }
    // Sort panes.
    for (int i = 0; i < m_WandPanes.Length; ++i) {
      m_WandPanes[i].orderedPanelList.Sort(ComparePanelsByAttachHeight);
    }
  }

  private static int ComparePanelsByAttachHeight(BasePanel a, BasePanel b) {
    return b.m_WandAttachYOffset_Stable.CompareTo(a.m_WandAttachYOffset_Stable);
  }

  public void DoCollisionSimulationForWidgetPanels() {
    // Calculate depenetration from each one against each other one.
    for (int i = 0; i < m_AllPanels.Count; ++i) {
      if (IsAffectedByCollision(m_AllPanels[i]) &&
          (m_AllPanels[i].m_Widget != m_ImmovableWidget)) {
        float iRad = m_AllPanels[i].m_Widget.m_CollisionRadius;
        for (int j = 0; j < m_AllPanels.Count; ++j) {
          if (i != j && IsAffectedByCollision(m_AllPanels[j])) {
            float jRad = m_AllPanels[j].m_Widget.m_CollisionRadius;
            m_AllPanels[i].m_Panel.CalculateDepenetration(
                m_AllPanels[j].m_Panel.transform.position, iRad + jRad);
          }
        }
      }
    }

    // Apply forces.
    for (int i = 0; i < m_AllPanels.Count; ++i) {
      // Controlled panel doesn't get forces.
      if (IsAffectedByCollision(m_AllPanels[i]) &&
          (m_AllPanels[i].m_Widget != m_ImmovableWidget)) {
        m_AllPanels[i].m_Panel.UpdatePositioningForces();
      }
    }
  }

  // Used to ignore widgets that aren't active, grabbable, or fixed when
  // performing collisions.
  public bool IsAffectedByCollision(PanelData data) {
    return data.AvailableInCurrentMode &&
        data.m_Widget != null && !data.m_Widget.PanelSibling.m_Fixed &&
        data.m_Widget.gameObject.activeSelf && data.m_Widget.IsAvailable();
  }

  public void OpenPanel(BasePanel.PanelType type, TrTransform trSpawnXf) {
    if (type != BasePanel.PanelType.SketchSurface && type != BasePanel.PanelType.Color &&
        type != BasePanel.PanelType.Brush) {
      TrTransform xfSpawn = trSpawnXf;
      xfSpawn.scale = 0.0f;
      TrTransform xfTarget = trSpawnXf;
      xfTarget.scale = 1.0f;
      for (int i = 0; i < m_AllPanels.Count; ++i) {
        if (m_AllPanels[i].m_Panel.Type == type && m_AllPanels[i].AvailableInCurrentMode) {
          if (m_AllPanels[i].m_Widget) {
            if (m_AllPanels[i].m_Panel.m_Fixed) {
              m_AllPanels[i].m_Panel.m_Fixed = false;
              m_AllPanels[i].m_Panel.VerifyStateForFloating();
            }
            m_AllPanels[i].m_Widget.InitIntroAnim(xfSpawn, xfTarget, false);
            m_AllPanels[i].m_Widget.Show(true);
            m_LastOpenedPanelIndex = i;
            if (type != BasePanel.PanelType.Tutorials) {
              PromoManager.m_Instance.RequestFloatingPanelsPromo();
            }

            PrimeCollisionSimForWidgets(m_AllPanels[i].m_Widget);
          }
        }
      }
    }
  }

  public bool IsPanelOpen(BasePanel.PanelType type) {
    for (int i = 0; i < m_AllPanels.Count; ++i) {
      if (m_AllPanels[i].m_Panel.Type == type && m_AllPanels[i].AvailableInCurrentMode) {
        return m_AllPanels[i].m_Panel.gameObject.activeSelf &&
            (m_AllPanels[i].m_Widget == null || m_AllPanels[i].m_Widget.Showing);
      }
    }
    return false;
  }

  public void DismissNonCorePanel(BasePanel.PanelType type) {
    Debug.AssertFormat(!IsPanelCore(type), "DismissNonCorePanel called on Core Panel.");
    for (int i = 0; i < m_AllPanels.Count; ++i) {
      if (m_AllPanels[i].m_Panel.Type == type) {
        m_AllPanels[i].m_Panel.m_Fixed = false;
        _DismissPanelInternal(i);
        m_CachedPanelLayouts.WriteToDisk(m_AllPanels);
        break;
      }
    }
  }

  void _DismissPanelInternal(int index, bool bPlayAudio = true) {
    m_AllPanels[index].m_RestoreFlag = m_AllPanels[index].m_Panel.gameObject.activeSelf;
    m_AllPanels[index].m_Panel.ResetPanel();

    if (m_AllPanels[index].m_Widget) {
      m_AllPanels[index].m_Widget.Show(false, bPlayAudio);
    } else {
      m_AllPanels[index].m_Panel.gameObject.SetActive(false);
    }
  }

  // This method is only used when switching to the app loading state to dismiss panels
  // until they should be restored via RestoreHiddenPanels().
  public void HideAllPanels() {
    for (int i = 0; i < m_AllPanels.Count; ++i) {
      if (m_AllPanels[i].m_Widget) {
        bool bGameObjectEnabled = m_AllPanels[i].m_Panel.gameObject.activeSelf;
        m_AllPanels[i].m_RestoreFlag = bGameObjectEnabled;

        // We don't need to do all this if the panel gameobject isn't enabled.
        if (bGameObjectEnabled) {
          m_AllPanels[i].m_Panel.ResetPanel();
          m_AllPanels[i].m_Widget.Show(false, false);
        }
      }
    }
  }

  public void RestoreHiddenPanels() {
    for (int i = 0; i < m_AllPanels.Count; ++i) {
      if (m_AllPanels[i].m_Widget && m_AllPanels[i].m_RestoreFlag &&
          m_AllPanels[i].AvailableInCurrentMode) {
        _RestorePanelInternal(i);
      }
    }
    App.Switchboard.TriggerStencilModeChanged();
  }

  void _RestorePanelInternal(int index) {
    if (m_AllPanels[index].m_Widget) {
      // Config widget for restoration.
      m_AllPanels[index].m_Widget.Restoring = true;
      m_AllPanels[index].m_Widget.Show(true, false);
      m_AllPanels[index].m_RestoreFlag = false;
      m_AllPanels[index].m_Widget.Restoring = false;
    } else {
      m_AllPanels[index].m_Panel.gameObject.SetActive(true);
    }
  }

  // General use function for calling a method on all panels of a type.
  public void ExecuteOnPanel<T>(System.Action<T> action) where T : BasePanel {
    for (int i = 0; i < m_AllPanels.Count; ++i) {
      if (m_AllPanels[i].m_Panel is T) {
        action(m_AllPanels[i].m_Panel as T);
      }
    }
  }

  public void SetCurrentColorOnAllColorPickers(Color col) {
    App.BrushColor.CurrentColor = col;
  }
}

}  // namespace TiltBrush