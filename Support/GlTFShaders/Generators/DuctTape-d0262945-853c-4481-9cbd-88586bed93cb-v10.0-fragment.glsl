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

// This is the v15 version

precision mediump float;

uniform vec4 u_ambient_light_color;
uniform vec4 u_SceneLight_0_color;
uniform vec4 u_SceneLight_1_color;

uniform vec3 u_SpecColor;
uniform float u_Shininess;
uniform float u_Cutoff;
uniform sampler2D u_MainTex;

varying vec4 v_color;
varying vec3 v_normal;
varying vec3 v_position;
varying vec3 v_light_dir_0;
varying vec3 v_light_dir_1;
varying vec2 v_texcoord0;

float dispAmount = .00009;

#include "Fog.glsl"
#include "NormalMap.glsl"
#include "SurfaceShader.glsl"

vec3 computeLighting(vec3 normal) {
  
  // Always use front-facing normal for double-sided surfaces.
  normal.z *= mix(-1.0, 1.0, float(gl_FrontFacing));
  
  vec3 lightDir0 = normalize(v_light_dir_0);
  vec3 lightDir1 = normalize(v_light_dir_1);
  vec3 eyeDir = -normalize(v_position);

  vec3 lightOut0 = SurfaceShaderSpecularGloss(normal, lightDir0, eyeDir, u_SceneLight_0_color.rgb,
      v_color.rgb, u_SpecColor, u_Shininess);
  vec3 lightOut1 = ShShaderWithSpec(normal, lightDir1, u_SceneLight_1_color.rgb, v_color.rgb, u_SpecColor);
  vec3 ambientOut = v_color.rgb * u_ambient_light_color.rgb;

  return (lightOut0 + lightOut1 + ambientOut);
}

void main() {
  float brush_mask = texture2D(u_MainTex, v_texcoord0).w;
  brush_mask *= v_color.w;

  // WARNING: PerturbNormal uses derivatives and must not be called conditionally.
  vec3 normal = PerturbNormal(v_position.xyz, normalize(v_normal), v_texcoord0);

  // Unfortunately, the compiler keeps optimizing the call to PerturbNormal into the branch below, 
  // causing issues on some hardware/drivers. So we compute lighting just to discard it later.
  gl_FragColor.rgb = ApplyFog(computeLighting(normal));
  gl_FragColor.a = 1.0;

  // This must come last to ensure PerturbNormal is called uniformly for all invocations.
  if (brush_mask <= u_Cutoff) {
	  discard;
  }
}
