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

Shader "Brush/Special/Race" {
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
      #include "Assets/ThirdParty/Shaders/Noise.cginc"

      #define BRUSHES_DIGITAL_ROWS 5

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
        // copied from Digital.shader with a modification on the chance
        // that a tile will connect with its neighbor
        float stroke_width = .1;
        float antialias_feather_px = 4;

        float2 st = i.texcoord;

        // used for antialiasing
        float2 st_per_px = fwidth(st);

        // create the grid.
        // rc means row,col. rc.x actually really means x i.e. it's columns
        float2 rc = floor(st);

        // get the tile coordinates
        // map tile_st.y to [-1, 1]
        float tile_st = frac(st);
        tile_st -= .5;
        tile_st *= 2;

        float lum = 0;
        float r = length(tile_st);

        // for each neighboring tile...
        for (int ii = -1; ii <= 1; ii++) {
          if (rc.x + ii < 0) continue;
          for (int jj = -1; jj <= 1; jj++) {
            if (abs(ii) == abs(jj)) continue;
            if (rc.y + jj < 0) continue;
            if (rc.y + jj >= BRUSHES_DIGITAL_ROWS) continue;

            // me and my neighbor decide if we should connect our tiles.
            // The ascii art below is copied from Digital.shader. The idea is the same, but it
            // wouldn't be an exact depiction here, given the specific numbers.
            //
            // (x) represents random(rc)
            // [x] represents random(rc) + random(rc + ij)
            //
            // (0.5)xx[1.3]xx(0.8)
            //   x
            //   x
            // [1.1]         [0.4]
            //   x
            //   x
            // (0.6)  [0.7]  (0.1)
            //
            // if i'm (0.5), then i'm going to draw (0.5)xxx---(0.8)
            // if i'm (0.8), then i'm going to draw (0.5)---xxx(0.8)
            if (ii == 0) {
                // horizontal connections are more likely to connect
                if (random(rc) + random(rc + float2(ii, jj)) < 1.5) continue;
            } else {
                // vertical connections are less likely to connect
                if (random(rc) + random(rc + float2(ii, jj)) < .5) continue;
            }

            // draw a rectangle with width stroke_width through the center of
            // this tile towards the other tile
            //      i=0,j=1   i=-1,j=0
            //    ( --111--   ------- )   --111--
            //    ( --111--   ------- )   --111--
            //    ( --111--   11111-- )   11111--
            // max( --111-- , 11111-- ) = 11111--
            //    ( --111--   11111-- )   11111--
            //    ( -------   ------- )   -------
            //    ( -------   ------- )   -------

            float2 ij = float2(ii, jj);
            float2 ij_perp = float2(-jj, ii);
            // when ij points to the right:
            // bound1 is bottomleft
            // bound2 is topright
            float2 bound1 = stroke_width * -ij_perp + stroke_width * -ij;
            float2 bound2 = ij + stroke_width * ij_perp;
            float2 min_bound = min(bound1, bound2);
            float2 max_bound = max(bound1, bound2);

            float2 aa_feather = st_per_px * antialias_feather_px;
            float current_lum =
                smoothstep(min_bound.x - aa_feather.x, min_bound.x, st.x) *
                smoothstep(max_bound.x + aa_feather.x, max_bound.x, st.x) *
                smoothstep(min_bound.y - aa_feather.y, min_bound.y, st.y) *
                smoothstep(max_bound.y + aa_feather.y, max_bound.y, st.y);
            lum = max(lum, current_lum);
          }
        }
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
