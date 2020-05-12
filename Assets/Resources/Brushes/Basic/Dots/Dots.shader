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

Shader "Brush/Visualizer/Dots" {
Properties {
  _TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
  _MainTex ("Particle Texture", 2D) = "white" {}
  _WaveformFreq("Waveform Freq", Float) = 1
  _WaveformIntensity("Waveform Intensity", Vector) = (0,1,0,0)
  _BaseGain("Base Gain", Float) = 0
  _EmissionGain("Emission Gain", Float) = 0
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "DisableBatching"="True" }
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
      #pragma multi_compile __ SELECTION_ON
      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"
      #include "Assets/Shaders/Include/Particles.cginc"
      #include "Assets/ThirdParty/Shaders/Noise.cginc"
      #include "Assets/Shaders/Include/MobileSelection.cginc"

      sampler2D _MainTex;
      fixed4 _TintColor;

      struct v2f {
        float4 vertex : SV_POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        float waveform : TEXCOORD1;
      };

      float4 _MainTex_ST;
      float _WaveformFreq;
      float4 _WaveformIntensity;
      float _EmissionGain;
      float _BaseGain;

      v2f vert (ParticleVertex_t v)
      {
        v.color = TbVertToSrgb(v.color);
        v2f o;
        float birthTime = v.texcoord.w;
        float rotation = v.texcoord.z;
        float halfSize = GetParticleHalfSize(v.corner.xyz, v.center, birthTime);
        float4 center = float4(v.center.xyz, 1);
        PrepForOds(center);
        float4 corner = OrientParticle(center.xyz, halfSize, v.vid, rotation);
        float waveform = 0;
        // TODO: displacement should happen before orientation
#ifdef AUDIO_REACTIVE
        float4 dispVec = float4(0,0,0,0);
        float4 corner_WS = mul(unity_ObjectToWorld, corner);
        // TODO: worldspace is almost certainly incorrect: use scene or object?
        waveform = tex2Dlod(_FFTTex, float4(fmod(corner_WS.x * _WaveformFreq + _BeatOutputAccum.z*.5,1),0,0,0) ).b * .25;
        dispVec.xyz += waveform * _WaveformIntensity.xyz;
        corner = corner + dispVec;
#endif
        o.vertex = UnityObjectToClipPos(corner);
        o.color = v.color * _BaseGain;
        o.texcoord = TRANSFORM_TEX(v.texcoord.xy,_MainTex);
        o.waveform = waveform * 15;
        return o;
      }

      // Input color is srgb
      fixed4 frag (v2f i) : SV_Target
      {
#ifdef AUDIO_REACTIVE
        // Deform uv's by waveform displacement amount vertically
        // Envelop by "V" UV to keep the edges clean
        float vDistance = abs(i.texcoord.y - .5)*2;
        float vStretched = (i.texcoord.y - 0.5) * (.5 - abs(i.waveform)) * 2 + 0.5;
        i.texcoord.y = lerp(vStretched, i.texcoord.y, vDistance);
#endif
        float4 tex = tex2D(_MainTex, i.texcoord);
        float4 c = i.color * _TintColor * tex;

        // Only alpha channel receives emission boost
        c.rgb += c.rgb * c.a * _EmissionGain;
        c.a = 1;
        c = SrgbToNative(c);
        c = encodeHdr(c.rgb);
#if SELECTION_ON
        c.rgb = GetSelectionColor() * tex.r;
#endif
        return c;
      }
      ENDCG
    }
  }
}
}
