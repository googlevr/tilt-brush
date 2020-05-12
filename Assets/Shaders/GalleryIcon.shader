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

Shader "Custom/GalleryIcon" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Texture", 2D) = "white" {}
    _FadeIn("FadeIn", Range(0, 1)) = 1
    _Aspect("Aspect Ratio", Float) = 1
  }
  SubShader {
    Tags { "RenderType"="Opaque" }
    LOD 100

    CGPROGRAM
    #pragma surface surf Lambert nofog
    #include "Assets/Shaders/Include/Hdr.cginc"

    sampler2D _MainTex;
    float _FadeIn;
    float _Aspect;
    fixed4 _Color;

    struct Input {
      float2 uv_MainTex;
    };

    void surf (Input IN, inout SurfaceOutput o) {
      IN.uv_MainTex -= 0.5;

      // Landscape format images
      if (_Aspect > 1.0) {
          IN.uv_MainTex.x /= _Aspect;
      }

      // Portrait format images
      else {
          IN.uv_MainTex.y *= _Aspect;
      }

      IN.uv_MainTex += 0.5;

      fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
     float vignette = pow( abs(IN.uv_MainTex - .5) * 1.5, 2);
     // Apply a subtle vignette to thumbnails with dark values.
      o.Emission = lerp(c.rgb, max(c.rgb, 0.15), saturate(vignette));
      o.Alpha = c.a;

      //Put t in .5:1 range
      float t = clamp(_FadeIn, 0.0, 1.0) * .5 + .5;
      //tints unloaded files gray
      o.Emission = lerp(0.5f, o.Emission * _Color, t);
    }
    ENDCG
  }
  FallBack "Diffuse"
}
