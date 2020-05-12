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
public class LongPressToolButton : LongPressButton {
  [SerializeField] private BaseTool.ToolType m_Tool;
  [SerializeField] private bool m_EatGazeInputOnPress = false;

  override protected void Awake() {
    base.Awake();
    App.Switchboard.ToolChanged += UpdateVisuals;
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    App.Switchboard.ToolChanged -= UpdateVisuals;
  }

  override public void UpdateVisuals() {
    base.UpdateVisuals();
    // Toggle buttons poll for status.
    if (m_ToggleButton) {
      bool bWasToggleActive = m_ToggleActive;
      m_ToggleActive = SketchSurfacePanel.m_Instance.GetCurrentToolType() == m_Tool;
      if (bWasToggleActive != m_ToggleActive) {
        SetButtonActivated(m_ToggleActive);
      }
    }
  }

  override protected void OnButtonPressed() {
    if (m_ToggleActive) {
      SketchSurfacePanel.m_Instance.DisableSpecificTool(m_Tool);
    } else {
      if (m_EatGazeInputOnPress) {
        SketchControlsScript.m_Instance.EatGazeObjectInput();
      }
      SketchSurfacePanel.m_Instance.RequestHideActiveTool(true);
      SketchSurfacePanel.m_Instance.EnableSpecificTool(m_Tool);
    }
  }
}
}  // namespace TiltBrush
