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

Shader "Custom/VisualizerRing" {
  Properties{
    _MainTex("Main Texture", 2D) = "white" {}
    _EmissionGain("Emission Gain", Range(0, 1)) = 0.5
    _Color("Main Color", Color) = (1,1,1,1)
  }

    Category{
    Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
    Blend One One
    AlphaTest Greater .01
    ColorMask RGB
    Cull Off Lighting Off ZWrite Off Fog{ Color(0,0,0,0) }

    SubShader{
    Pass{

    CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    #pragma multi_compile_particles

    #include "UnityCG.cginc"
    #include "Assets/Shaders/Include/Brush.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    float _EmissionGain;
    float4 _Color;

  struct appdata_t {
    float4 vertex : POSITION;
    fixed4 color : COLOR;
    float2 texcoord : TEXCOORD0;
  };

  struct v2f {
    float4 vertex : POSITION;
    float4 color : COLOR;
    float2 texcoord : TEXCOORD0;
  };

  v2f vert(appdata_t v)
  {
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
    o.color = bloomColor(v.color, _EmissionGain);
    return o;
  }

  fixed4 frag(v2f i) : COLOR
  {

  float index = i.texcoord.x;
  float wav = (tex2D(_WaveFormTex, float2(index,0)).r - .5f);
  float4 c = tex2D(_MainTex, i.texcoord + half2(0,wav));

  c.w = 1;
  return i.color * c * _Color;
  }
    ENDCG
  }
  }
  }
}
