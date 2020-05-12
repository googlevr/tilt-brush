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

Shader "Brush/Special/Rain" {
Properties {
	_MainTex ("Particle Texture", 2D) = "white" {}
	_NumSides("Number of Sides", Float) = 5
	_Speed("Speed", Float) = 1
	_Bulge("Displacement Amount", Float) = 2.25
}

Category {
	Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
	Blend One One
	BlendOp Add, Min
	AlphaTest Greater .01
	ColorMask RGBA
	Cull off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

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
			float4 _MainTex_ST;
			float _NumSides;
			float _Speed;
			float _Bulge;

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
				float4 worldPos : TEXCOORD1;
			};


			v2f vert (appdata_full v)
			{
				PrepForOds(v.vertex);
				v.color = TbVertToSrgb(v.color);

				v2f o;

				// Inflate the tube outward to explode it into
				// strips - giving us negative space w/o as much overdraw.
				_Bulge = 2.25;
				float radius = v.texcoord.z;
				v.vertex.xyz += v.normal.xyz * _Bulge * radius;

				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
				o.color = TbVertToNative(v.color);
				return o;
			}

			float rand_1_05(in float2 uv)
			{
				float2 noise = (frac(sin(dot(uv, float2(12.9898, 78.233)*2.0)) * 4550));
				return abs(noise.x) * 0.7;
			}

			// Input color is srgb
			fixed4 frag (v2f i) : COLOR
			{
				float u_scale = _Speed;
				float t = fmod(_Time.y * 4 * u_scale, u_scale);

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
				float u = uvs.x * u_scale - t;

				// Calculate a an ID value for each face.
				// on a 4 sided tube, the v coords are  0:.25, .25:.5, .5,.75, .75:1
				// so multiplying by number of sides and taking the integer is the ID
				// *NOTE: we should ask jeremy I think this only actually works because of float precisions

				float row_id = (int) (uvs.y *(_NumSides));
				float rand = rand_1_05(row_id.xx);

				// Randomize by row ID, add _Time offset by row and add an offset back into U
				// so the strips don't animate together
				u += rand * _Time.y * 2.75 * u_scale;

				// Wrap the u coordinate in the 0:u_scale range.
				// If we don't do this, then the strokes we offset previously
				// will have values that are too large
				u = fmod(u, u_scale);

				// Rescale the V coord of each strip in the 0:1 range
				float v = uvs.y * _NumSides;

				// Sample final texture
				half4 tex = tex2D(_MainTex, half2(u,v));

				tex = u < 0 ? 0 : tex;
				tex = u > 1 ? 0 : tex;

				// Fade at edges of a given stroke
				float fade = pow(abs(i.texcoord.x * 0.25), 9);
				float4 color = i.color * tex;
				float4 finalColor = lerp(color, float4(0, 0, 0, 0), saturate(fade));

				color = encodeHdr(finalColor.rgb * finalColor.a);
				color = SrgbToNative(color);
				return color;
			}
			ENDCG
		}
	}
}
}
