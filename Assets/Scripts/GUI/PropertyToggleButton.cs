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
  /// Allows the button to be connected to a bool property on a component, and automatically
  /// reflect its value as well as toggling its value when the button in pressed. An option
  /// to inverse sign on the highlight is included.
  public class PropertyToggleButton : BaseButton {

    [SerializeField] private SerializedPropertyReferenceBool m_Property;
    [SerializeField] private bool m_InverseHighlight;

    protected override void Start() {
      base.Start();
      HighlightButton();
    }

    protected override void OnButtonPressed() {
      if (m_Property.HasValue) {
        m_Property.Value = !m_Property.Value;
        HighlightButton();
      }
    }

    override public void UpdateVisuals() {
      base.UpdateVisuals();
      HighlightButton();
    }

    protected void HighlightButton() {
      if (m_Property.HasValue) {
        bool value = m_Property.Value ^ m_InverseHighlight;
        SetButtonActivated(value);
      }
    }
  }
} // namespace TiltBrush
