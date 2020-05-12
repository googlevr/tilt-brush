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

Shader "Custom/ToolPanel" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
}

SubShader {
  Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
  LOD 200
  Cull Off


  CGPROGRAM
  #pragma surface surf Lambert vertex:vert alpha


  struct Input {
    float2 uv_MainTex;
  };

  uniform float4 _Color;
  sampler2D _MainTex;

  void vert (inout appdata_full v) {
    v.vertex.xyz -= v.normal * .3;
  }

  void surf (Input IN, inout SurfaceOutput o) {
    fixed4 c = tex2D(_MainTex, IN.uv_MainTex * half2(1,1)) * _Color;
    o.Emission = c.rgb;
    o.Alpha = c.a * .25;
  }
  ENDCG


Blend One One
ZWrite Off
CGPROGRAM
  #pragma surface surf Lambert vertex:vert alpha


  struct Input {
    float2 uv_MainTex;
  };

  uniform float4 _Color;
  sampler2D _MainTex;

  void vert (inout appdata_full v) {

      v.vertex.xyz -= v.normal * .15;
  }

  void surf (Input IN, inout SurfaceOutput o) {
    fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
    o.Emission = c.rgb * .7;
    o.Alpha = c.a;
  }
  ENDCG


CGPROGRAM
#pragma surface surf Lambert alpha

sampler2D _MainTex;
fixed4 _Color;
float3 _WorldSpaceOVRCameraPos;
float3 _OVRCameraForward;

struct Input {
  float2 uv_MainTex;
  float3 worldPos;
};

void surf (Input IN, inout SurfaceOutput o) {
  fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
  o.Emission = c.rgb;
  o.Alpha = c.a;
}
ENDCG

}

Fallback "Unlit/Diffuse"
}
