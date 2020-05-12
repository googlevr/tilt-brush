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

/// Promo: How to duplicate a selection.
/// Completion: User duplicates or stamps a selection.
/// Conditions: Selection tool is active and hovering over the selection, or the selection is
///             being grabbed and can be stamped.
public class DuplicatePromo : BasePromo {
  public override string PrefsKey { get { return PromoManager.kPromoPrefix + "Duplicate"; } }

  public DuplicatePromo() : base(PromoType.Duplicate) { }

  protected override void OnDisplay() {
    var controller = InputManager.ControllerName.Brush;
    if (SketchControlsScript.m_Instance.OneHandGrabController != InputManager.ControllerName.None) {
      controller = SketchControlsScript.m_Instance.OneHandGrabController;
    }
    m_HintObject = InputManager.GetControllerGeometry(controller).DuplicateHint;
    if (m_HintObject) { m_HintObject.Activate(true); }
    base.OnDisplay();
  }

  protected override void OnHide() {
    base.OnHide();
    m_HintObject = null;
  }

  public override void OnActive() {
    if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.DuplicateSelection)) {
      PromoManager.m_Instance.RecordCompletion(this.PromoType);
      m_Request = RequestingState.ToHide;
    }
    if (!SketchControlsScript.m_Instance.IsUserInteractingWithSelectionWidget() &&
        !SketchControlsScript.m_Instance.IsUserIntersectingWithSelectionWidget()) {
      // Deactivate the button mesh manually, otherwise it lags behind when being deactivated
      // using the normal tooltip animation
      m_Request = RequestingState.ToHide;
    }
  }

  public override void OnIdle() {
    if (SketchControlsScript.m_Instance.IsUserInteractingWithSelectionWidget() ||
        SketchControlsScript.m_Instance.IsUsersBrushIntersectingWithSelectionWidget()) {
      m_Request = RequestingState.ToDisplay;
    }
  }
}
} // namespace TiltBrush
