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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using DriveData = Google.Apis.Drive.v3.Data;
using static TiltBrush.DriveAccess;

namespace TiltBrush {
/// A sketchset made up of the sketches stored on Google drive, excepting the ones that are copies
/// of the sketches on the local device.
public class GoogleDriveSketchSet : SketchSet {

  public class GoogleDriveFileInfo : SceneFileInfo {
    private const string kCachePath = "DriveCache";
    private DriveData.File m_File;
    private Texture2D m_Thumbnail;
    private bool m_AbortLoad;
    private string m_FileName;
    private TiltFile m_TiltFile;
    private FileStream m_DownloadStream;
    private string m_SourceId;  // If this is a derivative work of a poly asset, that asset id.
    private string m_Source;

    public Texture2D Thumbnail => m_Thumbnail;

    public FileInfoType InfoType => FileInfoType.Cloud;

    public string HumanName => m_File?.Name ?? "Untitled";

    public bool Valid => true;

    public bool Available => m_TiltFile != null;

    public string FullPath => Path.Combine(App.UserSketchPath(), HumanName);

    public bool Exists => true;

    public bool ReadOnly => true;

    public string AssetId => null;

    public string SourceId {
      get { return m_SourceId; }
      set { m_SourceId = value; }
    }

    public int? TriangleCount => null;

    public float Progress {
      get {
        if (m_DownloadStream == null) {
          return 1f;
        }
        return m_DownloadStream.Position / (float)m_File.Size.Value;
      }
    }

    public DateTime LastModifiedTime => m_File.ModifiedTime.Value;

    public string Source => m_Source;

    public string Description => m_File.Description;

    private string CachePath => Path.Combine(Application.temporaryCachePath, kCachePath);

    public GoogleDriveFileInfo(DriveData.File file, string source) {
      m_File = file;
      m_FileName = Path.Combine(CachePath, m_File.Id + ".tilt");
      if (File.Exists(m_FileName)) {
        var fileInfo = new FileInfo(m_FileName);
        if (DriveAccess.IsNewer(m_File, fileInfo)) {
          File.Delete(m_FileName);
        } else {
          m_TiltFile = new TiltFile(m_FileName);
        }
      }

      m_Source = source;
    }

    public void Delete() {
      throw new NotImplementedException();
    }

    public bool IsHeaderValid() {
      return true; // TODO
    }

    public Stream GetReadStream(string subfileName) {
      return m_TiltFile.GetReadStream(subfileName);
    }

    public IEnumerator LoadThumbnail() {
      UnityWebRequest request = UnityWebRequestTexture.GetTexture(m_File.ThumbnailLink,
                                                                  nonReadable: true);
      var operation = request.SendWebRequest();
      m_AbortLoad = false;
      while (!operation.isDone && !m_AbortLoad) {
        yield return null;
      }
      if (m_AbortLoad) { yield break;}
      if (!request.isNetworkError) {
        m_Thumbnail = DownloadHandlerTexture.GetContent(request);
      }
      // TODO: show broken texture url here?
    }

    public void UnloadThumbnail() {
      m_AbortLoad = true;
      UnityEngine.Object.Destroy(m_Thumbnail);
      m_Thumbnail = null;
    }

    public async Task DownloadAsync(CancellationToken token) {
      Directory.CreateDirectory(CachePath);
      using (m_DownloadStream = new FileStream(m_FileName, FileMode.Create)) {
        try {
          await Retry(() => App.DriveAccess.DownloadFileAsync(m_File, m_DownloadStream, token));
        } catch (OperationCanceledException) {
          m_DownloadStream.Close();
          File.Delete(m_FileName);
        } finally {
          m_DownloadStream = null;
        }
      }
      File.SetLastWriteTime(m_FileName, m_File.ModifiedTime.Value);
      m_TiltFile = new TiltFile(m_FileName);
    }
  }


  private bool m_Refreshing;
  private GoogleDriveFileInfo[] m_Sketches;
  private Coroutine m_ThumbnailLoadingCoroutine;
  private bool m_Changed;

  public SketchSetType Type => SketchSetType.Drive;

  public bool IsReadyForAccess => m_Sketches != null;

  public bool IsActivelyRefreshingSketches => m_Refreshing || App.DriveAccess.Initializing;

  public bool RequestedIconsAreLoaded => m_ThumbnailLoadingCoroutine == null;

  public int NumSketches => m_Sketches?.Length ?? 0;

  public void Init() {
    App.DriveAccess.OnReadyChanged += OnDriveEnabledChanged;
    EnumerateTiltFilesWhenReady();
  }

  public bool IsSketchIndexValid(int iIndex) {
    return iIndex >= 0 && iIndex < NumSketches;
  }

  public void RequestOnlyLoadedMetadata(List<int> requests) {
    // Get rid of existing icons
    for (int i = 0; i < NumSketches; ++i) {
      m_Sketches[i].UnloadThumbnail();
    }
    Resources.UnloadUnusedAssets();

    if (m_ThumbnailLoadingCoroutine != null) {
      App.Instance.StopCoroutine(m_ThumbnailLoadingCoroutine);
      m_Refreshing = false;
    }

    m_ThumbnailLoadingCoroutine = App.Instance.StartCoroutine(LoadThumbnailsCoroutine(requests));
  }

  public bool GetSketchIcon(int i, out Texture2D icon, out string[] authors,
                            out string description) {
    if (i < 0 || i >= NumSketches) {
      icon = null;
      authors = null;
      description = null;
      return false;
    }
    var sketch = m_Sketches[i];
    icon = sketch.Thumbnail;
    authors = new[] {$"From {sketch.Source}"};
    description = string.IsNullOrEmpty(sketch.Description) ? null : sketch.Description;
    return icon != null;
  }

  public SceneFileInfo GetSketchSceneFileInfo(int i) {
    if (i < 0 || i >= NumSketches) {
      return null;
    }
    return m_Sketches[i];
  }

  public string GetSketchName(int i) {
    return GetSketchSceneFileInfo(i)?.HumanName;
  }

  public void DeleteSketch(int toDelete) {
    throw new NotImplementedException();
  }

  public void PrecacheSketchModels(int i) {
    if (i >= 0 && i < NumSketches) {
      // TODO: this currently causes the models to also be loaded into memory
      App.PolyAssetCatalog.PrecacheModels(m_Sketches[i], $"GoogleDriveSketchSet {i}");
    }
  }

  public void NotifySketchCreated(string fullpath) {
    m_Changed = true;
  }

  public void NotifySketchChanged(string fullpath) {
    m_Changed = true;
  }

  public void RequestRefresh() {
    // nothing
  }

  public void Update() {
    if (!App.GoogleIdentity.LoggedIn || !App.GoogleUserSettings.Initialized) {
      return;
    }
    if (!m_Refreshing && m_Changed) {
      m_Changed = false;
      EnumerateTiltFilesAsync().AsAsyncVoid();
    }
  }

  public event Action OnChanged;

  public event Action OnSketchRefreshingChanged;

  private void OnDriveEnabledChanged() {
    if (App.DriveSync.SyncEnabled) {
      EnumerateTiltFilesWhenReady();
      OnSketchRefreshingChanged?.Invoke();
    } else {
      if (m_Sketches != null && m_Sketches.Length != 0) {
        foreach (var sketch in m_Sketches) {
          sketch.UnloadThumbnail();
        }
        m_Sketches = null;
        OnChanged?.Invoke();
      }
    }
  }

  /// This will enumerate tilt files if the Drive access is ready - but otherwise will add itself
  /// the DriveAccess.OnReadyChanged event to get called when that happens. It removes itself from
  /// the event afterwards. Multiple calls will only result in a single enumeration.
  private void EnumerateTiltFilesWhenReady() {
    if (App.DriveAccess.Ready) {
      if (!m_Refreshing) {
        EnumerateTiltFilesAsync().AsAsyncVoid();
      }
      App.DriveAccess.OnReadyChanged -= EnumerateTiltFilesWhenReady;
    } else {
      App.DriveAccess.OnReadyChanged += EnumerateTiltFilesWhenReady;
    }
  }

  private async Task EnumerateTiltFilesAsync() {
    m_Refreshing = true;
    OnSketchRefreshingChanged?.Invoke();
    try {
      // Gets all the 'Sketches' folders within each device folder on Google Drive
      List<DriveData.File> deviceFolders =
          (await Retry(() => App.DriveAccess.GetFoldersInFolderAsync(
              App.GoogleUserSettings.DriveSyncFolderId, CancellationToken.None))).ToList();

      var sketchTasks = deviceFolders
          .Select(x => EnumerateTiltFilesForDevice(x, CancellationToken.None)).ToArray();
      await Task.WhenAll(sketchTasks);
      // If the sketch is a backup of something that came from the local machine, only show it if
      // the file is no longer present on the local machine.
      var sketchList = new List<GoogleDriveFileInfo>();
      for (int i = 0; i < deviceFolders.Count; ++i) {
        if (deviceFolders[i].Id == App.DriveAccess.DeviceFolder) {
          sketchList.AddRange(sketchTasks[i].Result.Where(x => !File.Exists(x.FullPath)));
        } else {
          sketchList.AddRange(sketchTasks[i].Result);
        }
      }
      m_Sketches = sketchList.OrderByDescending(x => x.LastModifiedTime).ToArray();
    } finally {
      await new WaitForUpdate();
      m_Refreshing = false;
      OnSketchRefreshingChanged?.Invoke();
      OnChanged?.Invoke();
    }
  }

  private async Task<IEnumerable<GoogleDriveFileInfo>> EnumerateTiltFilesForDevice(
      DriveData.File folder, CancellationToken token) {
    var sketchFolder =
        await Retry(() => App.DriveAccess.GetFolderAsync("Sketches", folder.Id, token));
    if (sketchFolder == null) {
      return Enumerable.Empty<GoogleDriveFileInfo>();
    }
    var files = await Retry(() => App.DriveAccess.GetFilesInFolderAsync(sketchFolder.Id, token));
    var tiltFiles = files.Where(x => Path.GetExtension(x.Name) == ".tilt");
    return tiltFiles.Select(x => new GoogleDriveFileInfo(x, folder.Name));
  }

  private IEnumerator<Null> LoadThumbnailsCoroutine(List<int> requests) {
    if (requests.Count == 0) {
      yield break;
    }
    var loadingCoroutines = requests.Select(x => m_Sketches[x].LoadThumbnail()).ToArray();

    // Stagger kicking off each loading coroutine by a frame
    foreach (var coroutine in loadingCoroutines) {
      coroutine.MoveNext();
      yield return null;
    }

    bool stillLoading;
    do {
      stillLoading = false;
      for (int i = 0; i < loadingCoroutines.Length; ++i) {
        var coroutine = loadingCoroutines[i];
        if (coroutine == null) {
          continue;
        }
        if (coroutine.MoveNext()) {
          stillLoading = true;
        } else {
          loadingCoroutines[i] = null;
        }
      }
      yield return null;
    } while (stillLoading);
    m_ThumbnailLoadingCoroutine = null;
  }
}
} // namespace TiltBrush
