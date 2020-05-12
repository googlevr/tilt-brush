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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;
using DriveData = Google.Apis.Drive.v3.Data;

namespace TiltBrush {
public partial class DriveSync {
  public const long kMinimumFreeSpace = 512 * 1024 * 1024; // Half a Gig

  public class DataTransferError : Exception {
    public DataTransferError(string message, Exception inner) : base(message, inner) { }
  }

  [Flags]
  private enum SyncType : short{
    Download = 0x1,
    Upload = 0x2,
    UploadAndDownload = Download | Upload,
  }

  [Serializable]
  public enum SyncedFolderType {
    Sketches = 0,
    Snapshots,
    Videos,
    MediaLibrary,
    Exports,
    Num,
  }

  private class SyncedFolder {
    public string AbsoluteLocalPath;
    public string Name;
    public string ParentDriveId;
    public DriveData.File Drive;
    public DirectoryInfo Local;
    public SyncType SyncType;
    public bool Recursive;
    public string[] ExcludeExtensions;
    public SyncedFolderType FolderType;

    public bool Upload => (SyncType & SyncType.Upload) > 0;
    public bool Download => (SyncType & SyncType.Download) > 0;
  }

  public class SyncItem {
    public string Name;
    public string AbsoluteLocalPath;
    public string ParentId;
    public string FileId;
    public bool Overwrite;
    public DateTime LastModified;
    public bool Upload;
    public long Size;
    public SyncedFolderType FolderType;
  }

  /// This is a sorted queue for storing SyncItems. It has O(logN) access times, and the items are
  /// sorted by their modification date. You 'Insert' rather than 'Enqueue' as the item will be
  /// inserted into the queue by modified time rather than put on the end. When you Dequeue it will
  /// remove and return the item with the most recent modified time.
  private class SyncItemQueue {
    private SortedDictionary<DateTime, Queue<SyncItem>> m_Queue;
    private int m_Count = 0;

    public int Count => m_Count;

    public SyncItemQueue() {
      m_Queue = new SortedDictionary<DateTime, Queue<SyncItem>>();
    }

    public void Insert(SyncItem item) {
      Queue<SyncItem> itemQueue;
      lock (m_Queue) {
        if (!m_Queue.TryGetValue(item.LastModified, out itemQueue)) {
          itemQueue = new Queue<SyncItem>();
          m_Queue[item.LastModified] = itemQueue;
        }
        itemQueue.Enqueue(item);
        m_Count++;
      }
    }

    public SyncItem Dequeue() {
      SyncItem item;
      lock (m_Queue) {
        if (m_Count == 0) {
          return null;
        }
        m_Count--;
        var itemQueue = m_Queue.Last().Value;
        item = itemQueue.Dequeue();
        if (itemQueue.Count == 0) {
          m_Queue.Remove(item.LastModified);
        }
      }
      return item;
    }

    public void Clear() {
      lock (m_Queue) {
        m_Queue.Clear();
        m_Count = 0;
      }
    }
  }

  /// Class for storing a user preference, along with storing it in PlayerPrefs.
  private class UserSyncFlag {
    private string m_PreferenceName;
    private bool m_Value;
    public UserSyncFlag(string user, string preference) {
      m_PreferenceName = $"{user}_{preference}";
      m_Value = PlayerPrefs.GetInt(m_PreferenceName, 0) == 1;
    }
    public bool Value {
      get => m_Value;
      set {
        m_Value = value;
        PlayerPrefs.SetInt(m_PreferenceName, m_Value ? 1 : 0);
      }
    }
  }

  private class Transfer : IProgress<long> {
    public SyncItem Item { get; private set; }
    public TaskAndCts TaskAndCts { get; private set; }
    public long BytesTransferred { get; private set; }
    public Task Task => TaskAndCts.Task;
    public Transfer(DriveSync ds, SyncItem item) {
      Item = item;
      TaskAndCts = new TaskAndCts();
      TaskAndCts.Task = item.Upload
          ? ds.UploadItemAsync(this, TaskAndCts.Token)
          : ds.DownloadItemAsync(this, TaskAndCts.Token);
    }
    public void Report(long value) {
      BytesTransferred = value;
    }
    public void Cancel() {
      TaskAndCts.Cancel();
    }
  }

  private List<SyncedFolder> m_Folders = new List<SyncedFolder>();
  private SyncItemQueue m_ToTransfer = new SyncItemQueue();
  private ConcurrentDictionary<Transfer, object> m_Transfers =
      new ConcurrentDictionary<Transfer, object>();
  private TaskAndCts m_InitTask;
  private TaskAndCts m_SyncTask;
  private TaskAndCts m_UpdateTask;
  private bool m_Uninitializing = false;
  private bool m_Initialized = false;
  private UserSyncFlag m_SyncEnabled;
  private UserSyncFlag[] m_SyncedFolderFlags;
  private DriveAccess m_DriveAccess;
  private OAuth2Identity m_GoogleIdentity;
  private long m_TotalBytesToTransfer;
  private long m_PreviousTotalBytesToTransfer;
  private long m_BytesTransferred;
  private bool m_IsCancelling;

  public event Action SyncEnabledChanged;

  // True if initialization is in progress.
  public bool Initializing => m_GoogleIdentity.LoggedIn && m_InitTask != null;

  public bool Initialized => m_Initialized;

  public float Progress {
    get {
      // We use the larger of the byte totals from the current and interrupted sync, so that the
      // progress does not jump around while the scan is performed.
      long totalBytes = Math.Max(m_TotalBytesToTransfer, m_PreviousTotalBytesToTransfer);
      if (totalBytes == 0) {
        return 0;
      }
      long bytesTransferred = m_BytesTransferred;
      bytesTransferred += m_Transfers.Keys.Sum(x => x.BytesTransferred);
      float progress = m_TotalBytesToTransfer == 0
          ? 0
          : Mathf.Clamp01((float) bytesTransferred / totalBytes);
      return progress;
    }
  }

  /// This is whether the drive is set to sync. It does not imply that syncing has been successfully
  /// initialized. Its value cannot be changed while the service is being cancelled / shut down.
  public bool SyncEnabled {
    get => m_SyncEnabled?.Value ?? false;
    set {
      if (m_Uninitializing) {
        return;
      }
      if (m_SyncEnabled == null) {
        if (value && !m_DriveAccess.Ready && !m_DriveAccess.Initializing
                  && m_GoogleIdentity.LoggedIn) {
          // It seems like Drive Access initialization failed. Let's try again.
          m_DriveAccess.InitializeDriveLinkAsync();
        }
        return;
      }
      bool changed = value != m_SyncEnabled.Value;
      if (changed) {
        m_SyncEnabled.Value = value;
        SyncEnabledChanged?.Invoke();
      }
      if (value) {
        if (!Initializing && !m_Initialized) {
          InitializeDriveSyncAsync();
        }
      } else {
        if (Initializing || m_Initialized) {
          UninitializeAsync().AsAsyncVoid();
        }
      }
    }
  }

  public bool Syncing {
    get {
      if (!m_Initialized) { return false; }
      if (m_SyncTask != null || m_ToTransfer.Count != 0) {
        return true;
      }
      return m_Transfers.Any();
    }
  }

  public bool DriveIsLowOnSpace => m_DriveAccess.HasSpaceQuota &&
                                   (m_DriveAccess.DriveFreeSpace < kMinimumFreeSpace);

  public void InitUserSyncOptions() {
    string userId = m_GoogleIdentity.Profile.id.Substring(7);
    m_SyncEnabled = new UserSyncFlag(userId, "GoogleDriveSyncEnabled");
    m_SyncedFolderFlags = new UserSyncFlag[(int)SyncedFolderType.Num];
    for (int i = 0; i < (int) SyncedFolderType.Num; ++i) {
      SyncedFolderType folderType = (SyncedFolderType) i;
      m_SyncedFolderFlags[i] = new UserSyncFlag(userId, $"GoogleDriveSyncFlag_{folderType}");
    }
  }

  public void UninitUserSyncOptions() {
    m_SyncEnabled = null;
    m_SyncedFolderFlags = null;
  }

  public void ToggleSyncOnFolderOfType(SyncedFolderType type) {
    if (m_SyncedFolderFlags == null) {
      return;
    }
    int flagIndex = (int)type;
    Debug.Assert(flagIndex >= 0 && flagIndex < m_SyncedFolderFlags.Length);
    if (flagIndex >= 0 && flagIndex < m_SyncedFolderFlags.Length) {
      m_SyncedFolderFlags[flagIndex].Value ^= true;
    }
  }

  public bool IsFolderOfTypeSynced(SyncedFolderType type) {
    if (m_SyncedFolderFlags == null) {
      return false;
    }
    int flagIndex = (int)type;
    Debug.Assert(flagIndex >= 0 && flagIndex < m_SyncedFolderFlags.Length);
    if (flagIndex >= 0 && flagIndex < m_SyncedFolderFlags.Length) {
      return m_SyncedFolderFlags[flagIndex].Value;
    }
    return false;
  }

  public DriveSync(DriveAccess driveAccess, OAuth2Identity googleIdentity) {
    m_DriveAccess = driveAccess;
    m_GoogleIdentity = googleIdentity;
    m_DriveAccess.OnReadyChanged += OnDriveAccessReady;
  }

  /// This reacts to drive access being turned on or off.
  private void OnDriveAccessReady() {
    if (m_DriveAccess.Ready) {
      InitUserSyncOptions();
      if (SyncEnabled) {
        InitializeDriveSyncAsync();
      }
    } else {
      UninitializeAsync().AsAsyncVoid();
      UninitUserSyncOptions();
    }
  }

  /// Initializes the Google Drive sync. Will create the required folders on Drive, and afterwards
  /// kick off a process to sync them.
  public async void InitializeDriveSyncAsync() {
    async Task InitializeAsync(CancellationToken token) {
      // Make sure we have a root folder
      await m_DriveAccess.CreateRootFolderAsync(token);
      // Make sure we have a folder for the device
      await m_DriveAccess.CreateDeviceFolderAsync(token);

      if (m_DriveAccess.HasSpaceQuota) {
        Debug.Log($"User has {m_DriveAccess.DriveFreeSpace} free space on Drive.\n" +
                  $"That's {m_DriveAccess.DriveFreeSpace - kMinimumFreeSpace} " +
                  "before hitting low free space.");
      } else {
        Debug.Log("User has no quota on their Drive.");
      }

      m_Initialized = true;

      // TODO: Do an upload-only sync for Export, Snapshots and Videos.
    }

    if (!m_GoogleIdentity.LoggedIn) {
      return;
    }

    if (!m_DriveAccess.Ready) {
      return;
    }

    while (m_Uninitializing) {
      await new WaitForUpdate();
    }

    if (m_InitTask != null || Initialized) {
      await UninitializeAsync();
    }

    m_InitTask = new TaskAndCts();
    m_InitTask.Task = InitializeAsync(m_InitTask.Token);

    try {
      await m_InitTask.Task;
    } catch (OperationCanceledException) {
    } finally {
      m_InitTask = null;
    }

    // Don't wait for the sync to finish so that we can start transfers immediately.
    SyncLocalFilesAsync().AsAsyncVoid();

    // A background task handles the transfers.
    m_UpdateTask = new TaskAndCts();
    m_UpdateTask.Task = ManageTransfersAsync(m_UpdateTask.Token);
    m_UpdateTask.Task.AsAsyncVoid();
  }

  private async Task SetupSyncFoldersAsync(CancellationToken token) {
    var deviceRootId = m_DriveAccess.DeviceFolder;
    m_Folders.Clear();
    var folderSyncs = new List<Task>();
      if (IsFolderOfTypeSynced(SyncedFolderType.Sketches)) {
        folderSyncs.Add( AddSyncedFolderAsync(
                             "Sketches",
                             App.UserSketchPath(),
                             deviceRootId,
                             SyncType.Upload,
                             SyncedFolderType.Sketches,
                             token));
      }
      if (IsFolderOfTypeSynced(SyncedFolderType.MediaLibrary)) {
        var mediaLibrary =
            await m_DriveAccess.CreateFolderAsync("Media Library", deviceRootId, token);
        folderSyncs.Add(AddSyncedFolderAsync(
                            "Images",
                            App.ReferenceImagePath(),
                            mediaLibrary.Id,
                            SyncType.Upload,
                            SyncedFolderType.MediaLibrary,
                            token));
        if (!App.Config.IsMobileHardware) {
          folderSyncs.Add(AddSyncedFolderAsync(
                            "Models",
                            App.ModelLibraryPath(),
                            mediaLibrary.Id,
                            SyncType.Upload,
                            SyncedFolderType.MediaLibrary,
                            token,
                            recursive: true));
        }
        folderSyncs.Add(AddSyncedFolderAsync(
                            "Videos",
                            App.VideoLibraryPath(),
                            mediaLibrary.Id,
                            SyncType.Upload,
                            SyncedFolderType.MediaLibrary,
                            token));
      }
      if (IsFolderOfTypeSynced(SyncedFolderType.Snapshots)) {
        folderSyncs.Add(AddSyncedFolderAsync(
                            "Snapshots",
                            App.SnapshotPath(),
                            deviceRootId,
                            SyncType.Upload,
                            SyncedFolderType.Snapshots,
                            token));
      }

      if (!App.Config.IsMobileHardware) {
        if (IsFolderOfTypeSynced(SyncedFolderType.Videos)) {
          folderSyncs.Add(AddSyncedFolderAsync(
                            "Videos",
                            App.VideosPath(),
                            deviceRootId,
                            SyncType.Upload,
                            SyncedFolderType.Videos,
                            token,
                            excludeExtensions: new[] {".bat", ".usda"}));
          folderSyncs.Add(AddSyncedFolderAsync(
                            "VrVideos",
                            App.VrVideosPath(),
                            deviceRootId,
                            SyncType.Upload,
                            SyncedFolderType.Videos,
                            token));
        }
      }

      if (IsFolderOfTypeSynced(SyncedFolderType.Exports)) {
        folderSyncs.Add(AddSyncedFolderAsync(
                            "Exports",
                            App.UserExportPath(),
                            deviceRootId,
                            SyncType.Upload,
                            SyncedFolderType.Exports,
                            token,
                            recursive: true));
      }
      await m_DriveAccess.RefreshFreeSpaceAsync(token);
      await Task.WhenAll(folderSyncs);
  }

  /// Uninitializes the drive sync, cancels any in-flight transfers, and cancels any in-flight
  /// initialization.
  public async Task UninitializeAsync() {
    try {
      m_Uninitializing = true;
      m_ToTransfer.Clear();

      async Task CancelTaskCts(TaskAndCts taskAndCts) {
        try {
          if (taskAndCts != null && !taskAndCts.Task.IsCompleted) {
            taskAndCts.Cancel();
            await taskAndCts.Task;
          }
        } catch (OperationCanceledException) { }
      }

      // Wait for five seconds for cancellation to happen - if it still hasn't, well - continue
      // anyway and hope everything is fine.
      var maxWait = Task.Delay(TimeSpan.FromSeconds(5));
      var allTasks = Task.WhenAll(m_Transfers.Keys.Select(x => x.TaskAndCts)
                                      .Concat(new []{m_InitTask, m_SyncTask, m_UpdateTask})
                                      .Select(CancelTaskCts));
      await Task.WhenAny(allTasks, maxWait);
      m_Initialized = false;
      m_Transfers.Clear();
      m_InitTask = null;
      m_SyncTask = null;
      m_UpdateTask = null;
      m_Folders.Clear();
      m_TotalBytesToTransfer = 0;
      m_BytesTransferred = 0;
    } finally {
      m_Uninitializing = false;
    }
  }

  private async Task ClearAllTransfers() {
    m_ToTransfer.Clear();
    foreach (var transfer in m_Transfers.Keys) {
      transfer.Cancel();
    }
    await Task.WhenAll(m_Transfers.Keys.Select(x => x.Task));
    m_Transfers.Clear();
  }

  /// Syncs the local files with the device's Google Drive folder. If a sync is already in progress
  /// it will be cancelled before a new sync is performed. The sync prepares the transfers required
  /// to sync and then the actual transfers happen in the Update function.
  public async Task SyncLocalFilesAsync() {
    if (!m_GoogleIdentity.LoggedIn || !SyncEnabled || DriveIsLowOnSpace || m_IsCancelling) {
      return;
    }

    if (m_SyncTask != null) {
      m_IsCancelling = true;
      try {
        await m_SyncTask.Task;
      } catch (OperationCanceledException) {
      } finally {
        m_IsCancelling = false;
      }
    }

    try {
      m_SyncTask = new TaskAndCts();
      m_SyncTask.Task = PrepareAllFolderTransfersAsync(m_SyncTask.Token);
      await m_SyncTask.Task;
    } catch (OperationCanceledException) {
    } finally {
      m_SyncTask = null;
    }
  }

    /// Adds a folder to the list of synced folders.
  private async Task AddSyncedFolderAsync(
        string name,
        string localPath,
        string parentId,
        SyncType syncType,
        SyncedFolderType folderType,
        CancellationToken token,
        bool recursive = false,
        string[] excludeExtensions = null) {
      var folder = new SyncedFolder() {
        Name = name,
        AbsoluteLocalPath = localPath,
        Local = Directory.Exists(localPath) ? new DirectoryInfo(localPath) : null,
        Drive = await m_DriveAccess.GetFolderAsync(name, parentId, token),
        SyncType = syncType,
        Recursive = recursive,
        ParentDriveId = parentId,
        ExcludeExtensions = excludeExtensions,
        FolderType = folderType,
    };
    m_Folders.Add(folder);
  }


  /// Enumerates the transfers required to sync all folders, sorts those transfers in descending
  /// order of modification date, and stores the transfer information in the transfer queue.
  private async Task PrepareAllFolderTransfersAsync(CancellationToken token) {
    if (!App.GoogleUserSettings.Initialized || !m_Initialized || m_Uninitializing) {
      return;
    }
    // If this is called while there are still things being transferred, we store off the total
    // number of bytes that was being transferred by the old sync.
    m_PreviousTotalBytesToTransfer = m_TotalBytesToTransfer;
    m_ToTransfer.Clear();
    // Cancel any transfers to folders we no longer want to backup
    var toRemove = m_Transfers.Where(x => !IsFolderOfTypeSynced(x.Key.Item.FolderType))
                              .Select(x => x.Key).ToArray();
    foreach (var transfer in toRemove) {
      m_Transfers.TryRemove(transfer, out _);
      transfer.Cancel();
    }
    foreach (var transfer in toRemove) {
      try {
        await transfer.Task;
      } catch (OperationCanceledException) {
      } catch (Exception ex) {
        Debug.LogException(ex);
      }
    }
    // We set the new total to be the number of bytes in the files currently in-flight, and add the
    // total number of bytes made up by completely transferred files. This will get increased as the
    // scan is performed, and the new total to transfer will be larger than the old one.
    m_TotalBytesToTransfer = m_Transfers.Keys.Sum(x => x.Item.Size) + m_BytesTransferred;

    await SetupSyncFoldersAsync(token);

    if (!m_IsCancelling) {
      var enumerateTasks =
          m_Folders.Select(x => EnumerateFolderTransfersAsync(x, token)).ToArray();
      await Task.WhenAll(enumerateTasks);
    }
    // now the scan is complete, clear the previous total.
    m_PreviousTotalBytesToTransfer = 0;
  }

  /// Compares the contents of a local folder, and a one on Drive, for parity and creates a queue
  /// of transfers in each direction to sync them.
  private async Task EnumerateFolderTransfersAsync(SyncedFolder folder, CancellationToken token) {
    if (folder.Local == null && folder.Download) {
      Debug.Log($"Creating new local directory at {folder.AbsoluteLocalPath}.");
      folder.Local = Directory.CreateDirectory(folder.AbsoluteLocalPath);
    }

    if (folder.Drive == null && folder.Upload) {
      Debug.Log($"Creating new Drive folder called {folder.Name}.");
      folder.Drive = await m_DriveAccess.CreateFolderAsync(
                                     folder.Name, folder.ParentDriveId, CancellationToken.None);
    }

    if (folder.Drive == null || folder.Local == null || m_IsCancelling) {
      return;
    }

    var driveContents =
        await m_DriveAccess.GetFolderContentsAsync(folder.Drive.Id, true, true,
                                                   CancellationToken.None);
    var driveFiles = new Dictionary<string, DriveData.File>();
    foreach (var item in driveContents
        .Where(x => x.MimeType != "application/vnd.google-apps.folder")) {
      if (driveFiles.ContainsKey(item.Name)) {
        Debug.LogWarning($"Error! two copies of {folder.Name}/{item.Name} found.");
        if (item.ModifiedTime > driveFiles[item.Name].ModifiedTime) {
          driveFiles[item.Name] = item;
        }
      } else {
        driveFiles.Add(item.Name, item);
      }
    }
    var localFiles = folder.Local.GetFiles().ToDictionary(x => x.Name);
    if (folder.ExcludeExtensions != null) {
      localFiles = localFiles.Values
          .Where(x => !folder.ExcludeExtensions.Contains(Path.GetExtension(x.Name)))
          .ToDictionary(x => x.Name);
      driveFiles = driveFiles.Values
          .Where(x => !folder.ExcludeExtensions.Contains(Path.GetExtension(x.Name)))
          .ToDictionary(x => x.Name);
    }
    var localSet = new HashSet<string>(localFiles.Keys);
    var driveSet = new HashSet<string>(driveFiles.Keys);

    if (folder.Download) {
      var toDownload = driveFiles
          .Where(x => !localSet.Contains(x.Key) || DriveAccess.IsNewer(x.Value, localFiles[x.Key]))
          .Select(x => x.Value).ToArray();
      foreach (var file in toDownload) {
        if (m_Transfers.Keys.Any(x => x.Item.Name == file.Name &&
                                      x.Item.AbsoluteLocalPath == folder.Local.FullName)) {
          continue;
        }
        m_ToTransfer.Insert(new SyncItem {
            Name = file.Name,
            AbsoluteLocalPath = folder.Local.FullName,
            FileId = file.Id,
            Overwrite = localSet.Contains(file.Name),
            LastModified = file.ModifiedTime.Value,
            Upload = false,
            Size = file.Size.Value,
            FolderType = folder.FolderType,
        });
        m_TotalBytesToTransfer += file.Size.Value;
      }
    }

    if (folder.Upload) {
      var toUpload = localFiles
          .Where(x => !driveSet.Contains(x.Key) || DriveAccess.IsNewer(x.Value, driveFiles[x.Key]))
          .Select(x => x.Value).ToArray();
      foreach (var file in toUpload) {
        if (m_Transfers.Keys.Any(x => x.Item.Name == file.Name &&
                                      x.Item.AbsoluteLocalPath == folder.Local.FullName)) {
          continue;
        }
        m_ToTransfer.Insert(new SyncItem() {
            Name = file.Name,
            AbsoluteLocalPath = folder.Local.FullName,
            ParentId = folder.Drive.Id,
            FileId = driveSet.Contains(file.Name) ? driveFiles[file.Name].Id : null,
            LastModified = file.LastWriteTime,
            Upload = true,
            Size = file.Length,
            FolderType = folder.FolderType,
        });
        m_TotalBytesToTransfer += file.Length;
      }
    }

    if (!folder.Recursive) {
      return;
    }

    var driveFolders = driveContents.Where(x => x.MimeType == "application/vnd.google-apps.folder")
        .ToDictionary(x => x.Name);
    var localFolders = folder.Local.GetDirectories().ToDictionary(x => x.Name);
    var folderNames = new HashSet<string>(driveFolders.Keys.Concat(localFolders.Keys));
    foreach (var subFolderName in folderNames) {
      bool OnDrive = driveFolders.ContainsKey(subFolderName);
      bool OnLocal = localFolders.ContainsKey(subFolderName);

      if (m_IsCancelling) {
        return;
      }

      var subfolder = new SyncedFolder {
          Name = subFolderName,
          AbsoluteLocalPath = Path.Combine(folder.AbsoluteLocalPath, subFolderName),
          Drive = OnDrive ? driveFolders[subFolderName] : null,
          Local = OnLocal ? localFolders[subFolderName] : null,
          SyncType = folder.SyncType,
          Recursive = folder.Recursive,
          ParentDriveId = folder.Drive.Id,
          FolderType = folder.FolderType,
      };
      await EnumerateFolderTransfersAsync(subfolder, token);
    }
  }

  // Update checks to see if any transfer tasks are ready to be performed and kicks them off
  // Currently, only one upload and one download happen at the same time.
  private async Task ManageTransfersAsync(CancellationToken token) {
    var updateWait = new WaitForUpdate();
    while (!token.IsCancellationRequested) {
      await updateWait;
      if (m_ToTransfer.Count == 0 && !m_Transfers.Any()) {
        continue;
      }

      // Clear out completed transfer tasks
      foreach (var task in m_Transfers.Keys.Where(x => x.Task.IsCompleted &&
                                                       x.Task.Exception != null)) {
        Debug.LogException(task.Task.Exception);
      }

      var toRemove = m_Transfers.Keys.Where(x => x.Task.IsCompleted).ToArray();
      foreach (var transfer in toRemove) {
        m_BytesTransferred += transfer.BytesTransferred;
        m_Transfers.TryRemove(transfer, out _);
        string fileName = Path.Combine(transfer.Item.AbsoluteLocalPath, transfer.Item.Name);
      }

      // Kick off transfers in empty slots
      while (m_Transfers.Count < 4 && m_ToTransfer.Count > 0) {
        var item = m_ToTransfer.Dequeue();
        m_Transfers.TryAdd(new Transfer(this, item), null);
      }

      if (m_Transfers.IsEmpty && m_ToTransfer.Count == 0) {
        m_TotalBytesToTransfer = 0;
        m_PreviousTotalBytesToTransfer = 0;
        m_BytesTransferred = 0;
      }
    }
  }

  private async Task UploadItemAsync(Transfer transfer, CancellationToken token) {
    var item = transfer.Item;
    string path = Path.Combine(item.AbsoluteLocalPath, item.Name);
    FileInfo fileInfo = new FileInfo(path);
    var modified = fileInfo.LastWriteTime;
    var metadata = new DriveData.File {
        Name = item.Name,
        ModifiedTime = modified,
        Parents = new[] {item.ParentId},
    };
    switch (Path.GetExtension(item.Name)) {
      case ".tilt":
        metadata.MimeType = "application/octet-stream";
        metadata.ContentHints = await CreateTiltFileContentHintsAsync(path);
        break;
      case ".jpg":
      case ".jpeg":
        metadata.MimeType = "image/jpeg";
        break;
      case ".png":
        metadata.MimeType = "image/png";
        break;
      case ".obj":
      case ".fbx":
      case ".gltf":
      case ".glb":
        metadata.MimeType = "application/octet-stream";
        break;
      case ".mp4":
      case ".m4v":
        metadata.MimeType = "video/mp4";
        break;
      case ".avi":
        metadata.MimeType = "video/x-msvideo";
        break;
      case ".mov":
        metadata.MimeType = "video/quicktime";
        break;
      case ".3gp":
        metadata.MimeType = "video/3gpp";
        break;
    }

    try {
      using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
        if (item.FileId == null) {
          await m_DriveAccess.UploadFileAsync(metadata, stream, token, transfer);
        } else {
          metadata.Id = item.FileId;
          await m_DriveAccess.UpdateFileAsync(metadata, stream, token, transfer);
        }
      }
    } catch (IOException ex) {
      // Something went wrong with accessing the local file. Ignore for now, hopefully it will work
      // next time around.
      throw new IOException(ex.Message);
    }
    if (DriveIsLowOnSpace && !token.IsCancellationRequested) {
      await ClearAllTransfers();
      Debug.Log(
          $"Backup stopped. User has {m_DriveAccess.DriveFreeSpace} remaining. " +
          $"User must have at least {kMinimumFreeSpace} free to backup.\n" +
          $"At least {kMinimumFreeSpace - m_DriveAccess.DriveFreeSpace} more bytes required.");
      ControllerConsoleScript.m_Instance.AddNewLine(
          "Google Drive low space warning! Drive backup stopped.", bNotify:true);
    }
    if (Path.GetExtension(path) == ".tilt") {
      var driveSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Drive);
      if (item.FileId == null) {
        driveSet.NotifySketchCreated(path);
      } else {
        driveSet.NotifySketchChanged(path);
      }
    }
  }

  private async Task<DriveData.File.ContentHintsData> CreateTiltFileContentHintsAsync(string path) {
    var hints = new DriveData.File.ContentHintsData();
    var fileInfo = new DiskSceneFileInfo(path);
    using (var thumbStream = fileInfo.GetReadStream(TiltFile.FN_THUMBNAIL)) {
      var thumbBytes = new byte[thumbStream.Length];
      int read = await thumbStream.ReadAsync(thumbBytes, 0, thumbBytes.Length);
      if (read != thumbBytes.Length) {
        return null;
      }

      // The thumbnail has to be encoded as URL-safe Base64, which is not the Base64 that C# encodes
      // to. (RFC 4648 section 5). This section converts it to be url-safe.
      byte[] base64 = Encoding.ASCII.GetBytes(Convert.ToBase64String(thumbBytes));
      for (int i = 0; i < base64.Length; ++i) {
        if (base64[i] == (byte)'+') {
          base64[i] = (byte) '-';
        } else if (base64[i] == (byte) '/') {
          base64[i] = (byte) '_';
        }
      }

      hints.Thumbnail = new DriveData.File.ContentHintsData.ThumbnailData {
          Image = Encoding.ASCII.GetString(base64),
          MimeType = TiltFile.THUMBNAIL_MIME_TYPE,
      };
    }
    return hints;
  }

  private async Task DownloadItemAsync(Transfer transfer, CancellationToken token) {
    var item = transfer.Item;
    string path = Path.Combine(item.AbsoluteLocalPath, item.Name);
    string tempPath = path + ".partial";
    if (File.Exists(path)) {
      // The following moves the file to the recycling bin, which is safer in case we do the wrong
      // thing in overwriting a file.
      FileUtils.DeleteWithRecycleBin(path);
    }
    try {
      using (var stream = new FileStream(tempPath, FileMode.Create)) {
        await m_DriveAccess.DownloadFileAsync(item.FileId, stream, token, transfer);
      }
      File.Move(tempPath, path);
      File.SetLastWriteTime(path, item.LastModified);
    } catch (Exception) {
      // If there's an exception partway through - delete the partial file.
      if (File.Exists(path)) {
        File.Delete(path);
      }
      throw;
    }
    if (Path.GetExtension(path) == ".tilt") {
      if (item.Overwrite) {
        SketchCatalog.m_Instance.NotifyUserFileChanged(path);
      } else {
        SketchCatalog.m_Instance.NotifyUserFileCreated(path);
      }
    }
  }

  public async Task CancelTransferAsync(string filename) {
    if (!Initialized) {
      return;
    }
    string name = Path.GetFileName(filename);
    string path = Path.GetDirectoryName(filename);
    var transfer = m_Transfers.Keys.FirstOrDefault(x => x.Item.Name == name &&
                                                        x.Item.AbsoluteLocalPath == path);
    if (transfer != null) {
      transfer.TaskAndCts.Cancel();
      try {
        await transfer.Task;
      } catch (OperationCanceledException) {}
    }
  }
}
} // namespace TiltBrush
