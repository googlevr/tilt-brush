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

// Brush-specific shader for GlTF web preview, based on General generator
// with parameters lit=1, a=0.5.

precision mediump float;

uniform vec4 u_ambient_light_color;
uniform vec4 u_SceneLight_0_color;
uniform vec4 u_SceneLight_1_color;

uniform vec3 u_SpecColor;
uniform float u_Shininess;
uniform sampler2D u_MainTex;
uniform vec4 u_time;
uniform float _EmissionGain;

varying vec4 v_color;
varying vec3 v_normal;
varying vec3 v_position;
varying vec3 v_light_dir_0;
varying vec3 v_light_dir_1;
varying vec2 v_texcoord0;


#include "SurfaceShader.glsl"

vec3 computeLighting() {
  vec3 normal = normalize(v_normal);
  if (!gl_FrontFacing) {
    // Always use front-facing normal for double-sided surfaces.
    normal *= -1.0;
  }
  vec3 lightDir0 = normalize(v_light_dir_0);
  vec3 lightDir1 = normalize(v_light_dir_1);
  vec3 eyeDir = -normalize(v_position);

  float smoothness = .8;
  vec3 spec = vec3(.05,.05,.05);
  vec3 diffuse = vec3(0.0,0.0,0.0);
  vec3 lightOut0 = SurfaceShaderSpecularGloss(normal, lightDir0, eyeDir, u_SceneLight_0_color.rgb,
      diffuse, spec, smoothness);
  vec3 lightOut1 = ShShaderWithSpec(normal, lightDir1, u_SceneLight_1_color.rgb, diffuse, spec);
  vec3 ambientOut = vec3(0.0,0.0,0.0);

  return (lightOut0 + lightOut1 + ambientOut);
}

vec4 bloomColor(vec4 color, float gain) {
   color = pow(color, vec4(2.2,2.2,2.2,2.2));
   color.rgb *= 80.0 * exp(gain * 10.0);
   return color;
}

void main() {
  gl_FragColor.rgb = computeLighting();
  vec2 uv = v_texcoord0;
  uv.x -= u_time.x * 15.0;
  uv.x = mod( abs(uv.x), 1.0);
  float neon = pow(10.0 * clamp(.2 - uv.x,0.0,1.0), 5.0);
  neon = clamp(neon,0.0,1.0);
  vec4 bloom = bloomColor(v_color, _EmissionGain);

  vec3 eyeDir = -normalize(v_position);
  vec3 normal = normalize(v_normal);
  float NdotV = abs(dot(normal, eyeDir));
  bloom *= pow(NdotV,2.0);
  bloom *= NdotV;

  gl_FragColor.rgb += neon * bloom.rgb;
  gl_FragColor.a = 1.0;

}
