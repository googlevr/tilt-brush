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

Shader "Custom/SketchSurface" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _BackColor ("Backside Color", Color) = (1,1,1,1)
  _BorderTex ("Border Color", 2D) = "white" {}
  _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
  _BackTex ("Backside Color", 2D) = "white" {}
}

SubShader {
  Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
  LOD 100

CGPROGRAM
#pragma surface surf Lambert alpha

sampler2D _MainTex;
sampler2D _BorderTex;
fixed4 _Color;

struct Input {
  float2 uv_MainTex;
  float2 uv_BorderTex;
};

void surf (Input IN, inout SurfaceOutput o) {
  fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
  fixed4 border = tex2D(_BorderTex, IN.uv_BorderTex) * _Color;
  o.Emission = c.rgb + border.rgb;
  o.Alpha = c.a * .25 + border.a;
}
ENDCG



Cull Front
CGPROGRAM
#pragma surface surf Lambert alpha

sampler2D _BackTex;
sampler2D _BorderTex;
fixed4 _BackColor;

struct Input {
  float2 uv_MainTex;
  float2 uv_BorderTex;
};

void surf (Input IN, inout SurfaceOutput o) {
  fixed4 c = tex2D(_BackTex, IN.uv_MainTex) * _BackColor;
  fixed4 border = tex2D(_BorderTex, IN.uv_BorderTex) * _BackColor;
  o.Emission = c.rgb + border.rgb;
  o.Alpha = c.a * .25 + border.a;
}
ENDCG

}

Fallback "Unlit/Diffuse"
}
