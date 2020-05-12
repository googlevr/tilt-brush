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

public class UploadSketchButton : OptionButton {
  override protected void OnButtonPressed() {
    // Our manager should be the UploadPopUpWindow.  If it's not, no biggie, just warn.
    UploadPopUpWindow popup = (m_Manager != null) ?
        m_Manager.GetComponent<UploadPopUpWindow>() : null;
    if (popup != null) {
      if (m_Command == SketchControlsScript.GlobalCommands.LoginToGenericCloud) {
        popup.UserPressedLoginButton(m_CommandParam);
      } else if (m_Command == SketchControlsScript.GlobalCommands.UploadToGenericCloud) {
        // We can't tell if it's safe to upload yet, so pass on responsibility for calling the base
        // class (which would start the actual upload).
        popup.UserPressedUploadButton((Cloud) m_CommandParam, base.OnButtonPressed);
        return;
      } else {
        // This button should be either set to login or upload. If not, just warn.
        Debug.LogWarning("UploadSketchButton command should be either " +
                         "LoginToGenericCloud or UploadToGenericCloud.");
      }
    } else {
      Debug.LogWarning("UploadSketchButton on a panel or popup that isn't UploadPopUpWindow.");
    }

    base.OnButtonPressed();
  }
}
}  // namespace TiltBrush
