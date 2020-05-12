//
// KinoVignette - Natural vignetting effect
//
// Copyright (C) 2015 Keijiro Takahashi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

// Modified by the Tilt Brush Authors.

Shader "FX/Kino/VignetteAndChromaticAberration"
{
    Properties
    {
        _MainTex ("-", 2D) = "" {}
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float2 _Aspect;
    float _Falloff;
    float _ChromaticAberration;
    float4 _MainTex_TexelSize;

    float4 frag(v2f_img i) : SV_Target
    {
        float2 coord = (i.uv - 0.5) * 2; // * _Aspect;
        float2 coordDP = dot(coord, coord);
        float rf = sqrt(coordDP) * _Falloff;
        float rf2_1 = rf * rf + 1.0;
        float e = 1.0 / (rf2_1 * rf2_1);
        
        float2 guv = i.uv - _MainTex_TexelSize.xy * _ChromaticAberration * coord * coordDP;
        float4 srcg = tex2D(_MainTex, guv) * e;
        float4 srcrb = tex2D(_MainTex, i.uv) * e;
        
        return float4(srcrb.r, srcg.g, srcrb.b, 1); // src.a);
    }

    ENDCG

    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            ENDCG
        }
    }
}
