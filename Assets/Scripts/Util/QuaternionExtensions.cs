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

// TODO: add unit tests
public static class QuaternionExtensions {
  /// Like Quaternion.AngleAxis, but takes angle in radians.
  public static Quaternion AngleAxisRad(float angle, Vector3 axis) {
    // Versions that use radians are called "AxisAngle" rather than "AngleAxis".
    // They're marked deprecated because Unity wants everyone to use degrees.
#pragma warning disable 0612, 0618
    return Quaternion.AxisAngle(axis, angle);
#pragma warning restore 0612, 0618
  }

  /// Like Quaternion.ToAngleAxis, but returns angle in radians.
  public static void ToAngleAxisRad(this Quaternion q, out float angle, out Vector3 axis) {
#pragma warning disable 0612, 0618
    q.ToAxisAngle(out axis, out angle);
#pragma warning restore 0612, 0618
  }

  /// Quaternion logarithm; returns something like an angle-axis.
  /// Assumes q is a unit quaternion (saves some work, more stable).
  ///
  /// Quaternion logarithm is useful because:
  ///   result.w == 0
  ///   Len(result.xyx) = theta/2 in radians (theta is the rotation of the quat)
  ///   result.xyz.normalized = axis of rotation
  ///
  /// Quaternion log makes it easy to apply a quat multiple times.
  /// If a quat q rotates by N degrees, q*q rotates by N*2 degrees, and
  /// in general q^k rotates by N*k degrees. Scalar powers are easy,
  /// but what if you want eg 1.5x a quaternion? Use the identity:
  ///   log(q^k) = k * log(q)
  /// Then convert back to a quat
  ///   q^k = exp (k * log(q))
  public static Quaternion Log(this Quaternion q) {
    float vecLenSq = q.x*q.x + q.y*q.y + q.z*q.z;
    // This method only works on the restricted domain of unit quaternions
    {
      float lenSq = vecLenSq + q.w * q.w;
      if (Mathf.Abs(lenSq - 1) > 3e-3f) {
        throw new System.ArgumentException("Quaternion must be unit");
      }
    }

    float sinTheta = Mathf.Sqrt(vecLenSq);
    // Acos is sensitive to domain errors, eg if q.w > 1
    float theta = Mathf.Atan2(sinTheta, q.w); // Mathf.Acos(q.w);

    if (sinTheta < 1e-5f) {
      if (q.w > 0) {
        // q ~= (0,0,0, 1)
        // theta / sin(theta) = 1;
        return new Quaternion(q.x, q.y, q.z, 0);
      } else {
        // q ~= (0,0,0, -1)
        // angle is ~= pi, but the axis gets unstable
        Vector3 axis = new Vector3(q.x, q.y, q.z).normalized;
        if (axis == Vector3.zero) { axis = Vector3.up; }
        axis *= theta;
        return new Quaternion(axis.x, axis.y, axis.z, 0);
      }
    } else {
      float k = theta / sinTheta;
      return new Quaternion(k*q.x, k*q.y, k*q.z, 0);
    }
  }

  /// Quaternion exponentiation. See Log() for why this is useful.
  /// The log of a unit-length quaternion has w == 0.
  /// This method only accepts quaternions that have w == 0.
  public static Quaternion Exp(this Quaternion q) {
    // If you Exp() a non-pure-imaginary quaternion you get a non-unit quat,
    // which is not useful for rotations. Better to catch the mistake than to
    // be fully general.
    if (q.w != 0) {
      throw new System.ArgumentException("Quaternion must be pure (w=0)");
    }
    Debug.Assert(q.w == 0);
    Vector3 v = new Vector3(q.x, q.y, q.z);
    float vLen = v.magnitude;
    float sinVOverV;
    if (vLen < 1e-4f) {
      sinVOverV = vLen; // (sin x / x) -> 1 as x -> 0
    } else {
      sinVOverV = Mathf.Sin(vLen) / vLen;
    }

    v *= sinVOverV;
    return new Quaternion(v.x, v.y, v.z, Mathf.Cos(vLen));
  }

  /// Unary minus, except extension methods can't add operator overloads
  public static Quaternion Negated(this Quaternion q) {
    return new Quaternion(-q.x, -q.y, -q.z, -q.w);
  }

  /// Returns the imaginary portion of q
  public static Vector3 Im(this Quaternion q) {
    return new Vector3(q.x, q.y, q.z);
  }
  /// Returns the real/scalar portion of q
  public static float Re(this Quaternion q) {
    return q.w;
  }

  /// returns true if the Quaternion is not (0,0,0,0)
  public static bool IsInitialized(this Quaternion q) {
    return q.x != 0 || q.y != 0 || q.z != 0 || q.w != 0;
  }

  /// Analagous to Vector3.normalized (except this isn't a read-only property)
  public static Quaternion normalized(this Quaternion q) {
    Vector4 v = new Vector4(q.x, q.y, q.z, q.w).normalized;
    return new Quaternion (v.x, v.y, v.z, v.w);
  }

  /// Analagous to Vector4.magnitude (except this isn't a read-only property)
  public static float magnitude(this Quaternion q) {
    Vector4 v = new Vector4(q.x, q.y, q.z, q.w);
    return v.magnitude;
  }

  /// Workaround for Unity's Quaternion.Inverse not dividing by sqrMagnitude
  public static Quaternion TrueInverse(this Quaternion q) {
    Vector4 v = new Vector4(q.x, q.y, q.z, q.w);
    float f = 1f / v.sqrMagnitude;
    return new Quaternion(-q.x * f, -q.y * f, -q.z * f, q.w * f);
  }

  /// Workaround for Unity's Quaternion.operator == failing for non-unit
  /// quaternions (in particular, (0,0,0,0)), and for it being approximate.
  public static bool TrueEquals(this Quaternion q, Quaternion rhs) {
    return (q.x == rhs.x && q.y == rhs.y && q.z == rhs.z && q.w == rhs.w);
  }

  /// Workaround for Unity's Quaternion.operator != failing for non-unit
  /// quaternions (in particular, (0,0,0,0)), and for it being approximate,
  /// and for it doing the wrong thing with NaN
  public static bool TrueNotEquals(this Quaternion q, Quaternion rhs) {
    return (q.x != rhs.x || q.y != rhs.y || q.z != rhs.z || q.w != rhs.w);
  }
}

} // namespace TiltBrush
