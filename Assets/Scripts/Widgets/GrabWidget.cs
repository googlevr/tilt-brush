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

using UnityEngine;

namespace TiltBrush {

public class GrabWidget : MonoBehaviour {
  public enum State {
    Showing,
    Visible,
    Tossed,
    Hiding,
    Invisible
  }

  public enum Axis {
    Invalid = -1,
    // These must have the same numbers as Vector3[] indices
    X = 0,
    Y = 1,
    Z = 2,
    // The rest of these are "virtual" axes, whose axis direction can't
    // be retrieved by extracting a column from the transform's mat4.
    YZ,
    XZ,
    XY,
  }

  protected static Color m_InactiveGrey = new Color(.323f, .323f, .323f);

  // Tunables. Units are units/s (linear) and deg/s (angular)

  // Time for velocity to decay to ~35% of its original value, in seconds
  const float m_LinVelDecayTime = .1f;
  const float m_AngVelDecayTime = .1f;
  // Velocity below which we clamp to zero
  const float m_LinVelEpsilonSqr = .001f;
  const float m_AngVelEpsilonSqr = .001f;
  // Velocity above which we disable damping. Overridden by subclasses.
  protected float m_AngVelDampThreshold = 100000;
  // Velocity below which we do not inherit from user motion
  const float m_LinVelInheritMin = 0.8f;
  const float m_AngVelInheritMin = 25f;
  // Maximum amount of distance that widget can be "thrown", in units and degrees
  const float m_LinDistInheritMax = 1f;
  const float m_AngDistInheritMax = 30f;

  public float m_ShowDuration;  // currently .2
  public float m_GrabDistance;
  public float m_CollisionRadius = 1.2f;

  [SerializeField] private bool m_AllowTwoHandGrab = false;
  [SerializeField] private bool m_DestroyOnHide = false;
  [SerializeField] private bool m_AllowHideWithToss = false;
  [SerializeField] private bool m_DisableDrift = false;
  [SerializeField] protected bool m_RecordMovements = false;
  [SerializeField] protected bool m_AllowSnapping = false;
  [SerializeField] private float m_SnapDisabledDelay = 0.2f;
  [SerializeField] private bool m_AllowPinning = false;
  [SerializeField] private bool m_AllowDormancy = true;
  [SerializeField] private float m_TossDuration;

  [SerializeField] protected Renderer[] m_TintableMeshes;

  [SerializeField] private Vector3 m_SpawnPlacementOffset;
  [SerializeField] private float m_IntroAnimSpinAmount = 360.0f;

  [SerializeField] protected BoxCollider m_BoxCollider;
  [SerializeField] protected Transform m_Mesh;
  [SerializeField] protected Transform[] m_HighlightMeshXfs;

  [SerializeField] protected float m_ValidSnapRotationStickyAngle;

  [SerializeField] protected Material m_SnapGhostMaterial;

  // Internal data
  protected bool m_Registered;
  protected float m_ShowTimer;
  protected State m_CurrentState;
  protected Vector3 m_Velocity_LS;
  protected Vector3 m_AngularVelocity_LS;
  protected bool m_UserInteracting = false;
  protected bool m_UserTwoHandGrabbing = false;
  protected InputManager.ControllerName m_InteractingController;
  protected float m_SnapEnabledTimeStamp;
  protected bool m_SnappingToHome;
  protected bool m_SnapDriftCancel;
  protected Quaternion m_BaseSnapRotation;
  protected bool m_IsSpinningFreely = false;
  protected bool m_Restoring = false;
  protected float m_TossTimer;
  protected NonScaleChild m_NonScaleChild;
  protected MeshFilter[] m_HighlightMeshFilters;
  protected Color m_ActiveTint = Color.white;
  protected int m_BackupLayer;
  protected ushort m_BatchId;

  // For custom flavor spawn animations.
  protected TrTransform m_xfIntroAnimSpawn_LS;
  protected TrTransform m_xfIntroAnimTarget_LS;
  protected float m_IntroAnimValue;
  protected float m_IntroAnimSpeed;

  protected bool m_Pinned;
  protected WidgetPinScript m_Pin;
  protected float m_PinScalar = 1.0f;

  protected List<Quaternion> m_ValidSnapRotations_SS;
  protected int m_PrevValidSnapRotationIndex;
  protected Transform m_SnapGhost;
  protected bool m_bWasSnapping = false;
  protected bool m_CustomShowHide = false;

  protected Collider m_GrabCollider;
  protected Dictionary<Renderer, Material[]> m_InitialMaterials;
  protected Dictionary<Renderer, Material[]> m_NewMaterials;
  protected Renderer[] m_WidgetRenderers;
  protected List<string> m_Keywords = new List<string>();
  protected bool m_Highlighted;
  protected bool m_OldHighlighted;

  private SketchGroupTag m_Group = SketchGroupTag.None;

  protected enum IntroAnimState {
    Off,
    In,
    On
  }
  protected IntroAnimState m_IntroAnimState;

  /// Canvas which contains this widget
  public CanvasScript Canvas => transform.parent.GetComponent<CanvasScript>();

  public bool AllowTwoHandGrab {
    get { return m_AllowTwoHandGrab; }
  }

  public bool AllowPinning {
    get { return m_AllowPinning && App.Config.m_AllowWidgetPinning; }
  }

  public virtual bool AllowDormancy {
    get { return m_AllowDormancy; }
  }

  public float PinScalar {
    get { return m_PinScalar; }
  }

  public bool IsSpinningFreely {
    get { return m_IsSpinningFreely; }
  }

  public bool Restoring {
    get { return m_Restoring; }
    set { m_Restoring = value; }
  }

  public bool Pinned {
    get { return m_Pinned; }
  }

  public virtual float HapticDuration {
    get { return 0.025f; }
  }

  public ushort BatchId {
    get { return m_BatchId; }
  }

  public SketchGroupTag Group {
    get => m_Group;
    set {
      var oldGroup = m_Group;
      m_Group = value;

      SelectionManager.m_Instance.OnWidgetRemovedFromGroup(this, oldGroup);
      SelectionManager.m_Instance.OnWidgetAddedToGroup(this);
    }
  }

  void ForceSnapDisabled() {
    m_SnapEnabledTimeStamp = 0.0f;
  }

  protected bool SnapEnabled {
    get {
      return Time.realtimeSinceStartup - m_SnapEnabledTimeStamp < m_SnapDisabledDelay;
    }
    set {
      if (value) {
        m_SnapEnabledTimeStamp = Time.realtimeSinceStartup;
      }
    }
  }

  public bool Showing {
    get { return m_CurrentState == State.Showing || m_CurrentState == State.Visible; }
  }

  virtual public bool SupportsNegativeSize => false;

  public static Color InactiveGrey { get { return m_InactiveGrey; } }

  protected Vector3 Velocity_GS {
    get { return ParentTransform.MultiplyVector(m_Velocity_LS); }
    set { m_Velocity_LS = ParentTransform.inverse.MultiplyVector(value); }
  }

  protected Vector3 AngularVelocity_GS {
    get { return ParentTransform.rotation * m_AngularVelocity_LS; }
    set {
      m_AngularVelocity_LS = Quaternion.Inverse(ParentTransform.rotation) * value;
      m_IsSpinningFreely = m_AngularVelocity_LS.magnitude > m_AngVelDampThreshold;
    }
  }

  public Collider GrabCollider {
    get {
      if (m_GrabCollider == null) {
        m_GrabCollider = GetComponentInChildren<Collider>();
      }
      return m_GrabCollider;
    }
  }

  /// The point about which angular velocity is applied.
  ///
  /// We assume that m_BoxCollider is symmetric and therefore the BoxCollider's
  /// position is a reasonable estimate of the center of mass/rotation.
  /// Most widgets don't have an offset here, but ModelWidgets in particular
  /// sometimes put their collider far away, since models can be very off-center.
  protected Transform CenterOfMassTransform {
    get { return m_BoxCollider != null ? m_BoxCollider.transform : transform; }
  }

  /// The pose of center of mass, relative to us (ie, in object space)
  /// Can also be thought of as WidgetFromCm
  protected TrTransform CenterOfMassPose_OS {
    get {
      TrTransform pose_OS = TrTransform.identity;
      for (var cur = CenterOfMassTransform; cur != transform; cur = cur.parent) {
        pose_OS = TrTransform.FromLocalTransform(cur) * pose_OS;
      }
      return pose_OS;
    }
  }

  /// The pose of center of mass, relative to our pos/rot parent
  /// Can also be thought of as ParentFromCm/CanvasFromCm
  protected TrTransform CenterOfMassPose_LS {
    get {
      return LocalTransform * CenterOfMassPose_OS;
    }
  }

  public virtual float MaxAxisScale {
    get {
      return Mathf.Max(transform.localScale.x,
        Mathf.Max(transform.localScale.y, transform.localScale.z));
    }
  }

  // This function is virtual so widgets with multiple, independent grab points can specify
  // specific transforms.
  virtual public Transform GrabTransform_GS {
    get { return transform; }
  }

  TrTransform ParentTransform {
    get {
      var scaleParent = transform.parent;
      var rotPosParent = (m_NonScaleChild ? m_NonScaleChild.parent : transform.parent);
      // return a mix of the two
      TrTransform xf = Coords.AsGlobal[scaleParent];
      xf.translation = rotPosParent.position;
      xf.rotation = rotPosParent.rotation;
      return xf;
    }
  }

  /// The parent of a GrabWidget is always a Canvas, so this is the same as
  /// the canvas-space pose.
  /// It is illegal to modify GrabWidget.transform.localScale in any way except
  /// through public GrabWidget API (ie, the Size and LocalTransform properties,
  /// or Get/SetWidgetSize())
  public TrTransform LocalTransform {
    get {
      TrTransform xf = m_NonScaleChild == null ?
        TrTransform.FromLocalTransform(transform) : m_NonScaleChild.PositionRotationInParentSpace;
      xf.scale = GetSignedWidgetSize();
      return xf;
    }
    set {
      if (m_NonScaleChild != null) {
        m_NonScaleChild.PositionRotationInParentSpace = value;
      } else {
        transform.localPosition = value.translation;
        transform.localRotation = value.rotation;
      }
      SetSignedWidgetSize(value.scale);
    }
  }

  /// Stores one extra bit of pertinent information.
  /// Fluid because each type of widget has a different need.
  /// This is opaque; do not do anything with it besides assign back to CustomDimension.
  /// Because of implementation issues in MoveWidgetCommand (see constructor docs), this
  /// value must be orthogonal to LocalTransform -- modifying LocalTransform must not cause
  /// changes to CustomDimension, and vice versa.
  public virtual Vector3 CustomDimension {
    get { return Vector3.one; }
    // ReSharper disable once ValueParameterNotUsed
    // Intentionally not used.
    set { }
  }

  /// Returns Axis.Invalid unless the widget is currently being manipulated
  /// along a specific axis.
  ///
  /// Pass:
  ///   handA, handB - grip positions, in arbitrary order
  ///
  /// Returns:
  ///   The extent and direction of manipulation of the widget along the returned axis,
  ///   in global space.
  ///   (only valid if axis != Invalid)
  ///
  public virtual Axis GetScaleAxis(Vector3 handA, Vector3 handB, out Vector3 axisDirection, out float axisExtent) {
    axisDirection = default(Vector3);
    axisExtent = default(float);
    return Axis.Invalid;
  }

  // Return the bounds in selection canvas space.
  public virtual Bounds GetBounds_SelectionCanvasSpace() {
    if (m_BoxCollider != null) {
      TrTransform boxColliderToCanvasXf = App.Scene.SelectionCanvas.Pose.inverse *
          TrTransform.FromTransform(m_BoxCollider.transform);
      Bounds bounds = new Bounds(boxColliderToCanvasXf * m_BoxCollider.center, Vector3.zero);

      // Transform the corners of the widget bounds into canvas space and extend the total bounds
      // to encapsulate them.
      for (int i = 0; i < 8; i++) {
        bounds.Encapsulate(boxColliderToCanvasXf * (m_BoxCollider.center + Vector3.Scale(
            m_BoxCollider.size,
            new Vector3((i & 1) == 0 ? -0.5f : 0.5f,
                        (i & 2) == 0 ? -0.5f : 0.5f,
                        (i & 4) == 0 ? -0.5f : 0.5f))));
      }

      return bounds;
    }
    return new Bounds();
  }

  public bool IsUserInteracting(InputManager.ControllerName interactionController) {
    return m_UserInteracting && interactionController == m_InteractingController;
  }

  public void UserInteracting(bool interacting,
      InputManager.ControllerName controller = InputManager.ControllerName.None) {
    // Update state before calling OnUserBegin and OnUserEnd so we can use that state in
    // those functions.
    bool prevInteracting = m_UserInteracting;
    m_UserInteracting = interacting;
    m_InteractingController = controller;

    if (prevInteracting != m_UserInteracting) {
      if (interacting) {
        OnUserBeginInteracting();
      } else {
        OnUserEndInteracting();
      }
    }

    // Keep pin visible if we're holding it and it's pinned.
    if (Pinned && m_Pin != null && !m_Pin.IsAnimating()) {
      m_Pin.ShowWidgetAsPinned();
    }
  }

  public bool UserTwoHandGrabbing {
    get { return m_UserTwoHandGrabbing; }
  }

  // If true, caller must also pass:
  //   primary - the hand that first grabbed the object. Guaranteed to be inside.
  //   secondary - the other hand grabbing the object. Not guaranteed to be inside.
  //   secondaryInObject
  public void SetUserTwoHandGrabbing(
      bool value,
      InputManager.ControllerName primary=InputManager.ControllerName.None,
      InputManager.ControllerName secondary=InputManager.ControllerName.None,
      bool secondaryInObject=false) {
    if (value != m_UserTwoHandGrabbing) {
      if (value) {
        Vector3 vPrimary = InputManager.Controllers[(int)primary].Transform.position;
        Vector3 vSecondary = InputManager.Controllers[(int)secondary].Transform.position;
        OnUserBeginTwoHandGrab(vPrimary, vSecondary, secondaryInObject);
      } else {
        OnUserEndTwoHandGrab();
      }
    }

    if (value) {
      m_InteractingController = primary;
    }
  }

  virtual public int GetTiltMeterCost() { return 0; }

  // Returns true if the widget is in a state that should trigger collisions.
  // May be overridden in subclasses if there are different requirements for
  // collisions other than movement.
  virtual public bool IsCollisionEnabled() {
    return IsMoving();
  }

  /// Returns true if either angular or linear velocity is > 0
  public bool IsMoving() {
    return m_Velocity_LS.sqrMagnitude > 0.0f || m_AngularVelocity_LS.sqrMagnitude > 0.0f;
  }

  /// Returns true if we think the user intends to throw this thing away
  bool IsHideToss(Vector3 vLinVel, Vector3 vAngVel, Vector3 vPivot) {
    if (m_Pinned || ! m_AllowHideWithToss) {
      return false;
    }

    var SCS = SketchControlsScript.m_Instance;
    Vector3 vLinVelMeters = vLinVel * App.UNITS_TO_METERS;

    // Add the component due to angular motion about a pivot
    {
      Vector3 r = (CenterOfMassTransform.position - vPivot) * App.UNITS_TO_METERS;
      r = r.normalized * Mathf.Min(SCS.m_TossMaxPivotDistMeters, r.magnitude);
      Vector3 omega = vAngVel * Mathf.Deg2Rad;
      vLinVelMeters += Vector3.Cross(omega, r);
    }

    return vLinVelMeters.magnitude >= SCS.m_TossThresholdMeters;
  }

  public void SetCanvas(CanvasScript newCanvas) {
    // Walk up from our current transform until we find the original canvas script.
    Transform currentTransform = transform;
    CanvasScript originalCanvas = null;
    while (originalCanvas == null && currentTransform != null) {
      originalCanvas = currentTransform.GetComponent<CanvasScript>();
      currentTransform = currentTransform.parent;

      // TODO: NonScaleChild transforms don't actually live in the canvas hierarchy and
      // and we don't support changing their canvas. At some point, NonScaleChild will probably
      // go away (because symmetry widget will be deprecated in favor of a mirror guide widget).
      Debug.Assert(currentTransform.GetComponent<NonScaleChild>() == null);
    }

    // Set the new widget size. This needs to be done right after the transform has been reparented
    // because both those operations adjust the local scale.
    Debug.Assert(originalCanvas != null);
    float originalScale = originalCanvas.Pose.scale;
    float newScale = newCanvas.Pose.scale;
    float originalWidgetSize = GetSignedWidgetSize();
    transform.parent = newCanvas.transform;
    SetSignedWidgetSize(originalWidgetSize * originalScale / newScale);

    // Set the canvas parent.
    if (m_NonScaleChild != null) {
      m_NonScaleChild.ParentCanvas = newCanvas;
    }

    var addKeywords = newCanvas.BatchManager.MaterialKeywords.Except(
        originalCanvas.BatchManager.MaterialKeywords);
    var removeKeywords = originalCanvas.BatchManager.MaterialKeywords.Except(
        newCanvas.BatchManager.MaterialKeywords);

    foreach (string keyword in addKeywords) {
      EnableKeyword(keyword);
    }

    foreach (string keyword in removeKeywords) {
      DisableKeyword(keyword);
    }
  }

  /// This function enables keywords from the parent canvas of the passed transform.
  /// If the passed transform does not have a parent canvas, nothing happens.
  public void TrySetCanvasKeywordsFromObject(Transform xf) {
    CanvasScript canvas = null;
    while (canvas == null && xf != null) {
      canvas = xf.GetComponent<CanvasScript>();
      xf = xf.parent;
    }

    if (canvas != null) {
      var keywords = canvas.BatchManager.MaterialKeywords;
      foreach (string keyword in keywords) {
        EnableKeyword(keyword);
      }
    }
  }

  // vLinVel: units/s
  // vAngVel: degrees/s
  // vPivot: world-space position about which the angular velocity is applied
  public void SetVelocities(Vector3 vLinVel, Vector3 vAngVel, Vector3 vPivot) {
    if (IsHideToss(vLinVel, vAngVel, vPivot)) {
      StartHideToss(vLinVel, vAngVel, vPivot);
      return;
    }

    // Dropping widgets feels very strange if only one of the two
    // velocities is respected. But: in cases where only one of the speeds
    // breaks the threshold, should we choose to inherit, or not-inherit?
    float linSpeed = vLinVel.magnitude;
    float angSpeed = vAngVel.magnitude;
    bool bInherit = (linSpeed > m_LinVelInheritMin || angSpeed > m_AngVelInheritMin);

    // Because of our decay function, we can write v(t) in closed form
    //  v(t) = v0 * exp (k*t)
    //         where k = -1 / decaytime
    // which can be integrated to find the (finite!) maximum distance
    // as a function of v0, which can then be inverted to find the
    // maximum allowable v0 as a function of desired max distance.
    float linVelInheritMax = m_LinDistInheritMax / m_LinVelDecayTime;
    float angVelInheritMax = m_AngDistInheritMax / m_AngVelDecayTime;
    if (! bInherit) {
      Velocity_GS = Vector3.zero;
    } else {
      Velocity_GS = vLinVel.normalized * Mathf.Min(linSpeed, linVelInheritMax);
    }

    bool wasSpinningFreely = m_IsSpinningFreely;
    m_IsSpinningFreely = (angSpeed >= m_AngVelDampThreshold);
    if (! bInherit && ! m_IsSpinningFreely) {
      m_AngularVelocity_LS = Vector3.zero;
    } else if (angSpeed > angVelInheritMax && !m_IsSpinningFreely) {
      AngularVelocity_GS = vAngVel * (angVelInheritMax / angSpeed);
    } else {
      AngularVelocity_GS = vAngVel;
    }

    // To convert angular to linear velocity: v = omega x r
    // - omega is angular velocity in radians-per-time
    // - r is the radial vector from the center-of-rotation to the point whose
    //   velocity we want.
    {
      Vector3 omega = AngularVelocity_GS * Mathf.Deg2Rad;
      Vector3 r = CenterOfMassTransform.position - vPivot;
      Velocity_GS += Vector3.Cross(omega, r);
    }

    // A series of checks to see if we should clear our velocities.
    if (m_CurrentState == State.Tossed) {
      // Tossed widgets should keep moving
    } else if (m_IsSpinningFreely) {
      // Spinning widgets should keep moving.
    } else {
      if (m_DisableDrift || m_SnapDriftCancel || m_Pinned) {
        ClearVelocities();
      }
    }

    if (!wasSpinningFreely && m_IsSpinningFreely) {
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new MoveWidgetCommand(this, LocalTransform, CustomDimension, final: true));
    }
  }

  public void ClearVelocities() {
    // Separated out mostly to filter out bogus calls to SetVelocities()
    // in the debugger.
    m_Velocity_LS = m_AngularVelocity_LS = Vector3.zero;
  }

  protected float GetShowRatio() { return m_ShowTimer / m_ShowDuration; }

  virtual protected void Awake() {
    // TODO : Why do we serialize transforms when we pull the mesh filter out
    // and never use the transform?  We should just serialize the filters.
    if (m_HighlightMeshXfs != null) {
      m_HighlightMeshFilters = m_HighlightMeshXfs.Select(x => x.GetComponent<MeshFilter>()).ToArray();
    }

    m_CurrentState = State.Invisible;
    Activate(false);
    m_NonScaleChild = gameObject.GetComponent<NonScaleChild>();
    m_IntroAnimValue = 0.0f;
    m_IntroAnimSpeed = 1.0f / m_ShowDuration;
    m_IntroAnimState = IntroAnimState.Off;
    m_SnappingToHome = false;
    m_BatchId = 0;

    if (m_AllowSnapping) {
      m_PrevValidSnapRotationIndex = -1;
      m_ValidSnapRotations_SS = new List<Quaternion>();
      float fYawInc = 90.0f;
      float fPitchInc = 90.0f;
      float fRollInc = 90.0f;
      for (float yaw = -180.0f; yaw <= 180.0f; yaw += fYawInc) {
        for (float pitch = -180.0f; pitch <= 180.0f; pitch += fPitchInc) {
          for (float roll = -180.0f; roll <= 180.0f; roll += fRollInc) {
            m_ValidSnapRotations_SS.Add(Quaternion.Euler(roll, yaw, pitch));
          }
        }
      }
    }

    RegisterWithWidgetManager();
  }

  virtual protected void Start() {
    RegisterWithWidgetManager();
  }

  void RegisterWithWidgetManager() {
    if (!m_Registered && WidgetManager.m_Instance != null) {
      WidgetManager.m_Instance.RegisterGrabWidget(gameObject);
      m_Registered = true;
    }
  }

  /// Called when transitioning into State.Hiding.
  protected virtual void OnHideStart() { }

  virtual public void Show(bool bShow, bool bPlayAudio = true) {
    //if we're hiding from a toss, don't zero out our velocities
    if (bShow || m_CurrentState != State.Tossed) {
      ClearVelocities();
    }

    if (bShow) {
      gameObject.SetActive(true);
      //if we just switched to showing, trigger a sound effect
      if ((m_CurrentState == State.Hiding || m_CurrentState == State.Invisible) && bPlayAudio) {
        AudioManager.m_Instance.ShowHideWidget(true, transform.position);
      }
      m_CurrentState = State.Showing;
      OnShow();
    } else {
      //if we just switched to hiding, trigger a sound effect
      if ((m_CurrentState == State.Showing || m_CurrentState == State.Visible) && bPlayAudio) {
        // TODO: Play sound only if hidden by clicking button, not if tossing
        // Second case is handled in StartHideToss()
        AudioManager.m_Instance.ShowHideWidget(false, transform.position);
      }
      m_CurrentState = State.Hiding;
      OnHideStart();
    }
  }

  virtual public GrabWidget Clone() {
    Debug.LogWarning("You're cloning a base GrabWidget. This is probably not what you intended.");
    GrabWidget clone = GameObject.Instantiate(this);
    clone.transform.parent = transform.parent;
    HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
    return clone;
  }

  public void HaltDrift() {
    ClearVelocities();
  }

  public bool IsAvailable() { return m_CurrentState == State.Showing || m_CurrentState == State.Visible; }

  void StartHideToss(Vector3 vLinVel, Vector3 vAngVel, Vector3 vPivot) {
    Vector3 r = CenterOfMassTransform.position - vPivot;
    Velocity_GS = vLinVel + Vector3.Cross(vAngVel * Mathf.Deg2Rad, r);
    AngularVelocity_GS = vAngVel;

    // Play sound at the beginning of toss animation
    AudioManager.m_Instance.ShowHideWidget(false, transform.position);
    m_CurrentState = State.Tossed;
    m_TossTimer = m_TossDuration;
  }

  public bool IsTossed() {
    return m_CurrentState == State.Tossed;
  }

  public bool IsHiding() {
    return m_CurrentState == State.Hiding;
  }

  public void Hide() {
    m_CurrentState = State.Invisible;
    ClearVelocities();
    OnHide();
  }

  // State change for use by undo stack
  public virtual void RestoreFromToss() {
    Show(true);
    m_ShowTimer = m_ShowDuration;
  }

  public bool VerifyVisibleState(MoveWidgetCommand comm) {
    if (m_CurrentState == State.Tossed || m_CurrentState == State.Hiding) {
      m_ShowTimer = m_ShowDuration;
      m_CurrentState = State.Visible;
      // User has indicated that this action is meant to hide the widget, but the hiding
      // animation hasn't been completed yet.
      // TODO: Separate widget animations more cleanly from undo stack.
      comm.Merge(new HideWidgetCommand(this));
      return true;
    }
    return false;
  }

  void Update() {
    switch (m_CurrentState) {
    case State.Showing:
      m_ShowTimer += Time.deltaTime;
      if (m_ShowTimer >= m_ShowDuration) {
        m_ShowTimer = m_ShowDuration;
        m_CurrentState = State.Visible;
      }
      break;
    case State.Visible:
      UpdatePositionAndVelocities();
      break;
    case State.Tossed:
      UpdatePositionAndVelocities();
      m_TossTimer -= Time.deltaTime;
      if (m_TossTimer <= 0.0f) {
        Show(false);
        OnTossComplete();
      }
      break;
    case State.Hiding:
      UpdatePositionAndVelocities();
      m_ShowTimer -= Time.deltaTime;
      if (m_ShowTimer <= 0.0f) {
        m_ShowTimer = 0.0f;
        if (m_RecordMovements && !m_CustomShowHide) {
          SketchMemoryScript.m_Instance.PerformAndRecordCommand(new HideWidgetCommand(this));
        } else {
          Hide();
        }
      }
      break;
    case State.Invisible: break;
    }

    UpdateIntroAnimState();

    OnUpdate();
  }

  private void LateUpdate() {
#if UNITY_ANDROID
    if (m_Highlighted != m_OldHighlighted) {
      if (m_Highlighted) {
        AddKeyword("HIGHLIGHT_ON");
      } else {
        RemoveKeyword("HIGHLIGHT_ON");
      }
    }
    m_OldHighlighted = m_Highlighted;
    m_Highlighted = false;
#endif
  }

  private void AddKeyword(string keyword) {
    m_Keywords.Add(keyword);
    EnableKeyword(keyword);
  }

  private void RemoveKeyword(string keyword) {
    m_Keywords.Remove(keyword);
    DisableKeyword(keyword);
    if (m_Keywords.Count == 0) {
      RestoreSharedMaterials();
    }
  }

  private void EnableKeyword(string keyword) {
    if (m_InitialMaterials == null) {
      m_WidgetRenderers = GetComponentsInChildren<Renderer>();
      m_InitialMaterials = m_WidgetRenderers.ToDictionary(x => x, x => x.sharedMaterials);
      m_NewMaterials = m_WidgetRenderers.ToDictionary(x => x, x => x.materials);
    }

    foreach (var renderer in m_WidgetRenderers) {
      var materials = m_NewMaterials[renderer];
      foreach (var material in materials) { 
        material.EnableKeyword(keyword);
      }
      renderer.materials = materials;
    }
  }

  private void DisableKeyword(string keyword) {
    if (m_WidgetRenderers == null) {
      Debug.LogError("GrabWidget.DisableKeyword called, but m_WidgetRenderers is null!");
      return;
    }
    foreach (var renderer in m_WidgetRenderers) {
      var materials = m_NewMaterials[renderer];
      foreach (var material in materials) { 
        material.DisableKeyword(keyword);
      }
      renderer.materials = materials;
    }
  }

  /// It is necessary to call this function when cloning a widget as the widget will be selected
  /// and the clone will not have these values set, although they will be expected when deselection
  /// happens.
  protected void CloneInitialMaterials(GrabWidget other) {
    m_WidgetRenderers = GetComponentsInChildren<Renderer>();
    m_InitialMaterials = m_WidgetRenderers.ToDictionary(x => x, x => x.sharedMaterials);
    m_NewMaterials = m_WidgetRenderers.ToDictionary(x => x, x => x.materials);
  }

  private void RestoreSharedMaterials() {
    if (m_WidgetRenderers == null) {
      Debug.LogError("GrabWidget.RestoreSharedMaterials called, but m_WidgetRenderers is null!");
      return;
    }
    foreach (var renderer in m_WidgetRenderers) {
      renderer.materials = m_InitialMaterials[renderer];
    }
  }

  public void HideNow() {
    if (m_CurrentState == State.Tossed) {
      m_TossTimer = 0;
      Show(false);
      OnTossComplete();
    }
    m_ShowTimer = 0;
  }

  static protected TrTransform WithUnitScale(TrTransform xf) {
    return TrTransform.TRS(xf.translation, xf.rotation, 1);
  }

  // Integrate angular and linear velocity into a delta-transform, and apply to widget.
  void UpdatePositionAndVelocities() {
    // Nomenclature for this function:
    // - LS means "parent-local space", or "Local". Think of it as canvas space.
    // - The coordinate system of the center of massis more well-known in dynamics
    //   as the "Body Frame". So, "Body" rather than "CenterOfMassSpace".

    bool wasMoving = IsMoving();
    if (!wasMoving) {
      // Skip the command-recording and the assignment to LocalTransform
      return;
    }

    TrTransform xfDelta_LS; {
      // We store the velocities in canvas-space so they're invariant to the
      // rotation, but they are easiest to apply in body space.

      // Do this all without scale, because it's impossible to apply a canvas-space
      // velocity if the CM is scaled to zero. No matter how fast the CM moves,
      // if the widget has 0 scale, then to the canvas the CM hasn't moved. This
      // manifests as velocity_BS going to infinity when widget.size goes to zero.
      TrTransform bodyNoScale_LS = WithUnitScale(CenterOfMassPose_LS);
      TrTransform bodyFromLocal = bodyNoScale_LS.inverse;

      // Angular velocity is normal-like; it's a direction (a normal) * degrees/sec
      Vector3 angularVelocity_BS = bodyFromLocal.MultiplyNormal(m_AngularVelocity_LS);
      // Linear velocity is vector-like; it gets scaled
      Vector3 velocity_BS = bodyFromLocal.MultiplyVector(m_Velocity_LS);
      float dt = Time.deltaTime;
      TrTransform xfDelta_BS = TrTransform.TR(
        velocity_BS * dt,
        Quaternion.AngleAxis(
          angularVelocity_BS.magnitude * dt,
          angularVelocity_BS.normalized));

      xfDelta_LS = xfDelta_BS.TransformBy(bodyNoScale_LS);
      // No input scale means no output scale, but be defensive:
      // verify that TransformBy produces exact results, and deal with it if it doesn't.
      Debug.Assert(xfDelta_LS.scale == 1f);
      xfDelta_LS.scale = 1;
    }

    bool bFadingAway = (m_CurrentState == State.Tossed) || (m_CurrentState == State.Hiding);

    // Move velocities toward zero
    if (m_Velocity_LS.sqrMagnitude < m_LinVelEpsilonSqr) {
      m_Velocity_LS = Vector3.zero;
    } else if (!bFadingAway) {
      float fDecayAmount = Mathf.Exp(-Time.deltaTime / m_LinVelDecayTime);
      m_Velocity_LS *= fDecayAmount;
    }

    float omegaSqr = m_AngularVelocity_LS.sqrMagnitude;
    if (omegaSqr < m_AngVelEpsilonSqr) {
      m_AngularVelocity_LS = Vector3.zero;
    } else if (!m_IsSpinningFreely && !bFadingAway) {
      float fDecayAmount = Mathf.Exp(-Time.deltaTime / m_AngVelDecayTime);
      m_AngularVelocity_LS *= fDecayAmount;
    }

    // Apply the delta
    TrTransform newLocalTransform = xfDelta_LS * LocalTransform;

    // When the mirror drifts, we are iterating "x = delta * x"
    // This will slowly build up error if delta is slightly non-unit
    newLocalTransform.rotation = newLocalTransform.rotation.normalized();

    if (m_RecordMovements && !m_IsSpinningFreely && !bFadingAway) {
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new MoveWidgetCommand(this, newLocalTransform, CustomDimension, final: !IsMoving()),
        discardIfNotMerged: true);
    } else {
      LocalTransform = newLocalTransform;
    }
  }

  /// Size for "SupportsNegativeSize == true" widgets can be negative. This can happen through the
  /// flip selection command which inverts the scale.
  public void RecordAndSetSize(float value) {
    if (m_RecordMovements) {
      TrTransform resizedXf = LocalTransform;
      var sizeRange = GetWidgetSizeRange();
      if (!SupportsNegativeSize && value < 0) {
        throw new System.NotSupportedException($"Negative size not allowed for {GetType()}.");
      }
      resizedXf.scale =
          Mathf.Sign(value) * Mathf.Clamp(Mathf.Abs(value), sizeRange.x, sizeRange.y);
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(
          new MoveWidgetCommand(this, resizedXf, CustomDimension));
    } else {
      SetSignedWidgetSize(value);
    }
  }

  /// Pass a global-space transform.
  /// scale is currently ignored. Use RecordAndSetSize() instead.
  /// If you call multiple RecordAndSet* methods, this must be the final one you call.
  virtual public void RecordAndSetPosRot(TrTransform inputXf) {
    // Refresh snap input and enter/exit snapping state.
    if (m_AllowSnapping) {
      SnapEnabled = InputManager.Controllers[(int)m_InteractingController].GetCommand(
          InputManager.SketchCommands.MenuContextClick) &&
          SketchControlsScript.m_Instance.ShouldRespondToPadInput(m_InteractingController) &&
          !m_Pinned;

      if (!m_bWasSnapping && SnapEnabled) {
        InitiateSnapping();
      }

      if (m_bWasSnapping && !SnapEnabled) {
        FinishSnapping();
        m_SnapDriftCancel = false;
      }
    }

    var xf_GS = GetDesiredTransform(inputXf);

    if (m_RecordMovements) {
      TrTransform newXf = TrTransform.FromTransform(
        m_NonScaleChild == null ? transform.parent : m_NonScaleChild.parent).inverse * xf_GS;
      newXf.scale = GetSignedWidgetSize();

      SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new MoveWidgetCommand(this, newXf, CustomDimension));
    } else {
      transform.position = xf_GS.translation;
      transform.rotation = xf_GS.rotation;

      if (m_NonScaleChild != null) {
        m_NonScaleChild.OnPosRotChanged();
      }
    }

    if (m_SnapGhost != null && SnapEnabled) {
      m_SnapGhost.position = inputXf.translation;
      m_SnapGhost.rotation = inputXf.rotation;
    }

    m_bWasSnapping = SnapEnabled;

    OnEndUpdateWithDesiredTransform();
  }

  virtual public TrTransform GetGrabbedTrTransform() {
    if (SnapEnabled && m_SnapGhost != null) {
      return TrTransform.FromTransform(m_SnapGhost.transform);
    }
    return TrTransform.FromTransform(transform);
  }

  virtual protected void InitiateSnapping() {
    if (m_SnapGhost) {
      m_SnapGhost.localPosition = Vector3.zero;
      m_SnapGhost.localRotation = Quaternion.identity;
      m_SnapGhost.gameObject.SetActive(true);
      m_PrevValidSnapRotationIndex = -1;
      if (CanSnapToHome()) {
        WidgetManager.m_Instance.EnableHome(true);
      }
    }
  }

  virtual protected void FinishSnapping() {
    if (m_SnapGhost) {
      m_SnapGhost.gameObject.SetActive(false);
      WidgetManager.m_Instance.EnableHome(false);
    }
  }

  // Takes the current widget xf in global space and returns the snapped xf in global space.
  // The scale of the returned TrTransform should be ignored.
  virtual protected TrTransform GetSnappedTransform(TrTransform xf_GS) {
    TrTransform outXf_GS = xf_GS;
    int iNearestIndex = GetBestSnapRotationIndex(xf_GS.rotation);

    // Update our rotation if we found a valid, new index and the dot value is
    // beyond our sticky threshold.
    if (iNearestIndex != -1 && iNearestIndex != m_PrevValidSnapRotationIndex) {
      bool bUpdateRotation = true;
      if (m_PrevValidSnapRotationIndex != -1) {
        float a = Quaternion.Angle(xf_GS.rotation, App.Scene.Pose.rotation *
            m_ValidSnapRotations_SS[m_PrevValidSnapRotationIndex]);
        bUpdateRotation = a > m_ValidSnapRotationStickyAngle;
      }

      if (bUpdateRotation) {
        m_PrevValidSnapRotationIndex = iNearestIndex;
      }
    }
    outXf_GS.rotation =
      App.Scene.Pose.rotation * m_ValidSnapRotations_SS[m_PrevValidSnapRotationIndex];

    Quaternion qDelta = outXf_GS.rotation * Quaternion.Inverse(xf_GS.rotation);
    Vector3 grabSpot = InputManager.m_Instance.GetControllerPosition(m_InteractingController);
    Vector3 grabToCenter = xf_GS.translation - grabSpot;
    outXf_GS.translation = grabSpot + qDelta * grabToCenter;

    return outXf_GS;
  }

  protected int GetBestSnapRotationIndex(Quaternion rot) {
    float fNearestDot = 0.0f;
    int iNearestIndex = -1;
    for (int i = 0; i < m_ValidSnapRotations_SS.Count; ++i) {
      float dot = Mathf.Abs(Quaternion.Dot(rot,
          App.Scene.Pose.rotation * m_ValidSnapRotations_SS[i]));
      if (dot > fNearestDot) {
        fNearestDot = dot;
        iNearestIndex = i;
      }
    }
    return iNearestIndex;
  }

  protected void InitSnapGhost(Transform media, Transform parent) {
    m_SnapGhost = Instantiate(media);
    m_SnapGhost.gameObject.name = "SnapGhost";
    m_SnapGhost.position = parent.position;
    m_SnapGhost.rotation = parent.rotation;
    m_SnapGhost.parent = parent;
    m_SnapGhost.localScale = Vector3.one;
    HierarchyUtils.RecursivelySetLayer(m_SnapGhost, LayerMask.NameToLayer("Panels"));
    HierarchyUtils.RecursivelyDisableShadows(m_SnapGhost);
    HierarchyUtils.RecursivelySetMaterial(m_SnapGhost, m_SnapGhostMaterial);
    m_SnapGhost.gameObject.SetActive(false);
  }

  // Returns the offset in global space from the current location given the snapped orientation.
  protected virtual Vector3 GetHomeSnapLocation(Quaternion snapOrient) {
    // Figure out what our bottom is, with that hypothetical orientation.
    Vector3 vForward = snapOrient * Vector3.forward;
    Vector3 vUp = snapOrient * Vector3.up;
    Vector3 vRight = snapOrient * Vector3.right;
    Vector3 vSize = HomeSnapOffset;

    // Look for the longest of the Y components of our transformed cardinal directions.
    // The longest signifies the most "downward" (or upward).
    Vector3 vOffset = Vector3.zero;
    float fAbsFY = Mathf.Abs(vForward.y);
    float fAbsUY = Mathf.Abs(vUp.y);
    float fAbsRY = Mathf.Abs(vRight.y);
    if (fAbsFY > fAbsUY && fAbsFY > fAbsRY) {
      vOffset = vForward;
      vOffset.y *= vSize.z * Mathf.Sign(vForward.y);
    } else if (fAbsUY > fAbsRY) {
      vOffset = vUp;
      vOffset.y *= vSize.y * Mathf.Sign(vUp.y);
    } else {
      vOffset = vRight;
      vOffset.y *= vSize.x * Mathf.Sign(vRight.y);
    }
    return vOffset;
  }

  // Extension point for UpdateWithDesiredTransform
  // Takes the xf from controller input in global space and applies snapping logic.
  // Returns the desired xf in global space.
  virtual protected TrTransform GetDesiredTransform(TrTransform xf_GS) {
    TrTransform outXf_GS = xf_GS;
    if (m_AllowSnapping && SnapEnabled) {
      outXf_GS = GetSnappedTransform(xf_GS);

      // If we're near snap home, put us there.
      if (CanSnapToHome()) {
        bool bGhostWasVisible = (m_SnapGhost != null) ?
            m_SnapGhost.gameObject.activeSelf : false;

        // Hypothetically, if we were to snap, what should our orientation be?
        Quaternion qSnapOrient = transform.rotation;
        int iNearestIndex = GetBestSnapRotationIndex(qSnapOrient);
        if (iNearestIndex != -1) {
          qSnapOrient = App.Scene.Pose.rotation * m_ValidSnapRotations_SS[iNearestIndex];
        }

        Vector3 vOffset = GetHomeSnapLocation(qSnapOrient);

        // Update hint line for home.
        WidgetManager.m_Instance.UpdateHomeHintLine(xf_GS.translation - vOffset);

        if (WidgetManager.m_Instance.IsOriginHomeWithinSnapRange(
            xf_GS.translation - vOffset)) {
          // We're near home, so lock our position and rotation.
          Transform xf = GetOriginHomeXf();
          outXf_GS.rotation = qSnapOrient;
          outXf_GS.translation = xf.position + vOffset;

          m_SnappingToHome = true;
        } else {
          // If we're not near home, keep the ghost locked and appropriately active.
          if (m_SnapGhost != null) {
            m_SnapGhost.gameObject.SetActive(bGhostWasVisible);
          }
          m_SnappingToHome = false;
        }
      }
    }
    return outXf_GS;
  }

  // Extension point for RecordAndSet*.
  // Called after it has finished doing all its work.
  virtual protected void OnEndUpdateWithDesiredTransform() { }

  public void PinFromSave() {
    if (App.Config.m_AllowWidgetPinning) {
      Pin(bPin: true, fromSave: true);
    }
  }

  public void SetPinned(bool bPin, bool fromSave = false) {
    m_Pinned = bPin;

    // Destroy any lingering pin we have around and get a new one.
    WidgetManager.m_Instance.DestroyWidgetPin(m_Pin);
    m_Pin = WidgetManager.m_Instance.GetWidgetPin();
    m_Pin.SuppressAudio = fromSave;
    InitPin();

    // Animate pin according to command.
    m_Pin.gameObject.SetActive(true);
    if (bPin) {
      if (SketchControlsScript.m_Instance.IsUserGrabbingWidget(this)) {
        m_Pin.PinWidget();
      } else {
        m_Pin.WobblePin(m_InteractingController);
      }
    } else {
      m_Pin.UnpinWidget();
    }

    // This is a temp pin, so flag it to free itself.
    m_Pin.SuppressAudio = false;
    m_Pin.DestroyOnStateComplete();
  }

  public void Pin(bool bPin, bool fromSave = false) {
    if (m_RecordMovements && !fromSave) {
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(new PinWidgetCommand(this, bPin));
    } else {
      SetPinned(bPin, fromSave);
    }

    // When coming from save, don't show our pin visuals.
    if (fromSave && m_Pin) {
      m_Pin.gameObject.SetActive(false);
    }

    if (bPin && m_SnapGhost) {
      m_SnapGhost.gameObject.SetActive(false);
    }
    // Home doesn't need to be enabled when switching pin states.
    WidgetManager.m_Instance.EnableHome(false);
  }

  public void VisualizePinState() {
    if (AllowPinning && m_Pinned && !WidgetManager.m_Instance.WidgetsDormant) {
      if (m_Pin == null) {
        m_Pin = WidgetManager.m_Instance.GetWidgetPin();
        InitPin();
        m_Pin.gameObject.SetActive(true);
        m_Pin.DestroyOnStateComplete();
      }
      m_Pin.ShowWidgetAsPinned();
    }
  }

  // This is a virtual function so derived classes can define the pin parent.
  virtual protected void InitPin() {
    m_Pin.Init(transform, this);
  }

  virtual public void Activate(bool bActive) {
    if (m_TintableMeshes != null) {
      Color rMatColor = bActive && !WidgetManager.m_Instance.WidgetsDormant ?
          m_ActiveTint : m_InactiveGrey;
      for (int i = 0; i < m_TintableMeshes.Length; ++i) {
        m_TintableMeshes[i].material.color = rMatColor;
      }
    }
    if (bActive) {
      // Render the highlight mask into the stencil buffer
      RegisterHighlight();

      // Set appropriate post process values for the highlight post effecct
      if (m_UserInteracting) {
        Shader.SetGlobalFloat("_GrabHighlightIntensity", 1.0f);
      } else {
        Shader.SetGlobalFloat("_GrabHighlightIntensity", 0.5f);
      }

      // When someone tries to manipulate a pinned object,
      // show additive fill along with pin, tooltip, and a haptic pulse.
      if (m_UserInteracting && Pinned) {
        Shader.SetGlobalFloat("_NoGrabHighlightIntensity", 1.0f);
      } else {
        Shader.SetGlobalFloat("_NoGrabHighlightIntensity", 0.0f);
      }
    }
  }

  virtual public void RegisterHighlight() {
#if !UNITY_ANDROID
    if (m_HighlightMeshFilters != null) {
      for (int i = 0; i < m_HighlightMeshFilters.Length; i++) {
        App.Instance.SelectionEffect.RegisterMesh(m_HighlightMeshFilters[i]);
      }
    }
#else
    m_Highlighted = true;
#endif
  }

  virtual protected void UnregisterHighlight() {
#if !UNITY_ANDROID
    if (m_HighlightMeshFilters != null) {
      for (int i = 0; i < m_HighlightMeshFilters.Length; i++) {
        App.Instance.SelectionEffect.UnregisterMesh(m_HighlightMeshFilters[i]);
      }
    }
#else
    m_Highlighted = false;
#endif
  }

  virtual public bool HasHoverInteractions() { return false; }

  virtual public void AssignHoverControllerMaterials(InputManager.ControllerName controller) {}

  virtual public void AssignControllerMaterials(InputManager.ControllerName controller) {
    if (m_InteractingController == InputManager.ControllerName.None) {
      return;
    }

    // If the widget is pinned, don't pretend like we can snap it to things.
    bool show = m_AllowSnapping && !Pinned;
    InputManager.GetControllerGeometry(m_InteractingController)
                .TogglePadSnapHint(SnapEnabled, show);
  }

  // Returns distance from center of collider if point is inside, 0..1
  // any number greater than one indicates that it is not within the bounds
  protected bool PointInCollider(Vector3 point) {
    Vector3 vInvTransformedPos = transform.InverseTransformPoint(point);
    Vector3 vSize = m_BoxCollider.size * 0.5f;
    vSize.x *= m_BoxCollider.transform.localScale.x;
    vSize.y *= m_BoxCollider.transform.localScale.y;
    vSize.z *= m_BoxCollider.transform.localScale.z;
    return Mathf.Abs(vInvTransformedPos.x) < vSize.x &&
      Mathf.Abs(vInvTransformedPos.y) < vSize.y &&
      Mathf.Abs(vInvTransformedPos.z) < vSize.z;
  }

  // Returns "score" that rates grab distance from 0 to 1, 0 being worst, 1 best, and -1 out of
  // range. A typical use of this is to return a negative value when the grab point is outside of
  // the object and a value ranging from 0 down to 1 as the grab point approaches the center of the
  // object.
  virtual public float GetActivationScore(
      Vector3 vControllerPos, InputManager.ControllerName name) {
    if (m_BoxCollider) {
      Vector3 vInvTransformedPos = transform.InverseTransformPoint(vControllerPos);
      Vector3 vSize = m_BoxCollider.size * 0.5f;
      vSize.x *= m_BoxCollider.transform.localScale.x;
      vSize.y *= m_BoxCollider.transform.localScale.y;
      vSize.z *= m_BoxCollider.transform.localScale.z;
      float xDiff = vSize.x - Mathf.Abs(vInvTransformedPos.x);
      float yDiff = vSize.y - Mathf.Abs(vInvTransformedPos.y);
      float zDiff = vSize.z - Mathf.Abs(vInvTransformedPos.z);
      if (xDiff > 0.0f && yDiff > 0.0f && zDiff > 0.0f) {
        return ((xDiff / vSize.x) * 0.333f) +
            ((yDiff / vSize.y) * 0.333f) +
            ((zDiff / vSize.z) * 0.333f);
      }
      return -1.0f;
    }

    float fDist = Vector3.Distance(vControllerPos, transform.position);
    if (fDist > m_GrabDistance) {
      return -1.0f;
    }
    return 1.0f - (fDist / m_GrabDistance);
  }

  protected virtual Vector3 HomeSnapOffset {
    get {
      if (m_BoxCollider == null) { return Vector3.zero; }
      Vector3 vSize = m_BoxCollider.size * 0.5f * App.Scene.Pose.scale;
      vSize.Scale(m_BoxCollider.transform.localScale);
      return vSize;
    }
  }

  virtual protected Transform GetOriginHomeXf() {
    return WidgetManager.m_Instance.GetHomeXf();
  }

  virtual protected void OnShow() { }

  virtual protected void OnHide() {
    if (m_DestroyOnHide) {
      WidgetManager.m_Instance.UnregisterGrabWidget(gameObject);
      Destroy(gameObject);
    } else {
      UnregisterHighlight();
      gameObject.SetActive(false);
    }
  }

  virtual protected void OnTossComplete() { }

  public void InitIntroAnim(TrTransform xfSpawn, TrTransform xfTarget, bool bFaceUser,
      Quaternion? endForward = null) {
    Vector3 vSpawnForwardNoY = xfSpawn.forward;
    vSpawnForwardNoY.y = 0.0f;
    Quaternion qSpawnOrient = Quaternion.LookRotation(vSpawnForwardNoY);
    Vector3 placementOffset = m_SpawnPlacementOffset;

    // Default is to spawn off to the right.  See if we should flip that.
    Ray headRay = ViewpointScript.Gaze;
    Vector3 gazeNoY = headRay.direction;
    gazeNoY.y = 0.0f;
    gazeNoY.Normalize();

    Vector3 headToSpawn = xfSpawn.translation - headRay.origin;
    headToSpawn.y = 0.0f;
    headToSpawn.Normalize();
    if (Vector3.Cross(headToSpawn, gazeNoY).y < 0.0f) {
      placementOffset.x *= -1.0f;
    }
    Vector3 vRotatedOffset = qSpawnOrient * placementOffset;
    xfTarget.translation += vRotatedOffset;

    // Face us toward user.
    if (bFaceUser) {
      Vector3 vToUser = headRay.origin - xfTarget.translation;
      xfTarget.rotation = Quaternion.LookRotation(vToUser.normalized);
    } else {
      Vector3 vToPanel = xfTarget.translation - headRay.origin;
      xfTarget.rotation = Quaternion.LookRotation(vToPanel.normalized);
    }

    if (endForward != null) {
      xfTarget.rotation *= Quaternion.RotateTowards(Quaternion.identity, endForward.Value, 180);
    }

    m_xfIntroAnimSpawn_LS = ParentTransform.inverse * xfSpawn;
    m_xfIntroAnimTarget_LS = ParentTransform.inverse * xfTarget;
  }

  virtual protected void UpdateIntroAnimState() {
    switch (m_IntroAnimState) {
    case IntroAnimState.In:
      m_IntroAnimValue += Time.deltaTime * m_IntroAnimSpeed;
      if (m_IntroAnimValue >= 1.0f) {
        m_IntroAnimValue = 1.0f;
        m_IntroAnimState = IntroAnimState.On;
      }
      UpdateIntroAnim();
      break;
    }
  }

  // Uses m_IntroAnimValue to lerp this widget's entrance into the scene.
  protected virtual void UpdateIntroAnim() {
    // The intro anim animates the center of mass.
    var desiredCm_LS = CenterOfMassPose_LS;
    desiredCm_LS.translation = Vector3.Lerp(
        m_xfIntroAnimSpawn_LS.translation,
        m_xfIntroAnimTarget_LS.translation,
        m_IntroAnimValue);
    desiredCm_LS.rotation = Quaternion.Slerp(
        m_xfIntroAnimSpawn_LS.rotation,
        m_xfIntroAnimTarget_LS.rotation,
        m_IntroAnimValue);

    // The added spin should also be about the center of mass

    // At t=0, softT = 1, softT' = -1
    // At t=1, softT = 0, softT' =  0 (exactly)
    float softT = (1 - Mathf.Cos(Mathf.PI/2 * (1 - m_IntroAnimValue)));
    desiredCm_LS.rotation = desiredCm_LS.rotation
        * Quaternion.AngleAxis(softT * m_IntroAnimSpinAmount,
                               Vector3.forward);

    // Find the corresponding widget pose, given a center-of-mass pose
    //   this * CenterOfMass_OS = CenterOfMass        // invariant (and by definition of _OS)
    //   this = CenterOfMass * CenterOfMass_OS.inverse
    var desiredWidget_LS = desiredCm_LS * CenterOfMassPose_OS.inverse;

    // The above manipulations don't touch scale, so this is safe.
    // This is also _necessary_ because widgets (inadvisedly) use Set/GetWidgetSize()
    // instead of scale. Thus, make sure the intro anim doesn't disturb it.
    desiredWidget_LS.scale = LocalTransform.scale;
    LocalTransform = desiredWidget_LS;
  }

  /// GameObject.Destroy imposes a frame of latency before OnDestroy() is sent, so this
  /// is a chance for subclasses to do any immediate cleanup.
  /// The implementation must be tolerant of being called multiple times.
  virtual public void OnPreDestroy() {}

  virtual protected void OnDestroy() {
    OnPreDestroy();
    WidgetManager.m_Instance.DestroyWidgetPin(m_Pin);
  }

  virtual protected void OnUpdate() { }

  virtual protected void OnUserBeginInteracting() {
    // Assign a pin if we can be pinned.
    if (m_AllowPinning) {
      WidgetManager.m_Instance.DestroyWidgetPin(m_Pin);
      m_Pin = WidgetManager.m_Instance.GetWidgetPin();
      InitPin();

      // Start disabled.
      m_Pin.gameObject.SetActive(false);

      // If we are pinned, jiggle the pin.
      if (m_Pinned) {
        m_Pin.gameObject.SetActive(true);
        m_Pin.WobblePin(m_InteractingController);
      }
    }

    if (m_AllowSnapping) {
      WidgetManager.m_Instance.SetHomeOwner(transform);
    }

    if (m_RecordMovements) {
      if (m_IsSpinningFreely) {
        SketchMemoryScript.m_Instance.PerformAndRecordCommand(
          new MoveWidgetCommand(this, LocalTransform, CustomDimension));
      }
    }
  }

  virtual protected void OnUserEndInteracting() {
    // If snap is enabled when we let go, make sure we don't drift.
    m_SnapDriftCancel = SnapEnabled || m_SnappingToHome;
    if (SnapEnabled) {
      FinishSnapping();
    }
    ForceSnapDisabled();

    // Grab widgets need a final MoveWidgetCommand to solidify their place in the command stack.
    // However, we don't want to record a command as final if it's still moving.  We'll leave
    // that work up to UpdatePositionAndVelocities.
    if (m_RecordMovements &&
        (!IsMoving() || m_DisableDrift || m_SnapDriftCancel) &&
        m_CurrentState != State.Tossed) {
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new MoveWidgetCommand(this, LocalTransform, CustomDimension, final: true));
    }

    // Give up usage of any pin we're using.
    if (m_Pin != null && m_Pin.IsAnimating()) {
      m_Pin.DestroyOnStateComplete();
    } else {
      WidgetManager.m_Instance.DestroyWidgetPin(m_Pin);
      m_Pin = null;
    }

    if (m_SnapGhost) {
      m_SnapGhost.gameObject.SetActive(false);
    }

    if (m_AllowSnapping) {
      WidgetManager.m_Instance.ClearHomeOwner();
    }

    // Because the user may be messing with the touchpad while holding a widget, eat input on the
    // pad when we're done here.  This prevents accidental tool resizing post-grab.
    SketchControlsScript.m_Instance.EatToolScaleInput();

    // Give up two handed grabbing.
    SetUserTwoHandGrabbing(false);
  }

  // Allows subclasses to do something when a two handed grab is initiated.
  virtual protected void OnUserBeginTwoHandGrab(
      Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInObject) { }

  // Allows subclasses to do something when a two handed grab is ended.
  virtual protected void OnUserEndTwoHandGrab() { }

  /// Returns the allowable range of the absolute value of the size. In other words, if the range is
  /// (1, 3), then allowable values for size are (-3, -1) and (1, 3).
  virtual public Vector2 GetWidgetSizeRange() { return new Vector2(1e-4f, 1e4f); }

  /// Size of the widget, which may be negative if SupportsNegativeSize is true.
  virtual public float GetSignedWidgetSize() { return 1.0f; }

  /// This sets the overall size of a widget. For non-uniformly scalable widgets, this will be the
  /// scale along the maximum aspect ratio. It is an error to try to set a negative scale if
  /// SupportsNegativeSize is not true.
  public void SetSignedWidgetSize(float fScale) {
    if (!SupportsNegativeSize && fScale < 0) {
      throw new NotSupportedException($"Negative size not allowed for {GetType()}.");
    }
    SetWidgetSizeInternal(fScale);
  }

  /// This sets the overall size of a widget. For non-uniformly scalable widgets, this will be the
  /// scale along the maximum aspect ratio. The scale may be negative if and only if
  /// SupportsNegativeSize is true.
  virtual protected void SetWidgetSizeInternal(float fScale) {}

  // Widgets that should use GPU intersection for activation should override this.
  virtual public bool HasGPUIntersectionObject() { return false; }

  // Widgets that should use GPU intersection for activation should override these and
  // update the GPU object with the layer used for detection.
  virtual public void SetGPUIntersectionObjectLayer(int layer) { }
  virtual public void RestoreGPUIntersectionObjectLayer() { }

  // Widgets that can be manipulated when we're in the selection tool deselection mode
  // should override this.
  virtual public bool CanGrabDuringDeselection() { return false; }

  /// Makes the object bigger by the specified amount along the specified axis.
  /// Other extents may be affected. For example:
  ///
  /// - Increasing capsule diameter must affect Extents.x and Extents.z, since
  ///   the capsule cross section is always a circle.
  /// - Increasing capsule diameter currently affects Extents.y, since the
  ///   radius of the end-spheres is also increased (and the height of the
  ///   cylindrical portion is not decreased to compensate)
  public virtual void RecordAndApplyScaleToAxis(float deltaScale, Axis axis) { }

  // Override for widgets that can't be snapped to home.
  public virtual bool CanSnapToHome() { return true; }

  // Override for widgets that need to configure custom layers after selection.
  public virtual void RestoreGameObjectLayer(int layer) {
    HierarchyUtils.RecursivelySetLayer(transform, layer);
  }

  virtual public bool DistanceToCollider(Ray ray, out float fDistance) {
    fDistance = 0.0f;
    return false;
  }

  public void AllowHideWithToss(bool bAllowHide) {
    m_AllowHideWithToss = bAllowHide;

    // Toggle the "toss to dismiss" message.
    // TODO: Add the dismiss message as a serialize field so that it's not hard-coded.
    if (m_TintableMeshes != null && m_TintableMeshes.Length > 1) {
      m_TintableMeshes[1].gameObject.SetActive(m_AllowHideWithToss);
    }
  }
}
}  // namespace TiltBrush
