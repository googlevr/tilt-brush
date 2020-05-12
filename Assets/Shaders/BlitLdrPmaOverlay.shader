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

// Clamps input to LDR (so alpha blending works better)
// alpha-composites premultiplied-alpha overlay
// then composites a premultiplied-alpha texture onto
// the source.
Shader "Custom/BlitLdrPmaOverlay" {
  Properties {
    _MainTex ("", 2D) = "white" {}
    _OverlayTex ("Overlay Texture", 2D) = "black" {}
    _OverlayUvRange  ("Overlay UV Range", Vector) = (0, 0, 1, 1)
  }

  SubShader {
    ZTest Off Cull Off ZWrite Off Fog { Mode Off }
    Blend Off

    Pass{
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "UnityCG.cginc"

      struct v2f {
        float4 pos : POSITION;
        float2 uv : TEXCOORD0;
      };

      v2f vert(appdata_img v) {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = MultiplyUV(UNITY_MATRIX_TEXTURE0, v.texcoord.xy);
        return o;
      }

      sampler2D _MainTex;
      sampler2D _OverlayTex;
      float4 _OverlayUvRange;

      float4 frag(v2f i) : COLOR {
        // Get the original color.
        float4 mainTex = tex2D(_MainTex, i.uv);

        // Calculate the overlay's texture coordinates.
        float2 uvMin = _OverlayUvRange.xy;
        float2 uvMax = _OverlayUvRange.zw;
        float2 uvSize = uvMax - uvMin;
        float2 overlayUV = saturate((i.uv - uvMin) / uvSize);

        // Get the overlay color.
        float4 overlayTex = tex2D(_OverlayTex, overlayUV);

        // Composite the result.
        return (1.0f - overlayTex.a) * saturate(mainTex) + overlayTex;
      }
      ENDCG
    }
  }
}
