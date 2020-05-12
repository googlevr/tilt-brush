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
uniform float u_Shininess;
uniform float u_RimIntensity;
uniform float u_RimPower;
uniform vec4 u_Color;

varying vec4 v_color;
varying vec3 v_normal;
varying vec3 v_position;
varying vec3 v_light_dir_0;
varying vec3 v_light_dir_1;
varying vec2 v_texcoord0;

#include "SurfaceShader.glsl"

// Specular only lighting
vec3 computeGlassReflection() {
  vec3 normal = normalize(v_normal);
  float backfaceDimming = 1.; 
  if (!gl_FrontFacing) {
    // Always use front-facing normal for double-sided surfaces.
    normal *= -1.0;
    backfaceDimming = .25;
  }
  vec3 lightDir0 = normalize(v_light_dir_0);
  vec3 lightDir1 = normalize(v_light_dir_1);
  vec3 eyeDir = -normalize(v_position);

  vec3 diffuseColor = vec3(0.,0.,0.);
  vec3 specularColor = vec3(u_Color.r, u_Color.g, u_Color.b);
  vec3 lightOut0 = SurfaceShaderSpecularGloss(normal, lightDir0, eyeDir, u_SceneLight_0_color.rgb,
      diffuseColor, specularColor, u_Shininess);

  // Calculate rim lighting
  float viewAngle = clamp(dot(eyeDir, normal),0.,1.);
  float rim =  pow(1. - viewAngle, u_RimPower) * u_RimIntensity;
  vec3 rimColor = vec3(rim,rim,rim);

  return (lightOut0 + rimColor) * backfaceDimming;
}

void main() {
    gl_FragColor.rgb = computeGlassReflection();
    gl_FragColor.a = 1.0;
}
