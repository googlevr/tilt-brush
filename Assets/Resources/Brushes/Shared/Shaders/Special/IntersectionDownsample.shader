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

Shader "Brush/Special/IntersectionDownsample" {

  Properties
  {
    _MainTex("Texture", 2D) = "white" {}
  }

  Subshader {
    // -------------------------------------------------------------------------------------- //
    // Downsample Pass
    // -------------------------------------------------------------------------------------- //

    Pass
    {
      // No culling or depth
      Cull Off ZWrite Off ZTest Always

      //
      // Fixed 32x downsample with "first valid" interpolation.
      //
      CGPROGRAM
#pragma vertex vert
#pragma fragment frag

#include "UnityCG.cginc"

      // Added to avoid unrolling the loops below (which takes forever to compile).
#pragma target 3.0

      sampler2D _MainTex;
      float4 _MainTex_TexelSize; // (1/w, 1/h, w, h)

      struct appdata
      {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };

      struct v2f
      {
        // WARNING: If an interpolation modifier is added to uv, code must be updated below.
        float2 uv : TEXCOORD0;
        float4 vertex : SV_POSITION;
      };

      v2f vert(appdata v)
      {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        return o;
      }

      half4 frag(v2f input) : SV_Target
      {
        // Convert from the input UV, which is at a coarse resoltuion, to the _MainTex uv
        // which is at a higher resolution (4x). Note that pixels are sampled at the pixel
        // center by default, which is why the uv is initially shifted to the pixel start.
        //
        // WARNING: This code is sensitive to interpolation modifiers, so if v2f.uv is
        //          updated to use a modifier, this code must be updated accordingly.
        float2 uv = input.uv
          - (1 / _ScreenParams.xy) * .5 // Move from center to the pixel corner.
          + _MainTex_TexelSize.xy  * .5 // Move to center of the first sub-pixel.
          ;

        // Step size = sub-pixel size (1/w, 1/h).
        float2 duv = _MainTex_TexelSize.xy;

        // Loop over every sub-pixel, take the first valid result we see.
        //
        // Future work: we may want to break this down into smaller batches, 1024 iterations
        // may not perform well on all hardware, though is does divide nicely into 64 and
        // 256, which will fit evenly into most hardware and doesn't incur the barrier of
        // multiple blits/passes.
        for (float i = 0; i < 4; i++) {
          for (float j = 0; j < 4; j++) {
            // Using tex2Dlod here to explicitly avoid computing ddx/ddy and
            // sampling mipmaps.
            half4 c = tex2Dlod(_MainTex, float4(uv + duv * float2(i, j), 0, 0));
            if (any(c)) {
              return c;
            }
          }
        }

        return half4(0, 0, 0, 0);
      }
      ENDCG
    } // pass

  } // subshader
}
