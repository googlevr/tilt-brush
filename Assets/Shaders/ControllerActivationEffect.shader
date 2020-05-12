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

Shader "Custom/ControllerActivationEffect" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Image", 2D) = "" {}
    _OutlineWidth("Outline Width", Float) = 0.02
    _Intensity("Intensity", Float) = 1

  }
  SubShader {
    Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}

    Blend One One
    ZWrite Off

    CGPROGRAM
    #pragma surface surf Lambert vertex:vert

    struct Input {
      float2 uv_MainTex;
    };

    uniform float4 _Color;
    sampler2D _MainTex;
    uniform float _OutlineWidth;
    float _Intensity;

    void vert (inout appdata_full v) {
      // Transform into worldspace
      float4 world_space_vertex = mul( unity_ObjectToWorld, v.vertex );

      // Create outline
      world_space_vertex.xyz += normalize(mul( unity_ObjectToWorld, float4(v.normal,0) ).xyz ) * _OutlineWidth;

      // Transform back into local space
      v.vertex = mul( unity_WorldToObject, world_space_vertex );
    }

    void surf (Input IN, inout SurfaceOutput o) {

      o.Albedo = 0;
      float3 col = tex2D(_MainTex, IN.uv_MainTex.xy - _Time.y * 1.75).xyz;
      o.Emission = 10.0f * _Intensity * _Color.xyz * col;
      o.Alpha = 1;
    }
    ENDCG
  }
  FallBack "Diffuse"
}
