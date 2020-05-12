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

public class ControllerBehaviorWand : BaseControllerBehavior {
  private bool m_QuickLoadHintRequested;
  // Fist time the promo appears for this sketch loading
  // (Promo is hidden if user presses the grip button, and we don't want to play the audio again
  // when the promo comes back)
  private bool m_QuickLoadHintFirstAppearance;

  void Start() {
    m_QuickLoadHintRequested = false;
  }

  public void EnableQuickLoadHintObject(bool bEnable) {
    m_QuickLoadHintFirstAppearance = true;
    m_QuickLoadHintRequested = bEnable;
  }

  public void EnableSketchbookPanelActivateHint(bool bEnable) {
    ControllerGeometry.MenuPanelHintObject.Activate(bEnable);
  }

  override protected void OnUpdate() {
    //update quick load hint
    var quickLoadHintObject = ControllerGeometry.QuickLoadHintObject;
    if (m_QuickLoadHintRequested) {
      //if we're supposed to show, make sure we can and update accordingly
      bool bCanShow = !SketchControlsScript.m_Instance.IsUserInteractingWithAnyWidget() &&
          !SketchControlsScript.m_Instance.IsUserGrabbingWorld() &&
          (VideoRecorderUtils.ActiveVideoRecording == null);
      if (bCanShow && !quickLoadHintObject.gameObject.activeSelf 
          && m_QuickLoadHintFirstAppearance) {
        AudioManager.m_Instance.PlayHintAnimateSound(quickLoadHintObject.transform.position);
      }
      quickLoadHintObject.Activate(bCanShow);
      m_QuickLoadHintFirstAppearance = false;
    } else if (quickLoadHintObject.gameObject.activeSelf) {
      //if we're not supposed to show, turn us off if we're on
      quickLoadHintObject.gameObject.SetActive(false);
    }
  }

  override public void ActivateHint(bool bActivate) {
    ControllerGeometry.SwipeHintObject.Activate(bActivate);
  }

}
}  // namespace TiltBrush
