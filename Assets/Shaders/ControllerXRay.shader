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

Shader "Custom/ControllerXRayEffect" {
Properties {
  _Color ("Color", Color) = (1, 1, 1, 1)
}

Category {
  SubShader {
  Pass {

    //
    // XXX: For an unknown reason, setting the Queue in the shader to be Overlay (or Overlay+N)
    // does not properly cause this geometry to render.  It flickers randomly between visible in none, one or both eyes.
    // If instead we set the Queue in the Material directly, it renders fine.  So for now, that's what we've done.
    //
    //
    // Tags { "Queue"="Overlay"  }
    //

    Blend SrcAlpha OneMinusSrcAlpha, Zero One
    ZTest Off
    ZWrite Off
    Fog { Mode Off }

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
      };

      struct v2f {
        float4 vertex : POSITION;
      };

    uniform float4 _Color;

      v2f vert (appdata_t v) {
        v2f o;
      o.vertex = UnityObjectToClipPos(v.vertex);
        return o;
      }

      fixed4 frag (v2f i) : COLOR {
     return _Color;
    }
      ENDCG
    }
  }
}

Fallback "Unlit/Diffuse"


}
