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

Shader "Custom/StencilSurfaceTutorial" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _BackColor ("Backside Color", Color) = (1,1,1,1)
  _LocalScale ("Local Scale", Vector) = (1,1,1)
  _GridSize ("Grid Size", Float) = 1
  _GridWidth ("Grid Width", Float) = .05
  _UVGridWidth ("UV Grid Width", Float) = .1
  [KeywordEnum(Cube, Sphere, Capsule)] _Shape ("Shape Type", Float) = 0

}

CGINCLUDE
  #include "UnityCG.cginc"
  #include "Assets/Shaders/Include/Brush.cginc"

  #pragma multi_compile _SHAPE_CUBE _SHAPE_SPHERE _SHAPE_CAPSULE

  uniform float4 _Color;
  uniform float4 _BackColor;
  uniform float3 _LocalScale;
  uniform float _GridSize;
  uniform float _GridWidth;
  uniform float _UVGridWidth;
  uniform float _ModeSwitch;

  struct appdata_t {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 texcoord : TEXCOORD0;
  };

  struct v2f {
    float4 vertex : SV_POSITION;
    float3 pos : TEXCOORD1;
    float3 normal : TEXCOORD2;
    float2 texcoord : TEXCOORD3;
    float4 screenPos : TEXCOORD4;
  };

  v2f vert (appdata_t v)
  {
    v2f o;

    // Shrink the stencil slightly to prevent z fighting when the user is drawing on top of it.
    float stencilShrinkAmount = .005f;
    float curvedStencilShrinkAmount = .015f;
    o.pos = v.vertex;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.normal = v.normal;
    o.texcoord = v.texcoord;
      o.screenPos = ComputeScreenPos(o.vertex);
    return o;
  }

  float4 createStencilGrid (v2f i, float gridSizeMultiplier, float gridWidthMultiplier, float UVGridWidthMultiplier) {
    float4 c = float4(0,0,0,0);
    float3 localPos = i.pos * _LocalScale;
    float depthFactor = i.screenPos.z / i.screenPos.w;

    // Change grid size based on scene scale
    float gridMultiplier = 1;
    const float sceneScale = length(mul(xf_CS, float4(1,0,0,0)));
    if (sceneScale > 5) gridMultiplier = 1;
    else if (sceneScale > 1) gridMultiplier = 2;
    else if (sceneScale > .5) gridMultiplier = 4;
    else if (sceneScale > .25) gridMultiplier = 8;
    else gridMultiplier = 16;

    _GridSize *= gridMultiplier * gridSizeMultiplier;
    _GridWidth *= gridMultiplier * gridWidthMultiplier;
    _UVGridWidth *= gridMultiplier * UVGridWidthMultiplier;

    float facingY = abs(dot(i.normal, float3(0,1,0)));
    float facingX = abs(dot(i.normal, float3(1,0,0)));
    float facingZ = abs(dot(i.normal, float3(0,0,1)));

    // Edges along the interior of the cube
    float interiorGrid = 0;
    float fmodBias = 10000; //keep fmod from wrapping into negative values
    interiorGrid += fmod( (localPos.y + fmodBias + (_GridWidth / 2.0f) ), _GridSize) < _GridWidth ? 1 - facingY : 0;
    interiorGrid = max(interiorGrid + fmod( (localPos.x + fmodBias + (_GridWidth / 2.0f) ), _GridSize) < _GridWidth ? 1 - facingX : 0, interiorGrid);
    interiorGrid += max(interiorGrid + fmod( (localPos.z + fmodBias + (_GridWidth / 2.0f) ), _GridSize) < _GridWidth ? 1 - facingZ : 0, interiorGrid);
    // Edges along the border of the cube, capsule or sphere
    float outerEdges = 0;

#if _SHAPE_CUBE
    float gridWidthX = _UVGridWidth / _LocalScale.x;
    float gridWidthY = _UVGridWidth / _LocalScale.y;
    float gridWidthZ = _UVGridWidth / _LocalScale.z;

    // top / bottom
    outerEdges += facingY * abs(.5 - i.texcoord.x) >  (.5 - gridWidthX) ;
    outerEdges += facingY * abs(.5 - i.texcoord.y) >  (.5 - gridWidthZ) ;

    // left / right
    outerEdges += facingX * abs(.5 - i.texcoord.x) >  (.5 - gridWidthZ) ;
    outerEdges += facingX * abs(.5 - i.texcoord.y) >  (.5 - gridWidthY) ;

    // front / back
    outerEdges += facingZ * abs(.5 - i.texcoord.x) >  (.5 - gridWidthX) ;
    outerEdges += facingZ * abs(.5 - i.texcoord.y) >  (.5 - gridWidthY) ;
#elif _SHAPE_CAPSULE
    int numLines = 4;
    float gridWidthX = .5 * _UVGridWidth / _LocalScale.x;
    outerEdges += fmod(( (i.texcoord.x - (gridWidthX / 2.0f) / (numLines)) * numLines + 1000), 1) >  (1-gridWidthX) ;

    float gridWidthY =  .25 * _UVGridWidth / (_LocalScale.y);
    outerEdges += abs(.5 - i.texcoord.y) >  (.5 - gridWidthY);
#elif _SHAPE_SPHERE
    float gridWidthX = _UVGridWidth / _LocalScale.x;
    outerEdges += abs(fmod(i.pos.x * 2 + 0 , 1)) < gridWidthX * 2;
    outerEdges += abs(fmod(i.pos.y * 2 + 0 , 1)) < gridWidthX * 2;
    outerEdges += abs(fmod(i.pos.z * 2 + 0 , 1)) < gridWidthX * 2;
#else
    return float4(1,0,1,1);
#endif

    // fade out with distance
    outerEdges *= saturate(1.25 - pow(depthFactor,4));
    interiorGrid *= 1 - pow(depthFactor,4);

    c.rgb += saturate(outerEdges);
    c.rgb += saturate(interiorGrid) * ( 1 - saturate(outerEdges));
    c.a = c.r * .5;



    return c;
  }
ENDCG

SubShader {
Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}

LOD 100
ColorMask RGB
Lighting Off Fog { Color (0,0,0,0) }
ZWrite Off

// back faces
Cull Front
Blend One One
Pass {
  CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    fixed4 frag (v2f i) : SV_Target
    {
      float4 c = createStencilGrid(i,2,.5,.25);
      //clip( c.x < 0.01f ? -1:1 );
      c.rgb += float3(.2,.2,.2);
      c.a = max (.5, c.a);
      return c * c.a * _Color * _BackColor;
    }
  ENDCG
  }

// front faces
Cull Back
Blend SrcAlpha OneMinusSrcAlpha // overlay
Offset 0, -500
Pass {
  CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag

    fixed4 frag (v2f i) : SV_Target
    {
      float4 c = createStencilGrid(i,1,1,.5);
      //clip( c.x < 0.01f ? -1:1 );
      c.a = .5;
      return c * c.a * _Color;
    }
  ENDCG
  }

} // end subshader
Fallback "Unlit/Diffuse"
}
