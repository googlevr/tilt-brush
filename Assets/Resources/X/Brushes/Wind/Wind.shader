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

Shader "Brush/Special/Wind" {
Properties {
	_MainTex ("Texture", 2D) = "white" {}
	_Speed ("Animation Speed", Range (0,1)) = 1
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
			#pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
			#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
			#include "UnityCG.cginc"
			#include "Assets/Shaders/Include/Brush.cginc"
			#include "Assets/Shaders/Include/Hdr.cginc"

			sampler2D _MainTex;
			sampler2D _AlphaMask;
			float4 _MainTex_ST;
			float _Speed;

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


			v2f vert (appdata_t v)
			{
				PrepForOds(v.vertex);
				v2f o;

				o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
				o.color = TbVertToNative(v.color);
				o.vertex = UnityObjectToClipPos(v.vertex);

				return o;
			}

			fixed4 frag (v2f i) : COLOR
			{
				// Simple scrollin'
				float time = _Time.y * _Speed;
				fixed2 scrollUV = i.texcoord;
				scrollUV.x += time * 0.5;

				float4 tex = tex2D(_MainTex, scrollUV);
				return encodeHdr(tex * i.color.rgb * i.color.a);
			}
			ENDCG
		}
	}
}
}
