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

Shader "Brush/Visualizer/WaveformTube" {
Properties {
  _MainTex ("Particle Texture", 2D) = "white" {}
  _EmissionGain ("Emission Gain", Range(0, 1)) = 0.5
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One //SrcAlpha One
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
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"

      sampler2D _MainTex;
      float4 _MainTex_ST;
      float _EmissionGain;

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
        PrepForOds(v.vertex);

        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

        o.color = bloomColor(v.color, _EmissionGain);
        return o;
      }

      fixed4 frag (v2f i) : COLOR
      {
        i.texcoord.x -= _BeatOutputAccum.x;
        i.texcoord.y += i.texcoord.x;
        i.texcoord.x *= .25;

        float wav = (tex2D(_WaveFormTex, float2(i.texcoord.x,0)).r - .5f);
        i.texcoord.y += wav;
        float4 c = i.color * tex2D(_MainTex, i.texcoord);
        return encodeHdr(c.rgb * c.a);
      }
      ENDCG
    }
  }
}
}
