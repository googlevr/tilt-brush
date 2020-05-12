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

Shader "Brush/Special/KeijiroTube" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
	_Shininess ("Shininess", Range (0.01, 1)) = 0.078125
}
    SubShader {
		LOD 400
    Cull Back

		CGPROGRAM
		#pragma target 3.0
		#pragma surface surf StandardSpecular vertex:vert alphatest:_Cutoff addshadow
		#pragma multi_compile __ AUDIO_REACTIVE
		#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

		#include "Assets/Shaders/Include/Brush.cginc"

		struct Input {
			float2 uv_MainTex;
			float2 uv_BumpMap;
			float4 color : Color;
			float radius;
		};

		fixed4 _Color;
		half _Shininess;

		void vert (inout appdata_full i, out Input o) {
			UNITY_INITIALIZE_OUTPUT(Input, o);
			// o.tangent = v.tangent;
			PrepForOds(i.vertex);
			i.color = TbVertToNative(i.color);

			float radius = i.texcoord.z;
			float wave = sin(i.texcoord.x - _Time.z);
			float pulse = smoothstep(.45, .5, saturate(wave));
			i.vertex.xyz -= pulse * radius * i.normal.xyz;
			o.radius = radius;
		}

		void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
			o.Albedo = _Color.rgb * IN.color.rgb;
			o.Smoothness = _Shininess;
			o.Specular = _SpecColor * IN.color.rgb;
		}
      ENDCG
    }


	FallBack "Diffuse"
}
