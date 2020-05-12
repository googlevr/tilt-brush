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

// This shader creates an outline by projecting an enlarged back face version
// of the geometry. The geometry needs to have smoothed normals.
//
// For non-uniform scales, this shader works best when all vertices are cubic
// because it's taking out the scaling component of the transform to keep the
// original orientation of the normals with respect to the geometry.
Shader "Custom/PointerScreenSpace" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Image", 2D) = "" {}
    _OutlineWidth("Outline Width", Float) = 0.02
    _BaseThickness("Thickness of Pointer Geometry", Float) = 0.025
    _MaxDistance("Screen Space Max Distance", Float) = 7.0
    _RevealSpeed("Reveal Speed", Float) = 2.0
    _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
  }

  SubShader {
    Tags { "Queue"="Geometry" "RenderType"="Geometry" }

    CGPROGRAM
    #pragma surface surf Lambert vertex:vert alphatest:_Cutoff
    #include "Assets/Shaders/Include/Math.cginc"

    struct Input {
      float2 uv_MainTex;
    };

    uniform float4 _Color;
    sampler2D _MainTex;
    uniform float _BaseThickness;
    uniform float _MaxDistance;
    uniform float _RevealSpeed;
    uniform float _RevealStartTime;

    void vert (inout appdata_full v) {
      // Transform into worldspace
      float4 world_space_vertex = mul( unity_ObjectToWorld, v.vertex );

      // Figure out distance to vertex in world space.
      float vertexDistance = length(world_space_vertex.xyz - _WorldSpaceCameraPos);

      if (vertexDistance > _MaxDistance) {
        float sizeIncrease = 0.5 * (vertexDistance / _MaxDistance - 1) * _BaseThickness;

        // Inflate the geometry.

        // Unscaled version of object to world matrix
        float3x3 unscaledObject2World;
        float3 unusedScale;
        factorRotationAndLocalScale(
            (float3x3)unity_ObjectToWorld, unscaledObject2World, unusedScale);

        // Push the outline out in the direction of the new unscaled normal.
        float3 world_normal = normalize(mul(unscaledObject2World, v.normal));

        world_space_vertex.xyz += world_normal * sizeIncrease;

        // Transform back into local space
        v.vertex = mul( unity_WorldToObject, world_space_vertex );
      }
    }

    void surf (Input IN, inout SurfaceOutput o) {
      o.Albedo = 0;
      o.Emission = _Color.rgb;
      if (_RevealStartTime) {
        o.Alpha = IN.uv_MainTex.x < (_Time.y - _RevealStartTime) * _RevealSpeed ? 1.0 : 0.0;
      } else {
        o.Alpha = 1.0;
      }
    }
    ENDCG

    Cull Front
    CGPROGRAM
    #pragma surface surf Lambert vertex:vert alphatest:_Cutoff
    #include "Assets/Shaders/Include/Math.cginc"

    struct Input {
      float2 uv_MainTex;
    };

    uniform float4 _Color;
    sampler2D _MainTex;
    uniform float _OutlineWidth;
    uniform float _BaseThickness;
    uniform float _MaxDistance;
    uniform float _RevealSpeed;
    uniform float _RevealStartTime;

    void vert (inout appdata_full v) {
      // Transform into worldspace
      float4 world_space_vertex = mul( unity_ObjectToWorld, v.vertex );

      // Figure out distance to vertex in world space.
      float vertexDistance = length(world_space_vertex.xyz - _WorldSpaceCameraPos);

      float sizeIncrease = vertexDistance > _MaxDistance ?
          0.5 * (vertexDistance / _MaxDistance * (_BaseThickness + 2 * _OutlineWidth) - _BaseThickness) :
          _OutlineWidth;

      // Create the outline.

      // Unscaled version of object to world matrix
      float3x3 unscaledObject2World;
      float3 unusedScale;
      factorRotationAndLocalScale(
          (float3x3)unity_ObjectToWorld, unscaledObject2World, unusedScale);

      // Push the outline out in the direction of the new unscaled normal.
      float3 world_normal = normalize(mul(unscaledObject2World, v.normal));
      world_space_vertex.xyz += world_normal * sizeIncrease;

      // Transform back into local space
      v.vertex = mul( unity_WorldToObject, world_space_vertex );
    }

    void surf (Input IN, inout SurfaceOutput o) {
      o.Albedo = 0;
      o.Emission = 0.0f;
      if (_RevealStartTime && _RevealSpeed != 0.0) {
        o.Alpha = IN.uv_MainTex[0] < (_Time.y - _RevealStartTime) * _RevealSpeed ? 1.0 : 0.0;
      } else {
        o.Alpha = 1.0;
      }
    }
    ENDCG
  }
  FallBack "Diffuse"
}
