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
internal class TestQuaternionExtensions : MathTestUtils {
  [TestCase(1,0,0, 0)]
  [TestCase(1,0,0, 180)]
  [TestCase(0,1,0, 180)]
  [TestCase(1,1,2, 5)]
  [TestCase(1,1,2, 185)]
  [TestCase(1,1,2, 355)]
  public void TestLogExp_FromAxisAngle(float ax, float ay, float az, float degrees) {
    Vector3 axis = new Vector3(ax, ay, az).normalized;
    float radians = degrees * Mathf.Deg2Rad;
    Quaternion q = Quaternion.AngleAxis(degrees, axis);
    Quaternion logq = q.Log();

    Assert.AreEqual(logq.Re(), 0);
    AssertAlmostEqual(logq.Im(), axis * (radians/2));

    Quaternion explogq = q.Log().Exp();
    AssertAlmostEqual(q, explogq);
  }

  [TestCase(-6.868475E-05f, 1.011354E-05f, -3.229485E-05f,  1+1e-6f)]
  public void TestLog_WGreaterThanOne(float qx, float qy, float qz, float qw) {
    Quaternion q = new Quaternion(qx, qy, qz,  qw);
    Quaternion lq = q.Log();
    Assert.IsFalse(float.IsNaN(Quaternion.Dot(lq, lq)));
  }

  [Test]
  public void TestLogExpIdentity() {
    for (int i = 0; i < 300; ++i) {
      Quaternion q = Random.rotationUniform;
      AssertAlmostEqual(q, q.Log().Exp());
    }
  }

  [Test]
  public void TestExpLogIdentity() {
    for (int i = 0; i < 300; ++i) {
      // Log values are periodic, so restrict angle to [-180, 180].
      // Without loss of generality, we can further restrict angle to [0, 180],
      // since the angle is just used as a length.
      // The identity does not apply at theta = 180 because Log() is a many-valued function there.
      // 180 +/- .1501 is the danger zone
      float degrees = Random.Range(1, 180-0.25f);
      Vector3 im = Random.onUnitSphere * degrees * Mathf.Deg2Rad;
      Quaternion q = new Quaternion(im.x, im.y, im.z, 0);
      AssertAlmostEqual(q, q.Exp().Log());
    }
  }

  [Test]
  public void TestExpLogIdentityNear0() {
    for (int i = 0; i < 300; ++i) {
      float degrees = Random.Range(0, 1);
      Vector3 im = Random.onUnitSphere * degrees * Mathf.Deg2Rad;
      Quaternion q = new Quaternion(im.x, im.y, im.z, 0);
      AssertAlmostEqual(q, q.Exp().Log());
    }
  }

  [Test]
  public void TestLogInvalidArgument() {
    Assert.That(() => new Quaternion(1, 2, 3, 4).Log(), Throws.ArgumentException);
  }

  [Test]
  public void TestExpInvalidArgument() {
    Assert.That(() => new Quaternion(0, 0, 0, 1).Exp(), Throws.ArgumentException);
  }

  [Test]
  public void TestQuaternionTrueEquals() {
    Quaternion qZero = new Quaternion(0,0,0,0);
    Quaternion qOne = Quaternion.identity;
    Quaternion qNan = qZero;
    qNan.x = qZero.x / qZero.x;

    Assert.IsTrue (qZero.TrueEquals   (qZero));
    Assert.IsTrue (qOne .TrueEquals   (qOne) );
    Assert.IsFalse(qZero.TrueEquals   (qNan) );
    Assert.IsFalse(qNan .TrueEquals   (qZero));
    Assert.IsTrue (qZero.TrueNotEquals(qNan) );
    Assert.IsTrue (qNan .TrueNotEquals(qZero));
  }
}  // Test
}  // TiltBrush
