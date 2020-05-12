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

Shader "Custom/AssetLoading" {
Properties {
  _Color("Color", Color) = (0,0,0,1)
  _LineColor("Line Color", Color) = (78,217,255,255)
  _MainTex("Base (RGB) TransGloss (A)", 2D) = "white" {}
  _Cutoff("Alpha cutoff", Range(0,1)) = 0.65
}


SubShader {
  Tags {"Queue"="AlphaTest+20" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
  LOD 100

  Pass {
    CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    #include "UnityCG.cginc"
    #include "Assets/Shaders/Include/Hdr.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    float4 _Color;
    float4 _LineColor;
    float _Ratio;
    float duration;
    uniform float _Cutoff;

  struct appdata_t {
    float4 vertex : POSITION;
    float2 texcoord : TEXCOORD0;
  };

  struct v2f {
    float4 vertex : SV_POSITION;
    float2 texcoord : TEXCOORD0;
  };

  v2f vert (appdata_t v) {
    v2f o;

    o.vertex = UnityObjectToClipPos(v.vertex);
    o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
    return o;
  }

  fixed4 frag (v2f i) : COLOR {


    float t = fmod(_Time.y, 2);
    t = saturate(t / 1.0);

    float angle = atan2(i.texcoord.x - 0.5, -i.texcoord.y + 0.5); //angle should go between 0, and 2pi
    angle = (1 - ((angle + 3.14159) / (2 * 3.14159))); // remap 0 : 1

    // - 0.2 and 0.5 here are values forcing the animation to begin and end where we want it
    float4 smoothstepColor = (smoothstep(t, t - 0.2, angle * 0.5));

    // Multiply through radial animation
    fixed4 c = tex2D(_MainTex, i.texcoord) * (smoothstepColor * _LineColor);

    // Multiply color here in case panel loses focus while assets are loading
    c.rgb *= _Color.rgb;

    if (c.a < _Cutoff) discard;

    return encodeHdr(c.rgb * c.a);
  }
    ENDCG
  }
}

}

