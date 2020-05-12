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
using TMPro;
using UnityEngine;

namespace TiltBrush {

[System.Serializable]
public class GuideBeam {
  public Transform m_Beam;
  public SymmetryWidget.BeamDirection m_Direction;
  [NonSerialized] public Renderer m_BeamRenderer;
  [NonSerialized] public Vector3 m_Offset;
  [NonSerialized] public Vector3 m_BaseScale;
}

public class SymmetryWidget : GrabWidget {
  [SerializeField] private Renderer m_LeftRightMesh;
  [SerializeField] private TextMeshPro m_TitleText;
  [SerializeField] private GameObject m_HintText;
  [SerializeField] private GrabWidgetHome m_Home;

  public enum BeamDirection {
    Up,
    Down,
    Left,
    Right,
    Front,
    Back
  }
  [SerializeField] private GuideBeam[] m_GuideBeams;
  [SerializeField] private float m_GuideBeamLength;
  private float m_GuideBeamShowRatio;

  [SerializeField] private Color m_SnapColor;
  [SerializeField] private float m_SnapOrientationSpeed = 0.2f;

  [SerializeField] private float m_SnapAngleXZPlane = 45.0f;
  [SerializeField] private float m_SnapXZPlaneStickyAmount;
  private float m_SnapQuantizeAmount = 15.0f;
  private float m_SnapStickyAngle = 1.0f;

  [SerializeField] private float m_JumpToUserControllerOffsetDistance;
  [SerializeField] private float m_JumpToUserControllerYOffset;

  public Plane ReflectionPlane {
    get {
      return new Plane(transform.right, transform.position);
    }
  }

  public override Vector3 CustomDimension {
    get { return m_AngularVelocity_LS; }
    set {
      m_AngularVelocity_LS = value;
      m_IsSpinningFreely = value.magnitude > m_AngVelDampThreshold;
    }
  }

  override protected void Awake() {
    base.Awake();

    m_AngVelDampThreshold = 600f;

    //initialize beams
    for (int i = 0; i < m_GuideBeams.Length; ++i) {
      m_GuideBeams[i].m_Offset = m_GuideBeams[i].m_Beam.position - transform.position;
      m_GuideBeams[i].m_BaseScale = m_GuideBeams[i].m_Beam.localScale;
      m_GuideBeams[i].m_BeamRenderer = m_GuideBeams[i].m_Beam.GetComponent<Renderer>();
      m_GuideBeams[i].m_BeamRenderer.enabled = false;
    }

    m_GuideBeamShowRatio = 0.0f;

    m_Home.Init();
    m_Home.SetOwner(transform);
    m_Home.SetFixedPosition(App.Scene.AsScene[transform].translation);

    m_CustomShowHide = true;
  }

  public void SetMode(PointerManager.SymmetryMode rMode) {
    switch (rMode) {
    case PointerManager.SymmetryMode.SinglePlane:
      m_LeftRightMesh.enabled = false;
      for (int i = 0; i < m_GuideBeams.Length; ++i) {
        m_GuideBeams[i].m_BeamRenderer.enabled = ((m_GuideBeams[i].m_Direction != BeamDirection.Left) &&
                                                  (m_GuideBeams[i].m_Direction != BeamDirection.Right));
      }
      break;
    case PointerManager.SymmetryMode.FourAroundY:
      m_LeftRightMesh.enabled = true;
      for (int i = 0; i < m_GuideBeams.Length; ++i) {
        m_GuideBeams[i].m_BeamRenderer.enabled = ((m_GuideBeams[i].m_Direction != BeamDirection.Up) &&
                                                  (m_GuideBeams[i].m_Direction != BeamDirection.Down));
      }
      break;
    }
  }

  protected override TrTransform GetDesiredTransform(TrTransform xf_GS) {
    if (SnapEnabled) {
      return GetSnappedTransform(xf_GS);
    }
    return xf_GS;
  }

  override protected void OnUpdate() {
    bool moved = m_UserInteracting;

    // Drive the top of the mirror towards room-space up, to keep the text readable
    // It's a bit obnoxious to do this when the user's grabbing it. Maybe we should
    // also not do this when the canvas is being manipulated?
    if (!m_UserInteracting && !m_IsSpinningFreely && !m_SnapDriftCancel) {
      // Doing the rotation in object space makes it easier to prove that the
      // plane normal will never be affected.
      // NOTE: This assumes mirror-up is object-space-up
      // and mirror-normal is object-space-right (see ReflectionPlane.get)
      Vector3 up_OS = Vector3.up;
      Vector3 normal_OS = Vector3.right;

      Vector3 desiredUp_OS = transform.InverseTransformDirection(Vector3.up);
      float stability;
      float angle = MathUtils.GetAngleBetween(up_OS, desiredUp_OS, normal_OS, out stability);
      if (stability > .1f && Mathf.Abs(angle) > .05f) {
        float delta = angle * m_SnapOrientationSpeed;
        Quaternion qDelta_OS = Quaternion.AngleAxis(delta, normal_OS);
        if (m_NonScaleChild != null) {
          var t = m_NonScaleChild;
          t.localRotation = t.localRotation * qDelta_OS;
        } else {
          var t = transform;
          t.localRotation = t.localRotation * qDelta_OS;
        }
      }
    }

    // Rotation about ReflectionPlane.normal is purely visual and does
    // not affect the widget functionality. So when spinning, rotate about
    // normal until the widget is in a "natural" orientation. Natural is
    // defined as: one of the arms of the widget is aligned as closely as
    // possible to the axis of rotation.
    //
    // The spinning plane divides space into 2 (really 3) regions:
    // - Points that are always in front of or in back of the plane
    //   (two cones joined at the tip)
    // - Points that alternate between front and back of the plane
    //
    // Aligning one of the arms this way makes that arm/axis trace out
    // the boundary between these regions. Interestingly enough, the other
    // axis traces out a plane whose normal is the axis of rotation.
    if (IsSpinningFreely) {
      float MAX_ROTATE_SPEED = 100f;  // deg/sec
      float DECAY_TIME_SEC = .75f;      // Time to decay 63% towards goal
      Vector3 normal = ReflectionPlane.normal;
      Vector3 projected = AngularVelocity_GS;
      projected = projected - Vector3.Dot(normal, projected) * normal;
      float length = projected.magnitude;
      if (length > 1e-4f) {
        projected /= length;

        // arm to rotate towards projected; pick the one that's closest
        // Choices are .up and .forward (and their negatives)
        Vector3 arm =
          (Mathf.Abs(Vector3.Dot(transform.up, projected)) >
           Mathf.Abs(Vector3.Dot(transform.forward, projected)))
          ? transform.up : transform.forward;
        arm *= Mathf.Sign(Vector3.Dot(arm, projected));

        // Rotate arm towards projected. Since both arm and projected
        // are on the plane, the axis should be +normal or -normal.
        Vector3 cross = Vector3.Cross(arm, projected);
        Vector3 axis = normal * Mathf.Sign(Vector3.Dot(cross, normal));
        float delta = Mathf.Asin(cross.magnitude) * Mathf.Rad2Deg;
        float angle = (1f - Mathf.Exp(-Time.deltaTime / DECAY_TIME_SEC)) * delta;
        angle = Mathf.Min(angle, MAX_ROTATE_SPEED * Time.deltaTime);
        Quaternion q = Quaternion.AngleAxis(angle, axis);
        transform.rotation = q * transform.rotation;
        moved = true;
      }
    }

    if (moved && m_NonScaleChild != null) {
      m_NonScaleChild.OnPosRotChanged();
    }

    //if our transform changed, update the beams
    float fShowRatio = GetShowRatio();
    bool bInTransition = m_GuideBeamShowRatio != fShowRatio;
    if (bInTransition || transform.hasChanged) {
      for (int i = 0; i < m_GuideBeams.Length; ++i) {
        Vector3 vTransformedOffset = transform.rotation * m_GuideBeams[i].m_Offset;
        Vector3 vGuideBeamPos = transform.position + vTransformedOffset;
        Vector3 vGuideBeamDir = GetBeamDirection(m_GuideBeams[i].m_Direction);

        float fBeamLength = m_GuideBeamLength * App.METERS_TO_UNITS;
        fBeamLength *= fShowRatio;

        //position guide beam half way to hit point
        Vector3 vHitPoint = vGuideBeamPos + (vGuideBeamDir * fBeamLength);
        Vector3 vHalfWay = (vGuideBeamPos + vHitPoint) * 0.5f;
        m_GuideBeams[i].m_Beam.position = vHalfWay;

        //set scale to half the distance
        Vector3 vScale = m_GuideBeams[i].m_BaseScale;
        vScale.y = fBeamLength * 0.5f;

        m_GuideBeams[i].m_Beam.localScale = vScale;
      }

      transform.hasChanged = false;
      m_GuideBeamShowRatio = fShowRatio;
    }
  }

  override protected TrTransform GetSnappedTransform(TrTransform xf_GS) {
    TrTransform outXf_GS = xf_GS;

    // Move rot into canvas space
    Quaternion localRot = Quaternion.Inverse(m_NonScaleChild.parent.rotation) * xf_GS.rotation;
    // Determine "last frames" roll value for hysteresis measurement.
    Vector3 vPrevRight = m_NonScaleChild.localRotation * Vector3.right;
    Vector3 vPrevRightNoY = vPrevRight;
    vPrevRightNoY.y = 0.0f;
    float fPrevRoll = Vector3.Angle(vPrevRight, vPrevRightNoY.normalized);

    // We're looking at axis angles for determining snap.
    Vector3 vDesiredRight = localRot * Vector3.right;
    Vector3 vDesiredForward = localRot * Vector3.forward;
    Vector3 vDesiredUp = localRot * Vector3.up;

    Vector3 vRightNoY = vDesiredRight;
    vRightNoY.y = 0.0f;
    Vector3 vForwardNoY = vDesiredForward;
    vForwardNoY.y = 0.0f;

    // If we were snapping to XZ plane last frame, make it sticky to unsnap.
    float fRollThreshold = m_SnapAngleXZPlane;
    if (fPrevRoll > m_SnapAngleXZPlane) {
      fRollThreshold -= m_SnapXZPlaneStickyAmount;
    }

    float fRoll = Vector3.Angle(vDesiredRight, vRightNoY.normalized);
    if (fRoll > fRollThreshold) {
      // Snap to the XZ plane.  (normal up/down)
      Vector3 vUpNoY = vDesiredUp;
      vUpNoY.y = 0.0f;
      outXf_GS.rotation = m_NonScaleChild.parent.rotation *
          Quaternion.LookRotation(vForwardNoY.normalized, vUpNoY.normalized);
    } else {
      // Quantize Y to m_SnapQuantizeAmount degree increments.
      float fSnapPad = m_SnapQuantizeAmount + m_SnapStickyAngle;
      Vector3 vPrevEulers = m_NonScaleChild.localRotation.eulerAngles;
      float fPrevQuantizedY = Mathf.Floor((vPrevEulers.y + (m_SnapQuantizeAmount * 0.5f)) /
          m_SnapQuantizeAmount);

      // Normal should be on the XZ plane.
      Vector3 vUpNoXZ = vDesiredUp;
      vUpNoXZ.x = 0.0f; vUpNoXZ.z = 0.0f;

      // Only pop to the new angle if we've moved beyond our pad amount.
      Vector3 vEulers = Quaternion.LookRotation(vForwardNoY.normalized, vUpNoXZ.normalized).eulerAngles;
      float fQuantizedY = Mathf.Floor((vEulers.y + (m_SnapQuantizeAmount * 0.5f)) /
          m_SnapQuantizeAmount);
      float fFinalY = fPrevQuantizedY;
      if (fPrevQuantizedY != fQuantizedY) {
        if (Mathf.Abs(MathUtils.PeriodicDifference(vPrevEulers.y, vEulers.y, 360.0f)) > fSnapPad) {
          fFinalY = fQuantizedY;
        }
      }

      vEulers.y = fFinalY * m_SnapQuantizeAmount;
      outXf_GS.rotation = m_NonScaleChild.parent.rotation * Quaternion.Euler(vEulers);
    }
    return outXf_GS;
  }

  override public void Activate(bool bActive) {
    base.Activate(bActive);
    if (bActive && SnapEnabled) {
      for (int i = 0; i < m_GuideBeams.Length; ++i) {
        m_GuideBeams[i].m_BeamRenderer.material.color = m_SnapColor;
      }
    }
    m_HintText.SetActive(bActive);
    m_TitleText.color = bActive ? Color.white : Color.grey;
  }

  Vector3 GetBeamDirection(BeamDirection rDir) {
    switch (rDir) {
    case BeamDirection.Up: return transform.up;
    case BeamDirection.Down: return -transform.up;
    case BeamDirection.Left: return -transform.right;
    case BeamDirection.Right: return transform.right;
    case BeamDirection.Front: return transform.forward;
    case BeamDirection.Back: return -transform.forward;
    }
    return transform.up;
  }

  override protected void OnUserBeginInteracting() {
    base.OnUserBeginInteracting();
    m_Home.gameObject.SetActive(true);
    m_Home.Reset();
  }

  override protected void OnUserEndInteracting() {
    base.OnUserEndInteracting();
    m_Home.gameObject.SetActive(false);
    if (m_Home.ShouldSnapHome()) {
      ResetToHome();
    }
  }

  public Mirror ToMirror() {
    return new Mirror {
      Transform = TrTransform.FromLocalTransform(transform),
    };
  }

  public void FromMirror(Mirror data) {
    transform.localPosition = data.Transform.translation;
    transform.localRotation = data.Transform.rotation;
    if (m_NonScaleChild != null) {
      m_NonScaleChild.OnPosRotChanged();
    }
  }

  public void ResetToHome() {
    m_IsSpinningFreely = false;
    App.Scene.AsScene[transform] = m_Home.m_Transform_SS;
    transform.localScale = Vector3.one;
    if (m_NonScaleChild != null) {
      m_NonScaleChild.OnPosRotChanged();
    }
  }

  public void BringToUser() {
    // Get brush controller and place a little in front and a little higher.
    Vector3 controllerPos =
        InputManager.m_Instance.GetController(InputManager.ControllerName.Brush).position;
    Vector3 headPos = ViewpointScript.Head.position;
    Vector3 headToController = controllerPos - headPos;
    Vector3 offset = headToController.normalized * m_JumpToUserControllerOffsetDistance +
        Vector3.up * m_JumpToUserControllerYOffset;
    TrTransform xf_GS = TrTransform.TR(controllerPos + offset, transform.rotation);

    // The transform we built was global space, but we need it in widget local for the command.
    TrTransform newXf = TrTransform.FromTransform(m_NonScaleChild.parent).inverse * xf_GS;
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new MoveWidgetCommand(this, newXf, CustomDimension, final: true),
        discardIfNotMerged: false);
  }

  public override void Show(bool bShow, bool bPlayAudio = true) {
    base.Show(bShow, false);

    if (bShow) {
      // Play mirror sound
      AudioManager.m_Instance.PlayMirrorSound(transform.position);
    }
  }
}
}  // namespace TiltBrush
