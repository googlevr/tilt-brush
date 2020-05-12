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

Shader "Custom/BorderSphere" {
  Properties {
    _MainColor ("MainColor", COLOR) = (1,1,1,1)
  }
  SubShader {
    Tags { "Queue"="Transparent" "RenderType"="Opaque" }
    CGPROGRAM
    #pragma surface surf Unlit alpha
    half4 LightingUnlit (SurfaceOutput s, half3 lightDir, half atten) {
          half4 c;
            c.rgb = s.Albedo;
            c.a = s.Alpha;
            return c;
        }
    struct Input {
      float2 uv_MainTex;
      float3 worldPos;
    };

    float3 _MainColor;
    uniform float3 _HighlightCenter;
    uniform float _HighlightRadius;

    void surf (Input IN, inout SurfaceOutput o) {
      //color according to distance from highlight center
      float fSafeRadius = max(_HighlightRadius, 0.0001);
      float fDistanceToHighlight = distance(_HighlightCenter, IN.worldPos);
      float fHighlightRatio = saturate(fDistanceToHighlight / fSafeRadius);

      o.Albedo.rgb = _MainColor;
      o.Alpha = lerp( 1.0, 0.0, fHighlightRatio );
    }
    ENDCG
  }
  FallBack "Diffuse"
}

