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

using System.IO;

namespace TiltBrush {
public enum FileInfoType {
  Disk,
  Cloud,
  None
}

/// SceneFileInfo is a reference to a tilt file stored somewhere, and accessed via GetReadStream.
public interface SceneFileInfo {
  FileInfoType InfoType { get; }

  /// User-friendly name for the sketch. NOT AUTOMATICALLY FILESYSTEM-SAFE.
  string HumanName { get; }

  /// Does this refer to a real sketch?
  /// For disk files, this is true if there's a file name (there may no be file there, though)
  /// For cloud files, this is always true
  bool Valid { get; }

  /// Is this sketch actually loadable right now (it may need to be downloaded)?
  bool Available { get; }

  /// Full path to the sketch (could be a file or a Resource)
  string FullPath { get; }

  /// Does the thing referred to by FullPath exist?
  bool Exists { get; }

  /// Can the original scene file be overwritten with an overwrite save?
  bool ReadOnly { get; }

  /// Poly asset of this sketch.
  string AssetId { get; }

  /// Poly asset id that this is derived from.
  string SourceId { get; }

  /// Rough triangle count, or null if unknown.
  /// Likely known only for cloud-based sources like Poly.
  int? TriangleCount { get; }

  void Delete();

  bool IsHeaderValid();

  /// Get a stream for a specific part of the file.  Subfilenames are defined in TiltFile.
  Stream GetReadStream(string subfileName);
}

}  // namespace TiltBrush
