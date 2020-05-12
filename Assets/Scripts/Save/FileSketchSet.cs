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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace TiltBrush {

public class FileSketchSet : SketchSet {
  static int ICON_LOAD_PER_FRAME = 3;

  /// Synchronously read thumbnail. Returns null on error.
  public static byte[] ReadThumbnail(SceneFileInfo fileinfo) {
    using (Stream s = fileinfo.GetReadStream(TiltFile.FN_THUMBNAIL)) {
      if (s == null) { return null; }
      byte[] buffer = new byte[32 * 1024];
      MemoryStream ms = new MemoryStream();
      while (true) {
        int read = s.Read(buffer, 0, buffer.Length);
        if (read == 0) {
          return ms.ToArray();
        }
        ms.Write(buffer, 0, read);
      }
    }
  }

  private class FileSketch : Sketch, IComparable<FileSketch> {
    private DiskSceneFileInfo m_FileInfo;
    private Texture2D m_Icon;
    private string[] m_Authors;
    private bool m_bMetadataValid;
    private IEnumerator m_coroutine = null;

    public SceneFileInfo SceneFileInfo {
      get { return m_FileInfo; }
    }

    public string[] Authors {
      get { return m_Authors; }
    }

    public Texture2D Icon {
      get { return m_Icon; }
    }

    public FileSketch(DiskSceneFileInfo info) {
      m_FileInfo = info;
      m_bMetadataValid = false;
    }

    public bool IconAndMetadataValid {
      get { return m_bMetadataValid; }
    }

    private IEnumerable RequestLoadIconAndMetadataCoroutineThreaded() {
      var thumbFuture = new Future<byte[]>(() => ReadThumbnail(m_FileInfo));
      byte[] data;
      while (!thumbFuture.TryGetResult(out data)) { yield return null; }

      if (data != null && data.Length > 0) {
        if (m_Icon == null) {
          m_Icon = new Texture2D(128, 128, TextureFormat.RGB24, true);
        }
        m_Icon.LoadImage(data);
        m_Icon.Apply();
      } else {
        // TODO: why is the icon missing/invalid? should we be noisier
        // about invalid files? But RequestLoadIcon() doesn't have a
        // way of indicating "not a tilt"
      }
      if (m_Authors == null) {
        var metadataFuture = new Future<SketchMetadata>(() => m_FileInfo.ReadMetadata());
        SketchMetadata metadata;
        while (!metadataFuture.TryGetResult(out metadata)) {
          yield return null;
        }
        if (metadata != null) {
          m_Authors = metadata.Authors;
        } else {
          if (SaveLoadScript.m_Instance.LastMetadataError != null) {
            ControllerConsoleScript.m_Instance.AddNewLine(
                string.Format("Error detected in sketch '{0}'.\nTry re-saving.",
                              m_FileInfo.HumanName));
            Debug.LogWarning(string.Format("Error reading metadata for {0}.\n{1}",
                m_FileInfo.FullPath, SaveLoadScript.m_Instance.LastMetadataError));
          }
        }
      }
      m_bMetadataValid = true;
    }

    /// Returns true if the request was fully processed.
    public bool RequestLoadIconAndMetadata() {
      if (m_bMetadataValid) {
        Debug.Assert(m_coroutine == null);
        return true;
      }

      if (m_coroutine == null) {
        if (! m_FileInfo.Exists) {
          return true;
        }
        m_coroutine = RequestLoadIconAndMetadataCoroutineThreaded().GetEnumerator();
      }

      if (! m_coroutine.MoveNext()) {
        m_coroutine = null;
        return true;
      } else {
        return false;
      }
    }

    private void AbortPendingRequest() {
      if (m_coroutine != null) {
        // This case should almost never be needed, so I'm giving it
        // a rubbish, but simple and safe implementation.
        while (m_coroutine.MoveNext()) {
          Thread.Sleep(0);
        }
        m_coroutine = null;
      }
    }

    public void UnloadIcon() {
      AbortPendingRequest();
      UnityEngine.Object.Destroy(m_Icon);
      m_Icon = null;
      m_bMetadataValid = false;
    }

    public int CompareTo(FileSketch rCompareSketch) {
      return rCompareSketch.m_FileInfo.CreationTime.CompareTo(m_FileInfo.CreationTime);
    }
  }

  protected SketchSetType m_Type;
  protected bool m_ReadyForAccess;
  private List<FileSketch> m_Sketches;
  private Stack<int> m_RequestedLoads;
  private FileWatcher m_FileWatcher;
  // collection of sketch paths to be added / deleted from set
  // Access to these containers is synchronized, but note this is not ConcurrentQueue.
  // File watcher thread is producer, main thread is consumer.
  private Queue m_ToAdd;
  private Queue m_ToDelete;

  public SketchSetType Type {
    get { return m_Type; }
  }

  public bool IsReadyForAccess {
    get { return m_ReadyForAccess; }
  }

  public bool IsActivelyRefreshingSketches {
    get { return false; }
  }

  public bool RequestedIconsAreLoaded {
    get { return m_RequestedLoads.Count == 0; }
  }

  public int NumSketches {
    get {return m_Sketches.Count; }
  }

  public FileSketchSet() {
    m_Type = SketchSetType.User;
    m_ReadyForAccess = false;
    m_RequestedLoads = new Stack<int>();
    m_Sketches = new List<FileSketch>();
    m_ToAdd = Queue.Synchronized(new Queue());
    m_ToDelete = Queue.Synchronized(new Queue());
  }

  public bool IsSketchIndexValid(int iIndex) {
    return (iIndex >= 0 && iIndex < m_Sketches.Count);
  }

  // Returns true if metadata.json has been deserialized.
  // Icon texture and SketchMetadata may be null if invalid.
  public bool GetSketchIcon(int iSketchIndex, out Texture2D icon, out string[] authors,
                            out string description) {
    description = null;
    if (!IsSketchIndexValid(iSketchIndex)) {
      Debug.Log("SketchCatalog Error: Invalid index for Sketch Metadata requested.");
      icon = null;
      authors = null;
      return false;
    } else if (!m_Sketches[iSketchIndex].IconAndMetadataValid) {
      icon = null;
      authors = null;
      return false;
    }

    icon = m_Sketches[iSketchIndex].Icon;
    authors = m_Sketches[iSketchIndex].Authors;
    return true;
  }

  public SceneFileInfo GetSketchSceneFileInfo(int iSketchIndex) {
    if (!IsSketchIndexValid(iSketchIndex)) {
      Debug.Log("SketchCatalog Error: Invalid index for Sketch SceneFileInfo requested.");
      return null;
    }
    return m_Sketches[iSketchIndex].SceneFileInfo;
  }

  public string GetSketchName(int iSketchIndex) {
    if (!IsSketchIndexValid(iSketchIndex)) {
      Debug.Log("SketchCatalog Error: Invalid index for Sketch Name requested.");
      return null;
    }
    return m_Sketches[iSketchIndex].SceneFileInfo.HumanName;
  }

  public void PrecacheSketchModels(int iSketchIndex) {
    if (!IsSketchIndexValid(iSketchIndex)) {
      Debug.Log("SketchCatalog Error: Invalid index for Sketch Name requested.");
      return;
    }
    App.PolyAssetCatalog.PrecacheModels(
        m_Sketches[iSketchIndex].SceneFileInfo, $"FileSketchSet {iSketchIndex}");
  }

  public virtual void DeleteSketch(int toDelete) {
    // Not sure why we need the validity check; but if any are invalid, it
    // is a serious error and we shouldn't touch anything
    if (!IsSketchIndexValid(toDelete)) {
      Debug.Assert(false, "Sketch set index out of range");
      return;
    }

    // Notify our file watcher to make sure it got the memo this sketch was deleted.
    m_FileWatcher.NotifyDelete(m_Sketches[toDelete].SceneFileInfo.FullPath);

    // Notify the drive sketchset as the deleted file may now be visible there.
    var driveSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Drive);
    if (driveSet != null) {
      driveSet.NotifySketchChanged(m_Sketches[toDelete].SceneFileInfo.FullPath);
    }

    m_Sketches[toDelete].SceneFileInfo.Delete();
  }

  public virtual void Init() {
    string sSketchDirectory = App.UserSketchPath();
    ProcessDirectory(sSketchDirectory);
    m_ReadyForAccess = true;

    // No real reason to do this; SaveLoadScript creates the directory itself
    try { Directory.CreateDirectory(sSketchDirectory); }
    catch (IOException) {}
    catch (UnauthorizedAccessException) {}

    if (Directory.Exists(sSketchDirectory)) {
      m_FileWatcher = new FileWatcher(sSketchDirectory, "*" + SaveLoadScript.TILT_SUFFIX);
      // TODO: improve robustness.  Using Created works for typical copy and move operations, but
      // doesn't handle e.g. streaming file.
      // Note: Renamed event not implemented on OS X, so we rely on Deleted + Created
      // If we ever start doing something special (like warning the user) with deleted or added, we
      // may need to add an explicit 'changed' queue, but delete then create works fine for now.
      m_FileWatcher.FileCreated += (object sender, FileSystemEventArgs e) => {
        m_ToAdd.Enqueue(e.FullPath);
      };
      m_FileWatcher.FileDeleted += (object sender, FileSystemEventArgs e) => {
        m_ToDelete.Enqueue(e.FullPath);
      };
      m_FileWatcher.FileChanged += (object sender, FileSystemEventArgs e) => {
        m_ToDelete.Enqueue(e.FullPath);
        m_ToAdd.Enqueue(e.FullPath);
      };
      m_FileWatcher.EnableRaisingEvents = true;
    }
  }

  /// Requests that we (as asynchronously as possible) load thumbnail icons
  /// and metadata for the specified sketches.
  ///
  /// Takes ownership of the list.
  public void RequestOnlyLoadedMetadata(List<int> requests) {
    DumpIconTextures();  // This clears out any pending requests
    m_RequestedLoads.Clear();

    requests.Reverse();
    foreach (var iSketch in requests) {
      Debug.Assert(IsSketchIndexValid(iSketch));
      m_RequestedLoads.Push(iSketch);
    }
  }

  void DumpIconTextures() {
    for (int i = 0; i < m_Sketches.Count; ++i) {
      m_Sketches[i].UnloadIcon();
    }
    Resources.UnloadUnusedAssets();
  }

  private Stack<int> Update__working = new Stack<int>();

  public void NotifySketchCreated(string fullpath) {
    m_FileWatcher.NotifyCreated(fullpath);
  }

  public void NotifySketchChanged(string fullpath) {
    m_FileWatcher.NotifyChanged(fullpath);
  }

  public void RequestRefresh() {
  }

  public void Update() {
    // process async directory changes from file system watcher
    // note: code here assumes we're the only consumer
    bool changedEvent = false;
    while (m_ToDelete.Count > 0) {
      string path = (string)m_ToDelete.Dequeue();
      if (RemoveSketchByPath(path)) {
        changedEvent = true;
      }
    }
    // be nice and only add one sketch per frame, since we're validating header
    if (m_ToAdd.Count > 0) {
      string path = (string)m_ToAdd.Dequeue();
      var fileInfo = new DiskSceneFileInfo(path);
      if (fileInfo.IsHeaderValid()) {
        AddSketchToSet(fileInfo);
        m_Sketches.Sort();
        changedEvent = true;
      }
    }
    if (changedEvent) {
      OnChanged();
    }

    // Grab a few units of work
    var working = Update__working;  // = new Stack<int>();
    Debug.Assert(working.Count == 0);
    for (int i = 0; i < ICON_LOAD_PER_FRAME && m_RequestedLoads.Count > 0; ++i) {
      working.Push(m_RequestedLoads.Pop());
    }

    // Process work (perhaps generating future work)
    while (working.Count > 0) {
      int iSketch = working.Pop();
      if (! m_Sketches[iSketch].RequestLoadIconAndMetadata()) {
        m_RequestedLoads.Push(iSketch);
      }
    }
  }

  private void ProcessDirectory(string path) {
    var di = new DirectoryInfo(path);
    if (!di.Exists) {
      return;
    }
    foreach (DiskSceneFileInfo info in SaveLoadScript.IterScenes(di)) {
      //don't add bogus files to the catalog
      if (info.IsHeaderValid()) {
        AddSketchToSet(info);
      }
    }
    m_Sketches.Sort();
  }

  protected void AddSketchToSet(DiskSceneFileInfo rInfo) {
    m_Sketches.Add(new FileSketch(rInfo));
  }

  /// Remove sketch with given path from set, returning false if no such sketch.
  private bool RemoveSketchByPath(string path) {
    // TODO: avoid this O(n) call by changing SketchSet API to not be index-based
    int index = m_Sketches.FindIndex(sketch => sketch.SceneFileInfo.FullPath == path);
    if (index != -1) {
      m_Sketches.RemoveAt(index);
      return true;
    } else {
      return false;
    }
  }

  public event Action OnChanged = delegate {};

  public event Action OnSketchRefreshingChanged {
    add { }
    remove { }
  }

}
} // namespace TiltBrush
