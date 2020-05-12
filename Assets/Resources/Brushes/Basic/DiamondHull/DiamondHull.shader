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

Shader "Brush/Special/DiamondHull" {
  Properties {
    _MainTex("Texture", 2D) = "white" {}
  }

  SubShader {
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Blend One One
    Cull off ZWrite Off
    Fog{ Mode Off }

    CGPROGRAM
      #pragma target 4.0
      #pragma surface surf StandardSpecular vertex:vert nofog
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #pragma multi_compile __ SELECTION_ON
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/ThirdParty/Shaders/Noise.cginc"
      #include "Assets/Shaders/Include/MobileSelection.cginc"

      sampler2D _MainTex;

      struct Input {
        float4 color : Color;
        float2 tex : TEXCOORD0;
        float3 viewDir;
        float3 worldPos;
        float3 worldNormal;
        INTERNAL_DATA
      };

      // Amplitude reflection coefficient (s-polarized)
      float rs(float n1, float n2, float cosI, float cosT) {
        return (n1 * cosI - n2 * cosT) / (n1 * cosI + n2 * cosT);
      }

      // Amplitude reflection coefficient (p-polarized)
      float rp(float n1, float n2, float cosI, float cosT) {
        return (n2 * cosI - n1 * cosT) / (n1 * cosT + n2 * cosI);
      }

      // Amplitude transmission coefficient (s-polarized)
      float ts(float n1, float n2, float cosI, float cosT) {
        return 2 * n1 * cosI / (n1 * cosI + n2 * cosT);
      }

      // Amplitude transmission coefficient (p-polarized)
      float tp(float n1, float n2, float cosI, float cosT) {
        return 2 * n1 * cosI / (n1 * cosT + n2 * cosI);
      }

      // cosI is the cosine of the incident angle, that is, cos0 = dot(view angle, normal)
      // lambda is the wavelength of the incident light (e.g. lambda = 510 for green)
      // http://www.gamedev.net/page/resources/_/technical/graphics-programming-and-theory/thin-film-interference-for-computer-graphics-r2962
      float thinFilmReflectance(float cos0, float lambda, float thickness, float n0, float n1, float n2) {
        float PI = 3.1415926536;

        // Phase change terms.
        const float d10 = lerp(PI, 0, n1 > n0);
        const float d12 = lerp(PI, 0, n1 > n2);
        const float delta = d10 + d12;

        // Cosine of the reflected angle.
        const float sin1 = pow(n0 / n1, 2) * (1 - pow(cos0, 2));

        // Total internal reflection.
        if (sin1 > 1) return 1.0;
        const float cos1 = sqrt(1 - sin1);

        // Cosine of the final transmitted angle, i.e. cos(theta_2)
        // This angle is for the Fresnel term at the bottom interface.
        const float sin2 = pow(n0 / n2, 2) * (1 - pow(cos0, 2));

        // Total internal reflection.
        if (sin2 > 1) return 1.0;

        const float cos2 = sqrt(1 - sin2);

        // Reflection transmission amplitude Fresnel coefficients.
        // rho_10 * rho_12 (s-polarized)
        const float alpha_s = rs(n1, n0, cos1, cos0) * rs(n1, n2, cos1, cos2);
        // rho_10 * rho_12 (p-polarized)
        const float alpha_p = rp(n1, n0, cos1, cos0) * rp(n1, n2, cos1, cos2);

        // tau_01 * tau_12 (s-polarized)
        const float beta_s = ts(n0, n1, cos0, cos1) * ts(n1, n2, cos1, cos2);
        // tau_01 * tau_12 (p-polarized)
        const float beta_p = tp(n0, n1, cos0, cos1) * tp(n1, n2, cos1, cos2);

        // Compute the phase term (phi).
        const float phi = (2 * PI / lambda) * (2 * n1 * thickness * cos1) + delta;

        // Evaluate the transmitted intensity for the two possible polarizations.
        const float ts = pow(beta_s, 2) / (pow(alpha_s, 2) - 2 * alpha_s * cos(phi) + 1);
        const float tp = pow(beta_p, 2) / (pow(alpha_p, 2) - 2 * alpha_p * cos(phi) + 1);

        // Take into account conservation of energy for transmission.
        const float beamRatio = (n2 * cos2) / (n0 * cos0);

        // Calculate the average transmitted intensity (polarization distribution of the
        // light source here. If unknown, 50%/50% average is generally used)
        const float t = beamRatio * (ts + tp) / 2;

        // Derive the reflected intensity.
        return 1 - t;
      }

      float3 GetDiffraction(float3 thickTex, float3 I, float3 N) {
        const float thicknessMin = 250;
        const float thicknessMax = 400;
        const float nmedium = 1;
        const float nfilm = 1.3;
        const float ninternal = 1;

        const float cos0 = abs(dot(I, N));

        //float3 thickTex = texture(thickness, u, v);
        const float t = (thickTex[0] + thickTex[1] + thickTex[2]) / 3.0;
        const float thick = thicknessMin*(1.0 - t) + thicknessMax*t;

        const float red = thinFilmReflectance(cos0, 650, thick, nmedium, nfilm, ninternal);
        const float green = thinFilmReflectance(cos0, 510, thick, nmedium, nfilm, ninternal);
        const float blue = thinFilmReflectance(cos0, 475, thick, nmedium, nfilm, ninternal);

        return float3(red, green, blue);
      }

      void vert (inout appdata_full v, out Input o) {
        PrepForOds(v.vertex);
        o.color = TbVertToSrgb(o.color);
        UNITY_INITIALIZE_OUTPUT(Input, o);
        o.tex = v.texcoord.xy;
      }

      // Input color is srgb
      void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
        // Hardcode some shiny specular values
        o.Smoothness = .8;
        o.Albedo = IN.color * .2;

        // Calculate rim
        half rim = 1.0 - abs(dot(normalize(IN.viewDir), IN.worldNormal));
        rim *= 1-pow(rim,5);

        const float3 I = (_WorldSpaceCameraPos - IN.worldPos);
        rim = lerp(rim, 150,
              1 - saturate(abs(dot(normalize(I), IN.worldNormal)) / .1));

        float3 diffraction = tex2D(_MainTex, half2(rim + _Time.x * .3 + o.Normal.x, rim + o.Normal.y)).xyz;
        diffraction = GetDiffraction(diffraction, o.Normal, normalize(IN.viewDir));

        o.Emission = rim * IN.color * diffraction * .5 + rim * diffraction * .25;
        SURF_FRAG_MOBILESELECT(o);
        o.Specular = SrgbToNative(IN.color).rgb * clamp(diffraction, .0, 1);
      }
    ENDCG
  }
}
