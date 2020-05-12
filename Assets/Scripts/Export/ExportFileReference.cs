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

using UnityEngine;

namespace TiltBrush {

/// A file referenced by the .an export. You should use this instead of bare strings because
/// the exporter treats these specially:
/// - Does the file copying into the output
/// - Only copies the file if it's actually referenced by the json
/// - Does some sanity-checking about URI encoding
public class ExportFileReference {
  /// For use with CreateDisambiguated().
  public class DisambiguationContext {
    // Only public for use by CreateDisambiguated
    // Exported file names that are being used and that are therefore off-limits.
    // Same as set(m_filesByOriginalLocation.values.m_uri)
    public HashSet<string> m_exportedFileNames = new HashSet<string>();
    // Only public for use by CreateDisambiguated
    // Non-http file refs keyed by ExportFileReference.m_originalLocation
    // (http file refs have null m_originalLocation)
    public Dictionary<string, ExportFileReference> m_filesByOriginalLocation =
        new Dictionary<string, ExportFileReference>();
  }

  // Returns a full path to the URI.
  // Throws an exception if arguments don't resolve to a local file:
  // - uri is http
  // - uri is relative, but there is no uriBase
  private static string GetFullPathForUri(string sourceUri, string uriBase) {
    if (IsHttp(sourceUri)) { throw new ArgumentException("sourceUri"); }

    if (sourceUri.StartsWith(ExportUtils.kBuiltInPrefix)) {
      string defaultName = sourceUri.Substring(ExportUtils.kBuiltInPrefix.Length);
      return Path.Combine(App.SupportPath(), defaultName);
    } else if (Path.IsPathRooted(sourceUri)) {
      Debug.LogFormat("Unexpected non-relative URI on export: {0}", sourceUri);
      return sourceUri;
    } else {
      if (uriBase == null) { throw new ArgumentNullException("uriBase"); }
      return Path.Combine(uriBase, sourceUri);
    }
  }

  public static bool IsHttp(string uri) {
    return (uri.StartsWith("http:") || uri.StartsWith("https:"));
  }

  /// Like the other CreateLocal but passes the name through a sanitizer.
  /// Use this if you don't control where "name" comes from.
  public static ExportFileReference CreateSafeLocal(string originalLocation, string unsafeName) {
    return CreateLocal(originalLocation,
                       FileUtils.SanitizeFilenameAndPreserveUniqueness(unsafeName));
  }

  /// Returns a FileReference that will get copied into the output.
  ///
  /// Warning: nothing currently prevents you from:
  /// - copying the same originalLocation to N different names
  /// - copying N different originalLocations to the same name
  ///
  /// Pass:
  ///   originalLocation - path to an existing file
  ///   name - the name of the file in the export folder; must consist entirely of alphanums and '_'
  public static ExportFileReference CreateLocal(string originalLocation, string name) {
    if (!File.Exists(originalLocation)) {
      throw new ArgumentException("originalLocation must exist");
    }
    if (Uri.EscapeUriString(name) != name) {
      throw new ArgumentException("name: has invalid characters");
    }
    return new ExportFileReference(true, originalLocation, name);
  }

  /// Returns a FileReference that will not get copied into the output.
  /// This is only useful if the uri is http:// or https://
  public static ExportFileReference CreateHttp(string uri) {
    if (! IsHttp(uri)) { throw new ArgumentException("Uri not http"); }
    return new ExportFileReference(false, null, uri);
  }

  /// Returns a (possibly shared) ExportFileReference for the uri.
  /// The destination file:
  /// - will have no path components in it
  /// - will not collide with any other destination files
  /// - will be similar to suggestedName (if suggestedName is passed)
  public static ExportFileReference GetOrCreateSafeLocal(
      DisambiguationContext disambiguationContext, string sourceUri, string uriBase,
      string suggestedName = null) {
    if (IsHttp(sourceUri)) {
      throw new ArgumentException("sourceUri");
    }

    // Passing the same source file multiple times should give the same destination file
    string sourcePath = GetFullPathForUri(sourceUri, uriBase);
    if (disambiguationContext.m_filesByOriginalLocation.TryGetValue(
        sourcePath, out var existingRef)) {
      return existingRef;
    }

    // A newly-seen source file should get an unused destination
    string exportedFileName = ExportUtils.CreateUniqueName(
        Path.GetFileName(suggestedName ?? sourcePath), disambiguationContext.m_exportedFileNames);
    var fileRef = CreateSafeLocal(sourcePath, exportedFileName);
    disambiguationContext.m_filesByOriginalLocation[fileRef.m_originalLocation] = fileRef;
    return fileRef;
  }

  // If true, m_uri looks like a relative path, and m_originalLocation needs to be copied into
  // the export output.
  // If false, m_uri looks like a http:// or https://, must be properly encoded,
  // m_originalLocation will be null, and no copying needs to take place.
  public readonly bool m_local;
  // A full path to the source file, or null if there is none (eg, it was http:)
  public readonly string m_originalLocation;
  // The uri that the export should use to refer to the file.
  // This is often relative to the export directory, but may be an absolute http.
  public readonly string m_uri;

  // Callers enforce these consitions:
  // If local is true, originalLocation must be non-null and refer to a readable file.
  // Otherwise, originalLocation must be null and uri must be http:
  private ExportFileReference(bool local, string originalLocation, string uri) {
    m_local = local;
    m_originalLocation = originalLocation;
    m_uri = uri;
  }

  public string AsJson() {
    return new SimpleJSON.JSONData(m_uri).ToString();
  }

  public bool IsHttp() {
    return ExportFileReference.IsHttp(m_uri);
  }
}

} // namespace TiltBrush
