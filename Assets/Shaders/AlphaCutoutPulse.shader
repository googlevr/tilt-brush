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

Shader "Custom/AlphaCutoutPulse" {
  Properties {
	_Color("Main Color", Color) = (1,1,1,1)
	_PulseColor("Pulse Color", Color) = (1,1,1,1)
	_PulseSpeed ("Pulse Speed", Float) = 0
    _MainTex ("Texture", 2D) = "white" {}
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  }
  SubShader {
    Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
    LOD 100
    Cull Off
    CGPROGRAM
    #pragma surface surf Lambert nofog

	fixed4 _Color;
	fixed4 _PulseColor;
    float _PulseSpeed;
	sampler2D _MainTex;
	fixed _Cutoff;

    struct Input {
      float2 uv_MainTex;
    };

    void surf (Input IN, inout SurfaceOutput o) {
      float t = abs(sin(_Time.y * _PulseSpeed));
      fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
      c *= lerp(1.0f, _PulseColor, t) * _Color;
	  o.Albedo = c.rgb;
      o.Emission = c.rgb;
      if (c.a < _Cutoff) {
        discard;
      }
      o.Alpha = 1;
    }
    ENDCG
  }
  FallBack "Transparent/Cutout/VertexLit"
}
