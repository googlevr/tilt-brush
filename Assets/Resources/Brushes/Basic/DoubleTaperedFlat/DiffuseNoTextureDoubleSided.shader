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

Shader "Brush/Special/DiffuseNoTextureDoubleSided" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
}

SubShader {
  Cull Off
  Tags{ "DisableBatching" = "True" }

  CGPROGRAM
  #pragma surface surf Lambert vertex:vert addshadow
  #pragma target 3.0
  #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
  #pragma multi_compile __ SELECTION_ON
  // Faster compiles
  #pragma skip_variants INSTANCING_ON
  #include "Assets/Shaders/Include/Brush.cginc"
  #include "Assets/Shaders/Include/MobileSelection.cginc"

  fixed4 _Color;

  struct appdata_t {
    float4 vertex : POSITION;
    fixed4 color : COLOR;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float2 texcoord0 : TEXCOORD0;
    float3 texcoord1 : TEXCOORD1;
    float4 texcoord2 : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
  };


  struct Input {
    float2 uv_MainTex;
    float4 color : COLOR;
    fixed vface : VFACE;
  };

  void vert (inout appdata_t v, out Input o) {
    PrepForOds(v.vertex);

    //
    // XXX - THIS TAPERING CODE SHOULD BE REMOVED ONCE THE TAPERING IS DONE IN THE GEOMETRY GENERATION
    // THE SHADER WILL REMAIN AS A SIMPLE "DiffuseNoTextureDoubleSided" SHADER.
    //

    UNITY_INITIALIZE_OUTPUT(Input, o);
    float envelope = sin(v.texcoord0.x * 3.14159);
    float widthMultiplier = 1 - envelope;
    v.vertex.xyz += -v.texcoord1 * widthMultiplier;
    v.color = TbVertToNative(v.color);
  }

  void surf (Input IN, inout SurfaceOutput o) {
    fixed4 c = _Color;
    o.Normal = float3(0,0,IN.vface);
    o.Albedo = c.rgb * IN.color.rgb;
    SURF_FRAG_MOBILESELECT(o);
  }
  ENDCG
}

Fallback "Transparent/Cutout/VertexLit"
}
