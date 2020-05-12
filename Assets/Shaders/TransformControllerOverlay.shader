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

Shader "Custom/TransformControllerOverlay" {
Properties {
  _Color ("Color", Color) = (0, 1, 1, 1)
  _BlackOutlineInflation ("Black Outline Inflation", Range (0, 1)) = 0.0
  _ColoredOutlineInflation ("Colored Outline Inflation", Range (0, 1)) = 0.0
  _BaseInflation ("Base Inflation", Float) = 0.01
}

Category {
  SubShader {
   Tags {"Queue"="AlphaTest+20"}

  Pass {
      Cull Front
      ZWrite On

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 3.0

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/ColorSpace.cginc"

      uniform float _BlackOutlineInflation;
      uniform float _Intensity;

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float3 normal : NORMAL;
      };

      struct v2f {
        float4 vertex : POSITION;
      };

      v2f vert (appdata_t v) {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex + float4(v.normal * _BlackOutlineInflation * _Intensity,0));
        return o;
      }

      fixed4 frag (v2f i) : COLOR {
        return float4(0,0,0,1);
      }
      ENDCG
    }

  Pass {
      Cull Front
    ZWrite On

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 3.0

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/ColorSpace.cginc"

      uniform float _ColoredOutlineInflation;
      uniform float _BaseInflation;

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float3 normal : NORMAL;
      };

      struct v2f {
        float4 vertex : POSITION;
      };

      uniform float4 _Color;
      uniform float _Intensity;

      v2f vert (appdata_t v) {
        v2f o;
        _ColoredOutlineInflation += _BaseInflation * abs(sin(_Time.w*2));
        o.vertex = UnityObjectToClipPos(v.vertex + float4(v.normal * _ColoredOutlineInflation * _Intensity,0));
        return o;
      }

      fixed4 frag (v2f i) : COLOR {
        _Color = GetAnimatedSelectionColor(_Color);
        return float4(_Color.xyz,1);
      }
      ENDCG
    }
  }
}
}
