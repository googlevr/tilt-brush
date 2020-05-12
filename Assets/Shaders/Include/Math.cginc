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

// -*- c -*-

// Factors a transform matrix into rotation and localScale. This
// assumes the transform's scale is only local (ie, shear-free).
void factorRotationAndLocalScale(
    float3x3 transform,
    out float3x3 rotate,
    out float3 localScale) {
  // We can extract the pure rotation of a non-skew 3x3 matrix by
  // factoring out the scale. The scale can be determined by the
  // fact that the first upper left 3x3 matrix is the rotation
  // matrix R multiplied by the scale matrix S and the fact that
  // (R * S)^T * (R * S) = S * S.

  // Calculate the scale squared matrix.
  float3x3 scaleSquaredMatrix = mul(transpose(transform), transform);

  // Calculate the inverse scale diagonal.
  float3 scaleSquaredDiagonal = float3(
      scaleSquaredMatrix[0][0],
      scaleSquaredMatrix[1][1],
      scaleSquaredMatrix[2][2]);
  float3 inverseScaleDiagonal = rsqrt(scaleSquaredDiagonal);

  // The rotate matrix is the unscaled version of the original transform.
  rotate = float3x3(
      transform[0] * inverseScaleDiagonal,
      transform[1] * inverseScaleDiagonal,
      transform[2] * inverseScaleDiagonal);

  // Calculate the local scale.
  localScale = scaleSquaredDiagonal * inverseScaleDiagonal;
}

// The inverse of a matrix should almost never be calculated inside a shader
// because it's too computationally expensive. But it may be useful as for
// testing.
float4x4 inverseSlow(float4x4 input) {
  #define minor(a, b, c) determinant(float3x3(input.a, input.b, input.c))
  float4x4 cofactors = float4x4(
      minor(_22_23_24, _32_33_34, _42_43_44),
     -minor(_21_23_24, _31_33_34, _41_43_44),
      minor(_21_22_24, _31_32_34, _41_42_44),
     -minor(_21_22_23, _31_32_33, _41_42_43),

     -minor(_12_13_14, _32_33_34, _42_43_44),
      minor(_11_13_14, _31_33_34, _41_43_44),
     -minor(_11_12_14, _31_32_34, _41_42_44),
      minor(_11_12_13, _31_32_33, _41_42_43),

      minor(_12_13_14, _22_23_24, _42_43_44),
     -minor(_11_13_14, _21_23_24, _41_43_44),
      minor(_11_12_14, _21_22_24, _41_42_44),
     -minor(_11_12_13, _21_22_23, _41_42_43),

     -minor(_12_13_14, _22_23_24, _32_33_34),
      minor(_11_13_14, _21_23_24, _31_33_34),
     -minor(_11_12_14, _21_22_24, _31_32_34),
      minor(_11_12_13, _21_22_23, _31_32_33)
  );
  #undef minor
  return transpose(cofactors) / determinant(input);
}

// The inverse of a matrix should almost never be calculated inside a shader
// because it's too computationally expensive. But it may be useful as for
// testing.
float3x3 inverseSlow(float3x3 input) {
  #define minor(a, b) determinant(float2x2(input.a, input.b))
  float3x3 cofactors = float3x3(
      minor(_22_23, _32_33),
     -minor(_21_23, _31_33),
      minor(_21_22, _31_32),

     -minor(_12_13, _32_33),
      minor(_11_13, _31_33),
     -minor(_11_12, _31_32),

      minor(_12_13, _22_23),
     -minor(_11_13, _21_23),
      minor(_11_12, _21_22)
  );
  #undef minor
  return transpose(cofactors) / determinant(input);
}

//
// Quaternion HLSL
// Taken from -> http://www.geeks3d.com/20141201/how-to-rotate-a-vertex-by-a-quaternion-in-glsl/
//

float4 quat_from_axis_angle(float3 axis, float angle) {
  float4 qr;
  float half_angle = (angle * 0.5) * 3.14159 / 180.0;
  qr.x = axis.x * sin(half_angle);
  qr.y = axis.y * sin(half_angle);
  qr.z = axis.z * sin(half_angle);
  qr.w = cos(half_angle);
  return qr;
}

float4 quat_conj(float4 q) {
  return float4(-q.x, -q.y, -q.z, q.w);
}

float4 quat_mult(float4 q1, float4 q2) {
  float4 qr;
  qr.x = (q1.w * q2.x) + (q1.x * q2.w) + (q1.y * q2.z) - (q1.z * q2.y);
  qr.y = (q1.w * q2.y) - (q1.x * q2.z) + (q1.y * q2.w) + (q1.z * q2.x);
  qr.z = (q1.w * q2.z) + (q1.x * q2.y) - (q1.y * q2.x) + (q1.z * q2.w);
  qr.w = (q1.w * q2.w) - (q1.x * q2.x) - (q1.y * q2.y) - (q1.z * q2.z);
  return qr;
}

float3 rotate_vertex_position(float3 position, float3 axis, float angle) {
  float4 qr = quat_from_axis_angle(axis, angle);
  float4 qr_conj = quat_conj(qr);
  float4 q_pos = float4(position.x, position.y, position.z, 0);

  float4 q_tmp = quat_mult(qr, q_pos);
  qr = quat_mult(q_tmp, qr_conj);

  return float3(qr.x, qr.y, qr.z);
}

// remaps x from the range [x1, x2] to the range [y1, y2]
float remap(float x1,float x2,float y1,float y2, float x)
{
  float t = (x-x1)/(x2-x1);
  return lerp(y1,y2,t);
}

// remaps x from the range [x1, x2] to the range [y1, y2] and clamps it to the range
float clampedRemap(float x1,float x2,float y1,float y2, float x)
{
  float t = clamp(x1,x2,x);
  return remap(x1,x2,y1,y2,t);
}
