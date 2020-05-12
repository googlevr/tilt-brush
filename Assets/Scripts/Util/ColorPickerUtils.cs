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
using System;

namespace TiltBrush {

// To add another color picker:
// + Add a "XY_S_SomeDescription" value to ColorPickerMode
// + Create Shaders/ColorPicker_xy_s.shader
// + Create Textures/Icons/colortoggle_xy_s.png
// + Find all the switch (mode) {} statements in ColorPickerUtils, add a case.
//   Most important are: ColorToRawValue(), RawValueToColor(),
//   ApplySliderConstraint(), ApplyPlanarConstraint()
// - Add some tests for ColorToRawValue() etc
// - Modify ColorPickerSlider.OnModeChanged, OnColorChanged
// - In the inspector:
//   - Fill out a new entry in App's CustomColorPaletteStorage.ModeToPickerInfo
//   - ALSO fill out a new entry in prefab ColorPickerPanel.ColorSpaceToggle,
//     because that array should be (but is not) properly initialized when the
//     prefab is constructred.

// Modes are named XY_S (rectilinear) or TR_S (polar)
// - XY are the X and Y axes, if rectilinear
// - TR are theta and radius, if polar
// - S is the slider
public enum ColorPickerMode {
  // These values are serialized into m_ModeToPickerInfo, so take care
  // if you shuffle them around.
  HS_L_Polar,    // Theta: hue   Radius: saturation   Slider: lightness
  SV_H_Rect,     // X: saturation   Y: value   Slider:  hue
  SL_H_Triangle, // X: chroma, Y: lightness, Slider: hue
  HL_S_Polar,    // Theta: hue, R: lightness, Slider: saturation
  HS_LogV_Polar, // Theta: hue, R: saturation, Slider: Log-scale value
  NUM_MODES
}

[Serializable]
public class ColorPickerInfo {
  public Shader shader;
  public Texture icon;
  public bool cylindrical;
  public bool experimental;  // if true, don't show in demo mode
  public bool hdr;
}

[Serializable]
public struct ModeAndPickerInfo {
  public ColorPickerMode mode;
  public ColorPickerInfo info;
}

public static class ColorPickerUtils {
  const float SQRT3 = 1.7320508f;

  // These should match the constants in ColorPicker_hs_logv.shader
  const float kLogVMin = -3f;
  static float sm_LogVMax;

  const float kMainLightMax = 8;
  const float kSecondaryLightMax = 1;

  private static ModeAndPickerInfo[] ModeToPickerInfo {
    get { return CustomColorPaletteStorage.m_Instance.ModeToPickerInfo; }
  }

  // Parameter 'hdr' refers to the caller being an hdr ColorPicker.  Or, more specifically,
  // if the ColorController that the ColorPicker is displaying has hdr properties.
  public static ColorPickerMode GetActiveMode(bool hdr) {
    return hdr ? ColorPickerUtils.HdrMode : CustomColorPaletteStorage.m_Instance.Mode;
  }

  public static ColorPickerMode HdrMode {
    get { return ColorPickerMode.HS_LogV_Polar; }
  }

  public static int NumModes(bool hdr) {
    int n = 0;
    for (int i = 0; i < ModeToPickerInfo.Length; i++) {
      if (!ModeToPickerInfo[i].info.experimental &&
          ModeToPickerInfo[i].info.hdr == hdr) {
        n++;
      }
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
      if (Config.IsExperimental && ModeToPickerInfo[i].info.experimental &&
          ModeToPickerInfo[i].info.hdr == hdr) {
        n++;
      }
#endif
    }
    return n;
  }

  static float MinHDRValue { get { return Mathf.Pow(2, kLogVMin); } }

  public static ColorPickerInfo GetInfoForMode(ColorPickerMode mode) {
    // N is small, so I don't feel bad about this
    for (int i = 0; i < ModeToPickerInfo.Length; ++i) {
      if (ModeToPickerInfo[i].mode == mode) {
        return ModeToPickerInfo[i].info;
      }
    }
    return null;
  }

  // Returns xMax such that 2^xMax - 2^xMin is exactly "height".
  // IOW, returns a range of the curve 2^x that starts at 2^xMin and whose
  // height is exactly "height".
  private static float GetHdrCurveConstant(float logMin, float height) {
    return Mathf.Log(height + Mathf.Pow(2, kLogVMin), 2);
  }

  public static void SetLogVRangeForMode(LightMode mode) {
    switch (mode) {
    case LightMode.Shadow:
      sm_LogVMax = GetHdrCurveConstant(kLogVMin, kMainLightMax);
      break;
    case LightMode.NoShadow:
      sm_LogVMax = GetHdrCurveConstant(kLogVMin, kSecondaryLightMax);
      break;
    }
    Shader.SetGlobalFloat("_LogVMax", sm_LogVMax);
    Shader.SetGlobalFloat("_LogVMin", kLogVMin);
  }

  /// Can never fail (unlike RawValueToColor)
  ///
  /// Note: Behavior is currently undefined if an hdr color is passed in,
  /// but the color picker cannot handle hdr.
  public static Vector3 ColorToRawValue(ColorPickerMode mode, Color rgb) {
    bool colorIsHdr = (rgb.r > 1 || rgb.g > 1 || rgb.b > 1);
    // If we do this a lot, maybe add this to ColorPickerInfo.hdr?
    // Or better, refactor this whole mess of code into small mode-specific classes.
    bool pickerSupportsHdr = (mode == ColorPickerMode.HS_LogV_Polar);
    if (colorIsHdr && !pickerSupportsHdr) {
      // Shouldn't happen except in experimental
      Debug.LogErrorFormat("Truncating HDR color to LDR");
      float h, s, v;
      Color.RGBToHSV(rgb, out h, out s, out v);
      rgb = Color.HSVToRGB(h, s, v, hdr:false);
    }

    switch (mode) {
    case ColorPickerMode.SV_H_Rect: {
        float h, s, v;
        Color.RGBToHSV(rgb, out h, out s, out v);
        return new Vector3(s, v, h);
      }
    case ColorPickerMode.HS_L_Polar: {
        // H is angle, S is radius, L is depth
        HSLColor color = (HSLColor) rgb;
        var angle = color.HueDegrees * Mathf.Deg2Rad;
        var vector = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * color.s / 2 +
          new Vector2(0.5f, 0.5f);
        return new Vector3(vector.x, vector.y, color.l);
      }
    case ColorPickerMode.HL_S_Polar: {
        // H is angle, (1-L) is radius, (1-S) is depth
        HSLColor color = (HSLColor) rgb;
        var angle = color.HueDegrees * Mathf.Deg2Rad;
        float radius = 1 - color.l;
        var vector = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius / 2 +
          new Vector2(0.5f, 0.5f);
        return new Vector3(vector.x, vector.y, 1 - color.s);
      }
    case ColorPickerMode.SL_H_Triangle: {
        HSLColor color = (HSLColor) rgb;
        Vector3 ret = new Vector3();
        ret.y = color.l;
        ret.z = color.Hue01;
        float maxChroma = SQRT3 * ((color.l < .5f) ? color.l : (1 - color.l));
        ret.x = maxChroma * color.s;
        return ret;
      }
    case ColorPickerMode.HS_LogV_Polar: {
      // H is angle, S is radius, log(V) is depth

      // This only needs to be > 0 and < the minimum ColorPickerMode.HS_LogV_Polar
      float kMinValue = 1e-7f;

      float h, s, v;
      Color.RGBToHSV(rgb, out h, out s, out v);
      Vector2 cartesian; {
        float angle = h * (Mathf.PI * 2);
        cartesian = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * s;
        // convert from [-1, 1] to [0, 1]
        cartesian = cartesian / 2 + new Vector2(.5f, .5f);
      }

      // Log-remap [2^-n, 2^n] to [-n, n]
      v += MinHDRValue;
      float slider = Mathf.Log(Mathf.Max(v, kMinValue), 2);
      slider = Mathf.Clamp(slider, kLogVMin, sm_LogVMax);
      // remap from [min, max] to [0, 1]
      slider = (slider - kLogVMin) / (sm_LogVMax - kLogVMin);
      return new Vector3(cartesian.x, cartesian.y, slider);
    }
    default:
      return new Vector3(1, 1, 1);
    }
  }

  /// The inverse of ColorToRawValue.
  /// Returns true on success, false on failure.
  /// (e.g. not out of circle bounds on HSL picker)
  /// Prefer this over using RawValueToHSLColor, as this will work properly with HDR
  public static bool RawValueToColor(ColorPickerMode mode, Vector3 raw, out Color color) {
    switch (mode) {
    case ColorPickerMode.SV_H_Rect:
    case ColorPickerMode.HS_L_Polar:
    case ColorPickerMode.SL_H_Triangle:
    case ColorPickerMode.HL_S_Polar: {
      HSLColor hsl;
      bool ret = RawValueToHSLColor(mode, raw, out hsl);
      color = (Color)hsl;
      return ret;
    }

    case ColorPickerMode.HS_LogV_Polar: {
      float hue01, saturation, value;
      bool ok = RawValueToHSV(mode, raw, out hue01, out saturation, out value);
      color = Color.HSVToRGB(hue01, saturation, value, hdr:true);
      return ok;
    }

    default:
      Debug.Assert(false);
      color = Color.black;
      return false;
    }
  }

  /// Returns true on success, false on failure.
  /// Failure cases are guaranteed to be identical to the failure cases of RawValueToColor
  /// Only implemented for the subset of pickers that are hsv "native"
  static bool RawValueToHSV(
      ColorPickerMode mode, Vector3 raw,
      out float hue01, out float saturation, out float value) {
    switch (mode) {
    case ColorPickerMode.HS_LogV_Polar: {
      // H is angle, S is radius, log(V) is depth
      var position = new Vector2(raw.x - 0.5f, raw.y - 0.5f) * 2;
      var radius = position.magnitude;
      if (radius > 1) {
        hue01 = saturation = value = 0;
        return false;
      }

      // x direction is 0 degrees (red)
      hue01 = (Mathf.Atan2(position.y, position.x) / (Mathf.PI * 2)) % 1;
      if (hue01 < 0) { hue01 += 1; }

      saturation = radius;

      // remap [0, 1] to [kLogVMin, kLogVMax]
      float logValue = kLogVMin + raw.z * (sm_LogVMax - kLogVMin);
      value = Mathf.Pow(2, logValue) - MinHDRValue;
      return true;
    }

    default:
      Debug.Assert(false);
      hue01 = saturation = value = 0;
      return false;
    }
  }

  /// Returns true on success, false on failure.
  /// Failure cases are guaranteed to be identical to the failure cases of RawValueToColor
  /// Use if converting raw -> Color is lossy; otherwise, use RawValueToColor
  /// HDR color pickers might disallow conversion to HSL.
  static bool RawValueToHSLColor(ColorPickerMode mode, Vector3 raw, out HSLColor color) {
    const float EPSILON = 1e-5f;
    switch (mode) {
    case ColorPickerMode.SV_H_Rect:
      color = HSLColor.FromHSV(raw.z * HSLColor.HUE_MAX, raw.x, raw.y);
      return true;

    case ColorPickerMode.HS_L_Polar: {
        var position = new Vector2(raw.x - 0.5f, raw.y - 0.5f) * 2;
        var radius = position.magnitude;
        color = new HSLColor();
        if (radius > 1) {
          if (radius > 1 + EPSILON) {
            return false;
          } else {
            radius = 1;
          }
        }
        // x direction is 0 degrees (red)
        color.HueDegrees = Mathf.Atan2(position.y, position.x) * Mathf.Rad2Deg;
        color.s = radius;
        color.l = raw.z;
        color.a = 1;
        return true;
      }

    case ColorPickerMode.SL_H_Triangle: {
        color.h = 0; // assigned later
        color.l = raw.y;
        float maxChroma = SQRT3 * ((color.l < .5f) ? color.l : (1 - color.l));
        color.s = (maxChroma == 0) ? 0 : raw.x / maxChroma;
        color.a = 1;
        color.Hue01 = raw.z;
        return (0 <= raw.x && raw.x <= maxChroma);
      }

    case ColorPickerMode.HL_S_Polar: {
        var position = new Vector2(raw.x - 0.5f, raw.y - 0.5f) * 2;
        var radius = position.magnitude;
        color = new HSLColor();
        if (radius > 1) {
          if (radius > 1 + EPSILON) {
            return false;
          } else {
            radius = 1;
          }
        }
        color.HueDegrees = Mathf.Atan2(position.y, position.x) * Mathf.Rad2Deg;
        color.l = 1 - radius;
        color.s = 1 - raw.z;
        color.a = 1;
        return true;
      }

    case ColorPickerMode.HS_LogV_Polar:
      throw new InvalidOperationException("This is a HDR mode");

    default:
      Debug.Assert(false);
      color = new HSLColor();
      return false;
    }
  }

  // Apply luminance and/or saturation constraints to the slider (ie, RawValue.z)
  public static float ApplySliderConstraint(
      ColorPickerMode mode, float value_z,
      float luminanceMin, float saturationMax) {
    switch (mode) {
    case ColorPickerMode.SL_H_Triangle:
    case ColorPickerMode.SV_H_Rect:
      // Slider should be the half-open range [0, 1) to avoid double-covering red
      // (which causes artifacts when switching)
      value_z = Mathf.Clamp(value_z, 0, 1 - 1e-4f);
      break;
    case ColorPickerMode.HS_L_Polar:
      value_z = Mathf.Max(value_z, luminanceMin);
      break;
    case ColorPickerMode.HL_S_Polar:
      value_z = Mathf.Max(value_z, 1 - saturationMax);
      break;
    }
    return value_z;
  }

  // Apply brush and geometric constraints to value.x and value.y.
  // Examples of brush constraint: lum >= epsilon
  // Examples of geometric constraint: radius <= 1
  public static Vector3 ApplyPlanarConstraint(Vector3 value, ColorPickerMode mode,
      float luminanceMin, float saturationMax) {
    switch (mode) {
    case ColorPickerMode.SV_H_Rect:
      // TODO: simplify luminance clamping.  Instead of clamping in UI space, we can
      // get HSL color from prospective selection, clamp, then update UI to match.
      // Given HSL and HSV color spaces:  L = (2 - S) * V / 2
      // (See http://ariya.blogspot.com/2008/07/converting-between-hsl-and-hsv.html)
      // To clamp exactly to our minimum luminance value we'd need to solve for closest
      // point on a curve to user's selected position.  The function is mostly linear
      // in our given range for small L, so we could approximate by closest point to a
      // line.  Even simpler, since the slope is mostly parallel to S axis, we take S
      // as is and solve for V.
      value.x = Mathf.Min(saturationMax, value.x);
      float s = value.x;
      // float v = value.y
      float vMin = 2 * luminanceMin / (2 - s);
      value.y = Mathf.Clamp(value.y, vMin, 1);
      break;

    case ColorPickerMode.SL_H_Triangle:
      value.y = Mathf.Clamp(value.y, luminanceMin, 1);
      // If we end up shipping this picker, should change this to closest-point-in-triangle
      float maxX = SQRT3 * ((value.y < .5f) ? value.y : (1 - value.y));
      maxX *= saturationMax;
      value.x = Mathf.Clamp(value.x, 0, maxX);
      break;

    case ColorPickerMode.HS_LogV_Polar:
    case ColorPickerMode.HS_L_Polar:
    case ColorPickerMode.HL_S_Polar: {
        float maxRadius = (mode == ColorPickerMode.HL_S_Polar) ? 1 - luminanceMin
            : (mode == ColorPickerMode.HS_L_Polar) ? saturationMax
            : 1;
        Vector2 offset = new Vector2(0.5f, 0.5f);
        Vector2 flat = (((Vector2)value) - offset) * 2;
        float radius = flat.magnitude;
        if (radius > maxRadius) {
          flat *= (maxRadius / radius);
          // Assign back
          flat = flat / 2 + offset;
          value.x = flat.x; value.y = flat.y;
        }
        break;
      }
    }
    return value;
  }

  public static bool ModeIsValid(ColorPickerMode mode) {
    ColorPickerInfo info = GetInfoForMode(mode);
    if (info == null) {
      return false;
    }
    if (info.hdr) {
      return false;
    }
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      return true;
    }
#endif
    if (info.experimental) {
      return false;
    }

    return true;
  }

  public static void GoToNextMode(bool forward = true) {
    int NUM_MODES = (int)ColorPickerMode.NUM_MODES;
    int iCur = (int)CustomColorPaletteStorage.m_Instance.Mode;

    /// We want modulo, but C#'s % operator is a remainder.
    /// So use positive numbers, noting that -1 == K-1 (in mod K arithmetic)
    int direction = forward ? 1 : NUM_MODES - 1;
    for (int i = 0; i < NUM_MODES; ++i) {
      int iNext = iCur + (i + 1) * direction;
      ColorPickerMode nextMode = (ColorPickerMode)(iNext % NUM_MODES);
      if (ModeIsValid(nextMode)) {
        CustomColorPaletteStorage.m_Instance.Mode = nextMode;
        return;
      }
    }
  }

  // This function takes an HDR color and clamps it down to an LDR color.
  // We can use this in shaders to better represent HDR colors.
  public static Color ClampColorIntensityToLdr(Color lightColor) {
    float h, s, v;
    Color.RGBToHSV(lightColor, out h, out s, out v);
    v = Mathf.Min(v, 1.0f);
    return Color.HSVToRGB(h, s, v);
  }

  static void MakeHueRamp(int width, int height, Color[] buf) {
    Debug.Assert(buf.Length == width * height);
    HSLColor hsl = new HSLColor(0, 1, 0.5f, 1);
    for (int iy = 0; iy < height; ++iy) {
      hsl.Hue01 = (float)iy / height;
      Color rgb = (Color)hsl;
      for (int ix = 0; ix < width; ++ix) {
        buf[(iy * width) + ix] = rgb;
      }
    }
  }

  static void MakeLightnessRamp(HSLColor hsl, int width, int height, Color[] buf) {
    Debug.Assert(buf.Length == width * height);
    hsl.a = 1;
    for (int iy = 0; iy < height; ++iy) {
      hsl.l = (float)iy / height;
      Color rgb = (Color)hsl;
      for (int ix = 0; ix < width; ++ix) {
        buf[(iy * width) + ix] = rgb;
      }
    }
  }

  static void MakeSaturationRamp(HSLColor hsl, int width, int height, Color[] buf) {
    Debug.Assert(buf.Length == width * height);
    hsl.a = 1;
    for (int iy = 0; iy < height; ++iy) {
      hsl.s = 1 - (float)iy / height;
      Color rgb = (Color)hsl;
      for (int ix = 0; ix < width; ++ix) {
        buf[(iy * width) + ix] = rgb;
      }
    }
  }

  static void MakeLogValueRamp(float hue01, float saturation,
      int width, int height, Color[] buf) {
    Debug.Assert(buf.Length == width * height);
    for (int iy = 0; iy < height; ++iy) {
      float logValue = Mathf.Lerp(kLogVMin, sm_LogVMax, (float)iy / height);
      float value = Mathf.Pow(2, logValue) - MinHDRValue;
      Color rgb = Color.HSVToRGB(hue01, saturation, value, hdr: true);
      rgb.a = 1;
      for (int ix = 0; ix < width; ++ix) {
        buf[(iy * width) + ix] = rgb;
      }
    }
  }

  public static void MakeRamp(ColorPickerMode mode,
      int width, int height, Color[] buf, Vector3? raw = null) {
    switch (mode) {
    case ColorPickerMode.SL_H_Triangle:
    case ColorPickerMode.SV_H_Rect:
      MakeHueRamp(width, height, buf);
      break;
    case ColorPickerMode.HL_S_Polar: {
      Debug.Assert(raw.HasValue);
      HSLColor color;
      bool ok = RawValueToHSLColor(mode, raw.Value, out color);
      Debug.Assert(ok);
      MakeSaturationRamp(color, width, height, buf);
      break;
    }
    case ColorPickerMode.HS_L_Polar: {
      Debug.Assert(raw.HasValue);
      HSLColor color;
      bool ok = RawValueToHSLColor(mode, raw.Value, out color);
      Debug.Assert(ok);
      MakeLightnessRamp(color, width, height, buf);
      break;
    }
    case ColorPickerMode.HS_LogV_Polar: {
      Debug.Assert(raw.HasValue);
      float hue01, saturation, value;
      bool ok = RawValueToHSV(mode, raw.Value, out hue01, out saturation, out value);
      Debug.Assert(ok);
      MakeLogValueRamp(hue01, saturation, width, height, buf);
      break;
    }
    }
  }

  // Adjust color to a min luminance value.
  // minLuminance should be in [0, 1)
  public static Color ClampLuminance(Color color, float minLuminance) {
    if (color.r > 1 || color.g > 1 || color.b > 1) {
      // Can't convert to HSLColor (because HDR), but luminance minimum is already satisfied
      return color;
    }
    HSLColor hsl = (HSLColor)color;
    if (hsl.l >= minLuminance) {
      // Avoid a needless round-trip to hsl and back
      return color;
    } else {
      hsl.l = minLuminance;
      return (Color)hsl;
    }
  }
}
} // namespace TiltBrush
