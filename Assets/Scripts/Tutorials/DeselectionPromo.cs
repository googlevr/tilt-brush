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
public class DeselectionPromo : BasePromo {
  private bool m_ControllerSwapped;

  public override string PrefsKey { get { return PromoManager.kPromoPrefix + "Deselection"; } }

  public DeselectionPromo() : base(PromoType.Deselection) {
    m_Request = RequestingState.ToDisplay;
  }

  protected override void OnDisplay() {
    InputManager.Brush.Geometry.DeselectionHintButton.SetActive(true);
    if (m_HintObject == null) {
      m_HintObject = InputManager.Brush.Geometry.DeselectionHint;
      m_HintObject.Activate(true);
    }
  }

  protected override void OnHide() {
    InputManager.Brush.Geometry.DeselectionHintButton.SetActive(false);
    if (m_ControllerSwapped) { m_HintObject = null; }
  }

  public override void OnIdle() {
    if (m_ControllerSwapped) {
      m_ControllerSwapped = false;
      m_Request = RequestingState.ToDisplay;
    }
  }

  public override void OnActive() {
     if (!SelectionManager.m_Instance.ShouldRemoveFromSelection()) {
      // Deactivate the button mesh manually, otherwise it lags behind when being deactivated
      // using the normal tooltip animation
      InputManager.Brush.Geometry.DeselectionHintButton.SetActive(false);
      m_ControllerSwapped = InputManager.m_Instance.ControllersAreSwapping();
      m_Request = RequestingState.ToHide;
    }
  }
}
} // namespace TiltBrush
