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
varying vec4 v_texcoord1;

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

vec4 _Time;

#include "Particles.glsl"

void main() {
  vec4 pos = GetParticlePositionLS();

  _Time = u_time;
  v_normal = normalMatrix * a_normal;
  v_color = a_color;
  v_texcoord0 = a_texcoord0.xy;
  v_texcoord1 = a_texcoord1;

  float scrollAmount = _Time.y;
  float t = mod(scrollAmount * u_ScrollRate + a_color.a, 1.0);

  vec4 dispVec = (t - .5) * vec4(u_ScrollDistance.x, u_ScrollDistance.y, u_ScrollDistance.z, 0.0);

  dispVec.x += sin(t * u_ScrollJitterFrequency + _Time.y) * u_ScrollJitterIntensity;
  dispVec.z += cos(t * u_ScrollJitterFrequency * .5 + _Time.y) * u_ScrollJitterIntensity;

  vec3 worldPos = (modelMatrix * pos).xyz;
  worldPos.xyz += dispVec.xyz;

  v_color.a = pow(1.0 - abs(2.0*(t - .5)), 3.0);

  gl_Position = projectionMatrix * viewMatrix * vec4(worldPos.x, worldPos.y, worldPos.z,1.0);
  v_position = (modelViewMatrix * pos).xyz;
}
