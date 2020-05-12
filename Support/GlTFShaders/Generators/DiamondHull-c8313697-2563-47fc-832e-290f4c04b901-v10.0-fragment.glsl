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

// Auto-copied from Ink-c0012095-3ffd-4040-8ee1-fc180d346eaa-v10.0-fragment.glsl
#extension GL_OES_standard_derivatives : enable
// Brush-specific shader for GlTF web preview, based on General generator
// with parameters lit=1, a=0.5.

precision mediump float;

uniform vec4 u_time;
uniform vec4 u_ambient_light_color;
uniform vec4 u_SceneLight_0_color;
uniform vec4 u_SceneLight_1_color;

uniform sampler2D u_MainTex;

// From three.js
uniform vec3 cameraPosition;

varying vec4 v_color;
varying vec3 v_normal;        // Camera-space.
varying vec3 v_worldNormal;   // World-space.
varying vec3 v_position;      // Camera-space.
varying vec3 v_worldPosition; // World-space.
varying vec3 v_light_dir_0;
varying vec3 v_light_dir_1;
varying vec2 v_texcoord0;

float dispAmount = .0025;

#include "Fog.glsl"
#include "NormalMap.glsl"
#include "SurfaceShader.glsl"

// Amplitude reflection coefficient (s-polarized)
float rs(float n1, float n2, float cosI, float cosT) {
  return (n1 * cosI - n2 * cosT) / (n1 * cosI + n2 * cosT);
}

// Amplitude reflection coefficient (p-polarized)
float rp(float n1, float n2, float cosI, float cosT) {
  return (n2 * cosI - n1 * cosT) / (n1 * cosT + n2 * cosI);
}

// Amplitude transmission coefficient (s-polarized)
float ts(float n1, float n2, float cosI, float cosT) {
  return 2.0 * n1 * cosI / (n1 * cosI + n2 * cosT);
}

// Amplitude transmission coefficient (p-polarized)
float tp(float n1, float n2, float cosI, float cosT) {
  return 2.0 * n1 * cosI / (n1 * cosT + n2 * cosI);
}

float thinFilmReflectance(float cos0, float lambda, float thickness, float n0, float n1, float n2) {
  float PI = 3.1415926536;

  // Phase change terms.
  float d10 = mix(PI, 0.0, float(n1 > n0));
  float d12 = mix(PI, 0.0, float(n1 > n2));
  float delta = d10 + d12;

  // Cosine of the reflected angle.
  float sin1 = pow(n0 / n1, 2.0) * (1.0 - pow(cos0, 2.0));

  // Total internal reflection.
  if (sin1 > 1.0) return 1.0;
  float cos1 = sqrt(1.0 - sin1);

  // Cosine of the final transmitted angle, i.e. cos(theta_2)
  // This angle is for the Fresnel term at the bottom interface.
  float sin2 = pow(n0 / n2, 2.0) * (1.0 - pow(cos0, 2.0));

  // Total internal reflection.
  if (sin2 > 1.0) return 1.0;

  float cos2 = sqrt(1.0 - sin2);

  // Reflection transmission amplitude Fresnel coefficients.
  // rho_10 * rho_12 (s-polarized)
  float alpha_s = rs(n1, n0, cos1, cos0) * rs(n1, n2, cos1, cos2);
  // rho_10 * rho_12 (p-polarized)
  float alpha_p = rp(n1, n0, cos1, cos0) * rp(n1, n2, cos1, cos2);

  // tau_01 * tau_12 (s-polarized)
  float beta_s = ts(n0, n1, cos0, cos1) * ts(n1, n2, cos1, cos2);
  // tau_01 * tau_12 (p-polarized)
  float beta_p = tp(n0, n1, cos0, cos1) * tp(n1, n2, cos1, cos2);

  // Compute the phase term (phi).
  float phi = (2.0 * PI / lambda) * (2.0 * n1 * thickness * cos1) + delta;

  // Evaluate the transmitted intensity for the two possible polarizations.
  float ts = pow(beta_s, 2.0) / (pow(alpha_s, 2.0) - 2.0 * alpha_s * cos(phi) + 1.0);
  float tp = pow(beta_p, 2.0) / (pow(alpha_p, 2.0) - 2.0 * alpha_p * cos(phi) + 1.0);

  // Take into account conservation of energy for transmission.
  float beamRatio = (n2 * cos2) / (n0 * cos0);

  // Calculate the average transmitted intensity (polarization distribution of the
  // light source here. If unknown, 50%/50% average is generally used)
  float t = beamRatio * (ts + tp) / 2.0;

  // Derive the reflected intensity.
  return 1.0 - t;
}

vec3 GetDiffraction(vec3 thickTex, vec3 I, vec3 N) {
  const float thicknessMin = 250.0;
  const float thicknessMax = 400.0;
  const float nmedium = 1.0;
  const float nfilm = 1.3;
  const float ninternal = 1.0;
  
  float cos0 = abs(dot(I, N));

  float t = (thickTex[0] + thickTex[1] + thickTex[2]) / 3.0;
  float thick = thicknessMin*(1.0 - t) + thicknessMax*t;

  float red = thinFilmReflectance(cos0, 650.0, thick, nmedium, nfilm, ninternal);
  float green = thinFilmReflectance(cos0, 510.0, thick, nmedium, nfilm, ninternal);
  float blue = thinFilmReflectance(cos0, 475.0, thick, nmedium, nfilm, ninternal);

  return vec3(red, green, blue);
}

vec3 computeLighting(vec3 normal, vec3 albedo, vec3 specColor, float shininess) {
  if (!gl_FrontFacing) {
    // Always use front-facing normal for double-sided surfaces.
    normal *= -1.0;
  }
  vec3 lightDir0 = normalize(v_light_dir_0);
  vec3 lightDir1 = normalize(v_light_dir_1);
  vec3 eyeDir = -normalize(v_position);

  vec3 lightOut0 = SurfaceShaderSpecularGloss(normal, lightDir0, eyeDir, u_SceneLight_0_color.rgb,
      albedo, specColor, shininess);
  vec3 lightOut1 = ShShaderWithSpec(normal, lightDir1, u_SceneLight_1_color.rgb, albedo, specColor);
  vec3 ambientOut = albedo * u_ambient_light_color.rgb;

  return (lightOut0 + lightOut1 + ambientOut);
}

void main() {
  // Hardcode some shiny specular values
  float shininess = .8;
  vec3 albedo = v_color.rgb * .2;

  // Calculate rim
  vec3 viewDir = normalize(cameraPosition - v_worldPosition);
  vec3 normal = v_normal;

  float rim = 1.0 - abs(dot(normalize(viewDir), v_worldNormal));
  rim *= 1.0 - pow(rim, 5.0);

  rim = mix(rim, 150.0,
            1.0 - clamp(abs(dot(normalize(viewDir), v_worldNormal)) / .1, 0.0, 1.0));

  vec3 diffraction = texture2D(u_MainTex, vec2(rim + u_time.x * .3 + normal.x, rim + normal.y)).xyz;
  diffraction = GetDiffraction(diffraction, normal, normalize(viewDir));

  vec3 emission = rim * v_color.rgb * diffraction * .5 + rim * diffraction * .25;
  vec3 specColor = v_color.rgb * clamp(diffraction, 0.0, 1.0);

  gl_FragColor.rgb = computeLighting(v_normal, albedo, specColor, shininess) + emission;
  gl_FragColor.a = 1.0;
}

