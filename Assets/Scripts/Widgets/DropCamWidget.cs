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
public class FrustumBeam {
  public Transform m_Beam;
  public Renderer m_BeamMesh;
  [NonSerialized] public Vector3 m_BaseScale;
}

public class DropCamWidget : GrabWidget {
  public enum Mode {
    SlowFollow,
    Stationary,
    Wobble,
    Circular
  }

  public const Mode kDefaultMode = Mode.Stationary;

  [SerializeField] private TextMeshPro m_TitleText;
  [SerializeField] private GameObject m_HintText;
  [SerializeField] private FrustumBeam[] m_FrustumBeams;

  [SerializeField] private Transform m_GhostMesh;

  [Header("Wobble Mode")]
  [SerializeField] private float m_WobbleSpeed;
  [SerializeField] private float m_WobbleScale;

  [Header("Circle Mode")]
  [SerializeField] private float m_CircleSpeed;
  [SerializeField] private float m_CircleRadius;
  [SerializeField] private GameObject m_GuideCircleObject;

  [Header("Slow Follow Mode")]
  [SerializeField] private float m_SlowFollowSmoothing;

  private float m_GuideBeamShowRatio;
  private Renderer[] m_Renderers;

  private Mode m_CurrentMode;

  private Vector3 m_vWobbleBase_RS;
  private Vector3 m_vCircleBase_RS;

  private float m_AnimatedPathTime;
  private Quaternion m_CircleOrientation;
  private float m_CircleRadians;

  private Vector3 m_SlowFollowMoveVel;
  private Vector3 m_SlowFollowRotVel;

  override protected void Awake() {
    base.Awake();
    m_Renderers = GetComponentsInChildren<Renderer>();

    //initialize beams
    for (int i = 0; i < m_FrustumBeams.Length; ++i) {
      //cache scale and set to zero to prep for first time use
      m_FrustumBeams[i].m_BaseScale = m_FrustumBeams[i].m_Beam.localScale;
      m_FrustumBeams[i].m_Beam.localScale = Vector3.zero;
    }

    m_GuideBeamShowRatio = 0.0f;
    m_CurrentMode = kDefaultMode;
    ResetCam();

    // Register the drop camera with scene settings
    Camera camera = GetComponentInChildren<Camera>();
    SceneSettings.m_Instance.RegisterCamera(camera);

    InitSnapGhost(m_GhostMesh, transform);
  }

  protected override TrTransform GetDesiredTransform(TrTransform xf_GS) {
    if (SnapEnabled) {
      return GetSnappedTransform(xf_GS);
    }
    return xf_GS;
  }

  protected override TrTransform GetSnappedTransform(TrTransform xf_GS) {
    TrTransform outXf_GS = xf_GS;

    Vector3 forward = xf_GS.rotation * Vector3.forward;
    forward.y = 0;
    outXf_GS.rotation = Quaternion.LookRotation(forward);

    Vector3 grabSpot = InputManager.m_Instance.GetControllerPosition(m_InteractingController);
    Vector3 grabToCenter = xf_GS.translation - grabSpot;
    outXf_GS.translation = grabSpot +
      grabToCenter.magnitude * (grabToCenter.y > 0 ? Vector3.up : Vector3.down);

    return outXf_GS;
  }

  override public void Show(bool bShow, bool bPlayAudio = true) {
    base.Show(bShow, bPlayAudio);

    RefreshRenderers();
  }

  override protected void OnShow() {
    base.OnShow();

    TrTransform xfSpawn = TrTransform.FromTransform(ViewpointScript.Head);
    InitIntroAnim(xfSpawn, xfSpawn, true);
    m_IntroAnimState = IntroAnimState.In;
    m_IntroAnimValue = 0.0f;
  }

  override protected void UpdateIntroAnimState() {
    IntroAnimState prevState = m_IntroAnimState;
    base.UpdateIntroAnimState();

    // If we're exiting the in state, notify our panel.
    if (prevState != m_IntroAnimState) {
      if (m_IntroAnimState == IntroAnimState.On) {
        ResetCam();
      }
    }
  }

  static public string GetModeName(Mode mode) {
    switch (mode) {
    case Mode.SlowFollow: return "Head Camera";
    case Mode.Stationary: return "Stationary";
    case Mode.Wobble: return "Figure 8";
    case Mode.Circular: return "Circular";
    }
    return "";
  }

  void ResetCam() {
    // Reset wobble cam
    m_AnimatedPathTime = (float)(0.5 * Math.PI);
    m_vWobbleBase_RS = Coords.AsRoom[transform].translation;

    // Figure out which way points in for circle cam.
    Vector3 vInwards = transform.forward;
    vInwards.y = 0;
    vInwards.Normalize();

    // Set the center of the circle that we rotate around.
    m_vCircleBase_RS = transform.position + m_CircleRadius * vInwards;
    m_CircleRadians = (float)Math.Atan2(-vInwards.z, -vInwards.x);

    // Set the initial orientation for circle cam.
    Quaternion qCamOrient = transform.rotation;
    Vector3 eulers = new Vector3(0, (float)(m_CircleRadians * Mathf.Rad2Deg), 0);
    m_CircleOrientation = Quaternion.Euler(eulers) * qCamOrient;

    // Position the guide circle.
    m_GuideCircleObject.transform.localPosition =
        Quaternion.Inverse(transform.rotation) * Quaternion.Euler(0, (float)(-m_CircleRadians * Mathf.Rad2Deg), 0) * new Vector3(-m_CircleRadius, 0, 0);
    m_GuideCircleObject.transform.localRotation = Quaternion.Inverse(transform.rotation) * Quaternion.Euler(0, (float)(-m_CircleRadians * Mathf.Rad2Deg), 0);
    m_GuideCircleObject.transform.localScale = 2.0f * m_CircleRadius * Vector3.one;

    // On slow follow reset, snap to head.
    if (m_CurrentMode == Mode.SlowFollow) {
      transform.position = ViewpointScript.Head.position;
      transform.rotation = ViewpointScript.Head.rotation;
    }
  }

  override protected void OnUpdate() {
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Debug.isDebugBuild && Config.IsExperimental) {
      if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.ToggleHeadStationaryOrWobble)) {
        m_CurrentMode = (m_CurrentMode == Mode.Wobble) ? Mode.Stationary : Mode.Wobble;
        RefreshRenderers();
      } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
          InputManager.KeyboardShortcut.ToggleHeadStationaryOrFollow)) {
        m_CurrentMode = (m_CurrentMode == Mode.SlowFollow) ? Mode.Stationary : Mode.SlowFollow;
        RefreshRenderers();
      }
    }
#endif

    //animate the guide beams in and out, relatively to activation
    float fShowRatio = GetShowRatio();

    //if our transform changed, update the beams
    if (m_GuideBeamShowRatio != fShowRatio) {
      for (int i = 0; i < m_FrustumBeams.Length; ++i) {
        //update scale
        Vector3 vScale = m_FrustumBeams[i].m_BaseScale;
        vScale.z *= fShowRatio;
        m_FrustumBeams[i].m_Beam.localScale = vScale;
      }
    }
    m_GuideBeamShowRatio = fShowRatio;

    if (m_GuideBeamShowRatio >= 1.0f) {
      switch (m_CurrentMode) {
      case Mode.Wobble:
        if (m_UserInteracting) {
          ResetCam();
        } else {
          m_AnimatedPathTime += Time.deltaTime * m_WobbleSpeed;
          Vector3 vWidgetPos = m_vWobbleBase_RS;

          //sideways figure 8, or infinity symbol path
          float fCosTime = Mathf.Cos(m_AnimatedPathTime);
          float fSinTime = Mathf.Sin(m_AnimatedPathTime);
          float fSqrt2 = Mathf.Sqrt(2.0f);
          float fDenom = fSinTime * fSinTime + 1.0f;

          float fX = (m_WobbleScale * fSqrt2 * fCosTime) / fDenom;
          float fY = (m_WobbleScale * fSqrt2 * fCosTime * fSinTime) / fDenom;

          vWidgetPos += transform.right * fX * m_WobbleScale;
          vWidgetPos += transform.up * fY * m_WobbleScale;
          transform.position = vWidgetPos;
        }
        break;
      case Mode.SlowFollow: {
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
          if (Debug.isDebugBuild && Config.IsExperimental) {
            if (InputManager.m_Instance.GetKeyboardShortcutDown(
                InputManager.KeyboardShortcut.DecreaseSlowFollowSmoothing)) {
              m_SlowFollowSmoothing -= 0.001f;
              m_SlowFollowSmoothing = Mathf.Max(m_SlowFollowSmoothing, 0.0f);
            } else if (InputManager.m_Instance.GetKeyboardShortcutDown(
                InputManager.KeyboardShortcut.IncreaseSlowFollowSmoothing)) {
              m_SlowFollowSmoothing += 0.001f;
            }
          }
#endif

          transform.position = Vector3.SmoothDamp(transform.position, ViewpointScript.Head.position,
            ref m_SlowFollowMoveVel, m_SlowFollowSmoothing, Mathf.Infinity, Time.deltaTime);

          Vector3 eulers = transform.rotation.eulerAngles;
          Vector3 targetEulers = ViewpointScript.Head.eulerAngles;
          eulers.x = Mathf.SmoothDampAngle(eulers.x, targetEulers.x,
            ref m_SlowFollowRotVel.x, m_SlowFollowSmoothing, Mathf.Infinity, Time.deltaTime);
          eulers.y = Mathf.SmoothDampAngle(eulers.y, targetEulers.y,
            ref m_SlowFollowRotVel.y, m_SlowFollowSmoothing, Mathf.Infinity, Time.deltaTime);
          eulers.z = Mathf.SmoothDampAngle(eulers.z, targetEulers.z,
            ref m_SlowFollowRotVel.z, m_SlowFollowSmoothing, Mathf.Infinity, Time.deltaTime);

          transform.rotation = Quaternion.Euler(eulers);
        }
        break;
      case Mode.Circular:
        if (m_UserInteracting) {
          ResetCam();
          if (!m_GuideCircleObject.activeSelf) {
            m_GuideCircleObject.GetComponentInChildren<MeshRenderer>().material
                .SetFloat("_RevealStartTime", Time.time);
            m_GuideCircleObject.SetActive(true);
          }
        } else {
          // Set the camera position.
          m_CircleRadians += (float)(Time.deltaTime * m_CircleSpeed * 2.0 * Math.PI);
          Vector3 vWidgetPos = m_vCircleBase_RS;
          vWidgetPos[0] += m_CircleRadius * (float)Math.Cos(m_CircleRadians);
          vWidgetPos[2] += m_CircleRadius * (float)Math.Sin(m_CircleRadians);
          transform.position = vWidgetPos;

          // Set the camera orientation.
          Vector3 eulers = new Vector3(0, (float)(-m_CircleRadians * Mathf.Rad2Deg), 0);
          transform.rotation = Quaternion.Euler(eulers) * m_CircleOrientation;

          // Deactivate the guide circle.
          m_GuideCircleObject.SetActive(false);
        }
        break;
      }
    }
  }

  override public void Activate(bool bActive) {
    base.Activate(bActive);
    Color activeColor = bActive ? Color.white : Color.grey;
    m_TitleText.color = activeColor;
    m_HintText.SetActive(bActive);

    for (int i = 0; i < m_FrustumBeams.Length; ++i) {
      m_FrustumBeams[i].m_BeamMesh.material.color = activeColor;
    }
  }

  public void SetMode(Mode newMode) {
    m_CurrentMode = newMode;
    RefreshRenderers();
    ResetCam();
  }

  public Mode GetMode() {
    return m_CurrentMode;
  }

  void RefreshRenderers() {
    // Show the widget beams if we're not in slow follow mode, and we're active.
    bool bShow = ShouldHmdBeVisible();
    for (int i = 0; i < m_Renderers.Length; ++i) {
      m_Renderers[i].enabled = bShow;
    }
  }

  public bool ShouldHmdBeVisible() {
    return (m_CurrentMode != Mode.SlowFollow) &&
      (m_CurrentState == State.Showing || m_CurrentState == State.Visible);
  }
}
}  // namespace TiltBrush
