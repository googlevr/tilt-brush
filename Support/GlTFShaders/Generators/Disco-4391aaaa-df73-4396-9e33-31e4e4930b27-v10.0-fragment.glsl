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

precision highp float;

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

#include "Fog.glsl"
#include "SurfaceShader.glsl"

vec3 computeLighting(vec3 diffuseColor, vec3 specularColor) {
  vec3 normal = normalize(v_normal);

#ifdef GL_OES_standard_derivatives
  normal.x = dFdx(normal.x);
  vec3 facetedNormal = normalize(cross(dFdy(v_position), dFdx(v_position)));
#else
  vec3 facetedNormal = normal;
#endif

  vec3 lightDir0 = normalize(v_light_dir_0);
  vec3 lightDir1 = normalize(v_light_dir_1);
  vec3 eyeDir = -normalize(v_position);

  vec3 lightOut0 = SurfaceShaderSpecularGloss(facetedNormal, lightDir0, eyeDir,
      u_SceneLight_0_color.rgb, diffuseColor, specularColor, u_Shininess);
  vec3 lightOut1 = ShShaderWithSpec(normal, lightDir1, u_SceneLight_1_color.rgb, diffuseColor, u_SpecColor);
  vec3 ambientOut = diffuseColor * u_ambient_light_color.rgb;

  // Add a fake "disco ball" hot spot 
  // Note that in the glsl version of this shader, the hot spot is broader in order to create
  // additional highlights. Glsl does not support glossy environment specularity, so we need to compensate
  // with more visual interest here to make up for it.
  float fakeLightIntensity = pow( abs(dot( facetedNormal, vec3(0.0,1.0,0.0))), 10.0) * 20.;
  vec3 fakeLight = specularColor * fakeLightIntensity;
  return (lightOut0 + lightOut1 + ambientOut + fakeLight);
}

void main() {
  vec3 diffuseColor = vec3(0.0,0.0,0.0);
  vec3 specularColor = v_color.rgb * u_SpecColor;

  gl_FragColor.rgb = ApplyFog(computeLighting(diffuseColor, specularColor));
  gl_FragColor.a = 1.0;
}
