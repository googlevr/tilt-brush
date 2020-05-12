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

Shader "Brush/Particle/Stars" {
Properties {
  _MainTex ("Particle Texture", 2D) = "white" {}
  _SparkleRate ("Sparkle Rate", Float) = 2.5
  _SpreadRate ("Spread Rate", Range(0.3, 5)) = 1.539
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "DisableBatching"="True" }
  Blend One One // SrcAlpha One
  BlendOp Add, Min
  AlphaTest Greater .01
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
      #pragma multi_compile __ SELECTION_ON
      #pragma target 3.0

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"
      #include "Assets/Shaders/Include/Particles.cginc"
      #include "Assets/Shaders/Include/MobileSelection.cginc"

      sampler2D _MainTex;
      float4 _MainTex_ST;
      float _SparkleRate;
      float _SpreadRate;

      struct v2f {
        float4 vertex : SV_POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
      };

      v2f vert (ParticleVertexWithSpread_t v)
      {
        v.color = TbVertToSrgb(v.color);
        const float PI = 3.14159265359;
        v2f o;
        float birthTime = v.texcoord.w;
        float rotation = v.texcoord.z;
        float halfSize = GetParticleHalfSize(v.corner.xyz, v.center, birthTime);
        float spreadProgress = SpreadProgress(birthTime, _SpreadRate);
        float4 center = SpreadParticle(v, spreadProgress);
        PrepForOds(center);

        float phase = v.color.a * (2 * PI);
        float brightness;

#ifdef AUDIO_REACTIVE
        brightness = 800 * pow(abs(sin(_BeatOutputAccum.w * _SparkleRate + phase)), 20);
        brightness = brightness*.25 + 2*brightness * (_BeatOutput.w);
#else
        brightness = 800 * pow(abs(sin(_Time.y * _SparkleRate + phase)), 20);
#endif
        o.color.rgb = v.color.rgb * brightness;
        o.color.a = 1;
        o.texcoord = TRANSFORM_TEX(v.texcoord.xy,_MainTex);

        float4 corner = OrientParticle(center.xyz, halfSize, v.vid, rotation);
        o.vertex = UnityObjectToClipPos(corner);

        return o;
      }

      // Input color is srgb
      fixed4 frag (v2f i) : SV_Target
      {
        float4 texCol = tex2D(_MainTex, i.texcoord);
        float4 color = i.color * texCol;
        color = encodeHdr(color.rgb * color.a);
        color = SrgbToNative(color);
#if SELECTION_ON
        color.rgb = GetSelectionColor() * texCol.r;
        color.a = texCol.a;
#endif
        return color;
      }
      ENDCG
    }
  }
}
}
