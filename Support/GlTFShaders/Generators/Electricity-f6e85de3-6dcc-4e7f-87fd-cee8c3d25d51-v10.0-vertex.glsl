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

// Default shader for GlTF web preview.
//
// This shader is used as a fall-back when a brush-specific shader is
// unavailable.

attribute vec4 a_position;
attribute vec3 a_normal;
attribute vec4 a_color;
attribute vec2 a_texcoord0;
attribute vec3 a_texcoord1;

varying vec4 v_color;
varying vec3 v_normal;  // Camera-space normal.
varying vec3 v_position;  // Camera-space position.
varying vec2 v_texcoord0;

uniform mat4 modelViewMatrix;
uniform mat4 projectionMatrix;
uniform mat3 normalMatrix;
uniform mat4 modelMatrix;
uniform mat4 viewMatrix;

uniform vec4 u_time;
uniform float u_ScrollRate;
uniform vec3 u_ScrollDistance;
uniform float u_ScrollJitterIntensity;
uniform float u_ScrollJitterFrequency;
uniform float u_DisplacementIntensity;

void main() {
	float envelope = sin(a_texcoord0.x * (3.14159));
	float envelopePow =  (1.0-pow(1.0 - envelope, 10.0));
	vec3 offsetFromMiddleToEdge_CS = a_texcoord1.xyz;
	float widthiness_CS = length(offsetFromMiddleToEdge_CS) / .02;
	vec3 midpointPos_CS = a_position.xyz - offsetFromMiddleToEdge_CS;

	float t = u_time.w;

	vec3 worldPos = (modelMatrix * a_position).xyz;
	
	// This recreates the standard ribbon position with some tapering at edges
	worldPos.xyz = midpointPos_CS + offsetFromMiddleToEdge_CS * envelopePow; 
	worldPos = vec4(modelMatrix * vec4(worldPos.xyz, 1.0)).xyz;

	// This adds noise
	vec3 dispVec = vec3(0., 0., 0.);
	dispVec.x += sin(midpointPos_CS.z * 100.0 + t * 13.0 ) * 0.05;
	dispVec.y += cos(midpointPos_CS.x * 120.0 + t * 10.0 ) * 0.05;
	dispVec.z += cos(midpointPos_CS.y * 80.0 + t * 7.0 ) * 0.05;
	dispVec = (modelMatrix * vec4(dispVec, 0.0)).xyz;
	
	worldPos.xyz += widthiness_CS * dispVec * u_DisplacementIntensity * envelopePow; 
	
	gl_Position = projectionMatrix * viewMatrix * vec4(worldPos.x, worldPos.y, worldPos.z,1.0);
	v_position = (modelViewMatrix * a_position).xyz;
	v_color = a_color;
	
	// boost v_color at edges
	v_color += v_color * (1.0 - envelopePow);

	v_texcoord0 = a_texcoord0;
	v_normal = normalMatrix * a_normal;
}
