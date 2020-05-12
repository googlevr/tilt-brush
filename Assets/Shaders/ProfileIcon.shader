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

Shader "Custom/ProfileIcon" {
  Properties {
    _Color("Main Color", Color) = (1,1,1,1)
    _MainTex ("Texture", 2D) = "white" {}
    _MaskTex ("Texture", 2D) = "white" {}
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
 _Grayscale ("Grayscale", Float) = 0
  }
  SubShader {
    Pass {
      Tags{ "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
      Lighting Off

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      #include "UnityCG.cginc"

      struct appdata
      {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };

      struct v2f
      {
        float2 uv : TEXCOORD0;
        float4 vertex : SV_POSITION;
      };

      fixed4 _Color;
      sampler2D _MainTex;
      sampler2D _MaskTex;
      float4 _MainTex_ST;
      float _Cutoff;
      float _Grayscale;

      v2f vert (appdata v)
      {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        return o;
      }

      fixed4 frag (v2f i) : SV_Target
      {
        // sample the texture
        fixed4 c = tex2D(_MainTex, i.uv);
        c.rgb *= .75;
        c.rgb *= _Color.rgb;

        if (tex2D(_MaskTex, i.uv).a < _Cutoff)
          discard;

        if (_Grayscale == 1)
          return fixed4((c.r + c.g + c.b) / 3, (c.r + c.g + c.b) / 3, (c.r + c.g + c.b) / 3, 1);
        return c;
      }
      ENDCG
    }
  }
}
