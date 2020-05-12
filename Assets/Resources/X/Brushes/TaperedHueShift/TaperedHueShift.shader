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

Shader "Brush/Special/TaperedHueShift" {
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
        #include "Assets/Shaders/Include/Brush.cginc"
		#include "Assets/Shaders/Include/ColorSpace.cginc"
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float _Cutoff;

        struct appdata_t {
            float4 vertex : POSITION;
            float2 texcoord : TEXCOORD0;
            float4 color : COLOR;
        };

        struct v2f {
            float4 vertex : POSITION;
            float2 texcoord : TEXCOORD0;
            float4 color : COLOR;
            UNITY_FOG_COORDS(1)
        };

        v2f vert (appdata_t v)
        {
            PrepForOds(v.vertex);

            v2f o;

            o.vertex = UnityObjectToClipPos(v.vertex);
            o.texcoord = v.texcoord;
            o.color = TbVertToNative(v.color);
            UNITY_TRANSFER_FOG(o, o.vertex);
            return o;
        }

        fixed4 frag (v2f i) : COLOR
        {
			UNITY_APPLY_FOG(i.fogCoord, i.color);
            fixed4 c = tex2D(_MainTex, i.texcoord) * i.color;

			// Hijack colorspace to make a hue shift..this is probably awful and technically wrong?
			float shift = 5;
			shift += i.color;
			float3 hueshift = hue06_to_base_rgb(i.color * shift);
			fixed4 _ColorShift = float4(hueshift, 1);

			// Discard transparent pixels
			if (c.a < _Cutoff) {
                discard;
            }

			c.a = 1;

			float huevignette = pow(abs(i.texcoord - .5) * 2.0, 2.0);
            return lerp(c, _ColorShift, saturate(huevignette));
        }

        ENDCG
    }
}

Fallback "Unlit/Diffuse"

}
