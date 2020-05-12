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

public static class Vector2Extensions {
  /// Positive rotation is defined as usual: about the +z axis.
  /// Takes radians.
  public static Vector2 Rotate(this Vector2 v, float radians) {
    float sin = Mathf.Sin(radians);
    float cos = Mathf.Cos(radians);
    return new Vector2(cos * v.x - sin * v.y,
                       sin * v.x + cos * v.y);
  }
}

} // namespace TiltBrush