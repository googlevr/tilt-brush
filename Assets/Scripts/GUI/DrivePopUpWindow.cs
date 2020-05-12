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

public class DrivePopUpWindow : OptionsPopUpWindow {
  [SerializeField] private GameObject m_DriveLinkEnabledElements;
  [SerializeField] private GameObject m_DriveLinkDisabledElements;
  [SerializeField] private GameObject m_DriveFullElements;
  [SerializeField] private GameObject m_BackupCompleteElements;
  [SerializeField] private GameObject m_BackingUpElements;
  [SerializeField] private TextMesh m_BackingUpProgress;

  private bool m_DriveSyncing = false;

  private void Start() {
    App.DriveSync.SyncEnabledChanged += RefreshObjects;
  }

  void OnDestroy() {
    App.DriveSync.SyncEnabledChanged -= RefreshObjects;
  }

  override public void Init(GameObject rParent, string sText) {
    base.Init(rParent, sText);
    RefreshObjects();
    App.DriveAccess.RefreshFreeSpaceAsync().AsAsyncVoid();
  }

  override protected void BaseUpdate() {
    base.BaseUpdate();
    if (App.DriveSync.Syncing != m_DriveSyncing) {
      RefreshObjects();
    }
    RefreshBackupProgressText();
  }

  void RefreshObjects() {
    bool driveFull = App.DriveSync.DriveIsLowOnSpace;
    bool driveSyncEnabled = App.DriveSync.SyncEnabled;
    bool driveSyncing = App.DriveSync.Syncing;
    m_DriveFullElements.SetActive(driveFull && driveSyncEnabled);
    m_DriveLinkEnabledElements.SetActive(!driveFull && driveSyncEnabled);
    m_DriveLinkDisabledElements.SetActive(!driveSyncEnabled);
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
}
}  // namespace TiltBrush
