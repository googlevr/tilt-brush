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

Shader "Custom/AmbientGrid" {
Properties {
  _Color ("Color", Color) = (0.5,0.5,0.5,0.5)
  _NearFadeDistanceStart ("Near Fade Distance Start", Float) = 1
  _NearFadeDistanceEnd ("Near Fade Distance End", Float) = 1
  _FarFadeDistanceStart ("Far Fade Distance Start", Float) = 1
  _FarFadeDistanceEnd ("Far Fade Distance End", Float) = 1
  }

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One
  AlphaTest Greater .01
  ColorMask RGB
  Cull Off Lighting Off ZWrite Off


  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_particles
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Ods.cginc"

      fixed4 _Color;
      float _NearFadeDistanceStart;
      float _NearFadeDistanceEnd;
      float _FarFadeDistanceStart;
      float _FarFadeDistanceEnd;

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
      };

      struct v2f {
        float4 vertex : SV_POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        float viewdist : TEXCOORD1;
      };

      float4 _MainTex_ST;
      float3 _WorldSpaceRootCameraPosition;

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);
        v2f o;

        o.vertex = UnityObjectToClipPos(v.vertex);

         o.color = v.color;
         float3 rootCameraWorld = _WorldSpaceRootCameraPosition;
         float3 vertPosWorld = mul(unity_ObjectToWorld, v.vertex);

         o.viewdist = length(rootCameraWorld - vertPosWorld);

         o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        return o;
      }

      fixed4 frag (v2f i) : SV_Target
      {
        float4 outColor = _Color;
        outColor *= smoothstep(_NearFadeDistanceStart, _NearFadeDistanceEnd, i.viewdist);
        outColor *= smoothstep(_FarFadeDistanceEnd, _FarFadeDistanceStart, i.viewdist);
        return outColor;
      }
      ENDCG
    }
  }
}
}
