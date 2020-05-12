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

Shader "Brush/StandardSingleSided" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
    _Shininess ("Shininess", Range (0.01, 1)) = 0.078125
    _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
    _BumpMap ("Normalmap", 2D) = "bump" {}
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  }

  // -------------------------------------------------------------------------------------------- //
  // DESKTOP VERSION.
  // -------------------------------------------------------------------------------------------- //
  SubShader {
    Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
    LOD 400
    Cull Back

    CGPROGRAM
      #pragma target 3.0
      #pragma surface surf StandardSpecular vertex:vert alphatest:_Cutoff addshadow
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

      #include "Assets/Shaders/Include/Brush.cginc"

      struct Input {
        float2 uv_MainTex;
        float2 uv_BumpMap;
        float4 color : Color;
      };

      sampler2D _MainTex;
      sampler2D _BumpMap;
      fixed4 _Color;
      half _Shininess;

      void vert (inout appdata_full i /*, out Input o*/) {
        // UNITY_INITIALIZE_OUTPUT(Input, o);
        // o.tangent = v.tangent;
        PrepForOds(i.vertex);
        i.color = TbVertToNative(i.color);
      }

      void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
        fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
        o.Albedo = tex.rgb * _Color.rgb * IN.color.rgb;
        o.Smoothness = _Shininess;
        o.Specular = _SpecColor;
        o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
        o.Alpha = tex.a * IN.color.a;
      }
    ENDCG
  }

  // -------------------------------------------------------------------------------------------- //
  // MOBILE VERSION - Vert/Frag, MSAA + Alpha-To-Coverage, w/Bump.
  // -------------------------------------------------------------------------------------------- //
  SubShader {
    Tags{ "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
    Cull Back
    LOD 201

    Pass {
      Tags { "LightMode"="ForwardBase" }
      AlphaToMask On

      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 3.0

        #include "UnityCG.cginc"
        #include "Lighting.cginc"

        // Disable all the things.
        #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight noshadow

        struct appdata {
          float4 vertex : POSITION;
          float2 uv : TEXCOORD0;
          half3 normal : NORMAL;
          fixed4 color : COLOR;
          float4 tangent : TANGENT;
        };

        struct v2f {
          float4 pos : SV_POSITION;
          float2 uv : TEXCOORD0;
          half3 worldNormal : NORMAL;
          fixed4 color : COLOR;
          half3 tspace0 : TEXCOORD1;
          half3 tspace1 : TEXCOORD2;
          half3 tspace2 : TEXCOORD3;
        };

        sampler2D _MainTex;
        float4 _MainTex_ST;
        float4 _MainTex_TexelSize;
        sampler2D _BumpMap;

        fixed _Cutoff;
        half _MipScale;

        float ComputeMipLevel(float2 uv) {
          float2 dx = ddx(uv);
          float2 dy = ddy(uv);
          float delta_max_sqr = max(dot(dx, dx), dot(dy, dy));
          return max(0.0, 0.5 * log2(delta_max_sqr));
        }

        v2f vert (appdata v) {
          v2f o;
          o.pos = UnityObjectToClipPos(v.vertex);
          o.uv = TRANSFORM_TEX(v.uv, _MainTex);
          o.worldNormal = UnityObjectToWorldNormal(v.normal);
          o.color = v.color;

          half3 wNormal = UnityObjectToWorldNormal(v.normal);
          half3 wTangent = UnityObjectToWorldDir(v.tangent.xyz);
          half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
          half3 wBitangent = cross(wNormal, wTangent) * tangentSign;
          o.tspace0 = half3(wTangent.x, wBitangent.x, wNormal.x);
          o.tspace1 = half3(wTangent.y, wBitangent.y, wNormal.y);
          o.tspace2 = half3(wTangent.z, wBitangent.z, wNormal.z);
          return o;
        }

        fixed4 frag (v2f i, fixed vface : VFACE) : SV_Target {
          fixed4 col = i.color;
          col.a = tex2D(_MainTex, i.uv).a * col.a;
          col.a *= 1 + max(0, ComputeMipLevel(i.uv * _MainTex_TexelSize.zw)) * _MipScale;
          col.a = (col.a - _Cutoff) / max(2 * fwidth(col.a), 0.0001) + 0.5;

          half3 tnormal = UnpackNormal(tex2D(_BumpMap, i.uv));
          tnormal.z *= vface;

          // Transform normal from tangent to world space.
          half3 worldNormal;
          worldNormal.x = dot(i.tspace0, tnormal);
          worldNormal.y = dot(i.tspace1, tnormal);
          worldNormal.z = dot(i.tspace2, tnormal);

          fixed ndotl = saturate(dot(worldNormal, normalize(_WorldSpaceLightPos0.xyz)));
          fixed3 lighting = ndotl * _LightColor0;
          lighting += ShadeSH9(half4(worldNormal, 1.0));

          col.rgb *= lighting;

          return col;
        }

      ENDCG
    } // pass
  } // subshader

  // -------------------------------------------------------------------------------------------- //
  // MOBILE VERSION - Vert/Frag, Alpha Tested, w/Bump.
  // -------------------------------------------------------------------------------------------- //
  SubShader{
    Tags{ "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
    Cull Back
    LOD 200

    Pass {
      Tags { "LightMode"="ForwardBase" }

      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 3.0

        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "Assets/Shaders/Include/MobileSelection.cginc"

        #pragma multi_compile __ SELECTION_ON
        #pragma multi_compile_fog
        // Disable all the things.
        #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight noshadow

        struct appdata {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
            half3 normal : NORMAL;
            fixed4 color : COLOR;
            float4 tangent : TANGENT;
        };

        struct v2f {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
            fixed4 color : COLOR;
            half3 tspace0 : TEXCOORD1;
            half3 tspace1 : TANGENT;
            half3 tspace2 : NORMAL;
            UNITY_FOG_COORDS(5)

        };

        sampler2D _MainTex;
        float4 _MainTex_ST;
        sampler2D _BumpMap;

        fixed _Cutoff;

        v2f vert (appdata v) {
          v2f o;
          o.pos = UnityObjectToClipPos(v.vertex);
          o.uv = TRANSFORM_TEX(v.uv, _MainTex);
          o.color = v.color;

          half3 wNormal = UnityObjectToWorldNormal(v.normal);
          half3 wTangent = UnityObjectToWorldDir(v.tangent.xyz);
          half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
          half3 wBitangent = cross(wNormal, wTangent) * tangentSign;
          o.tspace0 = half3(wTangent.x, wBitangent.x, wNormal.x);
          o.tspace1 = half3(wTangent.y, wBitangent.y, wNormal.y);
          o.tspace2 = half3(wTangent.z, wBitangent.z, wNormal.z);
          UNITY_TRANSFER_FOG(o, o.pos);
          return o;
        }

        fixed4 frag (v2f i, fixed vface : VFACE) : SV_Target {
          fixed4 col = i.color;
          col.a = tex2D(_MainTex, i.uv).a * col.a;
          if (col.a < _Cutoff) { discard; }
          half3 tnormal = UnpackNormal(tex2D(_BumpMap, i.uv));
          tnormal.z *= vface;

          // transform normal from tangent to world space
          half3 worldNormal;
          worldNormal.x = dot(i.tspace0, tnormal);
          worldNormal.y = dot(i.tspace1, tnormal);
          worldNormal.z = dot(i.tspace2, tnormal);

          fixed ndotl = saturate(dot(worldNormal, normalize(_WorldSpaceLightPos0.xyz)));
          fixed3 lighting = ndotl * _LightColor0;
          lighting += ShadeSH9(half4(worldNormal, 1.0));

          col.rgb *= lighting;
          UNITY_APPLY_FOG(i.fogCoord, col);
          FRAG_MOBILESELECT(col)
          return col;
        }
      ENDCG
    } // pass
  } // subshader

  // -------------------------------------------------------------------------------------------- //
  // MOBILE VERSION -- vert/frag, MSAA + Alpha-To-Coverage, No Bump.
  // -------------------------------------------------------------------------------------------- //
  SubShader {
    Tags{ "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
    Cull Back
    LOD 150

    Pass {
      Tags { "LightMode"="ForwardBase" }
      AlphaToMask On

      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 3.0

        #include "UnityCG.cginc"
        #include "Lighting.cginc"

        // Disable all the things.
        #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight noshadow

        struct appdata {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
            half3 normal : NORMAL;
            fixed4 color : COLOR;
        };

        struct v2f {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
            half3 worldNormal : NORMAL;
            fixed4 color : COLOR;
        };

        sampler2D _MainTex;
        float4 _MainTex_ST;
        float4 _MainTex_TexelSize;

        fixed _Cutoff;
        half _MipScale;

        float ComputeMipLevel(float2 uv) {
          float2 dx = ddx(uv);
          float2 dy = ddy(uv);
          float delta_max_sqr = max(dot(dx, dx), dot(dy, dy));
          return max(0.0, 0.5 * log2(delta_max_sqr));
        }

        v2f vert (appdata v) {
          v2f o;
          o.pos = UnityObjectToClipPos(v.vertex);
          o.uv = TRANSFORM_TEX(v.uv, _MainTex);
          o.worldNormal = UnityObjectToWorldNormal(v.normal);
          o.color = v.color;
          return o;
        }

        fixed4 frag (v2f i, fixed vface : VFACE) : SV_Target {
          fixed4 col = i.color;
          col.a *= tex2D(_MainTex, i.uv).a;
          col.a *= 1 + max(0, ComputeMipLevel(i.uv * _MainTex_TexelSize.zw)) * _MipScale;
          col.a = (col.a - _Cutoff) / max(2 * fwidth(col.a), 0.0001) + 0.5;

          half3 worldNormal = normalize(i.worldNormal * vface);

          fixed ndotl = saturate(dot(worldNormal, normalize(_WorldSpaceLightPos0.xyz)));
          fixed3 lighting = ndotl * _LightColor0;
          lighting += ShadeSH9(half4(worldNormal, 1.0));

          col.rgb *= lighting;

          return col;
        }
      ENDCG
    } // pass
  } // subshader

  // -------------------------------------------------------------------------------------------- //
  // MOBILE VERSION - Lambert SurfaceShader, Alpha Test, No Bump.
  // -------------------------------------------------------------------------------------------- //
  SubShader{
    Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
    LOD 50

    CGPROGRAM
      #pragma surface surf Lambert vertex:vert alphatest:_Cutoff
      #pragma target 3.0

      sampler2D _MainTex;
      fixed4 _Color;

      struct Input {
        float2 uv_MainTex;
        float4 color : COLOR;
      };

      void vert (inout appdata_full v) {
      }

      void surf (Input IN, inout SurfaceOutput o) {
        fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
        o.Albedo = c.rgb * IN.color.rgb;
        o.Alpha = c.a * IN.color.a;
      }

    ENDCG
  } // SubShader

  FallBack "Transparent/Cutout/VertexLit"
} // shader
