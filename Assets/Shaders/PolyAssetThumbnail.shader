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

Shader "Custom/PolyAssetThumbnail" {
    Properties {
        _Color("Main Color", Color) = (1,1,1,1)
        _MainTex("Main Texture", 2D) = "white" {}
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.5
        _Grayscale("Grayscale", Float) = 0
    }
    SubShader {
        Tags{ "Queue" = "AlphaTest+20" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
        Pass {
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Assets/Shaders/Include/Hdr.cginc"

            fixed4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            uniform float _Cutoff;
            uniform float _Grayscale;
            uniform float _Ratio;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v) {

                v.uv -= 0.5;
                // Adjust UVs of 1:1 mesh for Poly's 4:3 image format
                v.uv.x /= 1.333;
                v.uv += 0.5;

                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                fixed4 c = tex2D(_MainTex, i.uv) * _Color;

                if (c.a < _Cutoff) discard;

                if (_Grayscale == 1) {
                    float grayscale = dot(c.rgb, float3(0.3, 0.59, 0.11));
                    return encodeHdr(grayscale);
                }

                return encodeHdr(c);
            }
            ENDCG
        }
    }
}
