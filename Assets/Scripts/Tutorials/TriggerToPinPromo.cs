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

/// Promo: Show the user how to pin a widget.
/// Completion: User pins a widget.
/// Conditions: User is grabbing a pinnable widget.
public class TriggerToPinPromo : BasePromo {
  private InputManager.ControllerName m_Controller;

  public override string PrefsKey { get { return PromoManager.kPromoPrefix + "TriggerToPin"; } }

  public TriggerToPinPromo() : base(PromoType.TriggerToPin) { }

  public override void OnActive() {
    if (!SketchControlsScript.m_Instance.IsUserInteractingWithAnyWidget() ||
        SketchControlsScript.m_Instance.OneHandGrabController != m_Controller ||
        SketchControlsScript.m_Instance.IsCurrentGrabWidgetPinned()) {
      m_Request = RequestingState.ToHide;
    }
  }

  protected override void OnDisplay() {
    m_Controller = SketchControlsScript.m_Instance.OneHandGrabController;
    m_HintObject = InputManager.GetControllerGeometry(m_Controller).PinHint;
    m_HintObject.Activate(true);
  }

  protected override void OnHide() {
    m_HintObject = null;
  }

  public override void OnIdle() {
    if (SketchControlsScript.m_Instance.IsUserInteractingWithAnyWidget() &&
        SketchControlsScript.m_Instance.CanCurrentGrabWidgetBePinned()) {
      m_Request = RequestingState.ToDisplay;
    }
  }
}
} // namespace TiltBrush
