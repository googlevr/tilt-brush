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

public class MemoryWarningMessage : UIComponent {
  [SerializeField] private GameObject m_ExceededMessage;
  [SerializeField] private GameObject m_WarningMessage;

  override protected void Awake() {
    base.Awake();
    App.Switchboard.MemoryExceededChanged += OnMemoryStateChanged;
    App.Switchboard.MemoryWarningAcceptedChanged += OnMemoryStateChanged;
    OnMemoryStateChanged();
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    App.Switchboard.MemoryExceededChanged -= OnMemoryStateChanged;
    App.Switchboard.MemoryWarningAcceptedChanged -= OnMemoryStateChanged;
  }

  void OnMemoryStateChanged() {
    bool memExceeded = (SketchMemoryScript.m_Instance != null) ?
        SketchMemoryScript.m_Instance.MemoryExceeded : false;
    m_ExceededMessage.SetActive(memExceeded);
    m_WarningMessage.SetActive(!memExceeded);
  }
}

} // namespace TiltBrush
