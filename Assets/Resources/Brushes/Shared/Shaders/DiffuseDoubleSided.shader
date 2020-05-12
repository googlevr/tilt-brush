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

Shader "Brush/DiffuseDoubleSided" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
  _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
}

SubShader {
  Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
  LOD 200
  Cull Off

CGPROGRAM
#pragma surface surf Lambert vertex:vert addshadow
#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
#pragma multi_compile __ SELECTION_ON
// Faster compiles
#pragma skip_variants INSTANCING_ON
#include "Assets/Shaders/Include/Brush.cginc"
#include "Assets/Shaders/Include/MobileSelection.cginc"
#pragma target 3.0

sampler2D _MainTex;
fixed4 _Color;
fixed _Cutoff;

struct appdata {
  float4 vertex : POSITION;
  float2 texcoord : TEXCOORD0;
  float2 texcoord1 : TEXCOORD1;
  float2 texcoord2 : TEXCOORD2;
  half3 normal : NORMAL;
  fixed4 color : COLOR;
  float4 tangent : TANGENT;
  UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Input {
  float2 uv_MainTex;
  float4 color : COLOR;
  fixed vface : VFACE;
};

void vert (inout appdata v, out Input o) {
  UNITY_INITIALIZE_OUTPUT(Input, o);
  PrepForOds(v.vertex);
  v.color = TbVertToNative(v.color);
}

void surf (Input IN, inout SurfaceOutput o) {
  fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
  o.Albedo = c.rgb * IN.color.rgb;
  o.Alpha = c.a * IN.color.a;
  if (o.Alpha < _Cutoff) {
    discard;
  }
  o.Alpha = 1;
  o.Normal = float3(0,0,IN.vface);
  SURF_FRAG_MOBILESELECT(o);
}

ENDCG
}


// MOBILE VERSION
SubShader {
  Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
  LOD 100
  Cull Off

CGPROGRAM
#pragma surface surf Lambert vertex:vert alphatest:_Cutoff
#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
#include "Assets/Shaders/Include/Brush.cginc"
#pragma target 3.0

sampler2D _MainTex;
fixed4 _Color;

struct Input {
  float2 uv_MainTex;
  float4 color : COLOR;
  fixed vface : VFACE;
};

void vert (inout appdata_full v) {
  PrepForOds(v.vertex);
  v.color = TbVertToNative(v.color);
}

void surf (Input IN, inout SurfaceOutput o) {
  fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
  o.Albedo = c.rgb * IN.color.rgb;
  o.Alpha = c.a * IN.color.a;
  o.Normal = float3(0,0,IN.vface);
}
ENDCG
}

Fallback "Transparent/Cutout/VertexLit"
}
