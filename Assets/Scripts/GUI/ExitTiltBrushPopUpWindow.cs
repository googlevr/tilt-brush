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

public class ExitTiltBrushPopUpWindow : PopUpWindow {
  [SerializeField] protected TextMesh m_Title;
  [SerializeField] protected SaveAndConfirmButton m_SaveButton;

  [SerializeField] private string m_Title_Saving;
  [SerializeField] private string m_Title_NoSaving;
  [SerializeField] private string m_SaveButtonDescription_Saving;
  [SerializeField] private string m_SaveButtonDescription_NoSaving;

  override public void Init(GameObject rParent, string sText) {
    base.Init(rParent, sText);
    bool shouldSave = SketchControlsScript.m_Instance.IsCommandAvailable(
        SketchControlsScript.GlobalCommands.SaveOnLocalChanges);
    m_Title.text = shouldSave ? m_Title_Saving : m_Title_NoSaving;
    m_SaveButton.SetDescriptionText(shouldSave ? m_SaveButtonDescription_Saving :
        m_SaveButtonDescription_NoSaving);
  }
}
}  // namespace TiltBrush
