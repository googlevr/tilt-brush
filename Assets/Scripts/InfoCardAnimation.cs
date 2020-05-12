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

public class InfoCardAnimation : MonoBehaviour {
  static public int s_NumCards;

  public enum State {
    Intro,
    Holding,
    Falling
  }

  [SerializeField] private Transform m_Mesh;
  [SerializeField] private TextMesh m_Text;
  [SerializeField] private float m_IntroStateDuration;
  [SerializeField] private float m_FallingStateDistance;
  [SerializeField] private float m_IntroPopAmount;
  [SerializeField] private Vector3 m_IntroPopVector;
  [SerializeField] private float m_HoldingTranslatePercent;
  [SerializeField] private float m_FallAcceleration;
  [SerializeField] private float m_FallRotationAcceleration;
  [SerializeField] private float m_FallTranslateAcceleration;
  [SerializeField] private float m_FallScaleDistance;
  [SerializeField] private AudioClip[] m_FallSounds;
  [SerializeField] private float m_FallVolume = 1;
  [SerializeField] private float m_ControllerInteractDistance;
  [SerializeField] private float m_FaceInteractDistance;
  [SerializeField] private float m_ControllerInteractDecay = 0.98f;
  [SerializeField] private float m_FaceInteractDecay = 0.98f;
  [SerializeField] private float m_InteractScalar;

  private State m_CurrentState;

  private float m_StateTimer;
  private float m_HoldingStateDuration = 10.0f;

  private Vector3 m_BaseScale;
  private Vector3 m_IntroPopVectorTransformed;

  private float m_FallVelocity;
  private float m_FallStartY;

  private Vector3 m_FallRotationAxis;
  private float m_FallRotationVelocity;

  private Vector3 m_FallTranslateDirection;
  private float m_FallTranslateVelocity;
  private Vector3 m_FallForces;

  private Vector3 m_FaceForces;

  void Awake() {
    ++s_NumCards;
  }

  void OnDestroy() {
    --s_NumCards;
  }

  public void Init(string sText, float fPopScalar = 1.0f) {
    m_Text.text = sText;

    // Measure length of button description by getting render bounds when mesh is axis-aligned.
    float fTextWidth = TextMeasureScript.m_Instance.GetTextWidth(m_Text.characterSize,
        m_Text.fontSize, m_Text.font, ("     " + sText));

    Vector3 vBGScale = m_Mesh.localScale;
    vBGScale.x = fTextWidth;
    m_Mesh.localScale = vBGScale;

    m_CurrentState = State.Intro;
    m_StateTimer = 0.0f;
    m_IntroPopAmount *= fPopScalar;
    m_IntroPopVectorTransformed = m_IntroPopVector.normalized * m_IntroPopAmount;
    m_BaseScale = transform.localScale;

    m_FallRotationAxis = Random.onUnitSphere;
    m_FallTranslateDirection = new Vector3(
        Random.Range(-1.0f, 1.0f), 0.0f, Random.Range(-1.0f, 1.0f));
    m_FallTranslateDirection.Normalize();

    UpdateScale(0.0f);
  }

  void FixedUpdate() {
    // Always update our state timer.
    m_StateTimer += Time.deltaTime;
    Vector3? interactionForce = null;

    m_FaceForces *= m_FaceInteractDecay;
    m_FallForces *= m_ControllerInteractDecay;
    // It's already falling.  Having the user push it down feels unnecessary.
    m_FallForces.y = Mathf.Max(m_FallForces.y, 0.0f);

    switch (m_CurrentState) {
    case State.Intro:
      UpdateIntroAnimation();
      UpdateFaceForces();
      if (m_StateTimer >= m_IntroStateDuration) {
        m_StateTimer = 0.0f;
        m_CurrentState = State.Holding;
      }
      break;
    case State.Holding:
      UpdateHoldingAnimation();
      UpdateFaceForces();
      interactionForce = GetControllerInteractionForce();
      if (m_StateTimer >= m_HoldingStateDuration || interactionForce != null) {
        m_FallForces = interactionForce.HasValue ? interactionForce.Value : Vector3.zero;
        m_StateTimer = 0.0f;
        m_FallStartY = transform.position.y;
        m_CurrentState = State.Falling;

        // Play fall sound when we start to fall.
        if (m_FallSounds.Length > 0) {
          if (AudioManager.Enabled) {
            AudioManager.m_Instance.TriggerOneShot(
              m_FallSounds[Random.Range(0, m_FallSounds.Length)],
              transform.position, m_FallVolume);
          }
        }
      }
      break;
    case State.Falling:
      UpdateFallingAnimation();
      UpdateFaceForces();
      interactionForce = GetControllerInteractionForce();
      if (interactionForce != null) {
        m_FallForces += interactionForce.Value;
      }
      if (Mathf.Abs(m_FallStartY - transform.position.y) > m_FallingStateDistance) {
        Destroy(gameObject);
      }
      break;
    }
  }

  /// Returns a vector from a nearby controller to the card.
  /// Null is returned if no controller is nearby.
  /// The length of the vector is modified to be (m_ControllerInteractDistance - vector.length)
  /// so that controllers of close proximity create larger forces.
  Vector3? GetControllerInteractionForce() {
    if (InputManager.Brush.IsTrackedObjectValid) {
      Vector3 brushToCard = transform.position -
          InputManager.Brush.Behavior.PointerAttachPoint.transform.position;
      float brushToCardDist = brushToCard.magnitude;
      if (brushToCardDist < m_ControllerInteractDistance) {
        brushToCard = brushToCard.normalized * (m_ControllerInteractDistance - brushToCardDist);
        return brushToCard;
      }
    }
    if (InputManager.Wand.IsTrackedObjectValid) {
      Vector3 wandToCard = transform.position -
          InputManager.Wand.Behavior.PointerAttachPoint.transform.position;
      float wandToCardDist = wandToCard.magnitude;
      if (wandToCardDist < m_ControllerInteractDistance) {
        wandToCard = wandToCard.normalized * (m_ControllerInteractDistance - wandToCardDist);
        return wandToCard;
      }
    }
    return null;
  }

  void UpdateFaceForces() {
    Vector3? interactionForce = GetFaceInteractionForce();
    if (interactionForce.HasValue) {
      m_FaceForces += interactionForce.Value;
      // Face forces only act on x/z.
      m_FaceForces.y = 0.0f;
    }
  }

  /// Returns a vector from the user's face to the card.
  /// Null is returned if there's no face nearby.
  /// The length of the vector is modified to be (m_FaceInteractDistance - vector.length).
  Vector3? GetFaceInteractionForce() {
    Vector3 faceToCard = transform.position -
        ViewpointScript.Head.position;
    float faceToCardDist = faceToCard.magnitude;
    if (faceToCardDist < m_FaceInteractDistance) {
      faceToCard = faceToCard.normalized * (m_FaceInteractDistance - faceToCardDist);
      return faceToCard;
    }
    return null;
  }

  void UpdateIntroAnimation() {
    // Scale up over this state.
    float fStateRatio = Mathf.Clamp01(m_StateTimer / m_IntroStateDuration);
    UpdateScale(fStateRatio);

    // Move along pop vector, slowing as we go.
    Vector3 vPos = transform.position;
    float fPopSpeed = Mathf.Lerp(1.0f, m_HoldingTranslatePercent, fStateRatio);
    vPos += m_IntroPopVectorTransformed * Time.fixedDeltaTime * fPopSpeed;
    vPos += m_FaceForces * m_InteractScalar * Time.deltaTime;
    transform.position = vPos;

    // Rotate to face user.
    Vector3 vForward = transform.position - ViewpointScript.Head.position;
    vForward.Normalize();
    transform.rotation = Quaternion.LookRotation(vForward, Vector3.up);
  }

  void UpdateHoldingAnimation() {
    // Move along pop vector, just as a slow translate.
    Vector3 vPos = transform.position;
    float sinScalar = Mathf.Sin(m_StateTimer);
    vPos += m_IntroPopVectorTransformed * Time.deltaTime * m_HoldingTranslatePercent * sinScalar;
    vPos += m_FaceForces * m_InteractScalar * Time.deltaTime;
    transform.position = vPos;
  }

  void UpdateFallingAnimation() {
    // Fall rotation.
    m_FallRotationVelocity += Time.deltaTime * m_FallRotationAcceleration;
    float fRotationStep = m_FallRotationVelocity * Time.deltaTime;
    Quaternion qCurrentRotation = transform.rotation;
    Quaternion qAdditionalRot = Quaternion.AngleAxis(fRotationStep, m_FallRotationAxis);
    transform.rotation = qCurrentRotation * qAdditionalRot;

    // Fall translation.
    Vector3 vPos = transform.position;
    m_FallTranslateVelocity += Time.deltaTime * m_FallTranslateAcceleration;
    vPos += m_FallTranslateDirection * m_FallTranslateVelocity *
        m_FallTranslateVelocity * Time.deltaTime;
    vPos += m_FallForces * m_InteractScalar * Time.deltaTime;
    vPos += m_FaceForces * m_InteractScalar * Time.deltaTime;

    // Constant fall speed.
    m_FallVelocity += Time.deltaTime * m_FallAcceleration;
    vPos.y -= m_FallVelocity * Time.deltaTime;
    transform.position = vPos;

    // Update scale with distance to destroy point.
    float fDistToEnd = Mathf.Abs(m_FallStartY - transform.position.y) - m_FallingStateDistance;
    float fScaleRatio = Mathf.Clamp01(Mathf.Abs(fDistToEnd) / m_FallScaleDistance);
    UpdateScale(fScaleRatio);
  }

  void UpdateScale(float fScale) {
    transform.localScale = m_BaseScale * fScale;
  }
}
}  // namespace TiltBrush
