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

Shader "Custom/ControllerResetPad" {
  Properties {
    _Color ("Color", Color) = (0,0,0,1)
    _EmissionColor ("Emission Color", Color) = (1,1,1,1)
    _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
    _Shininess ("Smoothness", Range(0.01,1)) = 0.13

  }
  SubShader {
    Tags { "RenderType"="Opaque" }

    CGPROGRAM
    #pragma surface surf Standard nofog
    #pragma target 3.0

    sampler2D _MainTex;

    struct Input {
    float2 uv_MainTex;
    float3 worldPos;
    };

    half _Shininess;
    fixed4 _Color;
    fixed4 _EmissionColor;
    float4 _AudioReactiveColor;

    void surf (Input IN, inout SurfaceOutputStandard o) {


      float2 UVs = IN.uv_MainTex;
      o.Smoothness = _Shininess;

      //
      // Animate an attract spin effect to attract users attention
      //
      float duration = 5.0f;
      float t = fmod(_Time.w, duration);
      t = saturate(t - (duration - 1.0f)); // remap 0 : 1
      t = sin( t * (3.14159 / 2.0f));
      float amount = 2 * 3.14159 * t;
      amount += .4f;
      float s = sin ( amount );
        float c = cos ( amount );
      float2x2 rotationMatrix = float2x2( c, -s, s, c);
      IN.uv_MainTex.xy = mul(IN.uv_MainTex.xy - 0.5, rotationMatrix ) + 0.5;
      float3 animated_tex = tex2D(_MainTex, IN.uv_MainTex).rgb;

      //
      // Apply the "person" icon
      //
      duration = 5.0f;
      t = fmod(_Time.w + 2, duration);
      t = saturate(t / duration); // remap 0 : 1
      t = (sin(t * 3.14159) + 1)/2; // remap 0 : 1 : 0
      t = 1 - pow(t,100);
      t = smoothstep(0,.9,t);

      float scale = lerp(1.2, .8, t);
      UVs -= 0.5;
      UVs *= scale;
      UVs += 0.5;
      // shift down a bit
      UVs.y += .035;
      float person_tex = tex2D(_MainTex, UVs).w;
      float person_tex_outline = tex2D(_MainTex, (UVs - .5) * .8f + .5).w;

      // Combine the animated and non animated texture elements
      float3 tex = animated_tex * (1 - person_tex_outline) + person_tex;

      // Outputs
      float4 color = float4(tex, 1) * _EmissionColor;

      o.Emission = color.rgb;
      o.Albedo = tex * _Color;
    }
    ENDCG
  }
  FallBack "Diffuse"
}
