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

Shader "Brush/Special/LightWire" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
    _Shininess ("Shininess", Range (0.01, 1)) = 0.078125
    _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
    _BumpMap ("Normalmap", 2D) = "bump" {}
  }
  SubShader {
    Cull Back
    CGPROGRAM
    #pragma target 4.0
    #pragma surface surf StandardSpecular vertex:vert noshadow
    #pragma multi_compile __ AUDIO_REACTIVE
    #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
    #pragma multi_compile __ SELECTION_ON
    #include "Assets/Shaders/Include/Brush.cginc"
    #include "Assets/Shaders/Include/MobileSelection.cginc"

    struct Input {
      float2 uv_MainTex;
      float4 color : Color;
    };

    sampler2D _MainTex;
    sampler2D _BumpMap;
    fixed4 _Color;
    half _Shininess;

    void vert (inout appdata_full v, out Input o) {
      UNITY_INITIALIZE_OUTPUT(Input, o);
      PrepForOds(v.vertex);
      v.color = TbVertToSrgb(v.color);

      // Radius is stored in texcoord (used to be tangent.w)
      float radius = v.texcoord.z;

      float t;
      float envelope = sin ( fmod ( v.texcoord.x * 2, 1.0f) * 3.14159);
      float lights = envelope < .15 ? 1 : 0;

      radius *= 0.9;
      v.vertex.xyz += v.normal * lights * radius;
    }

    float3 SrgbToNative3(float3 color) {
      return SrgbToNative(float4(color, 1)).rgb;
    }

    // Input color is srgb
    void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
      float envelope = sin ( fmod ( IN.uv_MainTex.x*2, 1.0f) * 3.14159);
      float lights = envelope < .1 ? 1 : 0;
      float border = abs(envelope - .1) < .01 ? 0 : 1;
      o.Specular =   .3 - lights * .15;
      o.Smoothness = .3 + lights * .3;

      float t;
#ifdef AUDIO_REACTIVE
      t = _BeatOutputAccum.x*10;
#else
      t = _Time.w;
#endif

      if (lights) {
        int colorindex = fmod(IN.uv_MainTex.x*2 + 0.5, 3);
        if (colorindex == 0) IN.color.rgb = IN.color.rgb * float3(.2,.2,1);
        else if (colorindex == 1) IN.color.rgb = IN.color.rgb * float3(1,.2,.2);
        else IN.color.rgb = IN.color.rgb * float3(.2,1,.2);

        float lightindex =  fmod(IN.uv_MainTex.x*2 + .5,7);
        float timeindex = fmod(t, 7);
        float delta = abs(lightindex - timeindex);
        float on = 1 - saturate(delta*1.5);
        IN.color = bloomColor(IN.color * on, .7);
      }

      o.Albedo = (1-lights) *  IN.color.rgb * .2;
      o.Albedo *= border;
      o.Specular *= border;

#ifdef AUDIO_REACTIVE
      IN.color.rgb = IN.color.rgb * .25 + IN.color.rgb*_BeatOutput.x * .75;
#endif
      o.Emission += lights * IN.color.rgb;

      o.Albedo   = SrgbToNative3(o.Albedo);
      o.Emission = SrgbToNative3(o.Emission);
      o.Specular = SrgbToNative3(o.Specular);

      SURF_FRAG_MOBILESELECT(o);
    }
    ENDCG
  }

  FallBack "Transparent/Cutout/VertexLit"
}
