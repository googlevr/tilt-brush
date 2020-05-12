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

// Unlit.glsl
precision mediump float;

varying vec4 v_color;
varying vec2 v_texcoord0;
varying vec3 v_position;

#if TB_HAS_ALPHA_CUTOFF
uniform sampler2D u_MainTex;
#endif

#include "Fog.glsl"

vec3 computeLighting() {
  return v_color.rgb;
}

void main() {
#if TB_HAS_ALPHA_CUTOFF
  const float alpha_threshold = TB_ALPHA_CUTOFF;
  float brush_mask = texture2D(u_MainTex, v_texcoord0).w;
  if (brush_mask > alpha_threshold) {
    gl_FragColor.rgb = ApplyFog(computeLighting());
    gl_FragColor.a = 1.0;
  } else {
    discard;
  }
#else
  gl_FragColor.rgb = ApplyFog(computeLighting());
  gl_FragColor.a = 1.0;
#endif
}
