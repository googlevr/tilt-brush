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

Shader "Custom/AudioReactiveBrushIcon" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Texture", 2D) = "white" {}
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  }
  SubShader {
    Pass {
      Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
      Lighting Off

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Assets/Shaders/Include/Brush.cginc"

      sampler2D _MainTex;
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
        v.vertex.xyz += v.vertex.xyz * _BeatOutput.x * .1;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = v.texcoord;
        return o;
      }

      fixed4 frag (v2f i) : COLOR
      {
        fixed4 c = tex2D(_MainTex, i.texcoord);
        c.rgb *= .75; // Hold over from the intensity modulation that happens with panel buttons
        c.rgb *= _Color.rgb;
        if (c.a < _Cutoff) discard;
        return c;
      }
      ENDCG
    }
  }
  FallBack "Diffuse"

}

