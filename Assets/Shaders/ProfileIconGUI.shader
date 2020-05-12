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

Shader "Custom/ProfileIconGUI" {
    Properties {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Avatar Texture", 2D) = "white" {}
        _AlphaMask ("Mask", 2D) = "white" {}
    }
    SubShader {
    Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"
    "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True"}
        LOD 200

    Cull Off
    Lighting Off
    ZWrite Off
    Blend One OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Lambert alpha nofog
        #pragma target 3.0
    #include "UnityCG.cginc"

        sampler2D _MainTex;
        sampler2D _AlphaMask;

        struct Input {
            float2 uv_MainTex;
            float2 uv_AlphaMask;
        };

        fixed4 _Color;

        void surf (Input IN, inout SurfaceOutput o) {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Emission = c.rgb;
            o.Alpha = tex2D (_AlphaMask, IN.uv_AlphaMask).a;
        }
        ENDCG
    }
    FallBack "Unlit/Diffuse"
}
