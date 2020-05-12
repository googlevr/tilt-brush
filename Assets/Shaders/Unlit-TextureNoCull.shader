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

Shader "Unlit/TextureNoCull" {
Properties {
  _MainTex ("Base (RGB)", 2D) = "white" {}
}

SubShader {
  Tags { "RenderType"="Opaque" }
  LOD 100

  Pass {
    Cull Off
    CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      #include "UnityCG.cginc"

      struct appdata_t {
        float4 vertex : POSITION;
        float2 texcoord : TEXCOORD0;
      };

      struct v2f {
        float4 vertex : SV_POSITION;
        float2 texcoord : TEXCOORD0;
      };

      sampler2D _MainTex;
      float4 _MainTex_ST;

      v2f vert (appdata_t v)
      {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

        return o;
      }

      fixed4 frag (v2f i) : SV_Target
      {
        return tex2D (_MainTex, i.texcoord.xy);
      }
    ENDCG
  }
}

}
