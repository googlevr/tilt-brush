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

public class SketchesPanel : BasePanel {
  // Two versions of the save button
  [SerializeField] GameObject m_NewSaveButton;
  [SerializeField] GameObject m_SaveOptionsButton;

  [SerializeField] OptionButton m_ShareButton;
  [SerializeField] Material m_ShareButtonStandard;
  [SerializeField] Material m_ShareButtonNotify;
  [SerializeField] string m_ShareButtonLoggedOutExtraText;
  [SerializeField] Renderer m_ProfileButtonRenderer;

  private bool m_LoggedIn;
  private float m_LastUploadProgress;

  public override void InitPanel() {
    base.InitPanel();

    m_LastUploadProgress = -1.0f;
    m_LoggedIn = App.GoogleIdentity.LoggedIn;
    RefreshLoginButtonText(m_LoggedIn);
  }

  void Update() {
    BaseUpdate();

    // Update save buttons availability.
    bool alreadySaved = SaveLoadScript.m_Instance.SceneFile.Valid &&
                        SaveLoadScript.m_Instance.CanOverwriteSource;
    m_NewSaveButton.SetActive(!alreadySaved);
    m_SaveOptionsButton.SetActive(alreadySaved);

    // Update share button's text.
    bool loggedIn = App.GoogleIdentity.LoggedIn;
    if (loggedIn != m_LoggedIn) {
      RefreshLoginButtonText(loggedIn);
    }

    // Update share button's availability.
    // This is a special case.  Normally, OptionButton.m_AllowUnavailable controls the availability
    // of buttons, but we want to run custom logic here to allow it to be available when the
    // sketch can't be shared, but we've got an upload in progress.
    bool bWasAvailable = m_ShareButton.IsAvailable();
    bool bAvailable = SketchControlsScript.m_Instance.IsCommandAvailable(
        SketchControlsScript.GlobalCommands.UploadToGenericCloud) || m_LastUploadProgress > 0.0f;
    // Manual in-app uploading is disabled in demo mode. The assistant can manually initiate
    // a demo upload by tapping U on the keyboard, or Tilt Brush can be configured to automatically
    // upload the sketch when the demo countdown is down.
    if (DemoManager.m_Instance.DemoModeEnabled) {
      bAvailable = false;
    }
    if (bWasAvailable != bAvailable) {
      m_ShareButton.SetButtonAvailable(bAvailable);
    }

    // Keep share button fresh.
    float uploadProgress = VrAssetService.m_Instance.UploadProgress;
    // In demo mode, since the button is always disabled, we don't want any distracting
    // flashing or color changes when the automatic uploads occur.
    if (DemoManager.m_Instance.DemoModeEnabled) {
      uploadProgress = 0;
    }
    if (m_LastUploadProgress != uploadProgress) {
      SetShareNotification(uploadProgress >= 1);
      m_ShareButton.GetComponent<Renderer>().material.SetFloat("_Ratio", uploadProgress);
      m_LastUploadProgress = uploadProgress;
    }
  }

  void SetShareNotification(bool notify) {
    var shareButtonRenderer = m_ShareButton.GetComponent<Renderer>();
    shareButtonRenderer.material =
      notify ? m_ShareButtonNotify : m_ShareButtonStandard;
    m_ShareButton.SetColor(GetGazeColor());
    if (shareButtonRenderer.material.HasProperty("_PulseSpeed")) {
      shareButtonRenderer.material.SetFloat("_PulseSpeed", m_GazeActive ? 2 : 1);
    }
  }

  void RefreshLoginButtonText(bool loggedIn) {
    string text = loggedIn ? "" : m_ShareButtonLoggedOutExtraText;
    m_ShareButton.SetExtraDescriptionText(text);
    m_LoggedIn = loggedIn;
  }

  // Make user profile photo grayscale when the panel is not in focus
  override protected void OnUpdateActive() {
    if (!m_GazeActive) {
      m_ProfileButtonRenderer.material.SetFloat("_Grayscale", 1);
    } else if (m_CurrentState == PanelState.Available) {
      m_ProfileButtonRenderer.material.SetFloat("_Grayscale", 0);
    }
  }

  public override void PanelGazeActive(bool bActive) {
    base.PanelGazeActive(bActive);
    var shareMat = m_ShareButton.GetComponent<Renderer>().material;
    if (shareMat.HasProperty("_PulseSpeed")) {
      shareMat.SetFloat("_PulseSpeed", m_GazeActive ? 2 : 1);
    }
  }
}
}  // namespace TiltBrush
