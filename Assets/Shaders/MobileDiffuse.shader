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

Shader "TiltBrush/MobileDiffuse" {
  Properties {
    _Color ("Color", Color) = (1,1,1,1)
    _MainTex("Albedo (RGB)", 2D) = "black" {}
    _LightMap("LightMap (RGB)", 2D) = "white" {}
  }

  SubShader {
    Tags { "RenderType"="Opaque" "LightMode"="ForwardBase"}
    LOD 100

    Pass {
      Tags{ "LightMode" = "ForwardBase" }

      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 3.0
        #pragma multi_compile_fog

        #include "UnityCG.cginc"
        #include "Lighting.cginc"

        // Disable all the things.
        // For details, see:
        // https://docs.unity3d.com/560/Documentation/Manual/SL-VertexFragmentShaderExamples.html
        #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
        #include "AutoLight.cginc"

        struct appdata_t {
          float4 vertex : POSITION;
          float2 uv0 : TEXCOORD0;
          float2 uv1 : TEXCOORD1;
          half3 normal : NORMAL;
          float4 color : COLOR;
        };

        struct v2f {
          float4 pos : SV_POSITION;  // Required to be called "pos" by shadow reciever.
          float2 uv0 : TEXCOORD0;
          float2 uv1 : TEXCOORD1;
          half3 worldNormal : NORMAL;
          UNITY_FOG_COORDS(2)
          SHADOW_COORDS(3) // put shadows data into TEXCOORD3
        };

        sampler2D _MainTex;
        uniform float4 _MainTex_ST;
        uniform float4 _LightMap_ST;
        sampler2D _LightMap;
        float4 _Color;

        v2f vert (appdata_t v) {
          v2f o;

          o.pos = UnityObjectToClipPos(v.vertex);
          o.uv0 = TRANSFORM_TEX(v.uv0, _MainTex);
          o.uv1 = TRANSFORM_TEX(v.uv1, _LightMap);
          o.worldNormal = UnityObjectToWorldNormal(v.normal);

          UNITY_TRANSFER_FOG(o, o.pos);
          TRANSFER_SHADOW(o);
          return o;
        }

        fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target {
          fixed4 albedo = tex2D(_MainTex, i.uv0) * _Color;
          fixed4 lightMap = tex2D(_LightMap, i.uv1);
          fixed shadow = SHADOW_ATTENUATION(i);

          half3 worldNormal = normalize(i.worldNormal * facing);

          fixed ndotl = saturate(dot(worldNormal, normalize(_WorldSpaceLightPos0.xyz)));
          fixed3 lighting = ndotl * _LightColor0 * shadow;
          lighting += ShadeSH9(half4(worldNormal, 1.0));

          // Pass lighting + baked lightmap to diffuse
          float4 finalColor = albedo;

          finalColor.rgb *= lightMap * lighting;

          UNITY_APPLY_FOG(i.fogCoord, finalColor);
          return finalColor;
        }
      ENDCG
    } // pass
  } // subshader

Fallback "Mobile/VertexLit"
}
