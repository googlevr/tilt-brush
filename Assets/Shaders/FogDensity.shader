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

Shader "Custom/FogDensity" {

  Properties {
    _Color ("Fog Color", Color) = (1,1,1,1)
    _NeutralColor("Neutral Color", Color) = (0,0,0,1)
    _FogTex_0 ("Texture", 2D) = "white" {}
    _FogTex_1 ("Texture", 2D) = "white" {}
    _FogTex_2 ("Texture", 2D) = "white" {}
    _FogTex_3 ("Texture", 2D) = "white" {}
    _FogTex_4 ("Texture", 2D) = "white" {}
    _FogDensity ("Fog Density", Float) = 0
    _Distance ("Distance", Range (0,1)) = 0
    _Cutoff ("Alpha Cutoff", Range (0,1)) = 0.5
  }

  CGINCLUDE
  #include "UnityCG.cginc"
    #include "Assets/Shaders/Include/Hdr.cginc"
  #pragma target 3.0

    sampler2D _FogTex_0;
    sampler2D _FogTex_1;
    sampler2D _FogTex_2;
    sampler2D _FogTex_3;
    sampler2D _FogTex_4;
    float4 _FogTex_0_ST;
    float4 _Color;
    float4 _NeutralColor;
    float _FogDensity;
    float _Distance;
    float _Cutoff;

  struct appdata_t {
      float4 vertex : POSITION;
      float4 color : COLOR;
      float3 normal : NORMAL;
      float4 tangent : TANGENT;
      float2 texcoord : TEXCOORD0;
  };

  struct v2f {
      float4 vertex : SV_POSITION;
      float4 color : COLOR;
      float2 texcoord : TEXCOORD0;
  };

  v2f vertInflate (appdata_t v, float currentSliceIndex) {

    float ratioMultiplier = -.5;

    v2f o;
    v.tangent.w = 1.0;
    float totalNumSlices = 5;
    float  ratio = (currentSliceIndex / (totalNumSlices - 1));
    v.vertex.z += ratioMultiplier * ratio * _Distance;
    totalNumSlices = 5;

    o.vertex = UnityObjectToClipPos(v.vertex);
    o.color = 0;
        o.texcoord = TRANSFORM_TEX(v.texcoord,_FogTex_0);
        return o;
  }

  v2f vertLayer0 (appdata_t v) {
    return vertInflate(v,0);
  }

  v2f vertLayer1 (appdata_t v) {
    return vertInflate(v,1);
  }

  v2f vertLayer2 (appdata_t v) {
    return vertInflate(v,2);
  }

  v2f vertLayer3 (appdata_t v) {
    return vertInflate(v,3);
  }

  v2f vertLayer4 (appdata_t v) {
    return vertInflate(v,4);
  }

  fixed4 fragFog0 (v2f i) : SV_TARGET {
    fixed4 tex = tex2D(_FogTex_0, i.texcoord.xy);
    _NeutralColor.rgb *= tex.rgb;
    float4 myColor = lerp(_NeutralColor, _Color, saturate(((8 * _FogDensity) + .5) * _FogDensity));
    myColor.a = tex.a;

    if (tex2D(_FogTex_0, i.texcoord).a < _Cutoff)
      discard;

    return encodeHdr(myColor);
  }

  fixed4 fragFog1 (v2f i) : SV_TARGET {
    fixed4 tex = tex2D(_FogTex_1, i.texcoord.xy);
    _NeutralColor.rgb *= tex.rgb;
    float4 myColor = lerp(_NeutralColor, _Color, saturate(((5 * _FogDensity) + .5) * _FogDensity));
    myColor *= 0.95;
    myColor.a = tex.a;

    if (tex2D(_FogTex_1, i.texcoord).a < _Cutoff)
      discard;

    return encodeHdr(myColor);
  }

  fixed4 fragFog2(v2f i) : SV_TARGET {
    fixed4 tex = tex2D(_FogTex_2, i.texcoord.xy);
    _NeutralColor.rgb *= tex.rgb;
    float4 myColor = lerp(_NeutralColor, _Color, saturate(((2.5 * _FogDensity) + .5) * _FogDensity));
    myColor *= 0.9;
    myColor.a = tex.a;

    if (tex2D(_FogTex_2, i.texcoord).a < _Cutoff)
      discard;

    return encodeHdr(myColor);
  }

  fixed4 fragFog3(v2f i) : SV_TARGET {
    fixed4 tex = tex2D(_FogTex_3, i.texcoord.xy);
    _NeutralColor.rgb *= tex.rgb;
    float4 myColor = lerp(_NeutralColor, _Color, saturate(((1 * _FogDensity) + .5) * _FogDensity));
    myColor *= 0.85;
    myColor.a = tex.a;

    if (tex2D(_FogTex_3, i.texcoord).a < _Cutoff)
      discard;

    return encodeHdr(myColor);
  }

  fixed4 fragFog4(v2f i) : SV_TARGET {
    fixed4 tex = tex2D(_FogTex_4, i.texcoord.xy);
    _NeutralColor.rgb *= tex.rgb;
    float4 myColor = lerp(_NeutralColor, _Color, saturate(((.5 * _FogDensity) + .5) * _FogDensity));
    myColor *= 0.8;
    myColor.a = tex.a;

    if (tex2D(_FogTex_4, i.texcoord).a < _Cutoff)
      discard;

    return encodeHdr(myColor);
  }

  ENDCG

  SubShader {
  Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
    AlphaTest Greater .01

    Zwrite On
    Ztest LEqual
    Pass{
      CGPROGRAM
      #pragma vertex vertLayer0
      #pragma fragment fragFog0
      ENDCG
    }

    Zwrite On
    Ztest LEqual
    Pass{
      CGPROGRAM
      #pragma vertex vertLayer1
      #pragma fragment fragFog1
      ENDCG
    }

    Zwrite On
    Ztest LEqual
    Pass{
      CGPROGRAM
      #pragma vertex vertLayer2
      #pragma fragment fragFog2
      ENDCG
    }

    Zwrite On
    Ztest LEqual
    Pass{
      CGPROGRAM
      #pragma vertex vertLayer3
      #pragma fragment fragFog3
      ENDCG
    }

    Zwrite On
    Ztest LEqual
    Pass{
      CGPROGRAM
      #pragma vertex vertLayer4
      #pragma fragment fragFog4
      ENDCG
    }
  }
  FallBack "Diffuse"
}
