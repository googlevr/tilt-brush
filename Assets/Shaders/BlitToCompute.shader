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

Shader "Hidden/BlitToCompute"
{
  Properties
  {
    _MainTex ("Texture", 2D) = "white" {}
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

      #pragma target 5.0
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

      v2f vert (appdata v)
      {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        return o;
      }

      sampler2D _MainTex;
      float4 _MainTex_TexelSize;
      RWStructuredBuffer<unsigned int> _CaptureBuffer : register(u1);

      fixed4 frag (v2f i) : SV_Target
      {
        float4 c = tex2D(_MainTex, i.uv);
        float4 cOut = c;

        // Make sure to ignore non-zero alpha.
        c.a = 1;

        // Convert to byte scale.
        c *= 255;

        // Unity Color32 format.
        c = c.abgr;

        // Clean up rounding error / left over HDR.
        c = clamp(c, 0, 255);

        // Write to UAV.
        // TexelSize = (1/w, 1/h, w, h)
        const float2 u = i.uv;
        const float width = _MainTex_TexelSize.z;
        const float height = _MainTex_TexelSize.w;
        const int x = u.x * width;
        const int y = u.y * height;
        _CaptureBuffer[(y * width) + x] = ((int)(c.r)) << 24 | ((int)(c.g)) << 16 | ((int)(c.b)) << 8 | ((int)(c.a));

        return cOut;
      }
      ENDCG
    }
  }
}
