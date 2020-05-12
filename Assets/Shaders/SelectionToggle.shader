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

Shader "Custom/SelectionToggle" {
  Properties {
    _Color ("Color", Color) = (0,0,0,1)
    _EmissionColor ("Emission Color", Color) = (1,1,1,1)
    _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
    _Shininess("Smoothness", Range(0.01,1)) = 0.13

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

      // Animate for a bump attract to get the user's attention.
      float duration = 4.0f;
      float t = fmod(_Time.w, duration);
      t = saturate(t -(duration - 1.0f)); // remap 0 : 1
	  t = (sin(t + 1)) / 2; // remap 0 : 1 : 0
      t = smoothstep(0, .75, t);

	  // separate float for animated UVs
	  float2 scrollUV = IN.uv_MainTex;
	  float scale = lerp(1.5, .8, t);
	  scrollUV -= 0.5;
	  scrollUV *= scale;
	  scrollUV += 0.5;

      float animated_tex = tex2D(_MainTex, scrollUV).w;
	  float3 static_tex = tex2D(_MainTex, IN.uv_MainTex).rgb;

	  // Combine textures!
	  float3 finalTex = animated_tex + static_tex;
	  float4 color = float4(finalTex, 1) * _EmissionColor;

      o.Emission = color.rgb;
      o.Albedo = finalTex * _Color;
      o.Smoothness = _Shininess;
    }
    ENDCG
  }
  FallBack "Diffuse"
}
