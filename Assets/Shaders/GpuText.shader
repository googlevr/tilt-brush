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

Shader "Unlit/GpuText"
{
	Properties
    {
        _MainTex ("Text Texture", 2D) = "white" {}
        _FontTex ("Font Texture", 2D) = "white" {}
        _Offset ("Font character offset", Float) = 32
        _FontWidth ("Font Characters across", Float) = 8
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
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
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            sampler2D _FontTex;
            float _Offset;
            float _FontWidth;
            float _Data[16];

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 RenderCharacterAtUv(float char, float2 inUv) {
                char = char - _Offset;
                char = clamp(char, 0, _FontWidth * _FontWidth - 1);

                float charLine = char / _FontWidth;
                float2 fontPos = float2(frac(charLine), floor(charLine) / _FontWidth);
                float2 charuv = frac(inUv * _MainTex_TexelSize.zw) / _FontWidth;

                return tex2D(_FontTex, fontPos + charuv);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 char = floor(tex2D(_MainTex, i.uv) * 255);

                if (char.g > 0) {
                    if (char.b == 0) {
                        char = 46; // decimal point
                    } else {
                        float dataIndex = char.g - 1;
                        float digit = char.b - 128;
                        float tens = pow(10, digit);
                        float data = _Data[dataIndex];
                        char = floor(fmod(floor(data / tens), 10)) + 48;
                    }
                }

                float4 col = RenderCharacterAtUv(char, i.uv);

                return col;
            }
            ENDCG
        }
    }
}
