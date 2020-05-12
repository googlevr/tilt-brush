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

  public class InspectSketchButton : BaseButton {
    private int m_SketchIndex;
    private SketchSetType m_SketchSetType;

    public void SetSketchDetails(int index, SketchSetType type) {
      m_SketchIndex = index;
      m_SketchSetType = type;
    }

    override protected void OnButtonPressed() {
      BasePanel panel = m_Manager.GetPanelForPopUps();
      panel.CreatePopUp(SketchControlsScript.GlobalCommands.SketchbookMenu,
          m_SketchIndex, (int)m_SketchSetType);

      // Position popup next to button.
      panel.PositionPopUp(transform.position);
    }
  }

}  // namespace TiltBrush
