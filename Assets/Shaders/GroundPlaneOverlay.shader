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

Shader "Unlit/GroundPlaneOverlay"
{
  Properties
  {
    _Color ("Color", Color) = (1,1,1,1)
    _ChaperoneScaleX ("Chaperone Scale X", Float) = 1
    _ChaperoneScaleZ ("Chaperone Scale Z", Float) = 1
  }

  SubShader
  {
	LOD 201
    Tags { "Queue"="Overlay" "RenderType"="Transparent" }
    Blend SrcAlpha OneMinusSrcAlpha, Zero One
    ZTest Always
    ZWrite Off
    Pass {
      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma exclude_renderers d3d9 d3d11_9x

        #include "UnityCG.cginc"

        struct appdata_t {
          float4 vertex : POSITION;
        };

        struct v2f {
          float4 vertex : SV_POSITION;
          float3 worldPosition : COLOR;
        };

        uniform float4 _Color;
        uniform float _ChaperoneScaleX;
        uniform float _ChaperoneScaleZ;

        v2f vert (appdata_t v)
        {
          v2f o;
          o.vertex = UnityObjectToClipPos(v.vertex);
          o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
          return o;
        }

        fixed4 frag (v2f i) : SV_Target
        {
          float t = _Color.w;
          float outer_distance = 100 * t;
          _ChaperoneScaleX = _ChaperoneScaleX * (t * .25 + .75);
          _ChaperoneScaleZ = _ChaperoneScaleZ * (t * .25 + .75);

          float room_border_width = .1f;
          float inside_room_bounds = (abs(i.worldPosition.x) < _ChaperoneScaleX - room_border_width ) && (abs(i.worldPosition.z) < _ChaperoneScaleZ - room_border_width ) ? 1 : 0;

          // Primary grid lines
          float3 grid3 = 1 - 2 * abs((i.worldPosition / (16 - inside_room_bounds * 13) % 1 + 1) % 1 - .5);
          grid3 = grid3 > .98 ? 1 : 0;
          float grid = max(grid3.x, grid3.z);

          // Grid line cutouts
          if (!inside_room_bounds) {
            float3 grid3b = 1 - 2 * abs((i.worldPosition / 16 % 1 + 1) % 1 - .5);
            grid3b = grid3b > .8 - .2 * t ? 0 : 1 ;
            grid *= 1 - max(grid3b.x, grid3b.z);
          }

          // Distance from origin
          float dist = length( float2(i.worldPosition.x, i.worldPosition.z) );

          // Prune grid points in distance
          grid *= dist > outer_distance ? 0 : 1;

          // Add outer ring
          grid += (dist > outer_distance) && (dist < outer_distance + 4) ? 1 : 0;

          // Create room border
          float border = ( abs(abs(i.worldPosition.x) - _ChaperoneScaleX) < room_border_width) &&  abs(i.worldPosition.z) < _ChaperoneScaleZ + room_border_width ? 20 : 0;
          border += ( abs(abs(i.worldPosition.z) - _ChaperoneScaleZ) < room_border_width) &&  abs(i.worldPosition.x) < _ChaperoneScaleX + room_border_width ? 20 : 0;

          // Draw room floor
          grid = saturate(grid + border) + inside_room_bounds * .5;

          _Color.w = .1 * t;
          return grid * _Color;
        }
      ENDCG
    }
  }

  // MOBILE VERSION
  SubShader {
	LOD 100
    Tags { "Queue"="Overlay" "RenderType"="Transparent" }
    Blend SrcAlpha OneMinusSrcAlpha, Zero One
    ZTest Always
    ZWrite Off
    Pass {
      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma exclude_renderers d3d9 d3d11_9x

        #include "UnityCG.cginc"

        struct appdata_t {
          float4 vertex : POSITION;
        };

        struct v2f {
          float4 vertex : SV_POSITION;
          float3 worldPosition : COLOR;
        };

        uniform float4 _Color;
        uniform float _ChaperoneScaleX;
        uniform float _ChaperoneScaleZ;

        v2f vert (appdata_t v)
        {
          v2f o;
          o.vertex = UnityObjectToClipPos(v.vertex);
          o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
          return o;
        }

        fixed4 frag (v2f i) : SV_Target
        {
		  // Mobile version does not animate.

          float outer_distance = 100;

          float room_border_width = .1f;
          float inside_room_bounds = (abs(i.worldPosition.x) < _ChaperoneScaleX - room_border_width ) && (abs(i.worldPosition.z) < _ChaperoneScaleZ - room_border_width ) ? 1 : 0;

          // Primary grid lines
          float3 grid3 = 1 - 2 * abs((i.worldPosition / (16 - inside_room_bounds * 13) % 1 + 1) % 1 - .5);
          grid3 = grid3 > .98 ? 1 : 0;
          float grid = max(grid3.x, grid3.z);

          // Grid line cutouts
          if (!inside_room_bounds) {
            float3 grid3b = 1 - 2 * abs((i.worldPosition / 16 % 1 + 1) % 1 - .5);
            grid3b = grid3b > .6 ? 0 : 1 ;
            grid *= 1 - max(grid3b.x, grid3b.z);
          }

          // Distance from origin
          float dist = length( float2(i.worldPosition.x, i.worldPosition.z) );

          // Prune grid points in distance
          grid *= dist > outer_distance ? 0 : 1;

          // Add outer ring
          grid += (dist > outer_distance) && (dist < outer_distance + 4) ? 1 : 0;

          // Create room border
          float border = ( abs(abs(i.worldPosition.x) - _ChaperoneScaleX) < room_border_width) &&  abs(i.worldPosition.z) < _ChaperoneScaleZ + room_border_width ? 20 : 0;
          border += ( abs(abs(i.worldPosition.z) - _ChaperoneScaleZ) < room_border_width) &&  abs(i.worldPosition.x) < _ChaperoneScaleX + room_border_width ? 20 : 0;

          // Draw room floor
          grid = saturate(grid + border) + inside_room_bounds * .5;

          _Color.w = .1;
          return grid * _Color;
        }
      ENDCG
    }
  }
}
