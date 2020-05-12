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

Shader "Custom/StandardforIcons" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _EmissionColor ("Emission Color", Color) = (1,1,1,1)
  _Shininess ("Smoothness", Range (0.01, 1)) = 0.013
  _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}

}
    SubShader {
    Tags{ "RenderType" = "Opaque" }
    LOD 100

    CGPROGRAM
    #pragma target 3.0
    #pragma surface surf Standard nofog

    struct Input {
      float2 uv_MainTex;
    };

    sampler2D _MainTex;
    fixed4 _Color;
    fixed4 _EmissionColor;
    half _Shininess;

    void surf(Input IN, inout SurfaceOutputStandard o) {
      fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
      o.Albedo = tex.rgb * _Color.rgb;
      o.Smoothness = _Shininess;

      float4 c = tex * _EmissionColor;
      o.Emission = c.rgb;
    }
      ENDCG
    }

  FallBack "Diffuse"
}
