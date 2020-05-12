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

using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;

namespace TiltBrush {

internal class TestTrTransform : MathTestUtils {
  const int kNumIters = 100;

  [Test]
  public void TestFromMatrix4x4() {
    for (int i = 0; i < 100; ++i) {
      var t = Random.insideUnitSphere * 10;
      var r = Random.rotationUniform;
      var s = Random.Range(2f, 100f);
      if (Random.value < .5f) { s = 1 / s; }

      var xf = TrTransform.TRS(t, r, s);
      var m4 = Matrix4x4.TRS(t, r, Vector3.one * s);
      AssertAlmostEqual(TrTransform.FromMatrix4x4(m4), xf, allowFlip:true);
    }
  }

  [Test]
  public void TestToMatrix4x4() {
    for (int i = 0; i < 100; ++i) {
      var t = Random.insideUnitSphere * 10;
      var r = Random.rotationUniform;
      var s = Random.Range(2f, 100f);
      if (Random.value < .5f) { s = 1 / s; }

      var xf = TrTransform.TRS(t, r, s);
      var m4 = Matrix4x4.TRS(t, r, Vector3.one * s);
      AssertAlmostEqual(xf.ToMatrix4x4(), m4);
    }
  }

  [Test]
  public void TestTransformPointAndVec() {
    for (int i = 0; i < 100; ++i) {
      var xf = RandomTr((i % 2) == 0);
      var m4 = xf.ToMatrix4x4();
      var p = Random.onUnitSphere;
      AssertAlmostEqual(xf.MultiplyPoint(p), m4.MultiplyPoint(p));
      AssertAlmostEqual(xf.MultiplyVector(p), m4.MultiplyVector(p));
      AssertAlmostEqual(xf.MultiplyVector(p).magnitude, xf.scale);
    }
  }

  [Test]
  public void TestCompose() {
    for (int i = 0; i < 100; ++i) {
      var xfa = RandomTr((i % 2) == 0);
      var xfb = RandomTr((i % 2) == 0);
      var m4a = xfa.ToMatrix4x4();
      var m4b = xfb.ToMatrix4x4();
      var desired = m4a * m4b;
      AssertAlmostEqual((xfa * xfb).ToMatrix4x4(), desired);
      AssertAlmostEqual(xfa.ToMatrix4x4() * m4b, desired);
      AssertAlmostEqual(m4a * xfb.ToMatrix4x4(), desired);
      AssertAlmostEqual((xfa * xfb).scale, xfa.scale * xfb.scale);
    }
  }

  [Test]
  public void TestInverseMultipliesToIdentity() {
    for (int i = 0; i < 100; ++i) {
      var xf = RandomTr((i % 2) == 0);
      var xfi = xf.inverse;
      AssertAlmostEqual(xf * xfi, TrTransform.identity);
      AssertAlmostEqual(xfi * xf, TrTransform.identity);
    }
  }

  [Test]
  public void TestInvMul() {
    for (int i = 0; i < 100; ++i) {
      var xf = RandomTr((i % 2) == 0);
      AssertAlmostEqual(TrTransform.InvMul(xf, xf), TrTransform.identity);
    }
  }

  [Test]
  public void TestInverseAsMat4() {
    for (int i = 0; i < 100; ++i) {
      var xf = RandomTr((i % 2) == 0);
      var m4i = xf.ToMatrix4x4().inverse;
      var m4i2 = xf.inverse.ToMatrix4x4();
      AssertAlmostEqual(m4i, m4i2);
      AssertAlmostEqual(xf.ToMatrix4x4() * m4i, Matrix4x4.identity);
    }
  }

  [Test]
  public void TestEqualIsNotApproximate() {
    // Unity's Vector== is approximate. Test that we aren't inheriting that
    // bad behavior.
    TrTransform a = TrTransform.TRS(new Vector3(0,2,3), new Quaternion(0,0,0,1), 8);
    TrTransform b = a;
    b.translation.x += 1e-6f;
    b.rotation.x += 1e-5f;
    Assert.IsFalse(b.Equals(a));
    Assert.IsFalse(b == a);
    Assert.IsTrue(b != a);
  }

  [Test]
  public void TestEqualAndHash() {
    TrTransform a = TrTransform.TRS(new Vector3(0,2,3), new Quaternion(4,5,6,7), 8);
    TrTransform b = a;
    Assert.IsTrue(a.Equals(b));
    Assert.IsTrue(b.Equals(a));
    Assert.IsTrue(a == b);
    Assert.IsTrue(b == a);
    Assert.IsTrue(! (a != b));
    Assert.IsTrue(! (b != a));
    Assert.IsTrue(a.GetHashCode() == b.GetHashCode());
  }

  [Test]
  public void TestTranformBy() {
    for (int i = 0; i < 1000; ++i) {
      var xfa = RandomTr((i % 2) == 0);
      var xfb = RandomTr((i % 2) == 0);
      var product1 = xfa * xfb;
      var product2 = xfb.TransformBy(xfa) * xfa;
      AssertAlmostEqual(product1, product2);
    }
  }

  // Checks that the transformed plane satisfies this property:
  //   Mirror(Transform(xf, plane), Transform(xf, point)) ==
  //   Transform(xf, Mirror(plane, point))
  // IOW, when mirroring and transforming a point, it doesn't matter which
  // operation happens first.
  static void HelperCheckMirrorInvariant(Plane p, Vector3 v, TrTransform tr) {
    Vector3 mirrorOfTransform = (tr * p).ReflectPoint(tr * v);
    Vector3 transformOfMirror = tr * (p.ReflectPoint(v));
    AssertAlmostEqual(transformOfMirror, mirrorOfTransform);
  }

  // Checks that the transformed plane preserves orientation
  //   GetSide(Transform(xf, plane), Transform(xf, point)) == GetSide(plane, point)
  static void HelperCheckOrientationInvariant(Plane p, Vector3 v, TrTransform tr) {
    bool side0 = p.GetSide(v);
    bool side1 = (tr * p).GetSide(tr * v);
    Assert.AreEqual(side0, side1);
  }

  [Test]
  public void TestTransformRandomPlane() {
    for (int i = 0; i < 10; ++i) {
      TrTransform tr = MathTestUtils.RandomTr();
      Plane p = MathTestUtils.RandomPlane();
      Vector3 v = Random.onUnitSphere * Random.Range(.1f, 10);
      HelperCheckOrientationInvariant(p, v, tr);
      HelperCheckMirrorInvariant(p, v, tr);
    }
  }
}

}
