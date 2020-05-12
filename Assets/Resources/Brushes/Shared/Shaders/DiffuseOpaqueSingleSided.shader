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

Shader "Brush/DiffuseOpaqueSingleSided" {

Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
}

SubShader {
  Cull Back

  CGPROGRAM
  #pragma surface surf Lambert vertex:vert addshadow

  #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
  #pragma multi_compile __ SELECTION_ON
  // Faster compiles
  #pragma skip_variants INSTANCING_ON

  #include "Assets/Shaders/Include/Brush.cginc"
  #include "Assets/Shaders/Include/MobileSelection.cginc"
  fixed4 _Color;

  struct Input {
    float4 color : COLOR;
  };

  void vert(inout appdata_full v, out Input o) {
    PrepForOds(v.vertex);
    v.color = TbVertToNative(v.color);
    UNITY_INITIALIZE_OUTPUT(Input, o);
  }

  void surf (Input IN, inout SurfaceOutput o) {
    o.Albedo = _Color * IN.color.rgb;
    SURF_FRAG_MOBILESELECT(o);
  }
  ENDCG
}  // SubShader

Fallback "Diffuse"

}  // Shader
