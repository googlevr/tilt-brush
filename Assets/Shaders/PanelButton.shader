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

Shader "Custom/PanelButton" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Texture", 2D) = "white" {}
  }
  SubShader {
    Tags {"Queue"="AlphaTest+20"}

    Pass {
      Lighting Off

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"

      sampler2D _MainTex;
      fixed4 _Color;
      uniform float _Activated;
      uniform float _PanelMipmapBias;

      struct appdata_t {
        float4 vertex : POSITION;
        float2 texcoord : TEXCOORD0;
      };

      struct v2f {
        float4 vertex : POSITION;
        float4 texcoord : TEXCOORD0;
      };

      v2f vert (appdata_t v)
      {
        v2f o;
        if (_Activated) v.vertex.xyz += float3(0,0,-.2);
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = float4(v.texcoord.xy, 0, _PanelMipmapBias);
        return o;
      }

      fixed4 frag (v2f i) : COLOR
      {
        fixed4 c = tex2Dbias(_MainTex, i.texcoord);
        if( _Activated > 0.5f )
        {
          if( (abs(i.texcoord.y - .5f) < .425f) && (abs(i.texcoord.x - .5f) < .425f) )
          {
            c.rgb = .5 - c.rgb;
          }
          else
          {
            c.rgb = 1;
          }
        }
        else
        {
          c.rgb *= .75;
        }

        c.rgb *= _Color.rgb;

        return encodeHdr(saturate(c.rgb * c.a));
      }
      ENDCG
    }
  }
  FallBack "Diffuse"

}

