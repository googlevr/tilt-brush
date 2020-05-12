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

Shader "Brush/Special/Space" {
Properties {
  _MainTex ("Particle Texture", 2D) = "white" {}
    _EmissionGain ("Emission Gain", Range(0, 1)) = 0.5
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One // SrcAlpha One
  BlendOp Add, Min
  ColorMask RGBA
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

      #pragma target 3.0
      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"
      #include "Assets/Shaders/Include/ColorSpace.cginc"
      #include "Assets/Shaders/Include/Math.cginc"
      #include "Assets/ThirdParty/Shaders/Noise.cginc"

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
      };

      float4 _MainTex_ST;
      half _EmissionGain;

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);
        v.color = TbVertToSrgb(v.color);

        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        o.color = v.color;
        return o;
      }

      // Input color is srgb
      fixed4 frag (v2f i) : COLOR
      {
        float analog_spread = .1;  // how far the analogous hues are from the primary
        float gain = 10;
        float gain2 = 0;

        // primary hue is chosen by user
        float3 i_HSV = RGBtoHSV(i.color.rgb);

        // we're gonna mix these 3 colors together
        float primary_hue = i_HSV.x;
        float analog1_hue = frac(primary_hue - analog_spread);
        float analog2_hue = frac(primary_hue + analog_spread);

        float r = abs(i.texcoord.y * 2 - 1);  // distance from center of stroke

        // determine the contributions of each hue
        float primary_a = .2 * fbm(i.texcoord + _Time.x) * gain + gain2;
        float analog1_a = .2 * fbm(float3(i.texcoord.x + 12.52, i.texcoord.y + 12.52, _Time.x * 5.2)) * gain + gain2;
        float analog2_a = .2 * fbm(float3(i.texcoord.x + 6.253, i.texcoord.y + 6.253, _Time.x * .8)) * gain + gain2;

        // the main hue is present in the center and falls off with randomized radius
        primary_a = clampedRemap(0, .5, primary_a, 0, r + fbm(float2(_Time.x + 50, i.texcoord.x)) * 2);

        // the analog hues start a little out from the center and increase with intensity going out
        analog1_a = clampedRemap(.2, 1, 0, analog1_a * 1.2, r);
        analog2_a = clampedRemap(.2, 1, 0, analog2_a * 1.2, r);

        fixed4 color;
        color.a = primary_a + analog1_a + analog2_a;

        float final_hue =
          primary_a * (primary_hue) +
          analog1_a * (analog1_hue) +
          analog2_a * (analog2_hue);
        final_hue /= color.a;

        // now sculpt the overall shape of the stroke
        float lum = 1 - r;
        float rfbm = fbm(float2(i.texcoord.x, _Time.x));
        rfbm += 1.2;
        rfbm *= .8;
        lum *= step(r, rfbm);  // shorten the radius with fbm

        // blur the edge a little bit
        lum *= smoothstep(rfbm, rfbm - .2, r);

        color.rgb = HSVToRGB(float3(final_hue, i_HSV.y, i_HSV.z * lum));
        color = saturate(color);  // i'm not sure why it's so bright without this
        color = bloomColor(color,_EmissionGain);
        color = SrgbToNative(color);
        color = encodeHdr(color.rgb);
        return color;
      }
      ENDCG
    }
  }
}
}
