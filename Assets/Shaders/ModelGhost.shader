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

Shader "Custom/ModelGhost" {
Properties {
  _Color ("Color", Color) = (1, 1, 1, 1)
}

Category {
  SubShader {

  Tags{ "Queue" = "Overlay" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

  Pass {

    //Blend SrcAlpha OneMinusSrcAlpha, Zero One
    Blend One One
       Lighting Off Cull Back ZTest Always ZWrite Off Fog { Mode Off }


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
        float3 normal : NORMAL;
      };

      struct v2f {
        float4 vertex : POSITION;
        float3 viewDir : TEXCOORD0;
    float3 normal : NORMAL;

      };

    uniform float4 _Color;

      v2f vert (appdata_t v) {
        v2f o;
      o.vertex = UnityObjectToClipPos(v.vertex);
        o.viewDir = normalize(ObjSpaceViewDir(v.vertex));
    o.normal = normalize(v.normal);
        return o;
      }

      fixed4 frag (v2f i) : COLOR {
         float facingRatio = saturate(dot(i.viewDir, i.normal));
     facingRatio = 1-facingRatio;
     float4 outColor = _Color * facingRatio;
     outColor.w = 1;
     return outColor;

    }
      ENDCG
    }
  }
}

Fallback "Unlit/Diffuse"


}
