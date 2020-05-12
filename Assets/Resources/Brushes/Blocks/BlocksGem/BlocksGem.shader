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

//
// Tilt Brush variant of the Blocks gem shader
//
Shader  "Blocks/BlocksGem"  {
  Properties {
    _Color ("Color", Color) = (1,1,1,1)
    _Shininess ("Shininess", Range(0,1)) = 0.8
    _RimIntensity ("Rim Intensity", Range(0,1)) = .2
    _RimPower ("Rim Power", Range(0,16)) = 5
    _Frequency ("Frequency", Float) = 1
    _Jitter ("Jitter", Float) = 1
  }

  SubShader {

  //
  // Voronoi implementation taken from
  // https://github.com/Scrawk/GPU-Voronoi-Noise
  // (MIT License)
  //

  Tags { "RenderType"="Transparent" "Queue"="Transparent"}
  LOD 200

  Blend One SrcAlpha
  Zwrite Off
  Cull Back

  CGPROGRAM
  #pragma surface surf StandardSpecular vertex:vert fullforwardshadows nofog
  #pragma target 3.0
  #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
  #pragma multi_compile __ SELECTION_ON HIGHLIGHT_ON

  #include "Assets/Shaders/Include/Brush.cginc"
  #include "Assets/Shaders/Include/MobileSelection.cginc"

  uniform float _Frequency;
  uniform float _Jitter;

  //1/7
  #define K 0.142857142857
  //3/7
  #define Ko 0.428571428571

  #define OCTAVES 1

  float3 mod(float3 x, float y) { return x - y * floor(x/y); }
  float2 mod(float2 x, float y) { return x - y * floor(x/y); }

  // Permutation polynomial: (34x^2 + x) mod 289
  float3 Permutation(float3 x)
  {
    return mod((34.0 * x + 1.0) * x, 289.0);
  }

  float2 inoise(float3 P, float jitter)
  {
    float3 Pi = mod(floor(P), 289.0);
    float3 Pf = frac(P);
    float3 oi = float3(-1.0, 0.0, 1.0);
    float3 of = float3(-0.5, 0.5, 1.5);
    float3 px = Permutation(Pi.x + oi);
    float3 py = Permutation(Pi.y + oi);

    float3 p, ox, oy, oz, dx, dy, dz;
    float2 F = 1e6;

    for(int i = 0; i < 3; i++) {
      for(int j = 0; j < 3; j++) {
        p = Permutation(px[i] + py[j] + Pi.z + oi); // pij1, pij2, pij3

        ox = frac(p*K) - Ko;
        oy = mod(floor(p*K),7.0)*K - Ko;

        p = Permutation(p);

        oz = frac(p*K) - Ko;

        dx = Pf.x - of[i] + jitter*ox;
        dy = Pf.y - of[j] + jitter*oy;
        dz = Pf.z - of + jitter*oz;

        float3 d = dx * dx + dy * dy + dz * dz; // dij1, dij2 and dij3, squared

        //Find lowest and second lowest distances
        for(int n = 0; n < 3; n++) {
          if(d[n] < F[0]) {
            F[1] = F[0];
            F[0] = d[n];
          } else if(d[n] < F[1]) {
            F[1] = d[n];
          }
        }
      }
    }
    return F;
  }

  // fractal sum, range -1.0 - 1.0
  float2 fBm_F0(float3 p, int octaves)
  {
    float freq = _Frequency, amp = 0.5;
    float2 F = inoise(p * freq, _Jitter) * amp;
    return F;
  }

  struct Input {
    float2 uv_MainTex;
    float3 localPos;
    float3 worldRefl;
    float3 viewDir;

    INTERNAL_DATA
  };

  half _Shininess;
  half _RimIntensity;
  half _RimPower;
  fixed4 _Color;

  void vert (inout appdata_full v, out Input o) {
   UNITY_INITIALIZE_OUTPUT(Input,o);
   PrepForOds(v.vertex);
   o.localPos = v.vertex.xyz;
 }

  void surf(Input IN, inout SurfaceOutputStandardSpecular o) {
    const float kPerturbIntensity = 10;
    float2 F = fBm_F0(IN.localPos, OCTAVES);
    float gem = (F.y - F.x);

    // Perturb normal with voronoi cells

    // Note: can't do "o.Normal += perturb" because tangent-space o.Normal
    // comes in as (0, 0, 0), not (0, 0, 1)
    o.Normal = (float3(0, 0, 1) +
                kPerturbIntensity * float3(ddy(gem), ddx(gem), 0));

    o.Albedo = 0;

    // Artifical diffraction highlights to simulate what I see in blocks. Tuned to taste.
    half3 refl = clamp(WorldReflectionVector (IN, o.Normal) + gem, -1.0,1.0);
    float3 colorRamp = float3(1,.3,0)*sin(refl.x * 30) + float3(0,1,.5)*cos(refl.y * 37.77) + float3(0,0,1)*sin(refl.z*43.33);

    // Use the voronoi for a specular mask
    half mask = saturate((1 - gem) + .25);
    o.Specular = _Color.rgb + colorRamp*.1;
    o.Smoothness = _Shininess;

    // Artificial rim lighting
    o.Emission =  (pow(1 - saturate(dot(IN.viewDir, o.Normal)), _RimPower)) * _RimIntensity;
    SURF_FRAG_MOBILESELECT(o);
  }
    ENDCG
} // end subshader

  FallBack "Diffuse"
} // end shader
