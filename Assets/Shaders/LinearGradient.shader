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

Shader "Custom/LinearGradient" {
    Properties
    {
        _ColorA ("ColorA", Color) = (0, 0, 0, 1)
        _ColorB ("ColorB", Color) = (1, 1, 1, 1)
        _GradientDirection("Gradient", Vector) = (0, 0, 1)
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct vertexIn {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 modelpos : TEXCOORD1;
            };

            v2f vert(vertexIn input)
            {
                v2f output;

                output.pos = UnityObjectToClipPos(input.pos);
                output.uv = input.uv;
                output.modelpos = input.pos;
                return output;
            }

            fixed4 _ColorA, _ColorB;
            float3 _GradientDirection;

            fixed4 frag(v2f input) : COLOR
            {
                float t = (dot(normalize(input.modelpos), _GradientDirection) + 1.0f) / 2.0f;
            return lerp(_ColorA, _ColorB, t);
            }
            ENDCG
        }

    }
}
