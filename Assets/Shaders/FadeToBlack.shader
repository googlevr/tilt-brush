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

Shader "Custom/FadeToBlack" {
  Properties {
    _MainColor ("MainColor", COLOR) = (1,1,1,1)
      _FadeStart ("Fade Start", Float) = 0.0
      _FadeEnd ("Fade End", Float) = 0.0
  }
  SubShader {
    Tags { "Queue"="Geometry" "RenderType"="Geometry" }
    CGPROGRAM
    #pragma surface surf Unlit
    #pragma target 3.0
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
    float _FadeStart;
    float _FadeEnd;

    void surf (Input IN, inout SurfaceOutput o) {
      //color according to Y height
      float fNormalizedY = max( abs(IN.worldPos.y) - _FadeStart, 0 );
      fNormalizedY /= (_FadeEnd - _FadeStart);
      fNormalizedY = clamp(fNormalizedY, 0, 1);

      float3 _black = float3(0, 0, 0);
      o.Albedo.rgb = lerp( _MainColor, _black, fNormalizedY );
    }
    ENDCG
  }
  FallBack "Diffuse"
}

