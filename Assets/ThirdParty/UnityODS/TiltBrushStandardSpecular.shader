// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

Shader "TiltBrush/Standard (Specular setup)"
{
  Properties
  {
    _Color("Color", Color) = (1,1,1,1)
    _MainTex("Albedo", 2D) = "white" {}

    _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

    _Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
    _GlossMapScale("Smoothness Factor", Range(0.0, 1.0)) = 1.0
    [Enum(Specular Alpha,0,Albedo Alpha,1)] _SmoothnessTextureChannel ("Smoothness texture channel", Float) = 0

    _SpecColor("Specular", Color) = (0.2,0.2,0.2)
    _SpecGlossMap("Specular", 2D) = "white" {}
    [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
    [ToggleOff] _GlossyReflections("Glossy Reflections", Float) = 1.0

    _BumpScale("Scale", Float) = 1.0
    _BumpMap("Normal Map", 2D) = "bump" {}

    _Parallax ("Height Scale", Range (0.005, 0.08)) = 0.02
    _ParallaxMap ("Height Map", 2D) = "black" {}

    _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
    _OcclusionMap("Occlusion", 2D) = "white" {}

    _EmissionColor("Color", Color) = (0,0,0)
    _EmissionMap("Emission", 2D) = "white" {}

    _DetailMask("Detail Mask", 2D) = "white" {}

    _DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
    _DetailNormalMapScale("Scale", Float) = 1.0
    _DetailNormalMap("Normal Map", 2D) = "bump" {}

    [Enum(UV0,0,UV1,1)] _UVSec ("UV Set for secondary textures", Float) = 0


    // Blending state
    [HideInInspector] _Mode ("__mode", Float) = 0.0
    [HideInInspector] _SrcBlend ("__src", Float) = 1.0
    [HideInInspector] _DstBlend ("__dst", Float) = 0.0
    [HideInInspector] _ZWrite ("__zw", Float) = 1.0
  }

  CGINCLUDE
    #define UNITY_SETUP_BRDF_INPUT SpecularSetup
    #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
    #include "Assets/Shaders/Include/Brush.cginc"

#if UNITY_NO_FULL_STANDARD_SHADER
#define TB_VERT_BASE VertexOutputBaseSimple
#define TB_VERT_ADD VertexOutputForwardAddSimple
#else
#define TB_VERT_BASE VertexOutputForwardBase
#define TB_VERT_ADD VertexOutputForwardAdd
#endif

  ENDCG

  SubShader
  {
    Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }
    LOD 300


    // ------------------------------------------------------------------
    //  Base forward pass (directional light, emission, lightmaps, ...)
    Pass
    {
      Name "FORWARD"
      Tags { "LightMode" = "ForwardBase" }

      Blend [_SrcBlend] [_DstBlend]
      ZWrite [_ZWrite]

      CGPROGRAM
      #pragma target 3.0

      // -------------------------------------

      #pragma shader_feature _NORMALMAP
      #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
      #pragma shader_feature _EMISSION
      #pragma shader_feature _SPECGLOSSMAP
      #pragma shader_feature ___ _DETAIL_MULX2
      #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
      #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
      #pragma shader_feature _ _GLOSSYREFLECTIONS_OFF
      #pragma shader_feature _PARALLAXMAP

      #pragma multi_compile_fwdbase
      #pragma multi_compile_fog
      #pragma multi_compile_instancing
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

      #pragma vertex vertBaseTB
      #pragma fragment fragBase
      #include "UnityStandardCoreForward.cginc"
      TB_VERT_BASE vertBaseTB(VertexInput v)
      {
        PrepForOds(v.vertex);
        return vertBase(v);
      }

      ENDCG
    }
    // ------------------------------------------------------------------
    //  Additive forward pass (one light per pass)
    Pass
    {
      Name "FORWARD_DELTA"
      Tags { "LightMode" = "ForwardAdd" }
      Blend [_SrcBlend] One
      Fog { Color (0,0,0,0) } // in additive pass fog should be black
      ZWrite Off
      ZTest LEqual

      CGPROGRAM
      #pragma target 3.0

      // -------------------------------------

      #pragma shader_feature _NORMALMAP
      #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
      #pragma shader_feature _SPECGLOSSMAP
      #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
      #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
      #pragma shader_feature ___ _DETAIL_MULX2
      #pragma shader_feature _PARALLAXMAP

      #pragma multi_compile_fwdadd_fullshadows
      #pragma multi_compile_fog

      #pragma vertex vertAddTB
      #pragma fragment fragAdd
      #include "UnityStandardCoreForward.cginc"

      TB_VERT_ADD vertAddTB(VertexInput v)
      {
        PrepForOds(v.vertex);
        return vertAdd(v);
      }

      ENDCG
    }
    // ------------------------------------------------------------------
    //  Shadow rendering pass
    Pass {
      Name "ShadowCaster"
      Tags { "LightMode" = "ShadowCaster" }

      ZWrite On ZTest LEqual

      CGPROGRAM
      #pragma target 3.0

      // -------------------------------------


      #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
      #pragma shader_feature _SPECGLOSSMAP
      #pragma shader_feature _PARALLAXMAP
      #pragma multi_compile_shadowcaster
      #pragma multi_compile_instancing
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

      #pragma vertex vertShadowCasterTB
      #pragma fragment fragShadowCaster

      #include "UnityStandardShadow.cginc"

      void vertShadowCasterTB(VertexInput v
	    , out float4 opos : SV_POSITION
	    #ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
	    , out VertexOutputShadowCaster o
	    #endif
	    #ifdef UNITY_STANDARD_USE_STEREO_SHADOW_OUTPUT_STRUCT
	    , out VertexOutputStereoShadowCaster os
	    #endif
      )
      {
        PrepForOds(v.vertex);
        vertShadowCaster(v, opos
    #ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
                                        ,o
    #endif
    #ifdef UNITY_STANDARD_USE_STEREO_SHADOW_OUTPUT_STRUCT
                                        ,os
    #endif
        );
      }

      ENDCG
    }
    // ------------------------------------------------------------------
    //  Deferred pass
    Pass
    {
      Name "DEFERRED"
      Tags { "LightMode" = "Deferred" }

      CGPROGRAM
      #pragma target 3.0
      #pragma exclude_renderers nomrt


      // -------------------------------------

      #pragma shader_feature _NORMALMAP
      #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
      #pragma shader_feature _EMISSION
      #pragma shader_feature _SPECGLOSSMAP
      #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
      #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
      #pragma shader_feature ___ _DETAIL_MULX2
      #pragma shader_feature _PARALLAXMAP

      #pragma multi_compile_prepassfinal
      #pragma multi_compile_instancing
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

      #pragma vertex vertDeferredTB
      #pragma fragment fragDeferred

      #include "UnityStandardCore.cginc"

      VertexOutputDeferred vertDeferredTB(VertexInput v)
      {
        PrepForOds(v.vertex);
        return vertDeferred(v);
      }

      ENDCG
    }

    // ------------------------------------------------------------------
    // Extracts information for lightmapping, GI (emission, albedo, ...)
    // This pass it not used during regular rendering.
    Pass
    {
      Name "META"
      Tags { "LightMode"="Meta" }

      Cull Off

      CGPROGRAM
      #pragma vertex vert_meta
      #pragma fragment frag_meta

      #pragma shader_feature _EMISSION
      #pragma shader_feature _SPECGLOSSMAP
      #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
      #pragma shader_feature ___ _DETAIL_MULX2
      #pragma shader_feature EDITOR_VISUALIZATION

      #include "UnityStandardMeta.cginc"
      ENDCG
    }
  }

  SubShader
  {
    Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }
    LOD 150

    // ------------------------------------------------------------------
    //  Base forward pass (directional light, emission, lightmaps, ...)
    Pass
    {
      Name "FORWARD"
      Tags { "LightMode" = "ForwardBase" }

      Blend [_SrcBlend] [_DstBlend]
      ZWrite [_ZWrite]

      CGPROGRAM
      #pragma target 2.0

      #pragma shader_feature _NORMALMAP
      #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
      #pragma shader_feature _EMISSION
      #pragma shader_feature _SPECGLOSSMAP
      #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
      #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
      #pragma shader_feature _ _GLOSSYREFLECTIONS_OFF
      #pragma shader_feature ___ _DETAIL_MULX2
      // SM2.0: NOT SUPPORTED shader_feature _PARALLAXMAP

      #pragma skip_variants SHADOWS_SOFT DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED

      #pragma multi_compile_fwdbase
      #pragma multi_compile_fog

      #pragma vertex vertBaseTB
      #pragma fragment fragBase
      #include "UnityStandardCoreForward.cginc"
      TB_VERT_BASE vertBaseTB(VertexInput v)
      {
        PrepForOds(v.vertex);
        return vertBase(v);
      }

      ENDCG
    }
    // This pass is for mobile selection.
    //
    Pass
    {
        Name "Selection"
        Tags { "LightMode" = "ForwardBase" }

        Blend OneMinusDstColor One
        ZWrite Off
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile __ SELECTION_ON HIGHLIGHT_ON
        //#include "Assets/Shaders/Include/Brush.cginc"
        #include "UnityCG.cginc"
        #include "Assets/Shaders/Include/MobileSelection.cginc"

        struct appdata_t {
            float4 vertex : POSITION;
        };

        struct v2f {
            float4 pos : POSITION;
        };

        v2f vert (appdata_t v)
        {
            v2f o;
#if SELECTION_ON
            PrepForOds(v.vertex);
            o.pos = UnityObjectToClipPos(v.vertex);
#else
            // If selection is turned off, early out by changing the vertex 0,0,0,0.
            // This creates a degenerate triangle that will not be rendered.
            o.pos = 0;
#endif
            return o;
        }

        fixed4 frag (v2f i) : COLOR
        {
            float4 c = float4(0,0,0,1);
            FRAG_MOBILESELECT(c)
            return c;
        }

        ENDCG
    }

    // ------------------------------------------------------------------
    //  Additive forward pass (one light per pass)
    Pass
    {
      Name "FORWARD_DELTA"
      Tags { "LightMode" = "ForwardAdd" }
      Blend [_SrcBlend] One
      Fog { Color (0,0,0,0) } // in additive pass fog should be black
      ZWrite Off
      ZTest LEqual

      CGPROGRAM
      #pragma target 2.0

      #pragma shader_feature _NORMALMAP
      #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
      #pragma shader_feature _SPECGLOSSMAP
      #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
      #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
      #pragma shader_feature ___ _DETAIL_MULX2
      // SM2.0: NOT SUPPORTED shader_feature _PARALLAXMAP
      #pragma skip_variants SHADOWS_SOFT

      #pragma multi_compile_fwdadd_fullshadows
      #pragma multi_compile_fog

      #pragma vertex vertAddTB
      #pragma fragment fragAdd
      #include "UnityStandardCoreForward.cginc"

      TB_VERT_ADD vertAddTB(VertexInput v)
      {
        PrepForOds(v.vertex);
        return vertAdd(v);
      }

      ENDCG
    }
    // ------------------------------------------------------------------
    //  Shadow rendering pass
    Pass {
      Name "ShadowCaster"
      Tags { "LightMode" = "ShadowCaster" }

      ZWrite On ZTest LEqual

      CGPROGRAM
      #pragma target 2.0

      #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
      #pragma shader_feature _SPECGLOSSMAP
      #pragma skip_variants SHADOWS_SOFT
      #pragma multi_compile_shadowcaster

      #pragma vertex vertShadowCasterTB
      #pragma fragment fragShadowCaster

      #include "UnityStandardShadow.cginc"
      void vertShadowCasterTB(VertexInput v
	    , out float4 opos : SV_POSITION
	    #ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
	    , out VertexOutputShadowCaster o
	    #endif
	    #ifdef UNITY_STANDARD_USE_STEREO_SHADOW_OUTPUT_STRUCT
	    , out VertexOutputStereoShadowCaster os
	    #endif
      )
      {
        PrepForOds(v.vertex);
        vertShadowCaster(v, opos
    #ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
                                        ,o
    #endif
    #ifdef UNITY_STANDARD_USE_STEREO_SHADOW_OUTPUT_STRUCT
                                        ,os
    #endif
        );
      }

      ENDCG
    }
    // ------------------------------------------------------------------
    // Extracts information for lightmapping, GI (emission, albedo, ...)
    // This pass it not used during regular rendering.
    Pass
    {
      Name "META"
      Tags { "LightMode"="Meta" }

      Cull Off

      CGPROGRAM
      #pragma vertex vert_meta
      #pragma fragment frag_meta

      #pragma shader_feature _EMISSION
      #pragma shader_feature _SPECGLOSSMAP
      #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
      #pragma shader_feature ___ _DETAIL_MULX2
      #pragma shader_feature EDITOR_VISUALIZATION

      #include "UnityStandardMeta.cginc"
      ENDCG
    }
  }

  FallBack "VertexLit"
  CustomEditor "StandardShaderGUI"
}
