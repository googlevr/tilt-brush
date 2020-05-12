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

namespace TiltBrush {

/// Promo: Show the user how to switch the selection tool to deselect mode.
/// Completion: User switches selection tool into deselct mode.
/// Conditions: Selection tool is active.
public class SelectionPromo : BasePromo {
  private bool m_ControllerSwapped;

  public override string PrefsKey { get { return PromoManager.kPromoPrefix + "Selection"; } }

  public SelectionPromo() : base(PromoType.Selection) {
    m_Request = RequestingState.ToDisplay;
  }

  protected override void OnDisplay() {
    InputManager.Brush.Geometry.SelectionHintButton.SetActive(true);
    if (m_HintObject == null) {
      m_HintObject = InputManager.Brush.Geometry.SelectionHint;
      m_HintObject.Activate(true);
    }
  }

  protected override void OnHide() {
    InputManager.Brush.Geometry.SelectionHintButton.SetActive(false);
    if (m_ControllerSwapped) { m_HintObject = null; }
  }

  public override void OnIdle() {
    if (m_ControllerSwapped) {
      m_ControllerSwapped = false;
      m_Request = RequestingState.ToDisplay;
    }
  }

  public override void OnActive() {
    if (InputManager.m_Instance.GetCommandDown(
          InputManager.SketchCommands.ToggleSelection)) {
      PromoManager.m_Instance.RecordCompletion(this.PromoType);
      m_Request = RequestingState.ToHide;
    }

    if (PromoManager.m_Instance.ShouldPausePromos ||
        SketchControlsScript.m_Instance.IsUserIntersectingWithSelectionWidget() ||
        !SelectionManager.m_Instance.HasSelection) {
      // Deactivate the button mesh manually, otherwise it lags behind when being deactivated
      // using the normal tooltip animation
      m_ControllerSwapped = InputManager.m_Instance.ControllersAreSwapping();
      m_Request = RequestingState.ToHide;
    }
  }
}
} // namespace TiltBrush
