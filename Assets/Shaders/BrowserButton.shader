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

Shader "Custom/BrowserButton" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Texture", 2D) = "white" {}
    _Grayscale ("Grayscale", Float) = 0
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
      float _Grayscale;

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
        if (_Activated) v.vertex.xyz += float3(0,0,-0.1);
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = v.texcoord;
        return o;
      }

      fixed4 frag (v2f i) : COLOR
      {
        fixed4 c = tex2D(_MainTex, i.texcoord);
        if( _Activated > 0.5f )
        {
            c.rgb = .75 - c.rgb;
        }
        else
        {
          c.rgb *= .75;
        }

        c.rgb *= _Color.rgb;

        if (_Grayscale == 1) {
            float grayscale = dot(c.rgb, float3(0.3, 0.59, 0.11));
            return grayscale;
        }

        return encodeHdr(saturate(c.rgb * c.a));
      }
      ENDCG
    }
  }
  FallBack "Diffuse"

}

