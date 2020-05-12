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

Shader "Custom/FixDistortion" {
  Properties {
      _RayOffsetX ("Ray Offset X", Float) = 0.0
      _RayOffsetY ("Ray Offset Y", Float) = 0.0
      _RayScaleX ("Ray Scale X", Float) = 0.0
      _RayScaleY ("Ray Scale Y", Float) = 0.0
      _MainTex ("Image", 2D) = "" {}
      _DistortX ("Distortion X", 2D) = "" {}
      _DistortY ("Distortion Y", 2D) = "" {}
  }

  SubShader {
      Lighting Off
      Cull Off
      Zwrite Off

    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      #include "UnityCG.cginc"

      uniform float _RayOffsetX;
      uniform float _RayOffsetY;
      uniform float _RayScaleX;
      uniform float _RayScaleY;
      uniform sampler2D _MainTex;
      uniform sampler2D _DistortX;
      uniform sampler2D _DistortY;

      struct fragment_input{
        float4 position : SV_POSITION;
        float2 uv : TEXCOORD0;
        float4 worldPos : TEXCOORD1;
      };

      fragment_input vert(appdata_img v) {
        fragment_input o;
        o.position = UnityObjectToClipPos(v.vertex);
        o.uv = MultiplyUV(UNITY_MATRIX_TEXTURE0, v.texcoord);
        o.worldPos = mul(unity_ObjectToWorld, v.vertex);
        return o;
      }

      float4 frag(fragment_input input) : COLOR {

        // Unwarp the point. Ray range is [-4, 4] X [-4, 4].
        float2 ray = input.uv * float2(8.0, 8.0) - float2(4.0, 4.0);
        float2 texDist = float2(_RayOffsetX, _RayOffsetY) + ray * float2(_RayScaleX, _RayScaleY);

        // Decode X and Y position floats from RGBA and rescale to [-0.6, 1.7).
        float texImageX = DecodeFloatRGBA(tex2D(_DistortX, texDist));
        texImageX = texImageX * 2.3 - 0.6;
        float texImageY = DecodeFloatRGBA(tex2D(_DistortY, texDist));
        texImageY = texImageY * 2.3 - 0.6;

        // Find the undistorted pixel location.
        float2 texCoord = float2(texImageX, texImageY);
        float a = tex2D(_MainTex, texCoord).a;
        float4 color = a;
        color.w = 1;


        return color;
      }
      ENDCG
    }
  }
}
