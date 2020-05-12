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

uniform vec4 u_ambient_light_color;
uniform vec4 u_SceneLight_0_color;
uniform vec4 u_SceneLight_1_color;
uniform float u_Shininess;   // Should be in [0.0, 1.0].
uniform vec3 u_SpecColor;
uniform vec4 u_time;

varying vec4 v_color;
varying vec3 v_normal;
varying vec3 v_position;
varying vec3 v_light_dir_0;
varying vec3 v_light_dir_1;
varying vec2 v_texcoord0;

#include "Fog.glsl"
#include "SurfaceShader.glsl"

vec3 computeLighting(vec3 diffuseColor, vec3 specularColor, float shininess) {
  vec3 normal = normalize(v_normal);
  if (!gl_FrontFacing) {
    // Always use front-facing normal for double-sided surfaces.
    normal *= -1.0;
  }
  vec3 lightDir0 = normalize(v_light_dir_0);
  vec3 lightDir1 = normalize(v_light_dir_1);
  vec3 eyeDir = -normalize(v_position);

  vec3 lightOut0 = SurfaceShaderSpecularGloss(normal, lightDir0, eyeDir, u_SceneLight_0_color.rgb,
      diffuseColor, specularColor, shininess);
  vec3 lightOut1 = ShShaderWithSpec(normal, lightDir1, u_SceneLight_1_color.rgb, diffuseColor, u_SpecColor);
  vec3 ambientOut = diffuseColor * u_ambient_light_color.rgb;

  return (lightOut0 + lightOut1 + ambientOut);
}

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
  float envelope = sin ( mod ( v_texcoord0.x*2., 1.) * 3.14159); 
  float lights = envelope < .1 ? 1. : 0.; 
  float border = abs(envelope - .1) < .01 ? 0. : 1.;

  vec3 specularColor = vec3(.3,.3,.3) - lights * vec3(.15,.15,.15);
  float smoothness = .3 - lights * .3;

  float t = u_time.w;

  vec4 color = v_color;
  if (lights > 0.) {
	float colorindex = floor(mod(v_texcoord0.x*2. + 0.5, 3.));
	if (colorindex == 0.) color.rgb = color.rgb * vec3(.2,.2,1.);
	else if (colorindex == 1.) color.rgb = color.rgb * vec3(1.,.2,.2);
	else color.rgb = color.rgb * vec3(.2,1.,.2);
			
	float lightindex =  mod(v_texcoord0.x*2. + .5,7.); 
	float timeindex = mod(t, 7.);
	float delta = abs(lightindex - timeindex);
	float on = 1. - clamp(delta*1.5, 0.0, 1.0);
	color = bloomColor(color * on, .7);
  }


  vec3 diffuseColor = (1.- lights) *  color.rgb * .2;
  diffuseColor *= border;
  specularColor *= border;
  
  gl_FragColor.rgb = computeLighting(diffuseColor, specularColor, smoothness);
  gl_FragColor.a = 1.0;

  // Emission
  gl_FragColor.rgb += lights * color.rgb;
  gl_FragColor.rgb = ApplyFog(gl_FragColor.rgb);
}
