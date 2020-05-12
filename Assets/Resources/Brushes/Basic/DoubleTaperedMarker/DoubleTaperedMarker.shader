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

Shader "Brush/Special/DoubleTaperedMarker" {
Properties {
}

Category {
  Cull Off Lighting Off

  SubShader {
    Tags{ "DisableBatching" = "True" }
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 3.0
      #pragma multi_compile_particles
      #pragma multi_compile_fog
      #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #pragma multi_compile __ SELECTION_ON

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"
      #include "Assets/Shaders/Include/MobileSelection.cginc"

      sampler2D _MainTex;

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float2 texcoord0 : TEXCOORD0;
        float3 texcoord1 : TEXCOORD1; //per vert offset vector
      };

      struct v2f {
        float4 pos : POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        UNITY_FOG_COORDS(1)
      };

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);

        //
        // XXX - THIS SHADER SHOULD BE DELETED AFTER TAPERING IS DONE IN THE GEOMETRY GENERATION
        //

        v2f o;
        float envelope = sin(v.texcoord0.x * 3.14159);
        float widthMultiplier = 1 - envelope;
        v.vertex.xyz += -v.texcoord1 * widthMultiplier;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.color = TbVertToNative(v.color);
        o.texcoord = v.texcoord0;
        UNITY_TRANSFER_FOG(o, o.pos);
        return o;
      }

      fixed4 frag (v2f i) : COLOR
      {
        UNITY_APPLY_FOG(i.fogCoord, i.color.rgb);
        float4 color = float4(i.color.rgb, 1);
        FRAG_MOBILESELECT(color)
        return color;
      }

      ENDCG
    }
  }
}
}
