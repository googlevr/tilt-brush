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

Shader "Brush/Special/Sparks" {
Properties {
	_MainTex ("Particle Texture", 2D) = "white" {}
    _EmissionGain ("Emission Gain", Range(0, 1)) = 0.5
	_DisplacementAmount ("Displacement Amount", Float) = 1
	_DisplacementExponent ("Displacement Exponent", Float) = 3
	_StretchDistortionExponent ("Stretch Distortion Exponent", Float) = 3
	_NumSides ("Number of Sides", Float) = 5
	_Speed ("Speed", Float) = 1
}

Category {
	Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
	Blend One One
	BlendOp Add, Min
	AlphaTest Greater .01
	ColorMask RGBA
	Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

	SubShader {
		Pass {

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile __ AUDIO_REACTIVE
			#pragma multi_compile_particles
			#pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
			#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
			#pragma target 3.0

			#include "UnityCG.cginc"
			#include "Assets/Shaders/Include/Brush.cginc"
			#include "Assets/Shaders/Include/Hdr.cginc"

			sampler2D _MainTex;

			struct appdata_t {
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float3 normal : NORMAL;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			float4 _MainTex_ST;
			half _EmissionGain;
			float _DisplacementAmount;
			float _DisplacementExponent;
			float _StretchDistortionExponent;
			float _NumSides;
			float _Speed;

			v2f vert (appdata_t v)
			{
				PrepForOds(v.vertex);
				v.color = TbVertToSrgb(v.color);

				v2f o;

				// This multiplier is a magic number but it's still not right. Is there a better
				// multiplciation for this (not using fmod) so I can count on the "lifetime" being contstant?
				/*
				float t01 = fmod(_Time.y * 0.95, 1);
				float3 incolor = v.color.rgb;
				v.color.rgb *= incolor * pow(1 - t01, 2) * 10;
				*/

				float displacement = pow(v.texcoord.x,_DisplacementExponent);
				v.vertex.xyz += v.normal * displacement * _DisplacementAmount;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
				o.color = TbVertToNative(v.color);
				return o;
			}

			float rand_1_05(in float2 uv)
			{
				float2 noise = (frac(sin(dot(uv, float2(12.9898, 78.233)*2.0)) * 50));
				return abs(noise.x + noise.y) * 0.75;
			}

			fixed4 frag (v2f i) : COLOR
			{

				// Distort U coord to taste. This makes the effect to "slow down" towards the end of the stroke
				// by clumping UV's closer together toward the beginning of the stroke
				i.texcoord.x = pow(i.texcoord.x, _StretchDistortionExponent);

				// Rescale time to go between 0 : u_scale, where u_scale is the range of warped u coords on the stroke
				float u_scale = _Speed;
				float t = fmod(_Time.w * u_scale, u_scale);

				// Rescale U coord in range 0 : u_scale.
				// Note that we subtract "t" because we want to move the origin (i.e. the "0" value)
				// of the U coordinate along the length of the stroke
				//
				// e.g.
				//     0  1  2  3  4  5  6 ...
				//    -1  0  1  2  3  4  5
				//    -2 -1  0  1  2  3  4
				//
				//  where the texture will begin at u = 0
				//
				float2 uvs = i.texcoord;
				float u = uvs.x * u_scale -  t;

				// Calculate a an ID value for each face.
				// on a 4 sided tube, the v coords are  0:.25, .25:.5, .5,.75, .75:1
				// so multiplying by number of sides and taking the integer is the ID
				// *NOTE: we should ask jeremy I think this only actually works because of float precision
				float row_id = (int) (uvs.y * (_NumSides));
				float rand = rand_1_05(row_id.xx);

				// Randomize by row ID and add an offset back into U, so the strips don't animate together
				u += rand * u_scale;

				// Wrap the u coordinate in the 0:u_scale range.
				// If we don't do this, then the strokes we offset previously
				// will have values that are too large
				u = fmod(u, u_scale);

				// Rescale the V coord of each strip in the 0:1 range
				float v = uvs.y * _NumSides;

				// Sample texture
				float4 tex = tex2D(_MainTex, half2(u, v));

				// Because texture is set to repeat, manually clamp
				// to zero outside texture bounds
				tex = u < 0 ? 0 : tex;
				tex = u > 1 ? 0 : tex;

				// Apply bloom based on a falloff down the stroke
				float bloom = exp(_EmissionGain * 5.0f) * (1 - i.texcoord.x);

				float4 color = i.color * tex * bloom;
				color = encodeHdr(color.rgb * color.a);
				color = SrgbToNative(color);
				return color;
			}
			ENDCG
		}
	}
}
}
