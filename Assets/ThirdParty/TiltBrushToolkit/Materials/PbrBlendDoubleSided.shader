// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// GLTF2 PBR Transparent Shader. For spec info, see:
// https://github.com/KhronosGroup/glTF/blob/master/specification/2.0/schema/material.pbrMetallicRoughness.schema.json
Shader "Poly/PbrBlendDoubleSided" {
  Properties {
    _BaseColorFactor ("Base Color Factor", Color) = (1,1,1,1)
    _BaseColorTex ("Base Color Texture", 2D) = "white" {}
    _MetallicFactor ("Metallic Factor", Range(0,1)) = 1.0
    _RoughnessFactor ("Roughness Factor", Range(0,1)) = 1.0
  }
  SubShader {
    Cull Off
    ZWrite Off
    Tags { "Queue"="Transparent" "RenderType"="Transparent" }

    CGPROGRAM
    // Physically based Standard lighting model
    // No shadow-receiving passes
    #pragma surface surf Standard vertex:vert noshadow alpha:blend

    // Selection
    #pragma multi_compile __ SELECTION_ON HIGHLIGHT_ON
    #include "Assets/Shaders/Include/MobileSelection.cginc"
    // ODS Render Support
    #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
    #include "Assets/Shaders/Include/Brush.cginc"

    // Use shader model 3.0 target, because we use VFACE and to get nicer looking lighting
    #pragma target 3.0

    sampler2D _BaseColorTex;

    struct Input {
      float2 uv_BaseColorTex;
      // Filled in with (rgb=1, a=1) if the mesh doesn't have color
      float4 color : COLOR;
      fixed vface : VFACE;
    };

    fixed4 _BaseColorFactor;
    half _MetallicFactor;
    half _RoughnessFactor;

    void vert (inout appdata_full i , out Input o) {
      UNITY_INITIALIZE_OUTPUT(Input, o);
      PrepForOds(i.vertex);
    }

    void surf (Input IN, inout SurfaceOutputStandard o) {
      // Albedo comes from a texture tinted by color
      float4 c = tex2D(_BaseColorTex, IN.uv_BaseColorTex) * _BaseColorFactor * IN.color;
      o.Normal = float3(0, 0, IN.vface);
      o.Albedo = c.rgb;
      o.Alpha = c.a;
      // Metallic and smoothness come from parameters.
      o.Metallic = _MetallicFactor;
      // Smoothness is the opposite of roughness.
      o.Smoothness = 1.0 - _RoughnessFactor;

      SURF_FRAG_MOBILESELECT(o);
    }
    ENDCG
  }
}
