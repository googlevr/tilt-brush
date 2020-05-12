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

public static class PlaneExtensions {
  static TrTransform kReflectX = new Plane(new Vector3(1, 0, 0), 0).ToTrTransform();

  /// Reflects a point across the plane.
  public static Vector3 ReflectPoint(this Plane plane, Vector3 point) {
    return point - 2 * plane.GetDistanceToPoint(point) * plane.normal;
  }

  /// Reflects a direction across the plane.
  /// This is like ReflectPoint() if the plane passes through the origin.
  public static Vector3 ReflectVector(this Plane plane, Vector3 vec) {
    return vec - 2 * Vector3.Dot(vec, plane.normal) * plane.normal;
  }

  /// Similar to concatenating a reflection:
  ///   plane.ToTrTransform() * xf
  /// except the resulting transform doesn't flip handedness (ie, it doesn't have
  /// negative scale).  Thus it's not really a reflection; it's a carefully-crafted
  /// rotation and translation.
  ///
  /// For this to work, everyone who fake-reflects things needs to agree on what
  /// sort of fake-reflection they're going to use.  So use this one.
  public static TrTransform ReflectPoseKeepHandedness(this Plane plane, TrTransform xf) {
    // We can preserve handedness by tacking on an arbitrary reflection.
    // This particular reflection is chosen because brush geometry
    // generation happens to be symmetric in (object space) X.
    //
    // TODO: we should move to a system where
    // - stroke.m_BrushScale is allowed to be negative.
    // - brushes perform the local flip-x (or whatever) if they can't or don't want to
    //   handle negative scale.
    return plane.ToTrTransform() * xf * kReflectX;
  }

  /// Returns a TrTransform that represents a reflection across a plane.
  /// In other words, return xf such that for all Vector3 v:
  ///    xf.MultiplyPoint(v) == plane.ReflectPoint(v)
  ///    xf.MultiplyVector(v) == plane.ReflectVector(v)
  public static TrTransform ToTrTransform(this Plane plane) {
    return TrTransform.TRS(
        plane.ReflectPoint(Vector3.zero),  // aka 2 * plane.distance * plane.normal
        Quaternion.AngleAxis(180, plane.normal),  // aka Quat(plane.normal, 0)
        -1);
  }
}
} // namespace TiltBrush
