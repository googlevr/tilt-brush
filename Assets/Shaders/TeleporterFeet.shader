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

Shader "Custom/TeleporterFeet" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _Color2 ("Main Color 2", Color) = (1,1,1,1)
    _MainTex ("Image", 2D) = "" {}
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  }
  SubShader {
    Tags { "Queue"="Geometry" "RenderType"="Geometry" }

    ZTest Always
    ZWrite Off
    CGPROGRAM
    #pragma surface surf Lambert nofog alphatest:_Cutoff

    struct Input {
      float2 uv_MainTex;
    };

    uniform float4 _Color;
    uniform float4 _Color2;
    uniform half _ScrollSpeed;
    sampler2D _MainTex;

    void surf (Input IN, inout SurfaceOutput o) {
      o.Albedo = 0;
      fixed4 c = tex2D(_MainTex, IN.uv_MainTex );
      o.Emission = c.rgb;

      float t = abs(sin(_Time.y * 4));
      o.Emission *= lerp( _Color2, _Color, t );
      o.Alpha = c.a;
    }
    ENDCG
  }

  FallBack "Diffuse"
}
