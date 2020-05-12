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

Shader "Custom/DefaultMaterialPreview" {
Properties {
  _Color ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
  _MainTex ("Particle Texture", 2D) = "white" {}
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend SrcAlpha One
  AlphaTest Greater .01
  ColorMask RGB
  Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_particles

      #include "UnityCG.cginc"

      sampler2D _MainTex;
      fixed4 _Color;

      struct appdata_t {
        float4 vertex : POSITION;
        float2 texcoord : TEXCOORD0;
      };

      struct v2f {
        float4 vertex : SV_POSITION;
        float2 texcoord : TEXCOORD0;
      };

      float4 _MainTex_ST;

      v2f vert (appdata_t v)
      {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        return o;
      }

      fixed4 frag (v2f i) : SV_Target
      {
        return 2 * _Color * tex2D(_MainTex, i.texcoord);
      }
      ENDCG
    }
  }
}
}
