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

/// Promo: Show first time users how to interact with panels if they are launched into the second
///        intro sketch.
/// Completion: N/A (promo is permanently hidden when user exits the intro panel)
/// Conditions: User is looking at the intro-mode menu panel and not pointing at it.
public class InteractIntroPanelPromo : BasePromo {
  private bool m_SwappingControls;

  public override string PrefsKey { get { return PromoManager.kPromoPrefix + "InteractIntroPanel"; } }

  public InteractIntroPanelPromo() : base(PromoType.InteractIntroPanel) {
    m_HintObject = InputManager.Brush.Geometry.PointAtPanelsHintObject;
    m_Request = RequestingState.ToDisplay;
  }

  protected override void OnDisplay() {
    PromoManager.m_Instance.HintLine.gameObject.SetActive(true);
    PromoManager.m_Instance.HintLine.GlobalMode = true;
  }

  public override void OnActive() {
    if (SketchControlsScript.m_Instance.IsUserInteractingWithUI() ||
        !InputManager.Brush.IsTrackedObjectValid || !InputManager.Wand.IsTrackedObjectValid) {
      m_Request = RequestingState.ToHide;
    }
    // If promo needs to change hands when it is already visible
    if (m_HintObject != InputManager.Brush.Geometry.PointAtPanelsHintObject) {
      m_SwappingControls = true;
      m_Request = RequestingState.ToHide;
    }
    if (!PanelManager.m_Instance.IntroSketchbookMode) {
      m_Request = RequestingState.ToHide;
    }

    AlignHintLine();
  }

  void AlignHintLine() {
    TrTransform xfBrush = TrTransform.FromTransform(
        InputManager.m_Instance.GetBrushControllerAttachPoint());
    TrTransform xfTarget = new TrTransform();
    var sketchbookPanel = PanelManager.m_Instance.GetSketchBookPanel();
    if (sketchbookPanel != null) {
      xfTarget.translation = TrTransform.FromTransform(
          sketchbookPanel.transform).translation;
      xfBrush.rotation = Quaternion.LookRotation(
          (xfTarget.translation - xfBrush.translation).normalized, Vector3.up);
      xfTarget.rotation = xfBrush.rotation;
      PromoManager.m_Instance.HintLine.UpdateLine(xfBrush, xfTarget);
    }
  }

  protected override void OnHide() {
    PromoManager.m_Instance.HintLine.Reset();
    PromoManager.m_Instance.HintLine.gameObject.SetActive(false);
    if (m_SwappingControls) {
      m_SwappingControls = false;
      m_HintObject = InputManager.Brush.Geometry.PointAtPanelsHintObject;
      m_Request = RequestingState.ToDisplay;
    }
  }

  public override void OnIdle() {
    if (PanelManager.m_Instance.IntroSketchbookMode &&
        !SketchControlsScript.m_Instance.IsUserInteractingWithUI() &&
        InputManager.Brush.IsTrackedObjectValid && InputManager.Wand.IsTrackedObjectValid) {
      m_Request = RequestingState.ToDisplay;
    }
  }
}
} // namespace TiltBrush