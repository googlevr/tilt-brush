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
using System.Text.RegularExpressions;

namespace TiltBrush {

/// Class that assists in getting an asset from Poly.
public class PolyRawAsset {
  private static readonly Regex kWindowsRootedPath = new Regex("^[A-Za-z]:[/\\\\]");

  private string m_AssetId;
  private VrAssetFormat m_DesiredAssetType;

  public class ElementInfo {
    public string filePath;
    public string dataURL;
    public byte[] assetBytes;
  }
  private ElementInfo m_RootElement;
  private List<ElementInfo> m_ResourceElements;

  public string Id {
    get { return m_AssetId; }
  }

  public VrAssetFormat DesiredType {
    get { return m_DesiredAssetType; }
  }

  public string RootFilePath {
    get { return m_RootElement.filePath; }
  }

  public string RootDataURL {
    get { return m_RootElement.dataURL; }
  }

  public List<ElementInfo> ResourceElements {
    get { return m_ResourceElements; }
  }

  public bool ValidAsset {
    get { return m_RootElement.assetBytes != null; }
  }

  public void SetRootElement(string filePath, string dataURL) {
    m_RootElement.filePath = filePath;
    m_RootElement.dataURL = dataURL;
  }

  public void CopyBytesToRootElement(byte[] bytes) {
    m_RootElement.assetBytes = bytes;
  }

  public void AddResourceElement(string filePath, string dataURL) {
    ElementInfo info = new ElementInfo();
    info.filePath = filePath;
    info.dataURL = dataURL;
    m_ResourceElements.Add(info);
  }

  public PolyRawAsset(string assetId, VrAssetFormat assetType) {
    m_AssetId = assetId;
    m_DesiredAssetType = assetType;
    m_RootElement = new ElementInfo();
    m_ResourceElements = new List<ElementInfo>();
  }

  private void RemoveFiles(List<string> filesToRemove) {
    for (int i = 0; i < filesToRemove.Count; i++) {
      System.IO.File.Delete(filesToRemove[i]);
    }
  }

  /// The directory must already have been created
  public bool WriteToDisk() {
    // First, iterate over everything that needs to be written to disk and verify it's valid.
    // If any invalid elements are found, it's likely due to a download failure and we will abort
    // the entire process.
    if (m_RootElement.assetBytes == null) {
      return false;
    }
    ulong requiredDiskSpace = (ulong)m_RootElement.assetBytes.Length;
    for (int j = 0; j < m_ResourceElements.Count; ++j) {
      if (m_ResourceElements[j].assetBytes == null) {
        // Download failed on one of the elements.
        return false;
      } else {
        requiredDiskSpace += (ulong)m_ResourceElements[j].assetBytes.Length;
      }
    }

    // Next, check to see if we have enough disk space to write all the files.
    string assetDir = App.PolyAssetCatalog.GetCacheDirectoryForAsset(m_AssetId);
    if (!FileUtils.HasFreeSpace(assetDir, requiredDiskSpace / (1024 * 1024))) {
      OutputWindowScript.Error(String.Format("Out of disk space! {0} {1}",
          requiredDiskSpace, m_AssetId));
      return false;
    }

    //
    // Next, begin writing to disk, remembering each file written to make the operation atomic.
    //
    var written = new List<string>();
    if (!Directory.Exists(assetDir)) {
      UnityEngine.Debug.LogErrorFormat("Caller did not create directory for me: {0}", assetDir);
    }
    string rootFilePath = Path.Combine(assetDir, GetPolySanitizedFilePath(m_RootElement.filePath));
    if (!FileUtils.InitializeDirectory(Path.GetDirectoryName(rootFilePath)) ||
        !FileUtils.WriteBytesIgnoreExceptions(m_RootElement.assetBytes, rootFilePath)) {
      return false;
    }
    written.Add(rootFilePath);

    // Write all resources to disk
    for (int j = 0; j < m_ResourceElements.Count; ++j) {
      string filePath = Path.Combine(assetDir, GetPolySanitizedFilePath(m_ResourceElements[j].filePath));
      if (!FileUtils.InitializeDirectory(Path.GetDirectoryName(filePath)) ||
          !FileUtils.WriteBytesIgnoreExceptions(m_ResourceElements[j].assetBytes, filePath)) {
        RemoveFiles(written);
        return false;
      }
      written.Add(filePath);
    }
    return true;
  }

  /// Returns a non-rooted path.
  public static string GetPolySanitizedFilePath(string path) {
    // I'd like to change this to use FileUtils sanitize but that would involve
    // clearing everyone's cache to force updating to the new naming

    // The kWindowsRootedPath check is there for compatibility with how this function used
    // to work; it is not necessary for correctness. Even without the check, this
    // function still always returns non-rooted paths.
    if (Path.IsPathRooted(path) || kWindowsRootedPath.IsMatch(path)) {
      return Path.GetFileName(path);
    }
    return path;
  }
}

}  // namespace TiltBrush
