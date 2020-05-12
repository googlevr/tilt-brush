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

// Brush-specific shader for GlTF web preview, based on Unlit.glsl
// generator with parameters: a=1.0.

precision mediump float;

varying vec4 v_color;
varying vec2 v_texcoord0;

uniform vec4 u_TintColor;

uniform sampler2D u_MainTex;

void main() {
  gl_FragColor = v_color * u_TintColor * texture2D(u_MainTex, v_texcoord0).w;
}
