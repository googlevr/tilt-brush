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
using UnityEngine;

namespace TiltBrush {

// The base class of brushes that draw flat ribbons of connected quads.
//
// In this class:
//   "strip" means the entire set of geometry
//   "segment" is the subset of geometry starting from the most-recent break
//
// The enumeration order for the front side of the vertices is as follows:
//             0--1  4
// trailing    |,' ,'|     leading
//             2  3--5
//      --stroke direction-->
//
// For brushes where a backface needs to be drawn, the quads and their backfaces
// are interleaved. For example, for each quad Q with backface quad P, the ordering
// looks like this:
// Quads:        Q0 P0 Q1 P1 Q2 P2 ... QN PN
// Vertex Index:  0  6 12 18 24 30     6N 6(N+1)
//
// The backface vertex indices also increase going clockwise around their face normal
// vector (as shown above for the front faces). However, since their normal vector is
// the negative of that of the front face, vertex 1 on the front face of any quad is
// not at the same location as vertex 1 on the back face. The mapping from front quad
// vertices to back quad vertices at the same location is as follows:
// Front: 0 1 2 3 4 5
// Back:  0 2 1 3 5 4
public abstract class QuadStripBrush : BaseBrushScript {
  // Former inspector data; move to brush.xml if you want these tunable
  // Distance for pressure to lerp to ~90% of its instantaneous value. Value in meters.
  static float kPressureSmoothWindowMeters_PS = .20f;
  const float kSolidMinLengthMeters_PS = 0.0015f;
  const float kMinimumMoveLengthMeters_PS = 5e-4f;
  const float kSolidAspectRatio = 0.2f;

  protected MasterBrush m_Geometry;

  // "Last" in the sense of "previous" as opposed to "ultimate"
  protected Vector3 m_LastFacing;
  protected Vector3 m_LastQuadCenter;
  protected Vector3 m_LastQuadForward;
  protected Vector3 m_LastQuadRight;
  protected Vector3 m_LastQuadNormal;
  protected int m_LastSegmentLengthSolids;
  protected float m_LastSpawnPressure;
  protected float m_LastSizeShrink;

  protected int m_NumQuads;

  // Initial is either quad 0, or immediately follows a break.
  //
  // [Initial, Leading) is a half-open range of quads (not solids!)
  // pointing at the last break-free portion of the stroke. This range is
  // guaranteed to consist of properly-sized quads.
  //
  // There is also zero or one solid at Leading. This solid (if it exists)
  // will be shorter than allowable. This is called the "leading edge".
  // If m_LeadingSegmentInitialQuadIndex != null, there is a solid there.
  protected int m_LeadingQuadIndex;
  protected int m_InitialQuadIndex;
  protected bool m_AllowStripBreak = true;
  protected int? m_LeadingSegmentInitialQuadIndex;

  protected int Stride {
    get { return 6 * (m_EnableBackfaces ? 2 : 1); }
  }

  public QuadStripBrush()
    : base(bCanBatch: true) {
  }

  protected void OnDestroy() {
    if (m_Geometry != null) {
      MasterBrush.Pool.PutAndClear(ref m_Geometry);
    }
  }

  //
  // BaseBrushScript override API
  //

  override public float GetSpawnInterval(float pressure01) {
    return kSolidMinLengthMeters_PS * App.METERS_TO_UNITS * POINTER_TO_LOCAL +
      (PressuredSize(pressure01) * kSolidAspectRatio);
  }

  override public int GetNumUsedVerts() {
    int nQuadsPerSolid = m_EnableBackfaces ? 2 : 1;

    // There might be no leading segment if we just created a quad.
    // In that case, pretend that the length is 0, causing us to drop it.
    int iLeadingSegmentLength = (m_LeadingSegmentInitialQuadIndex == null)
        ? 0 : m_LeadingQuadIndex - m_LeadingSegmentInitialQuadIndex.Value;

    // Default that our leading edge is attached to a segment of reasonable length, and
    // we'd like to include it in our mesh
    int iSolidAdjustmentAmount = 1;

    // Check for edge cases to clean up lone segments
    if (iLeadingSegmentLength == 0) {
      // The leading edge is not attached to a segment, so don't include it.
      iSolidAdjustmentAmount = 0;
      // However, we need to check if our previous segment was only one solid, and if so,
      // discard that as well
      if (m_LastSegmentLengthSolids == 1) {
        iSolidAdjustmentAmount = -1;
      }
    } else if (iLeadingSegmentLength == nQuadsPerSolid) {
      // The leading edge is attached to a single solid, so discard both of them
      iSolidAdjustmentAmount = -1;
    }

    int iQuadAdjustmentAmount = iSolidAdjustmentAmount * nQuadsPerSolid;
    return (m_LeadingQuadIndex + iQuadAdjustmentAmount) * 6;
  }

  override public bool AlwaysRebuildPreviewBrush() {
    return true;
  }

  override public void ResetBrushForPreview(TrTransform localPointerXf) {
    base.ResetBrushForPreview(localPointerXf);
    int nQuadsPerSolid = m_EnableBackfaces ? 2 : 1;
    m_Geometry.Reset((m_LeadingQuadIndex + nQuadsPerSolid) * 6);
    // Kind of irritating that MasterBrush reset also resets the layout, but whatever.
    m_Geometry.VertexLayout = GetVertexLayout(m_Desc);
    m_LeadingQuadIndex = 0;
    m_InitialQuadIndex = 0;
    m_LastQuadRight = Vector3.zero;
  }

  protected override void InitBrush(BrushDescriptor desc, TrTransform localPointerXf) {
    base.InitBrush(desc, localPointerXf);
    m_Geometry = MasterBrush.Pool.Get();
    Debug.Assert(m_Geometry.VertexLayout == null, "m_Geometry is not fully reset");
    m_Geometry.VertexLayout = GetVertexLayout(desc);
    m_LastQuadRight = Vector3.zero;

    m_NumQuads = m_Geometry.NumVerts / 6;

    MeshFilter mf = GetComponent<MeshFilter>();
    mf.mesh = null;  // Force a new, empty, mf-owned mesh to be generated
    mf.mesh.MarkDynamic();

    // We only need to set verts and tris here because the mesh is zeroed out, effectively hidden
    mf.mesh.vertices = m_Geometry.m_Vertices;
    mf.mesh.triangles = m_Geometry.m_Tris;
  }

  override public void DebugGetGeometry(
      out Vector3[] verts, out int nVerts,
      out Vector2[] uv0s,
      out int[] tris, out int nTris) {
    verts = m_Geometry.m_Vertices;
    nVerts = GetNumUsedVerts();
    uv0s = m_Geometry.m_UVs;
    tris = m_Geometry.m_Tris;
    // Yup, one index per vert
    nTris = GetNumUsedVerts();
  }

  override public GeometryPool.VertexLayout GetVertexLayout(BrushDescriptor desc) {
    return new GeometryPool.VertexLayout {
      uv0Size = 2,
      uv0Semantic = GeometryPool.Semantic.XyIsUv,
      uv1Size = 0,
      bUseNormals = true,
      bUseColors = true,
      bUseTangents = true,
    };
  }

  // Returns a new array consisting of the first 'num' values of the input array.
  // It is an error if num > array.Length.
  private static T[] SubsetOf<T>(T[] array, int num) {
    // With a little underhanded ListExtensions work, we could probably write a
    // relatively garbage-free O(1) version that returns List<T>; but since this
    // is currently only used for solitary brushes it's not worth the hackery.
    if (num > array.Length) { throw new ArgumentException("num"); }
    T[] ret = new T[num];
    Array.Copy(array, ret, num);
    return ret;
  }

  override public void FinalizeSolitaryBrush() {
    MasterBrush geom = m_Geometry;
    int iNumVerts = GetNumUsedVerts();
    int iNumTris = iNumVerts;  // Happens to be true for QuadStripBrush and descendants

    MeshFilter mf = GetComponent<MeshFilter>();
    mf.mesh.Clear(false);

    mf.mesh.vertices  = SubsetOf(geom.m_Vertices, iNumVerts);
    mf.mesh.triangles = SubsetOf(geom.m_Tris    , iNumTris);
    mf.mesh.normals   = SubsetOf(geom.m_Normals , iNumVerts);
    if (geom.VertexLayout.Value.texcoord0.size == 3) {
      mf.mesh.SetUVs(0, geom.m_UVWs.GetRange(0, iNumVerts));
    } else {
      mf.mesh.uv      = SubsetOf(geom.m_UVs     , iNumVerts);
    }
    mf.mesh.colors32  = SubsetOf(geom.m_Colors  , iNumVerts);
    mf.mesh.tangents  = SubsetOf(geom.m_Tangents, iNumVerts);

    mf.mesh.RecalculateBounds();
    MasterBrush.Pool.PutAndClear(ref m_Geometry);
  }

  override public BatchSubset FinalizeBatchedBrush() {
    int numVerts = GetNumUsedVerts();
    int numTris = numVerts;
    var geometry = m_Geometry;
    m_Geometry = null;

    // The Weld function only supports single-sided geometry; but this is fine since
    // there should be no double-sided QuadStrip brushes left.
    if (!m_EnableBackfaces) {
      int newNumVerts, newNumTris;
      WeldSingleSidedQuadStrip(
          GetVertexLayout(m_Desc), geometry, numVerts,
          out newNumVerts, out newNumTris);
      var ret = Canvas.BatchManager.CreateSubset(m_Desc, newNumVerts, newNumTris, geometry);

      // Put the triangles back to how they were because MasterBrush doesn't expect anyone
      // to modify m_Tris. We should really get rid of MasterBrush and use GeometryPool
      // everywhere. Alternatively, if every QuadStripBrush goes through the vertex-reduction
      // pathway then we can leave m_Tris trashed, since the vertex-reduction step always
      // writes new indices!

      // For MasterBrush, # verts === # indices, since it assumes each tri uses 3 unique verts.
      for (int i = 0; i < numVerts; ++i) {
        geometry.m_Tris[i] = i;
      }
      MasterBrush.Pool.PutAndClear(ref geometry);
      return ret;
    }

    return Canvas.BatchManager.CreateSubset(
        m_Desc, numVerts, numTris, geometry);
  }

  /// Rewrite vertices and indices, welding together identical verts.
  /// Input topology must be as documented at the top of QuadStripBrush.cs
  /// Output topology is the same as FlatGeometryBrush
  private static void WeldSingleSidedQuadStrip(
      GeometryPool.VertexLayout layout,
      MasterBrush geometry,
      int numVerts,
      out int newNumVerts,
      out int newNumTris) {
    // Offsets to Front/Back Left/Right verts, QuadStrip-style (see ascii art at top of file)
    // Because of duplication, there are sometimes multiple offsets.
    // "S" is the stride, either 6 or 12 depending on usesDoubleSidedGeometry.
    // In cases where there are multiple offsets to choose from, we choose the offset
    // which makes it safe to reorder verts in-place. See the comment below re: overlaps
    const int kBrOld = 2;  // also -S+5, 3
    const int kBlOld = 0;  // also -S+1, -S+4
    const int kFrOld = 5;  // also S+2, S+3
    const int kFlOld = 1;  // also 4, S+0

    // Offsets to Front/Back Left/Right verts, FlatGeometry-style
    // See FlatGeometryBrush.cs:15
    const int kBrNew = 0;
    const int kBlNew = 1;
    const int kFrNew = 2;
    const int kFlNew = 3;

    // End result:
    //     0--1  4      keep 2, 0, 5, 1      0--1
    //     |,' ,'|  ->  ignore 3, 4      ->  |,'|
    //     2  3--5                           2--5

    int vertRead = 0;  // vertex read index
    int vertWrite = 0;  // vertex write index
    int triWrite = 0;  // triangle write index

    var vs = geometry.m_Vertices;
    var ns = geometry.m_Normals;
    var cs = geometry.m_Colors;
    var ts = geometry.m_Tangents;

    var tris = geometry.m_Tris;

    var uv2s = geometry.m_UVs;
    var uv3s = geometry.m_UVWs.GetBackingArray();

    while (vertRead < numVerts) {
      // Compress a single connected strip.
      // The first quad is treated differently from subsequent quads,
      // because it doesn't have a previous quad to share its first two verts with.

      // First quad

      // We write to the same buffer being read from, which is a bit dangerous.
      // Correctness requires we not copy from a location that's been written to.
      // The potential problem cases where the read area overlaps the write area are:
      //   Quad #0:   write [0, 4)   read [0, 6)
      //   Quad #1:   write [4, 8)   read [6, 12)
      // For subsequent quads the read area is ahead of, and does not overlap, the write area.
      vs[vertWrite + kFlNew] = vs[vertRead + kFlOld];  // 3 <- 1   7 <- 7
      ns[vertWrite + kFlNew] = ns[vertRead + kFlOld];
      cs[vertWrite + kFlNew] = cs[vertRead + kFlOld];
      ts[vertWrite + kFlNew] = ts[vertRead + kFlOld];

      vs[vertWrite + kBlNew] = vs[vertRead + kBlOld];  // 1 <- 0   5 <- 6
      ns[vertWrite + kBlNew] = ns[vertRead + kBlOld];
      cs[vertWrite + kBlNew] = cs[vertRead + kBlOld];
      ts[vertWrite + kBlNew] = ts[vertRead + kBlOld];

      vs[vertWrite + kBrNew] = vs[vertRead + kBrOld];  // 0 <- 2   4 <- 8
      ns[vertWrite + kBrNew] = ns[vertRead + kBrOld];
      cs[vertWrite + kBrNew] = cs[vertRead + kBrOld];
      ts[vertWrite + kBrNew] = ts[vertRead + kBrOld];

      vs[vertWrite + kFrNew] = vs[vertRead + kFrOld];  // 2 <- 5   6 <- 11
      ns[vertWrite + kFrNew] = ns[vertRead + kFrOld];
      cs[vertWrite + kFrNew] = cs[vertRead + kFrOld];
      ts[vertWrite + kFrNew] = ts[vertRead + kFrOld];

      if (layout.texcoord0.size == 2) {
        uv2s[vertWrite + kFlNew] = uv2s[vertRead + kFlOld];
        uv2s[vertWrite + kBlNew] = uv2s[vertRead + kBlOld];
        uv2s[vertWrite + kBrNew] = uv2s[vertRead + kBrOld];
        uv2s[vertWrite + kFrNew] = uv2s[vertRead + kFrOld];
      } else {
        uv3s[vertWrite + kFlNew] = uv3s[vertRead + kFlOld];
        uv3s[vertWrite + kBlNew] = uv3s[vertRead + kBlOld];
        uv3s[vertWrite + kBrNew] = uv3s[vertRead + kBrOld];
        uv3s[vertWrite + kFrNew] = uv3s[vertRead + kFrOld];
      }

      // See FlatGeometryBrush.cs:240
      // SetTri(cur.iTri, cur.iVert, 0, BR, BL, FL);
      // SetTri(cur.iTri, cur.iVert, 1, BR, FL, FR);
      tris[triWrite + 0] = vertWrite + kBrNew;
      tris[triWrite + 1] = vertWrite + kBlNew;
      tris[triWrite + 2] = vertWrite + kFlNew;
      tris[triWrite + 3] = vertWrite + kBrNew;
      tris[triWrite + 4] = vertWrite + kFlNew;
      tris[triWrite + 5] = vertWrite + kFrNew;

      vertWrite += 4;  // we wrote to a range of 4 verts
      vertRead += 6;  // we read from a range of 6 verts
      triWrite += 6;  // we wrote 6 indices

      // Remaining quads.

      // Detect strip continuation by checking a single vertex position.
      // To be fully correct, we should check both the left and right verts, and all the
      // attributes. However, as of M16 this simpler version suffices for all shipping
      // QuadStripBrushes.
      while (vertRead < numVerts && vs[vertRead + kBrOld] == vs[vertWrite - 4 + kFrNew]) {
        vertWrite -= 2;  // Share 2 verts with the previous quad

        // The read range will provably never overlap with the write range. Note that
        // this cannot be quad 0 since there exists a previous quad.
        // Therefore, the closest the ranges get is:
        //   Quad #1:   write [2, 6)   read [6, 12)
        vs[vertWrite + kFlNew] = vs[vertRead + kFlOld];
        ns[vertWrite + kFlNew] = ns[vertRead + kFlOld];
        cs[vertWrite + kFlNew] = cs[vertRead + kFlOld];
        ts[vertWrite + kFlNew] = ts[vertRead + kFlOld];

        vs[vertWrite + kFrNew] = vs[vertRead + kFrOld];
        ns[vertWrite + kFrNew] = ns[vertRead + kFrOld];
        cs[vertWrite + kFrNew] = cs[vertRead + kFrOld];
        ts[vertWrite + kFrNew] = ts[vertRead + kFrOld];

        if (layout.texcoord0.size == 2) {
          uv2s[vertWrite + kFlNew] = uv2s[vertRead + kFlOld];
          uv2s[vertWrite + kFrNew] = uv2s[vertRead + kFrOld];
        } else {
          uv3s[vertWrite + kFlNew] = uv3s[vertRead + kFlOld];
          uv3s[vertWrite + kFrNew] = uv3s[vertRead + kFrOld];
        }

        tris[triWrite + 0] = vertWrite + kBrNew;
        tris[triWrite + 1] = vertWrite + kBlNew;
        tris[triWrite + 2] = vertWrite + kFlNew;
        tris[triWrite + 3] = vertWrite + kBrNew;
        tris[triWrite + 4] = vertWrite + kFlNew;
        tris[triWrite + 5] = vertWrite + kFrNew;

        vertWrite += 4;
        vertRead += 6;
        triWrite += 6;
      }
    }

    newNumVerts = vertWrite;
    newNumTris = triWrite;
  }

  override protected void InitUndoClone(GameObject clone) {
    var undo = clone.AddComponent<UndoMeshAnimScript>();
    undo.Init();
  }

  override public bool ShouldDiscard() {
    return GetNumUsedVerts() <= 0;
  }

  /// Unconditionally increments m_LeadingQuadIndex by 1 or 2
  private void AppendLeadingQuad(
      bool bGenerateNew, float opacity01,
      Vector3 vCenter, Vector3 vForward, Vector3 vNormal,
      Vector3 vRight, MasterBrush rMasterBrush,
      out int earliestChangedQuad) {
    // Get the current stroke from the MasterBrush so that quad positions and
    // orientations can be calculated.
    Vector3[] aVerts = rMasterBrush.m_Vertices;
    Vector3[] aNorms = rMasterBrush.m_Normals;
    Color32[] aColors = rMasterBrush.m_Colors;

    int stride = Stride;
    // Lay leading quad
    int iVertIndex = m_LeadingQuadIndex * 6;
    PositionQuad(aVerts, iVertIndex, vCenter, vForward, vRight);
    for (int i = 0; i < 6; ++i) {
      aNorms[iVertIndex + i] = vNormal;
    }

    earliestChangedQuad = m_LeadingQuadIndex;

    Color32 cColor = m_Color;
    cColor.a = (byte)(opacity01 * 255.0f);
    Color32 cLastColor = (iVertIndex - stride >= 0) ? aColors[iVertIndex - stride + 4] : cColor;

    aColors[iVertIndex    ] = cLastColor;
    aColors[iVertIndex + 1] = cColor;
    aColors[iVertIndex + 2] = cLastColor;
    aColors[iVertIndex + 3] = cLastColor;
    aColors[iVertIndex + 4] = cColor;
    aColors[iVertIndex + 5] = cColor;

    ++m_LeadingQuadIndex;

    // Create duplicates if we have backfaces enabled.
    if (m_EnableBackfaces) {
      int iCurrVertIndex = m_LeadingQuadIndex * 6;
      CreateDuplicateQuad(aVerts, aNorms, m_LeadingQuadIndex, vNormal);

      Color32 backColor, lastBackColor;
      if (m_Desc.m_BackfaceHueShift == 0) {
        backColor = cColor;
        lastBackColor = cLastColor;
      } else {
        HSLColor hsl = (HSLColor)(Color)m_Color;
        hsl.HueDegrees += m_Desc.m_BackfaceHueShift;
        backColor = (Color32)(Color)hsl;
        lastBackColor = (iCurrVertIndex - stride >= 0)
          ? aColors[iCurrVertIndex - stride + 4]
          : backColor;
      }

      aColors[iCurrVertIndex    ] = lastBackColor;
      aColors[iCurrVertIndex + 1] = lastBackColor;
      aColors[iCurrVertIndex + 2] = backColor;
      aColors[iCurrVertIndex + 3] = lastBackColor;
      aColors[iCurrVertIndex + 4] = backColor;
      aColors[iCurrVertIndex + 5] = backColor;

      ++m_LeadingQuadIndex;
    }

    // Walk backward and smooth out previous quads.
    int iStripLength = m_LeadingQuadIndex;      // In solids
    int iSegmentLength = m_LeadingQuadIndex - m_InitialQuadIndex;  // In solids
    if (m_EnableBackfaces) {
      iStripLength /= 2;
      iSegmentLength /= 2;
    }

    // We don't need to smooth anything if our strip is only 1 quad.
    if (iStripLength > 1) {
      // Indexes for later use
      int iIndexingOffset = m_EnableBackfaces ? 2 : 1;

      int iBackQuadIndex = m_LeadingQuadIndex - (3 * iIndexingOffset);
      int iBackQuadVert = iBackQuadIndex * 6;

      int iMidQuadIndex = m_LeadingQuadIndex - (2 * iIndexingOffset);
      int iMidQuadVert = iMidQuadIndex * 6;

      int iFrontQuadIndex = m_LeadingQuadIndex - iIndexingOffset;
      int iFrontQuadVert = iFrontQuadIndex * 6;

      if (iSegmentLength == 1) {
        // If we've got a long strip, but this segment is only 1 quad, touch up the previous quad.
        PositionQuad(aVerts, iMidQuadVert, m_LastQuadCenter, m_LastQuadForward, m_LastQuadRight);
        earliestChangedQuad = Mathf.Min(earliestChangedQuad, iMidQuadIndex);

        // Fuse back to mid if it exists and if they used to be fused.
        if (iStripLength > 2 && m_LastSegmentLengthSolids > 1) {
          FuseQuads(aVerts, aNorms, iBackQuadVert, iMidQuadVert, bGenerateNew);
          if (m_EnableBackfaces) {
            MakeConsistentBacksideQuad(aVerts, aNorms, iBackQuadVert);
          }
        } else if (bGenerateNew && m_LastSegmentLengthSolids == 1) {
          // If we've got a strip longer than one quad, and this segment is only one quad, it
          // means this is the start of a new segment. If we're beginning a new segment and
          // our previous segment is only one quad, squash that quad to clean up artifacts.
          PositionQuad(aVerts, iMidQuadVert, m_LastQuadCenter, Vector3.zero, Vector3.zero);
        }
        if (m_EnableBackfaces) {
          MakeConsistentBacksideQuad(aVerts, aNorms, iMidQuadVert);
        }

      } else if (iSegmentLength == 2) {
        // If we've got a long strip, but this segment is only 2 quads, just fuse.
        FuseQuads(aVerts, aNorms, iMidQuadVert, iFrontQuadVert, bGenerateNew);

        if (m_EnableBackfaces) {
          MakeConsistentBacksideQuad(aVerts, aNorms, iMidQuadVert);
          MakeConsistentBacksideQuad(aVerts, aNorms, iFrontQuadVert);
        }

      } else {
        // Set mid quad to the midpoint of back and front quads.
        for (int i = 0; i < 6; ++i) {
          aVerts[iMidQuadVert + i] = (aVerts[iBackQuadVert + i] + aVerts[iFrontQuadVert + i]) * 0.5f;
        }
        // Patch up the holes by connecting the leading edge of the back quad to the trailing
        // edge of the mid, and do the same from mid to front.
        FuseQuads(aVerts, aNorms, iBackQuadVert, iMidQuadVert, bGenerateNew);
        FuseQuads(aVerts, aNorms, iMidQuadVert, iFrontQuadVert, bGenerateNew);

        if (m_EnableBackfaces) {
          MakeConsistentBacksideQuad(aVerts, aNorms, iBackQuadVert);
          MakeConsistentBacksideQuad(aVerts, aNorms, iMidQuadVert);
          MakeConsistentBacksideQuad(aVerts, aNorms, iFrontQuadVert);
        }

        // Make sure the UVs are proper
        UpdateUVsForQuad(iMidQuadIndex);
      }
    }
  }

  // Adds a quad symmetric around point vCenter that is fully defined by specifying directions vForward
  // and vRight, the two vectors that point from the center to two perpendicular edges of the quad.
  void PositionQuad(Vector3[] aVerts, int iVertIndex, Vector3 vCenter, Vector3 vForward, Vector3 vRight) {
    aVerts[iVertIndex] = vCenter - vForward - vRight;
    aVerts[iVertIndex + 1] = vCenter + vForward - vRight;
    aVerts[iVertIndex + 2] = vCenter - vForward + vRight;
    aVerts[iVertIndex + 3] = vCenter - vForward + vRight;
    aVerts[iVertIndex + 4] = vCenter + vForward - vRight;
    aVerts[iVertIndex + 5] = vCenter + vForward + vRight;
  }

  // Use the coordinates of the front side to generate a quad with the same vertices as the front face,
  // but with a face normal in the opposite direction.
  void MakeConsistentBacksideQuad(Vector3[] aVerts, Vector3[] aNorms,
                                  int iFrontsideVertIndex) {
    // The backside quad comes immediately after the frontside quad by convention.
    int iBacksideVertIndex = iFrontsideVertIndex + 6;

    // The triangles on the backside are enumerated in the opposite direction. Points 0, 1, and 2
    // on the front side become 0, 2, and 1 on the back.
    aVerts[iBacksideVertIndex] = aVerts[iFrontsideVertIndex];
    aVerts[iBacksideVertIndex + 1] = aVerts[iFrontsideVertIndex + 2];
    aVerts[iBacksideVertIndex + 2] = aVerts[iFrontsideVertIndex + 1];
    aVerts[iBacksideVertIndex + 3] = aVerts[iFrontsideVertIndex + 3];
    aVerts[iBacksideVertIndex + 4] = aVerts[iFrontsideVertIndex + 5];
    aVerts[iBacksideVertIndex + 5] = aVerts[iFrontsideVertIndex + 4];
    // Set the normals on the backside to point in the opposite direction of those that of the front side.
    aNorms[iBacksideVertIndex] = -aNorms[iFrontsideVertIndex];
    aNorms[iBacksideVertIndex + 1] = -aNorms[iFrontsideVertIndex + 2];
    aNorms[iBacksideVertIndex + 2] = -aNorms[iFrontsideVertIndex + 1];
    aNorms[iBacksideVertIndex + 3] = -aNorms[iFrontsideVertIndex + 3];
    aNorms[iBacksideVertIndex + 4] = -aNorms[iFrontsideVertIndex + 5];
    aNorms[iBacksideVertIndex + 5] = -aNorms[iFrontsideVertIndex + 4];
  }

  // Connects two adjacent quads without a gap. If alterBackQuad is true, the back of the leading
  // quad and the front of the trailing quad are averaged together, otherwise, the back of the leading
  // quad is set equal to the front of the trailing quad. The normal vectors on the vertices of the
  // shared edge between two adjacent quads are averaged also.
  void FuseQuads(Vector3[] aVerts, Vector3[] aNorms, int iBackQuadVertIndex, int iFrontQuadVertIndex,
                 bool alterBackQuad) {
    Vector3 vTopPos =
      alterBackQuad
      ? (aVerts[iBackQuadVertIndex + 1] + aVerts[iFrontQuadVertIndex]) * 0.5f
      : aVerts[iBackQuadVertIndex + 1];
    Vector3 vBottomPos =
      alterBackQuad
      ? (aVerts[iBackQuadVertIndex + 5] + aVerts[iFrontQuadVertIndex + 2]) * 0.5f
      : aVerts[iBackQuadVertIndex + 5];

    aVerts[iBackQuadVertIndex + 1] = vTopPos;
    aVerts[iBackQuadVertIndex + 4] = vTopPos;
    aVerts[iBackQuadVertIndex + 5] = vBottomPos;
    aVerts[iFrontQuadVertIndex] = vTopPos;
    aVerts[iFrontQuadVertIndex + 2] = vBottomPos;
    aVerts[iFrontQuadVertIndex + 3] = vBottomPos;
    // Update the normals.
    // Note that we're using Vector3.Slerp to provide an arbitrary, non-zero vector in the rare
    // case that aNorms[iBackQuadVertIndex + 1] and aNorms[iFrontQuadVertIndex] would average
    // out to zero.  This, in practice, only happened for the preview brush on a cube stencil.
    Vector3 vNormalAvg = alterBackQuad
      ? Vector3.Slerp(aNorms[iBackQuadVertIndex + 1], aNorms[iFrontQuadVertIndex], 0.5f).normalized
      : aNorms[iBackQuadVertIndex + 1];

    aNorms[iBackQuadVertIndex + 1] = vNormalAvg;
    aNorms[iBackQuadVertIndex + 4] = vNormalAvg;
    aNorms[iBackQuadVertIndex + 5] = vNormalAvg;

    aNorms[iFrontQuadVertIndex] = vNormalAvg;
    aNorms[iFrontQuadVertIndex + 2] = vNormalAvg;
    aNorms[iFrontQuadVertIndex + 3] = vNormalAvg;
  }

  // Samples the instantaneous pressure and returns a smoothed value.
  // Requires that m_LastSpawnPressure be updated in tandem with m_LastSpawnPos.
  float GetSmoothedPressure(float pressure01, Vector3 pos) {
    if (m_PreviewMode) { return pressure01; }
    // Initial condition
    if (m_LeadingQuadIndex == 0) {
      return pressure01;
    }

    float distanceM = Vector3.Distance(m_LastSpawnPos, pos) * App.UNITS_TO_METERS;
    float windowM = kPressureSmoothWindowMeters_PS * POINTER_TO_LOCAL;
    float k = Mathf.Pow(.1f, distanceM / windowM);
    return k * m_LastSpawnPressure + (1-k) * pressure01;
  }

  override public void ApplyChangesToVisuals() {
    MeshFilter mf = GetComponent<MeshFilter>();
    MasterBrush geometry = m_Geometry;
    mf.mesh.vertices = geometry.m_Vertices;
    mf.mesh.normals  = geometry.m_Normals;
    mf.mesh.colors32 = geometry.m_Colors;
    mf.mesh.uv       = geometry.m_UVs;
    mf.mesh.tangents = geometry.m_Tangents;
    mf.mesh.RecalculateBounds();
  }

  override protected bool UpdatePositionImpl(
      Vector3 vPos, Quaternion ori, float fPressure) {
    UnityEngine.Profiling.Profiler.BeginSample("QuadStripBrush.UpdatePositionImpl");
    var rMasterBrush = m_Geometry;

    // This method MUST NOT modify any significant state if a quad is not generated,
    // Otherwise we are in danger of having bugs where playback does not match the
    // Initial drawing.

    float fSmoothedPressure = GetSmoothedPressure(fPressure, vPos);
    float fSpawnInterval = GetSpawnInterval(fSmoothedPressure);

    Vector3 vFacing = vPos - m_LastSpawnPos;
    float moveLength = vFacing.magnitude;
    if (moveLength < kMinimumMoveLengthMeters_PS * App.METERS_TO_UNITS * POINTER_TO_LOCAL) {
      UnityEngine.Profiling.Profiler.EndSample();
      return false;
    }
    vFacing /= moveLength;

    bool bGenerateNewQuad = moveLength >= fSpawnInterval;
    int iNumQuadsPer = m_EnableBackfaces ? 2 : 1;
    // Compute surface basis vectors.
    Vector3 vRight, vSurfaceNormal;
    {
      // If single-sided, always point the frontside towards the brush. Causes twisting.
      Vector3 vPreferredRight = m_Desc.m_BackIsInvisible
        ? Vector3.Cross(ori * Vector3.forward, vFacing)
        : m_LastQuadRight.normalized;
      ComputeSurfaceFrameNew(vPreferredRight, vFacing, ori,
                             out vRight, out vSurfaceNormal);
    }

    if (!bGenerateNewQuad) {
      // If we're not making a new quad, smooth our facing vector.
      float fRatio = moveLength / fSpawnInterval;
      vFacing = Vector3.Slerp(m_LastFacing, vFacing, fRatio);
    }

    float pressuredSize = PressuredSize(fSmoothedPressure) - m_LastSizeShrink;

    Vector3 vQuadCenter = ((vPos + m_LastSpawnPos) * 0.5f);
    Vector3 vQuadForward = vFacing * moveLength * 0.5f;
    Vector3 vQuadRight = vRight * pressuredSize * 0.5f;
    int iPrevInitialIndex = m_InitialQuadIndex;
    float sizeShrink = m_LastSizeShrink;

    // Check to see if we have a sharp bend.
    bool bIsBreak = false;
    if (DevOptions.I.AllowStripBreak && !m_PreviewMode) {
      int segmentLength = m_LeadingQuadIndex - m_InitialQuadIndex;
      if (segmentLength >= iNumQuadsPer) {
        float dotRight = Vector3.Dot(m_LastQuadForward, vQuadCenter + vQuadRight - m_LastQuadCenter);
        float dotLeft = Vector3.Dot(m_LastQuadForward, vQuadCenter - vQuadRight - m_LastQuadCenter);
        if (Vector3.Dot(m_LastQuadForward, vQuadCenter - m_LastSpawnPos) <= 0) {
          bIsBreak = true;
          // Create a break.
          UpdateUVsForSegment(m_InitialQuadIndex, m_LeadingQuadIndex,
                              pressuredSize);
          m_InitialQuadIndex = m_LeadingQuadIndex;
        } else if ((dotLeft < 0 && dotRight > 0) || (dotLeft > 0 && dotRight < 0)) {
          // Shrink the brush so that it doesn't self intersect.
          Vector3 vEndPointLeft = m_LastQuadCenter - m_LastQuadRight;
          Vector3 vEndPointRight = m_LastQuadCenter + m_LastQuadRight;
          if (dotLeft < 0) {
            // Turning towards left side.
            moveLength = (vQuadCenter + vQuadRight - vEndPointRight).magnitude;
            vQuadRight = vQuadCenter - vEndPointLeft;
          } else {
            // Turning towards right side.
            moveLength = (vQuadCenter - vQuadRight - vEndPointLeft).magnitude;
            vQuadRight = vEndPointRight - vQuadCenter;
          }
          float newPressuredSize = 2.0f * vQuadRight.magnitude;
          sizeShrink = m_LastSizeShrink + (pressuredSize - newPressuredSize);
          pressuredSize = newPressuredSize;
          Vector3 vPreferredForward = vQuadCenter - m_LastSpawnPos;
          vQuadForward = Vector3.Cross(vQuadRight, Vector3.Cross(vPreferredForward, vQuadRight));
          vQuadForward = vQuadForward.normalized * (vQuadCenter - m_LastQuadCenter).magnitude * 0.5f;
        } else if (bGenerateNewQuad) {
          sizeShrink = m_LastSizeShrink - Mathf.Min(m_LastSizeShrink, 1f * moveLength);
        }
      }
    }

    // Backup our current index and lay down our leading quad.
    int iPrevLeadingIndex = m_LeadingQuadIndex;
    int earliestQuad;
    AppendLeadingQuad(bGenerateNewQuad, PressuredOpacity(fSmoothedPressure),
                        vQuadCenter, vQuadForward, vSurfaceNormal, vQuadRight,
                        rMasterBrush, out earliestQuad);
    Debug.Assert(m_LeadingQuadIndex == iPrevLeadingIndex + iNumQuadsPer);

    UpdateUVs(Mathf.Min(iPrevInitialIndex, earliestQuad), m_LeadingQuadIndex, pressuredSize);

    if (bGenerateNewQuad) {
      m_LastFacing = vFacing;
      m_LastQuadCenter = vQuadCenter;
      m_LastQuadForward = vQuadForward;
      m_LastQuadRight = vQuadRight;
      m_LastQuadNormal = vSurfaceNormal;
      m_LastSegmentLengthSolids = (m_LeadingQuadIndex - m_InitialQuadIndex) / iNumQuadsPer;
      m_LastSpawnPressure = fSmoothedPressure;
      m_LastSizeShrink = sizeShrink;
      // There is no leading segment
      m_LeadingSegmentInitialQuadIndex = null;
    } else {
      // If we didn't lay a new quad, reset our UV and quad indexes for next frame.
      m_LeadingSegmentInitialQuadIndex = m_InitialQuadIndex;
      m_InitialQuadIndex = iPrevInitialIndex;
      m_LeadingQuadIndex = iPrevLeadingIndex;
      // m_LeadingSegmentInitialQuadIndex is now m_LeadingQuadIndex or m_InitialQuadIndex,
      // depending on if the quad just touched is a break.
      Debug.Assert(m_LeadingSegmentInitialQuadIndex ==
          (bIsBreak ? m_LeadingQuadIndex : m_InitialQuadIndex));
    }

    UnityEngine.Profiling.Profiler.EndSample();
    return bGenerateNewQuad;
  }

  override public bool IsOutOfVerts() {
    int iIndexingOffset = m_EnableBackfaces ? 2 : 1;
    return m_LeadingQuadIndex >= m_NumQuads - iIndexingOffset;
  }

  //
  // QuadStripBrush API for subclasses
  //

  // Pass a quad index (not a solid index)
  // This should only be called for the first quad of each solid!
  abstract protected void UpdateUVsForQuad(int iQuadIndex);

  // Called when a segment is broken.
  abstract protected void UpdateUVsForSegment(
      int iSegmentBack,
      int iSegmentFront, float size);

  // Called every time leading position changes.
  // Pass:
  //   iQuad0, iQuad1 - half-open range of quads (not solids!) to update
  //                    was m_PrevQuadIndex, m_LeadingQuadIndex
  //   size - brush size
  abstract protected void UpdateUVs(int iQuad0, int iQuad1, float size);

  protected float SolidLength(Vector3[] aVerts, int iSolid) {
    int quadsPerSolid = m_EnableBackfaces ? 2 : 1;
    int iVert = iSolid * quadsPerSolid * 6;
    float lengthA = Vector3.Distance(aVerts[iVert    ], aVerts[iVert + 1]);
    float lengthB = Vector3.Distance(aVerts[iVert + 3], aVerts[iVert + 5]);
    return Mathf.Lerp(lengthA, lengthB, 0.5f);
  }

  protected float QuadLength(Vector3[] aVerts, int iQuad) {
    int quadsPerSolid = m_EnableBackfaces ? 2 : 1;
    return SolidLength(aVerts, iQuad / quadsPerSolid);
  }
}
}  // namespace TiltBrush
