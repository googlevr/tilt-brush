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

Shader "Brush/Special/Plasma" {
Properties {
  _MainTex ("Particle Texture", 2D) = "white" {}

  _Scroll1 ("Scroll1", Float) = 0
  _Scroll2 ("Scroll2", Float) = 0
  _EmissionScroll1 ("Emission Scroll1", Float) = 0
  _EmissionScroll2 ("Emission Scroll2", Float) = 0

  _DisplacementIntensity("Displacement", Float) = .1

    _EmissionGain ("Emission Gain", Range(0, 1)) = 0.5
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One //SrcAlpha One
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
      #pragma multi_compile_particles
      #pragma multi_compile __ SELECTION_ON
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"
      #include "Assets/Shaders/Include/MobileSelection.cginc"

      sampler2D _MainTex;

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float3 normal : NORMAL;
        float2 texcoord : TEXCOORD0;

      };

      struct v2f {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        float3 worldPos : TEXCOORD1;
      };

      float4 _MainTex_ST;
      fixed _Scroll1;
      fixed _Scroll2;
      fixed _EmissionScroll1;
      fixed _EmissionScroll2;
      half _DisplacementIntensity;
      half _EmissionGain;

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);

        v2f o;
        o.worldPos = mul(unity_ObjectToWorld, v.vertex);
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.color = v.color;
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        return o;
      }

      float rand_1_05(in float2 uv)
      {
        float2 noise = (frac(sin(dot(uv ,float2(12.9898,78.233)*2.0)) * 43758.5453));
        return abs(noise.x + noise.y) * 0.5;
      }

      fixed4 frag (v2f i) : COLOR
      {
        // Workaround for b/30500118, caused by b/30504121
        i.color.a = saturate(i.color.a);

        // Tuning constants for 3 lines
        half3 A     = half3(0.55, 0.3, 0.7 );
        half3 aRate = half3(1.2 , 1.0, 1.33);
        half3 M     = half3(1.0 , 2.2, 1.5);  // kind of a multiplier on A's values
        half3 bRate = half3(1.5 , 3.0, 2.25) + M * aRate;
        half3 LINE_POS = 0.5;
        half3 LINE_WIDTH = .012;

#ifdef AUDIO_REACTIVE
        float waveformcoord = i.texcoord.x * .2f;
        float envelope = sin(3.14159 * waveformcoord);
        float waveform = (tex2D(_WaveFormTex, float2(waveformcoord,0)).r - .5f);
        i.texcoord.y += waveform * envelope * .5f;
#endif

        // Calculate uvs for each line
        half3 us, vs;
        {
          us = A * i.texcoord.x - aRate * _Time.y;

          half3 tmp = M*A * i.texcoord.x - bRate * _Time.y;
          tmp = abs(frac(tmp) - 0.5);
          vs = i.texcoord.y + .4 * i.color.a * half3(1,-1,1) * tmp;
          vs = saturate(lerp((vs - .5) * 4, vs,  sin( (3.14159/2) * i.color.a)));
          }

        half4 tex = tex2D(_MainTex, half2(us[0], vs[0]));
        tex += tex2D(_MainTex, half2(us[1], vs[1]));
        tex += tex2D(_MainTex, half2(us[2], vs[2]));

        // render 3 procedural lines
        half3 procline = 1 - saturate(pow((vs - LINE_POS)/LINE_WIDTH, 2));
        tex += dot(procline, half3(1,1,1));

        // adjust brightness; modulate by color
        tex *= .8 * (1 + 30 * pow((1 - i.color.a), 5));
        i.color.a = 1; // kill any other alpha values that may come into this brush

        float4 c = i.color * tex;
#if SELECTION_ON
        c.rgb = GetSelectionColor() * tex;
#endif
        return encodeHdr(c.rgb * c.a);
      }

      ENDCG
    }
  }
}
}
