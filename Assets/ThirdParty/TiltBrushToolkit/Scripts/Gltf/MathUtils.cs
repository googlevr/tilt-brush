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

using UnityEngine;

namespace TiltBrushToolkit {

public static class MathUtils {
  /// Returns TRS, assuming the matrix is of the form T * R * S:
  ///  T is pure-translation
  ///  R is pure-rotation
  ///  S is diagonal (axis-aligned scale)
  public static void DecomposeToTrs(
      Matrix4x4 m, out Vector3 translation, out Quaternion rotation, out Vector3 scale) {
    translation = m.GetColumn(3);

    Vector3 c0 = m.GetColumn(0);
    Vector3 c1 = m.GetColumn(1);
    Vector3 c2 = m.GetColumn(2);
    // Determinant < 0 means it's a mirroring, which is equivalent to saying that
    // (c0 cross c1) dot c2 < 0, which is an improper rotation.
    // Flipping all 3 directions gives us a proper rotation, and moves the mirroring
    // into the scale, where we want it.
    float mirroring = Mathf.Sign(m.determinant);
    scale = new Vector3(c0.magnitude, c1.magnitude, c2.magnitude) * mirroring;

    // In Unity, c2 is forward; c1 is up.
    rotation = Quaternion.LookRotation(c2 * mirroring, c1 * mirroring);
  }

  // Because Unity's Vector3 == has arbitrary epsilons
  private static bool ExactlyEquals(Vector3 a, Vector3 b) {
    return a.x == b.x && a.y == b.y && a.z == b.z;
  }

  // Returns an arbitrary normal to a unit-length direction vector.
  private static Vector3 CalculateNormalToUnitDirection(Vector3 direction) {
    Vector3 n = Vector3.Cross(direction, Vector3.forward).normalized;
    if (! ExactlyEquals(n, Vector3.zero)) { return n; }
    // Should only get here if direction ~= forward, in which case anything else will work
    return Vector3.Cross(direction, Vector3.right).normalized;
  }

  /// <summary>
  /// Calculates a normal for three vertices given in standard order.
  /// In case of degeneracies, there may be many valid normals; an arbitrary one is returned.
  /// </summary>
  /// <param name="v1">First vertex.</param>
  /// <param name="v2">Second vertex.</param>
  /// <param name="v3">Third vertex.</param>
  /// <returns>The normal for the given vertices.</returns>
  public static Vector3 CalculateNormal(Vector3 v1, Vector3 v2, Vector3 v3) {
    Vector3 a = (v2 - v1).normalized;
    Vector3 b = (v3 - v1).normalized;
    bool aValid = ! ExactlyEquals(a, Vector3.zero);
    bool bValid = ! ExactlyEquals(b, Vector3.zero);
    if (aValid && bValid) {
      // The epsilon used by .normalized is almost certainly too small to
      // detect nearly-parallel a and b, so this should probably be a more-real
      // robustness check. On the other hand, a and b have already been normalized,
      // and a real check likely depends on their former size.
      Vector3 vn = Vector3.Cross(a, b).normalized;
      if (ExactlyEquals(vn, Vector3.zero)) {
        return CalculateNormalToUnitDirection(a);
      } else {
        return vn;
      }
    } else if (aValid) {
      return CalculateNormalToUnitDirection(a);
    } else if (bValid) {
      return CalculateNormalToUnitDirection(b);
    } else {
      // Three coincident points; every normal is valid.
      return new Vector3(0, 1, 0);
    }
  }
}

}
