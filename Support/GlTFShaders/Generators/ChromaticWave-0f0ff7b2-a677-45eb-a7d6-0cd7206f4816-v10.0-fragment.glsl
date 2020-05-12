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

uniform float u_EmissionGain;
uniform sampler2D u_MainTex;
uniform vec4 u_time;

varying vec4 v_color;
varying vec2 v_texcoord0;

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

	vec4 unbloomedColor = v_color;
	vec4 bloomedColor = bloomColor(v_color, u_EmissionGain);
	vec2 texcoord = v_texcoord0.xy;
	
	float envelope = sin(texcoord.x * 3.14159);
	texcoord.y += texcoord.x * 3.0;

	float waveform_r = .15 * sin( -20.0 * unbloomedColor.r * u_time.w + texcoord.x * 100.0 * unbloomedColor.r);
	float waveform_g = .15 * sin( -30.0 * unbloomedColor.g * u_time.w + texcoord.x * 100.0 * unbloomedColor.g);
	float waveform_b = .15 * sin( -40.0 * unbloomedColor.b * u_time.w + texcoord.x * 100.0 * unbloomedColor.b);
				   
	texcoord.y = mod(texcoord.y + texcoord.x, 1.0);
	texcoord.y = mod(texcoord.y + texcoord.x, 1.0);

	float procedural_line_r = clamp(1.0 - 40.0*abs(texcoord.y - .5 + waveform_r),0.0,1.0);
	float procedural_line_g = clamp(1.0 - 40.0*abs(texcoord.y - .5 + waveform_g),0.0,1.0);
	float procedural_line_b = clamp(1.0 - 40.0*abs(texcoord.y - .5 + waveform_b),0.0,1.0);

	vec4 color = procedural_line_r * vec4(1.0,0.0,0.0,0.0) + procedural_line_g * vec4(0.0,1.0,0.0,0.0) + procedural_line_b * vec4(0.0,0.0,1.0,0.0);
	color.w = 1.0;
	color = bloomedColor * color;

    gl_FragColor = vec4(color.rgb * color.a, 1.0);
}
