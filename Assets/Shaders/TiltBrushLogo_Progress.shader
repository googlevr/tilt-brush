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

Shader "Custom/TiltBrushLogo_Progress" {
  Properties {
    _Progress ("Progress", Float) = 0
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Texture", 2D) = "white" {}
  }
  SubShader {
    Tags { "RenderType"="Transparent" }
    Blend SrcAlpha OneMinusSrcAlpha
    LOD 100
    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      
      #include "UnityCG.cginc"
  
      float _Progress;
      sampler2D _MainTex;
      float4 _MainTex_ST;
      fixed4 _Color;
  
      struct appdata {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };
  
      struct v2f {
        float2 uv_MainTex : TEXCOORD0;
        float4 vertex : SV_POSITION;
      };
  
      // Creates a smooth in and out line from min to max over the range t = [0, 1].
      float smooth(float min, float max, float t) {
        const float PI = 3.14159265358979;
  
        // Clamp min and max to be within a certain limit of each other.
        float limit = 3;
        min = t < 0.5 ? min : max + limit * (saturate((min - max) / limit + 0.5) - 0.5);
        max = t > 0.5 ? max : min + limit * (saturate((max - min) / limit + 0.5) - 0.5);
  
        float inCurve = pow(cos(t * PI), .3);
        float outCurve = -pow(abs(cos(t * PI)), .5);
        return t < 0.5 ?
            (-0.5 * inCurve + .5) * (max - min) + min :
            (-0.5 * outCurve + .5) * (max - min) + min;
      }
      
      v2f vert (appdata v) {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv_MainTex = TRANSFORM_TEX(v.uv, _MainTex);
        return o;
      }
  
      fixed4 frag (v2f IN) : SV_Target {
        // Base theta is the rotational angle around the logo.
        const float TWO_PI = 2.0 * 3.14159265358979;
        const float PI_OVER_THREE = 3.14159265358979 / 3.0;
        float x = IN.uv_MainTex.x - 0.5;
        float y = IN.uv_MainTex.y - 0.5;
        float theta = fmod(atan2(x, y) + TWO_PI, TWO_PI);
  
        // Modify theta so that the first sixth of the progress sweeps across the first part of the
        // logo.
        const float A_MIN = 0.600;  // Left edge of the first part of the logo
        const float A_MAX = 1.100;  // Right edge of the first part of the logo
        const float B_MIN = 0.515;  // Top edge of the first part of the logo
        const float B_MAX = 0.682;  // Bottom edge of the first part of the logo
        const float cos_30 = 0.86602540378443864676372317075294;
        const float sin_30 = 0.5;
        float a = IN.uv_MainTex.x * sin_30 + IN.uv_MainTex.y * cos_30;
        a = (a - A_MIN) / (A_MAX - A_MIN);
        float b = IN.uv_MainTex.x * sin_30 + (1 - IN.uv_MainTex.y) * cos_30;
        b = (b - B_MIN) / (B_MAX - B_MIN);
  
        // Interpolate between the three different colors in the first 1/6th of the logo.
        const float edge1 = 1.0 / 18;
        const float factor1 = _Progress / edge1;
        const float edge2 = 1.0 / 9;
        const float factor2 = (_Progress - edge1) / (edge2 - edge1);
        const float factor2b = -5 * (1 - factor2) + 1 * factor2;
        const float edge3 = 1.0 / 6;
        const float factor3 = (_Progress - edge2) / (edge3 - edge2);
        float e1 = _Progress < edge1 ? 6.65 * (1 - factor1) + 5.00 * factor1 :
                   _Progress < edge2 ? 5.00 * (1 - factor2) + 3.30 * factor2 : 3.30 * (1 - factor3);
        float e2 = _Progress < edge1 ? 3.33 * (1 - factor1) + 3.30 * factor1 :
                   _Progress < edge2 ? 3.30 * (1 - factor2) + 0.00 * factor2 : 0.00 * (1 - factor3);
        float e3 = _Progress < edge1 ? 0.00 * (1 - factor1) + 0.00 * factor1 :
                   _Progress < edge2 ? 0.00 * (1 - factor2) + 1.60 * factor2 : 1.60 * (1 - factor3);
        float e4 = _Progress < edge1 ? 3.33 * (1 - factor1) + 5.00 * factor1 :
                   _Progress < edge2 ? 5.00 * (1 - factor2) + 1.60 * factor2 : 1.60 * (1 - factor3);
        float taper =
            b < 1.0 / 8 ? smooth(-e1 - 2, -e1, .5 + 4 * b) :
            b < 2.0 / 8 ? smooth(-e1, -e2, 4 * b - 0.5) :
            b < 3.0 / 8 ? smooth(-e1, -e2, 4 * b - 0.5) :
            b < 4.0 / 8 ? smooth(-e2, -e3, 4 * b - 1.5) :
            b < 5.0 / 8 ? smooth(-e2, -e3, 4 * b - 1.5) :
            b < 6.0 / 8 ? smooth(-e3, -e4, 4 * b - 2.5) :
            b < 7.0 / 8 ? smooth(-e3, -e4, 4 * b - 2.5) :
                          smooth(-e4, -e4 - 1, 4 * b - 3.5);
        taper *= 0.05;
  
        theta = a > 0 && a < 1 && b > 0 && b < 1
            ? (a + taper) * PI_OVER_THREE
            : theta;
  
        fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
        c *= _Color * theta < (_Progress * TWO_PI) ? 1 : 0.75;
  
        // Uncomment out the next two lines to test out the boundaries.
        //c.r += a > 0 && a < 1 ? .5 : 0;
        //c.g += b > 0 && b < 1 ? .5 : 0;
  
        return c;
      }
      ENDCG
    }
  }
  FallBack "Diffuse"
}
