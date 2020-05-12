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

precision mediump float;

varying vec4 v_color;
varying vec2 v_texcoord0;
uniform sampler2D u_MainTex;
uniform vec4 u_TintColor;
uniform float u_EmissionGain;

void main() {
  // This should be in the vertex shader

  vec4 color = 2.0 * v_color * u_TintColor * texture2D(u_MainTex, v_texcoord0);
  gl_FragColor = color;
}
