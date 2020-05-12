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


//
// ARGB32 HDR Encoding Support (RGB + Exponent + 1-bit Alpha)
//
// Note that the calling shader must have the following pragma defined:
// #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE

// The exponent itself needs to fit into 8-bits, so we have to apply a
// normalizing factor. The factor below assumes 20k-30k max HDR value.
// Note that tweaking this value will affect the look of the LDR bloom.
//
// HDR_SIMPLE
//
// HDR simple is used for mobile bloom rendering. The constraints for this are that much like the
// emulated HDR, the light magnitude is stored in the alpha channel. However, in HDR simple the
// RGB values must look okay without having any postprocessing applied to them. Therefore RGB
// values are saturated out in the top five bits, and then the original color is stored in the
// lowest 3 bits. This results in an almost white-out, with a very slight tint of the original
// color. In the bloom code the original color (or a reasonable approximation to it) is
// regenerated from the low 3 bits.
#define HDR_SCALE 16.0

float4 encodeHdr(float3 color)
{
#ifdef HDR_SIMPLE
  float m = max(max(color.r, color.g), color.b);
  if (m <= 1) return float4(color, 1);

  // colors with magnitudes over 1 get lerped to white to produce white-out
  float3 lerped = lerp(color, 1, clamp((m - 1) / 512, 0, 1));

  // Convert to 0 - 255, and drop the lower 3 bits.
  int3 iclamp = floor(clamp(lerped * 255, 0, 255));
  int3 bottom = iclamp & 248;

  // Create a normalized version of the color, and expand to an int expressible in 3 bits,
  // and combine the two parts
  float3 normalized = color / m;
  int3 inorm = round(normalized * 7);
  float3 combined =  inorm | bottom;

  m = clamp(log2(max(1, m)), 0, 1);
  return float4(combined / 255, 1 - (m / HDR_SCALE));
#else
#ifndef HDR_EMULATED
  return float4(color, 1.0);
#else
  // Find the max component, reduce to exponent.
  float m = max(max(color.r, color.g), color.b);
  float expf = min(log2(max(m, 0.001)), HDR_SCALE);
  float4 c = float4(color, 1.0);

  if (m > 1.0) {
    // If m < 1.0, its not HDR; no need to encode.
    c /= exp2(log2(max(m, 0.001)));
    c.a = 1 - (expf / HDR_SCALE);
  }

  return c;
#endif
#endif
}

float getExpf(float a) {
#ifndef HDR_EMULATED
  return 1.0;
#else
  float expf = a;
  if (expf <= 0.0) expf = 1.0;
  if (expf >= 1.0) expf = 1.0;
  return exp2((1-expf) * HDR_SCALE);
#endif
}

float getAmt(float a) {
#ifndef HDR_EMULATED
  return 0.0;
#else
  float expf = a;
  if (expf <= 0.0) return 0.0;
  if (expf >= 1.0) return 0.0;
  return 1.0;
#endif
}

float4 decodeHdr(float4 color) {
#ifdef HDR_EMULATED
  color.rgb *= lerp(1.0, getExpf(color.a), getAmt(color.a));
  color.a = 1.0;
#elif HDR_SIMPLE
  if (color.a < 1) {
    return float4(color.rgb, 1);
  }
  float3 col = clamp((color.rgb - 0.75) * 4, 0, 1);
  return float4(col.rgb * color.a, 1);
#endif

  // Always clamp to max to avoid massive brain-melting output spikes.
  // Clamping to zero is also important, since negative HDR values aren't supported and returning
  // negative values here will likely cause undefined behavior (due to pow(), etc).
  //
  // Unity bug: https://support.unity3d.com/hc/en-us/requests/434060
  //
  const float4 MAX_VALUE = float4(30001, 30001, 30001, 1.0);
  const float4 MIN_VALUE = float4(0, 0, 0, 0);
  return clamp(color, MIN_VALUE, MAX_VALUE);
}

