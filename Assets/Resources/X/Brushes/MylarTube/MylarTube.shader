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

Shader "Brush/Special/MylarTube" {
Properties {
	_MainTex ("Texture", 2D) = "white" {}
	_Color ("Main Color", Color) = (1,1,1,1)
	_SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
	_Shininess ("Shininess", Range (0.01, 1)) = 0.078125
	_SqueezeAmount("Squeeze Amount", Range(0.0,1)) = 0.825
}

    SubShader {

		CGPROGRAM
		#pragma target 3.0
		#pragma surface surf StandardSpecular vertex:vert addshadow
		#pragma multi_compile __ AUDIO_REACTIVE
		#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
		#include "Assets/Shaders/Include/Brush.cginc"
		#include "Assets/ThirdParty/Shaders/Noise.cginc"

		sampler2D _MainTex;
		fixed4 _Color;
		half _Shininess;
		half _SqueezeAmount;

		struct Input {
			float4 color : Color;
			float2 tex : TEXCOORD0;
			float3 viewDir;
			INTERNAL_DATA
		};

		void vert (inout appdata_full v, out Input o) {
			PrepForOds(v.vertex);

			float radius = v.texcoord.z;

			// Squeeze displacement
			float squeeze = sin(v.texcoord.x*3.14159);
			float3 squeeze_displacement = radius * v.normal.xyz * squeeze;
			v.vertex.xyz -= squeeze_displacement * _SqueezeAmount;

			// Perturb normal
			v.normal = normalize(v.normal + squeeze_displacement * 2.5);

			o.color = TbVertToSrgb(o.color);
			UNITY_INITIALIZE_OUTPUT(Input, o);
		    o.tex = v.texcoord.xy;
		}

		// Input color is srgb
		void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
		    o.Albedo =  _Color.rgb * IN.color.rgb;
			//o.Emission =  _Color.rgb * IN.color.rgb;
			o.Smoothness = _Shininess;
			o.Specular = _SpecColor * IN.color.rgb;

			// Calculate rim
			float3 n = WorldNormalVector (IN, o.Normal);
			half rim = 1.0 - abs(dot (normalize(IN.viewDir), n));
			rim *= 1-pow(rim,5);

			//Thin slit diffraction texture ramp lookup
			float3 diffraction = tex2D(_MainTex, half2(rim + _Time.x + o.Normal.y, rim + o.Normal.y)).xyz;
			o.Emission = rim*(.25 * diffraction * rim  + .75 * diffraction * IN.color);

		}
		ENDCG
    }

	  FallBack "Diffuse"
}
