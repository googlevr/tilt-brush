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

/// Promo: Show the user how to upload sketches to Poly.
/// Completion: User opens the upload popup whether or not they choose to actually share the
///             sketch, or uses the save and share button.
/// Conditions: User has saved a sketch that can be uploaded to Poly (no local media
///             library widgets, not based off an RR sketch) within this session. User has not
///             done any other actions (use panel, paint, etc.) after the promo is displayed.
public class ShareSketchPromo : BasePromo {
  private string m_LastSaveFileHumanName;
  private float m_TimeBeforeDisplay = 1.2f;
  private bool m_ControllerSwapped;

  public ShareSketchPromo() : base(PromoType.ShareSketch) {
    m_LastSaveFileHumanName = (SaveLoadScript.m_Instance == null) ? "" :
        SaveLoadScript.m_Instance.GetLastFileHumanName();
  }

  public override string PrefsKey { get { return PromoManager.kPromoPrefix + "ShareSketch"; } }

  protected override void OnDisplay() {
    PanelManager.m_Instance.GetAdminPanel().ActivatePromoBorder(true);
    InputManager.m_Instance.TriggerHapticsPulse(InputManager.ControllerName.Brush, 4, 0.15f, 0.1f);
    PromoManager.m_Instance.HintLine.gameObject.SetActive(true);

    // Parent and position the button highlight to our target button.
    PromoManager.m_Instance.ButtonHighlight.transform.parent =
        (PanelManager.m_Instance.GetAdminPanel() as AdminPanel).ShareButton;
    PromoManager.m_Instance.ResetButtonHighlightXf();
    PromoManager.m_Instance.ButtonHighlight.SetActive(true);

    if (m_HintObject == null) {
      m_HintObject = InputManager.Brush.Geometry.ShareSketchHintObject;
      m_HintObject.Activate(true);
    }

    AudioManager.m_Instance.PlayHintAnimateSound(m_HintObject.transform.position);
  }

  protected override void OnHide() {
    PromoManager.m_Instance.HintLine.Reset();
    PromoManager.m_Instance.HintLine.gameObject.SetActive(false);
    PromoManager.m_Instance.ButtonHighlight.SetActive(false);
    PanelManager.m_Instance.GetAdminPanel().ActivatePromoBorder(false);
    if (m_ControllerSwapped) { m_HintObject = null; }
  }

  public override void OnActive() {
    bool lineVisible = !SketchControlsScript.m_Instance.IsUserInteractingWithUI();
    if (PromoManager.m_Instance.HintLine.gameObject.activeSelf != lineVisible) {
      m_HintObject.Activate(lineVisible);
      PromoManager.m_Instance.HintLine.gameObject.SetActive(lineVisible);
    }
    if (lineVisible) {
      TrTransform xfBrush = TrTransform.FromTransform(
          InputManager.m_Instance.GetBrushControllerAttachPoint());
      TrTransform xfTarget = new TrTransform();
      xfTarget.translation = PanelManager.m_Instance.GetAdminPanel().transform.position;
      xfBrush.rotation = Quaternion.LookRotation(
          (xfTarget.translation - xfBrush.translation).normalized, Vector3.up);
      xfTarget.rotation = xfBrush.rotation;
      PromoManager.m_Instance.HintLine.UpdateLine(xfBrush, xfTarget);
    }

    bool panelClick = SketchControlsScript.m_Instance.IsUserInteractingWithUI() &&
      InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate);
    if (PromoManager.m_Instance.ShouldPausePromos &&
        (!SketchControlsScript.m_Instance.IsUserInteractingWithUI() || panelClick)) {
      m_ControllerSwapped = InputManager.m_Instance.ControllersAreSwapping();
      m_Request = RequestingState.ToHide;
    }
  }

  public override void OnIdle() {
    if (m_ControllerSwapped) {
      m_ControllerSwapped = false;
      m_Request = RequestingState.ToDisplay;
    }
    if (!PanelManager.m_Instance.SketchbookActive()) {
      if (m_TimeBeforeDisplay > 0) {
        // If we're counting down to show and the user does something that makes this
        // promo not make sense, bail out.
        string filename = (SaveLoadScript.m_Instance == null) ? "" :
            SaveLoadScript.m_Instance.GetLastFileHumanName();
        if (App.Instance.IsLoading() || !m_LastSaveFileHumanName.Equals(filename)) {
          m_TimeBeforeDisplay = -1.0f;
          m_Request = RequestingState.ToHide;
          return;
        }

        m_TimeBeforeDisplay -= Time.deltaTime;
        if (m_TimeBeforeDisplay <= 0) {
          m_Request = RequestingState.ToDisplay;
        }
      }
    }
  }
}
} // namespace TiltBrush