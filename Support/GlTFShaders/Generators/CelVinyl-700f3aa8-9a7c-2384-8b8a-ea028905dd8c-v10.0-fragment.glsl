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

#extension GL_OES_standard_derivatives : enable
// Brush-specific shader for GlTF web preview, based on General generator
// with parameters lit=1, a=0.5.

precision mediump float;

uniform float u_Cutoff;
uniform sampler2D u_MainTex;

varying vec4 v_color;
varying vec3 v_position;
varying vec2 v_texcoord0;

float dispAmount = .0025;

#include "Fog.glsl"

void main() {
  vec4 tex = texture2D(u_MainTex, v_texcoord0) * v_color;

  if (tex.a<= u_Cutoff) {
	  discard;
  }

  gl_FragColor.rgb = ApplyFog(tex.rgb);
  gl_FragColor.a = 1.0;
}
