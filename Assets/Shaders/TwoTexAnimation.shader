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

Shader "Custom/TwoTexAnimation"
{
  Properties
  {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Texture", 2D) = "white" {}
    _MainTex2 ("Texture2", 2D) = "white" {}
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  }
  SubShader
  {
    Tags{ "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
    LOD 100

    Pass
    {
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

      sampler2D _MainTex;
      sampler2D _MainTex2;
      fixed4 _Color;
      float4 _MainTex_ST;
      float _Cutoff;

      v2f vert (appdata v)
      {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        return o;
      }

      fixed4 frag (v2f i) : SV_Target
      {
        float aux = sin(_Time.w);
        if( aux < 0 ){
          // sample the texture
          fixed4 col = tex2D(_MainTex, i.uv);
          col *= _Color;
          if (tex2D(_MainTex, i.uv).a < _Cutoff)
            discard;
          return col;
        }
        else{
          // sample the texture
          fixed4 col = tex2D(_MainTex2, i.uv);
          col *= _Color;
          if (tex2D(_MainTex2, i.uv).a < _Cutoff)
            discard;

          return col;
        }

      }
      ENDCG
    }
  }
}
