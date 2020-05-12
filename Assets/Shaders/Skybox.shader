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

Shader "Custom/Skybox" {
Properties {
  _Tint ("Tint Color", Color) = (.5, .5, .5, .5)
  _Exposure ("Exposure", Range(0, 8)) = 1.0
  _SkyboxRotation ("Skybox rotation", Vector) = (0.0, 0.0, 0.0, 1.0)
  [NoScaleOffset] _Tex ("Cubemap   (HDR)", Cube) = "grey" {}
}

SubShader {
  Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
  Cull Off ZWrite Off

  Pass {

    CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag

    #include "UnityCG.cginc"

    samplerCUBE _Tex;
    half4 _Tex_HDR;
    half4 _Tint;
    half _Exposure;
    float4 _SkyboxRotation;

    float4 quat_mult(float4 q1, float4 q2) {
      float4 qr;
      qr.x = (q1.w * q2.x) + (q1.x * q2.w) + (q1.y * q2.z) - (q1.z * q2.y);
      qr.y = (q1.w * q2.y) - (q1.x * q2.z) + (q1.y * q2.w) + (q1.z * q2.x);
      qr.z = (q1.w * q2.z) + (q1.x * q2.y) - (q1.y * q2.x) + (q1.z * q2.w);
      qr.w = (q1.w * q2.w) - (q1.x * q2.x) - (q1.y * q2.y) - (q1.z * q2.z);
      return qr;
    }

    struct appdata_t {
      float4 vertex : POSITION;
    };

    struct v2f {
      float4 vertex : SV_POSITION;
      float3 texcoord : TEXCOORD0;
    };

    v2f vert (appdata_t v)
    {
      v2f o;
      float4 quatConjugate = float4(-_SkyboxRotation.x, -_SkyboxRotation.y, -_SkyboxRotation.z, _SkyboxRotation.w);
      o.vertex = UnityObjectToClipPos(quat_mult(_SkyboxRotation, quat_mult(v.vertex, quatConjugate)));
      o.texcoord = v.vertex;
      return o;
    }

    fixed4 frag (v2f i) : SV_Target
    {
      half4 tex = texCUBE (_Tex, i.texcoord);
      half3 c = DecodeHDR (tex, _Tex_HDR);
      //c = c * _Tint.rgb * unity_ColorSpaceDouble;
      c *= _Tint.rgb;
      c *= _Exposure;
      return half4(c, 1);
    }
    ENDCG
  }
}


Fallback Off

}
