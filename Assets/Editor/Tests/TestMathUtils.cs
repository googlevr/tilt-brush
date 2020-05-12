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

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Random = UnityEngine.Random;

namespace TiltBrush {

internal class WithRandomSeed : IDisposable {
  private UnityEngine.Random.State m_prevState;
  public WithRandomSeed(int seed) {
    m_prevState = Random.state;
    Random.InitState(seed);
  }
  public void Dispose() {
    Random.state = m_prevState;
  }
}

// I am so sorry about these class names.
internal class TestMathUtils : MathTestUtils {
  [TestCase( 1, 11,    0)]      // Result is -period
  [TestCase( 0,  8,    2)]      // Result in (-period, -period/2)
  [TestCase( 2,  7,   -5)]      // Result is -period/2
  [TestCase( 4,  8,   -4)]      // Result in (-period/2, 0)
  [TestCase( 4,  4,    0)]      // Result is 0
  [TestCase( 8,  4,    4)]      // Result in (0, period/2)
  [TestCase( 7,  2,   -5)]      // Result is period/2
  [TestCase( 8,  0,   -2)]      // Result in (period/2, period)
  [TestCase( 11, 1,    0)]      // Result is period
  public void TestPeriodicDifference(float lhs, float rhs, float expected) {
    float PERIOD = 10;
    for (int i = -1; i <= 1; ++i) {
      for (int j = -1; j <= 1; ++j) {
        float result = MathUtils.PeriodicDifference(
            lhs + i*PERIOD, rhs + j*PERIOD, PERIOD);
        Assert.AreEqual(expected, result);
      }
    }
  }

  [Test]
  public void TestGetAngleBetween() {
    int kNumTests = 30;
    int kNumOffsets = 5;
    // Test doesn't do a good job of avoiding instability, so just crank this up
    // in lieu of doing a more precise job (eg, by using the "stability" output)
    float kEpsilon = 5e-2f;

    for (int iTest = 0; iTest < kNumTests; ++iTest) {
      Vector3 axis = Random.onUnitSphere;
      float angle = Random.Range(-179f, 179f);
      // test too unstable; should write a better one if we need to test precision
      if (angle < 1f) { continue; }
      Quaternion q = Quaternion.AngleAxis(angle, axis);

      Vector3 v0 = Random.onUnitSphere;
      Vector3 v1 = q * v0;
      for (int iOffset = 0; iOffset < kNumOffsets; ++iOffset) {
        float stability;
        Vector3 v0p = v0 + Random.Range(-100f, 100f) * axis;
        Vector3 v1p = v1 + Random.Range(-100f, 100f) * axis;
        float result = MathUtils.GetAngleBetween(v0p, v1p, axis, out stability);
        Assert.AreEqual(angle, result, kEpsilon);
        result = MathUtils.GetAngleBetween(v1, v0, axis, out stability);
        Assert.AreEqual(angle, -result, kEpsilon);
      }
    }
  }

  const float kLeft = -1;
  const float kRight = 2;
  const float kBottom = -2;
  const float kTop = 6;
  const float kNear = 1;
  const float kFar = 10;
  static readonly Matrix4x4 kOffCenter = MathUtils.PerspectiveOffCenter(
      kLeft, kRight, kBottom, kTop, kNear, kNear, kFar);
  // Runs v through projection matrix, returns NDC
  static Vector3 Project(float x, float y, float z) {
    // Note that by convention, perspective matrices are right-handed,
    // with x = right, y = up, -z = forward
    Vector4 clip = kOffCenter * new Vector4(x, y, z, 1);
    var v = new Vector3(clip.x, clip.y, clip.z) / clip.w;
    return v;
  }

  [Test]
  public void TestPerspectiveOffCenter() {
    float k = kFar/kNear;
    // Test that the corners of the frustum cube map to the NDC cube.
    AssertAlmostEqual(Project(kLeft,  kBottom, -kNear), new Vector3(-1, -1, -1));
    AssertAlmostEqual(Project(kLeft,  kTop,    -kNear), new Vector3(-1,  1, -1));
    AssertAlmostEqual(Project(kRight, kBottom, -kNear), new Vector3( 1, -1, -1));
    AssertAlmostEqual(Project(kRight, kTop,    -kNear), new Vector3( 1,  1, -1));
    AssertAlmostEqual(Project(k*kLeft,  k*kBottom, k*-kNear), new Vector3(-1, -1,  1));
    AssertAlmostEqual(Project(k*kLeft,  k*kTop,    k*-kNear), new Vector3(-1,  1,  1));
    AssertAlmostEqual(Project(k*kRight, k*kBottom, k*-kNear), new Vector3( 1, -1,  1));
    AssertAlmostEqual(Project(k*kRight, k*kTop,    k*-kNear), new Vector3( 1,  1,  1));
  }

  [TestCase(false)]
  [TestCase(true)]
  public void TestDecomposeMatrix4x4(bool withMirror) {
    for (int i = 0; i < 50; ++i) {
      float scale = Random.Range(2f, 100f);
      if (Random.value < .5f) {
        scale = 1 / scale;
      }
      if (withMirror) { scale *= -1; }
      Vector3 translation = Random.insideUnitSphere * 10;
      Quaternion rotation = Random.rotationUniform;

      var m4 = Matrix4x4.TRS(translation, rotation, new Vector3(scale, scale, scale));

      Vector3 t2; Quaternion r2; float s2;
      MathUtils.DecomposeMatrix4x4(m4, out t2, out r2, out s2);
      AssertAlmostEqual(translation, t2, abseps:1e-5f, releps:1e-5f);
      AssertAlmostEqual(rotation, r2, abseps:1e-5f, releps:1e-5f, allowFlip:true);
      AssertAlmostEqual(scale, s2, abseps:1e-5f, releps:1e-5f);
    }
  }

  [Test]
  public void TestQuadratic() {
    bool success;
    float r0, r1;

    // Identical roots
    success = MathUtils.SolveQuadratic(1, -4, 4, out r0, out r1);
    Assert.IsTrue(success);
    AssertAlmostEqual(r0, 2);
    AssertAlmostEqual(r1, 2);

    // Identical roots again
    // (2x - 4) (4x - 8) = 0    roots: 2, 2
    // 8x^2 + (-16-16)x + 32 = 0
    success = MathUtils.SolveQuadratic(8, -16-16, 32, out r0, out r1);
    Assert.IsTrue(success);
    AssertAlmostEqual(r0, 2);
    AssertAlmostEqual(r1, 2);

    // 2 roots, one of each sign
    // (2x - 4) (4x + 3) = 0    roots: 2, -3/4
    // 8x^2 + (6-16)x - 12 = 0
    success = MathUtils.SolveQuadratic(8, 6-16, -12, out r0, out r1);
    Assert.IsTrue(success);
    AssertAlmostEqual(r0, -3f/4);
    AssertAlmostEqual(r1, 2);

    // No roots
    success = MathUtils.SolveQuadratic(1, 0, 1, out r0, out r1);
    Assert.IsFalse(success);
  }

  [Test]
  public void TestRaySphereIntersection() {
    Vector3 center = new Vector3(1, 0, 0);
    float radius = 2;

    float t0, t1;
    bool success;

    // Straight through the center
    success = MathUtils.RaySphereIntersection(
        new Vector3(-2, 0, 0),
        new Vector3(2, 0, 0),
        center, radius, out t0, out t1);
    Assert.IsTrue(success);
    AssertAlmostEqual(t0, .5f);
    AssertAlmostEqual(t1, 2.5f);

    // Straight through the center (opposite direction)
    success = MathUtils.RaySphereIntersection(
        new Vector3(-2, 0, 0),
        new Vector3(-2, 0, 0),
        center, radius, out t0, out t1);
    Assert.IsTrue(success);
    AssertAlmostEqual(t0, -2.5f);
    AssertAlmostEqual(t1, -.5f);

    // Tangent
    success = MathUtils.RaySphereIntersection(
        new Vector3(0, 2, 0),
        new Vector3(2, 0, 0),
        center, radius, out t0, out t1);
    Assert.IsTrue(success);
    AssertAlmostEqual(t0, .5f);
    AssertAlmostEqual(t1, .5f);

    // A miss
    success = MathUtils.RaySphereIntersection(
        new Vector3(0, 2.1f, 0),
        new Vector3(2, 0, 0),
        center, radius, out t0, out t1);
    Assert.IsFalse(success);
  }


  // Movement, rotation, and scale. No scale clamping.
  [TestCase(-1,  1,  0,   3, -1,  0,
            -1, -2,  1,   1,  6,  1,
            -1, -1,    2,
            0, 0, 1,
            90,  0, 0, 1,
            TestName="TRS no clamp")]
  // No movement or rotation. Clamp to min scale; controllers on opposite sides.
  [TestCase(-4,  0,  0,   4,  0,  0,
            -1,  0,  0,   1,  0,  0,
            .5f, 2,    .5f,
            0, 0, 0,
            0,  0, 0, 0,
            TestName="S, min, opposite")]
  // No movement or rotation. Clamp to max scale; controllers on opposite sides.
  [TestCase(-1, 0, 0,  1, 0, 0,
            -4, 0, 0,  4, 0, 0,
            .5f, 2,    2,
            0, 0, 0,
            0,  0, 0, 0,
            TestName="S, max, opposite")]
  // No movement or rotation. Clamp to min scale; controllers on same sides.
  [TestCase(-16, 0, 0,  0, 0, 0,
            -2, 0, 0,   0, 0, 0,
            .5f, 2,     .5f,
            0, 0, 0,
            0,  0, 0, 0,
            TestName="S, min, same")]
  // No movement or rotation. Clamp to max scale; controllers on same sides.
  [TestCase(-12, 0, 0,  -10, 0, 0,
            -16, 0, 0,   -6, 0, 0,
            .5f, 2,     2,
            11, 0, 0,
            0,  0, 0, 0,
            TestName="S, max, same")]
  // No movement or rotation. Clamp to min scale starting at min scale; controllers on same sides.
  [TestCase(-16, 0, 0,   -1, 0, 0,
            -2, 0, 0,    -1, 0, 0,
            1, 2,     1,
            0, 0, 0,
            0,  0, 0, 0,
            TestName="S, min from min, same")]
  // No movement or rotation. Clamp to max scale starting at max scale; controllers on same sides.
  [TestCase(-12, 0, 0,  -10, 0, 0,
            -16, 0, 0,   -6, 0, 0,
            0.5f, 1,     1,
            0, 0, 0,
            0,  0, 0, 0,
            TestName="S, max from max, same")]
  public void TestTwoPointNonUniformScale(
      float l0x, float l0y, float l0z, float r0x, float r0y, float r0z,
      float l1x, float l1y, float l1z, float r1x, float r1y, float r1z,
      float deltaScaleMin, float deltaScaleMax,
      float desiredDeltaScale,
      float tx, float ty, float tz,
      float angle, float ax, float ay, float az) {
    float deltaScale;
    var obj1 = MathUtils.TwoPointObjectTransformationNonUniformScale(
        new Vector3(1, 0, 0),  // axis
        TrTransform.T(new Vector3(l0x, l0y, l0z)),
        TrTransform.T(new Vector3(r0x, r0y, r0z)),
        TrTransform.T(new Vector3(l1x, l1y, l1z)),
        TrTransform.T(new Vector3(r1x, r1y, r1z)),
        TrTransform.identity,
        out deltaScale,
        deltaScaleMin: deltaScaleMin, deltaScaleMax: deltaScaleMax);
    AssertAlmostEqual(deltaScale, desiredDeltaScale);
    AssertAlmostEqual(obj1.translation, new Vector3(tx, ty, tz));
    AssertAlmostEqual(obj1.rotation, angle, new Vector3(ax, ay, az));
  }

  [TestCase(-1, 0, 0,  1, 0, 0,
            -2, 0, 0,  2, 0, 0,
            0.5f,
            0, 0, 0,
            0,  0, 0, 0,
            TestName="scale, constraint at 0.5")]
  [TestCase(-1, 0, 0,  1, 0, 0,
            -2, 0, 0,  2, 0, 0,
            2,
            3, 0, 0,
            0,  0, 0, 0,
            TestName="scale, constraint at 2")]
  public void TestTwoPointObjectTransformationNoScale(
      float l0x, float l0y, float l0z, float r0x, float r0y, float r0z,
      float l1x, float l1y, float l1z, float r1x, float r1y, float r1z,
      float constraintPositionT,
      float tx, float ty, float tz,
      float angle, float ax, float ay, float az) {
    var obj1 = MathUtils.TwoPointObjectTransformationNoScale(
        TrTransform.T(new Vector3(l0x, l0y, l0z)),
        TrTransform.T(new Vector3(r0x, r0y, r0z)),
        TrTransform.T(new Vector3(l1x, l1y, l1z)),
        TrTransform.T(new Vector3(r1x, r1y, r1z)),
        TrTransform.identity, constraintPositionT);
    AssertAlmostEqual(obj1.translation, new Vector3(tx, ty, tz));
    AssertAlmostEqual(obj1.rotation, angle, new Vector3(ax, ay, az));
    AssertAlmostEqual(obj1.scale, 1);
  }

  // Like Matrix4x4.TRS().inverse but more precise
  static Matrix4x4 Matrix4x4_InvTRS(Vector3 t, Quaternion r, Vector3 s) {
    // TRS.inv = (T * R * S).inv
    //   = S.inv * R.inv * T.inv
    return Matrix4x4.Scale(new Vector3(1/s.x, 1/s.y, 1/s.z))
      * Matrix4x4.TRS(Vector3.zero, Quaternion.Inverse(r), Vector3.one)
      * Matrix4x4.TRS(-t, Quaternion.identity, Vector3.one);
  }

  [Test]
  public void TestTwoPointNonUniform_Random() {
    // Test doesn't try very hard to avoid unstable regions
    int ALLOWED_FAILURES = 2;
    int TRIES = 5000;

    int seed = (int)((EditorApplication.timeSinceStartup % 1) * 100000);
    Random.InitState(seed);

    int failures = 0;
    for (int i = 0; i < TRIES; ++i) {
      try {
        var xfL0 = TrTransform.T(10 * Random.onUnitSphere);
        var xfR0 = TrTransform.T(xfL0.translation + 30 * Random.onUnitSphere);
        var xfL1 = TrTransform.T(xfL0.translation + 5 * Random.onUnitSphere);
        var xfR1 = TrTransform.T(xfR0.translation + 5 * Random.onUnitSphere);
        Vector3 axis = new Vector3(1, 0, 0);
        var obj0 = TrTransform.identity;
        float deltaScale;
        var obj1 = MathUtils.TwoPointObjectTransformationNonUniformScale(
            axis, xfL0, xfR0, xfL1, xfR1, obj0, out deltaScale);
        Assert.GreaterOrEqual(deltaScale, 0);

        // This unit test is too vulnerable to instability; detect unstable
        // regions in lieu of writing a better test
        if (deltaScale < .01f || deltaScale > 100f) {
          continue;
        }

        // Invariant: local-space grip positions are the same, before and after.
        // However, the invariant is broken when there is no solution (ie, when
        // deltaScale = 0)
        if (deltaScale > 1e-4f) {
          // inverse of identity is identity
          Matrix4x4 mInvObj0 = Matrix4x4.identity;
          Matrix4x4 mInvObj1 = Matrix4x4_InvTRS(
              obj1.translation, obj1.rotation, new Vector3(deltaScale, 1, 1));

          // Accuracy is quite poor :-/
          float ABSEPS = 2e-3f;
          float RELEPS = 5e-3f;

          try {
            var vL0 = mInvObj0.MultiplyPoint(xfL0.translation);
            var vL1 = mInvObj1.MultiplyPoint(xfL1.translation);
            CheckAlmostEqual(vL1, vL0, ABSEPS, RELEPS, "left");

            var vR0 = mInvObj0.MultiplyPoint(xfR0.translation);
            var vR1 = mInvObj1.MultiplyPoint(xfR1.translation);
            CheckAlmostEqual(vR1, vR0, ABSEPS, RELEPS, "right");
          } catch (NotAlmostEqual e) {
            Assert.Fail(e.Message);
          }
        }
      } catch (System.Exception) {
        if (++failures > ALLOWED_FAILURES) {
          Debug.LogFormat("Failed on seed {0} iteration {1}", seed, i);
          throw;
        }
      }
    }
  }

  [Test]
  public void TestTwoPointTransformationAxisResize_Simple() {
    // TODO: test the bounds clamping too
    // Just a simple sanity check
    var xfL0 = TrTransform.T(new Vector3(3, 4, 1));
    var xfR0 = TrTransform.T(new Vector3(4, 4, 1));
    var xfL1 = TrTransform.T(new Vector3(3, 2, 2));
    var xfR1 = TrTransform.T(new Vector3(3, 4, 2));
    var axis = new Vector3(1, 0, 0);
    var size = 8;
    var obj0 = TrTransform.T(new Vector3(2, 3, 0));
    float deltaScale;
    var obj1 = MathUtils.TwoPointObjectTransformationAxisResize(
        axis, size, xfL0, xfR0, xfL1, xfR1, obj0,
        out deltaScale);
    AssertAlmostEqual(deltaScale, 9/8f);
    AssertAlmostEqual(obj1.rotation, 90, new Vector3(0, 0, 1));
    AssertAlmostEqual(obj1.translation, new Vector3(4, 1.5f, 1));
  }

  static Vector3 CMul(Vector3 va, Vector3 vb) {
    return new Vector3(va.x * vb.x, va.y * vb.y, va.z * vb.z);
  }
  static Vector3 CDiv(Vector3 va, Vector3 vb) {
    return new Vector3(va.x / vb.x, va.y / vb.y, va.z / vb.z);
  }

  // Given a point on an ellipsoid of unknown radius,
  // return the surface normal at that point.
  static Vector3 NormalAtEllipsoid(Vector3 he, Vector3 p) {
    return CDiv(p, CMul(he, he)).normalized;
  }

  private static Vector3 EllipsoidSphericalToCartesian(Vector3 abc, Vector2 sphericalDegrees) {
    Vector2 spherical = sphericalDegrees * Mathf.PI / 180;
    float theta = spherical.x, phi = spherical.y;
    float sinTheta = Mathf.Sin(theta), cosTheta = Mathf.Cos(theta);
    float sinPhi   = Mathf.Sin(phi),   cosPhi   = Mathf.Cos(phi);
    return new Vector3(abc.x * sinTheta * cosPhi,
                       abc.y * sinTheta * sinPhi,
                       abc.z * cosTheta);
  }

  private static int GetOctant(Vector3 v) {
    int result = 0;
    if (v.x < 0) result |= 1;
    if (v.y < 0) result |= 2;
    if (v.z < 0) result |= 4;
    return result;
  }

  private delegate Vector3 EllipsoidClosestPoint_t(Vector3 abc, Vector3 point);

  // Tests random points both inside and outside the ellipsoid
  private void TestEllipsoidRandomPoints(EllipsoidClosestPoint_t func) {
    Vector3 he = new Vector3(4,3,2);
    for (int i = 0; i < 100; ++i) {
      Vector3 point = CMul(he, Random.onUnitSphere * 2);
      Vector3 cp = func(he, point);
      Vector3 surfaceNormal = NormalAtEllipsoid(he, cp);
      // We don't have ground truth, so instead we verify this invariant:
      // The normal at cp is parallel to (point - cp).
      Vector3 delta = (point - cp);
      Vector3 err = delta - (Vector3.Dot(surfaceNormal,delta) * surfaceNormal);
      AssertAlmostEqual(err, Vector3.zero, message: point);
    }
  }

  [Test]
  [Ignore("Original is not robust enough for this test")]
  public void TestEllipsoidTangent_Pld() {
    TestEllipsoidRandomPoints((a,b) => MathEllipsoidPld.ClosestPointEllipsoid(a, b));
  }

  [Test]
  public void TestEllipsoidTangent_Anton() {
    TestEllipsoidRandomPoints((a,b) => MathEllipsoidAnton.ClosestPointEllipsoid(a,b));
  }

  [Test]
  public void TestEllipsoidTangent_Eberly() {
    TestEllipsoidRandomPoints((a,b) => MathEllipsoidEberly.ClosestPointEllipsoid(a,b));
  }

  private void TestEllipsoidInOctant(EllipsoidClosestPoint_t func) {
    using (var unused = new WithRandomSeed(4)) {
      Vector3 abc = new Vector3(4,3,2);

      for (int theta = 0; theta < 360; theta += 7)
      for (int phi = 0; phi < 180; phi += 7)
      for (float bump = -1; bump <= 4f; bump += .32f) {
        // Find a spot on the ellipsoid, bump it out by its normal
        Vector3 pointOnEllipse = EllipsoidSphericalToCartesian(abc, new Vector2(theta, phi));
        Vector3 normal = NormalAtEllipsoid(abc, pointOnEllipse);
        Vector3 bumped = pointOnEllipse + normal * bump;
        // pointOnEllipse is guaranteed to be the closest point to "bumped" as long as we
        // haven't bumped the point into a different octant.
        if (GetOctant(bumped) != GetOctant(pointOnEllipse)) {
          continue;
        }

        AssertAlmostEqual(pointOnEllipse, func(abc, bumped), message: (theta, phi, bump));
      }
    }
  }

  [Test]
  public void TestEllipsoidInOctant_Pld() {
    TestEllipsoidInOctant((a, b) => MathEllipsoidPld.ClosestPointEllipsoid(a, b, 10, (float) 5E-05));
  }

  [Test]
  public void TestEllipsoidInOctant_Anton() {
    TestEllipsoidInOctant((a, b) => MathEllipsoidAnton.ClosestPointEllipsoid(a,b));
  }

  [Test]
  public void TestEllipsoidInOctant_Eberly() {
    TestEllipsoidInOctant((a, b) => MathEllipsoidEberly.ClosestPointEllipsoid(a,b));
  }

  // On Windows, this checks the C++ implementation against the original C# implementation.
  // To test the current C# implementation, comment out the USE_TILT_BRUSH_CPP definitions at the
  // top of MathUtils.cs.
  [Test]
  public void TestTransformVector3AsPoint() {
    TrTransform randomXf = RandomTr();
    Matrix4x4 mat = randomXf.ToMatrix4x4();
    int listSize = 10;
    var list = RandomVector3List(listSize);
    var listDuplicate = new List<Vector3>(list);
    int iVert = 3;
    int iVertEnd = 8;

    // Do the original version.
    for (int i = iVert; i < iVertEnd; i++) {
      list[i] = randomXf.MultiplyPoint(list[i]);
    }

    // Do the math utils version.
    MathUtils.TransformVector3AsPoint(mat, iVert, iVertEnd, listDuplicate.GetBackingArray());

    // Check results.
    for (int i = 0; i < listSize; i++) {
      AssertAlmostEqual(list[i], listDuplicate[i]);
    }
  }

  // On Windows, this checks the C++ implementation against the original C# implementation.
  // To test the current C# implementation, comment out the USE_TILT_BRUSH_CPP definitions at the
  // top of MathUtils.cs.
  [Test]
  public void TestTransformVector3AsVector() {
    TrTransform randomXf = RandomTr();
    Matrix4x4 mat = randomXf.ToMatrix4x4();
    int listSize = 10;
    var list = RandomVector3List(listSize);
    var listDuplicate = new List<Vector3>(list);
    int iVert = 3;
    int iVertEnd = 8;

    // Do the original version.
    for (int i = iVert; i < iVertEnd; i++) {
      list[i] = randomXf.MultiplyVector(list[i]);
    }

    // Do the math utils version.
    MathUtils.TransformVector3AsVector(mat, iVert, iVertEnd, listDuplicate.GetBackingArray());

    // Check results.
    for (int i = 0; i < listSize; i++) {
      AssertAlmostEqual(list[i], listDuplicate[i]);
    }
  }

  // On Windows, this checks the C++ implementation against the original C# implementation.
  // To test the current C# implementation, comment out the USE_TILT_BRUSH_CPP definitions at the
  // top of MathUtils.cs.
  [Test]
  public void TestTransformVector3AsZDistance() {
    TrTransform randomXf = RandomTr();
    int listSize = 10;
    var list = RandomVector3List(listSize);
    var listDuplicate = new List<Vector3>(list);
    int iVert = 3;
    int iVertEnd = 8;

    // Do the original version.
    for (int i = iVert; i < iVertEnd; i++) {
      list[i] = new Vector3(list[i].x, list[i].y, randomXf.scale * list[i].z);
    }

    // Do the math utils version.
    MathUtils.TransformVector3AsZDistance(randomXf.scale, iVert, iVertEnd, listDuplicate.GetBackingArray());

    // Check results.
    for (int i = 0; i < listSize; i++) {
      AssertAlmostEqual(list[i], listDuplicate[i]);
    }
  }

  // On Windows, this checks the C++ implementation against the original C# implementation.
  // To test the current C# implementation, comment out the USE_TILT_BRUSH_CPP definitions at the
  // top of MathUtils.cs.
  [Test]
  public void TestTransformVector4AsPoint() {
    TrTransform randomXf = RandomTr();
    Matrix4x4 mat = randomXf.ToMatrix4x4();
    int listSize = 10;
    var list = RandomVector4List(listSize);
    var listDuplicate = new List<Vector4>(list);
    int iVert = 3;
    int iVertEnd = 8;

    // Do the original version.
    for (int i = iVert; i < iVertEnd; i++) {
      Vector3 v3 = new Vector3(list[i].x, list[i].y, list[i].z);
      Vector3 v3Transformed = randomXf.MultiplyPoint(v3);
      list[i] = new Vector4(v3Transformed.x, v3Transformed.y, v3Transformed.z, list[i].w);
    }

    // Do the math utils version.
    MathUtils.TransformVector4AsPoint(mat, iVert, iVertEnd, listDuplicate.GetBackingArray());

    // Check results.
    for (int i = 0; i < listSize; i++) {
      AssertAlmostEqualV4(list[i], listDuplicate[i]);
    }
  }

  // On Windows, this checks the C++ implementation against the original C# implementation.
  // To test the current C# implementation, comment out the USE_TILT_BRUSH_CPP definitions at the
  // top of MathUtils.cs.
  [Test]
  public void TestTransformVector4AsVector() {
    TrTransform randomXf = RandomTr();
    Matrix4x4 mat = randomXf.ToMatrix4x4();
    int listSize = 10;
    var list = RandomVector4List(listSize);
    var listDuplicate = new List<Vector4>(list);
    int iVert = 3;
    int iVertEnd = 8;

    // Do the original version.
    for (int i = iVert; i < iVertEnd; i++) {
      Vector3 v3 = new Vector3(list[i].x, list[i].y, list[i].z);
      Vector3 v3Transformed = randomXf.MultiplyVector(v3);
      list[i] = new Vector4(v3Transformed.x, v3Transformed.y, v3Transformed.z, list[i].w);
    }

    // Do the math utils version.
    MathUtils.TransformVector4AsVector(mat, iVert, iVertEnd, listDuplicate.GetBackingArray());

    // Check results.
    for (int i = 0; i < listSize; i++) {
      AssertAlmostEqual(list[i], listDuplicate[i]);
    }
  }

  // On Windows, this checks the C++ implementation against the original C# implementation.
  // To test the current C# implementation, comment out the USE_TILT_BRUSH_CPP definitions at the
  // top of MathUtils.cs.
  [Test]
  public void TestTransformVector4AsZDistance() {
    TrTransform randomXf = RandomTr();
    int listSize = 10;
    var list = RandomVector4List(listSize);
    var listDuplicate = new List<Vector4>(list);
    int iVert = 3;
    int iVertEnd = 8;

    // Do the original version.
    for (int i = iVert; i < iVertEnd; i++) {
      list[i] = new Vector4(list[i].x, list[i].y, randomXf.scale * list[i].z, list[i].w);
    }

    // Do the math utils version.
    MathUtils.TransformVector4AsZDistance(randomXf.scale, iVert, iVertEnd, listDuplicate.GetBackingArray());

    // Check results.
    for (int i = 0; i < listSize; i++) {
      AssertAlmostEqual(list[i], listDuplicate[i]);
    }
  }

  // On Windows, this checks the C++ implementation against the original C# implementation.
  // To test the current C# implementation, comment out the USE_TILT_BRUSH_CPP definitions at the
  // top of MathUtils.cs.
  [TestCase(true, TestName = "random transform")]
  [TestCase(false, TestName = "identity transform")]
  public void TestGetBoundsFor(bool randomTransform) {
    TrTransform xf = randomTransform ? RandomTr() : TrTransform.identity;
    int listSize = 10;
    var list = RandomVector3List(listSize);
    int iVert = 3;
    int iVertEnd = 8;

    // Do the original version.
    Vector3[] transformedVert;
    transformedVert = new Vector3[list.GetBackingArray().Length];
    list.GetBackingArray().CopyTo(transformedVert, 0);
    MathUtils.TransformVector3AsPoint(xf.ToMatrix4x4(), iVert, iVertEnd,
                                      transformedVert);
    float minX = transformedVert[iVert].x;
    float maxX = minX;
    float minY = transformedVert[iVert].y;
    float maxY = minY;
    float minZ = transformedVert[iVert].z;
    float maxZ = minZ;
    for (int i = iVert + 1; i < iVertEnd; ++i) {
      if (minX > transformedVert[i].x) {
        minX = transformedVert[i].x;
      } else if (maxX < transformedVert[i].x) {
        maxX = transformedVert[i].x;
      }
      if (minY > transformedVert[i].y) {
        minY = transformedVert[i].y;
      } else if (maxY < transformedVert[i].y) {
        maxY = transformedVert[i].y;
      }
      if (minZ > transformedVert[i].z) {
        minZ = transformedVert[i].z;
      } else if (maxZ < transformedVert[i].z) {
        maxZ = transformedVert[i].z;
      }
    }
    Vector3 center = new Vector3(0.5f * (minX + maxX),
                                 0.5f * (minY + maxY),
                                 0.5f * (minZ + maxZ));
    Vector3 size = new Vector3(maxX - minX,
                               maxY - minY,
                               maxZ - minZ);

    // Do the math utils version.
    Vector3 centerTest = new Vector3();
    Vector3 sizeTest = new Vector3();
    MathUtils.GetBoundsFor(xf.ToMatrix4x4(), iVert, iVertEnd, list.GetBackingArray(),
        out centerTest, out sizeTest);

    // Check results.
    AssertAlmostEqual(center, centerTest);
    AssertAlmostEqual(size, sizeTest);
  }

  // Checks the invariant:
  //  3 distinct(!) points on a circle of radius r have menger curvature 1/r
  // Check is made on a circle at the origin, as well as an arbitrary-placed circle
  [Test]
  public void TestMengerCurvatureNonDegenerate() {
    float kRadius = 2.5f;
    float kInvRadius = 1/kRadius;

    Vector3 radius = new Vector3(kRadius, 0, 0);
    Vector3 axis = new Vector3(0, 1, 0);  // should be perpendicular to radius

    TrTransform arbitrary = TrTransform.TR(
        new Vector3(5,8,-10),
        Quaternion.AngleAxis(35, new Vector3(-1, 3, -5)));

    // Pick a bunch of triplets on a circle of radius r.
    // Points are chosen by selecting two arc angles a, b, and computing the third arc angle c.
    // Repetitions are avoided by choosing a, b, c such that a <= b <= c.
    // Degenerate cases are avoided by choosing a > 0 (implying a, b, c > 0).

    // If a > 120, one of b or c will always be smaller than a
    for (int a = 12; a <= 120; a += 24) {
      for (int b = a; /*b <= c*/; b += 24) {
        int c = 360 - a - b;
        if (! (b <= c)) { break; }
        Vector3 va = TrTransform.R(0, axis) * radius;
        Vector3 vb = TrTransform.R(a, axis) * radius;
        Vector3 vc = TrTransform.R(a+b, axis) * radius;
        AssertAlmostEqual(kInvRadius, MathUtils.MengerCurvature(va, vb, vc));

        Vector3 va2 = arbitrary * va;
        Vector3 vb2 = arbitrary * vb;
        Vector3 vc2 = arbitrary * vc;
        AssertAlmostEqual(kInvRadius, MathUtils.MengerCurvature(va2, vb2, vc2));
      }
    }
  }

  // Tests collinear and coincident points.
  [TestCase(1.3f, 5.1f, 11.2f)]
  [TestCase(1.3f, 1.3f, 11.2f)]
  public void TestMengerCurvatureDegenerate(float t0, float t1, float t2) {
    Vector3 kA = new Vector3(-3, 5, 11).normalized * 7;
    Vector3 kB = new Vector3(2, 10, 3).normalized;
    Vector3 v0 = kA + kB * t0;
    Vector3 v1 = kA + kB * t1;
    Vector3 v2 = kA + kB * t2;
    Assert.AreEqual(0, MathUtils.MengerCurvature(v0, v1, v2));
  }

  [Test]
  public void TestMinLongInt() {
    Assert.AreEqual(-3, MathUtils.Min((long)-3, 2));
    Assert.AreEqual(-3, MathUtils.Min((long)2, -3));
    Assert.Catch<System.Exception>(() => MathUtils.Min((long)int.MinValue - 1, int.MinValue));
  }

  [Test]
  public void TestLinearResample() {
    // Doesn't really test the math, but tests edge cases and so on

    Assert.Throws<ArgumentException>(
        // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
        () => MathUtils.LinearResampleCurve(new float[0], 10).ToArray());
    Assert.Throws<ArgumentException>(
        // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
        () => MathUtils.LinearResampleCurve(null, 10).ToArray());

    float[] floats = new float[30];
    for (int i = 0; i < floats.Length; ++i) {
      floats[i] = Random.Range(-10f, 10f);
    }

    float[] floats10 = MathUtils.LinearResampleCurve(floats, 10).ToArray();
    Assert.AreEqual(floats[0], floats10[0]);
    Assert.AreEqual(floats[floats.Length-1], floats10[floats10.Length-1]);

    float[] floats1000 = MathUtils.LinearResampleCurve(floats, 1000).ToArray();
    Assert.AreEqual(floats[0], floats1000[0]);
    Assert.AreEqual(floats[floats.Length-1], floats1000[floats1000.Length-1]);
  }
}
}
