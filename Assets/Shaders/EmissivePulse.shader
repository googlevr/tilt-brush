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

Shader "Custom/EmissivePulse" {
  Properties {
    _BaseColor ("Main Color", Color) = (1,1,1,1)
    _PulseColor ("PulseColor", COLOR) = (1,1,1,1)
    _PulseFrequency("Pulse Frequency", Float) = 8
  }

  // -------------------------------------------------------------------------------------------- //
  // DESKTOP VERSION
  // -------------------------------------------------------------------------------------------- //
  SubShader {
    Tags { "Queue"="Geometry" "RenderType"="Geometry" }
    LOD 201

    CGPROGRAM
    #pragma surface surf Lambert vertex:vert
    #include "Assets/Shaders/Include/Math.cginc"

    uniform float4 _BaseColor;
    uniform float4 _PulseColor;
    uniform float _PulseFrequency;

    struct Input {
      float2 uv_MainTex;
    };

    void vert (inout appdata_full v) {
    }

    void surf (Input IN, inout SurfaceOutput o) {
      float t = abs(sin(_Time.y * _PulseFrequency));
      o.Albedo = 0.0;
      o.Emission = lerp(_BaseColor, _PulseColor, t) * 100.0;
      o.Alpha = 1.0;
    }
    ENDCG
  }

  // -------------------------------------------------------------------------------------------- //
  // MOBILE VERSION
  // -------------------------------------------------------------------------------------------- //
  SubShader {
    Tags { "Queue"="Geometry" "RenderType"="Geometry" }
    LOD 150

    CGPROGRAM
    #pragma surface surf Lambert vertex:vert
    #include "Assets/Shaders/Include/Math.cginc"

    uniform float4 _BaseColor;
    uniform float4 _PulseColor;
    uniform float _PulseFrequency;

    struct Input {
      float2 uv_MainTex;
    };

    void vert (inout appdata_full v) {
    }

    void surf (Input IN, inout SurfaceOutput o) {
      float t = abs(sin(_Time.y * _PulseFrequency));
      o.Albedo = _BaseColor;
      o.Emission = t * _PulseColor;
      o.Alpha = 1.0;
    }
    ENDCG
  }
  FallBack "Diffuse"
}
