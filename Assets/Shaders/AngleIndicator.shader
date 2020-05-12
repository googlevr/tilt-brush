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

Shader "Custom/AngleIndicator" {
Properties {
  _Color ("Main Color", Color) = (0, 1, 1, 1)
}

Category {

  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend SrcAlpha One
  AlphaTest Greater .01
  ColorMask RGB
  Cull Off Lighting Off ZWrite Off Fog { Color (0, 0, 0, 0) }

  SubShader {
    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 3.0

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/ColorSpace.cginc"

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float3 normal : NORMAL;
        float4 texcoord : TEXCOORD0;
      };

      struct v2f {
        float4 vertex : POSITION;
        float3 uv : TEXCOORD0;

      };

      v2f vert (appdata_t v) {
        v2f o;

        float age = _Time.y;

        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = float4(v.texcoord.xy, 0, 0);
        return o;
      }

      uniform float4 _Color;
      uniform float _Angle;
      uniform float _CircleCutoutSize;

      fixed4 frag (v2f i) : COLOR {
        const float PI = 3.1415926535;
        float x = i.uv.x - 0.5;
        float y = i.uv.y - 0.5;
        float withinAngle = atan2(x, y) < _Angle - PI ? 1 : 0;
        float distance = sqrt(x * x + y * y);
        x = -x - .5 * sin(_Angle);
        y = -y - .5 * cos(_Angle);
        float cutoutDistance = 16 * (x * x + y * y);
        float withinCircle = distance < .5 && cutoutDistance > _CircleCutoutSize * _CircleCutoutSize ? 1 : 0;
        float grid = 1 - 2 * abs((distance * 6 % 1 + 1) % 1 - .5);
        grid = grid > .98 ? 1 : 0;
        return fixed4((0.05 + grid) * withinAngle * withinCircle * _Color.rgb, 1);
      }
      ENDCG
    }
  }
}
}
