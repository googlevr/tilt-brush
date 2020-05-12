// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/SENaturalBloomAndDirtyLens" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_Bloom0 ("Bloom0 (RGB)", 2D) = "black" {}
		_Bloom1 ("Bloom1 (RGB)", 2D) = "black" {}
		_Bloom2 ("Bloom2 (RGB)", 2D) = "black" {}
		_Bloom3 ("Bloom3 (RGB)", 2D) = "black" {}
		_Bloom4 ("Bloom4 (RGB)", 2D) = "black" {}
		_Bloom5 ("Bloom5 (RGB)", 2D) = "black" {}
		_LensDirt ("Lens Dirt", 2D) = "black" {}
	}
	
	
	CGINCLUDE
		#include "UnityCG.cginc"
		#include "Assets/Shaders/Include/Hdr.cginc"
		#pragma target 3.0
		#pragma multi_compile __ HDR_EMULATED

#define DEPTH_FIX

		sampler2D _MainTex;
		sampler2D _Bloom0;
		sampler2D _Bloom1;
		sampler2D _Bloom2;
		sampler2D _Bloom3;
		sampler2D _Bloom4;
		sampler2D _Bloom5;
		sampler2D _Bloom6;
		sampler2D _Bloom7;
		sampler2D _LensDirt;
		sampler2D _CameraDepthTexture;
		
		uniform float4 _MainTex_TexelSize;
		
		uniform float _BlurSize;
		uniform float _BloomIntensity;
		uniform float _LensDirtIntensity;
		uniform float _DepthBlendFactor;
		uniform float _MaxDepthBlendFactor;
		uniform float _DepthScatterFactor;
		uniform float _BloomScatterFactor;
		uniform float _LensDirtScatterFactor;
		uniform int _DepthBlendFunction;
		
		float4x4 ProjectionMatrixInverse;

		struct v2f_simple 
		{
			float4 pos : SV_POSITION; 
			float4 uv : TEXCOORD0;

        #if UNITY_UV_STARTS_AT_TOP
			float4 uv2 : TEXCOORD1;
		#endif
		};	
		 
		v2f_simple vertBloom ( appdata_img v )
		{
			v2f_simple o;
			
			o.pos = UnityObjectToClipPos (v.vertex);
        	o.uv = float4(v.texcoord.xy, 1, 1);		
        	
        	#if UNITY_UV_STARTS_AT_TOP
        		o.uv2 = float4(v.texcoord.xy, 1, 1);				
//        		if (_MainTex_TexelSize.y < 0.0)
//        			o.uv.y = 1.0 - o.uv.y;
        	#endif
        	        	
			return o; 
		}
		
		fixed3 Fixed3(float x)
		{
			return fixed3(x, x, x);
		}

		float Luminance(float3 c)
		{
			return dot(c, float3(0.33333, 0.33333, 0.33333));
		}

		float4 GetViewSpacePosition(float2 coord)
		{
			float depth = tex2Dlod(_CameraDepthTexture, float4(coord.x, coord.y, 0.0, 0.0)).x;
			
			#if defined(UNITY_REVERSED_Z)
			depth = 1.0 - depth;
			#endif

			float4 viewPosition = mul(ProjectionMatrixInverse, float4(coord.x * 2.0 - 1.0, coord.y * 2.0 - 1.0, 2.0 * depth - 1.0, 1.0));
			viewPosition /= viewPosition.w;

			return viewPosition;
		}

		float EncDepth(float depth, float3 color)
		{
			//return pow(depth, 1.0 / 5.0);
			return depth * (Luminance(color.rgb) * 1.0 + 0.00);
			//return depth + Luminance(color.rgb) * 0.01;
		}

		float DecDepth(float depth, float3 color)
		{
			//return pow(depth, 5.0);
			return depth / max(0.0000001, Luminance(color.rgb) * 1.0 + 0.00);
			//return depth - Luminance(color.rgb) * 0.01;
		}

		float FogFix(float blurredFogFactor, float fogFactor, inout float fogWeightAccum)
		{
			//return 1.0;
			float weight = saturate(1.0 - pow(max(0.0, fogFactor - blurredFogFactor) * 0.99, 1.0));
			fogWeightAccum += weight;
			return weight;
		}

		
		float4 fragBlendBloomWithScene ( v2f_simple i ) : COLOR
		{	
        	#if UNITY_UV_STARTS_AT_TOP
				float2 coord = i.uv2.xy;
			#else
				float2 coord = i.uv.xy;
			#endif
			
			// Get various source textures
			float4 color =  decodeHdr(tex2D(_MainTex, coord));
			float3 origColor = color.rgb;
			fixed3 lens = tex2D(_LensDirt, coord).rgb;
			float lensLum = dot(lens, Fixed3(0.33333));
			
			float4 b0s = tex2D(_Bloom0, coord);
			float4 b1s = tex2D(_Bloom1, coord);
			float4 b2s = tex2D(_Bloom2, coord);
			float4 b3s = tex2D(_Bloom3, coord);
			float4 b4s = tex2D(_Bloom4, coord);
			float4 b5s = tex2D(_Bloom5, coord);
			float4 b6s = tex2D(_Bloom6, coord);
			float4 b7s = tex2D(_Bloom7, coord);

			fixed3 b0 = b0s.rgb;
			fixed3 b1 = b1s.rgb;
			fixed3 b2 = b2s.rgb;
			fixed3 b3 = b3s.rgb;
			fixed3 b4 = b4s.rgb;
			fixed3 b5 = b5s.rgb;
			fixed3 b6 = b6s.rgb;
			fixed3 b7 = b7s.rgb;

			float depthp = tex2D(_CameraDepthTexture, coord).x;
			float depth = Linear01Depth(depthp);

			float blurredFogFactor0 = (DecDepth(b0s.a, b0s.rgb));
			float blurredFogFactor1 = (DecDepth(b1s.a, b1s.rgb));
			float blurredFogFactor2 = (DecDepth(b2s.a, b2s.rgb));
			float blurredFogFactor3 = (DecDepth(b3s.a, b3s.rgb));
			float blurredFogFactor4 = (DecDepth(b4s.a, b4s.rgb));
			float blurredFogFactor5 = (DecDepth(b5s.a, b5s.rgb));
			float blurredFogFactor6 = (DecDepth(b6s.a, b6s.rgb));
			float blurredFogFactor7 = (DecDepth(b7s.a, b7s.rgb));
			
			
			
			// Various bloom type weights (for each bloom "octave")
			float bloomWeights[8] =
			{
				pow(1.0, _BloomScatterFactor),
				pow(2.0, _BloomScatterFactor),
				pow(3.0, _BloomScatterFactor),
				pow(4.0, _BloomScatterFactor),
				pow(5.0, _BloomScatterFactor),
				pow(6.0, _BloomScatterFactor),
				pow(7.0, _BloomScatterFactor),
				pow(8.0, _BloomScatterFactor)
			};
			const float bloomWeightsInvTotal = 1.0 / (bloomWeights[0] + bloomWeights[1] + bloomWeights[2] + bloomWeights[3] + bloomWeights[4] + bloomWeights[5] + bloomWeights[6] + bloomWeights[7]);

			float lensDirtBloomWeights[8] =
			{
				pow(1.0, _LensDirtScatterFactor),
				pow(2.0, _LensDirtScatterFactor),
				pow(3.0, _LensDirtScatterFactor),
				pow(4.0, _LensDirtScatterFactor),
				pow(5.0, _LensDirtScatterFactor),
				pow(6.0, _LensDirtScatterFactor),
				pow(7.0, _LensDirtScatterFactor),
				pow(8.0, _LensDirtScatterFactor)
			};
			const float lensDirtBloomWeightsInvTotal = 1.0 / (lensDirtBloomWeights[0] + lensDirtBloomWeights[1] + lensDirtBloomWeights[2] + lensDirtBloomWeights[3] + lensDirtBloomWeights[4] + lensDirtBloomWeights[5] + lensDirtBloomWeights[6] + lensDirtBloomWeights[7]);
			
			float fogBloomWeights[8] =
			{
				0.1,
				0.27,
				0.45,
				0.55,
				0.45,
				0.38,
				0.23,
				0.00
			};



			// Fog blending. Use encoded blurred fog factors and compare them with a per-pixel fog factor here to fix issues 
			// with bright foreground objects causing distant fog bloom. Then update each fog bloom weight accordingly
			float fogFactor = 0.0;
			float3 viewPos = GetViewSpacePosition(coord.xy).xyz;
			float dist = length(viewPos);

			if (_DepthBlendFunction == 0)
			{
				fogFactor = 1.0 - exp(-dist * _DepthBlendFactor);
			}
			else
			{
				fogFactor = pow(1.0 - exp(-dist * _DepthBlendFactor), 2.0);
			}
			
			fogFactor = min(fogFactor, _MaxDepthBlendFactor);

			float weightSum = 0.0;
			
			fogBloomWeights[0] *= FogFix(blurredFogFactor0, fogFactor, weightSum) * pow(1.0, _DepthScatterFactor);
			fogBloomWeights[1] *= FogFix(blurredFogFactor1, fogFactor, weightSum) * pow(2.0, _DepthScatterFactor);
			fogBloomWeights[2] *= FogFix(blurredFogFactor2, fogFactor, weightSum) * pow(3.0, _DepthScatterFactor);
			fogBloomWeights[3] *= FogFix(blurredFogFactor3, fogFactor, weightSum) * pow(4.0, _DepthScatterFactor);
			fogBloomWeights[4] *= FogFix(blurredFogFactor4, fogFactor, weightSum) * pow(5.0, _DepthScatterFactor);
			fogBloomWeights[5] *= FogFix(blurredFogFactor5, fogFactor, weightSum) * pow(6.0, _DepthScatterFactor);
			fogBloomWeights[6] *= FogFix(blurredFogFactor6, fogFactor, weightSum) * pow(7.0, _DepthScatterFactor);
			fogBloomWeights[7] *= FogFix(blurredFogFactor7, fogFactor, weightSum) * pow(8.0, _DepthScatterFactor);

			const float fogBloomWeightsInvTotal = 1.0 / (fogBloomWeights[0] + fogBloomWeights[1] + fogBloomWeights[2] + fogBloomWeights[3] + fogBloomWeights[4] + fogBloomWeights[5] + fogBloomWeights[6] + fogBloomWeights[7]);




			// Sum up bloom octaves for each type based on weights
			fixed3 bloom	= b0 * bloomWeights[0]
							+ b1 * bloomWeights[1]
							+ b2 * bloomWeights[2]
							+ b3 * bloomWeights[3]
							+ b4 * bloomWeights[4]
							+ b5 * bloomWeights[5]
							+ b6 * bloomWeights[6]
							+ b7 * bloomWeights[7]
							;
			bloom *= bloomWeightsInvTotal;


			fixed3 fogBloom = b0 * fogBloomWeights[0]
							+ b1 * fogBloomWeights[1]
							+ b2 * fogBloomWeights[2]
							+ b3 * fogBloomWeights[3]
							+ b4 * fogBloomWeights[4]
							+ b5 * fogBloomWeights[5]
							+ b6 * fogBloomWeights[6]
							+ b7 * fogBloomWeights[7]
							;
			fogBloom *= fogBloomWeightsInvTotal;
			
			
			fixed3 lensDirtBloom 	= b0 * lensDirtBloomWeights[0]
									+ b1 * lensDirtBloomWeights[1]
									+ b2 * lensDirtBloomWeights[2]
									+ b3 * lensDirtBloomWeights[3]
									+ b4 * lensDirtBloomWeights[4]
									+ b5 * lensDirtBloomWeights[5]
									+ b6 * lensDirtBloomWeights[6]
									+ b7 * lensDirtBloomWeights[7]
									;
			lensDirtBloom *= lensDirtBloomWeightsInvTotal;




			// Blend various blooms with the main color
			color.rgb = lerp(color.rgb, bloom.rgb, Fixed3(_BloomIntensity));
			color.rgb = lerp(color.rgb, lensDirtBloom.rgb, saturate(lens.rgb * _LensDirtIntensity));
			color.rgb = lerp(color.rgb, fogBloom.rgb, Fixed3(fogFactor));
			
//			// Lens flare stuff. Not very good, just uncomment this block if you want to try it out
//			float2 lensCoord0 = 1.0 - coord.xy;
//			float2 lensCoord1 = (((lensCoord0 * 2.0 - 1.0) / 1.9) * 0.5 + 0.5);
//			float2 lensCoord2 = (((lensCoord0 * 2.0 - 1.0) / 1.3) * 0.5 + 0.5);
//			float2 lensCoord3 = (((lensCoord0 * 2.0 - 1.0) / 0.9) * 0.5 + 0.5);
//			float2 lensCoord4 = (((lensCoord0 * 2.0 - 1.0) / 0.5) * 0.5 + 0.5);
//			float2 lensCoord5 = (((lensCoord0 * 2.0 - 1.0) / 0.1) * 0.5 + 0.5);
//			float2 lensCoord6 = (((lensCoord0 * 2.0 - 1.0) / -0.05) * 0.5 + 0.5);
//			float2 lensCoord7 = (((lensCoord0 * 2.0 - 1.0) / -0.2) * 0.5 + 0.5);
//			float2 lensCoord8 = (((lensCoord0 * 2.0 - 1.0) / -0.8) * 0.5 + 0.5);
//			float2 lensCoord9 = (((lensCoord0 * 2.0 - 1.0) / -1.1) * 0.5 + 0.5);
//			float2 lensCoord10 = (((lensCoord0 * 2.0 - 1.0) / -2.1) * 0.5 + 0.5);
//			 
//			color.rgb += tex2D(_Bloom1, lensCoord0).rgb * 0.2 * 0.0005f;
//			color.rgb += tex2D(_Bloom1, lensCoord1).rgb * 0.2 * 0.001f;
//			color.rgb += tex2D(_Bloom1, lensCoord2).rgb * 0.2 * 0.0003f;
//			color.rgb += tex2D(_Bloom1, lensCoord3).rgb * 0.2 * 0.0008f;
//			color.rgb += tex2D(_Bloom1, lensCoord4).rgb * 0.2 * 0.0002f;
//			color.rgb += tex2D(_Bloom1, lensCoord5).rgb * 0.2 * 0.0007f;
//			color.rgb += tex2D(_Bloom1, lensCoord6).rgb * 0.2 * 0.0007f;
//			color.rgb += tex2D(_Bloom1, lensCoord7).rgb * 0.2 * 0.0007f;
//			color.rgb += tex2D(_Bloom1, lensCoord8).rgb * 0.2 * 0.0007f;
//			color.rgb += tex2D(_Bloom1, lensCoord9).rgb * 0.2 * 0.0007f;
//			color.rgb += tex2D(_Bloom1, lensCoord10).rgb * 0.2 * 0.0007f;

			return color;
		} 
		
		float4 fragBloomLQ ( v2f_simple i ) : COLOR
		{	
        	#if UNITY_UV_STARTS_AT_TOP
				float2 coord = i.uv2.xy;
			#else
				float2 coord = i.uv.xy;
			#endif
			float4 color =  decodeHdr(tex2D(_MainTex, coord));
			fixed3 lens = tex2D(_LensDirt, coord).rgb;
			
			fixed3 b0 = tex2D(_Bloom0, coord).rgb;
			fixed3 b1 = tex2D(_Bloom1, coord).rgb;
			fixed3 b2 = tex2D(_Bloom2, coord).rgb;
			fixed3 b3 = tex2D(_Bloom3, coord).rgb;
			fixed3 b4 = tex2D(_Bloom4, coord).rgb;
//			fixed3 b5 = tex2D(_Bloom5, coord).rgb;
//			fixed3 b6 = tex2D(_Bloom6, coord).rgb;
//			fixed3 b7 = tex2D(_Bloom7, coord).rgb;
			
//			return float4(b0.rgb, 1.0);
			
			
			fixed3 bloom = b0 * 0.5f
						 + b1 * 0.6f
						 + b2 * 0.6f
						 + b3 * 0.45f 
						 + b4 * 0.35f
//						 + b5 * 0.23f
//						 + b6 * 0.13f
//						 + b7 * 0.08f
						 ;
			
			bloom *= 0.3401f;

//			fixed3 lensDirtBloom = b0 * 1.0f
//							 + b1 * 0.8f
//							 + b2 * 0.6f
//							 + b3 * 0.45f
//							 + b4 * 0.35f
////							 + b5 * 0.23f
////							 + b6 * 0.13f
////							 + b7 * 0.08f
//							 ;
//			lensDirtBloom *= 0.2747f;

			color.rgb = lerp(color.rgb, bloom.rgb, Fixed3(_BloomIntensity));
//			color.r = lerp(color.r, lensDirtBloom.r, (saturate(lens.r * _LensDirtIntensity)));
//			color.g = lerp(color.g, lensDirtBloom.g, (saturate(lens.g * _LensDirtIntensity)));
//			color.b = lerp(color.b, lensDirtBloom.b, (saturate(lens.b * _LensDirtIntensity)));

			//color.rgb += bloom;
			return color;
		} 
		
		
		struct v2f_tap
		{
			float4 pos : SV_POSITION;
			float4 uv20 : TEXCOORD0;
			float4 uv21 : TEXCOORD1;
			float4 uv22 : TEXCOORD2;
			float4 uv23 : TEXCOORD3;
		};
		
		v2f_tap vert4Tap ( appdata_img v )
		{
			v2f_tap o;

			o.pos = UnityObjectToClipPos (v.vertex);
        	o.uv20 = float4(v.texcoord.xy + _MainTex_TexelSize.xy * float2(0.5h, 0.5h), 0.0, 0.0);				
			o.uv21 = float4(v.texcoord.xy + _MainTex_TexelSize.xy * float2(-0.5h,-0.5h), 0.0, 0.0);	
			o.uv22 = float4(v.texcoord.xy + _MainTex_TexelSize.xy * float2(0.5h,-0.5h), 0.0, 0.0);		
			o.uv23 = float4(v.texcoord.xy + _MainTex_TexelSize.xy * float2(-0.5h,0.5h), 0.0, 0.0);		
  
			return o; 
		}		
		
		float4 fragDownsample ( v2f_tap i ) : COLOR
		{				
			float4 color = tex2D (_MainTex, i.uv20.xy);
			return color;
			color += decodeHdr(tex2D (_MainTex, i.uv21.xy));
			color += decodeHdr(tex2D (_MainTex, i.uv22.xy));
			color += decodeHdr(tex2D (_MainTex, i.uv23.xy));
			color *= 0.25;
			return color;
		}
		
		static const float curve[7] = { 0.0205, 0.0855, 0.232, 0.324, 0.232, 0.0855, 0.0205 };

		static const float4 curve4[7] = { float4(0.0205,0.0205,0.0205,0), 
										 float4(0.0855,0.0855,0.0855,0), 
										 float4(0.232,0.232,0.232,0),
										 float4(0.324,0.324,0.324,1), 
										 float4(0.232,0.232,0.232,0), 
										 float4(0.0855,0.0855,0.0855,0), 
										 float4(0.0205,0.0205,0.0205,0) };
										 
		
		struct v2f_withBlurCoords8 
		{
			float4 pos : SV_POSITION;
			float4 uv : TEXCOORD0;
			float4 offs : TEXCOORD1;
		};		
		
		v2f_withBlurCoords8 vertBlurHorizontal (appdata_img v)
		{
			v2f_withBlurCoords8 o;
			o.pos = UnityObjectToClipPos (v.vertex);
			
			o.uv = float4(v.texcoord.xy,1,1);
//			o.offs = float4(_MainTex_TexelSize.xy * float2(1.0, 0.0) * _BlurSize,1,1);
			o.offs = float4(_MainTex_TexelSize.xy * float2(1.0, 0.0),1,1);

			return o; 
		}
		
		v2f_withBlurCoords8 vertBlurVertical (appdata_img v)
		{
			v2f_withBlurCoords8 o;
			o.pos = UnityObjectToClipPos (v.vertex);
			
			o.uv = float4(v.texcoord.xy,1,1);
//			o.offs = float4(_MainTex_TexelSize.xy * float2(0.0, 1.0) * _BlurSize,1,1);
			o.offs = float4(_MainTex_TexelSize.xy * float2(0.0, 1.0),1,1);
			 
			return o; 
		}	
		


		float4 fragBlur8 ( v2f_withBlurCoords8 i ) : COLOR
		{
//			float2 uv = i.uv.xy; 
//			float2 netFilterWidth = i.offs.xy;  
//			float2 coords = uv - netFilterWidth * 3.0;  
//			
//			float4 color = 0;
//  			for( int l = 0; l < 7; l++ )  
//  			{   
//				float4 tap = decodeHdr(tex2D(_MainTex, coords));
//				color += tap * curve4[l];
//				coords += netFilterWidth;
//  			}
//			return color;

			float2 uv = i.uv.xy;			

			const float offsets[3] = {0.0f, 1.38461f, 3.23076f};
			const float weights[3] = {0.22702f, 0.31624f, 0.07087f};
			
			float4 color = float4(0.0, 0.0, 0.0, 0.0);
			float4 cs = decodeHdr(tex2D(_MainTex, uv));
			//return cs;
			color = cs * weights[0];
			//float depth = color.a * (Luminance(color.rgb) * 1.0 + 2.0) * weights[0];
			//float depth = DecDepth(cs.a, color.rgb) * weights[0];

			float3 centerTap = color.rgb;
			
			for (int l = 1; l < 3; l++)
			{
				float4 tap1 = decodeHdr(tex2D(_MainTex, uv + offsets[l] * i.offs.xy * 1.0));
				float4 tap2 = decodeHdr(tex2D(_MainTex, uv - offsets[l] * i.offs.xy * 1.0));
				color += tap1 * weights[l] + tap2 * weights[l];
				//depth += tap1.a * (Luminance(tap1.rgb) + 2.0) * weights[l] + tap2.a * (Luminance(tap2.rgb) + 2.0) * weights[l];
				//depth += DecDepth(tap1.a, tap1.rgb) * weights[l] + DecDepth(tap2.a, tap2.rgb) * weights[l];
			}


			//depth = EncDepth(depth, color.rgb);
			
			//color.a = depth * 1.0;

			return color;
		}
		
		float4 fragClampSourceAndEncodeFogFactor ( v2f_simple i ) : COLOR
		{
			float4 color = float4(0.0, 0.0, 0.0, 1.0);
			color.rgb = decodeHdr(tex2D(_MainTex, i.uv.xy)).rgb;
			
			color.rgb = clamp(color.rgb, float3(0.0, 0.0, 0.0), float3(100000000.0, 100000000.0, 100000000.0));




			//color.a = tex2D(_CameraDepthTexture, i.uv.xy).x / (Luminance(color.rgb) * 1.0 + 2.0);
			float depth = tex2D(_CameraDepthTexture, i.uv.xy).x;
			float dist = LinearEyeDepth(depth);
			float fogFactor = 0.0;
			if (_DepthBlendFunction == 0)
			{
				fogFactor = 1.0 - exp(-dist * _DepthBlendFactor);
			}
			else
			{
				fogFactor = pow(1.0 - exp(-dist * _DepthBlendFactor), 2.0);
			}

			//color.a = EncDepth(Linear01Depth(depth), color.rgb);
			color.a = EncDepth(fogFactor, color.rgb);
			
			return color;
		}
		
	ENDCG

	SubShader 
	{
		ZTest Off Cull Off ZWrite Off Blend Off
		Fog {Mode off}
		
		Pass	//0 Main
		{
			CGPROGRAM
			#pragma vertex vertBloom
			#pragma fragment fragBlendBloomWithScene
			#pragma fragmentoption ARB_precision_hint_fastest 
			ENDCG
		}
		
		Pass 	//1 Downsample
		{ 	
			CGPROGRAM			
			#pragma vertex vert4Tap
			#pragma fragment fragDownsample
			#pragma fragmentoption ARB_precision_hint_fastest 			
			ENDCG		 
		}
		
		Pass 	//2 Blur Vertical
		{ 	
			CGPROGRAM			
			#pragma vertex vertBlurVertical
			#pragma fragment fragBlur8
			#pragma fragmentoption ARB_precision_hint_fastest 			
			ENDCG		 
		}
		
		Pass 	//3 Blur Horizontal
		{ 	
			CGPROGRAM			
			#pragma vertex vertBlurHorizontal
			#pragma fragment fragBlur8
			#pragma fragmentoption ARB_precision_hint_fastest 			
			ENDCG		 
		}
		
		Pass 	//4 Main LQ
		{
			CGPROGRAM
			#pragma vertex vertBloom
			#pragma fragment fragBloomLQ
			#pragma fragmentoption ARB_precision_hint_fastest 
			ENDCG
		}
		
		Pass 	//5 clamp
		{
			CGPROGRAM
			#pragma vertex vertBloom
			#pragma fragment fragClampSourceAndEncodeFogFactor
			#pragma fragmentoption ARB_precision_hint_fastest 
			ENDCG
		}
	} 
	FallBack Off
}
