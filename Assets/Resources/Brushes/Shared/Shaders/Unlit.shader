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

Shader "Brush/Special/Unlit" {

Properties {
    _MainTex ("Texture", 2D) = "white" {}
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
}

SubShader {
    Pass {
        Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
        Lighting Off
        Cull Off

        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
        #pragma multi_compile_fog
        #pragma multi_compile __ SELECTION_ON
        #include "Assets/Shaders/Include/Brush.cginc"
        #include "UnityCG.cginc"
        #include "Assets/Shaders/Include/MobileSelection.cginc"

        sampler2D _MainTex;
        float _Cutoff;



        struct appdata_t {
            float4 vertex : POSITION;
            float2 texcoord : TEXCOORD0;
            float4 color : COLOR;
        };

        struct v2f {
            float4 pos : POSITION;
            float2 texcoord : TEXCOORD0;
            float4 color : COLOR;
            UNITY_FOG_COORDS(1)
        };

        v2f vert (appdata_t v)
        {
            PrepForOds(v.vertex);

            v2f o;

            o.pos = UnityObjectToClipPos(v.vertex);
            o.texcoord = v.texcoord;
            o.color = TbVertToNative(v.color);
            UNITY_TRANSFER_FOG(o, o.pos);
            return o;
        }

        fixed4 frag (v2f i) : COLOR
        {
            fixed4 c;
            UNITY_APPLY_FOG(i.fogCoord, i.color);
            c = tex2D(_MainTex, i.texcoord) * i.color;
            if (c.a < _Cutoff) {
                discard;
            }
            c.a = 1;
            FRAG_MOBILESELECT(c)
            return c;
        }

        ENDCG
    }
}

Fallback "Unlit/Diffuse"

}
