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

namespace TiltBrush {

public class QuadStripUnitizedUVBrush : QuadStripBrush {
  override protected void UpdateUVs(int iQuad0, int iQuad1, float size) {
    var rMasterBrush = m_Geometry;
    //recalculate UVs for newly laid or modified quads
    int iNumQuadsCreated = iQuad1 - iQuad0;
    int quadsPerSolid = m_EnableBackfaces ? 2 : 1;
    for (int i = iNumQuadsCreated; i > 0; i -= quadsPerSolid) {
      int iIndex = (iQuad1 - i) * 6;
      rMasterBrush.m_UVs[iIndex].Set(0, 1);
      rMasterBrush.m_UVs[iIndex + 1].Set(1, 1);
      rMasterBrush.m_UVs[iIndex + 2].Set(0, 0);
      rMasterBrush.m_UVs[iIndex + 3].Set(0, 0);
      rMasterBrush.m_UVs[iIndex + 4].Set(1, 1);
      rMasterBrush.m_UVs[iIndex + 5].Set(1, 0);
    }

    ComputeTangentSpaceForQuads(
        rMasterBrush.m_Vertices,
        rMasterBrush.m_UVs,
        rMasterBrush.m_Normals,
        rMasterBrush.m_Tangents,
        quadsPerSolid * 6,
        iQuad0 * 6,
        iQuad1 * 6);

    if (m_EnableBackfaces) {
      for (int i = iNumQuadsCreated; i > 0; i -= quadsPerSolid) {
        int iIndex = (iQuad1 - i) * 6;
        MirrorQuadFace(rMasterBrush.m_UVs, iIndex);
        MirrorTangents(rMasterBrush.m_Tangents, iIndex);
      }
    }
  }

  protected override void UpdateUVsForQuad(int iQuadIndex) { }
  protected override void UpdateUVsForSegment(int iSegmentBack, int iSegmentFront, float size) { }
}
}  // namespace TiltBrush
