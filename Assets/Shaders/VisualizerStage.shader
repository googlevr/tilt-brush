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

Shader "Custom/VisualizerStage" {
  Properties{
    _Color("Main Color", Color) = (1,1,1,1)
    _SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
    _Shininess("Shininess", Range(0.01, 1)) = 0.078125
    _MainTex("Base (RGB) TransGloss (A)", 2D) = "white" {}
    _BumpMap("Normalmap", 2D) = "bump" {}
    _EmissionGain("Emission Gain", Range(0, 1)) = 0.5
  }
    SubShader{
    CGPROGRAM
    #pragma target 3.0
    #pragma surface surf StandardSpecular
    #include "Assets/Shaders/Include/Brush.cginc"

  struct Input {
    float2 uv_MainTex;
    float2 uv_BumpMap;
    float4 color : Color;
  };

  sampler2D _MainTex;
  sampler2D _BumpMap;
  fixed4 _Color;
  half _Shininess;
  float _EmissionGain;

  void surf(Input IN, inout SurfaceOutputStandardSpecular o) {
    int quant = 20;
    float index = floor((IN.uv_MainTex.x)*quant) / quant;
    float4 wav = tex2D(_WaveFormTex, float2(index, 0)) - .5;
    float4 mask = tex2D(_MainTex, IN.uv_MainTex);
    wav = floor(wav * quant) / quant;

    float4 c = .0;
    float3 tintcolor = _PeakBandLevels.x * float3(.7,0,.3) + _PeakBandLevels.z * float3(.3,0,.7) + _PeakBandLevels.w * 2 * float3(0,1,0);
    tintcolor = normalize(tintcolor);
    c = abs(IN.uv_MainTex.y - .5) < wav.g ? float4(tintcolor,1) : 0;
    c.rgb *= mask.rgb;
    c.rgb = c.rgb * .5 + c.rgb * _BeatOutput.y;

    c.w = 1;
    o.Emission = bloomColor(c, _EmissionGain);

    o.Albedo = _Color.rgb * mask.rgb;
    o.Smoothness = _Shininess;
    o.Specular = _SpecColor * mask.a;
    o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
    o.Alpha = 1;
  }
  ENDCG
  }

  FallBack "Diffuse"
}
