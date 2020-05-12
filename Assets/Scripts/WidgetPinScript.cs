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

public class WidgetPinScript : MonoBehaviour {
  [SerializeField] private Transform m_MeshXf;
  [SerializeField] private Vector3 m_SpawnOffset;
  [SerializeField] private Vector2 m_ScaleRange_RS;
  [SerializeField] private float m_BaseEnterSpeed = 150.0f;
  [SerializeField] private float m_PenetrationScalar = 1.0f;
  [SerializeField] private float m_WobbleDuration = 1.0f;
  [SerializeField] private float m_WobbleAngle;
  [SerializeField] private float m_WobbleSpeed;
  [SerializeField] private float m_DisplayDuration = 0.25f;
  [SerializeField] private float m_UnpinPopForce;
  [SerializeField] private float m_UnpinRotationSpeed;
  [SerializeField] private float m_UnpinFallDuration;
  [SerializeField] private float m_UnpinFallDrag;
  [SerializeField] private float m_UnpinFallGravity;

  public enum PinState {
    None,
    Entering,
    Wobbling,
    Pinned,
    Removing
  }
  private PinState m_PinState;
  private Transform m_Parent;
  private GrabWidget m_ParentWidgetScript;
  private Vector3 m_EntranceTarget;
  private Quaternion m_BaseRotation;
  private Vector3 m_BaseMeshScale;
  private Quaternion m_BaseMeshRotation;
  private Vector3? m_TransformedSpawnOffset;
  private float m_WobbleCountdown;
  private float m_DisplayCountdown;
  private Vector3 m_WobbleCurrent;
  private Vector3 m_WobbleTarget;
  private Vector3 m_UnpinVelocity;
  private Vector3 m_UnpinRotationAxis;
  private float m_UnpinFallCountdown;
  private float m_SpeedScalar;
  private float m_WorkingPenetrationScalar;
  private bool m_DestroyOnStateComplete;
  private bool m_SuppressAudio;

  public bool SuppressAudio {
    get { return m_SuppressAudio; }
    set { m_SuppressAudio = value; }
  }

  public void SetPenetrationScalar(float scalar) {
    m_WorkingPenetrationScalar = scalar;
  }

  public void DestroyOnStateComplete() {
    m_DestroyOnStateComplete = true;
  }

  void Awake() {
    m_BaseRotation = transform.rotation;
    m_BaseMeshScale = m_MeshXf.localScale;
    m_BaseMeshRotation = m_MeshXf.localRotation;
    Coords.CanvasPoseChanged += OnCanvasPoseChanged;
  }

  void OnDestroy() {
    Coords.CanvasPoseChanged -= OnCanvasPoseChanged;
  }

  void OnCanvasPoseChanged(TrTransform prev, TrTransform current) {
    WidgetManager.m_Instance.DestroyWidgetPin(this);
  }

  public void Init(Transform parent, GrabWidget parentWidget) {
    m_WorkingPenetrationScalar = m_PenetrationScalar;
    m_DestroyOnStateComplete = false;
    m_WobbleCountdown = 0.0f;
    m_Parent = parent;
    m_ParentWidgetScript = parentWidget;
    InitTransformedSpawnOffset();
  }

  public void ShowWidgetAsPinned() {
    RefreshPositionAndScale();
    SwitchPinState(PinState.Pinned);
  }

  public void WobblePin(InputManager.ControllerName heldController) {
    RefreshPositionAndScale();
    SwitchPinState(PinState.Wobbling);
    // If the user tries to grab a pinned widget, give a little buzz.
    if (heldController == InputManager.ControllerName.Brush ||
        heldController == InputManager.ControllerName.Wand) {
      InputManager.m_Instance.TriggerHapticsPulse(heldController, 3, 0.10f, 0.07f);
    }
  }

  public void PinWidget() {
    SwitchPinState(PinState.Entering);
  }

  public void UnpinWidget() {
    RefreshPositionAndScale();
    SwitchPinState(PinState.Removing);
  }

  public bool IsAnimating() {
    return (m_PinState == PinState.Entering) || (m_PinState == PinState.Removing) ||
        (m_PinState == PinState.Wobbling && m_WobbleCountdown > 0.0f);
  }

  void Update() {
    // Render the grab mask highlight on the pin if it's not a temp pin.
    if (!m_DestroyOnStateComplete) {
      App.Instance.SelectionEffect.RegisterMesh(m_MeshXf.GetComponent<MeshFilter>());
    }

    switch (m_PinState) {
    case PinState.Entering:
      Vector3 vToTarget = m_EntranceTarget - transform.position;
      float fStep = m_SpeedScalar * m_BaseEnterSpeed * Time.deltaTime;
      if (vToTarget.sqrMagnitude < fStep * fStep) {
        transform.position = m_EntranceTarget;
        SwitchPinState(PinState.Wobbling);
      } else {
        transform.position += vToTarget.normalized * fStep;
      }
      break;
    case PinState.Pinned:
      // Delayed hide.
      if (m_DestroyOnStateComplete && m_DisplayCountdown <= 0.0f) {
        WidgetManager.m_Instance.DestroyWidgetPin(this);
      }
      break;
    case PinState.Wobbling:
      // Delayed hide.
      if (m_DestroyOnStateComplete && m_WobbleCountdown <= 0.0f) {
        SwitchPinState(PinState.Pinned);
      }
      break;
    case PinState.Removing:
      m_UnpinFallCountdown -= Time.deltaTime;
      if (m_UnpinFallCountdown <= 0.0f) {
        if (m_DestroyOnStateComplete) {
          WidgetManager.m_Instance.DestroyWidgetPin(this);
        } else {
          gameObject.SetActive(false);
          m_PinState = PinState.None;
        }
      } else {
        // Update position.
        m_UnpinVelocity.y += m_UnpinFallGravity * Time.deltaTime;
        m_UnpinVelocity.x *= m_UnpinFallDrag;
        m_UnpinVelocity.z *= m_UnpinFallDrag;
        transform.position += m_UnpinVelocity * m_SpeedScalar * Time.deltaTime;

        // Spin around random axis.
        float fRotationStep = m_UnpinRotationSpeed * Time.deltaTime;
        Quaternion qCurrentMeshRot = m_MeshXf.rotation;
        Quaternion qAdditionalRot = Quaternion.AngleAxis(fRotationStep, m_UnpinRotationAxis);
        m_MeshXf.rotation = qCurrentMeshRot * qAdditionalRot;

        // Scale down.
        m_MeshXf.localScale = m_BaseMeshScale * (m_UnpinFallCountdown / m_UnpinFallDuration);
      }
      break;
    }

    // Tick down display counter.  We'll use it in the Pinned state if necessary.
    m_DisplayCountdown -= Time.deltaTime;

    if (m_WobbleCountdown > 0.0f) {
      // Move toward our wobble target.
      Vector3 vWobbleDiff = m_WobbleTarget - m_WobbleCurrent;
      float fWobbleStep = m_WobbleSpeed * Time.deltaTime;
      if (vWobbleDiff.magnitude < fWobbleStep) {
        m_WobbleCurrent = m_WobbleTarget;
        m_WobbleTarget *= -1.0f;
      } else {
        m_WobbleCurrent += vWobbleDiff.normalized * fWobbleStep;
      }

      // Tick down our timer and zero out our wobble if we're out of time.
      m_WobbleCountdown -= Time.deltaTime;
      if (m_WobbleCountdown <= 0.0f) {
        m_WobbleCurrent = Vector3.zero;
      }

      // Wobble!
      float fWobbleScalar = m_WobbleCountdown / m_WobbleDuration;
      transform.rotation = m_BaseRotation * Quaternion.Euler(m_WobbleCurrent * fWobbleScalar);
    }
  }

  void SwitchPinState(PinState desired) {
    // Enter desired.
    switch (desired) {
    case PinState.Entering:
      PrimeForEntering();
      break;
    case PinState.Pinned:
      m_DisplayCountdown = m_DisplayDuration;
      break;
    case PinState.Wobbling:
      Wobble();
      break;
    case PinState.Removing:
      // Pop pin out and have it fall away.
      Unpin();
      break;
    }
    m_PinState = desired;
  }

  void InitTransformedSpawnOffset() {
    Vector3 vHeadToParentNoY = m_Parent.position - ViewpointScript.Head.position;
    vHeadToParentNoY.y = 0.0f;
    Quaternion qUserFacingRotation = Quaternion.LookRotation(vHeadToParentNoY.normalized);
    m_TransformedSpawnOffset = qUserFacingRotation * m_SpawnOffset;
  }

  void PrimeForEntering() {
    InitTransformedSpawnOffset();
    RefreshPositionAndScale();

    // Place pin offset, relative to user facing, heading toward the widget.
    float fMaxAxisScale = m_ParentWidgetScript.MaxAxisScale;
    float fMaxAxisScale_RS = App.Scene.Pose.scale * fMaxAxisScale;
    transform.position = m_EntranceTarget + m_TransformedSpawnOffset.Value * fMaxAxisScale_RS;
    if (!m_SuppressAudio) {
      AudioManager.m_Instance.PlayPinSound(m_EntranceTarget, AudioManager.PinSoundType.Enter);
    }
  }

  void RefreshPositionAndScale() {
    Debug.Assert(m_TransformedSpawnOffset != null,
        "m_TransformedSpawnOffset must be initialized before being used.");
    // The pin target should be from the parent center, backward along the entrance vector,
    // by a distance relative to the room scale of the object.
    float fMaxAxisScale = m_ParentWidgetScript.MaxAxisScale;
    float fWidgetScalar = m_ParentWidgetScript ? m_ParentWidgetScript.PinScalar : 1.0f;
    float fMaxAxisScale_RS = App.Scene.Pose.scale * fMaxAxisScale * fWidgetScalar;
    Vector3 normalizedTransformedSpawnOffset = m_TransformedSpawnOffset.Value.normalized;
    m_EntranceTarget = m_Parent.position + normalizedTransformedSpawnOffset *
        fMaxAxisScale_RS * m_WorkingPenetrationScalar;

    transform.position = m_EntranceTarget;
    transform.forward = -normalizedTransformedSpawnOffset;
    m_BaseRotation = transform.rotation;

    m_SpeedScalar = Mathf.Clamp(fMaxAxisScale_RS, m_ScaleRange_RS.x, m_ScaleRange_RS.y);
    transform.localScale = Vector3.one * m_SpeedScalar;
    m_MeshXf.localScale = m_BaseMeshScale;
    m_MeshXf.localRotation = m_BaseMeshRotation;
  }

  void Wobble() {
    m_WobbleCountdown = m_WobbleDuration;
    m_WobbleCurrent = Vector3.zero;
    m_WobbleTarget = Random.insideUnitCircle.normalized * m_WobbleAngle;
    m_WobbleTarget.z = 0.0f;
    if (!m_SuppressAudio) {
      AudioManager.m_Instance.PlayPinSound(transform.position, AudioManager.PinSoundType.Wobble);
    }
  }

  void Unpin() {
    m_UnpinVelocity = -transform.forward * m_UnpinPopForce;
    m_UnpinRotationAxis = Random.onUnitSphere;
    m_UnpinFallCountdown = m_UnpinFallDuration;
    if (!m_SuppressAudio) {
      AudioManager.m_Instance.PlayPinSound(transform.position, AudioManager.PinSoundType.Unpin);
    }
  }
}

} //namespace TiltBrush

