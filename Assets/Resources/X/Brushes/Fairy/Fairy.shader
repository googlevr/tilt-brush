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

Shader "Brush/Special/Fairy" {
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
      fixed4 frag (v2f i) : COLOR {
        float scale1 = 3;
        float scale2 = 3;

        float2 st = i.texcoord.xy;
        float2 uv = st;

        // fix aspect ratio
        st.x *= 5;

        // divide the space into tiles
        float2 scaler = floor(st);
        scaler = random(scaler);
        scaler *= scale1;
        scaler = max(scaler, 1);
        scaler = floor(scaler);
        st *= scaler;

        // and again
        scaler = floor(st);
        scaler = random(scaler + 234.4);
        scaler *= scale2;
        scaler = max(scaler, 1);
        scaler = floor(scaler);
        st *= scaler;

        // row,col (only used as random seed)
        float2 rc = floor(st);

        // get the tile uv
        st = frac(st);
        st -= .5;
        st *= 2;

        // scale it a bit
        float rscale = lerp(.2, 1, random(rc));
        st /= rscale;

        // move it a little bit
        float2 offset = random2(rc + 5) * .1;
        st += offset;

        float r = length(st);
        float lum = 1 - r;

        // make sure the dot fully fades by the time we get to the edge
        // of the square, otherwise it will get chopped off
        lum -= max(offset.x, offset.y);
        lum = saturate(lum);

        // vary the radial brightness falloff
        float powpow = random(rc);
        powpow = powpow * 2 - 1;
        powpow = max(.3, powpow);
        if (powpow < 0)
        {
            powpow = 1 / abs(powpow);
        }
        lum *= 2;
        lum = pow(lum, powpow);

        // fade the dots in and out with variety
        float fadespeed = lerp(.25, 1.25, random(rc));
        float fadephase = random(rc) * 2 * 3.14;
        float time = sin(_Time.z * fadespeed + fadephase) / 2 + .5;
        lum *= lerp(0, 1, time);

        fixed4 color;
        color.a = 1;
        color.rgb = lum*bloomColor(i.color,lum*_EmissionGain);
        return color;
      }
      ENDCG
    }
  }
}
}
