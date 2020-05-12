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

internal class TestColor : ColorTestUtils {
  // Test that HSLColor normalizes the incoming hue properly
  [TestCase(-HUE_MAX*2-1)]
  [TestCase(-HUE_MAX)]
  [TestCase(-1)]
  [TestCase(-0f)]
  [TestCase(0f)]
  [TestCase(HUE_MAX)]
  [TestCase(HUE_MAX*2+1)]
  public void HslModuloH(float H) {
    HSLColor hsl = new HSLColor(H, .5f, .5f);
    Assert.GreaterOrEqual(hsl.h, 0);
    Assert.Less(hsl.h, HUE_MAX);
  }

  // Test that the hue wheel isn't rotated
  // Also tests the "HueDegrees" property
  [TestCase(0,    1,0,0)]
  [TestCase(60,   1,1,0)]
  [TestCase(120,  0,1,0)]
  [TestCase(180,  0,1,1)]
  [TestCase(240,  0,0,1)]
  [TestCase(300,  1,0,1)]
  [TestCase(360,  1,0,0)]
  public void TestStandardHues(float degrees, float r, float g, float b) {
    HSLColor hsl = new HSLColor(0, 1, 0.5f, 1);
    hsl.HueDegrees = degrees;
    Color rgb = (Color)hsl;
    AssertNearlyEqualRGB(rgb, new Color(r, g, b, 1), 0);
  }

  // Test the "Hue01" property
  [TestCase( 0f,  1,0,0)]
  [TestCase(.5f,  0,1,1)]
  public void TestHue01(float h01, float r, float g, float b) {
    HSLColor hsl = new HSLColor(0, 1, 0.5f, 1);
    hsl.Hue01 = h01;
    Color rgb = (Color)hsl;
    AssertNearlyEqualRGB(rgb, new Color(r, g, b, 1), 0);
  }

  // Test that some RGB values are preserved exactly when
  // migrated through HSL
  // [TestCaseSource(typeof(ColorTestUtils), "RgbPrimariesAndSecondaries")]
  [Test]
  public void RgbHslRgbExact() {
    foreach (var color in RgbPrimariesAndSecondaries()) {
      HSLColor intermediate = (HSLColor)color;
      Color color2 = (Color)intermediate;
      AssertNearlyEqualRGB(color, color2, 0);
    }
  }

  // Test RGB -> HSL -> RGB gives the same value
  [Test]
  public void RgbHslRgb() {
    foreach (Color color in RgbTestCases(30)) {
      HSLColor intermediate = (HSLColor)color;
      Color color2 = (Color)intermediate;
      AssertNearlyEqualRGB(color, color2);
    }
  }

  // Test HSL -> RGB -> HSL gives the same value
  [Test]
  public void HslRgbHsl() {
    foreach (HSLColor color in HslTestCases(30)) {
      Color intermediate = (Color)color;
      HSLColor color2 = (HSLColor)intermediate;
      AssertNearlyEqualHSL(color, color2);
    }
  }

  // Cases that failed with earlier versions of AssertNearlyEqualHSL
  [TestCase(0.9980025f, 0.9140346f, 0.9997569f)]
  public void HslRgbHsl_Bad(float h, float s, float l) {
    HSLColor color = new HSLColor(h, s, l);
    HSLColor color2 = (HSLColor)(Color)color;
    AssertNearlyEqualHSL(color, color2);
  }

  private void Single_HslHsvHsl(HSLColor color) {
    float h, s, v;
    color.ToHSV(out h, out s, out v);
    var color2 = HSLColor.FromHSV(h, s, v);
    color2.a = color.a;

    float linv = 1 - color.l;
    if (color.l == 1 && color.s != 0) {
      // Conversion to HSV necessarily requires that HSV.s==1.
      // Saturation won't be able to round-trip exactly.
      AssertNearlyEqualHSL(color, color2);
    } else if (linv < 3e-4f) {
      // And when l is nearly 1, the saturation calculation starts
      // getting numerically unstable.
      AssertNearlyEqualHSL(color, color2);
    } else {
      AssertNearlyEqualHSLStrict(color, color2);
    }
  }

  // Cases that failed with earlier versions of Single_HslHsvHsl
  [TestCase(0.8309081f, 0.6814814f, 0.9998399f)]
  [TestCase(0.2468981f, 0.9439237f, 0.9997582f)]
  public void HslHsvHsl_Bad(float h, float s, float l) {
    Single_HslHsvHsl(new HSLColor(h,s,l, 1));
  }

  [Test]
  public void HslHsvHsl() {
    foreach (HSLColor hsl in HslTestCases(30)) {
      Single_HslHsvHsl(hsl);
    }
  }
}

internal class TestColorPicker : ColorTestUtils {

  private void Single_ColorPickerRoundTripFromColor(ColorPickerMode mode, Color rgb) {
    Vector3 raw = ColorPickerUtils.ColorToRawValue(mode, rgb);
    Color rgb2;
    bool ok = ColorPickerUtils.RawValueToColor(mode, raw, out rgb2);
    Assert.IsTrue(ok, "Mode {0}: {1} -> {2} -> fail", mode, Repr(rgb), raw);
    // RawValueToColor should always return colors with a=1
    Assert.AreEqual(1, rgb2.a, "RawValueToColor alpha");
    rgb.a = 1;  // Don't check incoming alpha
    AssertNearlyEqualRGB(rgb, rgb2);
  }

  [Test]
  public void TestColorPickerRoundTripFromColor(
      [Values(ColorPickerMode.HS_L_Polar,
              ColorPickerMode.SV_H_Rect,
              ColorPickerMode.SL_H_Triangle,
              ColorPickerMode.HL_S_Polar)]
      ColorPickerMode mode) {
    foreach (HSLColor hsl in HslTestCases(30)) {
      Single_ColorPickerRoundTripFromColor(mode, (Color)hsl);
    }
  }

  private void Single_ColorPickerRoundTripFromRawValue(ColorPickerMode mode, Vector3 raw) {
    Color rgb;
    bool ok = ColorPickerUtils.RawValueToColor(mode, raw, out rgb);
    if (!ok) { return; }
    Vector3 raw2 = ColorPickerUtils.ColorToRawValue(mode, rgb);
    Vector3 diff = raw2-raw;
    float EPSILON = 1e-4f;
    if (Mathf.Abs(diff.x) > EPSILON ||
        Mathf.Abs(diff.y) > EPSILON ||
        Mathf.Abs(diff.z) > EPSILON) {
      Assert.Fail("Mode {0} sent {1} -> {2}", mode, raw, raw2);
    }
  }

  // A bunch of sample positions in the unit cube.
  public static IEnumerable<Vector3> ColorPickerPositions() {
    for (int iz=0; iz<=8; ++iz) {
      float z = iz/10f;

      for (int iradius = 1; iradius <= 8; ++iradius)
      for (int degrees = 0; degrees < 360; degrees += 30) {
        float radius = iradius / 8f - 1e-5f;
        float x = .5f + .5f * radius * Mathf.Cos(degrees * Mathf.Deg2Rad);
        float y = .5f + .5f * radius * Mathf.Sin(degrees * Mathf.Deg2Rad);
        yield return new Vector3(x, y, z);
      }

      for (int ix=0; ix<=8; ++ix)
      for (int iy=0; iy<=8; ++iy) {
        yield return new Vector3(ix/8f, iy/8f, z);
      }
    }
  }

  // This tests that a color can go out of the UI, into Tilt Brush,
  // and back into the UI, without affecting the UI. It's a mildly unreasonable request,
  // since the UI sometimes has an entire circle that represents the color "black".
  // It used to work because ColorTo/FromRawValue passed/returned HSLColor, but that
  // changed when we started supporting HDR color picking
  [Ignore("HDR Color picking")]
  [Test]
  public void TestColorPickerRoundTripFromValue(
      [Values(ColorPickerMode.HS_L_Polar,
              ColorPickerMode.SV_H_Rect,
              ColorPickerMode.SL_H_Triangle,
              ColorPickerMode.HL_S_Polar)]
      ColorPickerMode mode) {
    foreach (var pos in ColorPickerPositions()) {
      Single_ColorPickerRoundTripFromRawValue(mode, pos);
    }
  }

  [TestCase(  0,   0)]
  [TestCase( 60,  60)]
  [TestCase(120, 120)]
  [TestCase(180, 180)]
  [TestCase(240, 240)]
  public void TestPicker_HS_L_Hue(float angle, float hueDegrees) {
    float radius = .5f; // - 1e-5f;
    Vector3 v = new Vector3(
        .5f + radius * Mathf.Cos(angle * Mathf.Deg2Rad),
        .5f + radius * Mathf.Sin(angle * Mathf.Deg2Rad),
        0.5f);
    Color rgb;
    bool ok = ColorPickerUtils.RawValueToColor(ColorPickerMode.HS_L_Polar, v, out rgb);
    Assert.IsTrue(ok, "{0}", v);
    AssertEqualPeriodic(hueDegrees, ((HSLColor)rgb).HueDegrees, 360, 1e-3f);
  }

  [TestCase(0,       0)]
  [TestCase(1f/3f, 120)]
  [TestCase(2f/3f, 240)]
  public void TestPicker_SV_H_Hue(float z, float hueDegrees) {
    Vector3 v = new Vector3(1, 1, z);
    Color rgb;
    bool ok = ColorPickerUtils.RawValueToColor(ColorPickerMode.SV_H_Rect, v, out rgb);
    Assert.IsTrue(ok, "{0}", v);
    AssertEqualPeriodic(hueDegrees, ((HSLColor)rgb).HueDegrees, 360, 1e-3f);
  }

  [Test]
  public void TestPicker_SL_H() {
    Color rgb = Color.green;
    float expected = ((HSLColor)rgb).Hue01;
    Vector3 raw = ColorPickerUtils.ColorToRawValue(ColorPickerMode.SL_H_Triangle, rgb);
    Assert.AreEqual(expected, raw.z, 1e-4f);
  }

  [Test]
  public void TestPicker_HL_S() {
    var mode = ColorPickerMode.HL_S_Polar;
    Vector3 raw = new Vector3(0.75f, 0.5f, 1);
    Color rgb;
    bool ok = ColorPickerUtils.RawValueToColor(mode, raw, out rgb);
    Assert.IsTrue(ok);
    AssertNearlyEqualRGB((Color)new HSLColor(0, 0, 0.5f, 1), rgb);
  }

}

}
