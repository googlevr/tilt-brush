// Modified version of a tilt shift shader from Martin Jonasson (http://grapefrukt.com/)
// Read http://notes.underscorediscovery.com/ for context on shaders and this file
// License : MIT
// Adapted for Unity by The Tilt Brush Authors.

/*
	Take note that blurring in a single pass (the two for loops below) is more expensive than separating
	the x and the y blur into different passes. This was used where bleeding edge performance
	was not crucial and is to illustrate a point.
	The reason two passes is cheaper?
	   texture2D is a fairly high cost call, sampling a texture.
	   So, in a single pass, like below, there are 3 steps, per x and y.
	   That means a total of 9 "taps", it touches the texture to sample 9 times.
	   Now imagine we apply this to some geometry, that is equal to 16 pixels on screen (tiny)
	   (16 * 16) * 9 = 2304 samples taken, for width * height number of pixels, * 9 taps
	   Now, if you split them up, it becomes 3 for x, and 3 for y, a total of 6 taps
	   (16 * 16) * 6 = 1536 samples

	   That's on a *tiny* sprite, let's scale that up to 128x128 sprite...
	   (128 * 128) * 9 = 147,456
	   (128 * 128) * 6 =  98,304
	   That's 33.33..% cheaper for splitting them up.
	   That's with 3 steps, with higher steps (more taps per pass...)
	   A really smooth, 6 steps, 6*6 = 36 taps for one pass, 12 taps for two pass
	   You will notice, the curve is not linear, at 12 steps it's 144 vs 24 taps
	   It becomes orders of magnitude slower to do single pass!
	   Therefore, you split them up into two passes, one for x, one for y.
*/

Shader "FX/TiltShift" {
	Properties{
		_MainTex("Texture", 2D) = "white" {}
		_BlurAmount("Blur Amount", float) = 5.0
		_Center("Center", float) = 1.1
		_StepSize("Step Size", float) = 0.0002
		_Steps("Steps", float) = 16.0
	}
	SubShader{
		ZTest Always Cull Off ZWrite Off

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

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			sampler2D _MainTex;

			float _BlurAmount;
			float _Center;
			float _StepSize;
			float _Steps;

			fixed4 frag(v2f i) : SV_Target
			{
				//I am hardcoding the constants like a jerk
				float minOffs = (float(_Steps - 1.0)) / -2.0;
				float maxOffs = (float(_Steps - 1.0)) / +2.0;

				//Work out how much to blur based on the mid point
				float amount = pow((i.uv.y * _Center) * 2.0 - 1.0, 2.0) * _BlurAmount;
				
				//This is the accumulation of color from the surrounding pixels in the texture
				float4 blurred = float4(0.0, 0.0, 0.0, 1.0);
				
				//From minimum offset to maximum offset
				for (float offsX = minOffs; offsX <= maxOffs; ++offsX) {
					for (float offsY = minOffs; offsY <= maxOffs; ++offsY) {

						//copy the coord so we can mess with it
						float2 temp_tcoord = i.uv.xy;

						//work out which uv we want to sample now
						temp_tcoord.x += offsX * amount * _StepSize;
						temp_tcoord.y += offsY * amount * _StepSize;

						//accumulate the sample
						blurred += tex2D(_MainTex, temp_tcoord);

					} //for y
				} //for x

				//because we are doing an average, we divide by the amount (x AND y, hence steps * steps)
				blurred /= float(_Steps * _Steps);
				blurred.a = 1.0;
				
				//return the final blurred color
				return blurred;
			}
			ENDCG
		}
	}
	FallBack "Diffuse"
}
