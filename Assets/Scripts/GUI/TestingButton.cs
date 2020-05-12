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

public class TestingButton : BaseButton {
  [SerializeField] string m_ResultText;

  public enum Type {
    Back,
    Next,
    Result
  }

  public Type m_Type;

  public string ResultText { get { return m_ResultText; } }

  override protected void OnButtonPressed() {
    TestingPanel testingParent = m_Manager.GetComponent<TestingPanel>();
    if (testingParent != null) {
      testingParent.OnButtonPressed(m_Type, m_ResultText);
    }
  }

  public void ToggleActive(bool active) {
    SetButtonActivated(active);
  }
}
}  // namespace TiltBrush
