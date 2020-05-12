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

Shader "Custom/TransformLine" {
Properties {
  _Color ("Color", Color) = (0, 1, 1, 1)
}

Category {
  SubShader {
  Pass {

      Cull Front

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
      };

    uniform float4 _Color;
    uniform float _Intensity;

      v2f vert (appdata_t v) {
        v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
        return o;
      }

      fixed4 frag (v2f i) : COLOR {
     _Color += .2 + abs(sin(_Time.w))*.1;
     return float4(_Color.xyz,1);

      }
      ENDCG
    }
  }
}
}
