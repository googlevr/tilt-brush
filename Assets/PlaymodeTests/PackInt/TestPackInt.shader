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

Shader "Tests/TestPackInt" {

Properties {
  // _MainTex ("Base (RGB)", 2D) = "white" {}
  _MainTex ("Base (RGB)", 2D) = "" {}
}

CGINCLUDE
  #include "Assets/Shaders/Include/PackInt.cginc"

  struct appdata_t {
    float4 pos : POSITION;
    float2 uv : TEXCOORD0;
  };

  struct v2f {
    float4 pos : POSITION;
    float2 uv : TEXCOORD0;
  };

  #pragma vertex vert
  #pragma fragment frag

  #define SUCCESS fixed3(0,1,0)
  // An assert that returns the color red on failure
  #define assert(condition) if (!all(condition)) return fixed3(1, 0.125, 0)
  // An assert that returns a user-defined color on failure
  #define assertf(color, condition) if (!all(condition)) return (color)
  // If the status color is not green, propagate it
  #define assertIsGreen(color_) {               \
    fixed3 color = (color_);                    \
    if (!all(color == SUCCESS)) {               \
      return color;                             \
    }                                           \
  }

  v2f vert(appdata_t v) {
    v2f o;
    o.pos = UnityObjectToClipPos(v.pos);
    o.uv = v.uv;
    return o;
  }

  // Returns integers in [0, 256)
  uint2 GetPixelCoord(v2f i) {
    float2 kRenderTargetSize = float2(256, 256);
    return i.uv.xy * kRenderTargetSize - float2(0.5, 0.5);
  }

  // Returns a uint16 based on the fragment being rendered
  uint2 GetTestUint16x2(v2f i) {
    uint2 coord = GetPixelCoord(i);
    uint ui16 = coord.y << 8 | coord.x;
    // Vary one word and leave the other one constant, to avoid correlation effects.
    // But also, test both the high and low words.
    // The value 0x44cc was chosen because has a resonable number of bits set.
    if (((coord.x ^ coord.y) & 1) == 1) {
      return uint2(0x44cc, ui16);
    } else {
      return uint2(ui16, 0x44cc);
    }
  }
ENDCG

SubShader {
  Tags {
    "IgnoreProjector" = "True"
    "Queue" = "Geometry"
    "RenderType" = "Opaque"
    "DisableBatching" = "True"
  }
  Lighting Off
  Cull Off
  Blend Off

  // Pass 0 is a self-contained unit test
  Pass {
    CGPROGRAM

    // Returns a uint16 based on the fragment being rendered
    uint GetTestUint16(v2f i) {
      uint2 coord = GetPixelCoord(i);
      return coord.y << 8 | coord.x;
    }

    // Test that Unpack(Pack(arg)) == arg
    fixed3 Test_PackAndUnpack(uint2 values) {
      assertf(fixed3(1,0,1), ((values & uint2(0xffff, 0xffff)) == values));

      half4 packedValues = PackUint16x2ToRgba8(values);
      uint2 values2 = UnpackRgba8ToUint16x2(packedValues);
      fixed3 ret = fixed3(0,1,0);
      assertf(fixed3(1,0,0), (values == values2));
      return SUCCESS;
    }

    fixed3 RunAllTests(v2f i) {
      uint ui16 = GetTestUint16(i);
      assertIsGreen(Test_PackAndUnpack(uint2(ui16, ui16)));
      assertIsGreen(Test_PackAndUnpack(uint2(0xffff, ui16)));
      assertIsGreen(Test_PackAndUnpack(uint2(ui16, 0xffff)));
      return SUCCESS;
    }

    half4 frag(v2f i) : COLOR {
      return float4(RunAllTests(i), 1);
    }

    ENDCG
  }

  // Passes 1 and 2 test that the value round-trips properly through a RenderTarget
  Pass {
    CGPROGRAM
    half4 frag(v2f i) : COLOR {
      return PackUint16x2ToRgba8(GetTestUint16x2(i));
    }
    ENDCG
  }

  Pass {
    CGPROGRAM
    sampler2D _MainTex;

    fixed3 Test_UnpackFromSample(v2f i) {
      uint2 actual = UnpackRgba8ToUint16x2(tex2D(_MainTex, i.uv));
      uint2 expected = GetTestUint16x2(i);
      assertf(fixed3(.25, 0, .25), all(actual == expected));
      return SUCCESS;
    }

    half4 frag(v2f i) : COLOR {
      return float4(Test_UnpackFromSample(i), 1);
    }
    ENDCG
  }

  // Pass 3 renders UVs for testing Blit on Android (which doesn't seem to work properly)
  Pass {
    CGPROGRAM
    half4 frag(v2f i) : COLOR {
      return float4((i.uv.x + 1) / 2, (i.uv.y + 1) / 2, 0.75, 1);
    }
    ENDCG
  }

  // Pass 4 is a copy shader, to replace 2-argument Blit() on Android
  Pass {
    CGPROGRAM
    sampler2D _MainTex;
    half4 frag(v2f i) : COLOR {
      return float4(tex2D(_MainTex, i.uv).rgb, 1);
    }
    ENDCG
  }

  // Pass 5 unpacks then repacks. It's not used for any tests currently,
  // but is useful for debugging, in conjunction with writing RenderTextures to files.
  Pass {
    CGPROGRAM
    sampler2D _MainTex;
    half4 frag(v2f i) : COLOR {
      float4 color = tex2D(_MainTex, i.uv);
      uint2 unpacked = UnpackRgba8ToUint16x2(color);
      return PackUint16x2ToRgba8(unpacked);
    }
    ENDCG
  }
} // SubShader

} // Shader
