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

// Brush-specific shader for GlTF web preview, based on Diffuse.glsl
// generator with parameters: a=0.067.

precision mediump float;

uniform vec4 u_ambient_light_color;
uniform vec4 u_SceneLight_0_color;
uniform vec4 u_SceneLight_1_color;

varying vec4 v_color;
varying vec3 v_normal;
varying vec3 v_position;
varying vec3 v_light_dir_0;
varying vec3 v_light_dir_1;
varying vec2 v_texcoord0;

uniform sampler2D u_MainTex;
uniform float u_Cutoff;

#include "Fog.glsl"

#include "SurfaceShader.glsl"

vec3 computeLighting() {
  vec3 normal = normalize(v_normal);
  if (!gl_FrontFacing) {
    // Always use front-facing normal for double-sided surfaces.
    normal *= -1.0;
  }
  vec3 lightDir0 = normalize(v_light_dir_0);
  vec3 lightDir1 = normalize(v_light_dir_1);

  vec3 lightOut0 = LambertShader(normal, lightDir0,
      u_SceneLight_0_color.rgb, v_color.rgb);
  vec3 lightOut1 = ShShader(normal, lightDir1,
      u_SceneLight_1_color.rgb, v_color.rgb);
  vec3 ambientOut = v_color.rgb * u_ambient_light_color.rgb;

  return (lightOut0 + lightOut1 + ambientOut);
}

void main() {
  float brush_mask = texture2D(u_MainTex, v_texcoord0).w;
  if (brush_mask > u_Cutoff) {
    gl_FragColor.rgb = ApplyFog(computeLighting());
    gl_FragColor.a = 1.0;
  } else {
    discard;
  }
}
