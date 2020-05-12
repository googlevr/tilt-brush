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

  /// Promo: Show the user how to toggle advanced panels.
  /// Completion: User clicks the advanced panels toggle button.
  /// Conditions: User has run Tilt Brush at least once and has not ever pressed the advanced
  ///             panels toggle button.
  public class AdvancedPanelsPromo : BasePromo {
    // This is a bummer these have to be hard coded.
    private float m_TimeBeforeDisplay = 2.0f;
    private float m_TimeBeforeReadDismiss = 4.0f;
    private float m_ReadDismissTimer;
    private HintObjectScript m_CustomHintObject;

    public AdvancedPanelsPromo() : base(PromoType.AdvancedPanels) {}

    public override string PrefsKey {
      get { return PromoManager.kPromoPrefix + "AdvancedPanels"; }
    }

    protected override void OnDisplay() {
      AdminPanel adminPanel = PanelManager.m_Instance.GetAdminPanel() as AdminPanel;
      adminPanel.ActivatePromoBorder(true);
      InputManager.m_Instance.TriggerHapticsPulse(InputManager.ControllerName.Wand,
          4, 0.15f, 0.1f);

      // Parent and position the button highlight to our target button.
      PromoManager.m_Instance.ButtonHighlight.transform.parent = adminPanel.AdvancedButton;
      PromoManager.m_Instance.ResetButtonHighlightXf();
      PromoManager.m_Instance.ButtonHighlight.SetActive(true);

      // We're not using BasePromo.m_HintObject because we need to control when it's being shown.
      m_CustomHintObject = adminPanel.AdvancedModeHintObject;
    }

    protected override void OnHide() {
      PromoManager.m_Instance.ButtonHighlight.SetActive(false);
      PanelManager.m_Instance.GetAdminPanel().ActivatePromoBorder(false);
    }

    public override void OnActive() {
      // If we made it to advanced mode, we should be gone.
      if (PanelManager.m_Instance.AdvancedModeActive()) {
        m_Request = RequestingState.ToHide;
        m_CustomHintObject.Activate(false);
        return;
      }

      // If we're looking at the admin panel, tick down our dismiss timer.
      BasePanel adminPanel = PanelManager.m_Instance.GetAdminPanel();
      if (SketchControlsScript.m_Instance.IsUserLookingAtPanel(adminPanel)) {
        m_ReadDismissTimer -= Time.deltaTime;
        m_CustomHintObject.Activate(true);
      } else {
        // If we're not looking at the admin panel, keep our timer refreshed.
        if (m_ReadDismissTimer > 0.0f) {
          m_ReadDismissTimer = m_TimeBeforeReadDismiss;
        } else {
          // If our timer expired while we were staring, we're done.  The user has had enough
          // time to process the promo, we're going to assume they read it and don't care.
          m_Request = RequestingState.ToHide;
        }
        m_CustomHintObject.Activate(false);
      }
    }

    public override void OnIdle() {
      if (!PanelManager.m_Instance.SketchbookActive() && !App.Instance.IsLoading() &&
          m_TimeBeforeDisplay > 0) {
        m_TimeBeforeDisplay -= Time.deltaTime;
        if (m_TimeBeforeDisplay <= 0) {
          m_ReadDismissTimer = m_TimeBeforeReadDismiss;
          m_Request = RequestingState.ToDisplay;
        }
      }
    }
  }
} // namespace TiltBrush
