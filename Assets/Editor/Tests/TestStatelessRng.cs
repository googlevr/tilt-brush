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
using Random = UnityEngine.Random;
using S = TiltBrush.Test.Stats;

namespace TiltBrush {
internal class TestStatelessRng {
  const float kTwoPi = 6.283185307179586f;
  const int kIters = 10000;
  const int kSeed = 0xD00B015;

  // Rescales angle in radians to angle in [0, 1)
  static float ToAngle01(float angleRad) {
    float normalized = (angleRad / kTwoPi) % 1;
    if (normalized < 0) { normalized += 1; }
    return normalized;
  }

  static void WriteFile(string name, IEnumerable<float> vals) {
    using (var writer = new System.IO.StreamWriter(@"C:\src\tb\logs\" + name)) {
      foreach (var val in vals) {
        writer.WriteLine("{0}", val);
      }
    }
  }

  static IEnumerable<T> Gen<T>(Func<int, T?> fn, int nIters=kIters) where T : struct {
    return Enumerable.Range(0, nIters)
        .Select(i => fn(i))
        .Where(v => v != null)
        .Select(v => v.Value);
  }

  static IEnumerable<T> Gen<T>(Func<int, T> fn, int nIters=kIters) where T : struct {
    return Enumerable.Range(0, nIters)
        .Select(i => fn(i));
  }

  [Test]
  public void TestUint32ToFloat() {
    Assert.AreNotEqual(1f, StatelessRng.kLargestFloatLessThanOne);
    Assert.AreNotEqual(1f, StatelessRng.UInt32ToFloat01(0xffffffffu));
    Assert.AreEqual(StatelessRng.kLargestFloatLessThanOne,
                    StatelessRng.UInt32ToFloat01(0xffffffffu));
  }

  // A quick test that CheckUniformity actually detects non-uniformity.
  // In this case, it's because the parametrization is buggy, not because
  // the rng result is nonuniform.
  [Test]
  public void TestCheckUniformity() {
    var rng = new StatelessRng(kSeed);
    Func<Vector2, Vector2?> Parametrize = v => new Vector2((v.x + 1) / 2, (v.y + 1) / 2);
    Assert.Throws<AssertionException>(() =>
        S.CheckUniformity(new[] { "x", "y" },
                          Gen(i => Parametrize(rng.InUnitCircle(i)))));
  }

  [Test]
  public void TestCorrelation_In01() {
    // Checks correlation between successive values of the generator.
    // This is kind of a wonky test, but it's what detected our terrible RNG in the first place.
    var rng = new StatelessRng(kSeed);
    var rStateless = S.GetCorrelation(Gen(i => new Vector2(rng.In01(2*i), rng.In01(2*i + 1))));
    Assert.Less(Mathf.Abs(rStateless), .01f);
  }

  [Test]
  public void TestUniformity_In01() {
    var rng = new StatelessRng(kSeed);
    S.CheckUniformity("u", Gen(i => rng.In01(i)));
  }

  [Test]
  public void TestUniformity_InRange() {
    var rng = new StatelessRng(kSeed);
    S.CheckUniformity("u", Gen(i => (rng.InRange(i, -10, 20) + 10f) / 30f));
  }

  [Test]
  public void TestUniformity_InIntRangeSmall() {
    var rng = new StatelessRng(kSeed);
    int kMin = -10, kMax = 40;
    S.CheckUniformity("u", Gen(i => (rng.InIntRange(i, kMin, kMax) - kMin + .5f) / (kMax - kMin)));
  }

  [Test]
  public void TestUniformity_OnUnitCircle() {
    var rng = new StatelessRng(kSeed);

    Func<int, float?> distribution = i => {
      Vector2 v = rng.OnUnitCircle(i);
      return ToAngle01(Mathf.Atan2(v.y, v.x));
    };
    S.CheckUniformity("theta", Gen(distribution));
  }

  [Test]
  public void TestUniformity_InUnitCircle() {
    var rng = new StatelessRng(kSeed);

    // Parametrization based on polar coords
    Func<Vector2, Vector2?> Parametrize = cartesian => {
      float radius = cartesian.magnitude;
      if (radius == 0) { return null; }
      // cdf = cumulative distribution function ~= "area under"
      // area of cirle of radius "mag" relative to area of entire circle
      // to make uniform, compute cdf(var) / cdf (total) = mag^2 / 1^2
      float radiusUniform = radius * radius;

      // area increases linearly with angle01, so just need to rescale to [0,1)
      float angle = Mathf.Atan2(cartesian.y, cartesian.x);
      float angleUniform = ToAngle01(angle);
      return new Vector2(angleUniform, radiusUniform);
    };

    S.CheckUniformity(new[] { "theta", "rr" },
        Gen(i => Parametrize(rng.InUnitCircle(i))));
  }

  // v3 is a point on the unit sphere.
  // Returns a uniform parametrization of the surface area.
  Vector2 ParametrizeSphereSurfaceThetaZ(Vector3 v3) {
    // Treat it as a cylinder, taking advantage of Archimedes
    // https://en.wikipedia.org/wiki/On_the_Sphere_and_Cylinder
    // http://mathcentral.uregina.ca/qq/database/QQ.09.99/wilkie1.html
    float param0 = ToAngle01(Mathf.Atan2(v3.x, v3.y));  // theta
    float param1 = (v3.z + 1) / 2;  // z
    return new Vector2(param0, param1);
  }

  [Test]
  public void TestUniformity_OnUnitSphere() {
    var rng = new StatelessRng(kSeed);
    Vector3[] vals = Gen(i => rng.OnUnitSphere(i)).ToArray();

    S.CheckUniformity(new[] { "theta", "z" }, vals.Select(ParametrizeSphereSurfaceThetaZ));
    // Try along another axis
    S.CheckUniformity(
        new[] { "theta", "y" },
        vals.Select(v => new Vector3(v.x, v.z, v.y))
            .Select(ParametrizeSphereSurfaceThetaZ));
  }

  [Test]
  public void TestUniformity_InUnitSphere() {
    var rng = new StatelessRng(kSeed);
    Vector3[] vals = Gen(i => rng.InUnitSphere(i)).ToArray();

    Func<Vector3, Vector3> ThetaRrrZr = v3 => {
      float r = v3.magnitude;
      if (r == 0) { return Vector3.zero; }
      float angle01 = ToAngle01(Mathf.Atan2(v3.y, v3.x));
      return new Vector3(angle01, r*r*r, ((v3.z / r) + 1f) / 2f);
    };
    S.CheckUniformity(new[] { "theta", "rrr", "z/r" }, vals.Select(ThetaRrrZr));
  }

  [Test]
  public void TestUniformity_Rotation() {
    var rng = new StatelessRng(kSeed);
    Quaternion[] vals = Gen(i => rng.Rotation(i)).ToArray();

    Func<Quaternion, Vector2> ParametrizeAxisAndAngle = q => {
      Vector3 axis;             // a unit vector
      float angleRad;
      q.ToAngleAxis(out angleRad, out axis);
      Vector2 param01 = ParametrizeSphereSurfaceThetaZ(axis);
      float param2 = ToAngle01(angleRad);
      return new Vector3(param01.x, param01.y, param2);
    };
    S.CheckUniformity(
        new[] { "axis theta", "axis z", "angle" },
        vals.Select(ParametrizeAxisAndAngle));
  }

#if false
  [Test]
  public void TestForSaltReuse() {
    var seen = new HashSet<int>();
    var rng = new StatelessRng(kSeed);
    StatelessRng.SaltUsed = (salt) => {
      Assert.IsTrue(seen.Add(salt), "Salt {0} is unused", salt);
    };
    try {
      rng.OnUnitCircle(0); seen.Clear();
      rng.InUnitCircle(0); seen.Clear();
      rng.OnUnitSphere(0); seen.Clear();
      rng.InUnitSphere(0); seen.Clear();
      rng.Rotation(0); seen.Clear();
    } finally {
      StatelessRng.SaltUsed = null;
    }
  }
#endif
}  // Test
}  // TiltBrush
