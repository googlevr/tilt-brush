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
/// Triggers a drive reinitialize when drive sync flags. Should be added to popups that can
/// change drive sync settings so that the change only happens once the popup closes.
public class DriveSyncFlagsWatcher : MonoBehaviour {

  private bool[] m_Flags;

  private void Awake() {
    m_Flags = new bool[(int)DriveSync.SyncedFolderType.Num];
  }

  private void OnEnable() {
    for (int i = 0; i < (int) DriveSync.SyncedFolderType.Num; ++i) {
      m_Flags[i] = App.DriveSync.IsFolderOfTypeSynced((DriveSync.SyncedFolderType)i);
    }
  }

  private void OnDisable() {
    for (int i = 0; i < (int) DriveSync.SyncedFolderType.Num; ++i) {
      if (m_Flags[i] != App.DriveSync.IsFolderOfTypeSynced((DriveSync.SyncedFolderType) i)) {
        App.DriveSync.SyncLocalFilesAsync().AsAsyncVoid();
        break;
      }
    }
  }
}
} // namespace TiltBrush
