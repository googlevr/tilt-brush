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

Shader "Custom/AudioReactiveIcon" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Texture", 2D) = "white" {}
    _ActivatedTex ("Texture", 2D) = "white" {}
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  }
  SubShader {
    Pass {
      Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
      Lighting Off

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 3.0
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"

      sampler2D _MainTex;
      sampler2D _ActivatedTex;
      fixed4 _Color;
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
#ifdef AUDIO_REACTIVE
        v.vertex.xyz += v.vertex.xyz * .2;
        v.vertex.z += -.2;
#endif
        o.vertex = UnityObjectToClipPos(v.vertex);

        o.texcoord = v.texcoord;
        return o;
      }

      fixed4 frag (v2f i) : COLOR
      {
        fixed4 c;
        if( _Activated > 0.5f )
        {


          float envelope = sin(i.texcoord.x * 3.14159);
#ifdef AUDIO_REACTIVE
          // real waveform line
          float waveform = (tex2D(_WaveFormTex, float2(i.texcoord.x * .2f,0)).r - .5f) * .5;
          float procedural_line = saturate(2 - 40*abs(i.texcoord.y - .5 + waveform * envelope));
          c = tex2D(_ActivatedTex, i.texcoord);
          c.rgb = max(c.rgb, procedural_line);
          // When audio reactive mode is locked in, invert colors to show button is fully activated.
          c.rgb = .4 - c.rgb;
          c = saturate(c);

#else
          // generated waveform line
          float waveform = .1 * sin( -10 * _Time.z + i.texcoord.x * 300  * .02);
          waveform += .1 * sin( -4 * _Time.z + i.texcoord.x * 300 * .01);
          float procedural_line = saturate(1 - 40*abs(i.texcoord.y - .5 + waveform * envelope));
          c = tex2D(_ActivatedTex, i.texcoord);
          c.rgb = max(c.rgb, procedural_line);

#endif
        }
        else
        {
          c = tex2D(_MainTex, i.texcoord);
          c.rgb *= .75;
        }

        c.rgb *= _Color.rgb;
        if (c.a < _Cutoff) discard;
        return encodeHdr(c.rgb * c.a);
      }
      ENDCG
    }
  }
  FallBack "Transparent/Cutout/VertexLit"
}

