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

Shader "Custom/LightWidget" {
    Properties {
        _TrueColor ("Color", Color) = (0,0,0,0)
    _Color ("Outline Color", Color) = (0,0,0,0)
        _ClampedColor ("Clamped Color", Color) = (1,1,1,1)
        _OutlineWidth ("White Outline Width", Float ) = 0.02
    _SecondOutlineWidth ("Black Outline Width", Float) = 0.03
        _FlattenAmount ("Flatten Amount", Range(0,1)) = 0
    }
    SubShader {

        Tags {"RenderType"="Opaque"}

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"
            #pragma target 3.0

            uniform float4 _ClampedColor;
            uniform float4 _TrueColor;
      uniform float4 _Color;
            float _FlattenAmount;

            struct Input {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            struct v2f {
                float4 pos : SV_POSITION;
                float4 posWorld : TEXCOORD0;
                float3 normalDir : TEXCOORD1;
            };
            v2f vert (Input v) {
                v2f o = (v2f)0;

                // Smash along the z axis based on a 0-1 ratio
                v.vertex.z = v.vertex.z - v.vertex.z * _FlattenAmount;

                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex );
                return o;
            }

            float4 frag(v2f i) : COLOR {

                i.normalDir = normalize(i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 normalDirection = i.normalDir;

                // Create a fresnel of the clamped HSV->RGB color which never blows out to white, mixed with the raw light color
                float3 finalColor = lerp(saturate(_TrueColor.rgb),_ClampedColor.rgb, pow(1.0 - max(0, dot(i.normalDir, viewDirection)), 1));
                return float4 (finalColor,1);

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
      #include "Assets/Shaders/Include/Hdr.cginc"

            float _OutlineWidth;
      uniform float4 _Color;
      float _FlattenAmount;

            struct appdata_t {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata_t v) {
                v2f o = (v2f)0;
        v.vertex.z = v.vertex.z - v.vertex.z * _FlattenAmount;

                o.pos = UnityObjectToClipPos(float4(v.vertex.xyz + v.normal*_OutlineWidth,1));
                return o;
            }

            float4 frag(v2f i) : COLOR{
                return encodeHdr(_Color.rgb);
            }
                ENDCG
        }

        // Make a black outline!

        Pass{

            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #pragma target 3.0

            uniform float _SecondOutlineWidth;
            float _FlattenAmount;

            struct Input {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            struct v2f {
                float4 pos : SV_POSITION;
            };
            v2f vert(Input v) {
                v2f o = (v2f)0;

                // Smash along the z axis based on a 0-1 ratio
                v.vertex.z = v.vertex.z - v.vertex.z * _FlattenAmount;

                o.pos = UnityObjectToClipPos(float4(v.vertex.xyz + v.normal*_SecondOutlineWidth,1));
                return o;
            }
            float4 frag(v2f i) : COLOR{

                return fixed4(float3(0,0,0),1);
            }
                ENDCG
         }
    }
    FallBack "Diffuse"
}
