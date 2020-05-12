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

Shader "Custom/ColorPicker_hs_l" {

Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _Slider01 ("Slider", Range(0,1)) = 0.5
}

CGINCLUDE
    #include "Assets/Shaders/Include/ColorSpace.cginc"
  #include "Assets/Shaders/Include/Hdr.cginc"
    float _Slider01;
    fixed4 _Color;

    struct appdata_t {
        float4 vertex : POSITION;
        float2 texcoord : TEXCOORD0;
        float4 tangent : TANGENT;
    };

    struct v2f {
        float2 texcoord : TEXCOORD0;
        float4 pos : POSITION;
    };
ENDCG

SubShader {
    Tags {
        "Queue"="AlphaTest+20"
        "IgnoreProjector"="True"
        "RenderType"="TransparentCutout"
    }

    Lighting Off
    Fog { Mode Off }
    LOD 100

    Pass {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag

        v2f vert (appdata_t v)
        {
            v2f o;
            v.vertex.z += v.vertex.z + 0.05;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.texcoord = v.texcoord;
            return o;
        }

        // Tilt Brush "circle" mode
        fixed4 frag (v2f i) : SV_Target
        {
            float2 rang = xy_to_polar(i.texcoord);
            clip(1 - rang.x);
            return encodeHdr(fixed4(0,0,0,0));
        }

        ENDCG
    }

    Pass {
        CGPROGRAM

        #pragma vertex vert
        #pragma fragment frag

        v2f vert (appdata_t v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.texcoord = v.texcoord;
            return o;
        }

        // Slider: Luminance. Polar.
        // Tilt Brush "circle" mode
        fixed4 frag (v2f i) : SV_Target
        {
            float2 rang = xy_to_polar(i.texcoord);
            clip(1 - rang.x);
            float3 base_rgb = hue33_to_base_rgb(rang.y * 6);
            return fixed4(sl_to_rgb(base_rgb, rang.x, _Slider01), 1) * _Color;
        }

        ENDCG
    }
}

}
