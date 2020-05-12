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

Shader "Custom/SwipeHint" {
  Properties{
    _MainTex("Base (RGB) Transgloss (A)", 2D) = "white" {}
    _Color("Color", Color) = (1,1,1,1)
    _Shininess("Smoothness", Range(0.01, 1)) = 0.13
    _PulseColor("Pulse Color", Color) = (3,3,3,3)
    _PulseColorDark("Pulse Color Dark", Color) = (3,3,3,3)
    _PulseFrequency ("Pulse Frequency", Float) = 10
    _PulseIntensity ("Pulse Intensity", Float) = 10

  }
    SubShader{
      Tags { "RenderType" = "Opaque" }
      LOD 100

      CGPROGRAM
      #pragma surface surf Standard nofog
      #pragma target 3.0
      #include "Assets/Shaders/Include/Math.cginc"

      sampler2D _MainTex;
      float2 uv_MainTex;
      half _Shininess;
      float4 _Color;
      uniform float4 _PulseColor;
      uniform float4 _PulseColorDark;
      uniform float _PulseFrequency;
      uniform float _PulseIntensity;

      struct Input {
      float2 uv_MainTex;
      };

      void surf(Input IN, inout SurfaceOutputStandard o) {
      fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
      fixed4 c = tex * _Color;
      o.Smoothness = _Shininess;

      // animate a nice lil' pulse
      float t = sin(_Time.y * _PulseFrequency) * 0.5 + 0.5;
      float4 lerpedColor = lerp(_PulseColor * c, _PulseColorDark * c, t);

      //rest o' the outputs
      o.Emission = lerpedColor * _PulseIntensity;
      o.Albedo = 0;
    }
    ENDCG
  }
Fallback "Unlit/Diffuse"
}

