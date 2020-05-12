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
public static class MathEllipsoidPld {
  /// Pass:
  ///   abc - coefficients of the ellipse equation; aka the half-extents
  ///   p - point in ellipse coordinates
  ///
  /// Returns naive results when inside the ellipse.
  public static Vector3 ClosestPointEllipsoid(
      Vector3 abc, Vector3 point,
      int maxIters=10, float distanceThreshold=5e-5f) {

    if (IsFullyInside(abc, point)) {
      // TODO: this algorithm is incredibly unstable inside the ellipse.
      // Until that's fixed, do something naive but stable.
      return CMul(CDiv(point, abc).normalized, abc);
    }

    float distanceThreshold2 = distanceThreshold * distanceThreshold;
    Vector3 abc2 = CSquare(abc);

    // ClosestPointEllipsoid(k) gives us the closest point to an ellipsoid
    // of unknown radius. Solve for the value of k that corresponds to radius=1.
    // IOW, use Newton's method to find roots of
    //   f (k) = RadiusSq(k) - 1^2
    //   f'(k) = d/dk RadiusSq(k)
    // Stop when we reach max iters, or when change in position is low.
    float k = 0;
    Vector3 closest = point; // == ClosestPointEllipsoid @ k=0
    for (int i=0; i < maxIters; ++i) {
      k = k - ((RadiusSqOfClosestPointEllipsoid(abc2, point, k) - 1)
               / ddkRadiusSqOfClosestPointEllipsoid(abc2, point, k));
      Vector3 prev = closest;
      closest = ClosestPointEllipsoid(abc2, point, k);
      if ((prev-closest).sqrMagnitude < distanceThreshold2) {
        break;
      }
    }

    return closest;
  }

  // Component-wise vector operations
  private static Vector3 CSquare(Vector3 v) { return CMul(v, v); }
  private static Vector3 CCube(Vector3 v) { return CMul(CMul(v, v), v); }
  private static Vector3 CMul(Vector3 va, Vector3 vb) {
    return new Vector3(va.x * vb.x, va.y * vb.y, va.z * vb.z);
  }
  private static Vector3 CDiv(Vector3 va, Vector3 vb) {
    return new Vector3(va.x / vb.x, va.y / vb.y, va.z / vb.z);
  }
  private static float ScalarSum(Vector3 v) { return v.x + v.y + v.z; }

  private static bool IsFullyInside(Vector3 abc, Vector3 point) {
    // convert to point-in-unit-ball check
    Vector3 unscaled = CDiv(point, abc);
    return unscaled.sqrMagnitude < 1;
  }

  // Returns a closest point on one of the family of ellipses with
  // the passed half-extent.
  //
  // Consider to be this a function of k, with abc2 and point being
  // constants. This function has singularities at k = -abc2[i].
  //
  // Pass:
  //   abc2 - square of the half-extents
  //   point - the query point
  //   k - an opaque parameter
  //
  // ClosestPointEllipsoid(k) == point. In other words,
  // k=0 represents the ellipse passing through point.
  //
  // abc2 and point are constant; consider this a function of k
  private static Vector3 ClosestPointEllipsoid(
      Vector3 abc2, Vector3 point, float k) {
    // This is a solution for X, for the equation
    //  (Point - X) = k/2 * ellipseNormal(X)
    // Derived with Mathematica
    // result_x = he_x^2 point_i / (he_x^2 + k);
    return CDiv(CMul(abc2, point),
                abc2 + k * Vector3.one);
  }

  // abc2 and point are constant; consider this a function of k
  private static float RadiusSqOfClosestPointEllipsoid(
      Vector3 abc2, Vector3 point, float k) {
    // Derived with Mathematica
    // Sum( he[i]^2 point[i]^2 / (he[i]^2 + k)^2 )
    return ScalarSum(
        CDiv(CMul(abc2, CSquare(point)),
             CSquare(abc2 + k * Vector3.one)));
  }

  // d/dk of RadiusSqOfClosestPointEllipsoid
  // abc2 and point are constant; consider this a function of k
  private static float ddkRadiusSqOfClosestPointEllipsoid(
      Vector3 abc2, Vector3 point, float k) {
    // Derived with Mathematica
    // -2 * Sum( he[i]^2 point[i]^2 / (he[i]^2 + k)^3 )
    return -2 * ScalarSum(
        CDiv(CMul(abc2, CSquare(point)),
             CCube(abc2 + k * Vector3.one)));
  }
}
} // namespace TiltBrush
