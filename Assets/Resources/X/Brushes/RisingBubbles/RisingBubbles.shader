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

Shader "Brush/Particle/RisingBubbles" {
Properties {
  _TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
  _MainTex ("Particle Texture", 2D) = "white" {}
  _ScrollRate("Scroll Rate", Float) = 1.0
  _ScrollDistance("Scroll Distance", Vector) = (1.0, 0, 0)
  _ScrollJitterIntensity("Scroll Jitter Intensity", Float) = 1.0
  _ScrollJitterFrequency("Scroll Jitter Frequency", Float) = 1.0
  _SpreadRate ("Spread Rate", Range(0.3, 5)) = 1.539
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "DisableBatching"="True" }
  Blend One One
  BlendOp Add, Min
  ColorMask RGBA
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_particles
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #pragma target 3.0

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"
      #include "Assets/Shaders/Include/Particles.cginc"
      #include "Assets/ThirdParty/Shaders/Noise.cginc"

      sampler2D _MainTex;
      fixed4 _TintColor;

      struct v2f {
        float4 vertex : SV_POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
      };

      float4 _MainTex_ST;
      float _ScrollRate;
      // In decimeters
      float3 _ScrollDistance;
      // Amplitude: in decimeters
      float _ScrollJitterIntensity;
      float _ScrollJitterFrequency;
      float _SpreadRate;

      // pos and seed should be stable values.
      // seed is a value in [0, 1]
      // t01 is a time value in [0, 1]
      float3 ComputeDisplacement(float3 pos, float seed, float t01) {
        float t2 = _Time.y / 3;

        // Animate the motion of the bubbles
        // Accumulate all displacement into a common, pre-transformed space.
        float4 dispVec = float4(_ScrollDistance, 0.0) * t01;

        // Overall motion of the bubbles
        dispVec.x += sin(t01 * _ScrollJitterFrequency + seed * 10 + t2 + pos.z) * _ScrollJitterIntensity;
        dispVec.y += (fmod(seed * 100, 1) - 0.5) * _ScrollDistance.y * t01;
        dispVec.z += cos(t01 * _ScrollJitterFrequency + seed * 7 + t2 + pos.x) * _ScrollJitterIntensity;

        return dispVec * kDecimetersToWorldUnits;
      }

      v2f vert (ParticleVertexWithSpread_t v) {
        v.color = TbVertToSrgb(v.color);
        v2f o;
        // Used as a random-ish seed for various calculations
        float seed = v.color.a;
        float t01 = fmod(_Time.y*_ScrollRate + seed * 10, 1);
        float birthTime = v.texcoord.w;
        float rotation = v.texcoord.z;
        float halfSize = GetParticleHalfSize(v.corner.xyz, v.center, birthTime);
        float spreadProgress = SpreadProgress(birthTime, _SpreadRate);
        float4 center = SpreadParticle(v, spreadProgress);
        float3 disp = ComputeDisplacement(center.xyz, seed, t01);
        disp = spreadProgress * disp;

        // Dim over lifetime
        v.color.rgb *= pow (1 - t01, 1)*5;

        // Custom vertex animation
#if 1
        // Displacement is in scene space
        // Note that xf_CS is actually scene, not canvas
        // The problem with this is that if you scale up a layer, the particles
        // get big but the overall motion stays the same.
        float4 center_WS = mul(unity_ObjectToWorld, center);
        center_WS.xyz += mul(xf_CS, disp);
        PrepForOdsWorldSpace(center_WS);
        float4 corner_WS = OrientParticle_WS(center_WS.xyz, halfSize, v.vid, rotation);
        o.vertex = mul(UNITY_MATRIX_VP, corner_WS);
#else
        // Displacement is in canvas space
        // Note that we assume object space == canvas space (which it is, for TB)

        PrepForOds(center);
        float4 corner = OrientParticle(center + disp, halfSize, v.vid, rotation);
        o.vertex UnityObjectToClipPos(corner);
#endif

        o.color = v.color;
        o.texcoord = TRANSFORM_TEX(v.texcoord.xy,_MainTex);

        return o;
      }

      // i.color is srgb
      fixed4 frag (v2f i) : SV_Target
      {
        float4 tex = tex2D(_MainTex, i.texcoord);

        // RGB Channels of the texture are affected by color
        float3 basecolor = i.color * tex.rgb;
        // Alpha channel of the texture is not affected by color.  It is the fake "highlight" bubble effect.
        float3 highlightcolor = tex.a;

        float4 color = float4(basecolor + highlightcolor, 1);
        return SrgbToNative(color);
        return color;
      }
      ENDCG
    }
  }
}
}
