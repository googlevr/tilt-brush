// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;

#if TILT_BRUSH
using AxisConvention = TiltBrush.AxisConvention;
#endif

namespace TiltBrushToolkit {
/// <summary>
/// Options that indicate how to import a given asset.
/// </summary>
[Serializable]
public struct GltfImportOptions {
  public enum RescalingMode {
    // Apply scaleFactor.
    CONVERT,
    // Scale the object such that it fits a box of desiredSize, ignoring scaleFactor.
    FIT,
  }

  /// <summary>
  /// If not set, axis conventions default to the glTF 2.0 standard.
  /// </summary>
  public AxisConvention? axisConventionOverride;

  /// <summary>
  /// What type of rescaling to perform.
  /// </summary>
  public RescalingMode rescalingMode;

  /// <summary>
  /// Scale factor to apply (in addition to unit conversion).
  /// Only relevant if rescalingMode==CONVERT.
  /// </summary>
  public float scaleFactor;

  /// <summary>
  /// The desired size of the bounding cube, if scaleMode==FIT.
  /// </summary>
  public float desiredSize;

  /// <summary>
  /// If true, recenters this object such that the center of its bounding box
  /// coincides with the center of the resulting GameObject (recommended).
  /// </summary>
  public bool recenter;

  /// <summary>
  /// Returns a default set of import options.
  /// </summary>
  public static GltfImportOptions Default() {
    GltfImportOptions options = new GltfImportOptions();
    options.recenter = true;
    options.rescalingMode = RescalingMode.CONVERT;
    options.scaleFactor = 1.0f;
    options.desiredSize = 1.0f;
    return options;
  }
}
}
