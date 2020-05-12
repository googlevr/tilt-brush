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

#define DLL_API __declspec(dllexport)

struct Vector3 {
  float x, y, z;
};

struct Vector4 {
  float x, y, z, w;
};

struct Matrix4x4 {
  float data[16];
};

extern "C" {
  DLL_API void TransformVector3AsPoint(Matrix4x4 mat, int iVert, int iVertEnd, Vector3 v3[]);
  DLL_API void TransformVector3AsVector(Matrix4x4 mat, int iVert, int iVertEnd, Vector3 v3[]);
  DLL_API void TransformVector3AsZDistance(float scale, int iVert, int iVertEnd, Vector3 v3[]);
  DLL_API void TransformVector4AsPoint(Matrix4x4 mat, int iVert, int iVertEnd, Vector4 v4[]);
  DLL_API void TransformVector4AsVector(Matrix4x4 mat, int iVert, int iVertEnd, Vector4 v4[]);
  DLL_API void TransformVector4AsZDistance(float scale, int iVert, int iVertEnd, Vector4 v4[]);
  DLL_API void GetBoundsFor(Matrix4x4 m, int iVert, int iVertEnd, Vector3 v3[],
                            Vector3* center, Vector3* size);
}
