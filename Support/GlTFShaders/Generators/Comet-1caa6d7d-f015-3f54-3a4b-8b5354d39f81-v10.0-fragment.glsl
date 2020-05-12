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

uniform float u_Speed;
uniform float u_EmissionGain;
uniform sampler2D u_MainTex;
uniform sampler2D u_AlphaMask;
uniform vec4 u_AlphaMask_TexelSize;
uniform vec4 u_time;

varying vec4 v_color;
varying vec2 v_texcoord0;

void main() {
  // Set up some staggered scrolling for "fire" effect
  float time = u_time.y * -u_Speed;
  vec2 scrollUV = v_texcoord0;
  vec2 scrollUV2 = v_texcoord0;
  vec2 scrollUV3 = v_texcoord0;
  scrollUV.y += time; // a little twisting motion
  scrollUV.x += time;
  scrollUV2.x += time * 1.5;
  scrollUV3.x += time * 0.5;

  // Each channel has its own tileable pattern which we want to scroll against one another
  // at different rates. We pack 'em into channels because it's more performant than
  // using 3 different texture lookups.
  float r = texture2D(u_MainTex, scrollUV).r;
  float g = texture2D(u_MainTex, scrollUV2).g;
  float b = texture2D(u_MainTex, scrollUV3).b;

  // Combine all channels
  float gradient_lookup_value = (r + g + b) / 3.0;
  // Rescales the lookup value from start to finish.
  gradient_lookup_value *= (1.0 - v_texcoord0.x); 
  gradient_lookup_value = (pow(gradient_lookup_value, 2.0) + 0.125) * 3.0;

  float falloff = max((0.2 - v_texcoord0.x) * 5.0, 0.0);
  // TODO: this shouldn't be necessary, but it seems the WebGL texture wrap mode doesn't
  //       match Unity texture wrap mode.
  float gutter = u_AlphaMask_TexelSize.x * .5;
  float u = clamp(gradient_lookup_value + falloff, 0.0 + gutter, 1.0 - gutter);
  vec4 tex = texture2D(u_AlphaMask, vec2(u, 0.0));

  gl_FragColor.rgb = (tex * v_color).rgb;
  gl_FragColor.a = 1.0;
}

