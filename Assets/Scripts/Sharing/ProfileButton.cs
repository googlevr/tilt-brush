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

class ProfileButton : OptionButton {
  [SerializeField] private Texture2D m_AnonymousTexture;

  [SerializeField] private string m_LoggedInText;
  [SerializeField] private string m_LogInText;

  // Source of identity - change when we merge VrAssetService & YouTube
  private OAuth2Identity m_Identity;

  private const string kIconSizeSuffix = "?sz=128";

  override protected void Awake() {
    base.Awake();
    m_Identity = App.GoogleIdentity;
    OAuth2Identity.ProfileUpdated += OnProfileUpdated;
    RefreshButtons();
  }

  override protected void Start() {
    base.Start();
    BasePanel parentPanel = m_Manager.GetComponent<BasePanel>();
    if (parentPanel is AdminPanel) {
      SetMaterialFloat("_Grayscale", 1);
    }
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    OAuth2Identity.ProfileUpdated -= OnProfileUpdated;
  }

  void RefreshButtons() { 
    OAuth2Identity.UserInfo userInfo = m_Identity.Profile;
    if (userInfo != null) {
      SetDescriptionText(m_LoggedInText);
      if (userInfo.icon != null) {
        SetButtonTexture(userInfo.icon);
      }
    } else {
      // Go back to anonymous
      SetDescriptionText(m_LogInText);
      SetButtonTexture(m_AnonymousTexture);
    }
  }

  private void OnProfileUpdated(OAuth2Identity _) {
    RefreshButtons();
  }

  override public void UpdateButtonState(bool bActivateInputValid) {
    base.UpdateButtonState(bActivateInputValid);
    BasePanel parentPanel = m_Manager.GetComponent<BasePanel>();
    if (parentPanel is AdminPanel && m_CurrentButtonState == ButtonState.Hover) {
      SetMaterialFloat("_Grayscale", 0);
    }
  }

  override public void ResetState() {
    base.ResetState();
    BasePanel parentPanel = m_Manager.GetComponent<BasePanel>();
    if (parentPanel is AdminPanel && m_CurrentButtonState == ButtonState.Untouched) {
      SetMaterialFloat("_Grayscale", 1);
    }
  }
}
}  // namespace TiltBrush
