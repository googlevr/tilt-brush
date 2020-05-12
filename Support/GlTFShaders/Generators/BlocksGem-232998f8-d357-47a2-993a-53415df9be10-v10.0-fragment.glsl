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

precision mediump float;

uniform vec4 u_ambient_light_color;
uniform vec4 u_SceneLight_0_color;
uniform vec4 u_SceneLight_1_color;
uniform float u_Shininess;
uniform float u_RimIntensity;
uniform float u_RimPower;
uniform vec4 u_Color;
uniform float u_Frequency;
uniform float u_Jitter;

varying vec4 v_color;
varying vec3 v_normal;
varying vec3 v_position;
varying vec3 v_local_position;
varying vec3 v_light_dir_0;
varying vec3 v_light_dir_1;
varying vec2 v_texcoord0;

#include "SurfaceShader.glsl"
#include "third_party/voronoi.glsl"

// Specular only lighting
vec3 computeGemReflection() {
  vec3 normal = normalize(v_normal);
  
  // Get Voronoi
  vec2 F = fBm_F0(v_local_position, OCTAVES);
  float gem = (F.y - F.x);

  // Perturb normal with voronoi cells
  float perturbIntensity = 50.; //was 10. in unity.  Presumably glsl vs. hlsl is the source of the discrepancy.
  normal.y += dFdy(gem) * perturbIntensity;
  normal.x += dFdx(gem) * perturbIntensity;
  normal = normalize(normal);

  vec3 lightDir0 = normalize(v_light_dir_0);
  vec3 lightDir1 = normalize(v_light_dir_1);
  vec3 eyeDir = -normalize(v_position);
  vec3 diffuseColor = vec3(0.,0.,0.);

  // Artifical diffraction highlights to simulate what I see in blocks. Tuned to taste.
  vec3 refl = eyeDir - 2. * dot(eyeDir, normal) * normal + gem;
  vec3 colorRamp = vec3(1.,.3,0)*sin(refl.x * 30.) + vec3(0.,1.,.5)*cos(refl.y * 37.77) + vec3(0.,0.,1.)*sin(refl.z*43.33);

  // was colorRamp * .1 in unity, but boosting since
  // we don't have an environment map on Poly
  vec3 specularColor = u_Color.rgb + colorRamp * .5;
  float smoothness =  u_Shininess;

  vec3 lightOut0 = SurfaceShaderSpecularGloss(normal, lightDir0, eyeDir, u_SceneLight_0_color.rgb,
      diffuseColor, specularColor, smoothness);

  // Calculate rim lighting
  float viewAngle = clamp(dot(eyeDir, normal),0.,1.);
  float rim =  pow(1. - viewAngle, u_RimPower);
  vec3 rimColor = vec3(rim,rim,rim) * u_RimIntensity;

  return (lightOut0 + rimColor);
}

void main() {
    gl_FragColor.rgb = computeGemReflection();
    gl_FragColor.a = 1.0;
}
