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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TiltBrushToolkit;
using Debug = UnityEngine.Debug;
using UObject = UnityEngine.Object;

namespace TiltBrush {

public class Model {
  public struct Location {
    public enum Type {
      Invalid,
      LocalFile,
      PolyAssetId
    }

    private Type type;
    private string path;
    private string id;                      // Only valid when the type is PolyAssetId.

    public static Location File(string relativePath) {
      return new Location {
        type = Type.LocalFile,
        path = relativePath
      };
    }

    public static Location PolyAsset(string assetId, string path) {
      return new Location {
        type = Type.PolyAssetId,
        path = path,
        id = assetId
      };
    }

    /// Can return null if this is a location for a fake Model (like the ones ModelWidget
    /// assigns itself while the real Model content is in progress of being loaded).
    public string AbsolutePath {
      get {
        if (path == null) {
          return null;
        }
        switch (type) {
        case Type.LocalFile:
          return Path.Combine(App.ModelLibraryPath(), path).Replace("\\", "/");
        case Type.PolyAssetId:
          return path.Replace("\\", "/");
        }
        return null;
      }
    }

    public string RelativePath {
      get {
        if (type == Type.LocalFile) { return path; }
        throw new Exception("Invalid relative path request");
      }
    }

    public string AssetId {
      get {
        if (type == Type.PolyAssetId) { return id; }
        throw new Exception("Invalid Poly asset id request");
      }
    }

    public Type GetLocationType() { return type; }

    public override int GetHashCode() {
      return this.ToString().GetHashCode();
    }

    public override string ToString() {
      if (type == Type.PolyAssetId) {
        return $"{type}:{id}";
      } else {
        return $"{type}:{path}";
      }
    }

    public override bool Equals(object obj) {
      if (!(obj is Location)) {
        return false;
      }
      return this == (Location)obj;
    }

    public static bool operator ==(Location a, Location b) {
      return a.type == b.type && a.path == b.path;
    }

    public static bool operator !=(Location a, Location b) {
      return !(a == b);
    }
  }

  private static readonly float kMeshMsPerFrame = 1.0f;

  private static readonly GltfImportOptions kPolyGltfImportOptions = new GltfImportOptions {
      rescalingMode = GltfImportOptions.RescalingMode.CONVERT,
      scaleFactor = App.METERS_TO_UNITS,
      axisConventionOverride = AxisConvention.kGltfAccordingToPoly,
      recenter = false
  };

  private static readonly GltfImportOptions kGltfImportOptions = new GltfImportOptions {
      rescalingMode = GltfImportOptions.RescalingMode.CONVERT,
      scaleFactor = App.METERS_TO_UNITS,
      recenter = false
  };

  static RateLimiter sm_Limiter = new RateLimiter(maxEventsPerFrame: 1);

  // This is the object that is cloned when attached to a button or widget.
  // It is the object that contains ObjModelScript.
  public Transform m_ModelParent;
  public Bounds m_MeshBounds;

  // Data & properties associated with current the state:
  // - Unloaded
  // - Trying to be loaded
  // - Load finished successfully
  // - Load finished unsuccessfully
  // Not all of these states are modeled explicitly; this is a WIP.

  public struct LoadError {
    public LoadError(string message, string detail=null) {
      this.message = message;
      this.detail = detail;
    }
    public readonly string message;  // Human-readable short message
    public readonly string detail;   // Maybe non-human-readable details
    // maybe? public bool transient;  // true if we know for sure that this error is transient
  }

  /// Is m_ModelParent assigned?
  /// m_Valid = true implies m_LoadError == null
  public bool m_Valid;

  /// m_LoadError != null implies m_Valid == false
  private LoadError? m_LoadError;
  public LoadError? Error => m_LoadError;

  // How many widgets are using this model?
  public int m_UsageCount;

  private Location m_Location;

  // Can the geometry in this model be exported.
  private bool m_AllowExport;

  private ImportMaterialCollector m_ImportMaterialCollector;

  // Returns the path starting after Media Library/Models
  // e.g. subdirectory/example.obj
  public string RelativePath {
    get { return m_Location.RelativePath; }
  }

  public string AssetId {
    get { return m_Location.AssetId; }
  }

  public string HumanName {
    get { return Path.GetFileNameWithoutExtension(m_Location.RelativePath); }
  }

  public bool AllowExport {
    get { return m_AllowExport; }
  }

  /// Only allowed if AllowExport = true
  public IExportableMaterial GetExportableMaterial(Material material) {
    // TODO: Maybe throw InvalidOperation if AllowExport==false rather than blowing
    // up with NullReference?
    return m_ImportMaterialCollector.GetExportableMaterial(material);
  }

  public Model(Location location) {
    m_Location = location;
  }

  public Location GetLocation() { return m_Location; }

  /// A helper class which allows import to run I/O on a background thread before producing Unity
  /// GameObject(s). Usage:
  ///   BeginAsyncLoad()
  ///   TryEndAsyncLoad()   repeat until it returns true; a bit of work is done each time(*)
  ///   CancelAsyncLoad()   if you give up waiting for it to return true
  ///
  /// (*) Although the caller appears to have the responsibility of a scheduler, ModelBuilder
  /// actually implements its own rate-limiting. After a set number of calls to TryEndAsyncLoad
  /// in one frame (whether on one or many objects), further calls will be very fast no-ops.
  /// So the caller can be (should be) a naive, performance-unaware scheduler that creates and
  /// pumps as many ModelBuilders as it likes.
  ///
  /// However, note that this does not apply to the background-thread work. Nothing managages
  /// that, so if N ModelBuilders are instantiated, N background threads will start running
  /// and competing with each other. Your implementation may want to restrict that work to I/O.
  abstract class ModelBuilder {
    protected string m_localPath;
    private Future<IDisposable> m_stateReader;
    private IEnumerator<Null> m_meshEnumerator;
    private ImportMaterialCollector m_ImportMaterialCollector;
    private GameObject m_root;

    /// In the current implementation:
    /// Before the first call to TryEndAsyncLoad, do not look at IsValid.
    /// After the first call to TryEndAsyncLoad, IsValid is always true.
    ///
    /// TODO: semantics of IsValid = false are unclear and DoUnityThreadWork looks buggy
    /// It's unclear if the intent is that the user should continue calling TryEndAsyncLoad
    /// until it returns true, or if they should stop calling TryEndAsyncLoad. etc. Probably
    /// we should remove this.
    public bool IsValid {
      get;
      protected set;
    }

    public ModelBuilder(string localPath) {
      m_localPath = localPath;
      IsValid = false;
    }

    public void BeginAsyncLoad() {
      if (m_stateReader != null) {
        throw new ApplicationException("BeginImport should only be called once.");
      }

      m_stateReader = new Future<IDisposable>(DoBackgroundThreadWork, id => id.Dispose());
    }

    public void CancelAsyncLoad() {
      // If we have already created a mesh, we need to destroy it and the gameobject it is on so
      // that we don't leave it orphaned in the heirarchy, and we don't leak meshes.
      if (m_root != null) {
        foreach (var mesh in m_root.GetComponentsInChildren<MeshFilter>()
            .Select(x => x.sharedMesh)){
          UObject.Destroy(mesh);
        }
        UObject.Destroy(m_root);
        m_root = null;
      }
      m_stateReader.Close();
    }

    /// Returns:
    ///   bool - false if incomplete, true upon successful completion.
    ///   GameObject - caller should check output GameObject to determine success.
    ///   ImportMaterialCollector - non-null upon successful completion.
    /// Raises an exception on unsuccessful completion.
    public bool TryEndAsyncLoad(out GameObject root,
                                out ImportMaterialCollector importMaterialCollector) {
      // Three things happen in this function.
      // 1: It waits to try and get the result of reading the model on a background thread
      // 2: It checks the rate limiter to make sure we don't have too many of these going on at once.
      // 3: It enumerates through, creating meshes for the model. These are time-limited so that
      //    it will stop if it has taken too long in a single frame.
      root = null;
      importMaterialCollector = null;
      if (m_meshEnumerator == null) {
        IDisposable state;
        if (!m_stateReader.TryGetResult(out state)) { return false; }

        IEnumerable<Null> enumerable;
        m_root = DoUnityThreadWork(state, out enumerable, out m_ImportMaterialCollector);
        // TODO: Possible bugs if DoUnityThreadWork ever did fail:
        // We assume the invariant that (root == null) == (IsValid == false)
        // We assume the invariant that m_ImportMaterialCollector != null
        // We don't dispose the GameObject or the enumerable
        // If the user calls TryEndAsyncLoad again we might try to call DoUnityThreadWork again
        if (m_root == null) {
          return false;
        }
        m_meshEnumerator = enumerable.GetEnumerator();
        m_root.SetActive(false);
      }
      // Yield until the limiter unblocks.
      // Multiple calls to TryGetResult above are harmless.
      if (sm_Limiter.IsBlocked()) {
        return false;
      }

      // Finish constructing the actual game object.
      Stopwatch stopwatch = new Stopwatch();
      stopwatch.Start();
      long numTicks = (long)((Stopwatch.Frequency * kMeshMsPerFrame) / 1000);
      while (true) {
        if (!m_meshEnumerator.MoveNext()) {
          m_root.SetActive(true);
          root = m_root;
          importMaterialCollector = m_ImportMaterialCollector;
          stopwatch.Stop();
          return true;
        }
        if (stopwatch.ElapsedTicks > numTicks) {
          stopwatch.Stop();
          return false;
        }
      }
    }

    // Performs whatever of the import process that can happen on a non-Unity thread.
    // Returns:
    //   disposable - passed to DoUnityThreadWork, or disposed of if the load is canceled.
    protected abstract IDisposable DoBackgroundThreadWork();

    // Performs whatever portion of the import process that is left.
    //
    // Pass:
    //   state - the value returned from DoBackgroundThreadWork. Ownership is transferred;
    //     callee is responsible for Disposing it.
    // Returns:
    //   GameObject - the root of the object hierarchy.
    //   ImportMaterialCollector - the materials that were created, and info about them
    //   IEnumerable<Null> - a coroutine that will be pumped to completion
    protected abstract GameObject DoUnityThreadWork(
        IDisposable state,
        out IEnumerable<Null> meshCreator,
        out ImportMaterialCollector importMaterialCollector);
  }

  /// The glTF ModelBuilder.
  class GltfModelBuilder : ModelBuilder {
    private readonly bool m_useThreadedImageLoad;
    private readonly bool m_fromPoly;

    public GltfModelBuilder(Location location, bool useThreadedImageLoad)
        : base(location.AbsolutePath) {
      m_useThreadedImageLoad = useThreadedImageLoad;
      m_fromPoly = (location.GetLocationType() == Location.Type.PolyAssetId);
    }

    protected override IDisposable DoBackgroundThreadWork() {
      var loader = new TiltBrushUriLoader(
          m_localPath, Path.GetDirectoryName(m_localPath), m_useThreadedImageLoad);
      var options = m_fromPoly ? kPolyGltfImportOptions : kGltfImportOptions;
      return ImportGltf.BeginImport(m_localPath, loader, options);
    }

    protected override GameObject DoUnityThreadWork(IDisposable state__,
                                                    out IEnumerable<Null> meshEnumerable,
                                                    out ImportMaterialCollector
                                                        importMaterialCollector) {
      meshEnumerable = null;
      importMaterialCollector = null;
      GameObject rootObject = null;
      using (IDisposable state_ = state__) {
        var state = state_ as ImportGltf.ImportState;
        if (state != null) {
          string assetLocation = Path.GetDirectoryName(m_localPath);
          // EndImport doesn't try to use the loadImages functionality of UriLoader anyway.
          // It knows it's on the main thread, so chooses to use Unity's fast loading.
          var loader = new TiltBrushUriLoader(m_localPath, assetLocation, loadImages: false);
          ImportGltf.GltfImportResult result =
              ImportGltf.EndImport(
                  state, loader,
                  new ImportMaterialCollector(assetLocation, uniqueSeed: m_localPath),
                  out meshEnumerable);

          if (result != null) {
            rootObject = result.root;
            importMaterialCollector = (ImportMaterialCollector)result.materialCollector;
          }
        }
      }
      IsValid = (rootObject != null);
      return rootObject;
    }
  }  // GltfModelBuilder

  GameObject LoadUsd(List<string> warnings) {
#if USD_SUPPORTED && (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      return ImportUsd.Import(m_Location.AbsolutePath, out warnings);
    }
#endif
    m_LoadError =  new LoadError("usd not supported");
    return null;
  }

  ///  Load model using FBX SDK.
  GameObject LoadFbx(List<string> warningsOut) {
#if !FBX_SUPPORTED
    m_LoadError = new LoadError("fbx not supported");
    return null;
#else
    try {
      var reader = new FbxReader(m_Location.AbsolutePath);
      var (gameObject, warnings, collector) = reader.Import();
      warningsOut.AddRange(warnings);
      m_ImportMaterialCollector = collector;
      m_AllowExport = (m_ImportMaterialCollector != null);
      return gameObject;
    } catch (Exception ex) {
      m_LoadError = new LoadError("Invalid data", ex.Message);
      m_AllowExport = false;
      Debug.LogException(ex);
      return null;
    }
#endif
  }

  GameObject LoadGltf(List<string> warnings) {
    // This is intended to be identical to using GltfModelBuilder, except synchronous.
    GameObject go;
    bool fromPoly = (m_Location.GetLocationType() == Location.Type.PolyAssetId);
    string localPath = m_Location.AbsolutePath;
    string assetLocation = Path.GetDirectoryName(localPath);
    // Synchronous, so don't use slow image loading
    var loader = new TiltBrushUriLoader(localPath, assetLocation, loadImages: false);
    try {
      ImportGltf.GltfImportResult result = ImportGltf.Import(
          localPath, loader,
          new ImportMaterialCollector(assetLocation, uniqueSeed: localPath),
          fromPoly ? kPolyGltfImportOptions : kGltfImportOptions);
      go = result.root;
      m_ImportMaterialCollector = (ImportMaterialCollector) result.materialCollector;
    } catch (Exception ex) {
      m_LoadError = new LoadError("Invalid data", ex.Message);
      go = null;
      m_AllowExport = false;
      Debug.LogException(ex);
    }

    m_AllowExport = (go != null);
    return go;
  }

  private ModelBuilder m_builder;

  public void CancelLoadModelAsync() {
    if (m_builder != null) {
      m_builder.CancelAsyncLoad();
      m_builder = null;
    }
  }

  /// Threaded image loading is slower, but won't block the main thread.
  /// If you're running in compositor and don't care about hitching, better to turn it off.
  public void LoadModelAsync(bool useThreadedImageLoad) {
    if (m_builder != null) {
      throw new ApplicationException("Load in progress");
    }

    bool allowUsd = false;
#if USD_SUPPORTED && (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    allowUsd = Config.IsExperimental;
#endif

    // Experimental usd loading.
    if (allowUsd &&
        m_Location.GetLocationType() == Location.Type.LocalFile &&
        Path.GetExtension(m_Location.AbsolutePath).ToLower().StartsWith(".usd")) {
      throw new NotImplementedException();
    } else if (m_Location.GetLocationType() == Location.Type.PolyAssetId) {
      // If we pulled this from Poly, it's going to be a gltf file.
      m_builder = new GltfModelBuilder(m_Location, useThreadedImageLoad);
    } else {
      // Assume local files load with the FbxReader.
      throw new NotImplementedException();
    }

    m_builder.BeginAsyncLoad();
  }

  public bool IsLoading() {
    return m_builder != null;
  }

  /// For use in conjunction with LoadModelAsync(), returns true when the async load is complete,
  /// false while still loading.
  ///
  /// Throws if called after async load is complete or before async load started.
  /// When this returns, the Model will be either Valid, or LoadError will be set.
  public bool TryLoadModel() {
    GameObject go = null;
    bool isValid = false;
    LoadError? error = null;
    try {
      if (!m_builder.TryEndAsyncLoad(out go, out m_ImportMaterialCollector)) {
        return false;
      }
      isValid = m_builder.IsValid;
    } catch (ObjectDisposedException ex) {
      // This is a bad exception, it means we closed the future before calling TryGetModel.
      error = new LoadError("Internal error", ex.Message);
      Debug.LogException(ex);
    } catch (FutureFailed ex) {
      // Something went wrong in the glTF loader on the background thread.
      error = new LoadError("Invalid data", ex.InnerException?.Message);
      Debug.LogException(ex);
      // TODO: Temporary, for b/139759540 and b/134430318
      // Leave the other exception alone so our analytics get the aggregated results.
      Debug.LogException(
          new Exception(string.Format("Failed loading model {0}", m_Location), ex));
    }

    m_builder = null;

    if (!isValid) {
      m_LoadError = error ?? new LoadError("Unexpected Failure");
    } else {
      m_AllowExport = go != null;
      CreatePrefab(go);
    }

    // Even if an exception occurs above, return true because the return value indicates async load
    // is complete.
    return true;
  }

  public void LoadModel() {
    CreatePrefab(null);
  }

  /// Either synchronously load a GameObject hierarchy and convert it to a "prefab"
  /// or take a previously (probably asynchronously-loaded) GameObject hierarchy and do the same.
  ///
  /// Sets m_ModelParent and m_MeshBounds.
  ///
  /// Requirements for the passed GameObject:
  /// - Its transform is identity
  /// - Every visible mesh also has a BoxCollider
  /// - Every BoxCollider also has a visible mesh
  private void CreatePrefab(GameObject go) {
    if (m_Valid) {
      // This case is handled properly but it seems wasteful.
      Debug.LogWarning($"Replacing already-loaded {m_Location}: did you mean to?");
    }

    List<string> warnings = new List<string>();

    // If we weren't provided a GameObject, construct one now.
    if (go == null) {
      m_AllowExport = false;
      // TODO: if it's not already null, why did we get here? Probably want to check for error
      // and bail at a higher level, and require as a precondition that error == null
      m_LoadError = null;

      string ext = Path.GetExtension(m_Location.AbsolutePath).ToLower();
      if (m_Location.GetLocationType() == Location.Type.LocalFile &&
          ext.StartsWith(".usd")) {
        // Experimental usd loading.
        go = LoadUsd(warnings);
      } else if (m_Location.GetLocationType() == Location.Type.PolyAssetId ||
                 ext == ".gltf2" || ext == ".gltf" || ext == ".glb") {
        // If we pulled this from Poly, it's going to be a gltf file.
        go = LoadGltf(warnings);
      } else if (ext == ".fbx" || ext == ".obj") {
        go = LoadFbx(warnings);
      } else {
        m_LoadError = new LoadError("Unknown format", ext);
      }
    }

    if (go == null) {
      m_LoadError = m_LoadError ?? new LoadError("Bad data");
      DisplayWarnings(warnings);
    } else {
      // TODO: this list of colliders is assumed to match the modelScript.m_MeshChildren array
      // This should be enforced.

      // bc.bounds is world-space; therefore this calculation requires that
      // go.transform be identity
      Debug.Assert(Coords.AsGlobal[go.transform] == TrTransform.identity);
      Bounds b = new Bounds();
      bool first = true;
      foreach (BoxCollider bc in go.GetComponentsInChildren<BoxCollider>()) {
        if (first) {
          b = new Bounds(bc.bounds.center, bc.bounds.size);
          first = false;
        } else {
          b.Encapsulate(bc.bounds);
        }
        UnityEngine.Object.Destroy(bc);
      }
      m_MeshBounds = b;

      if (first) {
        // There was no geometry
        Debug.LogErrorFormat("No usable geometry in model. LoadModel({0})", go.name);
      }

      // Adopt the GameObject
      go.name = m_Location.ToString();
      go.AddComponent<ObjModelScript>().Init();
      go.SetActive(false);
      if (m_ModelParent != null) {
        UnityEngine.Object.Destroy(m_ModelParent.gameObject);
      }
      m_ModelParent = go.transform;

      // !!! Add to material dictionary here?

      m_Valid = true;
      DisplayWarnings(warnings);
    }
  }

  public void UnloadModel() {
    if (m_builder != null) {
      m_builder.CancelAsyncLoad();
      m_builder = null;
    }
    m_Valid = false;
    m_LoadError = null;
    if (m_ModelParent != null) {
      // Procedurally created meshes need to be explicitly destroyed - you can't just destroy
      // the MeshFilter that references them.
      foreach (var mesh in m_ModelParent.GetComponentsInChildren<MeshFilter>()
                                        .Select(x => x.sharedMesh)){
        UObject.Destroy(mesh);
      }
      UObject.Destroy(m_ModelParent.gameObject);
      m_ModelParent = null;
    }
  }

  /// Resets this.Error and tries to load the model again.
  /// Pass the reason the Model is being pulled into memory, for logging purposes.
  ///
  /// When this coroutine terminates, you are guaranteed that m_Valid == true
  /// or m_LoadError != null.
  public IEnumerator LoadFullyCoroutine(string reason) {
    m_LoadError = null;
    var type = m_Location.GetLocationType();
    switch (type) {
      case Location.Type.LocalFile:
        yield return OverlayManager.m_Instance.RunInCompositor(
            OverlayType.LoadModel, LoadModel, 0.25f);
        break;
      case Location.Type.PolyAssetId:
        App.PolyAssetCatalog.RequestModelLoad(this, reason);
        yield return null;
        while (!m_Valid && !m_LoadError.HasValue) {
          yield return null;
        }
        break;
      default:
        m_LoadError = new LoadError($"Unknown load type {type}");
        break;
    }
  }

  private void DisplayWarnings(List<string> warnings) {
    if (warnings.Count > 0) {
      TiltBrush.ControllerConsoleScript.m_Instance.AddNewLine(
        "Loading " + Path.GetFileName(m_Location.AbsolutePath), true);
      foreach (string warning in warnings) {
        TiltBrush.ControllerConsoleScript.m_Instance.AddNewLine(
          OutputWindowScript.GetShorterFileName(warning.Replace("/", @"\")), false);
      }
    }
  }

  public bool IsCached() {
    return m_Location.GetLocationType() == Location.Type.PolyAssetId &&
      Directory.Exists(m_Location.AbsolutePath);
  }

  public void RefreshCache() {
    Directory.SetLastAccessTimeUtc(
      Path.GetDirectoryName(m_Location.AbsolutePath), System.DateTime.UtcNow);
  }

  // Returns all leaf meshes which are part of the model.
  // Analagous to ModelWidget.GetMeshes().
  // Do not mutate the return value.
  public MeshFilter[] GetMeshes() {
    if (!m_Valid) {
      throw new InvalidOperationException();
    }
    return m_ModelParent.GetComponent<ObjModelScript>().m_MeshChildren;
  }

  public string GetExportName() {
    switch (GetLocation().GetLocationType()) {
    case Model.Location.Type.LocalFile:
      return Path.GetFileNameWithoutExtension(RelativePath);
    case Model.Location.Type.PolyAssetId:
      return AssetId;
    }
    return "Unknown";
  }

}
} // namespace TiltBrush;
