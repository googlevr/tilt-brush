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

Shader "Brush/Intro/Bloom" {
Properties {
  _MainTex ("Particle Texture", 2D) = "white" {}
  _EmissionGain ("Emission Gain", Range(0, 1)) = 0.5
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One // SrcAlpha One
  BlendOp Add, Min
  AlphaTest Greater .01
  ColorMask RGBA
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_particles
      #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"

      sampler2D _MainTex;
      float4 _MainTex_ST;
      float _EmissionGain;
      half _IntroDissolve;

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
      };

      struct v2f {
        float4 vertex : POSITION;
        float4 color : COLOR;
        float2 texcoord : TEXCOORD0;
      };

      v2f vert (appdata_t v)
      {
        v.color = TbVertToSrgb(v.color);
        v2f o;
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        o.color = bloomColor(v.color, _EmissionGain) * (1.0 - _IntroDissolve);
        o.vertex = UnityObjectToClipPos(v.vertex);
        return o;
      }

      fixed4 frag (v2f i) : COLOR
      {
        float4 color = i.color * tex2D(_MainTex, i.texcoord);
        color = float4(color.rgb * color.a, 1.0);
        color = SrgbToNative(color);
        return encodeHdr(color.rgb);
      }

      ENDCG
    }
  }
}
}
