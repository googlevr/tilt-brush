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

Shader "Custom/ScrollingIcons" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _BaseDiffuseColor ("Base Diffuse Color", Color) = (.2,.2,.2,0)
    _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
    _IconsTex ("Icons Texture", 2D) = "white" {}
    _EmissionColor ("Emission Color", Color) = (1,1,1,1)
    _Ratio ("Scroll Ratio", Float) = 1
    _IconCount ("Icon Count", Float) = 1
    _UsedIconCount ("Used Icon Count", Float) = 1
  }
  SubShader {
    Tags{ "RenderType" = "Opaque" }
    LOD 100

    CGPROGRAM
    #pragma target 3.0
    #pragma surface surf Standard nofog

    sampler2D _MainTex;
    sampler2D _IconsTex;
    fixed4 _Color;
    fixed4 _BaseDiffuseColor;
    fixed4 _EmissionColor;
    float _Ratio;
    float _IconCount;
    float _UsedIconCount;

    struct Input {
      float2 uv_MainTex;
      float2 uv_IconsTex;
    };

    void surf (Input IN, inout SurfaceOutputStandard o) {
      fixed4 c = tex2D(_MainTex, IN.uv_MainTex);

      float2 scrolledUVs = IN.uv_IconsTex;
      scrolledUVs.x /= _IconCount;
      scrolledUVs.x += _Ratio * (_UsedIconCount / _IconCount);
      fixed4 icon = tex2D(_IconsTex, scrolledUVs);

      float4 finalColor = lerp(icon, c, c.w);
      c = finalColor;

      c *= _Color + _EmissionColor;
      o.Albedo = c + _BaseDiffuseColor;
      o.Emission = c * normalize(_EmissionColor);
      o.Alpha = 1;
    }
    ENDCG
  }
  FallBack "Diffuse"
}

