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

Shader "Brush/Special/BubbleWand" {
Properties {
	_MainTex ("Texture", 2D) = "white" {}
	_ScrollRate("Scroll Rate", Float) = 1.0
	_ScrollJitterIntensity("Scroll Jitter Intensity", Float) = 1.0
	_ScrollJitterFrequency("Scroll Jitter Frequency", Float) = 1.0
}

    SubShader {
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
		Blend One One
		Cull off ZWrite Off

		CGPROGRAM
		#pragma target 3.0
		#pragma surface surf StandardSpecular vertex:vert
		#pragma multi_compile __ AUDIO_REACTIVE
		#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
		#include "Assets/Shaders/Include/Brush.cginc"
		#include "Assets/ThirdParty/Shaders/Noise.cginc"

		sampler2D _MainTex;
		float _EmissionGain;
		float _ScrollRate;
		float _ScrollJitterIntensity;
		float _ScrollJitterFrequency;

		float4 displace(float4 pos, float timeOffset) {
			float t = _Time.y*_ScrollRate + timeOffset;

			pos.x += sin(t + _Time.y + pos.z * _ScrollJitterFrequency) * _ScrollJitterIntensity;
			pos.z += cos(t + _Time.y + pos.x * _ScrollJitterFrequency) * _ScrollJitterIntensity;
			pos.y += cos(t * 1.2 + _Time.y + pos.x * _ScrollJitterFrequency) * _ScrollJitterIntensity;

			float time = _Time.x;
			float d = 30;
			float freq = .1;
			float3 disp = float3(1,0,0) * curlX(pos.xyz * freq + time, d);
			disp += float3(0,1,0) * curlY(pos.xyz * freq +time, d);
			disp += float3(0,0,1) * curlZ(pos.xyz * freq + time, d);
			pos.xyz = _ScrollJitterIntensity * disp * kDecimetersToWorldUnits;
			return pos;
		}

		struct Input {
			float4 color : Color;
			float2 tex : TEXCOORD0;
			float3 viewDir;
			INTERNAL_DATA
		};

		void vert (inout appdata_full v, out Input o) {
			PrepForOds(v.vertex);

			float radius = v.texcoord.z;

			// Bulge displacement
			float wave = sin(v.texcoord.x*3.14159);
			float3 wave_displacement = radius * v.normal.xyz * wave;
			v.vertex.xyz += wave_displacement;

			// Noise displacement
			// TO DO: Need to make this scale invariant
			float4 displacement = displace(v.vertex,0);
			v.vertex.xyz += displacement.xyz;

			// Perturb normal
			v.normal = normalize(v.normal + displacement.xyz * 2.5 + wave_displacement * 2.5);

			o.color = TbVertToSrgb(o.color);
			UNITY_INITIALIZE_OUTPUT(Input, o);
		    o.tex = v.texcoord.xy;
		}

		// Input color is srgb
		void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
		    // Hardcode some shiny specular values
			o.Smoothness = .9;
			o.Specular = .6 * SrgbToNative(IN.color).rgb;
			o.Albedo = 0;

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
}
