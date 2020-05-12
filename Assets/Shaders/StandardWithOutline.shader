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

Shader "Custom/StandardWithOutline" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _EmissionColor ("Emission Color", Color) = (1,1,1,1)
  _Shininess ("Smoothness", Range (0.01, 1)) = 0.013
  _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
  _OutlineWidth("Outline Width", Float) = 0.02

}
    SubShader {
    Tags{ "RenderType" = "Opaque" }
    LOD 100

    CGPROGRAM
    #pragma target 3.0
    #pragma surface surf Standard

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


    Cull Front
    CGPROGRAM
#pragma surface surf Standard vertex:vert nofog
#include "Assets/Shaders/Include/Math.cginc"

      struct Input {
      float2 uv_MainTex;
    };

    uniform float4 _Color;
    sampler2D _MainTex;
    uniform float _OutlineWidth;

    void vert(inout appdata_full v) {
      // Transform into worldspace
      float4 world_space_vertex = mul(unity_ObjectToWorld, v.vertex);

      // Create outline.

      // Push the outline out in the direction of the unscaled normal.
      float3x3 unscaledObject2World;
      float3 unusedScale;
      factorRotationAndLocalScale(
        (float3x3)unity_ObjectToWorld, unscaledObject2World, unusedScale);

      // Push the outline out in the direction of the new unscaled normal.
      float3 world_normal = normalize(mul(unscaledObject2World, v.normal));
      world_space_vertex.xyz += world_normal * _OutlineWidth;

      // Transform back into local space
      v.vertex = mul(unity_WorldToObject, world_space_vertex);
    }

    void surf(Input IN, inout SurfaceOutputStandard o) {
      o.Albedo = 0;
      o.Emission = 0.0f;
      o.Alpha = 1.0;
    }
    ENDCG
  }

  FallBack "Diffuse"
}
