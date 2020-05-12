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
using UnityEngine;

namespace TiltBrush {

public class ReferenceImageCatalog : MonoBehaviour, IReferenceItemCatalog {
  const int IMAGE_LOAD_PER_FRAME = 4;
  const int IMAGE_LOAD_PER_FRAME_COMPOSITOR = 8;
  public const int TEXTURE_CREATIONS_PER_FRAME = 1;
  public const int MAX_ICON_TEX_DIMENSION = 256;

  static public ReferenceImageCatalog m_Instance;

  public event Action CatalogChanged;
  private int m_TexturesCreatedThisFrame;

  private FileWatcher m_FileWatcher;
  private string m_ReferenceDirectory;

  private List<ReferenceImage> m_Images;
  private Stack<int> m_RequestedLoads;  // it's okay if this contains duplicates
  private bool m_DirNeedsProcessing;
  private string m_ChangedFile;
  private int m_InCompositorLoad;
  private bool m_RunningImageCacheCoroutine;
  private bool m_ResetImageEnumeration;

  [SerializeField] private Texture2D m_ErrorImage;
  [SerializeField] string[] m_DefaultImages;

  public bool IsScanning => m_RunningImageCacheCoroutine;

  public Texture2D ErrorImage { get { return m_ErrorImage; } }
  public int TexturesCreatedThisFrame {
    get { return m_TexturesCreatedThisFrame; }
    set { m_TexturesCreatedThisFrame = value; }
  }

  public void ForceCatalogScan() {
    ProcessReferenceDirectory(false);
  }

  void Awake() {
    m_Instance = this;
    m_RequestedLoads = new Stack<int>();

    App.InitMediaLibraryPath();
    App.InitReferenceImagePath(m_DefaultImages);
    m_ReferenceDirectory = App.ReferenceImagePath();

    if (Directory.Exists(m_ReferenceDirectory)) {
      m_FileWatcher = new FileWatcher(m_ReferenceDirectory);
      m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
      m_FileWatcher.FileChanged += OnChanged;
      m_FileWatcher.FileCreated += OnChanged;
      m_FileWatcher.FileDeleted += OnChanged;
      m_FileWatcher.EnableRaisingEvents = true;
    }

    ImageCache.DeleteObsoleteCaches();

    m_Images = new List<ReferenceImage>();
    ProcessReferenceDirectory(userOverlay: false);
  }

  // This is not persistent state; it avoids allocating a transient Stack every frame
  private Stack<int> Update__temporarystack = new Stack<int>();

  void Update() {
    // Safest not to interfere with LoadAllImages().
    // This code can mutate m_Images or mutate entries in m_Images.
    // LoadAllImages() can cause hitchy loads, which if processed here can
    // interfere with the compositor/progress bar fade-in and fade-out, etc.
    if (m_InCompositorLoad > 0) {
      return;
    }

    // If our folder was tampered with, reset the directory
    if (m_DirNeedsProcessing) {
      ProcessReferenceDirectory();
    }

    m_TexturesCreatedThisFrame = 0;
    // Grab a few units of work
    var working = Update__temporarystack;
    Debug.Assert(working.Count == 0);
    for (int i = 0; i < IMAGE_LOAD_PER_FRAME && m_RequestedLoads.Count > 0; ++i) {
      working.Push(m_RequestedLoads.Pop());
    }

    // Process work (perhaps generating future work)
    while (working.Count > 0) {
      int iImage = working.Pop();
      if (!m_Images[iImage].RequestLoad()) {
        m_RequestedLoads.Push(iImage);
      }
    }
  }

  // It is possible for m_Images to change while this is happening. When that happens, the
  // m_ResetImageEnumeration flag is set, and the enumeration is reset, starting again from the
  // beginning.
  private IEnumerator<Null> LoadAvailableImageCaches() {
    Debug.Assert(m_RunningImageCacheCoroutine, "Caller must set this");
    try {
      // We can't set it ourselves because there's a frame of latency between
      // caller calling StartCoroutine and us getting control.
      yield return null;  // Give the compositor time to spool up
  restart:
      m_ResetImageEnumeration = false;
      foreach (var image in m_Images) {
        image.RequestLoadIconCache();
        yield return null;
        if (m_ResetImageEnumeration) {
          goto restart;
        }
      }
    } finally {
      m_RunningImageCacheCoroutine = false;
    }
  }

  /// Load all not-already-loaded images in parallel,
  /// allowing as much main thread usage as desired
  public IEnumerator<Null> LoadAllImagesCoroutine() {
    // Want this to happen right away. The returned coroutine won't be pumped
    // until after the compositor fade-in
    m_InCompositorLoad += 1;
    return LoadAllImagesCoroutineImpl();
  }

  IEnumerator<Null> LoadAllImagesCoroutineImpl() {
    // This pretty much recreates the Update() loading loop, except it
    // uses allowMainThread=true, and has a bit of logic for progress updates.
    try {
      var toLoad = m_Images.Where(im => ! im.RequestLoad(allowMainThread: true)).ToList();
      while (true) {
        int finished = 0;
        int pumped = 0;
        foreach (var image in toLoad) {
          m_TexturesCreatedThisFrame = 0;
          if (image.RequestLoad()) {
            finished += 1;
          } else {
            pumped += 1;
          }
          // For sanity, limit the number of outstanding requests we have
          if (pumped >= IMAGE_LOAD_PER_FRAME_COMPOSITOR) {
            break;
          }
        }

        if (finished == toLoad.Count) {
          break;
        } else {
          OverlayManager.m_Instance.UpdateProgress((float)(finished + 1) / toLoad.Count);
          yield return null;
        }
      }
    } finally {
      m_InCompositorLoad -= 1;
    }
  }

  public void UnloadAllImages() {
    for (int i = 0; i < m_Images.Count; ++i) {
      m_Images[i].Unload();
    }
    Resources.UnloadUnusedAssets();

    // CatalogChanged is used here to tell the single client (the ReferencePanel) to refresh
    // its icons.  This may be an overload of CatalogChanged, but because it only has one
    // client right now, it's reasonable.
    if (CatalogChanged != null) {
      CatalogChanged();
    }
  }

  public bool AnyImageValid() {
    for (int i = 0; i < m_Images.Count; ++i) {
      if (m_Images[i].Valid) {
        return true;
      }
    }
    return false;
  }

  void OnChanged(object source, FileSystemEventArgs e) {
    m_DirNeedsProcessing = true;

    // If a file was changed, store the name so we can refresh it.
    if (e.ChangeType == WatcherChangeTypes.Changed) {
      m_ChangedFile = e.FullPath;
    } else {
      m_ChangedFile = null;
    }
  }

  /// Returns a handle to the specified catalog entry, or null if the index is invalid.
  public ReferenceImage IndexToImage(int index) {
    if (0 <= index && index < m_Images.Count) {
      return m_Images[index];
    } else {
      return null;
    }
  }

  /// Returns an index to a catalog entry, or -1 if the handle is invalid.
  /// The inverse of IndexToHandle.
  /// Indices are not durable, so use the index immediately and do not keep it around.
  public int ImageToIndex(ReferenceImage image) {
    for (int i = 0; i < m_Images.Count; ++i) {
      if (m_Images[i] == image) {
        return i;
      }
    }
    return -1;
  }

  public int ItemCount {
    get { return m_Images.Count; }
  }

  // TODO: Look into making this append image requests instead of replacing
  // them.
  public void RequestLoadImage(ReferenceImage referenceImage) {
    Debug.Assert(referenceImage != null);
    int index = ImageToIndex(referenceImage);
    RequestLoadImages(index, index + 1);
  }

  /// Requests that the specified range be loaded.
  /// Range is half-open on the right.
  public void RequestLoadImages(int iMin, int iMax) {
    iMin = Mathf.Max(0, iMin);
    iMax = Mathf.Min(m_Images.Count, iMax);

    var newRequests = m_RequestedLoads
      .Concat(Enumerable.Range(iMin, iMax - iMin))
      .Distinct()
      .OrderBy(i => m_Images[i].Running ? 0 : 1)
      .ThenBy(i => (iMin <= i && i < iMax) ? 0 : 1);
    m_RequestedLoads = new Stack<int>(newRequests.Reverse());
    Resources.UnloadUnusedAssets();
  }

  /// Returns a Texture2D that may be not be full-resolution.
  /// Ownership does not transfer, so do not mutate or destroy the texture.
  /// The Texture data may disappear.
  /// The Texture2D will usually be square, but the aspect ratio may not be.
  public Texture2D GetImageIcon(int index, out float aspect) {
    if (0 <= index && index < m_Images.Count) {
      aspect = m_Images[index].ImageAspect;
      return m_Images[index].Icon;
    } else {
      aspect = 1;
      return null;
    }
  }

  // Update m_Images with latest contents of reference directory.
  // Preserves items if they're still in the directory.
  void ProcessReferenceDirectory(bool userOverlay = true) {
    m_DirNeedsProcessing = false;
    var oldImagesByPath = m_Images.ToDictionary(image => image.FilePath);

    // If we changed a file, pretend like we don't have it.
    if (m_ChangedFile != null) {
      if (oldImagesByPath.ContainsKey(m_ChangedFile)) {
        oldImagesByPath.Remove(m_ChangedFile);
      }
      m_ChangedFile = null;
    }
    m_Images.Clear();

    // Changed file may be deleted from the directory so indices are invalidated.
    m_RequestedLoads.Clear();

    //look for .jpg or .png files
    try {
      // GetFiles returns full paths, surprisingly enough.
      foreach (var filePath in Directory.GetFiles(m_ReferenceDirectory)) {
        string ext = Path.GetExtension(filePath).ToLower();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png") { continue; }
        try {
          m_Images.Add(oldImagesByPath[filePath]);
          oldImagesByPath.Remove(filePath);
        } catch (KeyNotFoundException) {
          m_Images.Add(new ReferenceImage(filePath));
        }
      }
    } catch (DirectoryNotFoundException) {}

    if (oldImagesByPath.Count > 0) {
      foreach (var entry in oldImagesByPath) {
        entry.Value.Unload();
      }
      Resources.UnloadUnusedAssets();
    }

    if (m_RunningImageCacheCoroutine) {
      m_ResetImageEnumeration = true;
    } else {
      m_RunningImageCacheCoroutine = true;
      if (userOverlay) {
        StartCoroutine(
            OverlayManager.m_Instance.RunInCompositor(
                OverlayType.LoadImages,
                LoadAvailableImageCaches(),
                fadeDuration: 0.25f));
      } else {
        StartCoroutine(LoadAvailableImageCaches());
      }
    }

    if (CatalogChanged != null) {
      CatalogChanged();
    }
  }

  // Pass a file name with no path components. Matching is purely based on name.
  // Returns null on error.

  public ReferenceImage FileNameToImage(string name) {
    // This function used to be vague about its arguments.
    // Catch anyone who is still doing the wrong thing.
    if (name != Path.GetFileName(name)) {
      Debug.LogErrorFormat("Got image name with path components: {0}", name);
      name = Path.GetFileName(name);
    }

    // TODO: do something better than O(n)?
    for (int i = 0; i < m_Images.Count; ++i) {
      if (m_Images[i].FileName == name) {
        return m_Images[i];
      }
    }
    return null;
  }
}
}  // namespace TiltBrush
