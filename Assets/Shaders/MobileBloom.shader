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

Shader "Hidden/Mobile Bloom"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Eye ("Eye", int) = 0
        _FinalOffset("Bloom offset", Vector) = (0,0,0,0)
    }

    CGINCLUDE
    #pragma vertex vert
    #pragma fragment frag

    #include "UnityCG.cginc"
    #include "Assets/Shaders/Include/ColorSpace.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    float3 _FinalOffset;  // x, y are offsets - z is rotation
    int _BloomEye;
    int _Eye;
    float _BloomAmount;

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float2 uv : TEXCOORD0;
        float4 vertex : SV_POSITION;
        int eye : TEXCOORD1;
    };

    v2f vert (appdata v)
    {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        o.eye = unity_StereoEyeIndex;
        return o;
    }

    float3 Sample(float2 uv) {
        return tex2D(_MainTex, uv).rgb;
    }

    float3 SampleBox(float2 uv) {
        float4 o = _MainTex_TexelSize.xyxy * float2(-1, 1).xxyy;
        float3 s = Sample(uv + o.xy) + Sample(uv + o.zy)
                 + Sample(uv + o.xw) + Sample(uv + o.zw);
        return s * 0.25f;
    }

    float4 SampleHDR(float2 uv) {
        float4 c = tex2D(_MainTex, uv);

        if (c.a == 1) {
          return 0;
        }

        int3 asint = round(c.rgb * 255);
        asint = asint * (asint > 240);
        c.rgb = float3(asint & 7) / 7;
        c.a = exp2((1 - c.a) * 16);

        return c;
    }

    float4 SampleBoxHDR(float2 uv) {
        float4 o = _MainTex_TexelSize.xyxy * float2(-1, 1).xxyy;
        float4 s = SampleHDR(uv + o.xy) + SampleHDR(uv + o.zy)
                + SampleHDR(uv + o.xw) + SampleHDR(uv + o.zw);
        return s * 0.25;
    }
    ENDCG

    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass { // 0 Inital Pass - Multiplies through only by pixels with HDR components
            CGPROGRAM
            fixed3 frag (v2f i) : SV_Target
            {
                float4 col = SampleBoxHDR(i.uv);
                col.rgb *= col.a;

                return col.rgb;
            }
            ENDCG
        }

        Pass { // 1 Normal reduction pass
            Blend One Zero
            CGPROGRAM
            fixed3 frag (v2f i) : SV_Target
            {
                fixed3 col = SampleBox(i.uv);
                return col;
            }
            ENDCG
        }

        Pass { // 2 Normal enlargement pass
            Blend One One
            CGPROGRAM
            fixed3 frag (v2f i) : SV_Target
            {
                fixed3 col = SampleBox(i.uv);
                return col;
            }
            ENDCG
        }

        Pass { // 3 Final pass
            Blend One One
            CGPROGRAM
            fixed3 frag (v2f i) : SV_Target
            {
                float2 pos = i.uv;
                // 0.6 is a fudge factor - TODO : make it adjustable from C#
                pos = tan(atan(pos - 0.5) + _FinalOffset.xy * (1 - abs(_Eye -_BloomEye)) * 0.6) + 0.5;
                fixed3 col = SampleBox(pos) * _BloomAmount;
				// This applies a gradient fade near the edges of the texture so there's not a
				// hard line if the bloom texture is smaller than the screen.
				float xGradient = saturate((1.0 - (abs(pos.x - 0.5) * 2)) * 2.5);
				float yGradient = saturate((1.0 - (abs(pos.y - 0.5) * 2)) * 2.5);
				col *= min(xGradient, yGradient);
                return col;
            }
            ENDCG
        }
    }
}
