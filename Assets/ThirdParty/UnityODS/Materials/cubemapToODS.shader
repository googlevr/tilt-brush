Shader "Hidden/cubemapToODS"
{
	Properties
	{
		_MainTex("Cubemap", Cube) = "black" {}
		_LeftTex( "Cubemap", Cube) = "black" {}
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100

		ZTest  Always
		Cull   Off
		ZWrite Off
		Fog{ Mode off }

		Pass
		{
			CGPROGRAM
			#pragma vertex   vert
			#pragma fragment frag
			#pragma multi_compile __ VR_180 ANAGLYPH

			float4  _uvTransform;
			float4	_orientation;
			float2  _scaleOffset;
			samplerCUBE _MainTex;
			samplerCUBE _LeftTex;
			
			#include "UnityCG.cginc"

			struct v2f
			{
				float4 vertex   : SV_POSITION;
				float2 texcoord : TEXCOORD0;
			};

			v2f vert(appdata_img v)
			{
				v2f o;
				o.vertex   = float4( v.texcoord.x*2.0-1.0, v.texcoord.y*2.0-1.0, 0.0, 1.0 );
				o.texcoord = v.texcoord;
				return o;
			}
			
			float3 computeRayDir(float2 tc)
			{
				//calculate the correct direction vector.
				float PI = 3.1415926535897932384626433832795;
				float4 scaleOffset = float4( 2.0f*PI, -PI, -PI, PI*0.5 );
				
				//given the coordinates, generate the angles.
				float2 angles = tc*scaleOffset.xy + scaleOffset.zw;

				//build a direction vector.
				float2 angleCos = cos(angles);
				float2 angleSin = sin(angles);
				return float3(angleSin.x * angleCos.y, angleSin.y, -angleCos.x * angleCos.y);
			}

			float3 computeRotatedDirection(float2 tc)
			{
				float3 dir = computeRayDir(tc);

				//rotate by the desired camera orientation,
				//_orientation is a quaternion.
				float4 q = _orientation;
				return dir + 2.0*cross(q.xyz, cross(q.xyz, dir) + q.w*dir);
			}

			float wrapX(float inX)
			{
				float outX = (1.0 - inX)*_scaleOffset.x + _scaleOffset.y;
				if (outX < 0.0) { outX += 1.0; }
				if (outX > 1.0) { outX -= 1.0; }
				return outX;
			}
			
			float4 stereoAnaglyph(float2 inTC)
			{
				//calculate the correct direction vector.
				float3 dir = computeRayDir(inTC);

				//Red-Cyan Anaglyph
				float4 outColor;
				outColor.r  = texCUBE(_MainTex, dir).r;
				outColor.gb = texCUBE(_LeftTex, dir).gb;
				outColor.a  = 1.0;
				
				return outColor;
			}
			
			float4 stereoStacked(float2 inTC)
			{
				bool  top = (inTC.y < 0.5);
				float yOffset = top ? 0.0 : -1.0;

				//remap the x coordinate based on the bloom padding (Tiltbrush specific)
				float xp = wrapX(inTC.x);

				//calculate the proper uv's since we are packing two images into one.
				float2 tc = float2(xp, inTC.y*2.0 + yOffset);

				//calculate the correct direction vector.
				float3 dir = computeRotatedDirection(tc);

				//read from the proper cubemap (left or right)
				float4 outColor;
				if (top) { outColor = texCUBE(_LeftTex, dir); }
				else     { outColor = texCUBE(_MainTex, dir); }
				return outColor;
			}

			float4 stereoSBS(float2 inTC)
			{
				bool left = (inTC.x < 0.5);
				float xp = inTC.x * 2.0;
				if (!left) { xp = xp - 1.0; }

				//remap the x coordinate based on the bloom padding (Tiltbrush specific)
				xp = wrapX(xp);

				//we're capturing 180 instead of 360 degrees, we can cheat a bit with the x component here to focus
				//on the center half of the view (i.e. 180 degrees); this changes the range exactly from
				// [-PI, +PI] to [-PI/2, +PI/2]
				xp = xp*0.5 + 0.25;

				//calculate the proper uv's since we are packing two images into one.
				float2 tc = float2(xp, inTC.y);

				//calculate the correct direction vector.
				float3 dir = computeRotatedDirection(tc);

				//read from the proper cubemap (left or right)
				float4 outColor;
				if (left) { outColor = texCUBE(_LeftTex, dir); }
				else      { outColor = texCUBE(_MainTex, dir); }
				return outColor;
			}

			float4 frag(v2f i) : SV_Target
			{
				#if defined(VR_180)
					return stereoSBS(i.texcoord.xy);
				#elif defined(ANAGLYPH)
					return stereoAnaglyph(i.texcoord.xy);
				#else //360 degree capture
					return stereoStacked(i.texcoord.xy);
				#endif
			}
			ENDCG
		}
	}
}
