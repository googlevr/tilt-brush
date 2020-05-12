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

// Default shader for GlTF web preview.
//
// This shader is used as a fall-back when a brush-specific shader is
// unavailable.

attribute vec4 a_position;
attribute vec3 a_normal;
attribute vec4 a_color;
attribute vec4 a_texcoord0;
attribute vec4 a_texcoord1;

varying vec4 v_color;
varying vec3 v_normal;  // Camera-space normal.
varying vec3 v_position;  // Camera-space position.
varying vec2 v_texcoord0;

uniform mat4 viewMatrix;
uniform mat4 modelMatrix;
uniform mat4 modelViewMatrix;
uniform mat4 projectionMatrix;
uniform mat3 normalMatrix;

uniform vec4 u_time;
uniform float u_ScrollRate;
uniform vec3 u_ScrollDistance;
uniform float u_ScrollJitterIntensity;
uniform float u_ScrollJitterFrequency;

#include "Particles.glsl"

void main() {
  vec4 pos = GetParticlePositionLS();

  v_normal = normalMatrix * a_normal;
  v_color = a_color;
  v_texcoord0 = a_texcoord0.xy;

  float t, t2;
   t = mod(u_time.y*u_ScrollRate + a_color.a * 10.0, 1.0);
   t2 = u_time.y;

  // Animate the motion of the embers
  // Accumulate all displacement into a common, pre-transformed space.
  vec4 dispVec = modelMatrix * vec4(u_ScrollDistance.x, u_ScrollDistance.y, u_ScrollDistance.z, 0.0) * t;
  vec3 worldPos = (modelMatrix * pos).xyz;

  dispVec.x += sin(t * u_ScrollJitterFrequency + a_color.a * 100.0 + t2 + worldPos.z) * u_ScrollJitterIntensity;
  dispVec.y += (mod(a_color.a * 100.0, 1.0) - 0.5) * u_ScrollDistance.y * t;
  dispVec.z += cos(t * u_ScrollJitterFrequency + a_color.a * 100.0 + t2 + worldPos.x) * u_ScrollJitterIntensity;

  worldPos.xyz += dispVec.xyz;


  // Ramp color from bright to dark over particle lifetime
  vec3 incolor = a_color.rgb;
  float t_minus_1 = 1.0-t;
  float sparkle = (pow(abs(sin(t2 * 3.0 + a_color.a * 10.0)), 30.0));

  v_color.rgb += pow(t_minus_1,10.0)*incolor*200.0;
  v_color.rgb += incolor * sparkle * 50.0;

  // Dim over lifetime
  v_color.rgb *= incolor * pow (1.0 - t, 2.0)*5.0;

  gl_Position = projectionMatrix * viewMatrix * vec4(worldPos.x, worldPos.y, worldPos.z,1.0);
  v_position = (modelViewMatrix * pos).xyz;
}
