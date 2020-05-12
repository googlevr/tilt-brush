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

public class DeleteCameraPathPopUpWindow : PopUpWindow {
  override public void Init(GameObject parent, string text) {
    // Intercept the text and tack on the index of the current path.
    int? index = WidgetManager.m_Instance.GetIndexOfCameraPath(
        WidgetManager.m_Instance.GetCurrentCameraPath().WidgetScript);
    if (index != null) {
      text += " " + (index + 1).ToString() + "?";
    }
    base.Init(parent, text);
  }

  override protected void UpdateVisuals() {
    base.UpdateVisuals();

    CameraPathWidget cpw = WidgetManager.m_Instance.GetCurrentCameraPath().WidgetScript;
    if (cpw != null) {
      cpw.HighlightEntirePath();
    }
  }
}
}  // namespace TiltBrush
