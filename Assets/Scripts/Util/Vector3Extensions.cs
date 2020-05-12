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

public static class Vector3Extensions {

  /// Returns the component-wise Min() of the vector.
  public static float Min(this Vector3 v) {
    return Mathf.Min(v.x, Mathf.Min(v.y, v.z));
  }

  /// Returns the component-wise Max() of the vector.
  public static float Max(this Vector3 v) {
    return Mathf.Max(v.x, Mathf.Max(v.y, v.z));
  }

  /// Returns the component-wise Abs() of the vector.
  public static Vector3 Abs(this Vector3 v) {
    return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
  }
}

} // namespace TiltBrush
