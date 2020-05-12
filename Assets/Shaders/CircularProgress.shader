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

// Circular progress bar.
Shader "Unlit/CircularProgress"
{
  Properties
  {
    _Progress ("Progress", float) = 0.33 // valid ranges: 0 to 1
    _Inner ("Inner", float) = 0.35 // inner radius of ring
    _Outer ("Outer", float) = 0.45 // outer radius of ring
    _Direction ("Direction", float) = -1 // Direction or progress 1 anticlockwise, -1 clockwise
    _FilledColor ("FilledColor", Color) = (0, 1, 1, 1) // Color of the filled in section
    _UnfilledColor ("UnfilledColor", Color) = (0.5, 1, 1, 1) // Color of the 'empty' section.
  }
  SubShader
  {
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    LOD 100

    ZWrite Off
    Blend SrcAlpha OneMinusSrcAlpha

    Pass
    {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      #include "UnityCG.cginc"

      struct appdata
      {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };

      struct v2f
      {
        float2 uv : TEXCOORD0;
        float4 vertex : SV_POSITION;
      };

      float _Progress;
      float _Inner;
      float _Outer;
      float _Direction;
      float4 _FilledColor;
      float4 _UnfilledColor;

      v2f vert (appdata v)
      {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        return o;
      }

      fixed4 frag (v2f i) : SV_Target
      {
        float2 offset = i.uv - float2(0.5, 0.5);
        float angle = 0.5 + atan2(offset.y, offset.x) * _Direction / (2 * 3.14159265359) ;
        float angle2 = 0.5 + atan2(-offset.y, -offset.x) * _Direction / (2 * 3.14159265359);
        float angleDelta = min(fwidth(angle), fwidth(angle2));
        float ring = smoothstep(_Progress - angleDelta, _Progress, angle);
        float4 ringcol = lerp(_FilledColor, _UnfilledColor, ring);

        float dist = length(offset);
        float delta = fwidth(dist);
        float alpha = smoothstep(_Inner - delta, _Inner, dist) -
                      smoothstep(_Outer - delta, _Outer, dist);

        float4 col = lerp(float4(0, 0, 0, 0), ringcol, alpha);

        return col;
      }
      ENDCG
    }
  }
}
