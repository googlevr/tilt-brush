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

// DefaultVS.glsl
attribute vec4 a_position;
attribute vec3 a_normal;
attribute vec4 a_color;
attribute vec2 a_texcoord0;

varying vec4 v_color;
varying vec3 v_normal;  // Camera-space normal.
varying vec3 v_position;  // Camera-space position.
varying vec3 v_local_position;
varying vec2 v_texcoord0;
varying vec3 v_light_dir_0;  // Camera-space light direction, main light.
varying vec3 v_light_dir_1;  // Camera-space light direction, other light.

uniform mat4 modelViewMatrix;
uniform mat4 projectionMatrix;
uniform mat3 normalMatrix;
uniform mat4 u_SceneLight_0_matrix;
uniform mat4 u_SceneLight_1_matrix;

void main() {
  gl_Position = projectionMatrix * modelViewMatrix * a_position;
  v_normal = normalMatrix * a_normal;
  v_position = (modelViewMatrix * a_position).xyz;
  v_local_position = a_position.xyz;
  v_light_dir_0 = mat3(u_SceneLight_0_matrix) * vec3(0, 0, 1);
  v_light_dir_1 = mat3(u_SceneLight_1_matrix) * vec3(0, 0, 1);
  v_color = a_color;
  v_texcoord0 = a_texcoord0;
}
