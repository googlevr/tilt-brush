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

Shader "Brush/Special/LeakyPen" {
	Properties {
		_MainTex ("Alpha Mask", 2D) = "white" {}
		_SecondaryTex("Diffuse Tex", 2D) = "white" {}
		_Cutoff("Alpha cutoff", Range(0,1)) = 0.5

	}
	SubShader {
		Tags{ "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }

		LOD 400
		CGPROGRAM
		#pragma surface surf StandardSpecular vertex:vert alphatest:_Cutoff addshadow
		#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
		#pragma target 3.0

		#include "UnityCG.cginc"
		#include "Assets/Shaders/Include/Brush.cginc"

		sampler2D _MainTex;
		sampler2D _SecondaryTex;

		struct Input {

		float2 uv_MainTex;
		float2 uv_SecondaryTex;
		float4 color : Color;
		};

		void vert(inout appdata_full i) {
			PrepForOds(i.vertex);
			i.color = TbVertToNative(i.color);
		}

		void surf (Input IN, inout SurfaceOutputStandardSpecular o) {

			float3 secondary_tex = tex2D(_MainTex, IN.uv_SecondaryTex).rgb;

			// Apply the alpha mask
			float primary_tex = tex2D(_MainTex, IN.uv_MainTex).w;

			// Combine the two texture elements
			float3 tex = secondary_tex * primary_tex;
			o.Specular = 0;
			o.Albedo = IN.color.rgb;
			o.Alpha = tex * IN.color.a;

		}
		ENDCG
	}
	FallBack "Diffuse"
}
