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

Shader "Brush/Standard Wireframe" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
  _Shininess ("Shininess", Range (0.01, 1)) = 0.078125
  _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
  _BumpMap ("Normalmap", 2D) = "bump" {}
  _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  _VertOrder ("Vertex Ordering", Int) = 1
}
// The wireframe shader works by giving each vertex of a triangle a barycentric coordinate:
//
// (1,0,0)
//    * - - - - - - - * (0, 1, 0)
//    |          __--
//   (Y) (X) __--
//    |  __--
//    *--
// (0, 0, 1)
//
// The barycentric coordinates are interpolated before reaching the fragment shader so that
// you can tell when you are near an edge because one of the coordinates will be very close
// to zero. So for instance, position (X) will have coordinates (0.5, 0.5, 0.5) and position
// (Y) will have coordinates (0.5, 0, 0.5). Therefore we know (Y) is on an edge.
//
// The difficulty with this is that in this shader we need to choose the barycentric coordinates
// for each vector in such a way that no triangle has vertices that share the same coordinates.
// We only have the vertex index to go on, and there are a couple of different ways we set up
// the vertex order, which is what the vertex ordering parameter controls.
//
// Here are the vertex orders:
//
// 0 : 0--1  4  (Used by Quad Strip Brush)
//     | /  /|
//     |/  / |
//     2  3--5
//
// 1:  Front: 2--6--10-14   Back: 3--7--11-15  (Used by Flat Geometry Brush)
//            | /| /| /|          | /| /| /|
//            |/ |/ |/ |          |/ |/ |/ |
//            0--4--8--12         1--5--9--13


  SubShader {
    Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
    LOD 400
  Cull Back

    CGPROGRAM
    #pragma target 3.0
    #pragma surface surf StandardSpecular vertex:vert alphatest:_Cutoff addshadow
    #pragma multi_compile __ AUDIO_REACTIVE
    #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

    #include "Assets/Shaders/Include/Brush.cginc"

    struct appdata_full_and_vid
    {
      uint vid : SV_VertexID;
       float4 vertex    : POSITION;  // The vertex position in model space.
      float3 normal    : NORMAL;    // The vertex normal in model space.
      float4 texcoord  : TEXCOORD0; // The first UV coordinate.
      float4 texcoord1 : TEXCOORD1; // The second UV coordinate.
      float4 texcoord2 : TEXCOORD2; // The second UV coordinate.
      float4 tangent   : TANGENT;   // The tangent vector in Model Space (used for normal mapping).
      float4 color     : COLOR;     // Per-vertex color
      UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Input {
      float2 uv_MainTex;
      float2 uv_BumpMap;
      float4 color : Color;
      float3 barycentric;
    };

    sampler2D _MainTex;
    sampler2D _BumpMap;
    fixed4 _Color;
    half _Shininess;
    int _VertOrder;

    void vert (inout appdata_full_and_vid i, out Input o) {
      o.color = TbVertToNative(i.color);
      o.uv_MainTex = i.texcoord;
      o.uv_BumpMap = i.texcoord1;

      int corner;
      if (_VertOrder == 0) {
          corner = i.vid % 3;
      } else {
          corner = (((i.vid / 2)  + 1) / 2) % 3;
      }
      o.barycentric = float3(corner, corner, corner) == float3(0,1,2);
    }

    void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
      fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
      o.Albedo = tex.rgb * _Color.rgb * IN.color.rgb;
      o.Smoothness = _Shininess;
      o.Specular = _SpecColor;
      o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
      o.Alpha = tex.a * IN.color.a;

      if (any(IN.barycentric < float3(0.02, 0.02, 0.02))) {
        o.Albedo = float4(1,1,1,1);
        o.Alpha = 1;
      }
    }
    ENDCG
  }

  FallBack "Transparent/Cutout/VertexLit"
}
