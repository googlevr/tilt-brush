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

// Canvas transform.
uniform float4x4 xf_CS;
// Inverse canvas transform.
uniform float4x4 xf_I_CS;
uniform uint _BatchID;  // NOTOOLKIT
#include "Ods.cginc"  // NOTOOLKIT

// Unity only guarantees signed 2.8 for fixed4.
// In practice, 2*exp(_EmissionGain * 10) = 180, so we need to use float4
float4 bloomColor(float4 color, float gain) {
  // Guarantee that there's at least a little bit of all 3 channels.
  // This makes fully-saturated strokes (which only have 2 non-zero
  // color channels) eventually clip to white rather than to a secondary.
  float cmin = length(color.rgb) * .05;
  color.rgb = max(color.rgb, float3(cmin, cmin, cmin));
  // If we try to remove this pow() from .a, it brightens up
  // pressure-sensitive strokes; looks better as-is.
  color = pow(color, 2.2);
  color.rgb *= 2 * exp(gain * 10);
  return color;
}

// Used by various shaders to animate selection outlines
// Needs to be visible even when the color is black
float4 GetAnimatedSelectionColor( float4 color) {
  return color + sin(_Time.w*2)*.1 + .2f;
}


//
// Common for Music Reactive Brushes
//

sampler2D _WaveFormTex;
sampler2D _FFTTex;
uniform float4 _BeatOutputAccum;
uniform float4 _BeatOutput;
uniform float4 _AudioVolume;
uniform float4 _PeakBandLevels;

// returns a random value seeded by color between 0 and 2 pi
float randomizeByColor(float4 color) {
  const float PI = 3.14159265359;
  float val =  (3*color.r + 2*color.g + color.b) * 1000;
  val =  2 * PI * fmod(val, 1);
  return val;
}

float3 randomNormal(float3 color) {
  float noiseX = frac(sin(color.x))*46336.23745f;
  float noiseY = frac(sin(color.y))*34748.34744f;
  float noiseZ = frac(sin(color.z))*59998.47362f;
  return normalize(float3(noiseX, noiseY, noiseZ));
}

float4 musicReactiveColor(float4 color, float beat) {
  float randomOffset = randomizeByColor(color);
  color.xyz = color.xyz * .5 + color.xyz * saturate(sin(beat * 3.14159 + randomOffset) );
  return color;
}

float4 musicReactiveAnimationWorldSpace(float4 worldPos, float4 color, float beat, float t) {
  float intensity = .15;
  float randomOffset = 2 * 3.14159 * randomizeByColor(color) + _Time.w + worldPos.z;
  // the first sin function makes the start and end points of the UV's (0:1) have zero modulation.
  // The second sin term causes vibration along the stroke like a plucked guitar string - frequency defined by color
  worldPos.xyz += randomNormal(color.rgb) * beat * sin(t * 3.14159) * sin(randomOffset) * intensity;
  return worldPos;
}

float4 musicReactiveAnimation(float4 vertex, float4 color, float beat, float t) {
  float4 worldPos = mul(unity_ObjectToWorld, vertex);
  return mul(unity_WorldToObject, musicReactiveAnimationWorldSpace(worldPos, color, beat, t));
}

// Unity 5.1 and below use camera-space particle vertices
// Unity 5.2 and above use world-space particle vertices
#if UNITY_VERSION < 520
uniform float4x4 _ParticleVertexToWorld;
float4 ParticleVertexToWorld(float4 vertex) {
  return mul(_ParticleVertexToWorld, vertex);
}
#else
float4 ParticleVertexToWorld(float4 vertex) {
  return vertex;
}
#endif

//
// For Toolkit support
//

float4 SrgbToLinear(float4 color) {
  // Approximation http://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html
  float3 sRGB = color.rgb;
  color.rgb = sRGB * (sRGB * (sRGB * 0.305306011 + 0.682171111) + 0.012522878);
  return color;
}

float4 SrgbToLinear_Large(float4 color) {
    float4 linearColor = SrgbToLinear(color);
  color.r = color.r < 1.0 ? linearColor.r : color.r;
  color.g = color.g < 1.0 ? linearColor.g : color.g;
  color.b = color.b < 1.0 ? linearColor.b : color.b;
  return color;
}

float4 LinearToSrgb(float4 color) {
  // Approximation http://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html
  float3 linearColor = color.rgb;
  float3 S1 = sqrt(linearColor);
  float3 S2 = sqrt(S1);
  float3 S3 = sqrt(S2);
  color.rgb = 0.662002687 * S1 + 0.684122060 * S2 - 0.323583601 * S3 - 0.0225411470 * linearColor;
  return color;
}

// TB mesh colors are sRGB. TBT mesh colors are linear.
// TOOLKIT: float4 TbVertToSrgb(float4 color) { return LinearToSrgb(color); }
// TOOLKIT: float4 TbVertToLinear(float4 color) { return color; }
float4 TbVertToSrgb(float4 color) { return color; } // NOTOOLKIT
float4 TbVertToLinear(float4 color) { return SrgbToLinear(color); } // NOTOOLKIT

// Conversions to and from native colorspace.
// Note that SrgbToLinear_Large only converts to linear in the 0:1 range
// because Linear HDR values don't work with the Tilt Brush bloom filter
#ifdef TBT_LINEAR_TARGET
float4 SrgbToNative(float4 color) { return SrgbToLinear_Large(color); }
float4 TbVertToNative(float4 color) { return TbVertToLinear(color); }
float4 NativeToSrgb(float4 color) { return LinearToSrgb(color); }
#else
float4 SrgbToNative(float4 color) { return color; }
float4 TbVertToNative(float4 color) { return TbVertToSrgb(color); }
float4 NativeToSrgb(float4 color) { return color; }
#endif

// TBT is in meters, TB is in decimeters.
// TOOLKIT: #define kDecimetersToWorldUnits 0.1
#define kDecimetersToWorldUnits 1.0 // NOTOOLKIT

