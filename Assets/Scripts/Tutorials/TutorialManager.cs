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

public enum TutorialType {
  NextTip,
  UndoRedo,
  WorldTransform,
  WorldTransformReset,
  Grip,
  ScaleModel,
  PinModel,
  SnapModel,
  SnapLine,
  DrawOnGuides,
  ColorPalette,
  SwapControllers,
  Duplicate,
  PinCushion,
  ControllerConsole,
  TossToDismiss,
  Num // Don't remove.
}

public enum IntroTutorialState {
  ActivateBrush,
  WaitForBrushStrokeStart,
  WaitForBrushStrokeEnd,
  SwipeToUnlockPanels,
  WaitForSwipe,
  ActivatePanels,
  DelayForPointToPanelHint,
  WaitForPanelInteract,
  InitializeForNoCreation,
  Done
}

[System.Serializable]
public struct TutorialPrefabMap {
  public GameObject m_TutorialPrefab;
  public TutorialType m_Type;
}

public class TutorialManager : MonoBehaviour {
  static public TutorialManager m_Instance;

  [Header("Intro Tutorial")]
  [SerializeField] private AudioClip m_IntroBeginSound;
  [SerializeField] private float m_IntroPaintRegisterDistance;
  [SerializeField] private float m_IntroPointHintDelay = 1.5f;
  [SerializeField] private float m_IntroSwipeAdvanceAmount = 0.3f;
  [SerializeField] private GameObject m_PointHintPrefab;

  [Header("Tutorial Panel")]
  [SerializeField] private TutorialPrefabMap [] m_PrefabMap;

  [Header("Light Gizmo Tutorial")]
  [SerializeField] private GameObject m_LightGizmoHintPrefab;
  [SerializeField] private float m_LightGizmoHoverTimeBeforeHint = 3f;

  private IntroTutorialState m_IntroState;
  private float m_IntroSwipeTotal;
  private float m_IntroStateCountdown;
  private HintArrowLineScript m_IntroPointHintArrowLine;

  private Vector3 m_IntroPaintLastPos;
  private float m_IntroPaintMovementAmount;
  private bool m_IntroPaintWasPainting;

  private HintObjectScript m_LightGizmoHint;
  private float m_LightGizmoHoverTimer;

  public IntroTutorialState IntroState {
    set {
      m_IntroState = value;
    }
    get { return m_IntroState; }
  }

  public bool TutorialActive() {
    return m_IntroState != TiltBrush.IntroTutorialState.Done;
  }

  public bool TutorialPanelsHaveSpawned() {
    return m_IntroState > TiltBrush.IntroTutorialState.WaitForSwipe;
  }

  void Awake() {
    m_Instance = this;
  }

  public GameObject GetTutorialPrefab(TutorialType type) {
    for (int i = 0; i < m_PrefabMap.Length; ++i) {
      if (m_PrefabMap[i].m_Type == type) {
        return m_PrefabMap[i].m_TutorialPrefab;
      }
    }
    return null;
  }

  public void UpdateIntroTutorial() {
    switch (m_IntroState) {
    case IntroTutorialState.ActivateBrush:
      ActivateControllerTutorial(InputManager.ControllerName.Brush, true);
      ActivateControllerTutorial(InputManager.ControllerName.Wand, false);
      IntroState = IntroTutorialState.WaitForBrushStrokeStart;
      break;

    case IntroTutorialState.WaitForBrushStrokeStart:
      if (PointerManager.m_Instance.IsMainPointerCreatingStroke()) {
        IntroState = IntroTutorialState.WaitForBrushStrokeEnd;
      }
      break;

    case IntroTutorialState.WaitForBrushStrokeEnd:
      if (PointerManager.m_Instance.IsMainPointerCreatingStroke()) {
        // Lazy init of initial painting position.
        if (!m_IntroPaintWasPainting) {
          m_IntroPaintLastPos = InputManager.Brush.Transform.position;
          m_IntroPaintMovementAmount = 0.0f;
          m_IntroPaintWasPainting = true;
        } else {
          // Increase our painting distance with the movement diff.
          Vector3 brushPos = InputManager.Brush.Transform.position;
          float fPrevAmount = m_IntroPaintMovementAmount;
          m_IntroPaintMovementAmount += Vector3.Distance(brushPos, m_IntroPaintLastPos);
          m_IntroPaintLastPos = brushPos;

          // If we went far enough, shine a bit.
          if (fPrevAmount < m_IntroPaintRegisterDistance &&
              m_IntroPaintMovementAmount >= m_IntroPaintRegisterDistance) {
            AudioManager.m_Instance.TriggerOneShot(m_IntroBeginSound,
                InputManager.m_Instance.GetControllerPosition(InputManager.ControllerName.Brush),
                1.0f);
            DisableControllerTutorial(InputManager.ControllerName.Brush);
          }
        }
      } else if (!PointerManager.m_Instance.IsMainPointerCreatingStroke()) {
        // If we're not painting, move on if we've painted far enough.
        m_IntroPaintWasPainting = false;
        if (m_IntroPaintMovementAmount >= m_IntroPaintRegisterDistance) {
          IntroState = IntroTutorialState.SwipeToUnlockPanels;
        }
      }
      break;

    case IntroTutorialState.SwipeToUnlockPanels:
      ActivateControllerTutorial(InputManager.ControllerName.Wand, true);
      m_IntroSwipeTotal = 0.0f;
      IntroState = IntroTutorialState.WaitForSwipe;
      break;

    case IntroTutorialState.WaitForSwipe:
      m_IntroSwipeTotal += InputManager.m_Instance.GetAdjustedWandScrollAmount();
      if (Mathf.Abs(m_IntroSwipeTotal) > m_IntroSwipeAdvanceAmount) {
        DisableControllerTutorial(InputManager.ControllerName.Wand);
        IntroState = IntroTutorialState.ActivatePanels;
      }
      break;

    case IntroTutorialState.ActivatePanels:
      SketchControlsScript.m_Instance.RequestPanelsVisibility(true);
      m_IntroStateCountdown = m_IntroPointHintDelay;
      IntroState = IntroTutorialState.DelayForPointToPanelHint;
      break;

    case IntroTutorialState.DelayForPointToPanelHint:
      // If we're waiting to tell the user how to interact with the UI and they do it already,
      // skip that step.
      if (SketchControlsScript.m_Instance.IsUserInteractingWithUI()) {
        IntroState = IntroTutorialState.Done;
        // The player can now be considered to have 'played the game'.
        PlayerPrefs.SetInt(App.kPlayerPrefHasPlayedBefore, 1);

        PromoManager.m_Instance.RequestPromo(PromoType.BrushSize);
        break;
      }

      m_IntroStateCountdown -= Time.deltaTime;
      if (m_IntroStateCountdown <= 0.0f) {
        GameObject obj = (GameObject)Instantiate(m_PointHintPrefab);
        m_IntroPointHintArrowLine = obj.GetComponent<HintArrowLineScript>();

        InputManager.m_Instance.TriggerHapticsPulse(InputManager.ControllerName.Brush, 4, 0.15f, 0.1f);
        EnablePointAtPanelHint(true);
        IntroState = IntroTutorialState.WaitForPanelInteract;
      }
      break;

    case IntroTutorialState.WaitForPanelInteract:
      // Keep hint arrow line connected to the pointer and nearest panel center.
      AlignPointLineToPanels();
      if (SketchControlsScript.m_Instance.IsUserInteractingWithUI()) {
        m_IntroPointHintArrowLine.Reset();
        Destroy(m_IntroPointHintArrowLine);
        m_IntroPointHintArrowLine = null;

        EnablePointAtPanelHint(false);
        IntroState = IntroTutorialState.Done;
        // The player can now be considered to have 'played the game'.
        PlayerPrefs.SetInt (App.kPlayerPrefHasPlayedBefore, 1);

        PromoManager.m_Instance.RequestPromo(PromoType.BrushSize);
      }
      break;

    // Special case for viewing only modes.  Like Tilt Brush Gallery RIP.
    case IntroTutorialState.InitializeForNoCreation:
      SketchControlsScript.m_Instance.RequestPanelsVisibility(true);
      IntroState = IntroTutorialState.Done;
      break;
    }
  }

  void AlignPointLineToPanels() {
    TrTransform xfBrush = TrTransform.FromTransform(
        InputManager.m_Instance.GetBrushControllerAttachPoint());
    TrTransform xfTarget = new TrTransform();
    xfTarget.translation = PanelManager.m_Instance.
        GetFixedPanelPosClosestToPoint(xfBrush.translation);
    xfBrush.rotation = Quaternion.LookRotation(
        (xfTarget.translation - xfBrush.translation).normalized, Vector3.up);
    xfTarget.rotation = xfBrush.rotation;
    m_IntroPointHintArrowLine.UpdateLine(xfBrush, xfTarget);
  }

  public void EnableQuickLoadTutorial(bool bEnable) {
    BaseControllerBehavior behavior =
        InputManager.m_Instance.GetControllerBehavior(InputManager.ControllerName.Wand);
    ControllerBehaviorWand rWandScript = behavior as ControllerBehaviorWand;
    if (rWandScript) {
      rWandScript.EnableQuickLoadHintObject(bEnable);
    }
  }

  public void EnablePointAtPanelHint(bool bEnable) {
    BaseControllerBehavior behavior =
        InputManager.m_Instance.GetControllerBehavior(InputManager.ControllerName.Brush);
    ControllerBehaviorBrush rBrushScript = behavior as ControllerBehaviorBrush;
    if (rBrushScript) {
      rBrushScript.EnablePointAtPanelsHintObject(bEnable);
    }
  }

  public void ActivateControllerTutorial(InputManager.ControllerName eName, bool bActivate) {
    ControllerTutorialScript tutorial =
        InputManager.m_Instance.GetControllerTutorial(eName);
    if (tutorial) {
      tutorial.Activate(bActivate);
      if (bActivate) {
        AudioManager.m_Instance.PlayHintAnimateSound(tutorial.transform.position);
      }
    }
  }

  public void UpdateLightGizmoHint() {
    if (m_LightGizmoHoverTimer >= 0) {
      if (!m_LightGizmoHint) {
        var hint = Instantiate(m_LightGizmoHintPrefab);
        m_LightGizmoHint = hint.GetComponent<HintObjectScript>();
      }

      // TODO: Fix this.
      // This assumes there is only one panel of type Lights created.
      BasePanel basePanel = PanelManager.m_Instance.GetPanelByType(BasePanel.PanelType.Lights);
      LightsPanel lightsPanel = basePanel as LightsPanel;
      if (lightsPanel != null) {
        if (lightsPanel.IsLightGizmoBeingDragged) {
          m_LightGizmoHoverTimer = -1;
          if (m_LightGizmoHint.IsActive()) {
            m_LightGizmoHint.Activate(false);
          }
        } else if (lightsPanel.IsLightGizmoBeingHovered) {
          m_LightGizmoHoverTimer += Time.deltaTime;
          if (m_LightGizmoHoverTimer > m_LightGizmoHoverTimeBeforeHint) {
            if (!m_LightGizmoHint.IsActive()) {
              m_LightGizmoHint.Activate(true);
            }
            m_LightGizmoHint.transform.position = lightsPanel.ActiveLightGizmoPosition;
            m_LightGizmoHint.transform.rotation = lightsPanel.transform.rotation;
          }
        } else {
          m_LightGizmoHint.Activate(false);
          m_LightGizmoHoverTimer = m_LightGizmoHoverTimeBeforeHint / 2;
        }
      }
    }
  }

  public void ClearLightGizmoHint() {
    m_LightGizmoHint.Activate(false);

    // -1 is the signifying value for the hint being registered.  If we're equal to or greater
    // than 0, the user didn't get the message, so reset the hint.
    if (m_LightGizmoHoverTimer >= 0) {
      m_LightGizmoHoverTimer = m_LightGizmoHoverTimeBeforeHint / 2;
    }
  }

  public void DisableControllerTutorial(InputManager.ControllerName eName) {
    ControllerTutorialScript tutorial =
        InputManager.m_Instance.GetControllerTutorial(eName);
    if (tutorial) {
      tutorial.DisableTutorialObject();
    }
  }
}

} // namespace TiltBrush