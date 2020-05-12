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

Shader "Custom/UnlitOutlineFlatten" {
  Properties{
    _Color("Main Color", Color) = (1,1,1,1)
    _MainTex("Image", 2D) = "" {}
    _OutlineWidth("Outline Width", Float) = 0.02
    _FlattenAmount("Flatten Amount", Range(1, 0)) = 0

  }
    SubShader{
    Tags{ "Queue" = "AlphaTest+20" "RenderType" = "Geometry" }

    CGPROGRAM
    #pragma surface surf Lambert vertex:vert nofog

    uniform float4 _Color;
    sampler2D _MainTex;
    float _FlattenAmount;

  struct Input {
    float2 uv_MainTex;
  };

  void vert(inout appdata_full v) {
    // Smash along the z axis based on 0-1 ratio
    v.vertex.z = v.vertex.z - v.vertex.z * _FlattenAmount;
  }

  void surf(Input IN, inout SurfaceOutput o) {
    o.Emission = _Color.rgb;
  }
  ENDCG

    Cull Front
    CGPROGRAM
    #pragma surface surf Lambert vertex:vert nofog
    #include "Assets/Shaders/Include/Math.cginc"

    struct Input {
    float2 uv_MainTex;
  };

    uniform float4 _Color;
    sampler2D _MainTex;
    uniform float _OutlineWidth;
    float _FlattenAmount;

  void vert(inout appdata_full v) {

    // Smash along the z axis based on 0-1 ratio
    v.vertex.z = v.vertex.z - v.vertex.z * _FlattenAmount;

    // Transform into worldspace
    float4 world_space_vertex = mul(unity_ObjectToWorld, v.vertex);

    // Create outline.

    // Push the outline out in the direction of the unscaled normal.
    float3x3 unscaledObject2World;
    float3 unusedScale;
    factorRotationAndLocalScale((float3x3)unity_ObjectToWorld, unscaledObject2World, unusedScale);

    // Push the outline out in the direction of the new unscaled normal.
    float3 world_normal = normalize(mul(unscaledObject2World, v.normal));
    world_space_vertex.xyz += world_normal * _OutlineWidth;

    // Transform back into local space
    v.vertex = mul(unity_WorldToObject, world_space_vertex);
  }

  void surf(Input IN, inout SurfaceOutput o) {
    o.Albedo = 0;
    o.Emission = 0.0f;
    o.Alpha = 1.0;
  }
  ENDCG
  }
    FallBack "Diffuse"
}
