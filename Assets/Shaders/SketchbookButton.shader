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

Shader "Custom/SketchbookButton" {

  Properties {
    _Color ("Color", Color) = (1,1,1,1)
    _Tex_0 ("Texture", 2D) = "white" {}
    _Tex_1 ("Texture", 2D) = "white" {}
    _Distance ("Distance", Range (0,1)) = 0
    _Grayscale ("Grayscale", Float) = 0
    _Cutoff ("Alpha Cutoff", Range (0,1)) = 0.5
  }

  CGINCLUDE
  #include "UnityCG.cginc"
  #include "Assets/Shaders/Include/Hdr.cginc"
  #include "Assets/Shaders/Include/Brush.cginc"
  #pragma target 3.0

  sampler2D _Tex_0;
  sampler2D _Tex_1;
  float4 _Tex_0_ST;
  float4 _Color;
  float _Distance;
  float _Cutoff;
  float4 _GrabHighlightActiveColor;
  uniform float _Activated;
  float _Grayscale;
  uniform float _PanelMipmapBias;

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
    float4 texcoord : TEXCOORD0;
  };

  v2f vertInflate (appdata_t v, float currentSliceIndex) {

    float ratioMultiplier = .5;

    v2f o;
    v.tangent.w = 1.0;
    float totalNumSlices = 5;
    float  ratio = (currentSliceIndex / (totalNumSlices - 1));
    v.vertex.z -= ratioMultiplier * ratio * _Distance;
    totalNumSlices = 5;

    o.vertex = UnityObjectToClipPos(v.vertex);
    o.color = 0;
    o.texcoord = float4(TRANSFORM_TEX(v.texcoord,_Tex_0).xy, 0, _PanelMipmapBias);
    return o;
  }

  v2f vertLayer0 (appdata_t v) {
    return vertInflate(v,0.25);
  }

  v2f vertLayer1 (appdata_t v) {
    return vertInflate(v,0.6);
  }

  fixed4 frag0 (v2f i) : SV_TARGET {
    fixed4 tex = tex2Dbias(_Tex_0, i.texcoord);
    // dim white values to match the rest of panel buttons
    tex.rgb *= .75;
    float4 myColor = _Color * tex;
    myColor.a = tex.a;

    if (myColor.a < _Cutoff)
      discard;

    // Let color bits go grayscale when not in focus
    if (_Grayscale == 1) {
        float grayscale = dot(myColor, float3(0.3, 0.59, 0.11));
        return encodeHdr(grayscale);
    }

    return encodeHdr(myColor);
  }

  fixed4 frag1 (v2f i) : SV_TARGET {
    fixed4 tex = tex2D(_Tex_1, i.texcoord.xy);
	tex.rgb *= .75;
    float4 myColor = _Color * tex;
    myColor.a = tex.a;

    if (myColor.a < _Cutoff)
      discard;

    // Let color bits go grayscale when not in focus
    if (_Grayscale == 1) {
        float grayscale = dot(myColor, float3(0.3, 0.59, 0.11));
        return encodeHdr(grayscale);
    }

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
      #pragma fragment frag0
      ENDCG
    }

    Zwrite On
    Ztest LEqual
    Pass{
      CGPROGRAM
      #pragma vertex vertLayer1
      #pragma fragment frag1
      ENDCG
    }
  }
  FallBack "Diffuse"
}
