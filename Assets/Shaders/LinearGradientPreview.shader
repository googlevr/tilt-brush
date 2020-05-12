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

Shader "Custom/LinearGradientPreview" {
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
  _ColorA ("ColorA", Color) = (0, 0, 0, 1)
        _ColorB ("ColorB", Color) = (1, 1, 1, 1)
        _GradientDirection("Gradient", Vector) = (0, 0, 1)
        _OutlineWidth("Outline Width", Range(0.001,1)) = 0.02
  _SecondOutlineWidth("Outline Width", Range(0.001,1)) = 0.02
        _EquatorWidth("Equator Width", Range(0.001,1)) = 0.008
    }

    SubShader
    {
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
      Cull Front
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

            fixed4 _ColorA, _ColorB, _Color;
            float3 _GradientDirection;
            float _EquatorWidth;

            fixed4 frag(v2f input) : COLOR
            {
                float t;
                t = input.uv.y;
                if (abs(t - 0.5) < _EquatorWidth) return _Color;
    return lerp(_ColorA, _ColorB, t);
            }
            ENDCG
        }

        //Make a white outline!
        Pass{

            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _OutlineWidth;
      fixed4 _Color;

            struct appdata_t {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata_t v) {
                v2f o = (v2f)0;
                o.pos = UnityObjectToClipPos(float4(v.vertex.xyz + v.normal*_OutlineWidth,1));
                return o;
            }

            float4 frag(v2f i) : COLOR{
                return fixed4(_Color);
            }
                ENDCG
        }
  //Make a 2nd black outline!
  Pass{

      Cull Front

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "UnityCG.cginc"

      float _SecondOutlineWidth;

      struct appdata_t {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
      };

      struct v2f {
    float4 pos : SV_POSITION;
      };

      v2f vert(appdata_t v) {
    v2f o = (v2f)0;
    o.pos = UnityObjectToClipPos(float4(v.vertex.xyz + v.normal*_SecondOutlineWidth,1));
    return o;
      }

      float4 frag(v2f i) : COLOR{
    return fixed4(float3(0,0,0),1);
      }
    ENDCG
  }

    }
}
