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
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush {

/// Back-end for cloud upload
/// These values might be serialized into prefabs; do not change them!
/// Make serializable so they don't get mangled by obfuscation.
[Serializable]
public enum Cloud {
  None,
  Poly,
  Sketchfab
}

[Serializable]
public enum VrAssetFormat {
  Unknown,
  TILT,
  GLTF,
  GLTF2,
}

public class VrAssetService : MonoBehaviour {
  // Constants

  const string kGltfName = "sketch.gltf";

  public const string kApiHost = "https://poly.googleapis.com";
  private const string kAssetLandingPage = "https://vr.google.com/sketches/uploads/publish/";

  private const string kListAssetsUri    = "/v1/assets";
  private const string kUserAssetsUri    = "/v1/users/me/assets";
  private const string kUserLikesUri     = "/v1/users/me/likedassets";
  private const string kGetVersionUri    = "/$discovery/rest?version=v1";

  public static string kPolyApiKey => App.Config.GoogleSecrets?.ApiKey;

  public const string kCreativeCommonsLicense = "CREATIVE_COMMONS_BY";

  // Poly API used by Tilt Brush.
  // If Poly doesn't support this version, don't try to talk to Poly and prompt the user to upgrade.
  private const string kPolyApiVersion = "v1";

  /// Change-of-basis transform
  public static readonly TrTransform kPolyFromUnity;

  private static Dictionary<string, string> kGltfMimetypes = new Dictionary<string, string> {
    { ".gltf", "model/gltf+json" },
    { ".bin",  "application/octet-stream" },
    { ".glsl", "text/plain" },
    { ".bmp",  "image/bmp" },
    { ".jpeg", "image/jpeg" },
    { ".jpg",  "image/jpeg" },
    { ".png",  "image/png" },
    { ".tilt", "application/octet-stream" }
  };

  // For progress reporting
  private enum UploadStep {
    CreateGltf,
    CreateTilt,
    ZipElements,
    UploadElements,
    UpdateAssetData,
    Done
  }

  // These are progress values at the start of each step.
  // TODO(b/146892613): have a different set for Poly vs Sketchfab?
  private static double[] kProgressSteps = {
      0.01, // UploadStep.CreateGltf -- progress > 0 means we have begun
      0.25, // UploadStep.CreateTilt
      0.3,  // UploadStep.ZipElements
      0.5,  // UploadStep.UploadElements
      0.95, // UploadStep.UpdateAssetData
      1,    // UploadStep.Done
      1     // <Sentinel>
  };

  // Classes and types

  class StreamWithReadProgress : WrappedStream {
    public int TotalRead { get; private set; }
    public StreamWithReadProgress(string filename) {
      SetWrapped(File.OpenRead(filename), ownsStream: true);
    }
    public override int Read(byte[] buffer, int offset, int count) {
      var amountRead = base.Read(buffer, offset, count);
      TotalRead += amountRead;
      return amountRead;
    }
    public override int ReadByte() {
      var amountRead = base.ReadByte();
      TotalRead += amountRead;
      return amountRead;
    }
  }

  /// A bag of information about a pending upload
  class UploadInfo : IProgress<double> {
    // Only one of sourceFile or sourceData is valid
    public readonly FileInfo m_sourceFile;
    public readonly byte[] m_sourceData;
    public readonly string m_remoteName;
    public readonly long m_length;
    public string m_elementId;
    public double m_uploadPercent = 0;

    public UploadInfo(string sourceFile, string remoteName) {
      m_sourceFile = new FileInfo(sourceFile);
      m_remoteName = remoteName;
      m_length = m_sourceFile.Length;
    }

    public UploadInfo(byte[] data, string remoteName) {
      m_sourceData = data;
      m_remoteName = remoteName;
      m_length = data.Length;
    }

    public void SetUploaded(string elementId) {
      m_elementId = elementId;
    }

    /// Allocates and returns a new Stream.
    /// Caller is responsible for disposing of it.
    public Stream OpenStream() {
      if (m_sourceData != null) {
        return new MemoryStream(m_sourceData);
      } else {
        return m_sourceFile.OpenRead();
      }
    }

    void IProgress<double>.Report(double value) {
      m_uploadPercent = value;
    }
  }

  /// Creates and then cleans up a uniquely-named temporary directory
  class TemporaryUploadDirectory : IDisposable {
    public string Value { get; }

    public TemporaryUploadDirectory() {
#if UNITY_EDITOR
      if (App.Config && App.Config.m_DebugUpload) {
        // Delay deleting the directory until the next upload
        string dirName = Path.Combine(Application.temporaryCachePath, "Upload");
        if (Directory.Exists(dirName)) {
          try { Directory.Delete(dirName, true); }
          catch (Exception e) { Debug.LogException(e); }
        }
      }
#endif
      Value = FileUtils.GenerateNonexistentFilename(
          Application.temporaryCachePath, "Upload", "");
      string failureMessage = $"Can't create upload directory: {Value}";
      bool dirCreated = FileUtils.InitializeDirectoryWithUserError(Value, failureMessage);
      if (!dirCreated) {
        throw new VrAssetServiceException(failureMessage);
      }
    }

    public void Dispose() {
#if UNITY_EDITOR
      if (App.Config && App.Config.m_DebugUpload) {
        // Delay deleting the directory until the next upload
        return;
      }
#endif
      if (Directory.Exists(Value)) {
        Directory.Delete(Value, true);
      }
    }
  }

  // Static API

  public static VrAssetService m_Instance;

  // Currently this always returns the standard API host when running unit tests
  private static string ApiHost {
    get {
      string cfg = App.UserConfig?.Sharing.VrAssetServiceHostOverride;
      if (! string.IsNullOrEmpty(cfg)) { return cfg; }
      return kApiHost;
    }
  }

  /// Returns true if Poly would accept a PATCH of the specified asset
  /// from the specified user.
  ///
  /// Pass:
  ///   type -
  ///     Where you found the assetId. Necessary because of some shortcuts
  ///     taken by the implementation.
  ///   userId - Poly user id of the currently-logged-in OAuth user. Get it
  ///     with GetAccountIdAsync().
  public static async Task<bool> IsMutableAssetIdAsync(
      FileInfoType type, string assetId, string userId, string apiHost) {
    if (assetId == null) { return false; }
    // There are too many assumptions here -- for both cloud and disk, the logic
    // should be "is it owned by me and unpublished? then it's mutable"
    if (type == FileInfoType.Cloud) {
      // Assumption: cloud files are immutable.
      // You can't "Like" an unpublished asset; and published assets are immutable.
      return false;
    } else if (type == FileInfoType.Disk) {
      // Assumption: this asset is mutable because it's unlikely for a cloud-based .tilt
      // to be in the user's Sketches/ folder. Local sketches always become un-published
      // and mutable assets when uploaded (remember, publishing makes a copy).
      // If someone grabbed a .tilt from their Poly asset cache and put it in Sketches/
      // that would break this assumption and I'm not sure what would happen.
      if (userId == null) { return false; }
      // It's mutable, but check whether it's mutable by _us_.
      try {
        // The null == null case is handled earlier
        WebRequest request = new WebRequest(
            $"{apiHost}{kListAssetsUri}/{assetId}?key={kPolyApiKey}",
            App.GoogleIdentity, UnityWebRequest.kHttpVerbGET);
        return (await request.SendAsync()).JObject?["accountId"].ToString() == userId;
      } catch (VrAssetServiceException) {
        return false;
      }
    } else {
      throw new InvalidOperationException($"Unknown FileInfoType {type}");
    }
  }

  // Instance API

  static VrAssetService() {
    Matrix4x4 polyFromUnity = AxisConvention.GetFromUnity(AxisConvention.kGltfAccordingToPoly);

    // Provably non-lossy: the mat4 is purely TRS, and the S is uniform
    kPolyFromUnity = TrTransform.FromMatrix4x4(polyFromUnity);
  }

  // Instance API

  [SerializeField] private int m_AssetsPerPage;
  [SerializeField] public float m_SketchbookRefreshInterval;

  private float m_UploadProgress;
  private bool m_LastUploadFailed;
  private string m_LastUploadErrorMessage;
  private string m_LastUploadErrorDetails;
  private bool m_UserCanceledLastUpload;
  private string m_LastUploadCompleteUrl;
  TaskAndCts<(string url, long bytes)> m_UploadTask = null;

  // Poly account id associated with the Google identity
  private string m_PolyAccountId;

  private enum PolyStatus {
    Ok,
    Disabled,
    NoConnection
  }
  private PolyStatus m_PolyStatus;

  public bool Available => m_PolyStatus == PolyStatus.Ok;

  public bool NoConnection => m_PolyStatus == PolyStatus.NoConnection;

  public float UploadProgress => m_UploadProgress;

  public bool LastUploadFailed {
    get { return m_LastUploadFailed; }
  }

  public string LastUploadErrorMessage {
    get { return m_LastUploadErrorMessage; }
  }

  public string LastUploadErrorDetails {
    get { return m_LastUploadErrorDetails; }
  }

  public bool UserCanceledLastUpload {
    get { return m_UserCanceledLastUpload; }
  }

  public string LastUploadCompleteUrl {
    get { return m_LastUploadCompleteUrl; }
  }

  private string AssetLandingPage {
    get {
      string cfg = App.UserConfig.Sharing.VrAssetServiceUrlOverride;
      if (! string.IsNullOrEmpty(cfg)) { return cfg; }
      return kAssetLandingPage;
    }
  }

  // Cannot be an UploadProgress setter because the getter's type is different.
  // pct is how much of that step has been completed.
  private void SetUploadProgress(UploadStep step, double pct) {
    var step0 = (float)kProgressSteps[Mathf.Min(kProgressSteps.Length-1, (int)step  )];
    var step1 = (float)kProgressSteps[Mathf.Min(kProgressSteps.Length-1, (int)step+1)];
    m_UploadProgress = Mathf.Lerp(step0, step1, (float)pct);
  }

  void Awake() {
    m_Instance = this;
  }

  void Start() {
    if (!string.IsNullOrEmpty(App.UserConfig.Sharing.VrAssetServiceHostOverride) ||
        !string.IsNullOrEmpty(App.UserConfig.Sharing.VrAssetServiceUrlOverride)) {
      Debug.LogFormat("Overriding VrAssetService Api Host: {0}  Landing Page: {1}",
                      ApiHost, AssetLandingPage);
    }

    // If auto profiling is enabled, disable automatic Poly downloading.
    if (!App.UserConfig.Profiling.AutoProfile) {
      VerifyPolyConnectionAndCheckApiVersionAsync();
    } else {
      m_PolyStatus = PolyStatus.Disabled;
    }
  }

  /// Consume the result of the previous upload (if any)
  public void ConsumeUploadResults() {
    // Our UI interprets "progress >= 1.0" as "there are results and we must display them!"
    // The UI should really be internally stateful, and only display these results when new results
    // appear that haven't yet been shown to the user. The issue with this design is that there
    // are other consumers of upload results. Thankfully, they are less important and don't
    // care about progress; so the hack is that we only "consume" the progress.
    if (m_UploadProgress >= 1) {
      m_UploadProgress = 0;
    }
  }

  /// If you try and upload a sketch while a sketch is already uploading,
  /// the existing upload will be canceled.
  public async Task UploadCurrentSketchAsync(Cloud backend, bool isDemoUpload) {
    // This function handles most of the setup and cleanup.
    // The heavy lifting is split out into UploadCurrentSketchInternal.

    // Generic to all kinds of failures
    void ReportFailure(string userFriendly, string fullMessage = "") {
      m_LastUploadFailed = true;
      m_LastUploadErrorMessage = userFriendly;
      m_LastUploadErrorDetails = fullMessage;
      ControllerConsoleScript.m_Instance.AddNewLine(LastUploadErrorMessage, skipLog: true);
      ControllerConsoleScript.m_Instance.AddNewLine(LastUploadErrorDetails, skipLog: true);
      AudioManager.m_Instance.PlayUploadCanceledSound(InputManager.Wand.Transform.position);
    }

    Debug.LogFormat("UploadCurrentSketch(demo: {0})", isDemoUpload);

    // Cancel previous upload coroutine if necessary.
    if (m_UploadTask != null) {
      CancelUpload();
      if (await Task.WhenAny(m_UploadTask.Task, Task.Delay(TimeSpan.FromSeconds(5))) !=
          m_UploadTask.Task) {
        // Definitely a bug we should fix; make it noisy because otherwise it just looks
        // like nothing is happening.
        OutputWindowScript.Error("Timed out waiting for canceled upload");
        return;
      }
    }


    m_LastUploadFailed = false;
    m_LastUploadErrorMessage = "";
    m_LastUploadErrorDetails = "";
    m_UserCanceledLastUpload = false;
    m_UploadProgress = 0.0f;

    using (var tempUploadDir = new TemporaryUploadDirectory())
    try {
      if (!isDemoUpload) {
        App.Instance.SetDesiredState(App.AppState.Uploading);
      }
      AudioManager.m_Instance.UploadLoop(true);
      var timer = System.Diagnostics.Stopwatch.StartNew();
      m_UploadTask = new TaskAndCts<(string url, long bytes)>();
      Debug.Assert(backend == Cloud.Sketchfab);
      m_UploadTask.Task = UploadCurrentSketchSketchfabAsync(m_UploadTask.Token, tempUploadDir.Value,
                                              isDemoUpload);
      var (url, totalUploadLength) = await m_UploadTask.Task;
      m_LastUploadCompleteUrl = url;
      ControllerConsoleScript.m_Instance.AddNewLine("Upload succeeded!");
      AudioManager.m_Instance.PlayUploadCompleteSound(InputManager.Wand.Transform.position);
      PanelManager.m_Instance.GetAdminPanel().ActivatePromoBorder(true);
      // Don't auto-open the URL on mobile because it steals focus from the user.
      if (!isDemoUpload && !App.Config.IsMobileHardware && m_LastUploadCompleteUrl != null) {
        // Can't pass a string param because this is also called from mobile GUI
        SketchControlsScript.m_Instance.IssueGlobalCommand(
            SketchControlsScript.GlobalCommands.ViewLastUpload);
      }
    } catch (VrAssetServiceException exception) {
      // "Expected" failures (40x, 50x, etc)
      Debug.LogWarning("UploadCurrentSketch external error");
      Debug.LogException(exception);
      ReportFailure(exception.UserFriendly, exception.Message);
    } catch (OperationCanceledException) {
      m_UserCanceledLastUpload = true;
      ReportFailure("Upload canceled.");
    } catch (Exception exception) {
      // Unexpected failures -- ie, bugs on our part
      Debug.LogError("UploadCurrentSketch internal error");
      ReportFailure("Upload failed.", exception.Message);
      throw;
    } finally {
      // Cleanup
      if (App.CurrentState == App.AppState.Uploading) {
        App.Instance.SetDesiredState(App.AppState.Standard);
      }

      m_UploadTask = null;
      AudioManager.m_Instance.UploadLoop(false);
      // This is how the upload popup knows we're complete
      m_UploadProgress = 1.0f;
    }
  }

  /// Request to cancel the upload -- does not happen synchronously.
  public void CancelUpload() {
    m_UploadTask?.Cancel();
  }

  private async void VerifyPolyConnectionAndCheckApiVersionAsync() {
    m_PolyStatus = await GetPolyStatus();
  }

  private static async Task<PolyStatus> GetPolyStatus() {
    // UserConfig override
    if (App.UserConfig.Flags.DisablePoly ||
        string.IsNullOrEmpty(App.Config.GoogleSecrets?.ApiKey)) {
      return PolyStatus.Disabled;
    }

    string uri = String.Format("{0}{1}", ApiHost, kGetVersionUri);
    try {
      var result = (await new WebRequest(uri, App.GoogleIdentity).SendAsync()).JObject;
      string version = result["version"].Value<string>();
      if (version == kPolyApiVersion) {
        return PolyStatus.Ok;
      } else {
        Debug.LogWarning($"Poly requires API {version} > {kPolyApiVersion}");
        return PolyStatus.Disabled;
      }
    } catch (VrAssetServiceException e) {
      Debug.LogWarning($"Error connecting to Poly: {e}");
      return PolyStatus.NoConnection;
    } catch (Exception e) {
      Debug.LogError($"Internal error connecting to Poly: {e}");
      return PolyStatus.NoConnection;
    }
  }

  /// Returns a writable SceneFileInfo
  private DiskSceneFileInfo GetWritableFile() {
    // hermetic gltf files currently don't work with AccessLevel.PRIVATE
    SceneFileInfo currentFileInfo = SaveLoadScript.m_Instance.SceneFile;

    DiskSceneFileInfo fileInfo;
    if (currentFileInfo.Valid) {
      if (currentFileInfo is DiskSceneFileInfo) {
        fileInfo = (DiskSceneFileInfo)currentFileInfo;
      } else {
        // This is a cloud sketch not saved before
        fileInfo = SaveLoadScript.m_Instance.GetNewNameSceneFileInfo();
      }
    } else {
      // Save as a new file
      fileInfo = SaveLoadScript.m_Instance.GetNewNameSceneFileInfo();
    }
    return fileInfo;
  }

  /// Returns a relative path R such that Join(fromDir, R) refers to toFile, or null on error.
  /// Does not handle ".." paths.
  public static string GetRelativePath(string fromDir, string toFile) {
    // Normalize paths so we can use plain string compares
    var alt = Path.AltDirectorySeparatorChar;
    var standard = Path.DirectorySeparatorChar;
    if (alt != standard) {
      fromDir = fromDir.Replace(alt, standard);
      toFile = toFile.Replace(alt, standard);
    }

    int baseLen = fromDir.Length;
    if (!toFile.StartsWith(fromDir)) { return null; }
    if (toFile.Length <= baseLen) { return null; }
    var sep = toFile[baseLen];
    if (sep != Path.DirectorySeparatorChar) {
      return null;
    }
    return toFile.Substring(baseLen+1);
  }

  private async Task CreateZipFileAsync(
      string zipName, string rootDir, string[] paths,
      CancellationToken token) {
    long totalLength = paths.Aggregate(0L, (acc, elt) => acc + new FileInfo(elt).Length) + 1;
    long read = 1;

    using (var zip = File.OpenWrite(zipName)) {
      using (var archive = new ZipArchive(zip, ZipArchiveMode.Create)) {
        foreach (var path in paths) {
          string archivedName = GetRelativePath(rootDir, path);
          if (archivedName == null) {
            Debug.LogWarning($"Ignoring {path} not under {rootDir}");
            continue;
          }
          ZipArchiveEntry entry = archive.CreateEntry(archivedName);
          using (Stream writer = entry.Open()) {
            using (var reader = new StreamWithReadProgress(path)) {
              var task = reader.CopyToAsync(writer, 0x1_0000, token);
              while (!task.IsCompleted) {
                long prev = reader.TotalRead;
                await Awaiters.NextFrame;
                read += reader.TotalRead - prev;
                SetUploadProgress(UploadStep.ZipElements, read / (double)(totalLength));
              }
              await task;
            }
          }
        }
      }
    }
  }

  private async Task<(string, long)> UploadCurrentSketchSketchfabAsync(
      CancellationToken token, string tempUploadDir, bool _) {
    DiskSceneFileInfo fileInfo = GetWritableFile();

    SetUploadProgress(UploadStep.CreateGltf, 0);
    // Do the glTF straight away as it relies on the meshes, not the stroke descriptions.
    string gltfFile = Path.Combine(tempUploadDir, kGltfName);
    var exportResults = await OverlayManager.m_Instance.RunInCompositorAsync(
        OverlayType.Export, fadeDuration: 0.5f,
        action: () => new ExportGlTF().ExportBrushStrokes(
            gltfFile,
            AxisConvention.kGltf2, binary: false, doExtras: false,
            includeLocalMediaContent: true, gltfVersion: 2,
            // Sketchfab doesn't support absolute texture URIs
            selfContained: true));
    if (!exportResults.success) {
      throw new VrAssetServiceException("Internal error creating upload data.");
    }

    // Construct options to set the background color to the current environment's clear color.
    Color bgColor = SceneSettings.m_Instance.CurrentEnvironment.m_RenderSettings.m_ClearColor;
    SketchfabService.Options options = new SketchfabService.Options();
    options.SetBackgroundColor(bgColor);

    // TODO(b/146892613): we're not uploading this at the moment. Should we be?
    // If we don't, we can probably remove this step...?
    SetUploadProgress(UploadStep.CreateTilt, 0);
    await CreateTiltForUploadAsync(fileInfo);
    token.ThrowIfCancellationRequested();

    // Create a copy of the .tilt file in tempUploadDir.
    string tempTiltPath = Path.Combine(tempUploadDir, "sketch.tilt");
    File.Copy(fileInfo.FullPath, tempTiltPath);

    // Collect files into a .zip file, including the .tilt file.
    string zipName = Path.Combine(tempUploadDir, "archive.zip");
    var filesToZip = exportResults.exportedFiles.ToList().Append(tempTiltPath);
    await CreateZipFileAsync(zipName, tempUploadDir, filesToZip.ToArray(), token);
    var uploadLength = new FileInfo(zipName).Length;

    var service = new SketchfabService(App.SketchfabIdentity);
    var progress = new Progress<double>(d => SetUploadProgress(UploadStep.UploadElements, d));
    var response = await service.CreateModel(
        fileInfo.HumanName, zipName, progress, token, options, tempUploadDir);
    // TODO(b/146892613): return the UID and stick it into the .tilt file?
    // Or do we not care since we aren't recording provenance and remixing

    // TODO(b/146892613): figure out this flow
    // response.uri is not very useful; it is an API uri that gives you json of asset details.
    // Also, the 3d-models URI might show that the asset is still processing. We can poll their
    // API and find out when it's done and pop up the window then?
    string uri = $"{SketchfabService.kModelLandingPage}{response.uid}";
    return (uri, uploadLength);
  }

  /// Helper for UploadCurrentSketchXxxAsync
  /// Writes the sketch to the passed fileInfo and returns a sketch thumbnail.
  private async Task<byte[]> CreateTiltForUploadAsync(DiskSceneFileInfo fileInfo) {
    // Create and save snapshot.
    SetUploadProgress(UploadStep.CreateTilt, 0);
    SketchControlsScript.m_Instance.GenerateReplacementSaveIcon();
    SketchSnapshot snapshot = await SaveLoadScript.m_Instance.CreateSnapshotWithIconsAsync();
    snapshot.AssetId = fileInfo.AssetId;  // FileInfo and snapshot must match
    await SaveLoadScript.m_Instance.SaveSnapshot(fileInfo, snapshot: snapshot);
    if (!File.Exists(fileInfo.FullPath)) {
      string exceptionMessage = "Internal error uploading .tilt.";
      if (SaveLoadScript.m_Instance.LastWriteSnapshotError != null) {
        exceptionMessage += " Error: " + SaveLoadScript.m_Instance.LastWriteSnapshotError;
      } else {
        exceptionMessage += " No error message";
      }

      throw new VrAssetServiceException(exceptionMessage);
    }

    byte[] thumbnail = SaveLoadScript.m_Instance.GetLastThumbnailBytes();
    if (thumbnail == null) {
      thumbnail = FileSketchSet.ReadThumbnail(fileInfo) ?? new byte[0];
    }

    return thumbnail;
  }

  public AssetGetter GetAsset(string assetId, VrAssetFormat type, string reason) {
    string uri = String.Format("{0}{1}/{2}?key={3}", ApiHost, kListAssetsUri, assetId, kPolyApiKey);
    return new AssetGetter(uri, assetId, type, reason);
  }

  public AssetLister ListAssets(SketchSetType type) {
    string filter = null;
    string errorMessage = null;
    switch (type) {
    case SketchSetType.Liked:
      if (!App.GoogleIdentity.LoggedIn) {
        return null;
      }
      filter = $"{kUserLikesUri}?format=TILT&orderBy=LIKED_TIME&key={kPolyApiKey}";
      errorMessage = "Failed to access your liked sketches.";
      break;
    case SketchSetType.Curated:
      if (string.IsNullOrEmpty(kPolyApiKey)) {
        return null;
      }
      filter = $"{kListAssetsUri}?format=TILT&curated=true&orderBy=NEWEST&key={kPolyApiKey}";
      errorMessage = "Failed to access featured sketches.";
      break;
    }

    string uri = $"{ApiHost}{filter}&pageSize={m_AssetsPerPage}";
    return new AssetLister(uri, errorMessage);
  }

  // Get a specific sketch and insert it into the listed sketches at the specified index.
  public IEnumerator<object> InsertSketchInfo(
      string assetId, int index, List<PolySceneFileInfo> infos) {
    string uri = String.Format("{0}{1}/{2}?key={3}", ApiHost, kListAssetsUri, assetId, kPolyApiKey);
    WebRequest request = new WebRequest(uri, App.GoogleIdentity, UnityWebRequest.kHttpVerbGET);
    using (var cr = request.SendAsync().AsIeNull()) {
      while (!request.Done) {
        try {
          cr.MoveNext();
        } catch (VrAssetServiceException e) {
          Debug.LogException(e);
          Debug.LogError("Failed to fetch sketch " + assetId);
          yield break;
        }
        yield return cr.Current;
      }
    }

    Future<JObject> f = new Future<JObject>(() => JObject.Parse(request.Result));
    JObject json;
    while (!f.TryGetResult(out json)) { yield return null; }
    infos.Insert(index, new PolySceneFileInfo(json.Root));
  }

  public AssetLister ListAssets(PolySetType type) {
    string uri = null;
    switch (type) {
    case PolySetType.Liked:
      uri = $"{ApiHost}{kUserLikesUri}?format=GLTF2&orderBy=LIKED_TIME&pageSize={m_AssetsPerPage}";
      break;
    case PolySetType.User:
      uri = $"{ApiHost}{kUserAssetsUri}?format=GLTF2&orderBy=NEWEST&pageSize={m_AssetsPerPage}";
      break;
    case PolySetType.Featured:
      uri = $"{ApiHost}{kListAssetsUri}?key={kPolyApiKey}" + 
            $"&format=GLTF2&curated=true&orderBy=NEWEST&pageSize={m_AssetsPerPage}";
      break;
    }
    return new AssetLister(uri, "Failed to connect to Poly.");
  }

  // Download a tilt file to a temporary file and load it
  public IEnumerator LoadTiltFile(string id) {
    string path = Path.GetTempFileName();
    string uri = String.Format("{0}{1}/{2}?key={3}", ApiHost, kListAssetsUri, id, kPolyApiKey);
    WebRequest request = new WebRequest(uri, App.GoogleIdentity, UnityWebRequest.kHttpVerbGET);
    using (var cr = request.SendAsync().AsIeNull()) {
      while (!request.Done) {
        try {
          cr.MoveNext();
        } catch (VrAssetServiceException) {
          yield break;
        }
        yield return cr.Current;
      }
    }
    JObject json = JObject.Parse(request.Result);
    var info = new PolySceneFileInfo(json);
    using (UnityWebRequest www = UnityWebRequest.Get(info.TiltFileUrl)) {
      yield return www.SendWebRequest();
      while (! www.downloadHandler.isDone) { yield return null; }
      FileStream stream = File.Create(path);
      byte[] data = www.downloadHandler.data;
      stream.Write(data, 0, data.Length);
      stream.Close();
    }

    SketchControlsScript.m_Instance.IssueGlobalCommand(
      SketchControlsScript.GlobalCommands.LoadNamedFile, sParam:path);
    File.Delete(path);
  }
}

}  // namespace TiltBrush
