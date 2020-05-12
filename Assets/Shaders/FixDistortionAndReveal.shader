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

Shader "Custom/FixDistortionAndReveal" {
  Properties {
      _RayOffsetX ("Ray Offset X", Float) = 0.0
      _RayOffsetY ("Ray Offset Y", Float) = 0.0
      _RayScaleX ("Ray Scale X", Float) = 0.0
      _RayScaleY ("Ray Scale Y", Float) = 0.0
      _MainTex ("Image", 2D) = "" {}
      _ColorRampTex("Color Ramp", 2D) = "" {}
      _DistortX ("Distortion X", 2D) = "" {}
      _DistortY ("Distortion Y", 2D) = "" {}

      _ForwardDirection ("Forward", Vector) = (0,0,1,1)
      _LocalSpaceOffset("Local Space Offset", Float) = 0

  }

  SubShader {
      Lighting Off
      Cull Off
      Zwrite Off
      Blend One One

    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "UnityCG.cginc"
      #pragma target 3.0
      #pragma glsl

      uniform float _RayOffsetX;
      uniform float _RayOffsetY;
      uniform float _RayScaleX;
      uniform float _RayScaleY;
      uniform sampler2D _MainTex;
      uniform sampler2D _DistortX;
      uniform sampler2D _DistortY;
      uniform sampler2D _ColorRampTex;
      float3 _ForwardDirection;
      float _LocalSpaceOffset;
      uniform float3 _WorldSpaceOVRCameraPos;
      uniform float3 _OVRCameraForward;
      int _BoostARFeed;


      struct fragment_input{
        float4 position : SV_POSITION;
        float2 uv : TEXCOORD0;
        float4 worldPos : TEXCOORD1;
      };

      fragment_input vert(appdata_img v) {
        fragment_input o;
        o.position = UnityObjectToClipPos(v.vertex);
        o.uv = MultiplyUV(UNITY_MATRIX_TEXTURE0, v.texcoord);
        o.worldPos = mul(unity_ObjectToWorld, v.vertex + float4(_LocalSpaceOffset,0,0,0));
        return o;
      }

      float4 frag(fragment_input input) : COLOR {

        // Unwarp the point. Ray range is [-4, 4] X [-4, 4].
        float2 ray = input.uv * float2(8.0, 8.0) - float2(4.0, 4.0);
        float2 texDist = float2(_RayOffsetX, _RayOffsetY) + ray * float2(_RayScaleX, _RayScaleY);

        // Decode X and Y position floats from RGBA and rescale to [-0.6, 1.7).
        float rawTexX = tex2D(_DistortX, texDist);
        float texImageX = DecodeFloatRGBA( rawTexX );
        texImageX = texImageX * 2.3 - 0.6;
        float rawTexY = tex2D(_DistortY, texDist);
        float texImageY = DecodeFloatRGBA(rawTexY);
        texImageY = texImageY * 2.3 - 0.6;

        // Find the undistorted pixel location.
        float2 texCoord = float2(texImageX, texImageY);

        float a = tex2D(_MainTex, texCoord).a;
        float4 color = a;
        color.w = 1;

        // Custom visuals for VR Sketch
        {
        // Calculate the world space ray from the camera to this pixel
        color = saturate(color);
        float3 dirToPixel = normalize(_WorldSpaceOVRCameraPos - input.worldPos);
        half dir_to_pixel_dot_forward = saturate(dot(dirToPixel, normalize(_ForwardDirection)));

        half ar_visibility =  saturate(dir_to_pixel_dot_forward + .6);

        // Boost AR visibility when you back away from the camera
        half lean_back_visibility = saturate( (1 - (_WorldSpaceOVRCameraPos.z+1) ) * .02f);
        ar_visibility += lean_back_visibility;

        // Dim the image in areas where we're working
        color.rgb *= ar_visibility;

        if (_BoostARFeed)
        {
          ar_visibility = 2;
        }
        else
        {
          // Adjust the gamma of the image based on the view direction
          half ar_power = lerp(3,1, dir_to_pixel_dot_forward);
          color.rgb = pow(color.rgb, ar_power) * 5.0f;
        }

        // Recolor the final image to make it look cooler and higher res
        color.rgb = tex2D(_ColorRampTex, half2(color.r,.5f)).rgb;

        // Additional boost for AR mode
        if (_BoostARFeed) color.rgb *= 3;

        // Add Scan lines
        color.rgb *= sin(input.worldPos.y * 100);
        }

        return color;
      }
      ENDCG
    }
  }
}
