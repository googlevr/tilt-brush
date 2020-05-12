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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush {

using VertexLayout = GeometryPool.VertexLayout;

/// MasterBrush holds geometry in progress.  It's used for both active stroke and the
/// preview stroke, and handles decay of preview brush for certain brush types.  Typical
/// use would push the geometry here down to the Unity mesh every frame.
///
/// ActiveStroke would be a better name.
public class MasterBrush : IPoolable {
  // Static

  // Taken from the old value used by our Pointer prefabs
  public const int kActiveStrokeQuads = 1000;

  private static Pool<MasterBrush> sm_pool = new Pool<MasterBrush>();
  public static Pool<MasterBrush> Pool {
    get { return sm_pool; }
  }

  // Instance

  private int m_NumVerts;

  public Vector3[] m_Vertices;
  public int[]     m_Tris;
  public Vector3[] m_Normals;
  public Vector2[] m_UVs;
  public List<Vector3> m_UVWs;
  public Color32[] m_Colors;
  public Vector4[] m_Tangents;

  private VertexLayout? m_VertexLayout;

  /// MasterBrush only supports a subset of possible VertexLayout options.
  /// If you need more, use GeometryBrush instead.
  public VertexLayout? VertexLayout {
    get {
      return m_VertexLayout;
    }
    set {
      if (value != null) {
        // Some sanity checking
        VertexLayout layout = value.Value;
        if (layout.texcoord0.size == 2) {
          Debug.Assert(layout.texcoord0.semantic == GeometryPool.Semantic.XyIsUv);
        } else if (layout.texcoord0.size == 3) {
          Debug.Assert(layout.texcoord0.semantic == GeometryPool.Semantic.XyIsUvZIsDistance);
        } else {
          Debug.LogError("Bad uv0 size");
        }
        Debug.Assert(layout.texcoord1.size == 0);
      }
      m_VertexLayout = value;
    }
  }

  public int NumVerts {
    get { return m_NumVerts; }
  }

  public MasterBrush() {
    int numQuads = kActiveStrokeQuads;

    m_NumVerts = numQuads * 6;
    m_Vertices = new Vector3[m_NumVerts];
    m_Tris = new int[m_NumVerts];
    m_Normals = new Vector3[m_NumVerts];
    m_UVs = new Vector2[m_NumVerts];
    m_UVWs = new List<Vector3>();
    m_UVWs.SetCount(m_NumVerts);
    m_Colors = new Color32[m_NumVerts];
    m_Tangents = new Vector4[m_NumVerts];

    for (int i = 0; i < numQuads; ++i) {
      int iVertGroupIndex = i * 6;

      m_UVs[iVertGroupIndex] = new Vector2(0.0f, 0.0f);
      m_UVs[iVertGroupIndex + 1] = new Vector2(1.0f, 0.0f);
      m_UVs[iVertGroupIndex + 2] = new Vector2(0.0f, 1.0f);
      m_UVs[iVertGroupIndex + 3] = new Vector2(0.0f, 1.0f);
      m_UVs[iVertGroupIndex + 4] = new Vector2(1.0f, 0.0f);
      m_UVs[iVertGroupIndex + 5] = new Vector2(1.0f, 1.0f);

      for (int j = 0; j < 6; ++j) {
        int iVertIndex = iVertGroupIndex + j;
        m_UVWs[iVertIndex] = new Vector3(m_UVs[iVertIndex].x, m_UVs[iVertIndex].y, 0);
        m_Vertices[iVertIndex] = Vector3.zero;
        m_Normals[iVertIndex] = Vector3.up;
        m_Tris[iVertIndex] = iVertIndex;
        m_Colors[iVertIndex] = Color.white;
        m_Tangents[iVertIndex] = Vector4.zero;
      }
    }
  }

  public void Reset(int numVerts = -1) {
    Array.Clear(m_Vertices, 0, numVerts < 0 ? m_NumVerts : numVerts);
    m_VertexLayout = null;
  }

  // IPoolable

  public void OnPoolPut() {}

  public void OnPoolGet() {
    Reset();
  }
}
}  // namespace TiltBrush
