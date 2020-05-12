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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Json;
using Google.Apis.Services;
using Google.Apis.Upload;
using Newtonsoft.Json;
using UnityEngine;
using DriveData = Google.Apis.Drive.v3.Data;

namespace TiltBrush {
/// Class for storing data in a settings file in a user's Google Drive Tilt Brush Application Folder
/// Contents of the file is not user visible or editable. Currently used for storing the id of the
/// folder used to sync Tilt Brush sketches to.
public class GoogleUserSettings {
  public class LoadSettingsFailed : Exception {
    public LoadSettingsFailed(string message) : base(message) {}
    public LoadSettingsFailed(string message, Exception inner) : base(message, inner) {}
  }

  private const string sm_SettingsFileName = "Settings.json";
  [Serializable]
  private class UserData {
    public string DriveSyncFolderId;
    public Dictionary<string, string> DeviceDriveFolderIds = new Dictionary<string, string>();
  }

  /// Folder Id for the Tilt Brush Sync directory on Google Drive.
  public string DriveSyncFolderId {
    get {
      Debug.Assert(Initialized,
                   "Attempt to use GoogleUserSettings before it has been initialized.");
      return m_UserData.DriveSyncFolderId;
    }
  }

  /// This will be true if there was no settings file on Drive, or if the settings file was
  /// successfully read.
  public bool Initialized => m_UserData != null;

  private UserData m_UserData;
  private DriveService m_DriveService;
  private OAuth2Identity m_GoogleIdentity;

  public GoogleUserSettings(OAuth2Identity googleIdentity) {
    m_GoogleIdentity = googleIdentity;
    m_GoogleIdentity.OnLogout += GoogleLoggedOut;
  }

  private void GoogleLoggedOut() {
    m_UserData = null;
    m_DriveService.Dispose();
    m_DriveService = null;
  }

  public string GetDeviceFolderId(string deviceName) {
    string result;
    if (m_UserData.DeviceDriveFolderIds.TryGetValue(deviceName, out result)) {
      return result;
    }
    return null;
  }

  public async Task SetDeviceFolderIdAsync(string deviceName, string folderId,
                                           CancellationToken token) {
    if (folderId == null) {
      m_UserData.DeviceDriveFolderIds.Remove(deviceName);
    } else {
      m_UserData.DeviceDriveFolderIds[deviceName] = folderId;
    }
    await SerializeToDriveAsync(token);
  }

  /// Attempts to read the user settings file from the Google Drive appData folder. If there is
  /// no settings file, will create a default UserData. If there is a failure UserData will be null.
  public async Task InitializeAsync(CancellationToken cToken) {
    if (m_DriveService == null) {
      m_DriveService = new DriveService(new BaseClientService.Initializer() {
          HttpClientInitializer = m_GoogleIdentity.UserCredential,
          ApplicationName = App.kGoogleServicesAppName,
      });
    }
    if (m_UserData == null) {
      // If DeserializeFromDriveAsync fails for anything other than the settings file not being
      // present, an exception will be thrown, and m_UserData will not get set.
      m_UserData = await DeserializeFromDriveAsync(cToken) ?? new UserData();
    }
  }


  /// Saves the User data to Google Drive AppData folder. Will overwrite an existing Settings.json
  /// if it exists.
  private async Task<bool> SerializeToDriveAsync(CancellationToken token) {
    string json = JsonConvert.SerializeObject(m_UserData);
    var jsonStream = new MemoryStream(Encoding.UTF8.GetBytes(json));

    var fileId = await GetSettingsFileIdAsync(token);
    // the action varies slightly depending on whether we're creating a new file or overwriting an
    // old one.
    if (string.IsNullOrEmpty(fileId)) {
      var file = new DriveData.File {
          Parents = new[] {"appDataFolder"},
          Name = sm_SettingsFileName,
      };
      var request = m_DriveService.Files.Create(file, jsonStream, "application/json");
      var result = await DriveAccess.Retry(() => {
        jsonStream.Seek(0, SeekOrigin.Begin);
        return request.UploadAsync(token);
      });
      return result.Status == UploadStatus.Completed;
    } else {
      var request = m_DriveService.Files.Update(new DriveData.File(), fileId, jsonStream,
                                                "application/json");
      var result = await DriveAccess.Retry(() => {
        jsonStream.Seek(0, SeekOrigin.Begin);
        return request.UploadAsync(token);
      });
      return result.Status == UploadStatus.Completed;
    }
  }

  /// Attempts to find the id of the settings file in the drive appdata folder. Will return null
  /// if no settings file is found.
  private async Task<string> GetSettingsFileIdAsync(CancellationToken token) {
    var findRequest = m_DriveService.Files.List();
    findRequest.Q = $"name = '{sm_SettingsFileName}' and trashed = false";
    findRequest.Spaces = "appDataFolder";
    findRequest.Fields = "nextPageToken, files(id, name, modifiedTime)";
    var files = await DriveAccess.Retry(() => findRequest.ExecuteAsync(token));
    return files.Files.OrderByDescending(x => x.ModifiedTime).FirstOrDefault()?.Id;
  }

  /// Attempts to set the drive sync folder. Serializes the settings once it is done.
  public async Task SetDriveSyncFolderIdAsync(string folderId, CancellationToken token) {
    m_UserData.DriveSyncFolderId = folderId;
    await SerializeToDriveAsync(token);
  }

  /// Reads the settings from the Google Drive App Data Folder settings file. If the file doesn't
  /// exist, it will return null. If there is a failure in reading the file, a LoadSettingsFailed
  /// exception will be thrown.
  private async Task<UserData> DeserializeFromDriveAsync(CancellationToken token) {
    var fileId = await GetSettingsFileIdAsync(token);
    if (string.IsNullOrEmpty(fileId)) {
      return null;
    }
    var downloadRequest = m_DriveService.Files.Get(fileId);
    MemoryStream jsonStream = new MemoryStream();
    var result = await DriveAccess.Retry(() => {
      jsonStream.Seek(0, SeekOrigin.Begin);
      return downloadRequest.DownloadAsync(jsonStream, token);
    });
    if (result.Status != DownloadStatus.Completed) {
      throw new LoadSettingsFailed($"{sm_SettingsFileName} failed to download.");
    }
    try {
      jsonStream.Seek(0, SeekOrigin.Begin);
      return NewtonsoftJsonSerializer.Instance.Deserialize<UserData>(jsonStream);
    } catch (JsonException ex) {
      throw new LoadSettingsFailed($"{sm_SettingsFileName} was incorrectly formatted.", ex);
    }
  }
}
} // namespace TiltBrush
