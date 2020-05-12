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

Shader "Unlit/ScrollingCutout" {
Properties {
  _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
  _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  _Color ("Color", Color) = (1,1,1,1)
}

SubShader {
  Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
  CGPROGRAM
  #pragma surface surf Lambert vertex:vert alphatest:_Cutoff addshadow

  sampler2D _MainTex;
  uniform float4 _Color;

  struct Input {
    float2 uv_MainTex;
  };

  void vert (inout appdata_full v) {
  }

  void surf (Input IN, inout SurfaceOutput o) {
    float2 timeUVs = IN.uv_MainTex;
    timeUVs.x += _Time.x * 2.0;
    timeUVs.y -= _Time.x * 1.0;
    fixed4 c = tex2D(_MainTex, timeUVs);

    o.Albedo = c.rgb * _Color;
    o.Emission = c.rgb * _Color;
    o.Alpha = c.a;
  }
  ENDCG
}

Fallback "Unlit/Diffuse"
}
