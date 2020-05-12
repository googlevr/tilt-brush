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

public class ProfilePopUpWindow : OptionsPopUpWindow {
  public enum Mode {
    Accounts,
    TakeOffHeadset,
    GoogleHelp,
    DriveHelp,
    SketchfabHelp,
    ConfirmLogin,
  }

  [SerializeField] private GameObject m_GoogleSignedInElements;
  [SerializeField] private GameObject m_GoogleSignedOutElements;
  [SerializeField] private GameObject m_GoogleConfirmSignOutElements;
  [SerializeField] private GameObject m_SketchfabSignedInElements;
  [SerializeField] private GameObject m_SketchfabSignedOutElements;
  [SerializeField] private GameObject m_SketchfabConfirmSignOutElements;
  [SerializeField] private Renderer m_GooglePhoto;
  [SerializeField] private Renderer m_SketchfabPhoto;
  [SerializeField] private TMPro.TextMeshPro m_GoogleNameText;
  [SerializeField] private TMPro.TextMeshPro m_SketchfabNameText;
  [SerializeField] private Texture2D m_GenericPhoto;

  [SerializeField] private GameObject m_Accounts;
  [SerializeField] private GameObject m_TakeOffHeadset;
  [SerializeField] private GameObject m_GoogleInfoElements;
  [SerializeField] private GameObject m_DriveInfoElements;
  [SerializeField] private GameObject m_SketchfabInfoElements;

  [SerializeField] private GameObject m_DriveSyncEnabledElements;
  [SerializeField] private GameObject m_DriveSyncDisabledElements;
  [SerializeField] private GameObject m_DriveFullElements;

  [SerializeField] private GameObject m_DriveSyncIconEnabled;
  [SerializeField] private GameObject m_DriveSyncIconDisabled;
  [SerializeField] private GameObject m_DriveSyncIconDriveFull;

  [SerializeField] private GameObject m_BackupCompleteElements;
  [SerializeField] private GameObject m_BackingUpElements;
  [SerializeField] private TextMesh m_BackingUpProgress;

  [Header("Mobile State Members")]
  [SerializeField] private GameObject m_ConfirmLoginElements;
  [SerializeField] private SaveAndOptionButton m_SaveAndProceedButton;

  private Mode m_CurrentMode;
  private bool m_DriveSyncing = false;

  private void Start() {
    App.DriveSync.SyncEnabledChanged += RefreshObjects;
  }

  override public void Init(GameObject rParent, string sText) {
    base.Init(rParent, sText);
    OAuth2Identity.ProfileUpdated += OnProfileUpdated;
    RefreshObjects();
    App.DriveAccess.RefreshFreeSpaceAsync().AsAsyncVoid();
  }

  void OnDestroy() {
    OAuth2Identity.ProfileUpdated -= OnProfileUpdated;
    App.DriveSync.SyncEnabledChanged -= RefreshObjects;
  }

  override protected void BaseUpdate() {
    base.BaseUpdate();
    if (App.DriveSync.Syncing != m_DriveSyncing) {
      RefreshObjects();
    }
    RefreshBackupProgressText();
  }

  void RefreshObjects() {
    // Google.
    bool driveFull = App.DriveSync.DriveIsLowOnSpace;
    bool driveSyncEnabled = App.DriveSync.SyncEnabled;
    bool driveSyncing = App.DriveSync.Syncing;

    OAuth2Identity.UserInfo googleInfo = App.GoogleIdentity.Profile;
    bool googleInfoValid = googleInfo != null;
    m_GoogleSignedInElements.SetActive(googleInfoValid);
    m_GoogleSignedOutElements.SetActive(!googleInfoValid);
    m_GoogleConfirmSignOutElements.SetActive(false);
    if (googleInfoValid) {
      m_GoogleNameText.text = googleInfo.name;
      m_GooglePhoto.material.mainTexture = googleInfo.icon;

      m_DriveSyncIconDriveFull.SetActive(driveFull && driveSyncEnabled);
      m_DriveSyncIconEnabled.SetActive(!driveFull && driveSyncEnabled);
      m_DriveSyncIconDisabled.SetActive(!driveSyncEnabled);
    }

    // Sketchfab.
    OAuth2Identity.UserInfo sketchfabInfo = App.SketchfabIdentity.Profile;
    bool sketchfabInfoValid = sketchfabInfo != null;
    m_SketchfabSignedInElements.SetActive(sketchfabInfoValid);
    m_SketchfabSignedOutElements.SetActive(!sketchfabInfoValid);
    m_SketchfabConfirmSignOutElements.SetActive(false);
    if (sketchfabInfoValid) {
      m_SketchfabNameText.text = sketchfabInfo.name;
      m_SketchfabPhoto.material.mainTexture = sketchfabInfo.icon;
    }

    m_DriveFullElements.SetActive(driveFull && driveSyncEnabled);
    m_DriveSyncEnabledElements.SetActive(!driveFull && driveSyncEnabled);
    m_DriveSyncDisabledElements.SetActive(!driveSyncEnabled);
    m_BackupCompleteElements.SetActive(!driveSyncing);
    m_BackingUpElements.SetActive(driveSyncing);
    m_DriveSyncing = driveSyncing;
    RefreshBackupProgressText();
  }

  void RefreshBackupProgressText() {
    if (m_BackingUpElements.activeSelf) {
      m_BackingUpProgress.text = string.Format("Backing Up... {0}",
          Mathf.Clamp(App.DriveSync.Progress, 0.01f, 0.99f).ToString("P0"));
    }
  }

  void UpdateMode(Mode newMode) {
    m_CurrentMode = newMode;
    m_Accounts.SetActive(m_CurrentMode == Mode.Accounts);
    m_TakeOffHeadset.SetActive(m_CurrentMode == Mode.TakeOffHeadset);
    m_GoogleInfoElements.SetActive(m_CurrentMode == Mode.GoogleHelp);
    m_DriveInfoElements.SetActive(m_CurrentMode == Mode.DriveHelp);
    m_SketchfabInfoElements.SetActive(m_CurrentMode == Mode.SketchfabHelp);
    if (m_ConfirmLoginElements != null) {
      m_ConfirmLoginElements.SetActive(m_CurrentMode == Mode.ConfirmLogin);
    }
    // Reset persistent flag when switching modes.
    m_Persistent = false;
  }

  void OnProfileUpdated(OAuth2Identity _) {
    // If we're currently telling the user to take of the headset to signin,
    // and they've done so correctly, switch back to the accounts view.
    if (m_CurrentMode == Mode.TakeOffHeadset) {
      UpdateMode(Mode.Accounts);
    }
    RefreshObjects();
  }

  // This function serves as a callback from ProfilePopUpButtons that want to
  // change the mode of the popup on click.
  public void OnProfilePopUpButtonPressed(ProfilePopUpButton button) {
    switch(button.m_Command) {
    // Identifier for signaling we understand the info message.
    case SketchControlsScript.GlobalCommands.Null:
    case SketchControlsScript.GlobalCommands.GoogleDriveSync:
      UpdateMode(Mode.Accounts);
      RefreshObjects();
      break;
    case SketchControlsScript.GlobalCommands.LoginToGenericCloud:
      // m_CommandParam 1 is Google.  m_CommandParam 2 is Sketchfab.
      if (button.m_CommandParam == 1 || button.m_CommandParam == 2) {
        if (App.Config.IsMobileHardware && m_SaveAndProceedButton != null) {
          m_SaveAndProceedButton.SetCommandParameters(button.m_CommandParam, 0);
          UpdateMode(Mode.ConfirmLogin);
        } else {
          OAuth2Identity.UserInfo userInfo = (button.m_CommandParam == 1) ?
              App.GoogleIdentity.Profile : App.SketchfabIdentity.Profile;
          if (userInfo == null) {
            UpdateMode(Mode.TakeOffHeadset);
            m_Persistent = true;
          }
        }
      }
      break;
    case SketchControlsScript.GlobalCommands.AccountInfo:
      // Identifier for triggering an info message.
      switch (button.m_CommandParam) {
      case 0: UpdateMode(Mode.DriveHelp); break;
      case 1: UpdateMode(Mode.GoogleHelp); break;
      case 2: UpdateMode(Mode.SketchfabHelp); break;
      }
      break;
    case SketchControlsScript.GlobalCommands.SignOutConfirm:
      switch((Cloud)button.m_CommandParam) {
      case Cloud.Poly:
        m_GoogleSignedInElements.SetActive(false);
        m_GoogleSignedOutElements.SetActive(false);
        m_GoogleConfirmSignOutElements.SetActive(true);
        break;
      case Cloud.Sketchfab:
        m_SketchfabSignedInElements.SetActive(false);
        m_SketchfabSignedOutElements.SetActive(false);
        m_SketchfabConfirmSignOutElements.SetActive(true);
        break;
      case Cloud.None: break;
      }
      break;
    }
  }
}
}  // namespace TiltBrush
