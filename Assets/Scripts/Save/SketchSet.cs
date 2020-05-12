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
using UnityEngine;

namespace TiltBrush {

// Not actually useful as only used internally in SketchSets.
public interface Sketch {
  SceneFileInfo SceneFileInfo { get; }
  string[] Authors { get; }
  Texture2D Icon { get; }
  bool IconAndMetadataValid { get; }
}

/// A collection of sketches from some source (user's local folder, showcase, cloud).
/// Name, icon and author are available here, and sketches are accessed by getting the
/// SceneFileInfo.
public interface SketchSet {
  SketchSetType Type { get; }

  /// True if the sketch set can be accessed, but does not imply that all the data (like icons, etc)
  /// have been read yet.
  bool IsReadyForAccess { get; }

  bool IsActivelyRefreshingSketches { get; }

  bool RequestedIconsAreLoaded { get; }

  int NumSketches { get; }

  /// Should be called on all SketchSets after construction.
  void Init();

  bool IsSketchIndexValid(int iIndex);

  /// Requests that we (as asynchronously as possible) load thumbnail icons
  /// and metadata for the specified sketches. Current implementations also seem to throw away
  /// currently loaded icons. The List may be altered by the function.
  void RequestOnlyLoadedMetadata(List<int> requests);

  bool GetSketchIcon(int iSketchIndex, out Texture2D icon, out string[] authors,
                     out string description);

  /// Returns null if index is invalid
  SceneFileInfo GetSketchSceneFileInfo(int iSketchIndex);

  string GetSketchName(int iSketchIndex);

  void DeleteSketch(int toDelete);

  void PrecacheSketchModels(int iSketchIndex);

  void NotifySketchCreated(string fullpath);

  void NotifySketchChanged(string fullpath);

  void RequestRefresh();

  // Called by SketchCatalog for async loading
  void Update();

  /// Signals change in set (at most one event per Update).
  /// This only fires for SketchSetType.User currently.
  /// TODO: more granular change events
  event Action OnChanged;

  /// Calls when cloud sketch sets start or stop refreshing.
  event Action OnSketchRefreshingChanged;
}

}  // namespace TiltBrush
