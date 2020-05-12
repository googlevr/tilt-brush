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

using JetBrains.Annotations;
using UnityEngine;

namespace TiltBrush {

/// Similar to Matrix4x4, except only handles translation, rotation,
/// and uniform scale; omits generalized scale and perspective.
///
[Newtonsoft.Json.JsonConverter(typeof(JsonTrTransformConverter))]
[System.Serializable]
public struct TrTransform {
  public Vector3 translation;
  public Quaternion rotation;
  public float scale;

  public static TrTransform identity = TR(Vector3.zero, Quaternion.identity);

  //
  // Methods analagous to Matrix4x4.TRS()
  //

  public static TrTransform T(Vector3 t) {
    return new TrTransform { translation = t, rotation = Quaternion.identity, scale = 1 };
  }

  public static TrTransform R(Quaternion r) {
    return new TrTransform { translation = Vector3.zero, rotation = r, scale = 1 };
  }

  public static TrTransform R(float angle, Vector3 axis) {
    Quaternion r = Quaternion.AngleAxis(angle, axis);
    return new TrTransform { translation = Vector3.zero, rotation = r, scale = 1 };
  }

  public static TrTransform S(float s) {
    return new TrTransform {
      translation = Vector3.zero, rotation = Quaternion.identity, scale = s };
  }

  public static TrTransform TR(Vector3 t, Quaternion r) {
    return new TrTransform { translation = t, rotation = r, scale = 1 };
  }

  public static TrTransform TRS(Vector3 t, Quaternion r, float scale) {
    return new TrTransform { translation = t, rotation = r, scale = scale };
  }

  public static TrTransform FromMatrix4x4(Matrix4x4 m) {
    TrTransform ret;
    MathUtils.DecomposeMatrix4x4(m, out ret.translation, out ret.rotation, out ret.scale);
    return ret;
  }

  /// Results are undefined if xf has non-uniform scale.
  public static TrTransform FromTransform(Transform xf) {
    return TRS(xf.position, xf.rotation, xf.GetUniformScale());
  }

  /// Results are undefined if xf has non-uniform scale.
  public static TrTransform FromLocalTransform(Transform xf) {
    return TRS(xf.localPosition, xf.localRotation, xf.localScale.x);
  }

  /// Returns a.inverse * b
  public static TrTransform InvMul(TrTransform a, TrTransform b) {
    // return a.inverse * b;
    Quaternion a_invrot = a.rotation.TrueInverse();
    return TRS(a_invrot * ((b.translation - a.translation) / a.scale),
               a_invrot * b.rotation,
               b.scale / a.scale);
  }

  /// Linearly interpolate between two TrTransforms.
  /// Translation is linearly interpolated, rotation is spherically interpolated,
  /// log of scale is linearly interpolated (so requires scale > 0).
  public static TrTransform Lerp(TrTransform a, TrTransform b, float t) {
    Debug.Assert(a.scale > 0 && b.scale > 0);
    return TRS(Vector3.Lerp(a.translation, b.translation, t),
              Quaternion.Slerp(a.rotation, b.rotation, t),
              Mathf.Exp(Mathf.Lerp(Mathf.Log(a.scale), Mathf.Log(b.scale), t)));
  }

  /// Equivalent to doing a matrix-multiply against the 4-vector (p, 1)
  /// See also MultiplyPoint(), MultiplyVector()
  public static Vector3 operator *(TrTransform a, Vector3 b) {
    return a.MultiplyPoint(b);
  }

  public static TrTransform operator *(TrTransform a, TrTransform b) {
    return TRS(a.rotation * (a.scale * b.translation) + a.translation,
               a.rotation * b.rotation,
               a.scale * b.scale);
  }

  public static Plane operator *(TrTransform xf, Plane plane) {
    // Derivation:
    // The plane equation is ax + by + cz + d = 0. Rewrite as a vector equation:
    //   given plane = vec4(a,b,c, d)
    //   given point = vec4(x,y,z, 1)
    //   plane equation is  plane dot point = 0
    //   equivalently,      plane.transpose * point = 0
    //
    // To transform by M, we want to find plane2 such that
    //   plane2.transpose * (M * point) = plane.transpose * point
    // Solve for plane2:
    //   plane2.transpose * M = plane.transpose
    //   plane2.transpose = plane.transpose * M.inverse
    //   plane2 = M.inverse.transpose * plane
    //
    // We could implement that as "return this.ToMatrix4x4().inverse.transpose * plane"
    // but we don't for a couple reasons:
    // - This might return a plane where (a,b,c).length != 1. Unity wants its planes in
    //   normalized format, since this preserves front/back orientation. Maybe we can
    //   do the normalization in-place if we expand the math. To normalize, we divide
    //   the whole plane (including plane.d) by plane.abc.
    // - It doesn't work if scale == 0; the matrix is not invertible.
    //
    // So, to calculate NormalizePlane(this.inverse.transpose * plane):
    //   TrTransform.inverse.transpose factors into (T * R * S).i.t
    //   = (S.i * R.i * T.i).t
    //   = T.i.t * R.i.t * S.i.t
    //   T.i.t = (identity matrix with -t on the bottom, where the perspective stuff normally goes)
    //   R.i.t = R
    //   S.i.t = S.i = Scale(1/s)
    // Multiply it all out (not shown) and denoting the plane (a,b,c, d) as (n, d) where n
    // is a unit-length vec3:
    //   plane2 = (TRS).it * (n, d) = (1/s R n, d - (1/s R n) dot T.translation)
    // Divide by plane2.abc.length (= 1/s) to get:
    //   plane2Normalized = (R n, s d - (R n) dot T.translation))
    Vector3 normal1 = xf.rotation * plane.normal;
    float d1 = (xf.scale * plane.distance) - Vector3.Dot(normal1, xf.translation);
    return new Plane(normal1, d1);
#if false
    // This works too, and expands out to something like the above,
    // plus a bit of extra wasted work.
    Plane ret = new Plane();
    ret.SetNormalAndPosition(
        xf.MultiplyNormal(plane.normal),
        xf.MultiplyPoint(plane.ClosestPointOnPlane(Vector3.zero)));
    return ret;
#endif
  }

  public static bool operator!=(TrTransform lhs, TrTransform rhs) {
    return !(lhs == rhs);
  }

  /// Returns true if the transforms are exactly identical, down to the
  /// quaternion components.
  public static bool operator==(TrTransform lhs, TrTransform rhs) {
    // Unity's Vector3 and Quaternion equality is approximate
    return (lhs.translation.x == rhs.translation.x &&
            lhs.translation.y == rhs.translation.y &&
            lhs.translation.z == rhs.translation.z &&
            lhs.rotation.x == rhs.rotation.x &&
            lhs.rotation.y == rhs.rotation.y &&
            lhs.rotation.z == rhs.rotation.z &&
            lhs.rotation.w == rhs.rotation.w &&
            lhs.scale == rhs.scale);
  }

  /// Returns true if the transforms are approximately equal.
  public static bool Approximately(TrTransform lhs, TrTransform rhs) {
    // Unity's Vector3 and Quaternion equality is approximate
    return (lhs.translation == rhs.translation &&
            lhs.rotation == rhs.rotation &&
            Mathf.Approximately(lhs.scale, rhs.scale));
  }

  public TrTransform inverse {
    get {
      var rinv = this.rotation.TrueInverse();
      float invScale = 1f / this.scale;
      return TRS((rinv * this.translation) * -invScale,
                 rinv,
                 invScale);
    }
  }

  public Vector3 forward {
    get {
      return this.rotation * Vector3.forward;
    }
  }

  public Vector3 up {
    get {
      return this.rotation * Vector3.up;
    }
  }

  public Vector3 right {
    get {
      return this.rotation * Vector3.right;
    }
  }

  public bool IsFinite() {
    return
        !float.IsNaN(translation.x) && !float.IsInfinity(translation.x) &&
        !float.IsNaN(translation.y) && !float.IsInfinity(translation.y) &&
        !float.IsNaN(translation.z) && !float.IsInfinity(translation.z) &&
        !float.IsNaN(rotation.x) && !float.IsInfinity(rotation.x) &&
        !float.IsNaN(rotation.y) && !float.IsInfinity(rotation.y) &&
        !float.IsNaN(rotation.z) && !float.IsInfinity(rotation.z) &&
        !float.IsNaN(rotation.w) && !float.IsInfinity(rotation.w) &&
        !float.IsNaN(scale) && !float.IsInfinity(scale);
  }

  public override string ToString() {
    // return string.Format("T: {0}\nR: {1}\n S: {2}", translation, rotation, scale);
    return string.Format("T: {0:e} {1:e} {2:e}\nR: {3:e} {4:e} {5:e}  {6:e}\n S: {7:e}",
                         translation.x, translation.y, translation.z,
                         rotation.x, rotation.y, rotation.z, rotation.w,
                         scale);
  }

  public override bool Equals(System.Object o) {
    if (o is TrTransform) {
      TrTransform rhs = (TrTransform)o;
      return (this == rhs);
    } else {
      return false;
    }
  }

  public override int GetHashCode() {
    return translation.GetHashCode() ^ rotation.GetHashCode() ^ scale.GetHashCode();
  }

  public Matrix4x4 ToMatrix4x4() {
    Vector3 vscale = new Vector3(this.scale, this.scale, this.scale);
    return Matrix4x4.TRS(translation, rotation, vscale);
  }

  public void ToTransform(Transform xf) {
    xf.position = this.translation;
    xf.rotation = this.rotation;
    xf.SetUniformScale(this.scale);
  }

  public void ToLocalTransform(Transform xf) {
    xf.localPosition = this.translation;
    xf.localRotation = this.rotation;
    xf.localScale = new Vector3(scale, scale, scale);
  }

  /// Equivalent to doing a matrix-multiply against the 4-vector (p, 1)
  [Pure] public Vector3 MultiplyPoint(Vector3 p) {
    // The matrix can be factored into [T] * [R] * [S], so:
    //    (T * (R * (S * v)))
    return translation + (rotation * (scale * p));
  }

  /// Equivalent to doing a matrix-multiply against the 4-vector (p, 0)
  [Pure] public Vector3 MultiplyVector(Vector3 v) {
    return rotation * (scale * v);
  }

  /// Multiply a bivector (the result of a cross-product).
  /// Use this for things with units of distance^2, like angular momentum.
  /// See http://www.terathon.com/gdc12_lengyel.pdf (starting at "Vector / bivector confusion")
  [Pure] public Vector3 MultiplyBivector(Vector3 v) {
    return rotation * ((scale * scale) * v);
  }

  /// Transforms a normal or a non-distance quantity like angular velocity.
  /// Leaves length unchanged, at least under uniform scale.
  /// See https://computergraphics.stackexchange.com/a/1506/6478
  [Pure] public Vector3 MultiplyNormal(Vector3 v) {
    return rotation * v;
  }

  /// Changes the coordinate system of an active transformation.
  ///
  /// 'this' should be an active transformation, like "rotate 10 degrees about up".
  /// Its input and output coordinate systems should be the same.
  ///
  /// 'rhs' should be a passive transformation -- like a WorldFromObject
  /// coordinate change (aka a pose). Its input coordinate system should
  /// be the same as the coordinate system of 'this'.
  ///
  /// Returns a transform that performs the same action as 'this', but that operates
  /// in a different coordinate system: the output coordinate system of 'rhs'.
  ///
  /// See also https://en.wikipedia.org/wiki/Active_and_passive_transformation
  public TrTransform TransformBy(TrTransform rhs) {
    // Solving "X * rhs == rhs * this" for X, we get
    //
    //      X = rhs * this * inv(rhs)
    //
    // However, this naive calculation is inaccurate; manually expanding and
    // simplifying allows an unnecessary multiply+divide by rhs.scale to be
    // removed.
    //
    // This increases accuracy of .translation and .scale, and avoids problems
    // when rhs is non-invertible as a result of zero scale.
    Quaternion similar = (rhs.rotation * this.rotation * rhs.rotation.TrueInverse());
    Vector3 retTrans = similar * (-this.scale * rhs.translation)
      + rhs.rotation * (rhs.scale * this.translation)
      + rhs.translation;

    return new TrTransform {
      translation = retTrans,
      rotation = similar,
      scale = this.scale
    };
  }
}

} // namespace TiltBrush
