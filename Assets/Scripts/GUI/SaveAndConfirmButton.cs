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

public class SaveAndConfirmButton : ConfirmationButton {
  override protected void OnButtonPressed() {
    // SaveOverwriteOrNewIfNotAllowed will do the right thing with regards to save/save new,
    // but in the event there's nothing to save, we don't want to bother.
    bool shouldSave = SketchControlsScript.m_Instance.IsCommandAvailable(
        SketchControlsScript.GlobalCommands.SaveOnLocalChanges);
    if (shouldSave) {
      SaveLoadScript.m_Instance.SuppressSaveNotifcation = true;
      SaveLoadScript.m_Instance.SaveOverwriteOrNewIfNotAllowed();
    }
    base.OnButtonPressed();
  }
}
}  // namespace TiltBrush
