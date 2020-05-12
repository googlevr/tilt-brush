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

uniform vec4 u_time;
uniform vec4 u_ambient_light_color;
uniform vec4 u_SceneLight_0_color;
uniform vec4 u_SceneLight_1_color;

uniform float u_Cutoff;
uniform sampler2D u_MainTex;

varying vec4 v_color;
varying vec3 v_normal;
varying vec3 v_position;
varying vec3 v_light_dir_0;
varying vec3 v_light_dir_1;
varying vec2 v_texcoord0;

float dispAmount = .0025;

#include "Fog.glsl"
#include "NormalMap.glsl"
#include "SurfaceShader.glsl"

vec3 computeLighting(vec3 normal, vec3 specColor, float shininess) {
  if (!gl_FrontFacing) {
    // Always use front-facing normal for double-sided surfaces.
    normal *= -1.0;
  }
  vec3 lightDir0 = normalize(v_light_dir_0);
  vec3 lightDir1 = normalize(v_light_dir_1);
  vec3 eyeDir = -normalize(v_position);

  vec3 lightOut0 = SurfaceShaderSpecularGloss(normal, lightDir0, eyeDir, u_SceneLight_0_color.rgb,
      v_color.rgb, specColor, shininess);
  vec3 lightOut1 = ShShaderWithSpec(normal, lightDir1, u_SceneLight_1_color.rgb, v_color.rgb, specColor);
  vec3 ambientOut = v_color.rgb * u_ambient_light_color.rgb;

  return (lightOut0 + lightOut1 + ambientOut);
}

void main() {
  vec2 scrollUV = v_texcoord0;

  // Animate flipbook motion. Currently tuned to taste.
  float anim = ceil(mod(u_time.y * 12.0, 6.0));
  scrollUV.x += anim;
  scrollUV.x *= 1.1;

  vec3 specColor = vec3(0.0);
  float shininess = 0.0;
  float brush_mask = texture2D(u_MainTex, scrollUV).w * v_color.a;

  if (brush_mask <= u_Cutoff) {
	  discard;
  }

  gl_FragColor.rgb = ApplyFog(computeLighting(v_normal, specColor, shininess));
  gl_FragColor.a = 1.0;
}
