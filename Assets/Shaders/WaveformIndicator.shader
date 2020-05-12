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

Shader "Custom/WaveformIndicator" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    _BGColor("BG Color", Color) = (0, 0, 0, 1)
  }
  SubShader {
    Pass {
      Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
      Lighting Off

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile __ AUDIO_REACTIVE
      #include "Assets/Shaders/Include/Brush.cginc"

      fixed4 _Color;
      fixed4 _BGColor;
      uniform float _Activated;
      float _Cutoff;

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
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = v.texcoord;
        return o;
      }

      fixed4 frag (v2f i) : COLOR
      {
        fixed4 c = 0;

        float envelope = sin(i.texcoord.x * 3.14159);
#ifdef AUDIO_REACTIVE
        // real waveform line
        float waveform = (tex2D(_WaveFormTex, float2(i.texcoord.x * .2f,0)).r - .5f);
        float procedural_line = saturate(3 - 40*abs(i.texcoord.y - .5 + waveform * envelope));
#else
        // generated waveform line
        float waveform = .1 * sin( -10 * _Time.z + i.texcoord.x * 6);
        waveform += .1 * sin( -4 * _Time.z + i.texcoord.x * 3);
        float procedural_line = saturate(3 - 40*abs(i.texcoord.y - .5 + waveform * envelope));
#endif
        c = lerp(_Color, _BGColor, 1.0f - procedural_line);
        if (c.a < _Cutoff) discard;
        return c;
      }
      ENDCG
    }
  }
  FallBack "Diffuse"

}

