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

// Packs two 16-bit ints in a form that can round-trip through an RGBA8 buffer.
float4 PackUint16x2ToRgba8(uint2 values) {
  // TODO: Danger!
  // https://www.khronos.org/registry/OpenGL/specs/gl/glspec46.core.pdf
  // Section 2.3.5.2 says that float -> unorm conversion isn't guaranteed to be
  // round-to-nearest. For full portability, we might need to sacrifice one bit per
  // channel with something like "(u1 >> 7) & 0x7f" and maybe add 0.5f.

  // In order to safely round-trip from float4 -> rgba -> float4,
  // the result must be in [0,1] and a multiple of 1/255.

  // Danger 2!
  // See https://support.unity3d.com/hc/en-us/requests/658516/
  // Unity 2017.x has a shader-compiler bug that generates invalid
  // Oculus Quest shader code when bitfieldExtract(SV_PrimitiveID) is called.
  // This failure shows up at RUNTIME, not at build time.
  //
  // The workaround is to convince the compiler to use vector operations as much
  // as possible. Do not make any changes to this function without testing on-device.

  // This packs two u16 values into a float4, which will become an 8 bit per channel RGBA texture
  // after exiting the fragment shader. The following code does this using vector operations.
  uint4 ucolor = values.xxyy;                          // Load the two u16 values
  ucolor &= uint4(0xff00, 0xff, 0xff00, 0xff);         // AND to get the high and low bytes of each
  return ucolor / float4(0xff00, 0xff, 0xff00, 0xff);  // Divide through to get to 0 - 1 float range
}

// Undoes PackUint16x2ToRgba8
uint2 UnpackRgba8ToUint16x2(float4 rgba8) {
  // no round() in gles 2.0
  float4 f255 = floor(rgba8 * 255.0 + float4(0.5, 0.5, 0.5, 0.5));
  uint4 u255 = f255;
  uint u1 = (u255.x << 8) | u255.y;
  uint u2 = (u255.z << 8) | u255.w;
  return uint2(u1, u2);
}
