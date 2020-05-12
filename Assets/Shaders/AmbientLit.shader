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

Shader "Custom/AmbientLit" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Base (RGB)", 2D) = "white" {}
  }
  SubShader {
    LOD 100

    CGPROGRAM
    #pragma surface surf NonDirectional

    sampler2D _MainTex;
    fixed _Cutoff;
    fixed4 _Color;

    struct Input {
      float2 uv_MainTex;
    };

    half4 LightingNonDirectional (SurfaceOutput s, half3 lightDir, half atten) {
              half NdotL = .5;
              half4 c;
              c.rgb = s.Albedo * _LightColor0.rgb * (NdotL * atten * 2);

              c.a = s.Alpha;
              return c;
          }

    void surf (Input IN, inout SurfaceOutput o) {
      half4 tex = tex2D (_MainTex, IN.uv_MainTex);
      fixed4 c = tex * _Color;
      o.Emission = 0.0;
      o.Albedo = c.rgb;
      o.Alpha = 1.0;
    }
    ENDCG
  }
  FallBack "Diffuse"
}
