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

Shader "Brush/Special/Faceted" {
Properties {
  _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
}

SubShader {
  Cull Back
  Pass{
    CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    #pragma target 3.0
    #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

    #include "UnityCG.cginc"
    #include "Assets/Shaders/Include/Brush.cginc"
    #include "Assets/ThirdParty/Shaders/Noise.cginc"
    sampler2D _MainTex;
    float4 _MainTex_ST;

    struct appdata_t {
      float4 vertex : POSITION;
      fixed4 color : COLOR;
      float3 normal : NORMAL;
      float2 texcoord : TEXCOORD0;
    };

    struct v2f {
      float4 vertex : SV_POSITION;
      fixed4 color : COLOR;
      float2 texcoord : TEXCOORD0;
      float3 worldPos : TEXCOORD1;
    };

    v2f vert (appdata_t v)
    {
      PrepForOds(v.vertex);
      v2f o;
      o.vertex = UnityObjectToClipPos(v.vertex);
        o.color = v.color;
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
      return o;
    }

    fixed4 frag (v2f i) : SV_Target
    {
      float3 n = normalize(cross(ddy(i.worldPos), ddx(i.worldPos)));
      i.color.xyz = n.xyz;
      return i.color;
    }

    ENDCG
    }
  }
Fallback "Diffuse"
}
