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

Shader "Custom/AnimalRuler" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Texture", 2D) = "white" {}
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    _Saturation ("Saturation", Range(0,1)) = 1.0
  }
  SubShader {
    Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
    LOD 100
    Cull Off
    CGPROGRAM
    #pragma surface surf Lambert nofog

    sampler2D _MainTex;
    fixed4 _Color;
    float _Saturation;
    fixed _Cutoff;

    struct Input {
      float2 uv_MainTex;
      float3 worldPos;
    };

    void surf (Input IN, inout SurfaceOutput o) {
      fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
      c *= lerp(1.0f, _Color + .25f, _Saturation);
      o.Emission = c.rgb;
      if (c.a < _Cutoff) {
        discard;
      }
      o.Alpha = 1;
    }
    ENDCG
  }
  FallBack "Transparent/Cutout/VertexLit"
}


