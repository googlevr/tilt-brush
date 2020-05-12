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

#include "TiltBrushCpp.h"

#define idx(c,r) (4 * c + r)

extern "C" {
  /// Apply a transform to a subset of an array of Vector3 elements as points.
  /// Pass:
  ///   m        - the transform to apply.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v3       - the array of vectors to transform
  void TransformVector3AsPoint(Matrix4x4 m, int iVert, int iVertEnd, Vector3 v3[]) {
    for (int i = iVert; i < iVertEnd; i++) {
      float x = v3[i].x;
      float y = v3[i].y;
      float z = v3[i].z;

      v3[i].x = m.data[idx(0,0)]*x + m.data[idx(1,0)]*y + m.data[idx(2,0)]*z + m.data[idx(3,0)];
      v3[i].y = m.data[idx(0,1)]*x + m.data[idx(1,1)]*y + m.data[idx(2,1)]*z + m.data[idx(3,1)];
      v3[i].z = m.data[idx(0,2)]*x + m.data[idx(1,2)]*y + m.data[idx(2,2)]*z + m.data[idx(3,2)];
    }
  }

  /// Apply a transform to a subset of an array of Vector3 elements as vectors.
  /// Pass:
  ///   m        - the transform to apply.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v3       - the array of vectors to transform
  void TransformVector3AsVector(Matrix4x4 m, int iVert, int iVertEnd, Vector3 v3[]) {

    for (int i = iVert; i < iVertEnd; i++) {
      float x = v3[i].x;
      float y = v3[i].y;
      float z = v3[i].z;

      v3[i].x = m.data[idx(0,0)]*x + m.data[idx(1,0)]*y + m.data[idx(2,0)]*z;
      v3[i].y = m.data[idx(0,1)]*x + m.data[idx(1,1)]*y + m.data[idx(2,1)]*z;
      v3[i].z = m.data[idx(0,2)]*x + m.data[idx(1,2)]*y + m.data[idx(2,2)]*z;
    }
  }

  /// Apply a transform to a subset of an array of Vector3 elements as Z distances.
  /// Pass:
  ///   scale    - the scale of the transform.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v3       - the array of vectors to transform
  void TransformVector3AsZDistance(float scale, int iVert, int iVertEnd, Vector3 v3[]) {
    for (int i = iVert; i < iVertEnd; i++) {
      v3[i].z *= scale;
    }
  }

  /// Apply a transform to a subset of an array of Vector4 elements as points.
  /// Pass:
  ///   m        - the transform to apply.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v4       - the array of vectors to transform
  void TransformVector4AsPoint(Matrix4x4 m, int iVert, int iVertEnd, Vector4 v4[]) {
    for (int i = iVert; i < iVertEnd; i++) {
      float x = v4[i].x;
      float y = v4[i].y;
      float z = v4[i].z;

      v4[i].x = m.data[idx(0,0)]*x + m.data[idx(1,0)]*y + m.data[idx(2,0)]*z + m.data[idx(3,0)];
      v4[i].y = m.data[idx(0,1)]*x + m.data[idx(1,1)]*y + m.data[idx(2,1)]*z + m.data[idx(3,1)];
      v4[i].z = m.data[idx(0,2)]*x + m.data[idx(1,2)]*y + m.data[idx(2,2)]*z + m.data[idx(3,2)];
    }
  }

  /// Apply a transform to a subset of an array of Vector4 elements as vectors.
  /// Pass:
  ///   m        - the transform to apply.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v4       - the array of vectors to transform
  void TransformVector4AsVector(Matrix4x4 m, int iVert, int iVertEnd, Vector4 v4[]) {
    for (int i = iVert; i < iVertEnd; i++) {
      float x = v4[i].x;
      float y = v4[i].y;
      float z = v4[i].z;

      v4[i].x = m.data[idx(0,0)]*x + m.data[idx(1,0)]*y + m.data[idx(2,0)]*z;
      v4[i].y = m.data[idx(0,1)]*x + m.data[idx(1,1)]*y + m.data[idx(2,1)]*z;
      v4[i].z = m.data[idx(0,2)]*x + m.data[idx(1,2)]*y + m.data[idx(2,2)]*z;
    }
  }

  /// Apply a transform to a subset of an array of Vector4 elements as Z distances.
  /// Pass:
  ///   scale    - the scale of the transform.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v4       - the array of vectors to transform
  void TransformVector4AsZDistance(float scale, int iVert, int iVertEnd, Vector4 v4[]) {
    for (int i = iVert; i < iVertEnd; i++) {
      v4[i].z *= scale;
    }
  }

  bool IsIdentity(const Matrix4x4 &m) {
    if (m.data[idx(0,0)]==1 && m.data[idx(1,0)]==0 && m.data[idx(2,0)]==0 && m.data[idx(3,0)]==0 &&
        m.data[idx(0,1)]==0 && m.data[idx(1,1)]==1 && m.data[idx(2,1)]==0 && m.data[idx(3,1)]==0 &&
        m.data[idx(0,2)]==0 && m.data[idx(1,2)]==0 && m.data[idx(2,2)]==1 && m.data[idx(3,2)]==0 &&
        m.data[idx(0,3)]==0 && m.data[idx(1,3)]==0 && m.data[idx(2,3)]==0 && m.data[idx(3,3)]==1) {
      return true;
    }
    return false;
  }

  /// Get the bounds for a transformed subset of an array of Vector3 point elements.
  /// Pass:
  ///   m        - the transform to apply.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v3       - the array of vectors to transform
  /// Output:
  ///   center   - the center of the bounds.
  ///   size     - the size of the bounds.
  void GetBoundsFor(Matrix4x4 m, int iVert, int iVertEnd, Vector3 v3[],
      Vector3* center, Vector3* size) {
    float minX, minY, minZ;
    float maxX, maxY, maxZ;

    if (iVert < iVertEnd) {
      float x = v3[iVert].x;
      float y = v3[iVert].y;
      float z = v3[iVert].z;

      minX = maxX = m.data[idx(0,0)]*x + m.data[idx(1,0)]*y + m.data[idx(2,0)]*z + m.data[idx(3,0)];
      minY = maxY = m.data[idx(0,1)]*x + m.data[idx(1,1)]*y + m.data[idx(2,1)]*z + m.data[idx(3,1)];
      minZ = maxZ = m.data[idx(0,2)]*x + m.data[idx(1,2)]*y + m.data[idx(2,2)]*z + m.data[idx(3,2)];
    } else {
      return;
    }

    if (IsIdentity(m)) {
      for (int i = iVert + 1; i < iVertEnd; i++) {
        float x = v3[i].x;
        float y = v3[i].y;
        float z = v3[i].z;

        if (minX > x) {
          minX = x;
        } else if (maxX < x) {
          maxX = x;
        }
        if (minY > y) {
          minY = y;
        } else if (maxY < y) {
          maxY = y;
        }
        if (minZ > z) {
          minZ = z;
        } else if (maxZ < z) {
          maxZ = z;
        }
      }
    } else {
      for (int i = iVert + 1; i < iVertEnd; i++) {
        float x = v3[i].x;
        float y = v3[i].y;
        float z = v3[i].z;

        float newX = m.data[idx(0,0)]*x + m.data[idx(1,0)]*y + m.data[idx(2,0)]*z + m.data[idx(3,0)];
        float newY = m.data[idx(0,1)]*x + m.data[idx(1,1)]*y + m.data[idx(2,1)]*z + m.data[idx(3,1)];
        float newZ = m.data[idx(0,2)]*x + m.data[idx(1,2)]*y + m.data[idx(2,2)]*z + m.data[idx(3,2)];

        if (minX > newX) {
          minX = newX;
        } else if (maxX < newX) {
          maxX = newX;
        }
        if (minY > newY) {
          minY = newY;
        } else if (maxY < newY) {
          maxY = newY;
        }
        if (minZ > newZ) {
          minZ = newZ;
        } else if (maxZ < newZ) {
          maxZ = newZ;
        }
      }
    }

    center->x = 0.5f * (minX + maxX);
    center->y = 0.5f * (minY + maxY);
    center->z = 0.5f * (minZ + maxZ);
    size->x = maxX - minX;
    size->y = maxY - minY;
    size->z = maxZ - minZ;
  }
}
