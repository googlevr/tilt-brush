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

Shader "Brush/Intro/Streamers" {
Properties {
  _MainTex ("Particle Texture", 2D) = "white" {}
  _Scroll1 ("Scroll1", Float) = 0
  _Scroll2 ("Scroll2", Float) = 0
  _DisplacementIntensity("Displacement", Float) = .1
    _EmissionGain ("Emission Gain", Range(0, 1)) = 0.5
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One // SrcAlpha One
  BlendOp Add, Min
  AlphaTest Greater .01
  ColorMask RGBA
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_particles
      #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
      #pragma target 3.0 // Required -> compiler error: too many instructions for SM 2.0

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"

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
        float4 worldPos : TEXCOORD1;
      };

      float4 _MainTex_ST;
      fixed _Scroll1;
      fixed _Scroll2;
      half _DisplacementIntensity;
      half _EmissionGain;
	  half _IntroDissolve;

      v2f vert (appdata_t v)
      {
        v.color = TbVertToSrgb(v.color);

        v2f o;
        o.worldPos = mul(unity_ObjectToWorld, v.vertex);
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        o.color = v.color * (1.0 - _IntroDissolve);
        return o;
      }

      float rand_1_05(in float2 uv)
      {
        float2 noise = (frac(sin(dot(uv ,float2(12.9898,78.233)*2.0)) * 43758.5453));
        return abs(noise.x + noise.y) * 0.5;
      }

      // Input color is srgb
      fixed4 frag (v2f i) : COLOR
      {
        // Create parametric flowing UV's
        half2 uvs = i.texcoord;
        float row_id = floor(uvs.y * 5);
        float row_rand = rand_1_05(row_id.xx);
        uvs.x += row_rand * 200;

        half2 sins = sin(uvs.x * half2(10,23) + _Time.z * half2(5,3));
        uvs.y = 5 * uvs.y + dot(half2(.05, -.05), sins);


        // Scrolling UVs
        uvs.x *= .5 + row_rand * .3;
        uvs.x -= _Time.y * (1 + fmod(row_id * 1.61803398875, 1) - 0.5);

        // Sample final texture
        half4 tex = tex2D(_MainTex, uvs);

        // Boost hot spot in texture
        tex += pow(tex, 2) * 55;

        // Clean up border pixels filtering artifacts
        tex *= fmod(uvs.y,1); // top edge
        tex *= fmod(uvs.y,1); // top edge
        tex *= 1 - fmod(uvs.y,1); // bottom edge
        tex *= 1 - fmod(uvs.y,1); // bottom edge

        float4 color = i.color * tex * exp(_EmissionGain * 5.0f);
        color = encodeHdr(color.rgb * color.a);
        color = SrgbToNative(color);
        return color;
      }
      ENDCG
    }
  }
}
}
