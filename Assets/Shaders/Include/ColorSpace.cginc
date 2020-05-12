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

// -*- c -*-

// hue06 is in [0,6]
float3 hue06_to_base_rgb(in float hue06) {
  float r = -1 + abs(hue06 - 3);
  float g =  2 - abs(hue06 - 2);
  float b =  2 - abs(hue06 - 4);
  return saturate(float3(r, g, b));
}

// hue33 is in [-3,3]
float3 hue33_to_base_rgb(in float hue33) {
  float r = -abs(hue33  ) + 2;
  float g =  abs(hue33+1) - 1;
  float b =  abs(hue33-1) - 1;
  return saturate(float3(r, g, b));
}

// Like sl_to_rgb, but takes chroma instead of saturation.  The difference is:
// - saturation is 1 at the edge of the cone
// - chroma is 1 at the edge of the cylinder
// If the chroma value is outside the cone, returns value with a=0.
float4 cl_to_rgb(in float3 base_rgb, float chroma, float lightness) {
  float max_chroma = (1 - abs(2 * lightness - 1));
  float3 rgb = (base_rgb - 0.5) * chroma + lightness;
  return chroma > max_chroma ? float4(0,0,0,0) : float4(rgb,1);
}

// HSL to RGB.  Get base_rgb from xxx_to_base_rgb.
half3 sl_to_rgb(in float3 base_rgb, float saturation, float lightness) {
  float max_chroma = (1 - abs(2 * lightness - 1));
  float chroma = max_chroma * saturation;
  return (base_rgb - 0.5) * chroma + lightness;
}

// HSV to RGB.  Get base_rgb from xxx_to_base_rgb.
float3 sv_to_rgb(in float3 base_rgb, float saturation, float value) {
  return ((base_rgb - 1) * saturation + 1) * value;
}

// Chroma-Luma to rgb. This is like HSL but with the bottom cone pushed flat
// (kind of like HSV is HSL with the top cone pushed flat), plus some work
// to try and make it perceptually-linear
float3 g_max_luma = float3(0.299, 0.587, 0.114);
float3 cy_to_rgb(in float3 base_rgb, in float chroma, in float luma) {
  float rgb_luma = dot(base_rgb, g_max_luma);
  if (luma < rgb_luma) {
    chroma *= luma / rgb_luma;
  } else if (luma < 1) {
    chroma *= (1 - luma) / (1 - rgb_luma);
  }
  return (base_rgb - rgb_luma) * chroma + luma;
}

// Helper for converting uv to polar coordinates
// Return radius in [0,1] , and angle in [-.5, .5]
float2 xy_to_polar(float2 xy) {
  float pi = 3.141592653589793;
  float2 centered = xy * 2 - 1;
  float radius = length(centered);
  float angle = atan2(centered.y, centered.x) * (.5 / pi);
  return float2(radius, angle);
}

// unoptimized
float3 HSVToRGB(in float3 hsv)
{
  float3 fullySaturatedRgb;
  float hue = hsv.x * 6.0f;
  float chroma = hsv.y * hsv.z;
  if (hue < 1.0f)
  {
      fullySaturatedRgb.x = hsv.y;
      fullySaturatedRgb.y = hue * hsv.y;
      fullySaturatedRgb.z = 0.0f;
  }
  else if (hue < 2.0f)
  {
      fullySaturatedRgb.x = (2.0f - hue) * hsv.y;
      fullySaturatedRgb.y = hsv.y;
      fullySaturatedRgb.z = 0.0f;
  }
  else if (hue < 3.0f)
  {
      fullySaturatedRgb.x = 0.0f;
      fullySaturatedRgb.y = hsv.y;
      fullySaturatedRgb.z = (hue - 2.0f) * hsv.y;
  }
  else if (hue < 4.0f)
  {
      fullySaturatedRgb.x = 0.0f;
      fullySaturatedRgb.y = (4.0f - hue) * hsv.y;
      fullySaturatedRgb.z = hsv.y;
  }
  else if (hue < 5.0f)
  {
      fullySaturatedRgb.x = (hue - 4.0f) * hsv.y;
      fullySaturatedRgb.y = 0.0f;
      fullySaturatedRgb.z = hsv.y;
  }
  else
  {
      fullySaturatedRgb.x = hsv.y;
      fullySaturatedRgb.y = 0.0f;
      fullySaturatedRgb.z = (6.0f - hue) * hsv.y;
  }

  float m = hsv.z - chroma;
  return fullySaturatedRgb + float3(m, m, m);
}

// unoptimized
float3 RGBtoHSV(in float3 rgb)
{
  float epsilon = 1e-10;
  float minRgb = min(min(rgb.x, rgb.y), rgb.z);
  float maxRgb = max(max(rgb.x, rgb.y), rgb.z);
  float chroma = maxRgb - minRgb;
  float hue;
  if (maxRgb == rgb.x)
  {
      // prevent divide by 0 by adding epsilon (numerator will also be 0 in this case)
      hue = (rgb.y - rgb.z) / (chroma + epsilon);
      if (hue < 0) hue += 6.0f;
  }
  else if (maxRgb == rgb.y)
  {
      hue = (rgb.z - rgb.x) / chroma + 2.0f;
  }
  else
  {
      hue = (rgb.x - rgb.y) / chroma + 4.0f;
  }
  float value = maxRgb;
  float3 hsv;
  hsv.x = hue / 6.0f;
  hsv.y = chroma / (value + epsilon);
  hsv.z = value;
  return hsv;
}

// less-optimized
#if 0
// c is a periodic value in [-6, 6), with period 6
float hue_to_rgb_component(float c, float t1, float t2) {
  // normalize to [0, 6)
  c += (c < 0) ? 6 : 0;
  // if (c < 0) c += 6;
  if      (c < 1) return t1 + (t2 - t1) * c;
  else if (c < 3) return t2;
  else if (c < 4) return t1 + (t2 - t1) * (4 - c);
  else            return t1;
}

// hue6 is in [-3,3]
float3 hsl_to_rgb(float hue6, float saturation, float lightness) {
  float t2;
  if (lightness < 0.5) {
    t2 = lightness * (1 + saturation);
  } else {
    t2 = lerp(saturation, 1, lightness);
  }
  float t1 = 2 * lightness - t2;

  float tr = hue6 + 2;      // in [-1, 5]
  float tg = hue6;          // in [-3, 3]
  float tb = hue6 - 2;      // in [-5, 1]
  return float3(
      hue_to_rgb_component(tr, t1, t2),
      hue_to_rgb_component(tg, t1, t2),
      hue_to_rgb_component(tb, t1, t2));
}
#endif
