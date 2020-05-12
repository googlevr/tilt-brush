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

Shader "Custom/PinCushionItem" {
    Properties {
        _ActivatedColor("Main Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader {
        Tags {"Queue"="AlphaTest+20"}

        Pass {
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
            #include "Assets/Shaders/Include/Brush.cginc"
            #include "Assets/Shaders/Include/Hdr.cginc"

            sampler2D _MainTex;
            fixed4 _ActivatedColor;
            uniform float _Activated;
            float _FlattenAmount;

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert (appdata_t v)
            {
                v2f o;

                // Smash inward along Z axis based on 0-1 ratio
                v.vertex.z = v.vertex.z * _Activated - v.vertex.z;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : COLOR
            {
                fixed4 c = tex2D(_MainTex, i.texcoord);

                if (_Activated < 0.5f) {

                    // Holy hover state Batman! a little "pick me!"
                    // animation by scaling the UVs ever so slightly.
                    // Numbers tuned to taste -- we want this to be subtle.
                    float t = sin(_Time.w * 3);
                    float scale = lerp(1, .99, t);
                    float2 scaleUV = i.texcoord;
                    scaleUV -= 0.5;
                    scaleUV *= scale;
                    scaleUV += 0.5;
                    fixed4 finalColor = tex2D(_MainTex, scaleUV);

                    // Pass in brush color (ultimately set on the CPU)
                    finalColor.rgb += _ActivatedColor * 0.75;
					finalColor.rgb = saturate(finalColor.rgb);
                    return encodeHdr(finalColor);
                }

                else {
                    c.rgb *= 0.4;
					c.rgb = saturate(c.rgb);
                    return encodeHdr(c.rgb);
                }
            }
            ENDCG
        }
    }
    FallBack "Transparent/Cutout/VertexLit"
}

