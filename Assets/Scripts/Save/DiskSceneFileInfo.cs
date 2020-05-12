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
using UnityEngine;

using Newtonsoft.Json;

#if USE_DOTNETZIP
using ZipSubfileReader = ZipSubfileReader_DotNetZip;
using ZipLibrary = Ionic.Zip;
#else
using ZipSubfileReader = TiltBrush.ZipSubfileReader_SharpZipLib;
using ZipLibrary = ICSharpCode.SharpZipLibUnityPort.Zip;
#endif

namespace TiltBrush {

public struct DiskSceneFileInfo : SceneFileInfo {
  static private string kInvalidHumanName = "<invalid>";

  /// Full path to a .tilt directory, a .tilt file, or a .tilt
  /// directory contained in Assets/Resources
  private string m_fullpath;
  private bool m_embedded;
  private string m_humanName;
  private string m_AssetId;
  private string m_SourceId;  // If this is a derivative work of a poly asset, that asset id
  private bool m_readOnly;
  private DateTime? m_creationTime;

  private TiltFile m_TiltFile;

  public FileInfoType InfoType {
    get { return FileInfoType.Disk; }
  }

  /// As human-readable a name as possible: no extension, no directory name
  public string HumanName {
    get { return m_humanName; }
  }

  public bool HumanNameValid {
    get {
      return m_humanName != null
          && m_humanName != ""
          && m_humanName != kInvalidHumanName;
    }
  }

  /// true iff the SceneFileInfo references a file.
  /// The file does not necessarily exist.
  public bool Valid {
    get { return m_fullpath != null; }
  }

  /// Files on disk are always available
  public bool Available {
    get { return true; }
  }

  /// True if standalone .tilt file accessible on the filesystem.
  public bool IsPlainFile {
    get { return Valid && !m_embedded && File.Exists(m_fullpath); }
  }

  public bool ReadOnly { get { return m_readOnly; } }

  public string AssetId {
    get { return m_AssetId; }
    set { m_AssetId = value; }
  }

  public string SourceId {
    get { return m_SourceId; }
    set { m_SourceId = value; }
  }

  /// Full path to a .tilt directory, a .tilt file, or a .tilt
  /// directory contained in Assets/Resources
  public string FullPath { get { return m_fullpath; } }

  /// true iff the SceneFileInfo references an existing file.
  public bool Exists {
    get {
      if (!Valid) { return false; }
      if (m_embedded) {
        var meta = string.Format("{0}/{1}", m_fullpath, TiltFile.FN_METADATA);
        return Resources.Load<TextAsset>(meta) != null;
      }
      return File.Exists(m_fullpath) || Directory.Exists(m_fullpath);
    }
  }

  public DateTime CreationTime {
    get {
      if (Valid) {
        if (m_creationTime.HasValue) {
          m_creationTime = m_creationTime.Value;
        } else if (File.Exists(m_fullpath)) {
          m_creationTime = File.GetLastWriteTime(m_fullpath);
        } else if (Directory.Exists(m_fullpath)) {
          // It's possible for an implementation to save a .tilt/ such that the
          // directory's mtime doesn't change, so check a file's mtime.
          m_creationTime = File.GetLastWriteTime(Path.Combine(m_fullpath, TiltFile.FN_SKETCH));
        } else {
          m_creationTime = DateTime.MinValue;
        }
        return m_creationTime.Value;
      }
      return DateTime.MinValue;
    }
  }

  /// true iff the .tilt file is embedded in (read-only) resource data
  public bool Embedded { get { return m_embedded; } }

  public int? TriangleCount => null;

  public DiskSceneFileInfo(string path, bool embedded=false, bool readOnly=false) {
    Debug.Assert(embedded == false || readOnly == true); // if embedded, should always be readonly.
    m_embedded = embedded;
    if (m_embedded) {
      m_fullpath = path;
      m_TiltFile = null;
      m_humanName = kInvalidHumanName;
    } else {
      m_fullpath = Path.GetFullPath(path);
      m_TiltFile = new TiltFile(m_fullpath);
      m_humanName = Path.GetFileNameWithoutExtension(SaveLoadScript.RemoveMd5Suffix(m_fullpath));
    }
    m_AssetId = null;
    m_SourceId = null;
    m_readOnly = readOnly;
    m_creationTime = null;
  }

  public override string ToString() {
    return $"DiskFile name {m_humanName}";
  }

  public void UpdateHumanNameFrom(SceneFileInfo sourceSceneFileInfo) {
    // Copy over relevant parts of the original scene file info.
    if (sourceSceneFileInfo != null) {
      m_humanName = sourceSceneFileInfo.HumanName;
    }
  }

  public void Delete() {
    if (!Valid) { return; }

    //look to delete the file version first
    if (File.Exists(m_fullpath)) {
      try {
        File.SetAttributes(m_fullpath, FileAttributes.Normal);
        File.Delete(m_fullpath);
      } catch (UnauthorizedAccessException) {
        Debug.LogFormat("Unauthorized Exception: Can't Delete {0}", m_fullpath);
      } catch (DirectoryNotFoundException) {
        Debug.LogFormat("Can't Find File to Delete {0}", m_fullpath);
      } catch (IOException) {
        Debug.LogFormat("IO Exception: Can't Delete {0}", m_fullpath);
      }
    } else if (Directory.Exists(m_fullpath)) {
      //if we can't find a file that matches the name, look for a directory
      try {
        Directory.Delete(m_fullpath, true);
      } catch (UnauthorizedAccessException) {
        Debug.LogFormat("Unauthorized Exception: Can't Delete Folder {0}", m_fullpath);
      } catch (DirectoryNotFoundException) {
        Debug.LogFormat("Can't Find Folder to Delete {0}", m_fullpath);
      } catch (IOException) {
        Debug.LogFormat("IO Exception: Can't Delete Folder {0}", m_fullpath);
      }
    }
  }

  /// Returns a readable stream to a pre-existing subfile,
  /// or null if the subfile does not exist,
  /// or null if the file format is invalid.
  public Stream GetReadStream(string subfileName) {
    if (!Valid) {
      return null;
    }

    if (m_embedded) {
      var asset = Resources.Load<TextAsset>(string.Format("{0}/{1}", m_fullpath, subfileName));
      if (asset == null) {
        return null;
      }
      return new MemoryStream(asset.bytes);
    }

    if (File.Exists(m_fullpath)) {
      // It takes a long time to figure out a file isn't a .zip, so it's worth the
      // price of a quick check up-front
      if (! IsHeaderValid()) {
        return null;
      }
      try {
        return new ZipSubfileReader(m_fullpath, subfileName);
      } catch (ZipLibrary.ZipException e) {
        Debug.LogFormat("{0}", e);
        return null;
      } catch (FileNotFoundException) {
        return null;
      }
    }

    string fullPath = Path.Combine(m_fullpath, subfileName);
    try {
      return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
    } catch (FileNotFoundException) {
      return null;
    }
  }

  /// Return true iff Exists && the first few bytes look like a valid header
  public bool IsHeaderValid() {
    if (Embedded) {
      return Exists;
    }
    if (!Valid) {
      return false;
    }

    return m_TiltFile.IsHeaderValid();
  }

  public SketchMetadata ReadMetadata() {
    SketchMetadata metadata = null;
    var stream = SaveLoadScript.GetMetadataReadStream(this);
    if (stream != null) {
      using (var jsonReader = new JsonTextReader(new StreamReader(stream))) {
        metadata = SaveLoadScript.m_Instance.DeserializeMetadata(jsonReader);
        m_SourceId = metadata.SourceId;
        m_AssetId = metadata.AssetId;
      }
    }
    return metadata;
  }


  /// Creates a stream from a file in streaming assets.
  /// On android, streaming assets are stored inside the application .apk, so for android
  /// we have to load the apk as a zip and return a stream of it.
  /// The 'proper' way to do this cross-platform is to use UnityWebRequest to load the file,
  /// but UnityWebRequest is an asynchronous API, and we need to do this synchronously.
  public Stream LoadFromStreamingAssets(string path) {
    string fullPath = Path.Combine(Application.streamingAssetsPath, path);
    if (Application.platform == RuntimePlatform.Android) {
      int jarIndex = fullPath.IndexOf("file://") + 7;
      int fileIndex = fullPath.IndexOf("!/") + 2;
      string jarFile = fullPath.Substring(jarIndex, fileIndex - jarIndex - 2);
      string subFile = fullPath.Substring(fileIndex);
      try {
        return new ZipSubfileReader(jarFile, subFile);
      } catch (ZipLibrary.ZipException e) {
        Debug.LogFormat("{0}", e);
        return null;
      } catch (FileNotFoundException) {
        Debug.LogFormat("Could not find {0}", jarFile);
        return null;
      }
    }
    try {
      return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
    } catch (FileNotFoundException) {
      Debug.LogFormat("Could not load {0}", fullPath);
      return null;
    }
  }
}

}  // namespace TiltBrush
