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

/// Promo: Informs user that they are capturing an icon for the new sketch they are saving.
/// Completion: N/A (promo should always show when user is saving a new sketch)
/// Conditions: SaveIconTool is active.
public class SaveIconPromo : BasePromo {
  private bool m_SwappingControls;

  public override string PrefsKey { get { return PromoManager.kPromoPrefix + "SaveIcon"; } }

  public SaveIconPromo() : base(PromoType.SaveIcon) {
    m_HintObject = InputManager.Brush.Geometry.SaveIconHint;
  }

  public override void OnIdle() {
    if (SketchSurfacePanel.m_Instance.ActiveToolType == BaseTool.ToolType.SaveIconTool) {
      m_Request = RequestingState.ToDisplay;
    }
  }

  public override void OnActive() {
    if (SketchSurfacePanel.m_Instance.ActiveToolType != BaseTool.ToolType.SaveIconTool) {
      m_Request = RequestingState.ToHide;
    }
    if (m_HintObject != InputManager.Brush.Geometry.SaveIconHint) {
      m_SwappingControls = true;
      m_Request = RequestingState.ToHide;
    }
  }

  protected override void OnHide() {
    if (m_SwappingControls) {
      m_SwappingControls = false;
      m_HintObject = InputManager.Brush.Geometry.SaveIconHint;
      m_Request = RequestingState.ToDisplay;
    }
  }
}
} // namespace TiltBrush