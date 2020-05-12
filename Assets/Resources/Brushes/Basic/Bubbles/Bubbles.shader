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

Shader "Brush/Particle/Bubbles" {
Properties {
  _MainTex ("Particle Texture", 2D) = "white" {}
  _ScrollRate("Scroll Rate", Float) = 1.0
  _ScrollJitterIntensity("Scroll Jitter Intensity", Float) = 1.0
  _ScrollJitterFrequency("Scroll Jitter Frequency", Float) = 1.0
  _SpreadRate ("Spread Rate", Range(0.3, 5)) = 1.539
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "DisableBatching"="True" }
  Blend One One
  AlphaTest Greater .01
  ColorMask RGB
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_particles
      #pragma target 3.0
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #pragma multi_compile __ SELECTION_ON

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Particles.cginc"
      #include "Assets/ThirdParty/Shaders/Noise.cginc"
      #include "Assets/Shaders/Include/MobileSelection.cginc"

      sampler2D _MainTex;
      fixed4 _TintColor;

      struct v2f {
        float4 vertex : SV_POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
      };

      float4 _MainTex_ST;
      float _ScrollRate;
      float _ScrollJitterIntensity;
      float _ScrollJitterFrequency;
      float3 _WorldSpaceRootCameraPosition;
      float _SpreadRate;

      float3 computeDisplacement(float3 seed, float timeOffset) {
        float3 jitter; {
          float t = _Time.y * _ScrollRate + timeOffset;
          jitter.x = sin(t       + _Time.y + seed.z * _ScrollJitterFrequency);
          jitter.z = cos(t       + _Time.y + seed.x * _ScrollJitterFrequency);
          jitter.y = cos(t * 1.2 + _Time.y + seed.x * _ScrollJitterFrequency);
          jitter *= _ScrollJitterIntensity;
        }

        float3 curl; {
          float3 v = (seed + jitter) * .1 + _Time.x * 5;
          float d = 30;
          curl = float3(curlX(v, d), curlY(v, d), curlZ(v, d)) * 10;
        }

        return (jitter + curl) * kDecimetersToWorldUnits;
      }

      v2f vert (ParticleVertexWithSpread_t v) {
        v2f o;
        v.color = TbVertToSrgb(v.color);
        float birthTime = v.texcoord.w;
        float rotation = v.texcoord.z;
        float halfSize = GetParticleHalfSize(v.corner.xyz, v.center, birthTime);
        float spreadProgress = SpreadProgress(birthTime, _SpreadRate);
        float4 center = SpreadParticle(v, spreadProgress);
        PrepForOds(center);

        float3 displacement_SS = spreadProgress * computeDisplacement(center, 1);
        float3 displacement_WS = mul(xf_CS, float4(displacement_SS, 0));
        float3 displacement_OS = mul(unity_WorldToObject, float4(displacement_WS, 0));
        center.xyz += displacement_OS;
        float4 corner = OrientParticle(center.xyz, halfSize, v.vid, rotation);
        o.vertex = UnityObjectToClipPos(corner);

        // Brighten up the bubbles
        o.color = v.color;
        o.color.a = 1;
        o.texcoord = TRANSFORM_TEX(v.texcoord.xy,_MainTex);

        return o;
      }

      fixed4 frag (v2f i) : SV_Target
      {
        float4 tex = tex2D(_MainTex, i.texcoord);

        // RGB Channels of the texture are affected by color
        float3 basecolor = i.color * tex.rgb;

        // Alpha channel of the texture is not affected by color.  It is the fake "highlight" bubble effect.
        float3 highlightcolor = tex.a;

        float4 color = float4(basecolor + highlightcolor, 1);
        color = SrgbToNative(color);

#if SELECTION_ON
        color.rgb = GetSelectionColor() * tex.r;
        color.a = tex.a;
#endif

        return color;
      }
      ENDCG
    }
  }
}
}
