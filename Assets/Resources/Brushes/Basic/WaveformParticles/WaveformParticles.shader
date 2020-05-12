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

Shader "Brush/Visualizer/WaveformParticles" {
Properties {
  _TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
  _MainTex ("Particle Texture", 2D) = "white" {}
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One
  BlendOp Add, Min
  AlphaTest Greater .01
  ColorMask RGBA
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 3.0
      #pragma glsl
      #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"
      #include "Assets/ThirdParty/Shaders/Noise.cginc"
      #include "Assets/Shaders/Include/Math.cginc"

      sampler2D _MainTex;
      fixed4 _TintColor;

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        float4 texcoord1 : TEXCOORD1;
        float3 tangent : TANGENT;
      };

      struct v2f {
        float4 vertex : SV_POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        float3 worldPos : TEXCOORD1;
        float lifetime : TEXCOORD2;
      };

      float4 _MainTex_ST;

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);

        v2f o;
        float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
        float3 perVertOffset = v.texcoord1.xyz;
        float lifetime = _Time.y - v.texcoord1.w;
        o.lifetime = lifetime;
        float release = saturate(lifetime * .1);
        float3 localMidpointPos = v.vertex.xyz - perVertOffset;
        float4 worldMidpointPos = mul(unity_ObjectToWorld, localMidpointPos);

#ifdef AUDIO_REACTIVE
        lifetime = -lifetime*.1 + _BeatOutputAccum.x;

#endif

        float time = lifetime;
        float d = 10 + v.color.g * 3;
        float freq = 1.5 + v.color.r;
        float3 disp = float3(1,0,0) * curlX(worldMidpointPos.xyz * freq + time, d);
        disp += float3(0,1,0) * curlY(worldMidpointPos.xyz * freq +time, d);
        disp += float3(0,0,1) * curlZ(worldMidpointPos.xyz * freq + time, d);

        worldMidpointPos.xyz += release * disp * 10;
        worldPos.xyz = worldMidpointPos.xyz + perVertOffset;

        o.vertex = mul(UNITY_MATRIX_VP, worldPos);
        o.color = v.color;
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        o.worldPos = worldPos.xyz;
        return o;
      }

      fixed4 frag (v2f i) : SV_Target
      {

        float4 c = i.color * _TintColor * tex2D(_MainTex, i.texcoord);
        return encodeHdr(c.rgb * c.a);
      }
      ENDCG
    }
  }
}
}
