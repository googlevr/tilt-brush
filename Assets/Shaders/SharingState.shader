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

Shader "Custom/SharingState" {
Properties {
  _Color("Color", Color) = (0,0,0,1)
  _LineColor("Line Color", Color) = (78,217,255,255)
  _MainTex("Base (RGB) TransGloss (A)", 2D) = "white" {}
  _BGTex("BGTex", 2D) = "white" {}
  _Ratio("Animation Ratio", Range(0,1)) = 0
}

SubShader {
  Tags { "RenderType"="Opaque" }
  LOD 100

  Pass {
    CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    #include "UnityCG.cginc"
    #include "Assets/Shaders/Include/Hdr.cginc"

    sampler2D _MainTex;
    sampler2D _BGTex;
    float4 _MainTex_ST;
    float4 _BGTex_ST;
    float4 _Color;
    float4 _LineColor;
    float _Ratio;
    float duration;

  struct appdata_t {
    float4 vertex : POSITION;
    float3 color : COLOR;
    float2 texcoord : TEXCOORD0;
  };

  struct v2f {
    float4 vertex : SV_POSITION;
    float4 color : COLOR;
    float2 texcoord : TEXCOORD0;
  };

  v2f vert (appdata_t v) {
    v2f o;

    // Hold animation 'til _Ratio < 0
    if (_Ratio == 0) {
      duration = 0.0f;
    }

    else {
      duration = 2.25f;
    }

    o.vertex = UnityObjectToClipPos(v.vertex);
    o.color = duration;
    o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
    return o;
  }

  fixed4 frag (v2f i) : COLOR {

    float duration = i.color;
    float2 UVs = i.texcoord;
    float t = fmod(_Time.y, duration * 2);
    t = saturate(t / duration);

    float angle = atan2(i.texcoord.x - 0.5, -i.texcoord.y + 0.5); //angle should go between 0, and 2pi
    angle = (1 - ((angle + 3.14159) / (2 * 3.14159))); // remap 0 : 1

    // - 0.2 and 0.5 here are values forcing the animation to begin and end where we want it
    float4 smoothstepColor = (smoothstep(t, t - 0.2, angle * 0.5));

    // Only apply TB cyan to the dotted line using _LineColor
    float3 animated_tex = tex2D(_MainTex, i.texcoord).rgb * (smoothstepColor * _LineColor);

    //
    // Apply the center icon (the alpha mask)
    //
    t = fmod(_Time.y, duration * 2.25);
    t = saturate(t / duration); // remap 0 : 1
    t = (sin(t * 3.14159) + 1) / 2; // remap 0 : 1 : 0
    t = 1 - pow(t,100);
    t = smoothstep(0,.9,t);

    float scale = lerp(1.2, 1, t);
    UVs -= 0.5;
    UVs *= scale;
    UVs += 0.5;

    float center_tex = tex2D(_MainTex, UVs).w;
    float bg_tex = tex2D(_BGTex, i.texcoord);

    // Combine the animated and non animated texture elements
    float3 tex = animated_tex + center_tex;

    // We dim tex to .75 to match the non _Activated state in PanelButton.shader
    tex *= .75;

    // _Color is multiplied here to allow GUI-style fade-in, fade out
    float4 color = (float4(tex, 1) + bg_tex) * _Color;
    return encodeHdr(color);
  }
    ENDCG
  }
}

}

