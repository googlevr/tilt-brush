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

Shader "Hidden/BlitDownsample"
{
  Properties
  {
    _MainTex ("Texture", 2D) = "white" {}
    _Scale("Scale", Range(0.01, 8.0)) = 1.0
  }
  SubShader
  {
    // No culling or depth
    Cull Off ZWrite Off ZTest Always

    Pass
    {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      #include "UnityCG.cginc"

      float _Scale;

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

      v2f vert (appdata v)
      {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv * _Scale;
        return o;
      }

      sampler2D _MainTex;

      fixed4 frag (v2f i) : SV_Target
      {
        return tex2D(_MainTex, i.uv);
      }
      ENDCG
    }
  }
}
