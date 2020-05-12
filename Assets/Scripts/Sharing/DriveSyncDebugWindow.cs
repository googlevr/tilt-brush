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

#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace TiltBrush {
public partial class DriveSync {

  [MenuItem("Tilt/DriveSync Debug Window")]
  public static void OpenDriveSyncWindow() {
    EditorWindow.GetWindow(typeof(DriveSync.DriveSyncWindow));
  }

  public class DriveSyncWindow : EditorWindow {
    private void OnGUI() {
      if (!Application.isPlaying) {
        EditorGUILayout.HelpBox("Only works in Play Mode.", MessageType.Info);
        return;
      }
      if (App.Instance == null || App.DriveSync == null) {
        EditorGUILayout.HelpBox("DriveSync has not been constructed yet.", MessageType.Info);
        return;
      }

      DriveSync ds = App.DriveSync;
      DriveAccess da = App.DriveAccess;
      EditorGUILayout.Toggle("m_InitTask", ds.m_InitTask != null);
      EditorGUILayout.Toggle("m_SyncTask", ds.m_SyncTask != null);
      EditorGUILayout.Toggle("m_UpdateTask", ds.m_UpdateTask != null);
      EditorGUILayout.Toggle("Initialized", ds.m_Initialized);
      EditorGUILayout.Toggle("Uninitializing", ds.m_Uninitializing);
      if (da.HasSpaceQuota) {
        float megabytes = ((float) App.DriveAccess.DriveFreeSpace) / (1024 * 1024);
        EditorGUILayout.LabelField("Free space (MB)", $"{megabytes}MB");
      } else {
        GUILayout.Label("Drive has no usage quota.");
      }
      GUILayout.Label("Transfers:");
      foreach (var transfer in ds.m_Transfers.Keys) {
        GUILayout.Label($"{transfer.Item.AbsoluteLocalPath}\\{transfer.Item.Name}");
      }
      EditorGUILayout.LabelField("Remaining to transfer:", $"{ds.m_ToTransfer.Count}");
      EditorGUILayout.LabelField("m_TotalBytesToTransfer:", $"{ds.m_TotalBytesToTransfer}");
      EditorGUILayout.LabelField("m_BytesTransferred:", $"{ds.m_BytesTransferred}");
      long bytesTransferred = ds.m_BytesTransferred;
      bytesTransferred += ds.m_Transfers.Keys.Sum(x => x.BytesTransferred);
      EditorGUILayout.LabelField("Running bytes tally", $"{bytesTransferred}");
      EditorGUILayout.LabelField("Progress:", $"{ds.Progress}");
      GUILayout.Label("Folders to transfer:");
      foreach (var folder in ds.m_Folders.Where(x => x?.Drive != null)) {
        EditorGUILayout.LabelField(folder.FolderType.ToString(), folder.Drive.Id);
      }
      GUILayout.Label("Drive State");
      EditorGUILayout.Toggle("GoogleIdentity.LoggedIn", App.GoogleIdentity.LoggedIn);
      EditorGUILayout.Toggle("DriveAccess.Initializing", App.DriveAccess.Initializing);
      EditorGUILayout.Toggle("DriveAccess.Ready", App.DriveAccess.Ready);
      EditorGUILayout.Toggle("DriveSync.Initializing", App.DriveSync.Initializing);
      EditorGUILayout.Toggle("DriveSync.Initialized", App.DriveSync.Initialized);
      EditorGUILayout.Toggle("DriveSync.Syncing", App.DriveSync.Syncing);
      var sketchset = SketchCatalog.m_Instance.GetSet(SketchSetType.Drive);
      if (sketchset != null) {
        EditorGUILayout.Toggle("SketchSet.IsReadyForAccess", sketchset.IsReadyForAccess);
        EditorGUILayout.Toggle("SketchSet.IsActivelyRefreshingSketches",
                               sketchset.IsActivelyRefreshingSketches);
      }
    }

    private void Update() {
      if (!Application.isPlaying) {
        return;
      }
      DriveSync ds = App.DriveSync;
      if (ds.m_InitTask != null ||
          ds.m_SyncTask != null ||
          ds.m_Uninitializing ||
          ds.m_ToTransfer.Count != 0 ||
          ds.m_Transfers.Any()) {
        Repaint();
      }
    }
  }
}
} // namespace TiltBrush
#endif
