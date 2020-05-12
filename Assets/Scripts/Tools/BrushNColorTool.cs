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

// These tools are used for monoscopic mode only
public class BrushNColorTool : BaseSelectionTool {
  public TextMesh m_SelectionTextExtra;
  public Color m_SelectionTextExtraColor;

  override public void Init() {
    base.Init();
    m_SelectionTextExtra.GetComponent<Renderer>().material.color = m_SelectionTextExtraColor;
  }

  override public void SetExtraText(string sExtra) {
    m_SelectionTextExtra.text = sExtra;
  }

  override public void UpdateTool() {
    base.UpdateTool();

    // If our info just became valid, update our selection text
    if (!m_SelectionInfoQueryWasComplete && m_SelectionInfoQueryComplete) {
      string description = (m_SelectionBrush != null) ? m_SelectionBrush.m_Description : "";
      SetExtraText(description);
      SetColor(m_SelectionColor);
    }

    // Inputs
    if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate)) {
      if (m_SelectionInfoValid) {
        PointerManager.m_Instance.SetBrushForAllPointers(m_SelectionBrush);
        PanelManager.m_Instance.SetCurrentColorOnAllColorPickers(m_SelectionColor);
      }
      m_RequestExit = true;
    }
  }
}

}  // namespace TiltBrush
