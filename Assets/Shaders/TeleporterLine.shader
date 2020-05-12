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

Shader "Custom/TeleporterLine" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _Color2 ("Main Color 2", Color) = (1,1,1,1)
    _MainTex ("Image", 2D) = "" {}
    _ScrollSpeed("Scroll Speed", Float) = 1
    _EmissionColor ("Emission Color", Color) = (1,1,1,1)
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  }
  SubShader {
    Tags { "Queue"="Geometry" "RenderType"="Geometry" }

    CGPROGRAM
    #pragma surface surf Lambert alphatest:_Cutoff

    struct Input {
      float2 uv_MainTex;
    };

    uniform float4 _Color;
    uniform float4 _Color2;
    uniform half _ScrollSpeed;

    void surf (Input IN, inout SurfaceOutput o) {

      o.Albedo = 0;
      float t = abs(sin(_Time.y * 4));
      o.Emission = lerp( _Color2, _Color, t );
      o.Alpha = 1.2 * (sin(IN.uv_MainTex.x + _Time.x * _ScrollSpeed) + 1.0f)/2.0f;
    }
    ENDCG

    CGPROGRAM
    #pragma surface surf Lambert alphatest:_Cutoff

    struct Input {
      float2 uv_MainTex;
    };

    uniform half _ScrollSpeed;
    uniform half4 _EmissionColor;

    void surf (Input IN, inout SurfaceOutput o) {

      o.Albedo = 0;
      o.Emission = _EmissionColor.xyz;

      // Isolate a thin strip inside the line renderer
      float strip = abs(IN.uv_MainTex.y - .5);
      strip = strip > .1 ? 0 : 1;
      o.Alpha = strip * (sin(IN.uv_MainTex.x + _Time.x * _ScrollSpeed) + 1.0f)/2.0f;
    }
    ENDCG
  }
  FallBack "Diffuse"
}
