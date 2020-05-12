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

Shader "Brush/Special/Wireframe" {
Properties {
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One
  AlphaTest Greater .01
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ ODS_RENDER
      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float3 normal : NORMAL;
        float2 texcoord : TEXCOORD0;
      };

      struct v2f {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
      };

      float4 _MainTex_ST;

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);

        v2f o;

        o.texcoord = v.texcoord;
        o.color = v.color;
        o.vertex = UnityObjectToClipPos(v.vertex);
        return o;
      }

      fixed4 frag (v2f i) : COLOR
      {

        half w = 0;
#ifdef AUDIO_REACTIVE
        float waveform = (tex2D(_WaveFormTex, float2(i.texcoord.y,0)).r - .5f);
        float envelope = sin(i.texcoord.y * 3.141569);
        i.texcoord.x += waveform * envelope;
        w = ( abs(i.texcoord.x - .5) > .5) ? 1 : 0;
#else
        w = ( abs(i.texcoord.x - .5) > .45) ? 1 : 0;
        w += ( abs(i.texcoord.y - .5) > .45) ? 1 : 0;
#endif
        //float angle = atan2(i.texcoord.x, i.texcoord.y);
        //w += ( abs(angle - (3.14/4.0)) < .05) ? 1 : 0;
        return i.color * w;
      }
      ENDCG
    }
  }
}
}
