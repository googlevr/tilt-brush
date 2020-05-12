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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Google;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using UnityEngine;
using Debug = UnityEngine.Debug;
using DriveData = Google.Apis.Drive.v3.Data;

namespace TiltBrush {
/// Manages access to Google Drive. The code doesn't explicitly retry on errors - it is up to the
/// calling code to do that. However, the Retry static functions provide an easy way to do limited
/// exponential backoff if needed.
public class DriveAccess {
  private class ParentNotFound : Exception {
    public ParentNotFound(Exception inner): base("", inner) {}
  }

  // FAT modified timestamp has 2s granularity, so assume any timestamps within 2.5s of each other
  // are the same.
  private const double kTimeEpsilon = 2.5;
  private const int kRetryAttempts = 3;
  private const int kRetryInitialWaitMs = 500;

  private string m_DeviceFolderId;
  private string m_DeviceId;
  private DriveService m_DriveService;
  private TaskAndCts m_InitTask;
  private bool m_Uninitializing = false;
  private bool m_Initialized = false;
  private OAuth2Identity m_GoogleIdentity;
  private GoogleUserSettings m_GoogleUserSettings;
  private bool m_HasSpaceQuota;
  private long m_DriveFreeSpace;

  /// Triggered any time the readiness of the drive to be accessed changes.
  public Action OnReadyChanged;
  /// True if Drive access is enabled and initialization is complete. Ready to use.
  public bool Ready => m_GoogleIdentity.LoggedIn && m_Initialized;
  // True if initialization is in progress.
  public bool Initializing => m_GoogleIdentity.LoggedIn && m_InitTask != null;
  public string DeviceFolder => m_DeviceFolderId;

  public bool HasSpaceQuota => m_HasSpaceQuota;
  /// Amount of free space left on the drive.
  public long DriveFreeSpace => m_HasSpaceQuota ? m_DriveFreeSpace : 0;

  public DriveAccess(OAuth2Identity googleIdentity, GoogleUserSettings googleUserSettings) {
    m_GoogleIdentity = googleIdentity;
    m_GoogleUserSettings = googleUserSettings;
    m_DeviceId = GetDeviceId();
    m_GoogleIdentity.OnSuccessfulAuthorization += OnGoogleLogin;
    m_GoogleIdentity.OnLogout += OnGoogleLogout;
  }

  private void OnGoogleLogin() {
    InitializeDriveLinkAsync();
  }

  private void OnGoogleLogout() {
    if (Initializing || m_Initialized) {
      UninitializeAsync().AsAsyncVoid();
    }
  }

  ~DriveAccess() {
    m_GoogleIdentity.OnSuccessfulAuthorization -= OnGoogleLogin;
    m_GoogleIdentity.OnLogout -= OnGoogleLogout;
    UninitializeAsync().AsAsyncVoid();
  }

  private static string GetDeviceId() {
    switch (Application.platform) {
      case RuntimePlatform.OSXPlayer:
      case RuntimePlatform.OSXEditor:
        Debug.LogError("Host id not implemented for macOS");
        return "macOS-unknown";
      case RuntimePlatform.Android:
        return GetAndroidId();
      default:
        return GetPcId();
    }
  }

  // wmic.exe can return several lines in its output - interperet a serial number as being anything
  // with a number in it.
  private static string GetPCSerialNumber(string target) {
    var process = new Process();
    process.StartInfo = new ProcessStartInfo();
    process.StartInfo.Arguments = $"{target} get serialnumber";
    process.StartInfo.CreateNoWindow = true;
    process.StartInfo.FileName = "wmic.exe";
    process.StartInfo.UseShellExecute = false;
    process.StartInfo.RedirectStandardOutput = true;
    process.Start();
    process.WaitForExit();
    var output = process.StandardOutput;
    while (!output.EndOfStream) {
      var line = output.ReadLine().Trim();
      if (Regex.IsMatch(line, @"\d") && !Regex.IsMatch(line, @"\s")) {
        return $"Windows-{line}";
      }
    }
    return null;
  }

  // On PCs we use the bios serial number, motherboard serial number, or the computer hostname if
  // that is not available.
  private static string GetPcId() {
    return GetPCSerialNumber("bios") ??
           GetPCSerialNumber("baseboard") ??
           $"Windows-{System.Environment.MachineName}";
  }

  // On Android we use the ANDROID_ID, which is unique for each app/device pair.
  private static string GetAndroidId() {
    var unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
    var context = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
    var resolver = context.Call<AndroidJavaObject>("getContentResolver");
    var secure = new AndroidJavaClass("android.provider.Settings$Secure");
    var androidIdString = secure.GetStatic<string>("ANDROID_ID");
    var android_id = secure.CallStatic<string>("getString", resolver, androidIdString);
    return $"Quest-{android_id}";
  }

  /// Initializes the Google Drive access.
  public async void InitializeDriveLinkAsync() {
    async Task InitializeAsync(CancellationToken token) {
      await m_GoogleUserSettings.InitializeAsync(token);
      m_DriveService = new DriveService(new BaseClientService.Initializer() {
          HttpClientInitializer = m_GoogleIdentity.UserCredential,
          ApplicationName = App.kGoogleServicesAppName,
      });

      m_DeviceFolderId = await GetDeviceFolderAsync(token);
      m_Initialized = true;
      await RefreshFreeSpaceAsync(token);
    }

    if (!m_GoogleIdentity.LoggedIn) {
      return;
    }

    while (m_Uninitializing) {
      await new WaitForUpdate();
    }

    if (m_InitTask != null) {
      if (!await UninitializeAsync()) {
        return;
      }
    }

    m_InitTask = new TaskAndCts();
    m_InitTask.Task = InitializeAsync(m_InitTask.Token);

    try {
      await m_InitTask.Task;
    } catch (OperationCanceledException) {
    } finally {
      m_InitTask = null;
    }

    if (Ready) {
      OnReadyChanged?.Invoke();
    }
  }

  /// Uninitializes the drive, cancelling any initialization that might be in progress.
  public async Task<bool> UninitializeAsync() {
    try {
      m_Uninitializing = true;
      m_Initialized = false;
      m_HasSpaceQuota = false;
      try {
        if (m_InitTask != null) {
          m_InitTask.Cancel();
          await m_InitTask.Task;
        }
      } catch (OperationCanceledException) { }
      m_InitTask = null;
      m_Initialized = false;
      OnReadyChanged?.Invoke();
    } finally {
      m_Uninitializing = false;
    }
    return true;
  }

  /// Returns a list of all the files in a drive folder
  public async Task<List<DriveData.File>> GetFilesInFolderAsync(
      string folderId, CancellationToken token) {
    return await GetFolderContentsAsync(folderId, true, false, token);
  }

  /// Returns a list of all the folders in a drive folder
  public async Task<List<DriveData.File>> GetFoldersInFolderAsync(
      string folderId, CancellationToken token) {
    return await GetFolderContentsAsync(folderId, false, true, token);
  }

  /// Returns a list of all the contents of a folder, files and folders.
  public async Task<List<DriveData.File>> GetFolderContentsAsync(string folderId,
      bool getFiles, bool getFolders, CancellationToken token) {
    var request = m_DriveService.Files.List();
    if (!getFiles && !getFolders) {
      return new List<DriveData.File>();
    }
    string mimeRequest = "";
    if (getFiles && !getFolders) {
      mimeRequest = "mimeType != 'application/vnd.google-apps.folder' and ";
    } else if (!getFiles && getFolders) {
      mimeRequest = "mimeType = 'application/vnd.google-apps.folder' and ";
    }
    request.Q = $"'{folderId}' in parents and " +
                $"{mimeRequest}" +
                $"trashed = false";
    request.Fields =
        "nextPageToken, " +
        "files(id, name, description, thumbnailLink, modifiedTime, size, mimeType, description)";

    var items = new List<DriveData.File>();
    do {
      try {
        var response = await Retry(() => request.ExecuteAsync(token));
        items.AddRange(response.Files);
        request.PageToken = response.NextPageToken;
      } catch (GoogleApiException ex) {
        if (ex.HttpStatusCode != HttpStatusCode.NotFound) {
          throw;
        }
      }
    } while (!string.IsNullOrEmpty(request.PageToken) && !token.IsCancellationRequested);

    return items;
  }

  /// Get the first folder inside another folder with a given name. Returns null if one can't be
  /// found.
  public async Task<DriveData.File> GetFolderAsync(string name, string parentId,
      CancellationToken token) {
    var request = m_DriveService.Files.List();
    request.Q = $"'{parentId}' in parents and name = '{name}' and trashed = false and " +
                "mimeType = 'application/vnd.google-apps.folder'";
    request.Fields = "nextPageToken, files(id, name)";
    try {
      var response = await Retry(() => request.ExecuteAsync(token));
      return response.Files.FirstOrDefault();
    } catch (GoogleApiException ex) {
      if (ex.HttpStatusCode == HttpStatusCode.NotFound) {
        return null;
      } else {
        throw;
      }
    }
  }

  /// Get the first file inside a folder with a given name. Returns null if one can't be found.
  public async Task<DriveData.File> GetFileAsync(string name, string parentId,
      CancellationToken token) {
    var request = m_DriveService.Files.List();
    request.Q = $"'{parentId}' in parents and name = '{name}' and trashed = false";
    request.Fields = "nextPageToken, files(id, name)";
    try {
      var response = await Retry(() => request.ExecuteAsync(token));
      return response.Files.FirstOrDefault();
    } catch (GoogleApiException ex) {
      if (ex.HttpStatusCode == HttpStatusCode.NotFound) {
        return null;
      } else {
        throw;
      }
    }
  }

  public async Task DownloadFileAsync(DriveData.File src, Stream destStream,
                                      CancellationToken token) {
    var downloadRequest = m_DriveService.Files.Get(src.Id);
    await Retry(() => downloadRequest.DownloadAsync(destStream, token));
  }

  /// Creates the root folder if it does not exist.
  public async Task CreateRootFolderAsync(CancellationToken token) {
    string folderId = m_GoogleUserSettings.DriveSyncFolderId;
    if (!string.IsNullOrEmpty(folderId)) {
      var rootRequest = m_DriveService.Files.Get(folderId);
      rootRequest.Fields = "id, trashed, mimeType";
      try {
        var rootMetadata = await Retry(() => rootRequest.ExecuteAsync(token));
        if (rootMetadata != null &&
            rootMetadata.Trashed.HasValue && !rootMetadata.Trashed.Value &&
            rootMetadata.MimeType == "application/vnd.google-apps.folder") {
          return;
        }
      } catch (GoogleApiException ex) {
        if (ex.HttpStatusCode != HttpStatusCode.NotFound) {
          throw;
        }
      }
    }

    // Look for a folder created by this app in the user's root folder. The user may have renamed it
    // from 'Tilt Brush'
    {
      var request = m_DriveService.Files.List();
      request.Q = "'root' in parents and trashed = false and " +
                  "mimeType = 'application/vnd.google-apps.folder'";
      request.Fields = "nextPageToken, files(id, name, createdTime)";
      var result = await Retry(() => request.ExecuteAsync(token));
      if (result != null && result.Files.Count > 0) {
        var rootFolder = result.Files.OrderBy(x => x.CreatedTime).First();
        await m_GoogleUserSettings.SetDriveSyncFolderIdAsync(rootFolder.Id, token);
        return;
      }
    }

    // Perhaps it's still called 'Tilt Brush', but has been moved elsewhere?
    {
      var request = m_DriveService.Files.List();
      request.Q = $"name = '{App.kDriveFolderName}' and trashed = false and " +
                  "mimeType = 'application/vnd.google-apps.folder'";
      request.Fields = "nextPageToken, files(id, name, createdTime)";
      var result = await Retry(() => request.ExecuteAsync(token));
      if (result != null && result.Files.Count > 0) {
        var rootFolder = result.Files.OrderBy(x => x.CreatedTime).First();
        await m_GoogleUserSettings.SetDriveSyncFolderIdAsync(rootFolder.Id, token);
        return;
      }
    }

    var folderMetadata = new DriveData.File();
    folderMetadata.Name = App.kDriveFolderName;
    folderMetadata.MimeType = "application/vnd.google-apps.folder";
    var createRequest = m_DriveService.Files.Create(folderMetadata);
    var folder = await Retry(() => createRequest.ExecuteAsync(token));
    await m_GoogleUserSettings.SetDriveSyncFolderIdAsync(folder.Id, token);
  }

  /// Gets the folder for the device in the drive, if available.
  public async Task<string> GetDeviceFolderAsync(CancellationToken token) {
    // Checking to see if it's in the google settings
    var folderId = m_GoogleUserSettings.GetDeviceFolderId(m_DeviceId);
    if (folderId != null) {
      // make sure it still exists!
      try {
        var request = m_DriveService.Files.Get(folderId);
        request.Fields = "id, trashed, mimeType";
        var metadata = await Retry(() => request.ExecuteAsync(token));
        if (metadata != null && metadata.Trashed.HasValue && !metadata.Trashed.Value) {
          return folderId;
        }
      } catch (GoogleApiException ex) {
        if (ex.HttpStatusCode != HttpStatusCode.NotFound) {
          throw;
        }
      }
    }

    if (!string.IsNullOrEmpty(m_GoogleUserSettings.DriveSyncFolderId)) {
      // Check to see if there is an appropriately named folder in the sync root.
      folderId =
          (await GetFolderAsync(m_DeviceId, m_GoogleUserSettings.DriveSyncFolderId, token))?.Id;
      if (folderId != null) {
        await m_GoogleUserSettings.SetDeviceFolderIdAsync(m_DeviceId, folderId, token);
      }
      return folderId;
    }
    return null;
  }

  /// Creates the device folder if it does not exist.
  public async Task CreateDeviceFolderAsync(CancellationToken token) {
    if (!string.IsNullOrEmpty(m_DeviceFolderId)) {
      return;
    }

    m_DeviceFolderId = (await CreateFolderAsync(m_DeviceId,
                                                m_GoogleUserSettings.DriveSyncFolderId,
                                                token))?.Id;
  }


  /// Returns the given Drive folder, creating it if necessary. If it cannot be created because the
  /// parent doesn't exist, raises ParentNotFound. If the supplied folder name contains a '/' will
  /// recursively ensure intermediate folders.
  public async Task<DriveData.File> CreateFolderAsync(string name, string parentId,
                                                      CancellationToken token) {
    // If the path contains multiple sections, recurse to create them all.
    if (name.Contains("/")) {
      var parts = name.Split('/');
      var folder = await CreateFolderAsync(parts[0], parentId, token);
      string remaining = string.Join("/", parts.Skip(1));
      return await CreateFolderAsync(remaining, folder.Id, token);
    }

    var folderRequest = m_DriveService.Files.List();
    folderRequest.Q = $"name = '{name}' and '{parentId}' in parents and " +
                      "mimeType = 'application/vnd.google-apps.folder' and " +
                      "trashed = false";
    folderRequest.Fields = "nextPageToken, files(id, name)";
    DriveData.FileList fileList;
    try {
      fileList = await Retry(() => folderRequest.ExecuteAsync(token));
    } catch (Google.GoogleApiException e) {
      if (e.HttpStatusCode == HttpStatusCode.NotFound) {
        throw new ParentNotFound(e);
      } else {
        throw;
      }
    }

    if (fileList.Files.Any()) {
      Debug.Assert(fileList.Files.Count == 1,
                   $"ALERT! more than one folder called {name} found! Choosing the first one.");
      return fileList.Files[0];
    } else {
      var folderMetadata = new DriveData.File();
      folderMetadata.Name = name;
      folderMetadata.Parents = new[] {parentId};
      folderMetadata.MimeType = "application/vnd.google-apps.folder";

      var createRequest = m_DriveService.Files.Create(folderMetadata);
      var folder = await Retry(() => createRequest.ExecuteAsync(token));

      return folder;
    }
  }

  public async Task RefreshFreeSpaceAsync(CancellationToken? token = null) {
    if (!Ready) { return; }
    if (!token.HasValue) {
      token = CancellationToken.None;
    }
    var request = m_DriveService.About.Get();
    request.Fields = "storageQuota";
    DriveData.About about = null;
    try {
      about = await Retry(() => request.ExecuteAsync(token.Value));
    } catch (OperationCanceledException) {
      return;
    }

    if (about == null) {
      m_HasSpaceQuota = true;
      m_DriveFreeSpace = 0;
      return;
    }

    // Corp Drive returns null for limit, so in that case, never complain that we're out of space.
    if (!about.StorageQuota.Limit.HasValue) {
      m_HasSpaceQuota = false;
      m_DriveFreeSpace = long.MaxValue;
    } else {
      m_HasSpaceQuota = true;
      m_DriveFreeSpace = about.StorageQuota.Limit.Value - about.StorageQuota.Usage.Value;
    }
  }

  public async Task TrashSketch(string fileName, CancellationToken token) {
    var sketchFolder = await GetFolderAsync("Sketches", m_DeviceFolderId, token);
    if (sketchFolder == null) {
      return;
    }
    var file = await GetFileAsync(fileName, sketchFolder.Id, token);
    if (file == null) {
      return;
    }
    var trashFile = new DriveData.File();
    trashFile.Trashed = true;
    var trashRequest = m_DriveService.Files.Update(trashFile, file.Id);
    await Retry(() => trashRequest.ExecuteAsync(token));
  }

  /// Upload a file to Drive. Make sure mimetype and parents are specified.
  public async Task UploadFileAsync(DriveData.File file, Stream dataStream,
                                    CancellationToken token, IProgress<long> progress = null) {
    Debug.Assert(file.Parents != null);
    Debug.Assert(dataStream.CanSeek);
    var uploader = m_DriveService.Files.Create(file, dataStream, file.MimeType);
    if (progress != null) { uploader.ProgressChanged += (p) => progress.Report(p.BytesSent); }
    long position = dataStream.Position;
    var result = await Retry(() => {
      dataStream.Seek(position, SeekOrigin.Begin);
      return uploader.UploadAsync(token);
    });
    if (result.Status != UploadStatus.Completed && !token.IsCancellationRequested) {
      Debug.LogException(new DriveSync.DataTransferError("Google Drive new file upload failed.",
                                                         result.Exception));
    }
    await RefreshFreeSpaceAsync(token);
  }

  /// Update an existing file. Make sure mimetype and existing id are specified. file paramater will
  /// be altered during this function.
  public async Task UpdateFileAsync(DriveData.File file, Stream dataStream,
                                    CancellationToken token, IProgress<long> progress = null) {
    Debug.Assert(!string.IsNullOrEmpty(file.Id));
    Debug.Assert(!string.IsNullOrEmpty(file.MimeType));
    Debug.Assert(dataStream.CanSeek);
    string id = file.Id;
    file.Id = null;
    file.Parents = null;
    var uploader = m_DriveService.Files.Update(file, id, dataStream, file.MimeType);
    if (progress != null) {
      uploader.ProgressChanged += (p) => progress.Report(p.BytesSent);
    }
    long position = dataStream.Position;
    var result = await Retry(() => {
      dataStream.Seek(position, SeekOrigin.Begin);
      return uploader.UploadAsync(token);
    });
    if (result.Status != UploadStatus.Completed && !token.IsCancellationRequested) {
      Debug.LogException(new DriveSync.DataTransferError("Google Drive file update failed.",
                                                         result.Exception));
    }
    await RefreshFreeSpaceAsync(token);
  }

  /// Download a file with a given id to a stream.
  public async Task DownloadFileAsync(string fileId, Stream dataStream, CancellationToken token,
                                      IProgress<long> progress = null) {
    Debug.Assert(dataStream.CanSeek);
    var downloadRequest = m_DriveService.Files.Get(fileId);
    if (progress != null) {
      downloadRequest.MediaDownloader.ProgressChanged += (p) => progress.Report(p.BytesDownloaded);
    }
    long position = dataStream.Position;
    await Retry(() => {
      dataStream.Seek(position, SeekOrigin.Begin);
      return downloadRequest.DownloadAsync(dataStream, token);
    });
  }

  public static bool IsNewer(DriveData.File driveFile, FileInfo localFile) {
    // Note that although Google Drive modified times use UTC, the Drive .NET API converts them all
    // in to local time, which is why we're not using localFile.LastWriteTimeUtc here.
    var diff = driveFile.ModifiedTime.Value - localFile.LastWriteTime;
    return diff.TotalSeconds >= kTimeEpsilon;
  }

  public static bool IsNewer(FileInfo localFile, DriveData.File driveFile) {
    var diff = localFile.LastWriteTime - driveFile.ModifiedTime.Value;
    return diff.TotalSeconds >= kTimeEpsilon;
  }

  public static bool IsNewer(FileInfo first, FileInfo second) {
    var diff = first.LastWriteTime - first.LastWriteTime;
    return diff.TotalSeconds >= kTimeEpsilon;
  }

  /// Retries a task several times with exponential backoff. Captures exceptions related to
  /// network connection issues.
  public static async Task<T> Retry<T>(Func<Task<T>> taskSource) {
    for (int i = 0; i < kRetryAttempts ; ++i) {
      Exception error;
      try {
        return await taskSource.Invoke();
      } catch (SocketException ex) {
        error = ex;
      } catch (WebException ex) {
        error = ex;
      } catch (HttpRequestException ex) {
        error = ex;
      }
      Debug.Log(
          $"Exception caught on attempt {i} - retrying.\n{error.Message}\n{error.StackTrace}");
      await Task.Delay((int) (Mathf.Pow(2, i) * kRetryInitialWaitMs));
    }
    return await taskSource.Invoke();
  }

  /// Retries a task several times with exponential backoff. Captures exceptions related to
  /// network connection issues.
  public static async Task Retry(Func<Task> taskSource) {
    for (int i = 0; i < kRetryAttempts ; ++i) {
      Exception error;
      try {
        await taskSource.Invoke();
        return;
      } catch (SocketException ex) {
        error = ex;
      } catch (WebException ex) {
        error = ex;
      } catch (HttpRequestException ex) {
        error = ex;
      }
      Debug.Log(
          $"Exception caught on attempt {i} - retrying.\n{error.Message}\n{error.StackTrace}");
      await Task.Delay((int) (Mathf.Pow(2, i) * kRetryInitialWaitMs));
    }
    await taskSource.Invoke();
  }
}
} // namespace TiltBrush
