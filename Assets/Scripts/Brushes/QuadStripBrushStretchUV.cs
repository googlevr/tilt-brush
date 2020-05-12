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

using UnityEngine;
using System;

namespace TiltBrush {

public class QuadStripBrushStretchUV : QuadStripBrush {
  struct UpdateUVRequest {
    public int back, front;

    public bool IsValid { get { return back != -1; } }
    public void Clear() { back = -1; front = -1; }
    public void Set(int back_, int front_) {
      back = back_; front = front_;
    }
  }

  // Store width in Z component of uv0. Currently used only by hypercolor.
  [SerializeField] bool m_StoreWidthInTexcoord0Z;
  private float[] m_QuadLengths;
  // private float m_TotalStrokeLength;
  private UpdateUVRequest m_UpdateUVRequest = new UpdateUVRequest();

  protected override void InitBrush(BrushDescriptor desc, TrTransform localPointerXf) {
    base.InitBrush(desc, localPointerXf);
    m_QuadLengths = new float[m_NumQuads];
    // m_TotalStrokeLength = 0.0f;
    m_UpdateUVRequest.Clear();
  }

  public override GeometryPool.VertexLayout GetVertexLayout(BrushDescriptor desc) {
    return new GeometryPool.VertexLayout {
      bUseColors = true,
      bUseNormals = true,
      bUseTangents = true,
      uv0Size = m_StoreWidthInTexcoord0Z ? 3 : 2,
      uv0Semantic = m_StoreWidthInTexcoord0Z ? GeometryPool.Semantic.XyIsUvZIsDistance : GeometryPool.Semantic.XyIsUv,
      uv1Size = 0
    };
  }

  // This should only be called for the first quad of each solid
  override protected void UpdateUVsForQuad(int iQuadIndex) {
    var rMasterBrush = m_Geometry;
    //compute length of this quad and add it to our total
    float fQuadLength = QuadLength(rMasterBrush.m_Vertices, iQuadIndex);
    // m_TotalStrokeLength -= m_QuadLengths[iQuadIndex];
    m_QuadLengths[iQuadIndex] = fQuadLength;
    // m_TotalStrokeLength += fQuadLength;

    //store our backface length, but don't bother adding it to our total
    if (m_EnableBackfaces) {
      Debug.Assert(iQuadIndex % 2 == 0);
      m_QuadLengths[iQuadIndex + 1] = fQuadLength;
    }
  }

  override protected void UpdateUVsForSegment(int iSegmentBack,
                                              int iSegmentFront, float size) {
    if (m_UpdateUVRequest.IsValid && m_UpdateUVRequest.back != iSegmentBack) {
      FlushUpdateUVRequest();
    }
    if (m_UpdateUVRequest.IsValid) {
      // Take the union of the two requests
      iSegmentFront = Mathf.Max(iSegmentFront, m_UpdateUVRequest.front);
    }
    m_UpdateUVRequest.Set(iSegmentBack, iSegmentFront);
  }

  override public void ApplyChangesToVisuals() {
    UnityEngine.Profiling.Profiler.BeginSample("QuadStripBrushStretchUV.ApplyChangesToVisuals");
    FlushUpdateUVRequest();

    MeshFilter mf = GetComponent<MeshFilter>();
    MasterBrush geometry = m_Geometry;
    mf.mesh.vertices = geometry.m_Vertices;
    mf.mesh.normals  = geometry.m_Normals;
    mf.mesh.colors32 = geometry.m_Colors;
    mf.mesh.tangents = geometry.m_Tangents;
    if (m_StoreWidthInTexcoord0Z) {
      mf.mesh.SetUVs(0, geometry.m_UVWs);
    } else {
      mf.mesh.uv = geometry.m_UVs;
    }
    mf.mesh.RecalculateBounds();
    UnityEngine.Profiling.Profiler.EndSample();
  }

  // iSegmentBack       quad index of segment, trailing edge
  // iSegmentFront      quad index of segment, leading edge
  // "solid" is my term for "the thing comprised of a frontface and backface quad"
  protected void FlushUpdateUVRequest() {
    MasterBrush rMasterBrush = m_Geometry;
    if (! m_UpdateUVRequest.IsValid) {
      return;
    }
    int iSegmentBack = m_UpdateUVRequest.back;
    int iSegmentFront = m_UpdateUVRequest.front;
    m_UpdateUVRequest.Clear();

    int quadsPerSolid = m_EnableBackfaces ? 2 : 1;
    int numSolids = (iSegmentFront - iSegmentBack) / quadsPerSolid;

    float fYStart, fYEnd;
    {
      float random01 = m_rng.In01(iSegmentBack * 6);
      int numV = m_Desc.m_TextureAtlasV;
      int iAtlas = (int)(random01 * numV);
      fYStart = (iAtlas  ) / (float)numV;
      fYEnd   = (iAtlas+1) / (float)numV;
    }

    //get length of current segment
    float fSegmentLength = 0.0f;
    for (int iSolid = 0; iSolid < numSolids; ++iSolid) {
      fSegmentLength += m_QuadLengths[iSegmentBack + (iSolid * quadsPerSolid)];
    }
    // Just enough to get rid of NaNs. If length is 0, doesn't really matter what UVs are
    if (fSegmentLength == 0) { fSegmentLength = 1; }

    //then, run back through the last segment and update our UVs
    float fRunningLength = 0.0f;
    for (int iSolid = 0; iSolid < numSolids; ++iSolid) {
      int iQuadIndex = iSegmentBack + (iSolid * quadsPerSolid);
      int iVertIndex = iQuadIndex * 6;
      float thisSolidLength = m_QuadLengths[iQuadIndex];  // assumes frontface == backface length
      float fXStart = fRunningLength / fSegmentLength;
      float fXEnd = (fRunningLength + thisSolidLength) / fSegmentLength;
      fRunningLength += thisSolidLength;

      rMasterBrush.m_UVs[iVertIndex].Set(fXStart, fYStart);
      rMasterBrush.m_UVs[iVertIndex + 1].Set(fXEnd, fYStart);
      rMasterBrush.m_UVs[iVertIndex + 2].Set(fXStart, fYEnd);
      rMasterBrush.m_UVs[iVertIndex + 3].Set(fXStart, fYEnd);
      rMasterBrush.m_UVs[iVertIndex + 4].Set(fXEnd, fYStart);
      rMasterBrush.m_UVs[iVertIndex + 5].Set(fXEnd, fYEnd);

      if (m_StoreWidthInTexcoord0Z) {
        for (int i = 0; i < 6; i++) {
          rMasterBrush.m_UVWs[iVertIndex + i] = new Vector3(
            rMasterBrush.m_UVs[iVertIndex + i].x,
            rMasterBrush.m_UVs[iVertIndex + i].y
          );
        }
      }
    }

    // Update tangent space
    ComputeTangentSpaceForQuads(
        rMasterBrush.m_Vertices,
        rMasterBrush.m_UVs,
        rMasterBrush.m_Normals,
        rMasterBrush.m_Tangents,
        quadsPerSolid * 6,
        iSegmentBack * 6,
        iSegmentFront * 6);

    if (m_StoreWidthInTexcoord0Z) {
      Vector3 uvw;
      for (int iSolid = 0; iSolid < numSolids; ++iSolid) {
        int iQuadIndex = iSegmentBack + (iSolid * quadsPerSolid);
        int iVertIndex = iQuadIndex * 6;
        float width = (rMasterBrush.m_Vertices[iVertIndex + 0]
                      - rMasterBrush.m_Vertices[iVertIndex + 2]).magnitude;
        for (int i = 0; i < 6; i++) {
          uvw = rMasterBrush.m_UVWs[iVertIndex + i];
          uvw.z = width;
          rMasterBrush.m_UVWs[iVertIndex + i] = uvw;
        }
      }
    }

    if (m_EnableBackfaces) {
      for (int iSolid = 0; iSolid < numSolids; ++iSolid) {
        int iQuadIndex = iSegmentBack + (iSolid * quadsPerSolid);
        int iVertIndex = iQuadIndex * 6;
        if (m_StoreWidthInTexcoord0Z) {
          MirrorQuadFace(rMasterBrush.m_UVWs, iVertIndex);
        } else {
          MirrorQuadFace(rMasterBrush.m_UVs, iVertIndex);
        }
        MirrorTangents(rMasterBrush.m_Tangents, iVertIndex);
      }
    }
  }

  override protected void UpdateUVs(int iQuad0, int iQuad1, float size) {
    // Store the length of the quads we just created. This doesn't update UVs.
    int iNumQuadsPer = m_EnableBackfaces ? 2 : 1;
    for (int i = iQuad0; i < iQuad1; i += iNumQuadsPer) {
      UpdateUVsForQuad(i);
    }
    // This actually modifies the UVs
    UpdateUVsForSegment(m_InitialQuadIndex, iQuad1, size);
  }

  public override BatchSubset FinalizeBatchedBrush() {
    FlushUpdateUVRequest();
    return base.FinalizeBatchedBrush();
  }

  public override void FinalizeSolitaryBrush() {
    FlushUpdateUVRequest();
    base.FinalizeSolitaryBrush();
  }
}
}  // namespace TiltBrush
