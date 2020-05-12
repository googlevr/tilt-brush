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

Shader "Brush/Special/TubeToonInverted" {
Properties {
  _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
}

CGINCLUDE

  #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

  #include "UnityCG.cginc"
  #include "Assets/Shaders/Include/Brush.cginc"
  #include "Assets/ThirdParty/Shaders/Noise.cginc"
  sampler2D _MainTex;
  float4 _MainTex_ST;

  struct appdata_t {
    float4 vertex : POSITION;
    fixed4 color : COLOR;
    float3 normal : NORMAL;
    float2 texcoord : TEXCOORD0;
  };

  struct v2f {
    float4 vertex : SV_POSITION;
    fixed4 color : COLOR;
    float2 texcoord : TEXCOORD0;
  };

  v2f vertInflate (appdata_t v, float inflate)
  {
    PrepForOds(v.vertex);

    v2f o;
    v.vertex.xyz += v.normal.xyz * inflate;
    o.vertex = UnityObjectToClipPos(v.vertex);
      o.color = v.color;
      o.color.a = 1;
      o.color.xyz += v.normal.y *.2;
      o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
    return o;
  }

  v2f vert (appdata_t v)
  {
    return vertInflate(v,0);
  }

  v2f vertEdge (appdata_t v)
  {
    // As the scale goes up, we want the outline to be smaller, almost as if it's in screenspace
    // however we don't want the outline to be a constant size, it should get smaller as the
    // stroke gets further from camera, to avoid distant strokes turning into a big black mess.
    float scale = length(mul(xf_I_CS, float3(0.05, 0, 0)));
    return vertInflate(v, scale);
  }

  fixed4 fragBlack (v2f i) : SV_Target
  {
    return float4(0,0,0,1);
  }

  fixed4 fragColor (v2f i) : SV_Target
  {
    return i.color;
  }

ENDCG



SubShader {
  Cull Back
  Pass{
    CGPROGRAM
    #pragma vertex vert
    #pragma fragment fragBlack
    ENDCG
    }

  Cull Front
  Pass{
    CGPROGRAM
    #pragma vertex vertEdge
    #pragma fragment fragColor
    ENDCG
    }
  }
Fallback "Diffuse"
}
