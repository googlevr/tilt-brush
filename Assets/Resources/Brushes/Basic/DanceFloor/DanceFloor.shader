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

Shader "Brush/Special/DanceFloor" {
Properties {
  _TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
  _MainTex ("Particle Texture", 2D) = "white" {}
}

Category {
  //Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  //Blend One One
  //BlendOp Add, Min
  //AlphaTest Greater .01
//  ColorMask RGBA
  Cull Off Lighting Off ZWrite On Fog { Color (0,0,0,0) }

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

      sampler2D _MainTex;
      fixed4 _TintColor;

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        float4 texcoord1 : TEXCOORD1;
        float3 normal : NORMAL;
      };

      struct v2f {
        float4 vertex : SV_POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        float3 worldPos : TEXCOORD1;
      };

      float4 _MainTex_ST;

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);
        v2f o;
        float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
        float waveform = 0;

        float lifetime = _Time.y - v.texcoord1.w;
        float release = saturate(lifetime);


#ifdef AUDIO_REACTIVE
        //worldPos.y -= release * fmod(_BeatOutputAccum.x * 2 - v.texcoord1.w, 5);
        //worldPos.y += .3 * release * pow(sin(_BeatOutputAccum.x * 5 + worldPos.x),5);
        lifetime = v.texcoord1.w * 10 + _BeatOutputAccum.x;
        o.color += tex2Dlod(_WaveFormTex, float4(lifetime * 5,0, 0, 0)).r - .5;

#endif

        v.color.xyz = pow(fmod(lifetime,1),3) * v.color.xyz; // * saturate(sin(lifetime * 10)) + v.color.zxy * saturate(cos(lifetime * 7));

        // Quantize vertices
        float q = 5;
        float3 quantPos = ceil(worldPos.xyz * q) / q;
        worldPos.xyz = quantPos;
        worldPos.xyz += v.normal * pow(fmod(lifetime,1),3) * .1;
        o.vertex = mul(UNITY_MATRIX_VP, worldPos);
        o.color = 2 * v.color + v.color.yzxw * _BeatOutput.x;
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        o.worldPos = worldPos.xyz;
        return o;
      }

      fixed4 frag (v2f i) : SV_Target
      {

        //float waveform = tex2D(_WaveFormTex, float2(fmod(i.worldPos.x * 0.1f + _Time.y,1),0) ).r - .5f;
        //i.texcoord.y += waveform;
        float4 c = i.color * _TintColor * tex2D(_MainTex, i.texcoord);
        return encodeHdr(c.rgb * c.a);
      }
      ENDCG
    }
  }
}
}
