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

// Brush-specific shader for GlTF web preview, based on EmissiveAlpha generator.

precision mediump float;

uniform sampler2D u_MainTex;
uniform vec4 u_time;
uniform float u_EmissionGain;
varying vec4 v_color;
varying vec2 v_texcoord0;

vec4 GetRainbowColor( vec2 texcoord)
{
	vec4 _Time = u_time;
	texcoord = clamp(texcoord, 0.0, 1.0);
	// Create parametric UV's
	vec2 uvs = texcoord;
	float row_id = floor(uvs.y * 5.0);
	uvs.y *= 5.0;
				 
	// Create parametric colors
	vec4 tex = vec4(0.0,0.0,0.0,1.0);
		
	float row_y = mod(uvs.y,1.0); 

	
	row_id = ceil(mod(row_id + _Time.z,5.0)) - 1.0;
	
	tex.rgb = row_id == 0.0 ? vec3(1.0,0.0,0.0) : tex.rgb;
	tex.rgb = row_id == 1.0 ? vec3(.7,.3,0.0) : tex.rgb;
	tex.rgb = row_id == 2.0 ? vec3(0.0,1.0,.0) : tex.rgb;
	tex.rgb = row_id == 3.0 ? vec3(0.0,.2,1.0) : tex.rgb;
	tex.rgb = row_id == 4.0 ? vec3(.4,0.0,1.2) : tex.rgb;
	
	// Make rainbow lines pulse
	tex.rgb *= pow( (sin(row_id * 1.0 + _Time.z)   + 1.0)/2.0,5.0);

	// Make rainbow lines thin
	tex.rgb *= clamp(pow(row_y * (1.0 - row_y) * 5.0, 50.0), 0.0, 1.0);  


	return tex;
}

void main() { 
  vec4 color = v_color;
  color.a = 1.;
  vec4 tex =  GetRainbowColor(v_texcoord0.xy);
  tex = color * tex * exp(u_EmissionGain * 3.0);
  
  gl_FragColor = tex * tex.a;
 
}
