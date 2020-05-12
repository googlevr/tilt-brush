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
using UnityEngine;

namespace TiltBrush {

/// HSLColor can round-trip any LDR color, but will not round-trip HDR color.
public struct HSLColor {
  public const float HUE_MAX = 6;

  public float h;   /// Range is [0, HUE_MAX)
  public float s;
  public float l;
  public float a;

  public float HueDegrees {
    get { return h * (360f / HUE_MAX); }
    set {
      // Order of operations is important here to avoid a ULP error
      value = ((value * HUE_MAX) / 360f) % HUE_MAX;
      if (value < 0) { value += HUE_MAX; }
      h = value;
    }
  }

  public float Hue01 {
    get { return h * (1f / HUE_MAX); }
    set {
      value = (value * HUE_MAX) % HUE_MAX;
      if (value < 0) { value += HUE_MAX; }
      h = value;
    }
  }

  /// The range of H is [0, HUE_MAX)
  public HSLColor(float H, float S, float L, float alpha = 1) {
    h = H % HUE_MAX;
    if (h < 0) { h += HUE_MAX; } // C# % is remainder, not modulus: -1 % N == -1
    s = S;
    l = L;
    a = alpha;
  }

  public static implicit operator HSLColor(Color32 color) {
    // Color32 -> Color -> HSLColor is safe, since the starting color is LDR
    return (HSLColor)(Color)color;
  }

  public static explicit operator HSLColor(Color color) {
    float min = Mathf.Min(Mathf.Min(color.r, color.g), color.b);
    if (min > 1) {
      // TODO: when this no longer detects any issues, convert to
      // InvalidOperationException and make the operator explicit
      Debug.Assert(false, "HSL cannot handle HDR color");
      min = 1;
      color.r = Mathf.Min(color.r, min);
      color.g = Mathf.Min(color.g, min);
      color.b = Mathf.Min(color.b, min);
      color.a = Mathf.Min(color.a, min);
    }

    float max = Mathf.Max(Mathf.Max(color.r, color.g), color.b);
    float delta = max - min;

    float h = 0;
    float s = 0;
    float l = (max + min) * 0.5f;

    if (delta != 0) {
      if (l < 0.5f) {
        s = delta / (max + min);
      } else {
        s = delta / (2 - max - min);
      }

      if (color.r == max) {
        h = (color.g - color.b) / delta;
      } else if (color.g == max) {
        h = 2 + (color.b - color.r) / delta;
      } else if (color.b == max) {
        h = 4 + (color.r - color.g) / delta;
      }
    }

    h *= HUE_MAX / 6;
    return new HSLColor(h, s, l, color.a);
  }

  public static explicit operator Color(HSLColor hslColor) {
    if (hslColor.s == 0) {
      return new Color(hslColor.l, hslColor.l, hslColor.l, hslColor.a);
    } else {

      float t2;
      if (hslColor.l < 0.5) {
        t2 = hslColor.l * (1 + hslColor.s);
      } else {
        t2 = (hslColor.l + hslColor.s) - (hslColor.l * hslColor.s);
      }
      float t1 = 2 * hslColor.l - t2;

      float th = hslColor.h * (6 / HUE_MAX);
      var tr = th + 2;
      var tg = th;
      var tb = th - 2;

      return new Color(ColorCalc(tr, t1, t2),
                       ColorCalc(tg, t1, t2),
                       ColorCalc(tb, t1, t2),
                       hslColor.a);
    }
  }

  // c is a periodic value in [0,6)
  // t1, t2 are parametric values in [0,1]
  private static float ColorCalc(float c, float t1, float t2) {
    // normalize (guaranteed not to be more than one period away
    if (c < 0) { c += 6; }
    else if (c >= 6) { c -= 6; }

    if (c < 1) { return t1 + (t2 - t1) * c; }
    if (c < 3) { return t2; }
    if (c < 4) { return t1 + (t2 - t1) * (4 - c); }
    return t1;
  }

  public override string ToString() {
    return String.Format("HSLA({0:F3}, {1:F3}, {2:F3}, {3:F3})", h, s, l, a);
  }

  /// Returns corresponding L=.5 color.
  public HSLColor GetBaseColor() {
    return new HSLColor(h, s, 0.5f, a);
  }

  // ret.l and ret.s are well-defined for all input values, except for
  // when v ~= 1 and s ~= 0. This corresponds to the case where the
  // conversion to HSV loses information (qv).
  public static HSLColor FromHSV(float h, float s, float v, float a = 1) {
    // Don't trust incoming h
    h = h % HUE_MAX;
    if (h < 0) { h += HUE_MAX; }

    HSLColor hsl;
    hsl.a = a;
    hsl.h = h;
    hsl.l = v - 0.5f*s*v;
    if (hsl.l <= .5f) {
      // S = vs / v(2-s);               // factor out the v for stability
      hsl.s = s / (2-s);
    } else {
      // hsl.s has a singularity at v=1, s=0:
      // S = vs / (2(1-v) + vs)
      // Limit has different values depending on how it is approached:
      //   as v -> 1, S=s/s -> 1
      //   as s -> 0, S     -> 0
      // Resolve by special-casing the singular case
      if (s == 0) {
        hsl.s = 0;
      } else {
        float vs = v*s;
        float vinv = (1-v);
        hsl.s = vs / (2*vinv + vs);
      }
    }
    return hsl;
  }

  // To allow round-tripping between HSV and HSL, we ensure that out_H
  // and out_S are well-defined even at singularities.  The only loss of
  // information is when this.l ~= 1 and this.s != 0.
  public void ToHSV(out float out_H, out float out_S, out float out_V) {
    out_H = h;
    if (l <= .5) {
      out_V = l + s * l;
      // out_S = 2*l*s / l*(1 + s);    // factor out the l for stability
      out_S = 2*s / (1 + s);
    } else {
      // If this.l == 1, hsv.s == 0. We lose information about hsl.s,
      // but it's unavoidable.
      float slinv = s * (1 - l);
      out_V = l + slinv;
      out_S = 2*slinv / (l + slinv);
    }
  }
}
}  // namespace TiltBrush
