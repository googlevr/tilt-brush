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

Shader "Brush/Intro/DiffuseOpaqueDoubleSided" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
   _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
}

SubShader {

Cull Off
    Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}

CGPROGRAM
#pragma surface surf Lambert vertex:vert addshadow alphatest:_IntroDissolve
#include "Assets/Shaders/Include/Brush.cginc"

fixed4 _Color;

struct Input {
  float4 color : COLOR;
};

void vert(inout appdata_full v) {
  v.color = TbVertToNative(v.color);
  // Custom alpha for the intro dissolve effect
  // Note that _IntroDissolve is being used as the alphatest parameter in the #pragma above
  v.color.a = v.texcoord.y;
}

void surf (Input IN, inout SurfaceOutput o) {
  o.Albedo = _Color * IN.color.rgb;
  o.Alpha = IN.color.a;
}
ENDCG
}

Fallback "Transparent/Cutout/VertexLit"
}
