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

// Auto-copied from Hypercolor-e8ef32b1-baa8-460a-9c2c-9cf8506794f5-v10.0-fragment.glsl
#extension GL_OES_standard_derivatives : enable
// Brush-specific shader for GlTF web preview, based on Standard.glsl
// generator with parameters: a=0.5.

precision mediump float;

uniform vec4 u_ambient_light_color;
uniform vec4 u_SceneLight_0_color;
uniform vec4 u_SceneLight_1_color;
uniform float u_Shininess;   // Should be in [0.0, 1.0].
uniform vec3 u_SpecColor;


varying vec4 v_color;
varying vec3 v_normal;
varying vec3 v_position;
varying vec3 v_light_dir_0;
varying vec3 v_light_dir_1;
varying vec2 v_texcoord0;

uniform sampler2D u_MainTex;
uniform vec4 u_time;
uniform float u_Cutoff;

float dispAmount = .0005;

#include "Fog.glsl"
#include "NormalMap.glsl"
#include "SurfaceShader.glsl"

vec3 computeLighting(vec3 diffuseColor, vec3 specularColor, vec3 normal) {
  if (!gl_FrontFacing) {
    // Always use front-facing normal for double-sided surfaces.
    normal *= -1.0;
  }
  vec3 lightDir0 = normalize(v_light_dir_0);
  vec3 lightDir1 = normalize(v_light_dir_1);
  vec3 eyeDir = -normalize(v_position);

  vec3 lightOut0 = SurfaceShaderSpecularGloss(normal, lightDir0, eyeDir, u_SceneLight_0_color.rgb,
      diffuseColor, specularColor, u_Shininess);
  vec3 lightOut1 = ShShaderWithSpec(normal, lightDir1, u_SceneLight_1_color.rgb, diffuseColor, u_SpecColor);
  vec3 ambientOut = diffuseColor * u_ambient_light_color.rgb;

  return (lightOut0 + lightOut1 + ambientOut);
}

void main() {
  vec4 tex = texture2D(u_MainTex, v_texcoord0);

  // WARNING: PerturbNormal uses derivatives and must not be called conditionally.
  vec3 normal = PerturbNormal(v_position.xyz, normalize(v_normal), v_texcoord0);

  // Unfortunately, the compiler keeps optimizing the call to PerturbNormal into the branch below, 
  // causing issues on some hardware/drivers. So we compute lighting just to discard it later.
  float scroll = u_time.z;
  tex.rgb = vec3(1.0, 0.0, 0.0) * (sin(tex.r * 2.0 + scroll*0.5 - v_texcoord0.x) + 1.0) * 2.0;
  tex.rgb += vec3(0.0, 1.0, 0.0) * (sin(tex.r * 3.3 + scroll*1.0 - v_texcoord0.x) + 1.0) * 2.0;
  tex.rgb += vec3(0.0, 0.0, 1.0) * (sin(tex.r * 4.66 + scroll*0.25 - v_texcoord0.x) + 1.0) * 2.0;

  float colorMultiplier = 0.5; // This factor is glsl specific - not exactly sure why I need to fudge this to match the look in Tilt Brush.
  vec3 specularColor = u_SpecColor * tex.rgb * colorMultiplier;
  vec3 diffuseColor = tex.rgb * v_color.rgb * colorMultiplier;
  gl_FragColor.rgb = ApplyFog(computeLighting(diffuseColor, specularColor, normal));
  gl_FragColor.a = 1.0;

  // This must come last to ensure PerturbNormal is called uniformly for all invocations.
  if (tex.w <= u_Cutoff) {
	  discard;
  }
}
