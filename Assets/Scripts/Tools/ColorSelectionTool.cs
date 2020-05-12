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

// These tools are used for monoscopic mode only
public class ColorSelectionTool : BaseSelectionTool {
  override public void UpdateTool() {
    base.UpdateTool();

    //if our info just became valid, update our selection color
    if (!m_SelectionInfoQueryWasComplete && m_SelectionInfoQueryComplete) {
      SetColor(m_SelectionColor);
    }

    //inputs
    if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate)) {
      if (m_SelectionInfoValid) {
        PanelManager.m_Instance.SetCurrentColorOnAllColorPickers(m_SelectionColor);
      }
      m_RequestExit = true;
    }
  }
}
}  // namespace TiltBrush
