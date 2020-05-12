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

/// This promo is shown on the brush when a floating panel is opened. It remains visible until
/// any action is taken by the user, at which point it hides. It reactivates if a user intersects
/// any floating panel. If the user grabs a panel, it switches into the tossed state, in which it
/// only activates on the grabbing controller if a user is grabbing a floating panel.
public class FloatingPanelPromo : BasePromo {
  BasePanel m_Panel;
  BasePanel m_InitialPanel;
  bool m_TossState;
  bool m_RequireNewPanelToDisplay;
  int m_NumPanelsGrabbed;

  bool ShouldShowTossVisuals() {
    return m_Panel != null &&
        PanelManager.m_Instance != null &&
        !PanelManager.m_Instance.IsPanelCore(m_Panel.Type) &&
        m_NumPanelsGrabbed > 1;
  }

  public override string PrefsKey {
    get { return PromoManager.kPromoPrefix + "FloatingPanel"; }
  }

  private bool TossState {
    get {
      return m_TossState;
    }
    set {
      m_TossState = value;
      // Update the text on both controllers' hint objects.
      string text = m_TossState ? PromoManager.m_Instance.TossPanelHintText :
          PromoManager.m_Instance.GrabPanelHintText;
      if (InputManager.Brush.Geometry.FloatingPanelHintObject != null) {
        InputManager.Brush.Geometry.FloatingPanelHintObject.SetHintText(text);
      }
      if (InputManager.Wand.Geometry.FloatingPanelHintObject != null) {
        InputManager.Wand.Geometry.FloatingPanelHintObject.SetHintText(text);
      }
    }
  }

  public FloatingPanelPromo() : base(PromoType.FloatingPanel) {
    // Initialize to grab panel state.
    TossState = false;
    m_RequireNewPanelToDisplay = false;
    m_NumPanelsGrabbed = 0;

    // Some tools, like the Multicam, don't look great with promos.
    if (SketchSurfacePanel.m_Instance.ActiveTool.CanShowPromosWhileInUse()) {
      RequestDisplayForGrabState();
    }
  }

  protected override void OnDisplay() {
    m_Panel.ActivatePromoBorder(!m_TossState || ShouldShowTossVisuals());
    if (m_HintObject != null) {
      AudioManager.m_Instance.PlayHintAnimateSound(m_HintObject.transform.position);
    }
  }

  protected override void OnHide() {
    m_Panel.ActivatePromoBorder(false);
    m_HintObject = null;
    PromoManager.m_Instance.HintLine.Reset();
    PromoManager.m_Instance.HintLine.gameObject.SetActive(false);
  }

  public override void OnActive() {
    // Update line visibility and alignment.
    bool lineVisible = !SketchControlsScript.m_Instance.IsUserInteractingWithUI() && !TossState;
    if (lineVisible) {
      if (!PromoManager.m_Instance.HintLine.gameObject.activeSelf) {
        PromoManager.m_Instance.HintLine.gameObject.SetActive(true);
        PromoManager.m_Instance.HintLine.GlobalMode = true;
      }
      AlignHintLine();
    } else if (PromoManager.m_Instance.HintLine.gameObject.activeSelf) {
      PromoManager.m_Instance.HintLine.gameObject.SetActive(false);
      PromoManager.m_Instance.HintLine.Reset();
    }

    // Update grab state.
    if (!TossState) {
      // If we open a new panel while the promo is active, switch focus to that panel.
      BasePanel lastPanel = PanelManager.m_Instance.LastOpenedPanel();
      if (m_Panel != lastPanel) {
        m_Panel.ActivatePromoBorder(false);
        m_Panel = lastPanel;
        m_Panel.ActivatePromoBorder(true);
        m_InitialPanel = m_Panel;
      }

      // If we're grabbing our highlighted panel, change the toss state.
      if (SketchControlsScript.m_Instance.IsUserGrabbingWidget(m_Panel.WidgetSibling)) {
        TossState = true;
        RequestDisplayForTossState();
      } else {
        // Bunch of cases where we should cancel the promo for now.
        bool usingActiveTool =
            InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate) &&
            !SketchSurfacePanel.m_Instance.ActiveTool.IsEatingInput;
        if (usingActiveTool ||
            SketchControlsScript.m_Instance.IsUserInteractingWithAnyWidget() ||
            SketchControlsScript.m_Instance.IsUserGrabbingWorld() ||
            InputManager.m_Instance.ControllersAreSwapping()) {
          m_RequireNewPanelToDisplay = true;
          m_Request = RequestingState.ToHide;
        }
      }
    } else {
      // If we threw the panel, this promo is complete.
      if (m_Panel.WidgetSibling.IsTossed()) {
        PromoManager.m_Instance.RecordCompletion(this.PromoType);
      } else if (!SketchControlsScript.m_Instance.IsUserGrabbingWidget(m_Panel.WidgetSibling)) {
        // If we stopped grabbing the panel, be done for now.
        m_Request = RequestingState.ToHide;
      }
    }
  }

  public override void OnIdle() {
    if (!TossState) {
      if (SketchSurfacePanel.m_Instance.ActiveTool.CanShowPromosWhileInUse()) {
        // If the promo was canceled during the grab state due to user input, don't show it again
        // until the user has spawned another panel.
        if (m_RequireNewPanelToDisplay) {
          if (m_InitialPanel != PanelManager.m_Instance.LastOpenedPanel()) {
            RequestDisplayForGrabState();
          } else if (SketchControlsScript.m_Instance.IsUserGrabbingAnyPanel()) {
            // Hey they figured it out without us!  Advance state.
            TossState = true;
            RequestDisplayForTossState();
          }
        } else {
          // Delayed display if the promo was requested when a blocking tool was active, or hidden.
          RequestDisplayForGrabState();
        }
      }
    } else {
      if (SketchControlsScript.m_Instance.IsUserGrabbingAnyPanel()) {
        RequestDisplayForTossState();
      }
    }
  }

  void RequestDisplayForGrabState() {
    m_Panel = PanelManager.m_Instance.LastOpenedPanel();
    m_InitialPanel = m_Panel;
    m_HintObject = InputManager.GetControllerGeometry(
        InputManager.ControllerName.Brush).FloatingPanelHintObject;
    if (m_HintObject != null) {
      m_HintObject.SetHintText(PromoManager.m_Instance.GrabPanelHintText);
    }
    m_RequireNewPanelToDisplay = false;
    m_Request = RequestingState.ToDisplay;
  }

  void RequestDisplayForTossState() {
    m_Panel = PanelManager.m_Instance.LastPanelInteractedWith;

    // If we've got a current hint object, close it.
    if (m_HintObject != null) {
      m_HintObject.Activate(false);
      m_HintObject = null;
    }

    ++m_NumPanelsGrabbed;
    // Don't show any flashy visuals unless we've grabbed a panel a couple times.
    // Note that we still want to change state each time, however, as we want to record
    // completion correctly if the user tosses a panel without us asking.
    if (ShouldShowTossVisuals()) {
      // Refresh our hint object to be on the grabbing controller and show it.
      m_HintObject = InputManager.GetControllerGeometry(
          SketchControlsScript.m_Instance.OneHandGrabController).FloatingPanelHintObject;
      if (m_HintObject != null) {
        m_HintObject.SetHintText(PromoManager.m_Instance.TossPanelHintText);
        m_HintObject.Activate(true);
      }
    }
    m_Request = RequestingState.ToDisplay;
  }

  void AlignHintLine() {
    TrTransform xfBrush = TrTransform.FromTransform(
        InputManager.m_Instance.GetBrushControllerAttachPoint());
    TrTransform xfTarget = new TrTransform();
    xfTarget.translation = TrTransform.FromTransform(m_Panel.WidgetSibling.transform).translation;
    xfBrush.rotation = Quaternion.LookRotation(
        (xfTarget.translation - xfBrush.translation).normalized, Vector3.up);
    xfTarget.rotation = xfBrush.rotation;
    PromoManager.m_Instance.HintLine.UpdateLine(xfBrush, xfTarget);
  }
}
} // namespace TiltBrush
