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

Shader "Brush/Special/AdditiveScrolling" {
Properties {
  _TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
  _MainTex ("Particle Texture", 2D) = "white" {}
  _ScrollRate("Scroll Rate", Float) = 1.0
  _ScrollDistance("Scroll Distance", Vector) = (1.0, 0, 0)
  _ScrollJitterIntensity("Scroll Jitter Intensity", Float) = 1.0
  _ScrollJitterFrequency("Scroll Jitter Frequency", Float) = 1.0
  _FalloffPower("Falloff Power", Float) = 2.0
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend SrcAlpha One
  AlphaTest Greater .01
  ColorMask RGB
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"

      sampler2D _MainTex;
      fixed4 _TintColor;

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

      float4 _MainTex_ST;
      float _ScrollRate;
      float3 _ScrollDistance;
      float _ScrollJitterIntensity;
      float _ScrollJitterFrequency;
      float3 _WorldSpaceRootCameraPosition;
      half _FalloffPower;

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);
        v2f o;

        // Custom vertex animation
        float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
        float t = fmod(_Time.y*_ScrollRate + v.color.a, 1);
        worldPos.xyz +=  (t - .5f) * _ScrollDistance;
        worldPos.x += sin(t * _ScrollJitterFrequency + _Time.y) * _ScrollJitterIntensity;
        worldPos.z += cos(t * _ScrollJitterFrequency * .5 + _Time.y) * _ScrollJitterIntensity;
        v.color.a = pow(1 - abs(2*(t - .5)),3);

        o.vertex = mul(UNITY_MATRIX_VP, worldPos);
        o.color = v.color;
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);

        // Edge Falloff
        float3 worldSpaceView = normalize(_WorldSpaceRootCameraPosition.xyz - mul(unity_ObjectToWorld, v.vertex).xyz);
        float3 worldSpaceNormal = normalize(mul(unity_ObjectToWorld, float4(v.normal.xyz,0)));
        float falloff =  abs(dot(worldSpaceNormal, worldSpaceView));
        o.color.a *= pow(falloff, _FalloffPower);

        return o;

      }

      fixed4 frag (v2f i) : SV_Target
      {
        return 2.0f * i.color * _TintColor * tex2D(_MainTex, i.texcoord);
      }
      ENDCG
    }
  }
}
}
