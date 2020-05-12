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

Shader "Custom/GrabHighlightUnmask" {
Properties {
}

//
// Stencil Mask Subshader
//
// REMOVES the outline stencil bit. Use for UI and other elements that need to render on top of selection outlines.
//
Category {
  SubShader {
  Tags {"Queue"="AlphaTest+30"}
  Pass {
    ZTest Always
    ZWrite Off
    ColorMask 0

    Stencil {
            Ref 0
            Comp always
            Pass replace
        }

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 3.0

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/ColorSpace.cginc"

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
      };

      struct v2f {
        float4 vertex : POSITION;
      };

      v2f vert (appdata_t v) {
        v2f o;
      o.vertex = UnityObjectToClipPos(v.vertex);
        return o;
      }

      void frag (v2f i, out fixed4 col : SV_Target) {
     col = float4(1,1,1,1);
      }
      ENDCG
      }
    }
  }
}
