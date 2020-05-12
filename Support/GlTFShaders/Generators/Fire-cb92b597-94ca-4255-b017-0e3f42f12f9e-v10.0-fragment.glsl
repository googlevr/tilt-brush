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

// Brush-specific shader for GlTF web preview, based on EmissiveAlpha generator.

precision mediump float;

uniform sampler2D u_MainTex;
uniform vec4 u_time;
uniform float u_EmissionGain;

varying vec4 v_color;
varying vec2 v_texcoord0;


vec4 bloomColor(vec4 color, float gain) {
  // Guarantee that there's at least a little bit of all 3 channels.
  // This makes fully-saturated strokes (which only have 2 non-zero
  // color channels) eventually clip to white rather than to a secondary.
  float cmin = length(color.rgb) * .05;
  color.rgb = max(color.rgb, vec3(cmin, cmin, cmin));
  // If we try to remove this pow() from .a, it brightens up
  // pressure-sensitive strokes; looks better as-is.
  color.r = pow(color.r, 2.2);
  color.g = pow(color.g, 2.2);
  color.b = pow(color.b, 2.2);
  color.a = pow(color.a, 2.2);
  color.rgb *= 2.0 * exp(gain * 10.0);
  return color;
}

void main() {
  float _Scroll1 = 20.0;
  float _Scroll2 = 0.0;
  vec4 _Time = u_time;
  float _DisplacementIntensity = 0.1;

  // Should be done in vertex shader
  vec4 bloomed_v_color = bloomColor(v_color, u_EmissionGain);

  float displacement = texture2D(u_MainTex, v_texcoord0.xy + vec2(-_Time.x * _Scroll1, 0.0)  ).a; 
  vec4 tex = texture2D(u_MainTex, v_texcoord0.xy + vec2(-_Time.x * _Scroll2, 0) - displacement * _DisplacementIntensity);

  gl_FragColor = bloomed_v_color * tex;
 // gl_FragColor = vec4(gl_FragColor.rgb * gl_FragColor.a * 5.0, tex.a);
}
