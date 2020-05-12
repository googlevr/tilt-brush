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

using HSLColor = TiltBrush.HSLColor;

class ColorTestUtils {
  public const float HUE_MAX = HSLColor.HUE_MAX;

  //
  // Utility methods
  //

  protected static string Repr(HSLColor hsl) {
    return string.Format("<HSLA {0} {1} {2} {3}>", hsl.h, hsl.s, hsl.l, hsl.a);
  }

  protected static string Repr(Color rgb) {
    return string.Format("<RGBA {0} {1} {2} {3}>", rgb.r, rgb.g, rgb.b, rgb.a);
  }

  // Almost equal using absolute distance
  static bool AlmostEqual(float lhs, float rhs, float eps) {
    return Mathf.Abs(lhs-rhs) <= eps;
  }

  // Returns difference between two periodic values.
  // Result range is [-period/2, period/2]
  public static float PeriodicDifference(
      float lhs, float rhs, float period) {
    // % has range (-period, period)
    float delta = (lhs - rhs) % period;
    // Convert to [0, period)
    if (delta < 0) { delta += period; }
    // Convert to [-period/2, period/2)
    if (delta >= period/2) { delta -= period; }
    return delta;
  }

  public static void AssertEqualPeriodic(float lhs, float rhs, float period, float eps) {
    float delta = PeriodicDifference(lhs, rhs, period);
    if (Mathf.Abs(delta) > eps) {
      Assert.Fail("Expected: {0}+n*{1} +/- {2}\n  But was: {3} (delta {4})",
                  lhs, period, eps, rhs, delta);
    }
  }

  static bool HueAlmostEqual(float lhs, float rhs, float eps) {
    // Assume lhs and rhs are in [0,6); compare values near 0 and 6 properly
    float delta = rhs-lhs;
    if (delta > HUE_MAX/2) { delta -= HUE_MAX; }
    else if (delta < -HUE_MAX/2) { delta += HUE_MAX; }
    return Mathf.Abs(delta) <= eps;
  }

  /// Assert that the two colors represent the same-looking color
  public static void AssertNearlyEqualRGB(Color lhs, Color rhs, float eps=1e-4f) {
    if (! AlmostEqual(lhs.r, rhs.r, eps) ||
        ! AlmostEqual(lhs.g, rhs.g, eps) ||
        ! AlmostEqual(lhs.b, rhs.b, eps) ||
        ! AlmostEqual(lhs.a, rhs.a, eps)) {
      Assert.Fail("{0} !~ {1}", lhs, rhs);
    }
  }

  // Convert from polar to rectilinear coordinates for ease of comparison
  static Vector3 HslAsRectilinear(HSLColor hsl) {
    float radius = .5f - Mathf.Abs(hsl.l - .5f);
    radius = radius * hsl.s;
    float angle = hsl.HueDegrees * Mathf.Deg2Rad;
    return new Vector3(radius * Mathf.Cos(angle),
                       radius * Mathf.Sin(angle),
                       hsl.l);
  }

  /// Assert that the two HSLs represent the same-looking color
  /// (as opposed to having exactly the same values)
  public static void AssertNearlyEqualHSL(HSLColor lhs, HSLColor rhs) {
    float eps = 1e-6f;
    float dist = (HslAsRectilinear(lhs) - HslAsRectilinear(rhs)).magnitude;
    if (!AlmostEqual(dist, 0, eps) ||
        !AlmostEqual(lhs.a, rhs.a, eps)) {
      Assert.Fail("{0} !~ {1}", Repr(lhs), Repr(rhs));
    }
  }

  /// Assert that the two colors have the same field values
  /// and that orthogonal dimensions are preserved for some L and S edge cases.
  public static void AssertNearlyEqualHSLStrict(HSLColor lhs, HSLColor rhs) {
    float eps = 1e-4f;
    if (! HueAlmostEqual(lhs.h, rhs.h, eps) ||
        ! AlmostEqual(lhs.s, rhs.s, eps) ||
        ! AlmostEqual(lhs.l, rhs.l, eps) ||
        ! AlmostEqual(lhs.a, rhs.a, eps)) {
      Assert.Fail("{0} !~ {1}", Repr(lhs), Repr(rhs));
    }
  }

  public static IEnumerable<Color> RgbPrimariesAndSecondaries() {
    for (float r=0; r<=1; r+= 1)
    for (float g=0; g<=1; g+= 1)
    for (float b=0; b<=1; b+= 1) {
      yield return new Color(r,g,b);
    }
  }

  public static IEnumerable<Color> RgbTestCases(int n) {
    for (float r=0; r<=1; r+=0.5f)
    for (float g=0; g<=1; g+=0.5f)
    for (float b=0; b<=1; b+=0.5f) {
      yield return new Color(r,g,b);
    }
    for (int i=0; i<n; ++i)
      yield return new Color(Random.value, Random.value, Random.value, Random.value);
  }

  public static IEnumerable<HSLColor> HslTestCases(int n) {
    // 12 H steps covers the 3 primaries, the 3 secondaries,
    // and the half-steps between them
    for (int ih=0; ih<=12; ++ih)
    for (float s=0; s<=1; s+=0.5f)
    for (float l=0; l<=1; l+=0.5f) {
      float h = (HUE_MAX * ih) / 12;
      yield return new HSLColor(h,s,l);
    }
    for (int i=0; i<n; ++i)
      yield return new HSLColor(Random.value, Random.value, Random.value, Random.value);
  }
}
