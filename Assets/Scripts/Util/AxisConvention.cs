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

/// Helpers for creating change-of-basis matrices that convert between different
/// axis conventions.
public struct AxisConvention {
  public static readonly AxisConvention kUnity = new AxisConvention {
    right   = Vector3.right,   // aka ( 1,  0,  0)
    up      = Vector3.up,      // aka ( 0,  1,  0)
    forward = Vector3.forward, // aka ( 0,  0,  1)
  };

  // Fbx allows specifying the axis conventions in the file itself, but that doesn't
  // seem widely used.  This is what the Unity Editor assumes fbx files use.
  public static readonly AxisConvention kFbxAccordingToUnity = new AxisConvention {
    right   = new Vector3(-1,  0,  0),
    up      = new Vector3( 0,  1,  0),
    forward = new Vector3( 0,  0,  1),
  };

  // When we implemented Poly support the gltf spec didn't specify a forward direction.
  public static readonly AxisConvention kGltfAccordingToPoly = new AxisConvention {
    right   = new Vector3( 1,  0,  0),
    up      = new Vector3( 0,  1,  0),
    forward = new Vector3( 0,  0, -1),
  };

  public static readonly AxisConvention kUsd = new AxisConvention {
    right   = new Vector3( 1,  0,  0),
    up      = new Vector3( 0,  1,  0),
    forward = new Vector3( 0,  0, -1),
  };

  // STL has no conventions; y-up seems to be the default, but z-up is also seen.
  // This is what we chose.
  public static readonly AxisConvention kStl = new AxisConvention {
    right   = new Vector3( 1,  0,  0),
    up      = new Vector3( 0,  0,  1),
    forward = new Vector3( 0, -1,  0),
  };

  // VRML/X3D don't specify an axis convention.
  // This is what we chose.
  public static readonly AxisConvention kVrml = new AxisConvention {
    right   = new Vector3( 1,  0,  0),
    up      = new Vector3( 0,  0,  1),
    forward = new Vector3( 0, -1,  0),
  };

  // https://github.com/KhronosGroup/glTF/blob/23ff5b4c5/specification/2.0/README.md
  // "glTF uses a right-handed coordinate system [... and] defines +Y as up.
  // The front of a glTF asset faces +Z"
  public static readonly AxisConvention kGltf2 = new AxisConvention {
    right   = new Vector3(-1,  0,  0),
    up      = new Vector3( 0,  1,  0),
    forward = new Vector3( 0,  0,  1),
  };

  // For reference and testing.
  public static readonly AxisConvention kUnreal = new AxisConvention {
    right   = new Vector3( 0,  1,  0),
    up      = new Vector3( 0,  0,  1),
    forward = new Vector3( 1,  0,  0),
  };

  /// Returns a matrix that converts to dst from Unity's axis conventions.
  public static Matrix4x4 GetFromUnity(AxisConvention dst) {
    // Solve for M:
    //   M * (1, 0, 0) = ac.right   (because 1,0,0 is Unity right)
    //   M * (0, 1, 0) = ac.up
    //   M * (0, 0, 1) = ac.forward
    return new Matrix4x4(dst.right, dst.up, dst.forward, new Vector4(0,0,0,1));
  }

  /// Returns a matrix that converts to Unity's axis conventions from src.
  public static Matrix4x4 GetToUnity(AxisConvention src) {
    // transpose == inverse since these conversions are all orthonormal.
    return GetFromUnity(src).transpose;
  }

  /// General-purpose conversion.
  public static Matrix4x4 GetToDstFromSrc(AxisConvention dst, AxisConvention src) {
    // It doesn't really matter which system we pivot through, so use Unity
    return GetFromUnity(dst) * GetToUnity(src);
  }

  public Vector3 right;
  public Vector3 up;
  public Vector3 forward;
}

}
