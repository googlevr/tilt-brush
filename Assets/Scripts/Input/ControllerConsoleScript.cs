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

public class ControllerConsoleScript : MonoBehaviour {
  /// Number of characters to look back when searching for a better character to split on when
  /// word wrapping
  private const int kSplitSearchMagnitude = 10;

  static public ControllerConsoleScript m_Instance;

  [SerializeField] private Transform m_RenderablesParent;
  [SerializeField] private Transform m_DynamicBackground;
  [SerializeField] private Transform m_Title;
  [SerializeField] private Transform m_Clock;
  [SerializeField] private ParticleSystem m_Notification;
  [SerializeField] private Transform m_NotificationAnchor;
  [SerializeField] private Transform m_AutosaveIcon;
  [SerializeField] private float m_NotificationDisplayDuration;
  [SerializeField] private Vector2 m_NotificationShrinkRange;
  [SerializeField] private GameObject m_InfoLinePrefab;
  [SerializeField] private Vector3 m_BaseLineLocalPositionOffset;
  [SerializeField] private float m_LineSpacing;
  [SerializeField] private int m_NumLines;
  [SerializeField] private float m_MinBackgroundWidth;
  [SerializeField] private InputManager.ControllerName m_AttachedControllerName;
  [SerializeField] private Color m_StandardTextColor;
  [SerializeField] private Color m_NotificationTextColor;
  [SerializeField] private float m_NotificationHapticPulse = 1.0f;
  [SerializeField] private float m_NotificationHapticTimeOffset;
  [SerializeField] private float m_MaxLineWidth;
  private Renderer[] m_MeshRenderers;
  private float m_NotificationDisplayTimer;
  private string m_LastLoggedLine;

  public class InfoLine {
    public GameObject m_Line;
    public TextMesh m_TextMesh;
    public Renderer m_Renderer;
    public float m_LineWidth;
  }
  private InfoLine[] m_InfoLines;
  private int m_LineOperateIndex;

  [SerializeField] private float m_ActivationAngle_Default = 115.0f;
  [SerializeField] private float m_ActivationAngle_LogitechPen = 145.0f;

  private float m_ActivateAngle;
  private float m_ActivateSpeed = 8.0f;
  private float m_ActivateTimer;

  [SerializeField] private float m_ActivateDelayDuration;
  private float m_ActivateDelayTimer;
  private enum State {
    Disabled,
    DelayBeforeActivate,
    Activating,
    Active,
    Disabling
  }
  private State m_CurrentState;

  void Awake() {
    m_Instance = this;

    //create and position all our text lines
    m_NumLines = Mathf.Max(m_NumLines, 1);
    m_InfoLines = new InfoLine[m_NumLines];
    for (int i = 0; i < m_InfoLines.Length; ++i) {
      m_InfoLines[i] = new InfoLine();

      GameObject rLine = (GameObject)Instantiate(m_InfoLinePrefab);
      rLine.transform.parent = m_RenderablesParent;
      m_InfoLines[i].m_Line = rLine;
      m_InfoLines[i].m_TextMesh = rLine.GetComponent<TextMesh>();
      m_InfoLines[i].m_Renderer = rLine.GetComponent<Renderer>();

      Vector3 vLocalPos = m_BaseLineLocalPositionOffset;
      vLocalPos.y += m_LineSpacing * (float)i;
      rLine.transform.localPosition = vLocalPos;
      rLine.transform.localRotation = Quaternion.identity;
    }

    m_LineOperateIndex = m_InfoLines.Length - 1;
    m_MeshRenderers = m_RenderablesParent.GetComponentsInChildren<Renderer>();

    UpdateBackgroundWidth();

    SetState(State.Disabled);

    InputManager.OnSwapControllers += AttachToBrush;

#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      m_AutosaveIcon.gameObject.SetActive(true);
    }
#endif
  }

  void OnDestroy() {
    InputManager.OnSwapControllers -= AttachToBrush;
  }

  void Start() {
    if (App.Config.VrHardware == VrHardware.None) {
      gameObject.SetActive(false);
    }
  }

  void Update() {
    //update state according to player's view to meter
    bool bRequestShowing = false;
    float fGazeAngle = 0.0f;
    if (InputManager.Controllers[(int)m_AttachedControllerName].IsTrackedObjectValid &&
        TutorialManager.m_Instance.TutorialPanelsHaveSpawned() &&
        !InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate) &&
        !SketchControlsScript.m_Instance.IsUserInteractingWithAnyWidget()) {
      Ray gazeRay = ViewpointScript.Gaze;
      fGazeAngle = Vector3.Angle(gazeRay.direction, -m_RenderablesParent.transform.forward);
      bRequestShowing = fGazeAngle > m_ActivateAngle;
    }

    // Update notification light.
    if (m_Notification.isPlaying) {
      // Set particle size according to how much we're looking at the light.
      float fSize = 1.0f;
      if (fGazeAngle > m_NotificationShrinkRange.x) {
        fSize = Mathf.InverseLerp(m_NotificationShrinkRange.y, m_NotificationShrinkRange.x,
            Mathf.Min(fGazeAngle, m_NotificationShrinkRange.y));
      }

      // Resize all particles.
      ParticleSystem.Particle[] aParticles =
          new ParticleSystem.Particle[m_Notification.main.maxParticles];
      int iNumParticles = m_Notification.GetParticles(aParticles);
      for (int i = 0; i < iNumParticles; ++i) {
        aParticles[i].startSize = fSize * m_Notification.main.startSizeMultiplier;
      }
      m_Notification.SetParticles(aParticles, iNumParticles);

      // Buzz each second, on the second.
      int iPrevRoundOff = (int)(m_NotificationDisplayTimer + m_NotificationHapticTimeOffset);
      m_NotificationDisplayTimer -= Time.deltaTime;
      int iCurrRoundOff = (int)(m_NotificationDisplayTimer + m_NotificationHapticTimeOffset);

      if (iPrevRoundOff != iCurrRoundOff) {
        InputManager.m_Instance.TriggerHaptics(m_AttachedControllerName,
            m_NotificationHapticPulse);
      }

      // Turn off if we're out of time of if the console is opening.
      if (bRequestShowing || m_NotificationDisplayTimer <= 0.0f) {
        m_Notification.Stop();
        m_Notification.Clear();
        m_NotificationDisplayTimer = 0.0f;
      }
    }

    //update current state
    switch (m_CurrentState) {
    case State.Disabled:
      if (bRequestShowing) {
        SetState(State.DelayBeforeActivate);
      }
      break;
    case State.DelayBeforeActivate:
      //bail out if we're not visible
      if (!bRequestShowing) {
        SetState(State.Disabling);
      } else {
        m_ActivateDelayTimer -= Time.deltaTime;
        if (m_ActivateDelayTimer <= 0.0f) {
          SetState(State.Activating);
        }
      }
      break;
    case State.Activating:
      //bail out if we're not visible
      if (!bRequestShowing) {
        SetState(State.Disabling);
      } else {
        m_ActivateTimer += Time.deltaTime * m_ActivateSpeed;
        if (m_ActivateTimer >= 1.0f) {
          m_ActivateTimer = 1.0f;
          SetState(State.Active);
        }
      }
      UpdateMeterScale(m_ActivateTimer);
      break;
    case State.Active:
      if (!bRequestShowing) {
        SetState(State.Disabling);
      }
      break;
    case State.Disabling:
      //start showing if we're visible
      if (bRequestShowing) {
        SetState(State.Activating);
      } else {
        m_ActivateTimer -= Time.deltaTime * m_ActivateSpeed;
        if (m_ActivateTimer <= 0.0f) {
          m_ActivateTimer = 0.0f;
          SetState(State.Disabled);
        }
      }
      UpdateMeterScale(m_ActivateTimer);
      break;
    }
  }

  void SetState(State rDesiredState) {
    switch (rDesiredState) {
    case State.Disabled:
      m_RenderablesParent.gameObject.SetActive(false);
      break;
    case State.DelayBeforeActivate:
      m_ActivateDelayTimer = m_ActivateDelayDuration;
      break;
    case State.Activating:
      m_RenderablesParent.gameObject.SetActive(true);
      for (int i = 0; i < m_MeshRenderers.Length; ++i) {
        m_MeshRenderers[i].enabled = true;
      }
      m_ActivateTimer = 0.0f;
      UpdateMeterScale(0.0f);
      break;
    }
    m_CurrentState = rDesiredState;
  }

  void UpdateMeterScale(float fRatio) {
    Vector3 vMeterScale = m_RenderablesParent.localScale;
    vMeterScale.x = fRatio;
    m_RenderablesParent.localScale = vMeterScale;
  }

  public void AddNewLine(string sText, bool bNotify = false, bool skipLog=false) {
    if (m_LastLoggedLine == sText) {
      // Don't log the same line multiple times in a row.
      return;
    }
    m_LastLoggedLine = sText;
    if (!skipLog) {
      System.Console.WriteLine("Controller console: " + sText);
    }

    if (sText.Contains("\n")) {
      foreach (string line in sText.Split('\n')) {
        AddNewLineImpl(line, bNotify);
        bNotify = false;
      }
    } else {
      AddNewLineImpl(sText, bNotify);
    }
  }

  private void AddNewLineImpl(string sText, bool bNotify = false) {
    string remainingText = "";

    //scoot all the lines around
    for (int i = 0; i < m_InfoLines.Length; ++i) {
      Vector3 vLocalPos = m_InfoLines[i].m_Line.transform.localPosition;
      vLocalPos.y += m_LineSpacing;
      m_InfoLines[i].m_Line.transform.localPosition = vLocalPos;
    }

    //set the new line's text
    InfoLine operateLine = m_InfoLines[m_LineOperateIndex];
    operateLine.m_Line.transform.localPosition = m_BaseLineLocalPositionOffset;
    operateLine.m_TextMesh.text = sText;
    operateLine.m_Renderer.material.color = bNotify ? m_NotificationTextColor :m_StandardTextColor;
    operateLine.m_LineWidth = TextMeasureScript.GetTextWidth(operateLine.m_TextMesh);

    // Split long lines - cheap and nasty, as it assumes that all characters in a string are the
    // same length, but as the console widens to accommodate its lines anyway, I don't think it
    // matters.
    if (operateLine.m_LineWidth > m_MaxLineWidth) {
      float portion = m_MaxLineWidth / operateLine.m_LineWidth;
      int splitPoint = Mathf.FloorToInt(sText.Length * portion);
      // Look back a few characters to see if there's a better character to split on
      for (int i = 0; (i > -kSplitSearchMagnitude) && ((i + splitPoint) > 0); i--) {
        if (!System.Char.IsLetter(sText[i + splitPoint])) {
          splitPoint += i;
          break;
        }
      }
      remainingText = sText.Substring(splitPoint).TrimStart();
      operateLine.m_TextMesh.text = sText.Substring(0, splitPoint);
      operateLine.m_LineWidth = TextMeasureScript.GetTextWidth(operateLine.m_TextMesh);
    }

    //update our operating index
    --m_LineOperateIndex;
    if (m_LineOperateIndex < 0) {
      m_LineOperateIndex += m_InfoLines.Length;
    }

    UpdateBackgroundWidth();

    //reset our timer
    if (bNotify) {
      m_NotificationDisplayTimer = m_NotificationDisplayDuration;
      m_Notification.Play();
    }
    if (remainingText.Length > 0) {
      // TODO: remove the tail-recursion
      AddNewLineImpl(remainingText, false);
    }
  }

  void UpdateBackgroundWidth() {
    //find the largest width
    float fBGWidth = m_MinBackgroundWidth;
    float fAdjustedHalfWidth = 0.5f * (2.0f - m_BaseLineLocalPositionOffset.x);
    for (int i = 0; i < m_InfoLines.Length; ++i) {
      fBGWidth = Mathf.Max(fBGWidth, m_InfoLines[i].m_LineWidth * fAdjustedHalfWidth);
    }

    //scale dynamic background to match width
    Vector3 vBGScale = m_DynamicBackground.localScale;
    vBGScale.x = fBGWidth;
    m_DynamicBackground.localScale = vBGScale;

    //position lines up against the far right side
    for (int i = 0; i < m_InfoLines.Length; ++i) {
      Vector3 vAdjustedAnchorPos = m_InfoLines[i].m_Line.transform.localPosition;
      vAdjustedAnchorPos.x = m_BaseLineLocalPositionOffset.x * fBGWidth;
      m_InfoLines[i].m_Line.transform.localPosition = vAdjustedAnchorPos;
    }

    //position title up against the far left side
    Vector3 vTitleAnchorPos = m_Title.localPosition;
    vTitleAnchorPos.x = -fBGWidth;
    m_Title.localPosition = vTitleAnchorPos;

    //position clock up against the far right side
    Vector3 vClockAnchorPos = m_Clock.localPosition;
    vClockAnchorPos.x = -fBGWidth;
    m_Clock.localPosition = vClockAnchorPos;

#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      //position autosave dot on the right hand side.
      Vector3 vAutosavePos = m_AutosaveIcon.localPosition;
      vAutosavePos.x = fBGWidth;
      m_AutosaveIcon.localPosition = vAutosavePos;
    }
#endif
  }

  private void AttachToBrush() {
    // Note the comment above InputManager.Brush.  This object may not be up
    // to date if controllers have changed this frame.
    AttachToController(InputManager.Brush.Behavior);
  }

  public void AttachToController(BaseControllerBehavior behavior) {
    ControllerGeometry geo = behavior.ControllerGeometry;
    transform.parent = null;
    transform.position = geo.ConsoleAttachPoint.position;
    transform.rotation = geo.ConsoleAttachPoint.rotation;
    transform.parent = geo.ConsoleAttachPoint.transform;

    m_NotificationAnchor.position = geo.BaseAttachPoint.position;
    m_NotificationAnchor.rotation = geo.BaseAttachPoint.rotation;

    // The logitech pen has a custom activation angle.
    ControllerStyle geoStyle = geo.Style;
    m_ActivateAngle = (geoStyle == ControllerStyle.LogitechPen) ?
        m_ActivationAngle_LogitechPen : m_ActivationAngle_Default;
  }
}
}  // namespace TiltBrush
