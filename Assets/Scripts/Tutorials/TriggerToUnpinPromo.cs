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

/// Promo: Show the user how to unpin a widget.
/// Completion: N/A (Always show the user how to upin a widget so that they can remove it.)
/// Conditions: User is grabbing a pinned widget.
public class TriggerToUnpinPromo : BasePromo {
  public override string PrefsKey { get { return PromoManager.kPromoPrefix + "TriggerToUnpin"; } }

  public TriggerToUnpinPromo() : base(PromoType.TriggerToUnpin) {}

  protected override void OnDisplay() {
    InputManager.ControllerName controller = SketchControlsScript.m_Instance.OneHandGrabController;
    if (controller != InputManager.ControllerName.None) {
      m_HintObject = InputManager.GetControllerGeometry(controller).UnpinHint;
      m_HintObject.Activate(true);
    } else {
      // If the user is no longer grabbing a pinned widget, there is no reason for this promo to
      // hog the active slot.
      m_Request = RequestingState.ToHide;
    }
  }

  protected override void OnHide() {
    m_HintObject = null;
  }

  public override void OnActive() {
    if (!SketchControlsScript.m_Instance.IsCurrentGrabWidgetPinned()) {
      m_Request = RequestingState.ToHide;
    }
  }

  public override void OnIdle() {
    if (SketchControlsScript.m_Instance.IsCurrentGrabWidgetPinned()) {
      m_Request = RequestingState.ToDisplay;
    }
  }
}
} // namespace TiltBrush
