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
using System.Linq;
using System.Net;
using System.Text;
using UnityEngine;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

namespace TiltBrush {
/// Serves a defined set of files from the app webserver. For security reasons all the files that
/// can be served are predefined - this reduces the chance of an exploit from someone creating a
/// cunningly-crafted Url.
/// All files are held on disk as TextAssets - this is fine for .html, .txt etc, but for images and
/// other binary files, they have to have the .bytes extension added. This makes them a little
/// awkward to work with.
/// Therefore, the idea with this class is that you will have a folder outside of Assets/ in which
/// you author the files. There is a 'Refresh Files' context menu command that you can run which
/// will copy all those files into a specified folder in the Assets folder hierarchy, while at the
/// same time setting up an entry in the file table for that file.
/// It is not intended that the m_Entries table should be hand-authored.
public class HttpFileServer : MonoBehaviour {
  [Serializable]
  public class HttpFileEntry {
    public TextAsset Asset;
    public string Path;
    public bool Binary;
    public string ContentType;
    private byte[] m_Bytes;
    private string m_Text;

    public byte[] Bytes {
      get {
        Debug.Assert(Binary);
        return m_Bytes;
      }
    }
    public string Text {
      get {
        Debug.Assert(!Binary);
        return m_Text;
      }
    }

    public void Initialize() {
      if (Binary) {
        m_Bytes = Asset.bytes;
      } else {
        m_Text = Asset.text;
      }
    }
  }

  [Header("Should start with / and is the path to serve from.")]
  [SerializeField] private string m_PathPrefix;
  [Header("Source folder containing files to serve. Should be outside of /Assets folder.")]
  [SerializeField] private string m_SourceFilesPath;
  [Header("Destination folder inside /Assets that files are copied to.")]
  [SerializeField] private string m_DestinationAssetPath;
  [Header("Files to serve. Use the context menu to refresh these.")]
  [SerializeField] private HttpFileEntry[] m_Entries;

  private Dictionary<string, string> m_Substitutions;

  public void OnEnable() {
    if (m_Substitutions == null) {
      m_Substitutions = new Dictionary<string, string>();
      foreach (var entry in m_Entries) {
        entry.Initialize();
      }
    }
    App.HttpServer.AddHttpHandler(m_PathPrefix, ReturnFile);
  }

  public void OnDisable() {
    App.HttpServer.RemoveHttpHandler(m_PathPrefix);
  }

  /// Substitute 'from' with 'to' in text files.
  public void AddSubstitution(string from, string to) {
    m_Substitutions[from] = to;
  }

  /// remove the given substitution.
  public void RemoveSubstitution(string from) {
    m_Substitutions.Remove(from);
  }

  private void ReturnFile(HttpListenerContext context) {
    string subPath = context.Request.Url.LocalPath.Substring(m_PathPrefix.Length + 1);
    var entry = m_Entries.FirstOrDefault(x => x.Path == subPath);
    if (entry == null) {
      context.Response.StatusCode = (int) HttpStatusCode.NotFound;
      return;
    }

    byte[] bytes;
    if (entry.Binary) {
      context.Response.ContentEncoding = Encoding.Default;
      bytes = entry.Bytes;
    } else {
      context.Response.ContentEncoding = Encoding.UTF8;
      string text = entry.Text;
      foreach (var substitution in m_Substitutions) {
        text = text.Replace(substitution.Key, substitution.Value);
      }

      bytes = Encoding.UTF8.GetBytes(text);
    }

    context.Response.ContentType = entry.ContentType;
    context.Response.ContentLength64 = bytes.Length;
    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
  }

  /// Below are a set of Unity Editor methods to help set up the HttpFileServer object and its
  /// set of file entries that it can serve.
#region EditorFunctions
#if UNITY_EDITOR
  [ContextMenu("Select Paths")]
  private void SelectPaths() {
    m_SourceFilesPath = RelativizePath(EditorUtility.OpenFolderPanel(
        "Select Source Path for Http File Server.", AbsolutizePath(m_SourceFilesPath), ""));
    m_DestinationAssetPath = RelativizePath(EditorUtility.OpenFolderPanel(
        "Select Destination Path for Http File Server.", AbsolutizePath(m_SourceFilesPath), ""));
    RefreshFiles();
  }

  /// Copies the files from the source folder into the destination asset folder, adding '.bytes' to
  /// binary files. At the same time it creates the entries list as it copies.
  [ContextMenu("Refresh Files")]
  private void RefreshFiles() {
    if (!EditorUtility.DisplayDialog(
        "Refresh files?",
        "Do you want to refresh the file list?",
        "OK", "Cancel")) {
      return;
    }

    var entries = new List<HttpFileEntry>();
    string absoluteDestination = AbsolutizePath(m_DestinationAssetPath);
    // We have to store the new destination paths because we can only create the asset references
    // after the copying is done and we've instructed the AssetDatabase to refresh itself.
    var entryToProjectPath = new Dictionary<HttpFileEntry, string>();

    foreach (var source in Directory.GetFiles(
        AbsolutizePath(m_SourceFilesPath), "*", SearchOption.AllDirectories)) {
      HttpFileEntry entry = new HttpFileEntry();
      (entry.ContentType, entry.Binary) = ExtensionToContentType(Path.GetExtension(source));
      if (string.IsNullOrEmpty(entry.ContentType)) {
        continue;
      }
      entry.Path = RelativizePath(source).Substring(m_SourceFilesPath.Length + 1).Replace("\\", "/");
      var destination = Path.Combine(absoluteDestination, entry.Path);
      // All binary assets get the .bytes extension added, especially important with image files
      // otherwise they will not be accessible as 'TextAssets'.
      if (entry.Binary) {
        destination += ".bytes";
      }
      entryToProjectPath[entry] = RelativizePath(destination);
      Directory.CreateDirectory(Path.GetDirectoryName(destination));
      File.Copy(source, destination, overwrite: true);
      entries.Add(entry);
    }

    // Refresh the AssetDatabase and set up all the references to the newly copied assets.
    AssetDatabase.Refresh();
    foreach (var entry in entries) {
      entry.Asset = AssetDatabase.LoadMainAssetAtPath(entryToProjectPath[entry]) as TextAsset;
    }
    m_Entries = entries.ToArray();
    EditorUtility.SetDirty(this);
  }

  /// Supported content Types
  private static readonly (string, string, bool)?[] sm_ContentTypes = {
      (".html", "text/html", false),
      (".txt", "text/plain", false),
      (".css", "text/css", false),
      (".js", "application/javascript", false),
      (".gif", "image/gif", true),
      (".jpg", "image/jpeg", true),
      (".jpeg", "image/jpeg", true),
      (".png", "image/png", true),
  };

  private static (string contentType, bool binary) ExtensionToContentType(string extension) {
    var contentType = sm_ContentTypes.FirstOrDefault(x => x.Value.Item1 == extension);
    if (contentType == null) {
      return (null, true);
    }
    return (contentType.Value.Item2, contentType.Value.Item3);
  }

  /// Function to make a path relative to the Unity project folder.
  private static string RelativizePath(string path) {
    string normalizedProject = Path.GetFullPath(Application.dataPath + "/../");
    string normalizedPath = Path.GetFullPath(path);
    if (!normalizedPath.StartsWith(normalizedProject)) {
      throw new ArgumentException($"Path ({path}) must be within the Unity Project.");
    }
    return normalizedPath.Substring(normalizedProject.Length);
  }

  /// Function to make an absolute path from a path relative to the Unity project folder.
  private static string AbsolutizePath(string path) {
    string normalizedProject = Path.GetFullPath(Application.dataPath + "/../");
    return Path.Combine(normalizedProject, path);
  }
#endif // UNITY_EDITOR
#endregion
}
} // namespace TiltBrush
