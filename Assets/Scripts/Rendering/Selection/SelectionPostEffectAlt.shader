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

Shader "Hidden/SelectionPostEffectAlt"
{
  Properties
  {
    _MainTex ("Texture", 2D) = "white" {}
    _MinIntensity("Minimum Intensity", Float) = .3
  }

  CGINCLUDE
    #include "UnityCG.cginc"
    #include "Assets/Shaders/Include/Brush.cginc"
    #include "Assets/Shaders/Include/Hdr.cginc"

    #pragma target 3.0
    #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE

    sampler2D _MainTex;
    uniform half4 _MainTex_TexelSize;
    uniform float _BlurSize;

    struct v2f_simple
    {
      float4 pos : SV_POSITION;
      half4 uv : TEXCOORD0;

      #if UNITY_UV_STARTS_AT_TOP
        half4 uv2 : TEXCOORD1;
      #endif
    };

    struct v2f_tap
    {
      float4 pos : SV_POSITION;
      half4 uv20 : TEXCOORD0;
      half4 uv21 : TEXCOORD1;
      half4 uv22 : TEXCOORD2;
      half4 uv23 : TEXCOORD3;
    };

    v2f_tap vert4Tap ( appdata_img v )
    {
      v2f_tap o;
      o.pos =  UnityObjectToClipPos (v.vertex);
      o.uv20 = half4(v.texcoord.xy + _MainTex_TexelSize.xy, 0.0, 0.0);
      o.uv21 = half4(v.texcoord.xy + _MainTex_TexelSize.xy * half2(-0.5h,-0.5h), 0.0, 0.0);
      o.uv22 = half4(v.texcoord.xy + _MainTex_TexelSize.xy * half2(0.5h,-0.5h), 0.0, 0.0);
      o.uv23 = half4(v.texcoord.xy + _MainTex_TexelSize.xy * half2(-0.5h,0.5h), 0.0, 0.0);

      return o;
    }

    fixed4 fragDownsample ( v2f_tap i ) : COLOR
    {
      float4 color = float4(0, 0, 0, 0);
      color += decodeHdr(tex2D(_MainTex, i.uv20.xy));
      color += decodeHdr(tex2D(_MainTex, i.uv21.xy));
      color += decodeHdr(tex2D(_MainTex, i.uv22.xy));
      color += decodeHdr(tex2D(_MainTex, i.uv23.xy));
      return max(color/4, 0);
    }

    static const half curve[7] = { 0.0205, 0.0855, 0.232, 0.324, 0.232, 0.0855, 0.0205 };

    static const half4 curve4[7] = { half4(0.0205,0.0205,0.0205,0),
                                     half4(0.0855,0.0855,0.0855,0),
                                     half4(0.232,0.232,0.232,0),
                                     half4(0.324,0.324,0.324,1),
                                     half4(0.232,0.232,0.232,0),
                                     half4(0.0855,0.0855,0.0855,0),
                                     half4(0.0205,0.0205,0.0205,0) };


    struct v2f_withBlurCoords8
    {
      float4 pos : SV_POSITION;
      half4 uv : TEXCOORD0;
      half4 offs : TEXCOORD1;
    };

    v2f_withBlurCoords8 vertBlurHorizontal (appdata_img v)
    {
      v2f_withBlurCoords8 o;
      o.pos = UnityObjectToClipPos (v.vertex);

      o.uv = half4(v.texcoord.xy,1,1);
      o.offs = half4(_MainTex_TexelSize.xy * half2(1.0, 0.0) * _BlurSize,1,1);

      return o;
    }

    v2f_withBlurCoords8 vertBlurVertical (appdata_img v)
    {
      v2f_withBlurCoords8 o;
      o.pos = UnityObjectToClipPos (v.vertex);

      o.uv = half4(v.texcoord.xy,1,1);
      o.offs = half4(_MainTex_TexelSize.xy * half2(0.0, 1.0) * _BlurSize,1,1);

      return o;
    }

    half4 fragBlur8 ( v2f_withBlurCoords8 i ) : COLOR
    {
      half2 uv = i.uv.xy;
      half2 netFilterWidth = i.offs.xy;
      half2 coords = uv - netFilterWidth * 3.0;

      half4 color = 0;
      for( int l = 0; l < 7; l++ )
      {
        half4 tap = tex2D(_MainTex, coords);
        color += tap * curve4[l];
        coords += netFilterWidth;
      }
      return color;
    }

  ENDCG


  SubShader
  {
    // No culling or depth
    ZTest Always Cull Off ZWrite Off

    Pass // Pass 0 - Outline Composite
    {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/ColorSpace.cginc"

      struct appdata
      {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };

      struct v2f
      {
        float2 uv : TEXCOORD0;
        float4 vertex : SV_POSITION;
      };

      v2f vert (appdata v)
      {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        return o;
      }

      sampler2D _SelectionMask;
      sampler2D _BlurredSelectionMask;

      float4 frag (v2f i) : SV_Target
      {
        float4 source = tex2D(_MainTex, i.uv);
        float4 mask = tex2D(_SelectionMask, i.uv);
        float4 blurredmask = tex2D(_BlurredSelectionMask, i.uv);
        float4 selectionColor = float4(1,1,1,1);
        float stereoHueOffset = .6;

        // Selecting color for left eye
        if(unity_StereoEyeIndex == 0){
          selectionColor = float4(sv_to_rgb( hue06_to_base_rgb((_Time.g*3)%6), .5, 1), 1);
        }
        // Offsetting color for right eye
        else if(unity_StereoEyeIndex == 1){
          selectionColor = float4(sv_to_rgb( hue06_to_base_rgb((_Time.g*3 + stereoHueOffset)%6), .5, 1), 1);
        }

        // Create an outline
        float upperthreshold = smoothstep(0.99, 1, mask.r);
        float lowerthreshold = 1-smoothstep(0, 0.01, mask.r);
        // Combine, then sharpen the mask so we can get an outline
        float4 edgemask = 1 - saturate(upperthreshold + lowerthreshold);

        // Selected strokes have HDR values, nuke 'em so they don't bloom
        float4 mixedRangeSource = lerp(source, saturate(source), mask.r);

        float4 interiorColor = mixedRangeSource * .75 + selectionColor * .1;
        float4 modifiedInteriorColor = lerp(mixedRangeSource, interiorColor, mask.r);

        return encodeHdr(lerp(modifiedInteriorColor, selectionColor, edgemask));
      }
      ENDCG
    }

    Pass 	// 1 Downsample
    {
      CGPROGRAM
      #pragma vertex vert4Tap
      #pragma fragment fragDownsample
      #pragma fragmentoption ARB_precision_hint_fastest
      ENDCG
    }

    Pass 	// 2 Blur Vertical
    {
      CGPROGRAM
      #pragma vertex vertBlurVertical
      #pragma fragment fragBlur8
      #pragma fragmentoption ARB_precision_hint_fastest
      ENDCG
    }

    Pass 	// 3 Blur Horizontal
    {
      CGPROGRAM
      #pragma vertex vertBlurHorizontal
      #pragma fragment fragBlur8
      #pragma fragmentoption ARB_precision_hint_fastest
      ENDCG
    }
  }
}
