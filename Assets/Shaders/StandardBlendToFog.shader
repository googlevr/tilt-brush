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

Shader "Custom/StandardBlendToFog" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
  _Shininess ("Shininess", Range (0.01, 1)) = 0.078125
  _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
  _BumpMap ("Normalmap", 2D) = "bump" {}
  _WorldSpaceFogRange("Fog Range", Vector) = (75.0, 100.0, 0)

}
    SubShader {
    Tags {"IgnoreProjector"="True"}
    LOD 200

    CGPROGRAM
    #pragma target 3.0
    #pragma surface surf StandardSpecular

    struct Input {
      float2 uv_MainTex;
      float2 uv_BumpMap;
      float3 worldPos;
    };

    sampler2D _MainTex;
    sampler2D _BumpMap;
    fixed4 _Color;
    half _Shininess;
    half2 _WorldSpaceFogRange;

    void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
      fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
      o.Albedo = tex.rgb * _Color.rgb;
      o.Smoothness = _Shininess;
      o.Specular = _SpecColor;
      o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));

      //World space fog falloff
      float dist = length(IN.worldPos);
      float falloff = smoothstep(_WorldSpaceFogRange.x, _WorldSpaceFogRange.y, dist);

      o.Emission = lerp(0, unity_FogColor, falloff);
      o.Albedo = lerp(o.Albedo, 0, falloff);
      o.Specular = lerp(o.Specular, 0, falloff);
      o.Smoothness = lerp(o.Smoothness, 0, falloff);
    }
      ENDCG
    }

  FallBack "Diffuse"
}
