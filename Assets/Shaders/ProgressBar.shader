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

Shader "Custom/ProgressBar" {
    Properties {
        _Color ("Color", Color) = (0,0,0,1)
        _ProgressColor("Progress Color", Color) = (78,217,255,255)
        _MainTex ("Progress Bar Mask", 2D) = "white" {}
        _Ratio ("Progress Ratio", Range(0,1)) = 0
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5

    }
    SubShader {
        Tags{ "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }

        LOD 100
        CGPROGRAM
        #pragma surface surf Unlit nofog alphatest:_Cutoff
        #pragma target 3.0

        half4 LightingUnlit(SurfaceOutput s, half3 lightDir, half atten) {
            half4 c;
            c.rgb = s.Albedo;
            c.a = 1.0;
            return c;
        }

        sampler2D _MainTex;
        float _Ratio;
        float4 _Color;
        float4 _ProgressColor;

        struct Input {
        float2 uv_MainTex;
        };

        void surf (Input IN, inout SurfaceOutput o) {

            _Color = IN.uv_MainTex.x < _Ratio ? _ProgressColor : _Color;
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);

            o.Albedo = 0;
            o.Emission = _Color.rgb;
            o.Alpha = tex;

        }
        ENDCG
    }
    FallBack "Diffuse"
}
