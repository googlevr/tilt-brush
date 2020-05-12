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
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json.Linq;

namespace TiltBrush {

// TODO: Specify tag for which sketches to query (curated, liked etc.)
public class PolySketchSet : SketchSet {

  const int kDownloadBufferSize = 1024 * 1024; // 1MB

  // Downloading is handled by PolySketchSet which will set the local paths

  private class PolySketch : Sketch {
    // This value holds the count of sketches that were downloaded by the sketch set
    // before this one.  It's used during our sort to retain order from Poly, while
    // allowing a custom sort on top.
    public int m_DownloadIndex;
    private PolySceneFileInfo m_FileInfo;
    private Texture2D m_Icon;

    public SceneFileInfo SceneFileInfo {
      get { return m_FileInfo; }
    }

    public string[] Authors {
      get {
        return m_FileInfo.Author != null
          ? new string[] { m_FileInfo.Author }
          : new string[] {};
      }
    }

    public Texture2D Icon {
      get { return m_Icon; }
      set { m_Icon = value; }
    }

    public bool IconAndMetadataValid {
      get { return m_Icon != null; }
    }

    public PolySketch(PolySceneFileInfo info) {
      m_FileInfo = info;
    }

    public void UnloadIcon() {
      UnityEngine.Object.Destroy(m_Icon);
      m_Icon = null;
    }

    // Not part of the interface
    public PolySceneFileInfo PolySceneFileInfo {
      get { return m_FileInfo; }
    }
  }

  private const string kIntroSketchAssetId = "agqmaia62KE";
  private const int kIntroSketchIndex = 1;

  private List<PolySketch> m_Sketches;
  private Dictionary<string, PolySketch> m_AssetIds;
  private int m_TotalCount = 0;
  private VrAssetService m_AssetService;
  private MonoBehaviour m_Parent;
  private bool m_Ready;
  private bool m_RefreshRequested;
  private bool m_NeedsLogin;
  private bool m_LoggedIn;
  private bool m_ActivelyRefreshingSketches;
  private int m_MaximumSceneTriangles;

  private SketchSetType m_Type;
  private string m_CacheDir;
  private Coroutine m_RefreshCoroutine;
  private float m_CooldownTimer;
  private List<int> m_RequestedIcons = new List<int>();
  private Coroutine m_TextureLoaderCoroutine;

  public SketchSetType Type { get { return m_Type; } }

  public VrAssetService VrAssetService {
    set { m_AssetService = value; }
  }

  public PolySketchSet(MonoBehaviour parent, SketchSetType type, int maxSceneTriangles,
                        bool needsLogin = false) {
    m_Parent = parent;
    m_Sketches = new List<PolySketch>();
    m_AssetIds = new Dictionary<string, PolySketch>();
    m_Ready = false;
    m_RefreshRequested = false;
    m_Type = type;
    m_NeedsLogin = needsLogin;
    m_MaximumSceneTriangles = maxSceneTriangles;
  }

  public void Init() {
    VrAssetService = VrAssetService.m_Instance;
    m_LoggedIn = false;
    m_RefreshRequested = true;
    m_CooldownTimer = VrAssetService.m_Instance.m_SketchbookRefreshInterval;
  }

  public bool IsReadyForAccess {
    get { return m_Ready; }
  }

  public bool IsActivelyRefreshingSketches {
    get { return m_ActivelyRefreshingSketches; }
    private set {
      m_ActivelyRefreshingSketches = value;
      OnSketchRefreshingChanged();
    }
  }

  public bool RequestedIconsAreLoaded {
    get { return false; }
  }

  public int NumSketches {
    get { return m_TotalCount; }
  }

  public void NotifySketchCreated(string fullpath) { }

  public void NotifySketchChanged(string fullpath) { }

  public void RequestRefresh() {
    m_RefreshRequested = true;
  }

  public bool IsSketchIndexValid(int index) {
    return index >= 0 && index < m_TotalCount;
  }

  public void RequestOnlyLoadedMetadata(List<int> requests) {
    // Stop any current texture loading
    if (m_TextureLoaderCoroutine != null) {
      m_Parent.StopCoroutine(m_TextureLoaderCoroutine);
      m_TextureLoaderCoroutine = null;
    }
    // Nuke textures on all icons
    foreach (var sketch in m_Sketches) {
      sketch.UnloadIcon();
    }
    m_RequestedIcons.Clear();
    m_RequestedIcons.AddRange(requests);
    m_TextureLoaderCoroutine = m_Parent.StartCoroutine(TextureLoaderCoroutine());
  }

  public bool GetSketchIcon(int iSketchIndex, out Texture2D icon, out string[] authors,
                            out string description) {
    description = null;
    if (iSketchIndex >= m_Sketches.Count) {
      icon = null;
      authors = null;
      return false;
    }
    PolySketch sketch = m_Sketches[iSketchIndex];
    icon = sketch.Icon;
    authors = sketch.Authors;
    return icon != null;
  }

  public SceneFileInfo GetSketchSceneFileInfo(int i) {
    return i < m_Sketches.Count ? m_Sketches[i].SceneFileInfo : null;
  }

  public string GetSketchName(int i) {
    return i < m_Sketches.Count ? m_Sketches[i].SceneFileInfo.HumanName : null;
  }

  public void PrecacheSketchModels(int i) {
    if (i < m_Sketches.Count) {
      App.PolyAssetCatalog.PrecacheModels(m_Sketches[i].SceneFileInfo, $"PolySketchSet {i}");
    }
  }

  public void DeleteSketch(int toDelete) {
    throw new NotImplementedException();
  }

  public void Update() {
    if (!VrAssetService.m_Instance.Available) {
      return;
    }

    if (m_NeedsLogin) {
      bool loggedIn = App.GoogleIdentity.LoggedIn;
      if (loggedIn != m_LoggedIn) {
        m_LoggedIn = loggedIn;
        if (!loggedIn) {
          if (m_RefreshCoroutine != null) {
            m_Parent.StopCoroutine(m_RefreshCoroutine);
            IsActivelyRefreshingSketches = false;
            m_RefreshCoroutine = null;
            m_Sketches.Clear();
            m_AssetIds.Clear();
            m_TotalCount = 0;
            m_CacheDir = null;
            OnChanged();
          }
        } else {
          // Lie about refreshing.  The refresh coroutine will happen in time, but
          // for the user, we want them to know something will happen [soon].
          IsActivelyRefreshingSketches = true;
          m_RefreshCoroutine = m_Parent.StartCoroutine(PeriodicRefreshCoroutine());
        }
      }
    } else if (m_RefreshRequested && m_RefreshCoroutine == null) {
      m_RefreshCoroutine = m_Parent.StartCoroutine(PeriodicRefreshCoroutine());
    }
  }

  public event Action OnChanged = delegate { };

  public event Action OnSketchRefreshingChanged = delegate { };

  private IEnumerator PeriodicRefreshCoroutine() {
    while (true) {
      if (m_RefreshRequested) {
        yield return Refresh();
        m_RefreshRequested = false;

        // Don't poll the showcase
        if (Type == SketchSetType.Curated) {
          yield break;
        }
        m_CooldownTimer = VrAssetService.m_Instance.m_SketchbookRefreshInterval;
      } else {
        yield return null;
      }
      while (m_CooldownTimer > 0.0f) {
        m_CooldownTimer -= Time.deltaTime;
        yield return null;
      }
    }
  }

  // Update our state from the cloud.
  // There are three stages to this, each with a separate coroutine:
  // 1: Pull all the metadata from the server and populate m_Sketches
  // If there are changes:
  //   2: Download any thumbnails and tilt files we don't already have
  //   3: Clean up any cached files that are no longer referenced
  private IEnumerator Refresh() {
    if (m_NeedsLogin && !m_LoggedIn) {
      m_Sketches.Clear();
      m_AssetIds.Clear();
      m_TotalCount = 0;
      m_CacheDir = null;
      OnChanged();
      yield break;
    }

    if (m_CacheDir == null) {
      m_CacheDir = CacheDir(Type);
      try {
        Directory.CreateDirectory(m_CacheDir);
      } catch (Exception ex) {
        // Most of the system exceptions returned by CreateDirectory are just things the user needs
        // to fix, and may well be sorted by reinstalling. If it's an ArgumentNullExeption or not a
        // SystemException, we need to know about it, so log the exception.
        if (!(ex is SystemException) || ex is ArgumentNullException) {
          Debug.LogException(ex);
        }
        ControllerConsoleScript.m_Instance.AddNewLine(
            $"Error creating cache directory. Try restarting {App.kAppDisplayName}.\n" + 
            $"If this error persists, try reinstalling {App.kAppDisplayName}.", true);
        yield break;
      }
    }

    // While we're fetching metadata, hold a flag so we can message state in the UI.
    IsActivelyRefreshingSketches = true;
    yield return PopulateSketchesCoroutine();
    IsActivelyRefreshingSketches = false;
  }

  // Fetch asset metadata from server and populate m_Sketches
  private IEnumerator PopulateSketchesCoroutine() {
    // Replacement data structures that will be populated with incoming data.  These
    // temporary structures will be copied to the "live" structures in chunks, so
    // beware of modifications that may reference incomplete data.
    List<PolySketch> sketches = new List<PolySketch>();
    Dictionary<string, PolySketch> assetIds = new Dictionary<string, PolySketch>();

    bool fromEmpty = m_AssetIds.Count == 0;
    int loadSketchCount = 0;

    AssetLister lister = null;
    List<PolySceneFileInfo> infos = new List<PolySceneFileInfo>();

    // If we don't have a connection to Poly and we're querying the Showcase, use
    // the json metadatas stored in resources, instead of trying to get them from Poly.
    if (VrAssetService.m_Instance.NoConnection && m_Type == SketchSetType.Curated) {
      TextAsset[] textAssets =
          Resources.LoadAll<TextAsset>(SketchCatalog.kDefaultShowcaseSketchesFolder);
      for (int i = 0; i < textAssets.Length; ++i) {
        JObject jo = JObject.Parse(textAssets[i].text);
        infos.Add(new PolySceneFileInfo(jo));
      }
    } else {
      lister = m_AssetService.ListAssets(Type);
    }

    bool changed = false;
    int pagesFetched = 0;
    while (lister == null || lister.HasMore || assetIds.Count == 0) {
      if (sketches.Count >= 180) {
        break; // Don't allow the sketchbook to become too big.
      }
      
      // lister will be null if we can't connect to Poly and we've pre-populated infos
      // with data from Resources.
      if (lister != null) {
        using (var cr = lister.NextPage(infos)) {
          while (true) {
            try {
              if (!cr.MoveNext()) {
                break;
              }
            } catch (VrAssetServiceException e) {
              ControllerConsoleScript.m_Instance.AddNewLine(e.UserFriendly);
              Debug.LogException(e);
              yield break;
            }
            yield return cr.Current;
          }
        }
      }
      if (infos.Count == 0) {
        break;
      }
      if (m_CacheDir == null) {
        yield break;
      }
      // Only cull the curated sketches.  If a user likes a sketch that's very high poly count,
      // there's no feedback to tell them why it didn't show up in their list.  b/123649719
      if (m_Type == SketchSetType.Curated) {
        infos = infos.Where(x => x.GltfTriangleCount < m_MaximumSceneTriangles).ToList();
      }
      if (m_Type == SketchSetType.Curated && !assetIds.Keys.Contains(kIntroSketchAssetId)) {
        yield return VrAssetService.m_Instance.InsertSketchInfo(
            kIntroSketchAssetId, kIntroSketchIndex, infos);
      }
      for (int i = 0; i < infos.Count; i++) {
        PolySceneFileInfo info = infos[i];
        PolySketch sketch;
        if (m_AssetIds.TryGetValue(info.AssetId, out sketch)) {
          // We already have this sketch
        } else {
          sketch = new PolySketch(info);
          sketch.m_DownloadIndex = loadSketchCount++;
          // Set local paths
          info.TiltPath = Path.Combine(m_CacheDir, String.Format("{0}.tilt", info.AssetId));
          info.IconPath = Path.Combine(m_CacheDir, String.Format("{0}.png", info.AssetId));
          changed = true;
        }
        if (assetIds.ContainsKey(info.AssetId)) {
          Debug.LogErrorFormat("VR Asset Service has returned two objects with asset id '{0}'.",
                               info.AssetId);
        } else {
          sketches.Add(sketch);
          assetIds.Add(info.AssetId, sketch);
        }
      }
      infos.Clear();

      // Only download the files with every other page, otherwise the sketch pages change too often,
      // Which results in a bad user experience.
      if ((++pagesFetched & 1) == 0 || lister == null || !lister.HasMore) {
        if (Type == SketchSetType.Curated) {
          sketches.Sort(CompareSketchesByTriangleCountAndDownloadIndex);
        }

        // If populating the set from new then show stuff as it comes in.
        // (We don't have to worry about anything being removed)
        if (fromEmpty) {
          yield return DownloadFilesCoroutine(sketches);
          RemoveFailedDownloads(sketches);
          // Copying sketches to m_Sketches before sketches has completed populating is a bit
          // dangerous, but as long as they're copied and then listeners are notified
          // immediately afterward with OnChanged(), there data should be stable.
          m_TotalCount = sketches.Count;
          m_Sketches = new List<PolySketch>(sketches);
          m_AssetIds = assetIds;
          m_Ready = true;
          OnChanged();
        }
      }
    }

    if (!fromEmpty) {
      // Find anything that was removed
      int removed = m_Sketches.Count(s => !assetIds.ContainsKey(s.PolySceneFileInfo.AssetId));
      if (removed > 0) {
        changed = true;
      }

      // Download new files before we notify our listeners that we've got new stuff for them.
      if (changed) {
        yield return DownloadFilesCoroutine(sketches);
        RemoveFailedDownloads(sketches);
      }

      // DeleteOldSketchesCoroutine relies on m_AssetIds being up to date, so set these before
      // we try to cull the herd.
      m_AssetIds = assetIds;
      if (changed) {
        yield return DeleteOldSketchesCoroutine();
      }

      // Set new data live
      m_TotalCount = sketches.Count;
      m_Sketches = new List<PolySketch>(sketches);
      m_Ready = true;
      if (changed) {
        OnChanged();
      }
    } else {
      // On first run populate, take the time to clean out any cobwebs.
      // Note that this is particularly important for the curated sketches, which do not have
      // a periodic refresh, meaning old sketches will never been deleted in the other path.
      yield return DeleteOldSketchesCoroutine();
      OnChanged();
    }
  }

  // If we have not managed to download a tilt file or its icon, we should remove it from the
  // sketches list so as not to confuse the user.
  private void RemoveFailedDownloads(List<PolySketch> sketches) {
    sketches.RemoveAll(x => !x.PolySceneFileInfo.TiltDownloaded ||
                            !x.PolySceneFileInfo.IconDownloaded);
  }

  // Download tilt files and thumbnails (that we don't already have)
  private IEnumerator DownloadFilesCoroutine(List<PolySketch> sketches) {
    bool notifyOnError = true;
    void NotifyCreateError(PolySceneFileInfo sceneFileInfo, string type, Exception ex) {
      string error = $"Error downloading {type} file for {sceneFileInfo.HumanName}.";
      ControllerConsoleScript.m_Instance.AddNewLine(error, notifyOnError);
      notifyOnError = false;
      Debug.LogException(ex);
      Debug.LogError($"{sceneFileInfo.HumanName} {sceneFileInfo.TiltPath}");
    }

    void NotifyWriteError(PolySceneFileInfo sceneFileInfo, string type, UnityWebRequest www) {
      string error = $"Error downloading {type} file for {sceneFileInfo.HumanName}.\n" +
          "Out of disk space?";
      ControllerConsoleScript.m_Instance.AddNewLine(error, notifyOnError);
      notifyOnError = false;
      Debug.LogError($"{www.error} {sceneFileInfo.HumanName} {sceneFileInfo.TiltPath}");
    }

    byte[] downloadBuffer = new byte[kDownloadBufferSize];
    // Load the icons first, then the thumbnails
    foreach (PolySketch sketch in sketches) {
      PolySceneFileInfo sceneFileInfo = sketch.PolySceneFileInfo;
      // TODO(b/36270116): Check filesizes when Poly can give it to us to detect incomplete downloads
      if (!sceneFileInfo.IconDownloaded) {
        if (File.Exists(sceneFileInfo.IconPath)) {
          sceneFileInfo.IconDownloaded = true;
        } else {
          using (UnityWebRequest www = UnityWebRequest.Get(sceneFileInfo.IconUrl)) {
            DownloadHandlerFastFile downloadHandler;
            try {
              downloadHandler = new DownloadHandlerFastFile(sceneFileInfo.IconPath, downloadBuffer);
            } catch (Exception ex) {
              NotifyCreateError(sceneFileInfo, "icon", ex);
              continue;
            }
            www.downloadHandler = downloadHandler;
            yield return www.SendWebRequest();
            if (www.isNetworkError || www.responseCode >= 400 || !string.IsNullOrEmpty(www.error)) {
              NotifyWriteError(sceneFileInfo, "icon", www);
            } else {
              sceneFileInfo.IconDownloaded = true;
            }
          }
        }
      }
      yield return null;
    }

    foreach (PolySketch sketch in sketches) {
      PolySceneFileInfo sceneFileInfo = sketch.PolySceneFileInfo;
      if (!sceneFileInfo.TiltDownloaded) {
        if (File.Exists(sceneFileInfo.TiltPath)) {
          sceneFileInfo.TiltDownloaded = true;
        } else {
          using (UnityWebRequest www = UnityWebRequest.Get(sceneFileInfo.TiltFileUrl)) {
            DownloadHandlerFastFile downloadHandler;
            try {
              downloadHandler = new DownloadHandlerFastFile(sceneFileInfo.TiltPath, downloadBuffer);
            } catch (Exception ex) {
              NotifyCreateError(sceneFileInfo, "sketch", ex);
              continue;
            }
            www.downloadHandler = downloadHandler;
            yield return www.SendWebRequest();
            if (www.isNetworkError || www.responseCode >= 400 || !string.IsNullOrEmpty(www.error)) {
              NotifyWriteError(sceneFileInfo, "sketch", www);
            } else {
              sceneFileInfo.TiltDownloaded = true;
            }
          }
        }
      }
      yield return null;
    }
    yield return null;
  }

  // Delete any files that aren't referenced in m_Sketches (actually m_AssetIds)
  // Does this on a background thread so prevent hitches.
  private IEnumerator DeleteOldSketchesCoroutine() {
    if (m_CacheDir != null) {
      var task = new Future<bool>(() => {
        var unknown = new DirectoryInfo(m_CacheDir).GetFiles().Where(
          f => !m_AssetIds.ContainsKey(Path.GetFileNameWithoutExtension(f.Name)));
        foreach (var f in unknown) {
          f.Delete();
        }
        return true;
      });

      bool unused;
      while (!task.TryGetResult(out unused)) {
        yield return null;
      }
    }
  }

  // Read the icon textures for all sketches in m_RequestedIcons
  private IEnumerator TextureLoaderCoroutine() {
    while (m_RequestedIcons.Count > 0) {
      foreach (int i in m_RequestedIcons) {
        PolySketch sketch = m_Sketches[i];
        string path = sketch.PolySceneFileInfo.IconPath;
        if (sketch.PolySceneFileInfo.IconDownloaded) {
          byte[] data = File.ReadAllBytes(path);
          Texture2D t = new Texture2D(2, 2);
          t.LoadImage(data);
          sketch.Icon = t;
        }
        yield return null;
      }
      m_RequestedIcons.RemoveAll(i => m_Sketches[i].Icon != null);
    }
  }

  private static string CacheDir(SketchSetType type) {
    switch (type) {
    case SketchSetType.Liked: {
      // Ids are in the format "people/123456" so just pull out the numeric part
      string id = App.GoogleIdentity.Profile.id;
      id = id.Substring(id.LastIndexOf('/') + 1);
      return Path.Combine(Application.persistentDataPath, String.Format("users/{0}/liked", id));
    }
    case SketchSetType.Curated:
      return Path.Combine(Application.persistentDataPath, "Curated Sketches");
    default:
      return null;
    }
  }

  // When we sort the sketches, we sort them into buckets while retaining order within those
  // buckets. We bucket the sketches using the gltf triangle count, but how we bucket depends on
  // whether we are sorting the liked sketches or the curated sketches.
  // Liked sketches get split into normal, complex (requires a warning), and impossible.
  // Curated sketches get bucketed by the nearest 100,000 triangles.
  private static int CompareSketchesByTriangleCountAndDownloadIndex(PolySketch a, PolySketch b) {
    int compareResult = CloudSketchComplexityBucket(a).CompareTo(CloudSketchComplexityBucket(b));

    // If both sketches are in the same grouping, sort them relative to download index.
    if (compareResult == 0) {
      return a.m_DownloadIndex.CompareTo(b.m_DownloadIndex);
    }

    return compareResult;
  }

  // Buckets the sketches into buckets 100000 tris in size.
  private static int CloudSketchComplexityBucket(PolySketch s) {
    return s.PolySceneFileInfo.GltfTriangleCount / 100000;
  }
}

public class PolySceneFileInfo : SceneFileInfo {

  // Asset
  private string m_AssetId;
  private string m_HumanName;
  private string m_License;

  private string m_localTiltFile;
  private string m_localIcon;
  private string m_TiltFileUrl;
  private string m_IconUrl;
  private int m_GltfTriangleCount;

  private TiltFile m_DownloadedFile;
  private bool m_IconDownloaded;

  // Populate metadata from the JSON returned by Poly for a single asset
  // See go/vr-assets-service-api
  public PolySceneFileInfo(JToken json) {
    m_AssetId = json["name"].ToString().Substring(7); // strip 'assets/' from start
    m_HumanName = json["displayName"].ToString();

    var format = json["formats"].First(x => x["formatType"].ToString() == "TILT")["root"];
    m_TiltFileUrl = format["url"].ToString();
    m_IconUrl = json["thumbnail"]?["url"]?.ToString();
    m_License = json["license"]?.ToString();


    // Some assets (old ones? broken ones?) are missing the "formatComplexity" field
    var gltfFormat = json["formats"].First(x => x["formatType"].ToString() == "GLTF");
    string gltfTriCount = gltfFormat?["formatComplexity"]?["triangleCount"]?.ToString();
    if (gltfTriCount == null) {
      Debug.Log($"{m_AssetId} has no tricount");
    }
    m_GltfTriangleCount = Int32.Parse(gltfTriCount ?? "1");

    m_DownloadedFile = null;
    m_IconDownloaded = false;
  }

  public override string ToString() {
    return $"CloudFile {AssetId} file {TiltPath}";
  }

  public FileInfoType InfoType {
    get { return FileInfoType.Cloud; }
  }

  public string HumanName {
    get { return m_HumanName; }
  }

  // Allow setting since it is not in the asset json object itself
  public string Author { get; set; }

  public bool Valid {
    get { return true; }
  }

  public bool Available {
    get { return m_DownloadedFile != null; }
  }

  public string FullPath {
    get { return m_localTiltFile; }
  }

  public bool Exists {
    get { return true; }
  }

  public bool ReadOnly {
    get { return true; }
  }

  public string SourceId {
    get { return null; }
  }

  public void Delete() {
    throw new NotImplementedException();
  }

  public bool IsHeaderValid() {
    // Assume it's valid until we download it
    return true;
  }

  // Cloud specific stuff
  public string AssetId {
    get { return m_AssetId; }
  }

  public string TiltFileUrl {
    get { return m_TiltFileUrl; }
  }

  public string IconUrl {
    get { return m_IconUrl; }
  }

  public string License {
    get { return m_License; }
  }

  // Path to the locally downloaded .tilt file
  public string TiltPath {
    get { return m_localTiltFile; }
    set { m_localTiltFile = value; }
  }

  // Path to the locally downloaded icon
  public string IconPath {
    get { return m_localIcon; }
    set { m_localIcon = value; }
  }

  public bool IconDownloaded {
    get { return m_IconDownloaded; }
    set { m_IconDownloaded = value; }
  }

  public bool TiltDownloaded {
    get { return m_DownloadedFile != null; }
    set {
      if (value) {
        m_DownloadedFile = new TiltFile(m_localTiltFile);
      } else {
        m_DownloadedFile = null;
      }
    }
  }

  // Not part of the interface
  public int? TriangleCount => m_GltfTriangleCount;

  public Stream GetReadStream(string subfileName) {
    return m_DownloadedFile.GetReadStream(subfileName);
  }

  // Not part of the interface
  public int GltfTriangleCount => m_GltfTriangleCount;
}
}  // namespace TiltBrush
