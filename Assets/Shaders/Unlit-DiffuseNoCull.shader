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

Shader "Unlit/DiffuseNoCull" {
Properties {
  _Color ("Color", Color) = (1,1,1,1)
}

SubShader {
  Tags { "RenderType"="Opaque" }
  LOD 100

  Pass {
    Lighting Off
    Cull Off

    CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_fog
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Ods.cginc"

      struct appdata_t {
        float4 vertex : POSITION;
      };

      struct v2f {
        float4 vertex : SV_POSITION;
        UNITY_FOG_COORDS(1)
      };

      uniform float4 _Color;

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        UNITY_TRANSFER_FOG(o,o.vertex);
        return o;
      }

      fixed4 frag (v2f i) : SV_Target
      {
        fixed4 col = _Color;
        UNITY_APPLY_FOG(i.fogCoord, col);
        return col;
      }
    ENDCG
  }
}

}
