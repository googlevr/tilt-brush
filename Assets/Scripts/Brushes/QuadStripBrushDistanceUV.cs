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

namespace TiltBrush {

public class QuadStripBrushDistanceUV : QuadStripBrush {
  const float kOpacityFadeDistanceMeters_PS = 0.025f;

  struct UpdateTangentRequest {
    public int back, front;

    public bool IsValid { get { return back != -1; } }
    public void Clear() { back = -1; front = -1; }
    public void Set(int back_, int front_) {
      back = back_; front = front_;
    }
  }

  private UpdateTangentRequest m_UpdateTangentRequest = new UpdateTangentRequest();

  protected override void InitBrush(BrushDescriptor desc,
      TrTransform localPointerXf) {
    base.InitBrush(desc, localPointerXf);
    m_UpdateTangentRequest.Clear();
  }

  override public void ResetBrushForPreview(TrTransform localPointerXf) {
    base.ResetBrushForPreview(localPointerXf);
    m_UpdateTangentRequest.Clear();
  }

  override protected void UpdateUVsForSegment(int iQuad0,
                                              int iQuad1, float size) {
    var rMasterBrush = m_Geometry;
    float fadeDistance = kOpacityFadeDistanceMeters_PS * App.METERS_TO_UNITS * POINTER_TO_LOCAL;
    int stride = Stride;
    int quadsPerSolid = m_EnableBackfaces ? 2 : 1;
    int iSolid0 = iQuad0 / quadsPerSolid;
    int iSolid1 = iQuad1 / quadsPerSolid;

    // Update UVs for segment. We should do the most recent solid, plus the 2
    // before it, because its length may have gotten tweaked during stitching.
    for (int iSolid = Mathf.Max(iSolid0, iSolid1-3); iSolid < iSolid1; ++iSolid) {
      int iVert = iSolid * Stride;
      float prevU, prevV0, prevV1;
      if (iSolid == iSolid0) {
        float random01 = m_rng.In01(iSolid0 * Stride);
        prevU = random01;
        int numV = m_Desc.m_TextureAtlasV;
        int iAtlas = (int)(random01 * 3331) % numV;
        prevV0 = (iAtlas  ) / (float)numV;
        prevV1 = (iAtlas+1) / (float)numV;
      } else {
        prevU  = rMasterBrush.m_UVs[iVert-stride + 4].x;
        prevV0 = rMasterBrush.m_UVs[iVert-stride + 4].y;
        prevV1 = rMasterBrush.m_UVs[iVert-stride + 5].y;
      }
      float length = SolidLength(rMasterBrush.m_Vertices, iSolid);
      float nextU = prevU + m_Desc.m_TileRate * (length / size);

      rMasterBrush.m_UVs[iVert + 0].Set(prevU, prevV0);
      rMasterBrush.m_UVs[iVert + 2].Set(prevU, prevV1);
      rMasterBrush.m_UVs[iVert + 3].Set(prevU, prevV1);
      rMasterBrush.m_UVs[iVert + 1].Set(nextU, prevV0);
      rMasterBrush.m_UVs[iVert + 4].Set(nextU, prevV0);
      rMasterBrush.m_UVs[iVert + 5].Set(nextU, prevV1);
      if (m_EnableBackfaces) {
        MirrorQuadFace(rMasterBrush.m_UVs, iVert);
      }

      prevU = nextU;
    }

    // Update opacity for segment
    // Opacity is 0 at leading edge and ramps linearly upwards with distance, clamping at 1.
    // Exception: Beginning opacity is also 0.
    // Would look a little nicer if we changed the stroke-start and stroke-end ramps to
    // use the same logic.
    float totalDist = 0;
    for (int iSolid = iSolid1-1; iSolid >= iSolid0; --iSolid) {
      byte leadingA  = (byte)(255 * Mathf.Min(1f, totalDist / fadeDistance));
      totalDist += SolidLength(rMasterBrush.m_Vertices, iSolid);
      byte trailingA = (byte)(255 * Mathf.Min(1f, totalDist / fadeDistance));
      if (iSolid == iSolid0) {
        trailingA = 0;
      }

      int iVert = iSolid * Stride;
      rMasterBrush.m_Colors[iVert + 0].a = trailingA;
      rMasterBrush.m_Colors[iVert + 2].a = trailingA;
      rMasterBrush.m_Colors[iVert + 3].a = trailingA;
      rMasterBrush.m_Colors[iVert + 1].a = leadingA;
      rMasterBrush.m_Colors[iVert + 4].a = leadingA;
      rMasterBrush.m_Colors[iVert + 5].a = leadingA;
      if (m_EnableBackfaces) {
        MirrorQuadFace(rMasterBrush.m_Colors, iVert);
      }

      // Early out if the previous quad looks valid already
      if (iSolid != iSolid0) {
        if (rMasterBrush.m_Colors[iVert - 6+5].a == trailingA) {
          break;
        }
      }
    }

    LazyUpdateTangentsForSegment(iQuad0, iQuad1);
  }

  override public void ApplyChangesToVisuals() {
    FlushUpdateTangentRequest();
    base.ApplyChangesToVisuals();
  }

  void LazyUpdateTangentsForSegment(int iQuad0, int iQuad1) {
    if (m_UpdateTangentRequest.IsValid && m_UpdateTangentRequest.back != iQuad0) {
      FlushUpdateTangentRequest();
    }
    if (m_UpdateTangentRequest.IsValid) {
      // Take the union of the two requests
      iQuad1 = Mathf.Max(iQuad1, m_UpdateTangentRequest.front);
    }
    m_UpdateTangentRequest.Set(iQuad0, iQuad1);
  }

  void FlushUpdateTangentRequest() {
    var rMasterBrush = m_Geometry;
    if (! m_UpdateTangentRequest.IsValid) {
      return;
    }
    int quadsPerSolid = m_EnableBackfaces ? 2 : 1;
    int iSolid0 = m_UpdateTangentRequest.back / quadsPerSolid;
    int iSolid1 = m_UpdateTangentRequest.front / quadsPerSolid;
    m_UpdateTangentRequest.Clear();

    // Update tangent space
    // TODO: We don't need to recompute all the way back to iSolid0,
    // but ComputeTangentSpaceForQuads currently assumes that iVert0
    // is also the segment start.
    ComputeTangentSpaceForQuads(
        rMasterBrush.m_Vertices,
        rMasterBrush.m_UVs,
        rMasterBrush.m_Normals,
        rMasterBrush.m_Tangents,
        Stride,
        iSolid0 * Stride,
        iSolid1 * Stride);
    if (m_EnableBackfaces) {
      for (int iSolid = iSolid0; iSolid < iSolid1; ++iSolid) {
        MirrorTangents(rMasterBrush.m_Tangents, iSolid * Stride);
      }
    }
  }

  override protected void UpdateUVs(int iQuad0, int iQuad1, float size) {
    UpdateUVsForSegment(m_InitialQuadIndex, iQuad1, size);
  }

  protected override void UpdateUVsForQuad(int iQuadIndex) { }
}
}  // namespace TiltBrush
