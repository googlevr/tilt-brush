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

Shader "Unlit/TextureCutoutNoCull" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
  _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
}

SubShader {
Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
Cull Off

CGPROGRAM
#pragma surface surf Lambert vertex:vert alphatest:_Cutoff addshadow

sampler2D _MainTex;
fixed4 _Color;
float _SceneFadeAmount;

struct Input {
  float2 uv_MainTex;
  float4 color : COLOR;
};

void vert (inout appdata_full v) {
  }

void surf (Input IN, inout SurfaceOutput o) {
  fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
  c.rgb *= _SceneFadeAmount;

  o.Emission = c.rgb * IN.color.rgb * 1.0;
  o.Albedo = 0.0;

  // original implementation with subtle shadows:
  //o.Emission = c.rgb * IN.color.rgb * .9;
  //o.Albedo = c.rgb * IN.color.rgb * .1; // Just to get a hint of shadows/highlights
  o.Alpha = c.a * IN.color.a;
}
ENDCG
}


Fallback "Unlit/Diffuse"
}
