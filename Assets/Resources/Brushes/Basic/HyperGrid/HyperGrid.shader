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

Shader "Brush/Special/HyperGrid" {
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
      #pragma multi_compile __ SELECTION_ON
      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"
      #include "Assets/ThirdParty/Shaders/Noise.cginc"
      #include "Assets/Shaders/Include/MobileSelection.cginc"

      sampler2D _MainTex;
      fixed4 _TintColor;

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        float4 texcoord1 : TEXCOORD1;
      };

      struct v2f {
        float4 pos : SV_POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
      };

      float4 _MainTex_ST;

      v2f vert (appdata_t v)
      {
        v.color = TbVertToSrgb(v.color);
        v2f o;
        // Subtract out the Canvas space pose to keep the verts from popping around while
        // transforming (e.g. apply quantization in an immutable space).
        float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
        PrepForOdsWorldSpace(worldPos);
        worldPos = mul(xf_I_CS, worldPos);

        float waveform = 0;

        float lifetime = _Time.y - v.texcoord1.w;
        float size = length(v.texcoord1.xyz);
        float release = saturate(lifetime);

#ifdef AUDIO_REACTIVE
        worldPos.y -= release * fmod(_BeatOutputAccum.x - v.texcoord1.w, 5);
        worldPos.y += .3 * release * pow(sin(_BeatOutputAccum.x * 2 + worldPos.x),5);
#endif
        // Quantize vertices
        float q = (1.0f / size) * .5;
        q += 5 * saturate(1- release*10);
        float3 quantPos = ceil(worldPos.xyz * q) / q;
        worldPos.xyz = quantPos;
        worldPos = mul(xf_CS, worldPos);
        o.pos = mul(UNITY_MATRIX_VP,  worldPos);

        o.color = 2 * v.color + v.color.yzxw * _BeatOutput.x;
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        return o;
      }

      // Input color is srgb
      fixed4 frag (v2f i) : SV_Target
      {
        float4 c = i.color * _TintColor * tex2D(_MainTex, i.texcoord);
        c = encodeHdr(c.rgb * c.a);
        c = SrgbToNative(c);
        FRAG_MOBILESELECT(c)
        return c;
      }
      ENDCG
    }
  }
}
}
