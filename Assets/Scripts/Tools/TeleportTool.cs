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

namespace TiltBrush {

public class TeleportTool : BaseTool {
  public enum BoundsState {
    FadingInDelay,
    FadingIn,
    Showing,
    FadingOut,
    Off
  }

  public enum TeleportFadeState {
    FadeToScene,
    FadingToBlack,
    Default
  }

  public enum ToolState {
    Enter,
    Active,
    Off
  }

  [SerializeField] private Transform m_TeleportBounds;
  [SerializeField] private DynamicBounds m_TeleportBoundsCurrent;
  [SerializeField] private DynamicBounds m_TeleportBoundsDesired;
  [SerializeField] private int m_TeleportParabolaPoints = 3;
  [SerializeField] private float m_TeleportParabolaSpeed = 1.0f;
  [SerializeField] private float m_TeleportParabolaGravity = -1.0f;
  [SerializeField] private float m_TeleportParabolaMaxY = 0.7f;
  [Range(0.0f, 1.0f)]
  [SerializeField] private float m_TeleportParabolaDampen;
  [SerializeField] private GameObject m_BadTeleportIcon;
  [SerializeField] private Transform m_TeleportPlaceIcon;
  [SerializeField] private Vector2 m_TeleportPlaceIconShrinkRange;

  [SerializeField] private float m_BoundsEnterDelay;
  [SerializeField] private float m_BoundsEnterSpeed = 8.0f;
  [SerializeField] private float m_TeleportFadeSpeed = 8.0f;
  [SerializeField] private float m_EnterSpeed = 8.0f;

#if JOGGING_ENABLED
  [SerializeField] float m_MotionSpeedup = 1.0f;
  [SerializeField] float m_MotionMinThreshhold = .5f;
  [SerializeField] float m_MotionMaxThreshhold = 10.0f;
  [SerializeField] float m_JogSpeed = .05f;
  [SerializeField] float m_JogMinThreshold = .05f;
#endif

  private bool m_LockToController;
  private Transform m_BrushController;

  private Vector3 m_TeleportPlaceIconBaseScale;
  private bool m_HideValidTeleportParabola;
  private float m_TeleportParabolaFadeAmount;

  private float m_BoundsEnterDelayCountdown;
  private float m_BoundsShowAmount;
  private BoundsState m_BoundsState;

  private TeleportFadeState m_TeleportFadeState;
  private float m_TeleportFadeAmount;
  private bool m_TeleportForceBoundsPosition;
  private float m_BadTeleportIconEnterAmount;

  private LineRenderer m_TeleportParabola;

  private Ray m_LastParabolaRay;
  private float m_LastParabolaTime;
  private Vector3 m_LastParabolaVelocity;

  private ToolState m_CurrentState;
  private float m_EnterAmount;
  private Vector3 m_BaseScale;

#if JOGGING_ENABLED
  private enum JogState {
    NotJogging,
    JoggingUp,
    JoggingDown
  }
  private JogState m_JogStage;
  private Vector3 m_JogLastHeadPosition;
#endif
  private Vector3 m_TeleportTargetVector;

  float BoundsRadius {
    get {
      return SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
    }
  }

  bool CanTeleport() {
    // Figure out the distance between our bounds.
    Vector3 vDiff = m_TeleportBoundsCurrent.transform.position -
        m_TeleportBoundsDesired.transform.position;
    float fSmallAmount = (0.05f * App.METERS_TO_UNITS) * (0.05f * App.METERS_TO_UNITS);
    bool bWorldStable = SketchControlsScript.m_Instance.IsGrabWorldStateStable() &&
        !SketchControlsScript.m_Instance.IsUserTransformingWorld() &&
        !SketchControlsScript.m_Instance.IsPinCushionShowing();
    return m_TeleportFadeState == TeleportFadeState.Default && bWorldStable &&
        !m_BadTeleportIcon.gameObject.activeSelf && (vDiff.sqrMagnitude > fSmallAmount);
  }

  override public void Init() {
    base.Init();

    m_BaseScale = transform.localScale;
    m_BoundsState = BoundsState.Off;
    m_BoundsShowAmount = 0.0f;
    m_CurrentState = ToolState.Off;
    m_EnterAmount = 0.0f;
    m_TeleportFadeState = TeleportFadeState.Default;
    m_TeleportFadeAmount = 0.0f;

    m_TeleportParabola = GetComponent<LineRenderer>();
    m_TeleportParabola.positionCount = m_TeleportParabolaPoints;
    for (int i = 0; i < m_TeleportParabolaPoints; ++i) {
      m_TeleportParabola.SetPosition(i, Vector3.zero);
    }
    m_TeleportParabola.material.SetColor("_EmissionColor", Color.black * 15.0f);
    m_TeleportParabola.enabled = false;

    LineRenderer rBadIconLine = m_BadTeleportIcon.GetComponentInChildren<LineRenderer>();
    rBadIconLine.material.SetColor("_EmissionColor", Color.red * 15.0f);

    m_TeleportPlaceIconBaseScale = m_TeleportPlaceIcon.localScale;
  }

  override public void HideTool(bool bHide) {
    base.HideTool(bHide);

    SetBoundsVisibility(false);
    m_BadTeleportIconEnterAmount = 0.0f;
    m_BadTeleportIcon.gameObject.SetActive(false);
  }

  override public void EnableTool(bool bEnable) {
    base.EnableTool(bEnable);

    // Initialize to zeroed out.
    m_EnterAmount = 0.0f;
    UpdateToolScale();
#if JOGGING_ENABLED
    m_JogLastHeadPosition = Vector3.zero;
#endif

    // Bounds need to be active, place icon should default to off.
    m_TeleportBounds.gameObject.SetActive(bEnable);
    m_TeleportPlaceIcon.gameObject.SetActive(false);

    // Eat up some input when we're enabled.
    if (bEnable) {
      m_LockToController = m_SketchSurface.IsInFreePaintMode();
      if (m_LockToController) {
        m_BrushController = InputManager.m_Instance.GetController(InputManager.ControllerName.Brush);
      }

      // Build out our play area mesh.
      m_TeleportBoundsCurrent.BuildBounds();
      m_TeleportBoundsDesired.BuildBounds();

      EatInput();
      m_BoundsState = BoundsState.Off;
      m_BoundsShowAmount = 0.0f;
      UpdateBoundsScale(m_BoundsShowAmount);
      UpdateIconScale();

      m_CurrentState = ToolState.Enter;
    } else {
      m_CurrentState = ToolState.Off;
    }

    // Make sure our UI reticle isn't active.
    SketchControlsScript.m_Instance.ForceShowUIReticle(false);

    SetBoundsVisibility(false);
    m_BadTeleportIconEnterAmount = 0.0f;
    m_BadTeleportIcon.gameObject.SetActive(false);
  }

  /// Returns true on success
  /// Also updates m_TeleportForceBoundsPosition, m_LastParabolaVelocity,
  ///  m_HideValidTeleportParabola, m_TeleportPlaceIcon, m_TeleportBoundsDesired
  /// Calls SetTeleportParabola()
  /// Calls UpdateIconScale()
  bool UpdateTool_PlaceParabola() {
    // Given pointing Y and our scalar, determine where our parabola intersects with y == 0.
    Vector3 vel = m_LastParabolaRay.direction * m_TeleportParabolaSpeed;
    float yPos = m_LastParabolaRay.origin.y;
    float fRadicand = vel.y * vel.y - 2.0f * m_TeleportParabolaGravity * yPos;
    if (fRadicand < 0f) {
      return false;
    }

    Vector3 vFeet = ViewpointScript.Head.position;
    vFeet.y = 0.0f;

    Vector3 vNewFeet = m_LastParabolaRay.origin;
    vNewFeet.y = m_TeleportBounds.position.y;

    m_LastParabolaTime = (vel.y + Mathf.Sqrt(fRadicand)) / -m_TeleportParabolaGravity;
    m_TeleportTargetVector = new Vector3(vel.x * m_LastParabolaTime, 0, vel.z * m_LastParabolaTime);
    vNewFeet += m_TeleportTargetVector;

    // Ensure vNewFeet remains valid
    {
      // We don't have any functions for validating foot poses. In fact, the
      // room doesn't "move" since it's the root of our hierarchy. So:

      // 1. Turn foot move into room move
      TrTransform xfRoomMove = TrTransform.T(vNewFeet - vFeet);
      // 2. Turn room move into new scene pose and validate
      //    Note: assumes old room transform is identity (which it is, because
      //    the room is the root of our transform hierarchy).
      TrTransform newScene = xfRoomMove.inverse * App.Scene.Pose;
      newScene = SketchControlsScript.MakeValidSceneMove(App.Scene.Pose, newScene, BoundsRadius);
      // 3. Reverse of #2
      xfRoomMove = App.Scene.Pose * newScene.inverse;
      // 4. Reverse of #1
      vNewFeet = vFeet + xfRoomMove.translation;
    }

    // Dampen motion of vNewFeet
    // Invariant: new room center == (vNewFeet - vFeet)
    if (!m_TeleportForceBoundsPosition) {
      vNewFeet = Vector3.Lerp(
          m_TeleportBoundsDesired.transform.position + vFeet,
          vNewFeet,
          m_TeleportParabolaDampen);
    } else {
      m_TeleportForceBoundsPosition = false;
    }

    // Curve parabola to hit final position.
    m_LastParabolaVelocity = (vNewFeet - m_LastParabolaRay.origin) / m_LastParabolaTime;
    m_LastParabolaVelocity.y = (
        (vNewFeet.y - m_LastParabolaRay.origin.y) -
        (m_TeleportParabolaGravity * m_LastParabolaTime * m_LastParabolaTime * 0.5f))
      / m_LastParabolaTime;

    Vector3 vVelNoY = vel;
    vVelNoY.y = 0.0f;
    Vector3 vLastVelNoY = m_LastParabolaVelocity;
    vLastVelNoY.y = 0.0f;
    bool bReasonableAngle = Vector3.Angle(vVelNoY, vLastVelNoY) < 60.0f;
    m_HideValidTeleportParabola = !bReasonableAngle ||
        (m_LastParabolaVelocity.normalized.y > m_TeleportParabolaMaxY);

    SetTeleportParabola();

    // Place icon at the user's head position inside the new bounds, facing the user.
    m_TeleportPlaceIcon.position = vNewFeet;
    Vector3 vGazeDirNoY = ViewpointScript.Head.forward;
    vGazeDirNoY.y = 0.0f;
    m_TeleportPlaceIcon.forward = vGazeDirNoY.normalized;

    // Shrink the icon if it's too close to us.
    UpdateIconScale();

    // Finally, set the desired bounds.
    m_TeleportBoundsDesired.transform.position = vNewFeet - vFeet;
    return true;
  }

  override public void UpdateTool() {
    base.UpdateTool();

    Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
    if (m_LockToController) {
      m_LastParabolaRay.origin = rAttachPoint.position;
      m_LastParabolaRay.direction = rAttachPoint.forward;
      m_BadTeleportIcon.transform.position = rAttachPoint.position;
      m_BadTeleportIcon.transform.rotation = rAttachPoint.rotation;
    } else {
      m_LastParabolaRay = ViewpointScript.Gaze;
    }

    bool bAimValid = m_LastParabolaRay.direction.y < m_TeleportParabolaMaxY;
    if (m_TeleportFadeState != TeleportFadeState.FadingToBlack) {
      bool visible = bAimValid;
      if (bAimValid) {
        bool canPlaceParabola = UpdateTool_PlaceParabola();
        visible = canPlaceParabola;
      } else {
        visible = false;
      }
      SetBoundsVisibility(visible);
    }

    // Update hide parabola counter.
    if (!m_HideValidTeleportParabola) {
      m_TeleportParabolaFadeAmount += m_BoundsEnterSpeed * Time.deltaTime;
      m_TeleportParabolaFadeAmount = Mathf.Min(m_TeleportParabolaFadeAmount, 1.0f);
    } else {
      m_TeleportParabolaFadeAmount -= m_BoundsEnterSpeed * Time.deltaTime;
      m_TeleportParabolaFadeAmount = Mathf.Max(m_TeleportParabolaFadeAmount, 0.0f);
    }

    // Update bad teleport icon scale and visibility.
    bool bShowBadTeleport = !bAimValid;
    if (bShowBadTeleport) {
      if (m_BadTeleportIconEnterAmount < 1.0f) {
        m_BadTeleportIconEnterAmount += m_BoundsEnterSpeed * Time.deltaTime;
        m_BadTeleportIconEnterAmount = Mathf.Min(m_BadTeleportIconEnterAmount, 1.0f);
        m_BadTeleportIcon.transform.localScale = new Vector3(m_BadTeleportIconEnterAmount,
            m_BadTeleportIconEnterAmount, m_BadTeleportIconEnterAmount);
      }
    } else {
      if (m_BadTeleportIconEnterAmount > 0.0f) {
        m_BadTeleportIconEnterAmount -= m_BoundsEnterSpeed * Time.deltaTime;
        m_BadTeleportIconEnterAmount = Mathf.Max(m_BadTeleportIconEnterAmount, 0.0f);
        m_BadTeleportIcon.transform.localScale = new Vector3(m_BadTeleportIconEnterAmount,
                    m_BadTeleportIconEnterAmount, m_BadTeleportIconEnterAmount);
      }
    }
    m_BadTeleportIcon.gameObject.SetActive(m_BadTeleportIconEnterAmount > 0.0f);

    // Update teleport fade.
    if (m_TeleportFadeState != TeleportFadeState.Default) {
      if (m_TeleportFadeState == TeleportFadeState.FadingToBlack) {
        m_TeleportFadeAmount += m_TeleportFadeSpeed * Time.deltaTime;
        if (m_TeleportFadeAmount >= 1.0f) {
          // Teleport the user
          Vector3 vMovement = m_TeleportBoundsCurrent.transform.position -
              m_TeleportBoundsDesired.transform.position;

          TrTransform newScene = App.Scene.Pose;
          newScene.translation += vMovement;
          // newScene might have gotten just a little bit invalid.
          // Enforce the invariant that teleport always sends you
          // to a scene which is MakeValidPose(scene)
          newScene = SketchControlsScript.MakeValidScenePose(newScene, BoundsRadius);
          App.Scene.Pose = newScene;

          m_TeleportFadeAmount = 1.0f;
          m_TeleportFadeState = TeleportFadeState.FadeToScene;
          ViewpointScript.m_Instance.FadeToScene(m_TeleportFadeSpeed);
          m_TeleportForceBoundsPosition = true;
        }
      } else if (m_TeleportFadeState == TeleportFadeState.FadeToScene) {
        m_TeleportFadeAmount -= m_TeleportFadeSpeed * Time.deltaTime;
        if (m_TeleportFadeAmount <= 0.0f) {
          // Done fading.
          m_TeleportFadeAmount = 0.0f;
          m_TeleportFadeState = TeleportFadeState.Default;
        }
      }
    }

    // Check for teleporting.
    if (!m_EatInput && !m_ToolHidden && CanTeleport() &&
        InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Teleport)) {
      m_TeleportFadeState = TeleportFadeState.FadingToBlack;
      ViewpointScript.m_Instance.FadeToColor(Color.black, m_TeleportFadeSpeed);
      AudioManager.m_Instance.PlayTeleportSound(
          ViewpointScript.Head.position + ViewpointScript.Head.forward);
    }

    PointerManager.m_Instance.SetMainPointerPosition(rAttachPoint.position);
  }

  void Update() {
    // Update bounds transitions.
    switch (m_BoundsState) {
    case BoundsState.FadingInDelay:
      m_BoundsEnterDelayCountdown -= Time.deltaTime;
      if (m_BoundsEnterDelayCountdown <= 0.0f) {
        m_BoundsState = BoundsState.FadingIn;
        m_TeleportPlaceIcon.gameObject.SetActive(true);
        m_TeleportParabola.enabled = true;
      }
      m_BoundsShowAmount = 0.0f;
      UpdateBoundsScale(m_BoundsShowAmount);
      UpdateIconScale();
      SetTeleportParabola();
      break;
    case BoundsState.FadingIn:
      m_BoundsShowAmount += m_BoundsEnterSpeed * Time.deltaTime;
      if (m_BoundsShowAmount >= 1.0f) {
        m_BoundsShowAmount = 1.0f;
        m_BoundsState = BoundsState.Showing;
      }
      UpdateBoundsScale(m_BoundsShowAmount);
      UpdateIconScale();
      SetTeleportParabola();
      break;
    case BoundsState.FadingOut:
      m_BoundsShowAmount -= m_BoundsEnterSpeed * Time.deltaTime;
      if (m_BoundsShowAmount <= 0.0f) {
        m_BoundsShowAmount = 0.0f;
        m_BoundsState = BoundsState.Off;
        m_TeleportPlaceIcon.gameObject.SetActive(false);
        m_TeleportParabola.enabled = false;
        m_BoundsEnterDelayCountdown = m_BoundsEnterDelay;
      }
      UpdateBoundsScale(m_BoundsShowAmount);
      UpdateIconScale();
      SetTeleportParabola();
      break;
    }

    // Update tool transitions.
    switch (m_CurrentState) {
    case ToolState.Enter:
      m_EnterAmount += (m_EnterSpeed * Time.deltaTime);
      if (m_EnterAmount >= 1.0f) {
        m_EnterAmount = 1.0f;
        m_CurrentState = ToolState.Active;
      }
      UpdateToolScale();
      break;
    case ToolState.Active:
      break;
    default:
    case ToolState.Off: break;
    }

    if (!m_LockToController) {
      // If we're not locking to a controller, update our transforms now, instead of in LateUpdate.
      UpdateTransformsFromControllers();
    }

#if JOGGING_ENABLED && (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      // Add jogging motion.
      Vector3 currentHeadPosition = ViewpointScript.Head.position;
      if (m_JogLastHeadPosition != Vector3.zero) {
        Vector3 headMotion = currentHeadPosition - m_JogLastHeadPosition;

        TrTransform newScene = Coords.ScenePose;
        float motionFactor = Vector3.Dot(headMotion, m_TeleportTargetVector);
        motionFactor = (motionFactor - m_MotionMinThreshhold) / (m_MotionMaxThreshhold - m_MotionMinThreshhold);
        motionFactor = Mathf.SmoothStep(0, 1, motionFactor) * (m_MotionMaxThreshhold - m_MotionMinThreshhold);
        newScene.translation -= m_MotionSpeedup * motionFactor * m_TeleportTargetVector.normalized;

        if (headMotion.y > m_JogMinThreshold) {
          m_JogStage = JogState.JoggingUp;
        }
        if (m_JogStage != JogState.NotJogging) {
          newScene.translation -= m_JogSpeed * m_TeleportTargetVector;
          if (m_JogStage == JogState.JoggingUp && headMotion.y < 0) {
            m_JogStage = JogState.JoggingDown;
          } else if (m_JogStage == JogState.JoggingDown && headMotion.y >= 0) {
            m_JogStage = JogState.NotJogging;
          }
        }

        // newScene might have gotten just a little bit invalid.
        // Enforce the invariant that teleport always sends you
        // to a scene which is MakeValidPose(scene)
        newScene = SketchControlsScript.MakeValidScenePose(newScene, BoundsRadius);
        Coords.ScenePose = newScene;
      }
      m_JogLastHeadPosition = currentHeadPosition;
    }
#endif

  }

  override public void LateUpdateTool() {
    base.LateUpdateTool();
    UpdateTransformsFromControllers();
  }

  private void UpdateTransformsFromControllers() {
    // Lock tool to camera controller.
    if (m_LockToController) {
      transform.position = m_BrushController.position;
      transform.rotation = m_BrushController.rotation;
    } else {
      transform.position = SketchSurfacePanel.m_Instance.transform.position;
      transform.rotation = SketchSurfacePanel.m_Instance.transform.rotation;
    }
  }

  Vector3 TransformOffset(Transform rBase, Vector3 vOffset) {
    Vector3 vTransformedOffset = rBase.rotation * vOffset;
    return rBase.position + vTransformedOffset;
  }

  void UpdateToolScale() {
    Vector3 vScale = m_BaseScale;
    vScale.x *= m_EnterAmount;
    transform.localScale = vScale;
  }

  void UpdateBoundsScale(float fScale) {
    Vector3 vScale = new Vector3(fScale, fScale, fScale);
    m_TeleportBoundsCurrent.transform.localScale = vScale;
    m_TeleportBoundsDesired.transform.localScale = vScale;
  }

  void UpdateIconScale() {
    Vector3 vCamPos = ViewpointScript.Head.position;

    // Figure out how far away from the user's head we are.
    Vector3 vIconFacing = m_TeleportPlaceIcon.position - vCamPos;
    vIconFacing.y = 0.0f;
    float fIconDistance = vIconFacing.magnitude;
    float fScale = Mathf.Max(fIconDistance - m_TeleportPlaceIconShrinkRange.x, 0.0f);
    fScale /= (m_TeleportPlaceIconShrinkRange.y - m_TeleportPlaceIconShrinkRange.x);

    // Shrink according to distance and transition amounts.
    fScale = Mathf.Min(fScale, 1.0f) * m_BoundsShowAmount;
    m_TeleportPlaceIcon.localScale = m_TeleportPlaceIconBaseScale * fScale;
  }

  void SetBoundsVisibility(bool bShow) {
    if (!bShow) {
      if (m_BoundsState == BoundsState.FadingIn || m_BoundsState == BoundsState.Showing) {
        m_BoundsState = BoundsState.FadingOut;
      } else if (m_BoundsState == BoundsState.FadingInDelay) {
        m_BoundsState = BoundsState.Off;
      }
    } else {
      if (m_BoundsState == BoundsState.FadingOut) {
        m_BoundsState = BoundsState.FadingIn;
      } else if (m_BoundsState == BoundsState.Off) {
        m_BoundsState = BoundsState.FadingInDelay;
        m_BoundsEnterDelayCountdown = m_BoundsEnterDelay;
      }
    }
  }

  void SetTeleportParabola() {
    float fPartialTime = m_LastParabolaTime * Mathf.Min(m_BoundsShowAmount * m_TeleportParabolaFadeAmount);
    float fTimeInterval = fPartialTime / (float)(m_TeleportParabolaPoints - 1.0f);
    for (int i = 0; i < m_TeleportParabolaPoints; ++i) {
      float time = (m_LastParabolaTime - fPartialTime) + (fTimeInterval * i);
      Vector3 vSetPoint = GetPositionOnParabola(m_LastParabolaRay.origin, m_LastParabolaVelocity, time);
      m_TeleportParabola.SetPosition(i, vSetPoint);
    }
  }

  Vector3 GetPositionOnParabola(Vector3 vInitialPos, Vector3 vVel, float time) {
    Vector3 vPos = Vector3.zero;
    vPos.y = vInitialPos.y + (vVel.y * time) + (m_TeleportParabolaGravity * time * time * 0.5f);
    vPos.x = vInitialPos.x + (vVel.x * time);
    vPos.z = vInitialPos.z + (vVel.z * time);
    return vPos;
  }

  override public bool LockPointerToSketchSurface() {
    return false;
  }

  override public bool InputBlocked() {
    return m_TeleportFadeState != TeleportFadeState.Default;
  }

  override public bool AllowsWidgetManipulation() {
    return m_TeleportFadeState == TeleportFadeState.Default;
  }

  override public bool AvailableDuringLoading() {
    return true;
  }

  override public bool AllowWorldTransformation() {
    return m_TeleportFadeState == TeleportFadeState.Default;
  }

  public override void EnableRenderer(bool enable) {
    base.EnableRenderer(enable);
    m_TeleportBounds.gameObject.SetActive(enable);
  }

  override public bool BlockPinCushion() {
    return m_TeleportFadeState != TeleportFadeState.Default;
  }

  override public bool CanShowPromosWhileInUse() {
    return false;
  }
}
}  // namespace TiltBrush
