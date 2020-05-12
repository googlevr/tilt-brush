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

//
// Tilt Brush variant of the Blocks glass shader
//
Shader  "Blocks/BlocksGlass"  {
  Properties {
    _Color ("Color", Color) = (1,1,1,1)
    _Noise ("Noise (RGB)", 2D) = "white" {}
    _Shininess ("Shininess", Range(0,1)) = 0.8
    _RimIntensity ("Rim Intensity", Range(0,1)) = .2
    _RimPower ("Rim Power", Range(0,16)) = 5
  }

  SubShader {

  Tags { "RenderType"="Transparent" "Queue"="Transparent"}
  LOD 200

  Blend One SrcAlpha
  Zwrite Off
  Cull Off

  CGPROGRAM
  #pragma surface surf StandardSpecular vertex:vert fullforwardshadows nofog
  #pragma target 3.0
  #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
  #pragma multi_compile __ SELECTION_ON HIGHLIGHT_ON

  #include "Assets/Shaders/Include/Brush.cginc"
  #include "Assets/Shaders/Include/MobileSelection.cginc"

  struct Input {
    float3 viewDir;
    fixed vface : VFACE;
  };

  half _Shininess;
  half _RimIntensity;
  half _RimPower;
  fixed4 _Color;

  void vert(inout appdata_full i, out Input o) {
    PrepForOds(i.vertex);
    UNITY_INITIALIZE_OUTPUT(Input, o);
  }

  void surf(Input IN, inout SurfaceOutputStandardSpecular o) {
    o.Normal = float3(0,0,IN.vface);

    // Dim Backfaces
    float backfaceDimming = IN.vface == -1 ? .25 : 1;

    o.Albedo = 0;
    o.Specular = _Color.rgb * backfaceDimming;

// Currently rim lighting is causing the entire object to go white in ODS renders.
// TODO: figure out what's causing this.
#if !defined(ODS_RENDER_CM)
    // Rim Lighting
    o.Emission = (pow(1 - saturate(dot(IN.viewDir, o.Normal)), _RimPower)) * _RimIntensity * backfaceDimming;
#endif

    o.Smoothness = _Shininess;
    SURF_FRAG_MOBILESELECT(o);
  }
  ENDCG

  }

  FallBack "Diffuse"
}
