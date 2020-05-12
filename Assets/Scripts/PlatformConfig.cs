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

namespace TiltBrush {

/// Used to store per-platform configuration data and constants.
/// As with Config, despite being public, all this data should be considered read-only.
/// TODO: .net 4.6 allows us to have serialized read-only fields
[CreateAssetMenu(fileName = "PlatformConfig", menuName = "Per Platform Config", order = 1)]
public class PlatformConfig : ScriptableObject {
  // Bounds the number of input verts we can send to the hull generator.
  // Empirically, numVerts <= .6 * numKnots.
  public int HullBrushMaxVertInputs;

  // Bounds the number of knots to allow when generating geometry.
  // The bulk of CPU time is in hull generation, so this isn't as useful as the above.
  public int HullBrushMaxKnots;

  // If a Reference Image file is beyond this size, we won't attempt to load it.
  public int ReferenceImagesMaxFileSize;

  // If a Reference Image's dimensions are beyond this size, we won't attempt to load it.
  public int ReferenceImagesMaxDimension;

  // If a Reference Image's dimensions are beyond this size but less than the max dimension, we
  // will attempt to load it but resize it to a more manageable size.
  public int ReferenceImagesResizeDimension;

  public int MemoryWarningVertCount;

  // On some platforms (eg Android) the C# FileSystemWatcher API does not seem to work.
  // In that case we need to use some manual workarounds. This can be used to test those
  // workarounds even on Windows.
  public bool UseFileSystemWatcher;

  // Autosave is currently disabled on Quest.
  // Maybe we should tune the interval instead, or kNanoSecondsPerSnapshotSlice, or both?
  public bool EnableAutosave;

  [Header("Loading")]
  public float QuickLoadMaxDistancePerFrame;

  // Intro sketch shown in the non-first run
  public GameObject IntroSketchPrefab;

  [Header("Poly")]
  [Tooltip("Preload Poly models while browsing, without requiring the user to click")]
  public bool EnablePolyPreload;
  [Tooltip("Workaround for b/150868218, but the workaround requires lots of memory")]
  public bool AvoidUploadHandlerFile;

  [Header("Export")]
  // Fbx export will crash if the fbx library isn't available
  public bool EnableExportFbx;
  // This should be okay to enable on both platforms.
  public bool EnableExportGlb;
  // Json export generates a ton of garbage and is not very useful any more
  public bool EnableExportJson;
  // Usd export will crash if the usd libraries aren't available
  public bool EnableExportUsd;
  // Trade off increased disk-space and export time for reduced memory
  public bool EnableExportMemoryOptimization;

  [Header("Multicam")]
  // preview image shown in multicam. Otherwise the picture is taken through the viewfinder.
  public bool EnableMulticamPreview;
  // Multicam styles available to use.
  public MultiCamStyle[] EnabledMulticamStyles;
  // The maximum size of a snapshot allowed in either dimension.
  public int MaxSnapshotDimension;
  // A mapping between the framerate and the frame gap between multicam previews.
  // (Leave empty to not have any gaps)
  public AnimationCurve FrameRateToPreviewRenderGap;
}
} // namespace TiltBrush
