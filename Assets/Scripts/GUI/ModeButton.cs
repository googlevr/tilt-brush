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
public abstract class ModeButton : BaseButton {
  override public void UpdateVisuals() {
    base.UpdateVisuals();
    // Toggle buttons poll for status.
    if (m_ToggleButton) {
      bool bWasToggleActive = m_ToggleActive;

      ModalPanel modalParent = m_Manager.GetComponent<ModalPanel>();
      if (modalParent) {
        m_ToggleActive = modalParent.IsInButtonMode(this);
      }

      if (bWasToggleActive != m_ToggleActive) {
        SetButtonActivated(m_ToggleActive);
      }
    }
  }
}
} // namespace TiltBrush
