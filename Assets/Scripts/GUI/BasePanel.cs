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
using TMPro;

namespace TiltBrush {

[System.Serializable]
public struct PopupMapKey {
  public GameObject m_PopUpPrefab;
  public SketchControlsScript.GlobalCommands m_Command;
}

public class BasePanel : MonoBehaviour {
  static private float GAZE_DISTANCE = 10 * App.METERS_TO_UNITS;
  static private float LARGE_DISTANCE = 9999.0f * App.METERS_TO_UNITS;

  // Types etc

  protected class TextSpring {
    public float m_CurrentAngle;
    public float m_DesiredAngle;
    public float m_Velocity;

    public void Update(float fK, float fDampen) {
      float fToDesired = m_DesiredAngle - m_CurrentAngle;
      fToDesired *= fK;
      float fDampenedVel = m_Velocity * fDampen;
      float fSpringForce = fToDesired - fDampenedVel;
      m_Velocity += fSpringForce;
      m_CurrentAngle += (m_Velocity * Time.deltaTime);
    }
  }

  protected enum DescriptionState {
    Open,
    Closing,
    Closed
  }

  protected enum PanelState {
    Unused,
    Unavailable,
    Available,
  }

  // These names are used in our player prefs, so they must be protected from obfuscation
  // Do not change the names of any of them, unless they've never been released.
  [Serializable]
  public enum PanelType {
    SketchSurface,
    Color,
    Brush,
    AudioReactor,
    AdminPanelMobile,
    ToolsBasicMobile,
    ToolsBasic,
    Experimental,
    ToolsAdvancedMobile,
    MemoryWarning,
    Labs,
    Sketchbook,
    SketchbookMobile,
    BrushMobile,
    AppSettings,
    Tutorials,
    Reference,
    Lights,
    GuideTools,
    Environment,
    Camera,
    Testing,
    Poly,
    BrushExperimental,
    ToolsAdvanced,
    AppSettingsMobile,
    AdminPanel,
    ExtraPanel,
    ExtraMobile,
    PolyMobile,
    LabsMobile,
    ReferenceMobile,
    CameraPath,
    BrushLab
  }

  private enum FixedTransitionState {
    Floating,
    FixedToFloating,
    FloatingToFixed,
    Fixed
  }

  // Inspector data, tunables
  [SerializeField] protected PanelType m_PanelType;

  [SerializeField] protected Collider m_Collider;
  [SerializeField] public GameObject m_Mesh;
  [SerializeField] protected Renderer m_Border;
  [SerializeField] protected Collider m_MeshCollider;
  [SerializeField] protected Vector3 m_ParticleBounds;

  [SerializeField] protected PopupMapKey [] m_PanelPopUpMap;
  [SerializeField] protected string m_PanelDescription;
  [SerializeField] protected GameObject m_PanelDescriptionPrefab;

  [SerializeField] protected Vector3 m_PanelDescriptionOffset;
  [SerializeField] protected Color m_PanelDescriptionColor;

  [SerializeField] protected GameObject m_PanelFlairPrefab;
  [SerializeField] protected Vector3 m_PanelFlairOffset;

  [SerializeField] protected float m_DescriptionSpringK = 4.0f;
  [SerializeField] protected float m_DescriptionSpringDampen = 0.2f;
  [SerializeField] protected float m_DescriptionClosedAngle = -90.0f;
  [SerializeField] protected float m_DescriptionOpenAngle = 0.0f;
  [SerializeField] protected float m_DescriptionAlphaDistance = 90.0f;

  [SerializeField] protected GameObject[] m_Decor;

  [SerializeField] protected float m_GazeHighlightScaleMultiplier = 1.2f;

  [SerializeField] private float m_BorderMeshWidth = 0.02f;
  [SerializeField] private float m_BorderMeshAdvWidth = 0.01f;

  [SerializeField] public float m_PanelSensitivity = 0.1f;
  [SerializeField] protected bool m_ClampToBounds = false;
  [SerializeField] protected Vector3 m_ReticleBounds;

  [SerializeField] public float m_BorderSphereHighlightRadius;
  [SerializeField] protected Vector2 m_PositioningSpheresBounds;
  [SerializeField] protected float m_PositioningSphereRadius = 0.4f;

  [SerializeField] public bool m_UseGazeRotation = false;
  [SerializeField] public float m_MaxGazeRotation = 20.0f;
  [SerializeField] protected float m_GazeActivateSpeed = 8.0f;

  [SerializeField] public Vector3 m_InitialSpawnPos;
  [SerializeField] public Vector3 m_InitialSpawnRotEulers;

  [SerializeField] public float m_WandAttachAngle;
  [SerializeField] public float m_WandAttachYOffset;
  [SerializeField] public float m_WandAttachHalfHeight;
  [SerializeField] private bool m_BeginFixed;
  [SerializeField] private bool m_CanBeFixedToWand = true;
  [SerializeField] private bool m_CanBeDetachedFromWand = true;

  [SerializeField] private float m_PopUpGazeDuration = .2f;

  [SerializeField] protected MeshRenderer[] m_PromoBorders;

  protected const float m_SwipeThreshold = 0.4f;
  protected Material m_BorderMaterial;

  // Public, mutable data

  [NonSerialized] public float m_SweetSpotDistance = 1.0f;
  [NonSerialized] public bool m_Fixed;
  [NonSerialized] public bool m_WandPrimedForAttach;
  [NonSerialized] public float m_WandAttachRadiusAdjust;
  [NonSerialized] public float m_WandAttachYOffset_Target;
  // This is used to store the Y offset of a fixed panel at the point where the user begins
  // interacting with a panelWidget.  Storing this value allows modifications to the panes to be
  // undone.  When the user ends interaction, _Stable is updated to be the _Target.
  [NonSerialized] public float m_WandAttachYOffset_Stable;

  // Internal data

  protected PopUpWindow m_ActivePopUp;
  protected SketchControlsScript.GlobalCommands m_DelayedCommand;
  protected int m_DelayedCommandParam;
  protected int m_DelayedCommandParam2;

  protected GameObject m_PanelDescriptionObject;
  protected Renderer m_PanelDescriptionRenderer;
  protected TextMeshPro m_PanelDescriptionTextMeshPro;

  protected Vector3 m_BaseScale;
  protected float m_AdjustedScale;

  protected TextSpring m_PanelDescriptionTextSpring;
  protected TextSpring m_PanelFlairSpring;
  protected PanelFlair m_PanelFlair;

  protected Vector3 m_ReticleOffset;
  protected Vector3 m_Bounds;
  protected Vector3 m_WorkingReticleBounds;

  protected DescriptionState m_PanelDescriptionState;
  protected DescriptionState m_PanelFlairState;
  protected float m_CloseAngleThreshold;

  protected PanelState m_CurrentState;
  protected PanelState m_DesiredState;
  protected bool m_GazeActive;
  protected bool m_GazeWasActive;
  protected bool m_GazeDescriptionsActive;

  protected bool m_InputValid;
  protected bool m_EatInput;
  protected Ray m_ReticleSelectionRay;

  protected float m_PositioningPercent;

  protected float m_MaxGazeOffsetDistance;
  protected Vector3 m_GazeRotationAxis;
  protected float m_GazeRotationAngle;
  protected Quaternion m_GazeRotationAmount;
  protected float m_GazeActivePercent;

  protected UIComponentManager m_UIComponentManager;
  protected NonScaleChild m_NonScaleChild;  // potentially null

  // Private

  private List<Renderer> m_DecorRenderers;
  private List<TextMeshPro> m_DecorTextMeshes;

  private float m_ScaledPositioningSphereRadius;
  private float m_PositioningExtent;
  private Vector3[] m_PositioningSpheres;
  private Vector3[] m_PositioningSpheresTransformed;

  private Vector3 m_PositioningHome;
  private float m_PositioningK = 0.5f;
  private float m_PositioningDampen = 0.1f;
  private float m_DepenetrationScalar = 200.0f;
  private Vector3 m_PositioningVelocity;

  private Vector3 m_GazeHitPositionCurrent;
  private Vector3 m_GazeHitPositionDesired;
  private float m_GazeHitPositionSpeed = 10.0f;

  protected Vector2 m_SwipeRecentMotion = Vector2.zero;

  private FixedTransitionState m_TransitionState;
  private TrTransform m_WandTransitionTarget;
  private static float m_WandTransitionDuration = .25f;
  private float m_WandTransitionPercent; // 0 = on wand, 1 = at target
  private float m_WandAttachAdjustSpeed = 16.0f;
  private float m_WandAttachYOffset_Initial;
  private float m_WandAttachAngle_Initial;
  private PanelWidget m_WidgetSibling;

  private GameObject m_TempPopUpCollider;
  private float m_PopUpGazeTimer;

  private bool m_AdvancedModePanel;
  private bool m_CurrentlyVisibleInAdvancedMode;

  // TODO: The following are just to track down an elusive NRE. Delete when we've figured
  // it out.
  private bool m_PanelInitializationStarted;
  private bool m_PanelInitializationFinished;
  private float m_PanelDescriptionCounter;

  // Accessors/properties

  public Vector3 GetBounds() { return m_Bounds; }
  public float GetPositioningExtent() { return m_PositioningExtent; }
  public bool IsAvailable() { return m_CurrentState != PanelState.Unavailable; }
  public bool IsActive() { return m_GazeActive; }
  public bool BeginFixed { get { return m_BeginFixed; } }
  public void ClearBeginFixed() { m_BeginFixed = false; }
  public bool CanBeDetached { get { return m_CanBeDetachedFromWand; } }
  public bool IsInInitialPosition() { return m_WandAttachYOffset == m_WandAttachYOffset_Initial &&
      m_WandAttachAngle == m_WandAttachAngle_Initial; }
  public PanelWidget WidgetSibling { get { return m_WidgetSibling; } }
  public bool AdvancedModePanel { get { return m_AdvancedModePanel; } }
  public bool CurrentlyVisibleInAdvancedMode { get { return m_CurrentlyVisibleInAdvancedMode; } }
  public Vector3 ParticleBounds { get { return m_ParticleBounds; } }
  public PopUpWindow PanelPopUp { get { return m_ActivePopUp; } }

  public Color GetGazeColorFromActiveGazePercent() {
    PanelManager pm = PanelManager.m_Instance;
    return Color.Lerp(pm.PanelHighlightInactiveColor, pm.PanelHighlightActiveColor,
        m_GazeActivePercent);
  }

  public PanelType Type {
    get { return m_PanelType; }
  }

  public virtual bool ShouldRegister { get { return true; } }

  public void InitAdvancedFlag(bool advanced) {
    m_AdvancedModePanel = advanced;
    m_CurrentlyVisibleInAdvancedMode = m_BeginFixed;
  }

  public float PopUpOffset {
    get { return m_ActivePopUp ? m_ActivePopUp.GetPopUpForwardOffset() : 0; }
  }

  virtual protected void CalculateBounds() {
    Vector3 vCurrentScale = transform.localScale;
    m_Bounds = m_ReticleBounds;
    m_Bounds.x *= vCurrentScale.x * 0.5f;
    m_Bounds.y *= vCurrentScale.y * 0.5f;
    m_Bounds.z = 0.0f;

    if (m_ActivePopUp == null) {
      m_WorkingReticleBounds = m_Bounds;
    } else {
      Vector3 vPopUpCurrentScale = m_ActivePopUp.transform.localScale;
      m_WorkingReticleBounds = m_ActivePopUp.GetReticleBounds();
      m_WorkingReticleBounds.x *= vPopUpCurrentScale.x * 0.5f;
      m_WorkingReticleBounds.y *= vPopUpCurrentScale.y * 0.5f;
    }
  }

  public void ActivatePromoBorder(bool activate) {
    if (m_WidgetSibling) {
      if (activate) {
        m_WidgetSibling.RemovePromoMeshesFromTintable(m_PromoBorders);
      } else {
        m_WidgetSibling.AddPromoMeshesToTintable(m_PromoBorders);
      }
    }
    foreach (var m in m_PromoBorders) {
      m.material = activate ? PromoManager.m_Instance.SharePromoMaterial : m_BorderMaterial;
    }
  }

  virtual public void SetInIntroMode(bool inIntro) { }

  public void SetPositioningPercent(float fPercent) {
    m_PositioningPercent = fPercent;
  }

  public void SetScale(float fScale) {
    m_AdjustedScale = fScale;
    transform.localScale = m_BaseScale * m_AdjustedScale;
  }

  public bool PanelIsUsed() { return m_CurrentState != PanelState.Unused; }

  public Collider GetCollider() { return m_Collider; }

  public bool HasMeshCollider() {
    return m_MeshCollider != null;
  }

  virtual public bool RaycastAgainstMeshCollider(Ray rRay, out RaycastHit rHitInfo, float fDist) {
    rHitInfo = new RaycastHit();
    bool bReturnValue = false;

    // Creating a popup when a popup exists will generate a temp collider.  This is to prevent
    // forced, accidental out of bounds user input.
    if (m_TempPopUpCollider != null) {
      BoxCollider tempCollider = m_TempPopUpCollider.GetComponent<BoxCollider>();
      bReturnValue = tempCollider.Raycast(rRay, out rHitInfo, fDist);
      if (!bReturnValue) {
        // If we're ever not pointing at the temp collider, destroy it.
        Destroy(m_TempPopUpCollider);
        m_TempPopUpCollider = null;
      }
    }

    // If we've got a pop-up, check collision against that guy first.
    if (m_ActivePopUp != null) {
      var collider = m_ActivePopUp.GetCollider();
      if (collider == null) { throw new InvalidOperationException("No popup collider"); }
      bReturnValue = bReturnValue ||
          m_ActivePopUp.GetCollider().Raycast(rRay, out rHitInfo, fDist);
    }

    // Check custom colliders on components.
    bReturnValue = bReturnValue ||
        m_UIComponentManager.RaycastAgainstCustomColliders(rRay, out rHitInfo, fDist);

    // If we didn't have a pop-up, or collision failed, default to base mesh.
    if (m_MeshCollider == null) { throw new InvalidOperationException("No mesh collider"); }
    bReturnValue = bReturnValue || m_MeshCollider.Raycast(rRay, out rHitInfo, fDist);
    return bReturnValue;
  }

  virtual public void AssignControllerMaterials(InputManager.ControllerName controller) {
    m_UIComponentManager.AssignControllerMaterials(controller);
  }

  /// This function is used to determine the value to be passed in to the controller pad mesh's
  /// shader for visualizing swipes and other actions.
  /// It is called by SketchControls when the panel is in focus.
  public float GetControllerPadShaderRatio(InputManager.ControllerName controller) {
    return m_UIComponentManager.GetControllerPadShaderRatio(controller);
  }

  public bool BrushPadAnimatesOnHover() {
    return m_UIComponentManager.BrushPadAnimatesOnAnyHover();
  }

  public bool UndoRedoBlocked() {
    // This function could be generalized to allow specific UIComponents to block undo/redo,
    // but we only need it in a single case right now, so it's a bit more specific.
    return m_ActivePopUp == null ? false : m_ActivePopUp.BlockUndoRedo();
  }

  virtual public void OnPanelMoved() { }

  virtual protected void Awake() {
    m_WandAttachYOffset_Initial = m_WandAttachYOffset;
    m_WandAttachAngle_Initial = m_WandAttachAngle;
    m_WandAttachYOffset_Target = m_WandAttachYOffset;
  }

  // Happens at App Start() time.
  virtual public void InitPanel() {
    m_PanelInitializationStarted = true;
    m_BorderMaterial = m_Border.material;
    m_BaseScale = transform.localScale;
    m_AdjustedScale = 1.0f;

    CalculateBounds();
    m_NonScaleChild = GetComponent<NonScaleChild>();

    if (m_PanelDescriptionPrefab != null) {
      m_PanelDescriptionObject = (GameObject)Instantiate(m_PanelDescriptionPrefab);
      m_PanelDescriptionObject.transform.position = m_Mesh.transform.position;
      m_PanelDescriptionObject.transform.rotation = m_Mesh.transform.rotation;
      m_PanelDescriptionObject.transform.SetParent(transform);
      Vector3 vScale = m_PanelDescriptionObject.transform.localScale;
      vScale *= transform.localScale.x;
      m_PanelDescriptionObject.transform.localScale = vScale;

      m_PanelDescriptionRenderer = m_PanelDescriptionObject.GetComponent<Renderer>();
      if (m_PanelDescriptionRenderer) {
        m_PanelDescriptionRenderer.enabled = false;
      }

      m_PanelDescriptionTextMeshPro = m_PanelDescriptionObject.GetComponent<TextMeshPro>();
      if (m_PanelDescriptionTextMeshPro) {
        m_PanelDescriptionTextMeshPro.text = m_PanelDescription;
        m_PanelDescriptionTextMeshPro.color = m_PanelDescriptionColor;
      }
    }

    if (m_PanelFlairPrefab != null) {
      GameObject go = (GameObject)Instantiate(m_PanelFlairPrefab);
      m_PanelFlair = go.GetComponent<PanelFlair>();
      m_PanelFlair.ParentToPanel(transform, m_PanelFlairOffset);
    }

    m_PanelDescriptionState = DescriptionState.Closing;
    m_PanelFlairState = DescriptionState.Closing;

    m_CloseAngleThreshold = Mathf.Abs(m_DescriptionOpenAngle - m_DescriptionClosedAngle) * 0.01f;

    m_PanelDescriptionTextSpring = new TextSpring();
    m_PanelDescriptionTextSpring.m_CurrentAngle = m_DescriptionClosedAngle;
    m_PanelDescriptionTextSpring.m_DesiredAngle = m_DescriptionClosedAngle;
    m_PanelFlairSpring = new TextSpring();
    m_PanelFlairSpring.m_CurrentAngle = m_DescriptionClosedAngle;
    m_PanelFlairSpring.m_DesiredAngle = m_DescriptionClosedAngle;

    PanelManager pm = PanelManager.m_Instance;
    m_Border.material.SetColor("_Color", pm.PanelHighlightInactiveColor);

    m_DecorRenderers = new List<Renderer>();
    m_DecorTextMeshes = new List<TextMeshPro>();

    if (m_Decor.Length > 0) {
      // Cache all decor renderers.
      for (int i = 0; i < m_Decor.Length; ++i) {
        Renderer [] aChildRenderers = m_Decor[i].GetComponentsInChildren<Renderer>();
        for (int j = 0; j < aChildRenderers.Length; ++j) {
          // Prefer to cache a text mesh pro object if we find one.
          TextMeshPro tmp = aChildRenderers[j].GetComponent<TextMeshPro>();
          if (tmp) {
            m_DecorTextMeshes.Add(tmp);
          } else {
            // Otherwise, the standard renderer will do.
            m_DecorRenderers.Add(aChildRenderers[j]);
            m_DecorRenderers[m_DecorRenderers.Count - 1].material.SetColor("_Color",
                pm.PanelHighlightInactiveColor);
          }
        }
      }
    }

    m_CurrentState = PanelState.Available;
    m_DesiredState = PanelState.Available;
    m_GazeActive = false;
    m_PositioningPercent = 0.0f;

    if (m_PositioningSpheresBounds.x > 0.0f && m_PositioningSpheresBounds.y > 0.0f && m_PositioningSphereRadius > 0.0f) {
      Vector2 vSphereBounds = m_PositioningSpheresBounds;
      vSphereBounds.x -= m_PositioningSphereRadius * 0.5f;
      vSphereBounds.y -= m_PositioningSphereRadius * 0.5f;
      m_ScaledPositioningSphereRadius = m_PositioningSphereRadius * transform.localScale.x;

      int iNumSpheresWidth = Mathf.CeilToInt((vSphereBounds.x * 2.0f) / m_PositioningSphereRadius);
      int iNumSpheresHeight = Mathf.CeilToInt((vSphereBounds.y * 2.0f) / m_PositioningSphereRadius);
      int iTotalNumSpheres = (iNumSpheresWidth * 2) + ((iNumSpheresHeight - 2) * 2);
      m_PositioningSpheres = new Vector3[iTotalNumSpheres];

      float fXInterval = (vSphereBounds.x / (float)(iNumSpheresWidth - 1)) * 2.0f;
      float fYInterval = (vSphereBounds.y / (float)(iNumSpheresHeight - 1)) * 2.0f;
      for (int i = 0; i < iNumSpheresWidth; ++i) {
        float fX = -vSphereBounds.x + (fXInterval * (float)i);
        m_PositioningSpheres[i].Set(fX, -vSphereBounds.y, 0.0f);
      }
      for (int i = 0; i < iNumSpheresHeight - 2; ++i) {
        float fY = -vSphereBounds.y + (fYInterval * (float)(i + 1));
        m_PositioningSpheres[iNumSpheresWidth + (i * 2)].Set(-vSphereBounds.x, fY, 0.0f);
        m_PositioningSpheres[iNumSpheresWidth + (i * 2) + 1].Set(vSphereBounds.x, fY, 0.0f);
      }
      for (int i = iTotalNumSpheres - iNumSpheresWidth; i < iTotalNumSpheres; ++i) {
        int iIndex = i - (iTotalNumSpheres - iNumSpheresWidth);
        float fX = -vSphereBounds.x + (fXInterval * (float)iIndex);
        m_PositioningSpheres[i].Set(fX, vSphereBounds.y, 0.0f);
      }

      m_PositioningSpheresTransformed = new Vector3[m_PositioningSpheres.Length];
      for (int i = 0; i < m_PositioningSpheresTransformed.Length; ++i) {
        m_PositioningSpheresTransformed[i] = new Vector3();
        m_PositioningExtent = Mathf.Max(m_PositioningExtent, Mathf.Max(m_PositioningSpheres[i].x, m_PositioningSpheres[i].y));
      }
      m_PositioningExtent += (m_PositioningSphereRadius * 2.0f);
    }

    m_MaxGazeOffsetDistance = m_Bounds.magnitude;
    m_GazeRotationAmount = Quaternion.identity;

    m_UIComponentManager = GetComponent<UIComponentManager>();
    m_UIComponentManager.SetColor(pm.PanelHighlightInactiveColor);

    m_WidgetSibling = GetComponent<PanelWidget>();
    if (m_Fixed) {
      m_TransitionState = FixedTransitionState.Fixed;
    }

    // Bake border meshs.
    float width = !m_AdvancedModePanel ? m_BorderMeshAdvWidth : m_BorderMeshWidth;
    Color baseCol = !m_AdvancedModePanel ? pm.PanelBorderMeshOutlineColor : pm.PanelBorderMeshBaseColor;
    Color outlineCol = !m_AdvancedModePanel ? pm.PanelBorderMeshBaseColor : pm.PanelBorderMeshOutlineColor;
    BakedMeshOutline[] bakeries = GetComponentsInChildren<BakedMeshOutline>(true);
    BakedMeshOutline borderBakery = m_Border.GetComponent<BakedMeshOutline>();
    for (int i = 0; i < bakeries.Length; ++i) {
      if (bakeries[i] == borderBakery) {
        // The border is the only bakery that gets custom treatment.
        bakeries[i].Bake(baseCol, outlineCol, width);
      } else {
        bakeries[i].Bake(pm.PanelBorderMeshBaseColor, pm.PanelBorderMeshOutlineColor, m_BorderMeshWidth);
      }
    }

    m_PanelInitializationFinished = true;
  }

  public void CloseActivePopUp(bool force) {
    if (m_ActivePopUp != null) {
      if (m_ActivePopUp.RequestClose(force)) {
        InvalidateIfActivePopup(m_ActivePopUp);
      }
    }
  }

  public void VerifyStateForFloating() {
    m_TransitionState = FixedTransitionState.Floating;
    m_WandTransitionPercent = 1;
  }

  public void SetPanelStableToTarget() {
    m_WandAttachYOffset_Stable = m_WandAttachYOffset_Target;
  }

  public void ResetPanelToInitialPosition() {
    m_WandAttachYOffset = m_WandAttachYOffset_Initial;
    m_WandAttachYOffset_Target = m_WandAttachYOffset_Initial;
    m_WandAttachYOffset_Stable = m_WandAttachYOffset_Initial;
    m_WandAttachAngle = m_WandAttachAngle_Initial;

    if (m_TransitionState != FixedTransitionState.Fixed) {
      m_Fixed = true;
      m_TransitionState = FixedTransitionState.Fixed;
      m_WandTransitionPercent = 0;
    }
  }

  public void ResetPanel() {
    // Get rid of the popup as fast as possible.
    if (m_ActivePopUp != null) {
      Destroy(m_ActivePopUp.gameObject);
      InvalidateIfActivePopup(m_ActivePopUp);
    }
    Destroy(m_TempPopUpCollider);
    m_TempPopUpCollider = null;

    //reset gaze animations
    m_PanelDescriptionState = DescriptionState.Closed;
    if (m_PanelDescriptionRenderer) {
      m_PanelDescriptionRenderer.enabled = false;
    }

    m_PanelFlairState = DescriptionState.Closed;
    if (m_PanelFlair != null) {
      m_PanelFlair.Hide();
    }

    UpdatePanelColor(PanelManager.m_Instance.PanelHighlightInactiveColor);

    m_GazeActive = false;
    m_GazeActivePercent = 0.0001f;
    m_GazeRotationAxis = Vector3.zero;
  }

  public void InvalidateIfActivePopup(PopUpWindow activePopup) {
    // If this popup isn't the active one, don't bother. 
    if (activePopup == m_ActivePopUp) {
      m_ActivePopUp = null;
      // Eat input when we close a popup so the close action doesn't carry over.
      m_EatInput = true;
    }
  }

  void OnEnable() {
    OnEnablePanel();
  }

  void OnDisable() {
    OnDisablePanel();
  }

  virtual protected void OnEnablePanel() {
    if (PanelManager.m_Instance != null) {
      if (PanelManager.m_Instance.AdvancedModeActive()) {
        m_CurrentlyVisibleInAdvancedMode = true;
      }
    }
  }

  virtual protected void OnDisablePanel() {
    if (PanelManager.m_Instance != null) {
      if (PanelManager.m_Instance.AdvancedModeActive()) {
        m_CurrentlyVisibleInAdvancedMode = false;
      }
    }
  }

  public void ResetReticleOffset() {
    m_ReticleOffset = Vector3.zero;
  }

  public void UpdateReticleOffset(float fXDelta, float fYDelta) {
    m_ReticleOffset.x += fXDelta * m_PanelSensitivity;
    m_ReticleOffset.y += fYDelta * m_PanelSensitivity;

    if (m_ClampToBounds) {
      m_ReticleOffset.x = Mathf.Clamp(m_ReticleOffset.x, -m_WorkingReticleBounds.x, m_WorkingReticleBounds.x);
      m_ReticleOffset.y = Mathf.Clamp(m_ReticleOffset.y, -m_WorkingReticleBounds.y, m_WorkingReticleBounds.y);
      m_ReticleOffset.z = m_WorkingReticleBounds.z;
    }
  }

  //  Given a position that has been proven to be a hit point on the panel's collider and the cast
  //  direction that resulted in that point, determine where the reticle should be located.
  //  Used by wand panel method of interacting with panels.
  virtual public void GetReticleTransformFromPosDir(Vector3 vInPos, Vector3 vInDir, out Vector3 vOutPos, out Vector3 vForward) {
    //by default, the collision point is ok, and the reticle's forward should be the same as the mesh
    vOutPos = vInPos;
    vForward = -m_Mesh.transform.forward;

    Vector3 dir = Vector3.forward;
    Ray rCastRay = new Ray(vInPos - vInDir * 0.5f, vInDir);

    // If we have a ghost popup, collide with that to find our position.
    if (m_TempPopUpCollider != null) {
      RaycastHit rHitInfo;
      if (DoesRayHitCollider(rCastRay, m_TempPopUpCollider.GetComponent<BoxCollider>(), out rHitInfo)) {
        vOutPos = rHitInfo.point;
      }
    }

    // Override default and ghost with standard popup collision.
    if (m_ActivePopUp != null) {
      m_ActivePopUp.CalculateReticleCollision(rCastRay, ref vOutPos, ref vForward);
    } else {
      // PopUps trump UIComponents, so if there isn't a PopUp, check against all UIComponents.
      m_UIComponentManager.CalculateReticleCollision(rCastRay, ref vOutPos, ref vForward);
    }
  }

  // TODO : This is currently broken.  Needs to be updated to use new
  // m_UIComponentManager.CalculateReticleCollision style.
  //  Using m_ReticleOffset, determine where the reticle should be located.
  //  Used by gaze method of interacting with panels.
  virtual public void GetReticleTransform(out Vector3 vPos, out Vector3 vForward, bool bGazeAndTap) {
    // For ViewingOnly controls, position the reticle at the gaze panel position
    if (bGazeAndTap) {
      // Calculate plane intersection
      Transform head = ViewpointScript.Head;
      Ray ray = new Ray(head.position, head.forward);
      RaycastHit rHitInfo;
      if (RaycastAgainstMeshCollider(ray, out rHitInfo, GAZE_DISTANCE)) {
        vPos = rHitInfo.point;
      } else {
        vPos = new Vector3(LARGE_DISTANCE, LARGE_DISTANCE, LARGE_DISTANCE);
      }
    } else {
      Vector3 vTransformedOffset = m_Mesh.transform.rotation * m_ReticleOffset;
      vPos = transform.position + vTransformedOffset;
    }
    vForward = -m_Mesh.transform.forward;
  }

  // This function should only be used when the app state changes in a way that requires
  // an out-of-focus panel's visuals to become reflect a stale state.
  virtual public void ForceUpdatePanelVisuals() {
    m_UIComponentManager.UpdateVisuals();
  }

  void Update() {
    BaseUpdate();
  }

  protected void BaseUpdate() {
    UpdateState();
    CalculateBounds();
    UpdateDescriptions();
    UpdateGazeBehavior();
    UpdateFixedTransition();
  }

  protected void UpdateState() {
    //check if we're switching activity
    if (m_GazeWasActive != m_GazeActive) {
      if (m_GazeActive) {
        AudioManager.m_Instance.ActivatePanel(true, transform.position);
      } else {
        m_UIComponentManager.ResetInput();
        AudioManager.m_Instance.ActivatePanel(false, transform.position);
      }

      m_GazeWasActive = m_GazeActive;
      OnUpdateActive();
    }

    bool bGazeDescriptionsWereActive = m_GazeDescriptionsActive;
    m_GazeDescriptionsActive = m_GazeActive;

    // Update components if gaze is active.
    if (m_GazeDescriptionsActive) {
      m_UIComponentManager.UpdateVisuals();
    }

    //check if we're switching descriptions showing
    if (bGazeDescriptionsWereActive != m_GazeDescriptionsActive) {
      if (m_GazeDescriptionsActive) {
        if (m_PanelDescriptionObject) {
          m_PanelDescriptionState = DescriptionState.Open;
          m_PanelDescriptionTextSpring.m_DesiredAngle = m_DescriptionOpenAngle;
          m_PanelDescriptionRenderer.enabled = true;
        }
      } else {
        if (m_PanelDescriptionObject) {
          m_PanelDescriptionState = DescriptionState.Closing;
          m_PanelDescriptionTextSpring.m_DesiredAngle = m_DescriptionClosedAngle;
          ResetPanelFlair();
        }
        m_UIComponentManager.ManagerLostFocus();
        m_UIComponentManager.Deactivate();
        if (m_ActivePopUp != null) {
          if (m_ActivePopUp.RequestClose()) {
            InvalidateIfActivePopup(m_ActivePopUp);
          }
        }
      }
    }

    //update state
    m_CurrentState = m_DesiredState;
  }

  virtual protected void OnUpdateActive() {
  }

  void UpdateMeshRotation() {
    m_Mesh.transform.rotation = m_GazeRotationAmount * transform.rotation;
  }

  protected void UpdateDescriptions() {
    try {
      m_PanelDescriptionCounter = 0;
      if (m_PanelDescriptionState != DescriptionState.Closed && m_PanelDescriptionObject != null) {
        m_PanelDescriptionCounter = 1;
        m_PanelDescriptionTextSpring.Update(m_DescriptionSpringK, m_DescriptionSpringDampen);

        m_PanelDescriptionCounter = 2;
        Quaternion qOrient = Quaternion.Euler(0.0f, m_PanelDescriptionTextSpring.m_CurrentAngle, 0.0f);
        m_PanelDescriptionObject.transform.rotation = m_Mesh.transform.rotation * qOrient;

        m_PanelDescriptionCounter = 3;
        Vector3 vPanelDescriptionOffset = m_Bounds;
        vPanelDescriptionOffset.x *= m_PanelDescriptionOffset.x;
        vPanelDescriptionOffset.y *= m_PanelDescriptionOffset.y;
        Vector3 vTransformedOffset = m_Mesh.transform.rotation * vPanelDescriptionOffset;
        m_PanelDescriptionObject.transform.position = m_Mesh.transform.position + vTransformedOffset;

        m_PanelDescriptionCounter = 4;
        float fDistToClosed = Mathf.Abs(m_PanelDescriptionTextSpring.m_CurrentAngle - m_DescriptionClosedAngle);
        Color descColor = m_PanelDescriptionColor;
        if (m_ActivePopUp != null) {
          descColor = Color.Lerp(m_PanelDescriptionColor,
              PanelManager.m_Instance.PanelHighlightInactiveColor,
              m_ActivePopUp.GetTransitionRatioForVisuals());
        }
        float fRatio = fDistToClosed / m_DescriptionAlphaDistance;
        descColor.a = Mathf.Min(fRatio * fRatio, 1.0f);
        if (m_PanelDescriptionTextMeshPro) {
          m_PanelDescriptionTextMeshPro.color = descColor;
        }

        m_PanelDescriptionCounter = 5;
        if (m_PanelDescriptionState == DescriptionState.Closing) {
          float fToDesired = m_PanelDescriptionTextSpring.m_DesiredAngle - m_PanelDescriptionTextSpring.m_CurrentAngle;
          if (Mathf.Abs(fToDesired) <= m_CloseAngleThreshold) {
            m_PanelDescriptionRenderer.enabled = false;
            m_PanelDescriptionState = DescriptionState.Closed;
          }
        }
      }

      m_PanelDescriptionCounter = 6;
      if (m_PanelFlairState != DescriptionState.Closed) {
        m_PanelDescriptionCounter = 7;
        m_PanelFlairSpring.Update(m_DescriptionSpringK, m_DescriptionSpringDampen);
        float fDistToClosed = Mathf.Abs(m_PanelFlairSpring.m_CurrentAngle - m_DescriptionClosedAngle);
        float fRatio = fDistToClosed / m_DescriptionAlphaDistance;
        float fAlpha = Mathf.Min(fRatio * fRatio, 1.0f);

        m_PanelDescriptionCounter = 8;
        Quaternion qOrient = m_Mesh.transform.rotation * Quaternion.Euler(0.0f, m_PanelFlairSpring.m_CurrentAngle, 0.0f);

        m_PanelDescriptionCounter = 9;
        if (m_PanelFlair != null) {
          m_PanelFlair.UpdateAnimationOnPanel(m_Bounds, qOrient, fAlpha);
        }

        m_PanelDescriptionCounter = 10;
        if (m_PanelFlairState == DescriptionState.Closing) {
          float fToDesired = m_PanelFlairSpring.m_DesiredAngle - m_PanelFlairSpring.m_CurrentAngle;
          if (Mathf.Abs(fToDesired) <= m_CloseAngleThreshold) {
            if (m_PanelFlair != null) {
              m_PanelFlair.Hide();
            }

            m_PanelFlairState = DescriptionState.Closed;
          }
        }
      }
    } catch (Exception ex) {
      string message = string.Format("{0}: Init State({1}, {2}), Counter({3})",
                                     ex.Message,
                                     m_PanelInitializationStarted,
                                     m_PanelInitializationFinished,
                                     m_PanelDescriptionCounter);
      throw new Exception(message);
    }
  }

  protected virtual void UpdateGazeBehavior() {
    if (m_UseGazeRotation && m_Mesh) {
      float fPrevPercent = m_GazeActivePercent;
      float fPercentStep = m_GazeActivateSpeed * Time.deltaTime;
      if (IsActive()) {
        m_GazeActivePercent = Mathf.Min(m_GazeActivePercent + fPercentStep, 1.0f);
      } else {
        m_GazeActivePercent = Mathf.Max(m_GazeActivePercent - fPercentStep, 0.0f);
      }

      //don't bother updating static panels
      if (fPrevPercent > 0.0f) {
        float fRotationAngle = m_GazeRotationAngle * m_GazeActivePercent * (1.0f - m_PositioningPercent);
        m_GazeRotationAmount = Quaternion.AngleAxis(fRotationAngle, m_GazeRotationAxis);
        UpdateMeshRotation();

        Color rPanelColor = GetGazeColor();
        if (m_Border.material == m_BorderMaterial) {
          m_Border.material.SetColor("_Color", rPanelColor);
        }

        if (fPrevPercent < 1.0f) {
          float fScaleMult = m_GazeActivePercent * (m_GazeHighlightScaleMultiplier - 1.0f);
          Vector3 newScale = m_BaseScale * m_AdjustedScale * (1.0f + fScaleMult);
          if (m_NonScaleChild != null) {
            m_NonScaleChild.globalScale = newScale;
          } else {
            transform.localScale = newScale;
          }
        }

        UpdatePanelColor(rPanelColor);
        m_UIComponentManager.GazeRatioChanged(m_GazeActivePercent);
      }
    }
  }

  public Color GetGazeColor() {
    PanelManager pm = PanelManager.m_Instance;
    Color targetColor = pm.PanelHighlightActiveColor;
    if (m_ActivePopUp != null) {
      targetColor = Color.Lerp(pm.PanelHighlightActiveColor,
          pm.PanelHighlightInactiveColor,
          m_ActivePopUp.GetTransitionRatioForVisuals());
    }

    float t = m_GazeActivePercent;
    if (SketchControlsScript.m_Instance.AtlasIconTextures) {
      // If we're atlasing panel textures, only send 0 and 1 through.
      t = Mathf.Floor(m_GazeActivePercent + 0.5f);
    }
    return Color.Lerp(pm.PanelHighlightInactiveColor, targetColor, t);
  }

  protected void UpdateFixedTransition() {
    // State machine for panel exploding off of wand controller.
    if (m_TransitionState == FixedTransitionState.FixedToFloating) {
      m_WandTransitionPercent += Time.deltaTime / m_WandTransitionDuration;
      if (m_WandTransitionPercent >= 1.0f) {
        m_WandTransitionPercent = 1.0f;
        m_TransitionState = FixedTransitionState.Floating;
      }
      PanelManager.m_Instance.UpdateWandTransitionXf(
          this, m_WandTransitionTarget, m_WandTransitionPercent);
    } else if (m_TransitionState == FixedTransitionState.FloatingToFixed) {
      m_WandTransitionPercent -= Time.deltaTime / m_WandTransitionDuration;
      if (m_WandTransitionPercent <= 0.0f) {
        m_WandTransitionPercent = 0.0f;
        m_TransitionState = FixedTransitionState.Fixed;
      }
      PanelManager.m_Instance.UpdateWandTransitionXf(
          this, m_WandTransitionTarget, m_WandTransitionPercent);
    } else if (WidgetSibling &&
        WidgetSibling.IsUserInteracting(InputManager.ControllerName.Brush)) {
      // If the user is interacting with this panel, lock to a pane if this panel allows it.
      if (m_CanBeFixedToWand) {
        PanelManager.m_Instance.AttachHeldPanelToWand(this);
      }
    } else if (m_TransitionState == FixedTransitionState.Fixed) {
      // Update attach height.
      float fStep = m_WandAttachAdjustSpeed * Time.deltaTime;
      float fToTarget = m_WandAttachYOffset_Target - m_WandAttachYOffset;
      if (Mathf.Abs(fToTarget) < fStep) {
        m_WandAttachYOffset = m_WandAttachYOffset_Target;
      } else {
        m_WandAttachYOffset += fStep * Mathf.Sign(fToTarget);
      }
      m_WandAttachRadiusAdjust = 0.0f;
    }
  }

  public void WidgetSiblingBeginInteraction() {
    PanelManager.m_Instance.InitPanesForPanelAttach(m_Fixed);

    // Initialize primed flag to off-- it'll get enabled if the attachment checks are valid.
    m_WandPrimedForAttach = false;

    if (m_Fixed) {
      m_Fixed = false;
      m_TransitionState = FixedTransitionState.Floating;
      m_WandTransitionPercent = 1;
    }
  }

  public void WidgetSiblingEndInteraction() {
    if (!m_Fixed) {
      PanelManager.m_Instance.ResetPaneVisuals();
      if (m_WandPrimedForAttach || !CanBeDetached) {
        m_Fixed = true;
        m_TransitionState = FixedTransitionState.Fixed;
        m_WandTransitionPercent = 0;
      }

      if (!m_WandPrimedForAttach) {
        // If we're releasing this panel and it's not snapping to a pane, make sure the pane
        // panels are reset back to their stable positions.
        PanelManager.m_Instance.SetFixedPanelsToStableOffsets();
      } else {
        // If this panel is snapping to a pane, update it's stable position so it's sorted
        // correctly when we close the gaps.
        SetPanelStableToTarget();
      }
      m_WandTransitionTarget = TrTransform.FromTransform(transform);
      WidgetSibling.SetActiveTintToShowError(false);

      PanelManager.m_Instance.ClosePanePanelGaps();
    }
  }

  // Target is ignored if the panel is reattaching to the wand
  public void TransitionToWand(bool bFixed, TrTransform target) {
    if (bFixed && m_TransitionState != FixedTransitionState.Fixed) {
      m_TransitionState = FixedTransitionState.FloatingToFixed;
    } else if (!bFixed && m_TransitionState != FixedTransitionState.Floating) {
      m_WandTransitionTarget = target;
      m_TransitionState = FixedTransitionState.FixedToFloating;
    }
  }

  void UpdatePanelColor(Color rPanelColor) {
    // Set the appropriate dim value for all our UI components.
    m_UIComponentManager.SetColor(rPanelColor);

    OnUpdateGazeBehavior(rPanelColor);

    for (int i = 0; i < m_DecorRenderers.Count; ++i) {
      m_DecorRenderers[i].material.SetColor("_Color", rPanelColor);
    }
    for (int i = 0; i < m_DecorTextMeshes.Count; ++i) {
      m_DecorTextMeshes[i].color = rPanelColor;
    }
  }

  virtual protected void OnUpdateGazeBehavior(Color rPanelColor) { }

  virtual public void PanelGazeActive(bool bActive) {
    m_GazeActive = bActive;
    m_GazeHitPositionCurrent = transform.position;
    m_UIComponentManager.ResetInput();
  }

  void SetPanelFlairText(string sText) {
    if (m_PanelFlair != null) {
      // Interpret any message as a good message.
      if (m_PanelFlair.AlwaysShow) {
        m_PanelFlair.SetText(sText);
        m_PanelFlair.Show();
        m_PanelFlairState = DescriptionState.Open;
        m_PanelFlairSpring.m_DesiredAngle = m_DescriptionOpenAngle;
      } else {
        if (sText != null && !sText.Equals("")) {
          m_PanelFlair.SetText(sText);
          m_PanelFlair.Show();
          m_PanelFlairState = DescriptionState.Open;
          m_PanelFlairSpring.m_DesiredAngle = m_DescriptionOpenAngle;
        } else if (m_PanelFlairState != DescriptionState.Closed) {
          m_PanelFlairState = DescriptionState.Closing;
          m_PanelFlairSpring.m_DesiredAngle = m_DescriptionClosedAngle;
        }
      }
    }
  }

  public void ResetPanelFlair() {
    if (m_PanelFlairState != DescriptionState.Closed) {
      m_PanelFlairState = DescriptionState.Closing;
      m_PanelFlairSpring.m_DesiredAngle = m_DescriptionClosedAngle;
    }
  }

  virtual public void OnWidgetShowAnimStart() { }

  virtual public void OnWidgetShowAnimComplete() { }

  virtual public void OnWidgetHide() { }

  /// Accumulates dpad input, and returns nonzero for a discrete swipe action.
  /// Return value is 1 for "backward" swipe moving a page forward
  /// and -1 for "forward" swipe moving a page backward.
  protected int AccumulateSwipe() {
    int direction = 0;
    if (InputManager.m_Instance.IsBrushScrollActive()) {
      // If our delta is beyond our trigger threshold, report it.
      float fDelta = InputManager.m_Instance.GetAdjustedBrushScrollAmount();
      if (IncrementMotionAndCheckForSwipe(fDelta)) {
        direction = (int)Mathf.Sign(m_SwipeRecentMotion.x) * -1;
        m_SwipeRecentMotion.x = 0.0f;
      }
    } else {
      m_SwipeRecentMotion.x = 0.0f;
    }

    return direction;
  }

  /// Virtual so panels can interpret m_SwipeRecentMotion however they'd like.
  virtual public bool IncrementMotionAndCheckForSwipe(float fMotion) {
    m_SwipeRecentMotion.x += fMotion;
    return Mathf.Abs(m_SwipeRecentMotion.x) > m_SwipeThreshold;
  }

  /// Panels are updated by SketchControls when they have focus.
  public void UpdatePanel(Vector3 vToPanel, Vector3 vHitPoint) {
    // Validate input and cache it for this update.
    m_InputValid = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate);
    if (m_EatInput) {
      m_EatInput = m_InputValid;
      m_InputValid = false;
    }

    // Cache input ray for this update.
    Vector3 vReticlePos = SketchControlsScript.m_Instance.GetUIReticlePos();
    m_ReticleSelectionRay = new Ray(vReticlePos - transform.forward, transform.forward);

    if (m_ActivePopUp != null) {
      var collider = m_ActivePopUp.GetCollider();
      if (collider == null) { throw new InvalidOperationException("No popup collider"); }
      RaycastHit hitInfo;

      // If we're pointing at a popup, increment our gaze timer.
      if (BasePanel.DoesRayHitCollider(m_ReticleSelectionRay,
          m_ActivePopUp.GetCollider(), out hitInfo)) {
        if (m_ActivePopUp.IsOpen()) {
          m_PopUpGazeTimer += Time.deltaTime;
        }
      } else if (!m_ActivePopUp.InputObjectHasFocus() &&
          (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate) ||
            m_PopUpGazeTimer > m_PopUpGazeDuration)) {
        // If we're not pointing at the popup and the user doesn't have focus on an element
        // of the popup, dismiss the popup if we've pointed at it before, or there's input.
        m_ActivePopUp.RequestClose();
      }
    }

    if (m_UseGazeRotation) {
      m_GazeHitPositionDesired = vHitPoint;
      Vector3 vCurrToDesired = m_GazeHitPositionDesired - m_GazeHitPositionCurrent;
      float fStep = m_GazeHitPositionSpeed * Time.deltaTime;
      if (vCurrToDesired.sqrMagnitude < fStep * fStep) {
        m_GazeHitPositionCurrent = m_GazeHitPositionDesired;
      } else {
        vCurrToDesired.Normalize();
        vCurrToDesired *= fStep;
        m_GazeHitPositionCurrent += vCurrToDesired;
      }

      Vector3 vToCenter = m_GazeHitPositionCurrent - GetCollider().transform.position;
      float fOffsetDist = vToCenter.magnitude;
      vToCenter.Normalize();
      m_GazeRotationAxis = Vector3.Cross(-vToPanel, vToCenter);
      m_GazeRotationAxis.Normalize();
      m_GazeRotationAngle = (fOffsetDist / m_MaxGazeOffsetDistance) * m_MaxGazeRotation;
    }

    // Update custom logic for panel.
    OnUpdatePanel(vToPanel, vHitPoint);

    // Update UIComponents, with PopUps taking priority.
    if (m_ActivePopUp) {
      if (!m_EatInput && m_PopUpGazeTimer > 0) {
        m_ActivePopUp.UpdateUIComponents(m_ReticleSelectionRay, m_InputValid, m_ActivePopUp.GetCollider());
      }
    } else {
      // TODO : I'm not convinced the 3rd parameter here should be the main collider.
      // I think it might need to be the mesh collider.  If so, the collider is only used in
      // monoscopic mode and should be renamed appropriately.
      m_UIComponentManager.UpdateUIComponents(m_ReticleSelectionRay, m_InputValid, GetCollider());

      // If a popup was just spawned, clear our active UI component, as input will now be
      // directed to the popup.
      if (m_ActivePopUp) {
        m_UIComponentManager.ResetInput();
      }

      if (m_UIComponentManager.ActiveInputUIComponent == null) {
        SetPanelFlairText(null);
      } else {
        SetPanelFlairText(m_UIComponentManager.ActiveInputUIComponent.Description);
      }
    }
  }

  /// Base level logic for updating panel.  This should be called from any derived class.
  virtual public void OnUpdatePanel(Vector3 vToPanel, Vector3 vHitPoint) { }

  public void InitForPanelMovement() {
    m_PositioningHome = transform.position;
    m_PositioningVelocity.Set(0.0f, 0.0f, 0.0f);
    m_WandAttachYOffset_Stable = m_WandAttachYOffset_Target;
  }

  virtual public void PanelHasStoppedMoving() {
    m_GazeHitPositionCurrent = transform.position;
  }

  public void CreatePopUp(SketchControlsScript.GlobalCommands rCommand,
      int iCommandParam, int iCommandParam2, string sDelayedText = "",
      Action delayedClose = null) {
    CreatePopUp(rCommand, iCommandParam, iCommandParam2, Vector3.zero, sDelayedText, delayedClose);
  }

  public void CreatePopUp(SketchControlsScript.GlobalCommands rCommand,
      int iCommandParam, int iCommandParam2, Vector3 vPopupOffset, string sDelayedText = "",
      Action delayedClose = null) {
    bool bPopUpExisted = false;
    Vector3 vPrevPopUpPos = Vector3.zero;
    // If we've got an active popup, send it packing.
    if (m_ActivePopUp != null) {
      // Create a copy of the collider so we don't pull the rug out from under the user.
      m_TempPopUpCollider = m_ActivePopUp.DuplicateCollider();
      vPrevPopUpPos = m_ActivePopUp.transform.position;
      CloseActivePopUp(false);
      bPopUpExisted = true;
    }

    if (m_ActivePopUp == null) {
      // Look for the appropriate popup for this command.
      int iPopUpIndex = -1;
      for (int i = 0; i < m_PanelPopUpMap.Length; ++i) {
        if (m_PanelPopUpMap[i].m_Command == rCommand) {
          iPopUpIndex = i;
          break;
        }
      }

      if (iPopUpIndex >= 0) {
        // Create a new popup.
        GameObject popUp = (GameObject)Instantiate(m_PanelPopUpMap[iPopUpIndex].m_PopUpPrefab,
          m_Mesh.transform.position, m_Mesh.transform.rotation);
        m_ActivePopUp = popUp.GetComponent<PopUpWindow>();

        // If we're replacing a popup, put the new one in the same position.
        if (bPopUpExisted) {
          popUp.transform.position = vPrevPopUpPos;
        } else {
          Vector3 vPos = m_Mesh.transform.position +
              (m_Mesh.transform.forward * m_ActivePopUp.GetPopUpForwardOffset()) +
              m_Mesh.transform.TransformVector(vPopupOffset);
          popUp.transform.position = vPos;
        }
        popUp.transform.parent = m_Mesh.transform;
        m_ActivePopUp.Init(gameObject, sDelayedText);
        m_ActivePopUp.SetPopupCommandParameters(iCommandParam, iCommandParam2);

        m_ActivePopUp.m_OnClose += delayedClose;
        m_PopUpGazeTimer = 0;
        m_EatInput = !m_ActivePopUp.IsLongPressPopUp();

        // If we closed a popup to create this one, we wan't don't want the standard visual
        // fade that happens when a popup comes in.  We're spoofing the transition value
        // for visuals to avoid a pop.
        if (bPopUpExisted) {
          m_ActivePopUp.SpoofTransitionValue();
        }

        // Cache the intended command.
        m_DelayedCommand = rCommand;
        m_DelayedCommandParam = iCommandParam;
        m_DelayedCommandParam2 = iCommandParam2;
      }
    }
  }

  public void PositionPopUp(Vector3 basePos) {
    m_ActivePopUp.transform.position = basePos;
  }

  public void ResolveDelayedButtonCommand(bool bConfirm, bool bKeepOpen = false) {
    PopUpWindow currentPopup = m_ActivePopUp;
    if (bConfirm) {
      SketchControlsScript.m_Instance.IssueGlobalCommand(
          m_DelayedCommand, m_DelayedCommandParam, m_DelayedCommandParam2);
    }
    // if the popup has changed since the start of the function (and it wasn't null at the start),
    // that means that the global command called kicked off a new popup. In which case we shouldn't
    // close the popup, or overwrite the delayed command.
    bool panelChanged = (currentPopup != null) && (m_ActivePopUp != currentPopup);
    if (!panelChanged) {
      m_DelayedCommand = SketchControlsScript.GlobalCommands.Null;
      m_DelayedCommandParam = -1;
      m_DelayedCommandParam2 = -1;
      if (m_ActivePopUp != null && !bKeepOpen) {
        m_ActivePopUp.RequestClose();
      }
    }
  }

  virtual public void GotoPage(int iIndex) {
  }

  virtual public void AdvancePage(int iAmount) {
  }

  public void InitForCollisionDetection() {
    for (int i = 0; i < m_PositioningSpheresTransformed.Length; ++i) {
      m_PositioningSpheresTransformed[i] = transform.TransformPoint(m_PositioningSpheres[i]);
    }
  }

  public void CalculateDepenetration(BasePanel rOther) {
    Vector3 vThemToUs = transform.position - rOther.transform.position;
    Vector3 vThemToUsNorm = vThemToUs.normalized;
    float fMaxExtent = m_PositioningExtent + rOther.GetPositioningExtent();

    if (vThemToUs.sqrMagnitude < fMaxExtent * fMaxExtent) {
      float fCombinedSphereRad = m_ScaledPositioningSphereRadius + rOther.m_ScaledPositioningSphereRadius;
      float fCombinedSphereRadSq = fCombinedSphereRad * fCombinedSphereRad;
      for (int i = 0; i < m_PositioningSpheresTransformed.Length; ++i) {
        for (int j = 0; j < rOther.m_PositioningSpheresTransformed.Length; ++j) {
          Vector3 vSphereToSphere = rOther.m_PositioningSpheresTransformed[j] - m_PositioningSpheresTransformed[i];
          if (vSphereToSphere.sqrMagnitude < fCombinedSphereRadSq) {
            float fSphereToSphereDist = vSphereToSphere.magnitude;
            float fDepenetrationAmount = fCombinedSphereRad - fSphereToSphereDist;
            m_PositioningVelocity += (vThemToUsNorm * fDepenetrationAmount * m_DepenetrationScalar * Time.deltaTime);
          }
        }
      }
    }
  }

  public void CalculateDepenetration(Vector3 vOtherPos, float fCombinedRadius) {
    Vector3 vThemToUs = transform.position - vOtherPos;
    if (vThemToUs.sqrMagnitude < fCombinedRadius * fCombinedRadius) {
      float fDepenetrationAmount = fCombinedRadius - vThemToUs.magnitude;
      m_PositioningVelocity += (vThemToUs.normalized * fDepenetrationAmount *
          m_DepenetrationScalar * Time.fixedDeltaTime);
    }
  }

  public void UpdatePositioningForces() {
    //use spring to bring us home
    Vector3 vToHome = m_PositioningHome - transform.position;
    vToHome *= m_PositioningK;
    Vector3 vDampenedVel = m_PositioningVelocity * m_PositioningDampen;
    Vector3 vSpringForce = vToHome - vDampenedVel;
    m_PositioningVelocity += vSpringForce;

    //update position
    Vector3 vPos = transform.position;
    vPos += (m_PositioningVelocity * Time.deltaTime);
    transform.position = vPos;
    if (m_NonScaleChild) {
      m_NonScaleChild.OnPosRotChanged();
    }
  }

  /// Update the delayed command parameter that is used when a popup confirmation button is pressed.
  /// This can be used when something provides more context to the initial command.
  public void UpdateDelayedCommandParameter(int newParam) {
    m_DelayedCommandParam = newParam;
  }

  static public bool DoesRayHitCollider(Ray rRay, Collider rCollider, bool skipAngleCheck = false) {
    if (!skipAngleCheck && Vector3.Angle(rRay.direction, rCollider.transform.forward) > 90.0f) {
      return false;
    }

    RaycastHit rHitInfo;
    return rCollider.Raycast(rRay, out rHitInfo, 100.0f);
  }

  static public bool DoesRayHitCollider(Ray rRay, Collider rCollider, out RaycastHit rHitInfo) {
    return rCollider.Raycast(rRay, out rHitInfo, 100.0f);
  }

  /*
   * For Debugging.
   *
  void OnDrawGizmos() {
    if (m_PositioningSpheres != null) {
      Gizmos.color = Color.yellow;
      for (int i = 0; i < m_PositioningSpheres.Length; ++i) {
        Gizmos.DrawWireSphere(transform.TransformPoint(m_PositioningSpheres[i]),
            m_ScaledPositioningSphereRadius);
      }
    }
    if (WidgetSibling != null) {
      Gizmos.color = Color.red;
      Gizmos.DrawWireSphere(transform.position, WidgetSibling.m_CollisionRadius);
    }
  }
  /**/
}
}  // namespace TiltBrush
