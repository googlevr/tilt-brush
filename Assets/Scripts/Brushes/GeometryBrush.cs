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

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush {

public abstract class GeometryBrush : BaseBrushScript {
  // TODO: change to class?
  public struct Knot {
    public PointerManager.ControlPoint point;

    /// Position, smoothed with a kernel of (.25, .5, .25)
    public Vector3 smoothedPos;

    /// Constant, associated with this knot
    public float smoothedPressure;

    /// Distance from previous knot to this knot, or 0 (if first).
    /// Mutated during geometry generation.
    public float length;

    /// Mutable, only valid if HasGeometry.
    /// Some subclasses choose to use this instead of nRight/nSurface
    /// TODO: remove nRight, nSurface and use this instead.
    public Quaternion qFrame;

    /// Mutable, associated with prev knot and this knot.
    /// Only valid if HasGeometry; unit-length.
    public Vector3 nRight;

    /// Mutable, associated with prev knot and this knot.
    /// Only valid if HasGeometry; unit-length.
    public Vector3 nSurface;

    /// First triangle used by this knot.
    /// Multiply by 3 and use as an index into m_geometry.m_tris2.
    /// Invariants:
    /// 0. knots[0].iTri == 0
    ///    This means that there's no geometry before the first knot.
    /// 0b. knots[0].nTri == 0
    ///    The first knot !HasGeometry. It's unclear whether we can relax this; but
    ///    currently, geometry generation can never happen for knot 0 because all
    ///    generators assume that there is a previous knot that they can use to create
    ///    a coordinate frame. Also, brushes likely assume that the 1st knot is the start
    ///    or a stroke (ie !prev.HasGeometry).
    /// 1. prev.iTri <= iTri <= prev.iTri + prev.nTri
    ///    This means the first triangle(s) might be shared with the prev knot,
    ///    but not further back than that.
    /// 2. iTri + nTri >= prev.iTri + prev.nTri
    ///    This means that the current knot's geometry doesn't end somewhere
    ///    in the middle of the last knot's geometry. That would have the effect
    ///    of shortening the stroke. The amount of geometry must be non-decreasing.
    /// 3. iTri == prev.iTri + prev.nTri when !HasGeometry
    ///    This means they point to the very end of the previous strip.
    public int iTri;

    /// First vertex used by this knot.
    /// Use as an index into m_geometry.m_verts2.
    /// Similar invariants to iTri (qv)
    public ushort iVert;

    /// Number of vertices/triangles in this chunk of geometry.
    /// Includes geometry shared with adjacent knots.
    public ushort nTri, nVert;

    /// Mutable, only valid HasGeometry.
    /// True if previous knot !HasGeometry
    public bool startsGeometry;

    /// Mutable, only valid HasGeometry.
    /// True if next knot !HasGeometry
    public bool endsGeometry;

    /// true iff there is geometry that ends at point.m_Pos.
    /// Used to detect the start/end of segments in a stroke.
    public bool HasGeometry { get { return nVert > 0; } }

    /// Because qFrame is only valid if HasGeometry
    public Quaternion? Frame { get { return HasGeometry ? (Quaternion?)qFrame : null; } }
  }

  // Distance for pressure to lerp to ~90% of its instantaneous value. Value in meters.
  static float kPressureSmoothWindowMeters_PS = .20f;

  /// Upper bound on the number of extra verts needed to add a new knot.
  protected int m_UpperBoundVertsPerKnot;

  /// If set, flipside geometry is automatically generated if you use
  /// the SetXxx APIs.  Top- and bottom-side verts and tris are adjacent
  /// to each other in the vert and tri arrays (halving the effective
  /// size of the post-transform cache)
  protected bool m_bDoubleSided;

  protected readonly bool m_bSmoothPositions;

  protected bool m_bM11Compatibility;

  /// Number of verts that we try to stay under.
  /// When this is exceeded, the stroke is stopped and a new one started.
  protected readonly int m_SoftVertexLimit;

  /// Number of sides: 1 if single sided, 2 if double sided.
  /// Used for convenient indexing into vert and tri data
  protected int NS;

  protected List<Knot> m_knots;
  protected GeometryPool m_geometry;
  protected int m_CachedNumVerts;  // for use after we free m_geometry
  protected int m_CachedNumTris;
  /// The first control point that hasn't had geometry created for it yet,
  /// or null if geometry is fully up-to-date.
  protected int? m_FirstChangedControlPoint;

  public int NumVerts {
    get {
      return (m_geometry != null) ? m_geometry.m_Vertices.Count : m_CachedNumVerts;
    }
  }

  public int NumTris {
    get {
      return (m_geometry != null) ? m_geometry.m_Tris.Count : m_CachedNumTris;
    }
  }

  /// bDoubleSided
  ///    If set, the Set{Tri,Vert,UV,Tangent} APIs will automatically
  ///    create duplicate geometry for the other side. If you want to
  ///    take this setting from the brush descriptor, see
  ///    SetDoubleSided()
  ///
  /// uppeBoundVertsPerKnot
  ///    Upper bound on (single-sided) number of verts needed to add
  ///    a new knot
  public GeometryBrush(
      bool bCanBatch,
      int upperBoundVertsPerKnot,
      bool bDoubleSided,
      bool bSmoothPositions = true)
    : base(bCanBatch: bCanBatch) {
    m_bDoubleSided = bDoubleSided;
    NS = (bDoubleSided ? 2 : 1);
    m_UpperBoundVertsPerKnot  = NS * upperBoundVertsPerKnot;
    m_knots = new List<Knot>();
    // TODO: make configurable by subclasses
    m_SoftVertexLimit = 9000;
    m_bSmoothPositions = bSmoothPositions;
  }

  // Useful for sanity-checking your new code, especially DecayBrush()
  protected void CheckKnotInvariants() {
#if DEBUG
    Knot k0 = m_knots[0];
    Debug.Assert(k0.iTri == 0 && k0.iVert == 0, "Invariant 0");
    Debug.Assert(k0.nTri == 0 && k0.nVert == 0, "Invariant 0b");
    for (int i = 1; i < m_knots.Count; ++i) {
      Knot prev = m_knots[i-1];
      Knot cur = m_knots[i];
      Debug.AssertFormat(prev.iTri <= cur.iTri, "starts before prev at {0}", i);
      Debug.AssertFormat(cur.iTri <= prev.iTri + prev.nTri, "non-contiguous at {0}", i);
      Debug.AssertFormat(cur.iTri + cur.nTri >= prev.iTri + prev.nTri, "shorten at {0}", i);
      Debug.AssertFormat(prev.iVert <= cur.iVert, "starts before prev at {0}", i);
      Debug.AssertFormat(cur.iVert <= prev.iVert + prev.nVert, "non-contiguous at {0}", i);
      Debug.AssertFormat(cur.iVert + cur.nVert >= prev.iVert + prev.nVert, "shorten at {0}", i);
    }
#endif
  }

  /// Helper for DecayBrush(); removes initial knots and their associated geometry.
  protected void RemoveInitialKnots(int knotsToShift) {
    if (knotsToShift == 0) {
      return;
    }

    m_knots.RemoveRange(0, knotsToShift);

    if (m_FirstChangedControlPoint.HasValue) {
      m_FirstChangedControlPoint = Mathf.Max(m_FirstChangedControlPoint.Value - knotsToShift, 1);
    }

    // Shift knots' pointers into geometry

    // Invariant 0 says 0th knot must start at i{Vert,Tri} = 0
    // Invariant 0b says that the 0th knot has no geometry
    ushort vertShift;
    int triShift; {
      Knot k0 = m_knots[0];
      vertShift = (ushort) (k0.iVert + k0.nVert);
      triShift = k0.iTri + k0.nTri;
      k0.iVert = 0;
      k0.nVert = 0;
      k0.iTri = 0;
      k0.nTri = 0;
      m_knots[0] = k0;
    }
    for (int k = 1; k < m_knots.Count; k++) {
      Knot dupe = m_knots[k];
      // Invariant 1 says that vertShift is <= cur.iVert, so this is safe.
      dupe.iVert = (ushort) (m_knots[k].iVert - vertShift);
      dupe.iTri = m_knots[k].iTri - triShift;
      m_knots[k] = dupe;
    }
    CheckKnotInvariants();

    // Shift geometry
    m_geometry.ShiftForward(vertShift, triShift);
  }

  //
  // GeometryBrush API
  //

  /// Called when control points are changed and/or added.
  /// Subclass should assume that all knots >= iKnot have changed.
  /// It should regenerate geometry, indices, etc.
  /// iKnot will always be > 0, so there is guaranteed to always be a previous knot.
  abstract protected void ControlPointsChanged(int iKnot);

  /// Used to work out the distance of a point from a knot. Default implementation just uses the
  /// straight-line distance between the two.
  protected virtual float DistanceFromKnot(int knotIndex, Vector3 pos) {
    return (pos - m_knots[knotIndex].point.m_Pos).magnitude;
  }

  //
  // BaseBrushScript override API
  //

  override public bool AlwaysRebuildPreviewBrush() {
    return true;
  }

  override public int GetNumUsedVerts() {
    return NumVerts;
  }

  override public bool IsOutOfVerts() {
    // Check if we have room for one more stride's worth of verts.
    // This is undocumented, but in Unity, 0xffff is an invalid index
    int LAST_VALID_INDEX = 0xfffe;
    return (GetNumUsedVerts() + m_UpperBoundVertsPerKnot)-1 > LAST_VALID_INDEX;
  }

  override public bool ShouldCurrentLineEnd() {
    return (IsOutOfVerts() || NumVerts > m_SoftVertexLimit);
  }

  override public bool ShouldDiscard() {
    // TODO: This should discard if the last stroke is too short (ie., 3 or fewer knots
    // since two knots are automatically added at the beginning).
    return GetNumUsedVerts() <= 0;
  }

  override public void ResetBrushForPreview(TrTransform localPointerXf) {
    base.ResetBrushForPreview(localPointerXf);

    m_knots.Clear();
    Vector3 pos = localPointerXf.translation;
    Quaternion ori = localPointerXf.rotation;
    Knot knot = new Knot {
      point = new PointerManager.ControlPoint {
        // TODO: better value for pressure?
        m_Pos = pos, m_Orient = ori, m_Pressure = 1
      },
      length = 0,
      smoothedPos = pos
    };
    m_knots.Add(knot);
    m_knots.Add(knot);
  }

  /// Set m_bDoubleSided according to settings in the descriptor
  protected void SetDoubleSided(TiltBrush.BrushDescriptor desc) {
    // Yuck. This class was authored assuming all this stuff was readonly,
    // which makes it awkward to now be able to set it from the descriptor
    if (desc.m_RenderBackfaces && !m_bDoubleSided) {
      // enable
      m_bDoubleSided = true;
      NS *= 2;
      m_UpperBoundVertsPerKnot *= 2;
    } else if (!desc.m_RenderBackfaces && m_bDoubleSided) {
      // disable
      m_bDoubleSided = false;
      NS /= 2;
      m_UpperBoundVertsPerKnot /= 2;
    }
  }

  protected override void InitBrush(BrushDescriptor desc, TrTransform localPointerXf) {
    base.InitBrush(desc, localPointerXf);
    m_bM11Compatibility = desc.m_M11Compatibility;
    m_geometry = GeometryPool.Allocate();

    m_knots.Clear();
    Vector3 pos = localPointerXf.translation;
    Quaternion ori = localPointerXf.rotation;
    Knot knot = new Knot {
      point = new PointerManager.ControlPoint {
        m_Pos = pos, m_Orient = ori, m_Pressure = 1
      },
      length = 0,
      smoothedPos = pos
    };
    m_knots.Add(knot);
    m_knots.Add(knot);

    MeshFilter mf = GetComponent<MeshFilter>();
    mf.mesh = null;  // Force a new, empty, mf-owned mesh to be generated
    mf.mesh.MarkDynamic();
  }

  override public void DebugGetGeometry(
      out Vector3[] verts, out int nVerts,
      out Vector2[] uv0s,
    out int[] tris, out int nTris) {
    verts  = m_geometry.m_Vertices.GetBackingArray();
    nVerts = m_geometry.m_Vertices.Count;
    if (m_geometry.Layout.texcoord0.size == 2) {
      uv0s = m_geometry.m_Texcoord0.v2.GetBackingArray();
    } else {
      uv0s = null;
    }
    tris   = m_geometry.m_Tris.GetBackingArray();
    nTris  = m_geometry.m_Tris.Count;
  }

  override public void FinalizeSolitaryBrush() {
    var mesh =  GetComponent<MeshFilter>().mesh;
    m_geometry.CopyToMesh(mesh);

    m_CachedNumVerts = NumVerts;
    m_CachedNumTris = NumTris;

    GeometryPool.Free(m_geometry);
    m_geometry = null;

    mesh.RecalculateBounds();
  }

  override public BatchSubset FinalizeBatchedBrush() {
    var mgr = this.Canvas.BatchManager;
    return mgr.CreateSubset(m_Desc, m_geometry);
  }

  /// Don't necessarily have to use the master's information to update the mesh.
  override public void ApplyChangesToVisuals() {
    if (! m_geometry.VerifySizes()) {
      return;
    }

    if (m_FirstChangedControlPoint != null) {
      try {
        StatelessRng.BeginSaltReuseCheck();
        ControlPointsChanged(m_FirstChangedControlPoint.Value);
      } finally {
        StatelessRng.EndSaltReuseCheck();
      }
      m_FirstChangedControlPoint = null;
    }

    var mesh = GetComponent<MeshFilter>().mesh;
    m_geometry.CopyToMesh(mesh);
    mesh.RecalculateBounds();
  }

  override protected void InitUndoClone(GameObject clone) {
    var rMeshScript = clone.AddComponent<UndoMeshAnimScript>();
    rMeshScript.Init();
  }

  override protected bool UpdatePositionImpl(Vector3 pos, Quaternion ori, float pressure) {
    Debug.Assert(m_knots.Count >= 2);

    // XXX: we want to be passed the control point instead
    int iUpdate = m_knots.Count-1;
    Knot updated = m_knots[iUpdate];
    updated.point.m_Pos = pos;
    updated.point.m_Orient = ori;
    updated.point.m_Pressure = pressure;
    updated.point.m_TimestampMs = (uint)(App.Instance.CurrentSketchTime * 1000);
    updated.smoothedPos = pos;
    if (iUpdate < 2) {
      // Retroactively update the 0th knot with better pressure data.
      float initialPressure = m_bM11Compatibility || m_PreviewMode ? 0 : pressure;
      Knot initialKnot = m_knots[0];
      initialKnot.point.m_Pressure = initialPressure;
      initialKnot.smoothedPressure = initialPressure;
      m_knots[0] = initialKnot;
    } else if (m_bSmoothPositions) {
      Knot middle = m_knots[iUpdate-1];
      Vector3 v0 = m_knots[iUpdate-2].point.m_Pos;
      Vector3 v1 = middle.point.m_Pos;
      Vector3 v2 = pos;
      middle.smoothedPos = (v0 + 2*v1 + v2) / 4;
      m_knots[iUpdate-1] = middle;
    }
    if (m_bSmoothPositions) {
      ApplySmoothing(m_knots[iUpdate - 1], ref updated);
    } else {
      updated.smoothedPressure = updated.point.m_Pressure;
    }
    m_knots[iUpdate] = updated;

    if (m_FirstChangedControlPoint.HasValue) {
      m_FirstChangedControlPoint = Mathf.Min(m_FirstChangedControlPoint.Value, iUpdate);
    } else {
      m_FirstChangedControlPoint = iUpdate;
    }

    float lastLength = DistanceFromKnot(iUpdate - 1, updated.point.m_Pos);
    bool keep = (lastLength > GetSpawnInterval(updated.smoothedPressure));

    // TODO: change this to the way PointerScript keeps control points
    if (keep) {
      Knot dupe = updated;
      dupe.iVert = (ushort)(updated.iVert + updated.nVert);
      dupe.nVert = 0;
      dupe.iTri = updated.iTri + updated.nTri;
      dupe.nTri = 0;
      m_knots.Add(dupe);
    }
    return keep;
  }

  //
  // Geometry-creation helpers
  //

  /// Set triangle and bottomside triangle.
  ///  iTri, iVert      pass knot.iTri, knot.iVert
  ///  tp               index of triangle pair
  ///  vp0, vp1, vp2    vertex pairs in that solid
  protected void SetTri(int iTri, int iVert, int tp, int vp0, int vp1, int vp2) {
    var tris = m_geometry.m_Tris;
    int i = (iTri + tp * NS) * 3;
    tris[i    ] = iVert + vp0 * NS;
    tris[i + 1] = iVert + vp1 * NS;
    tris[i + 2] = iVert + vp2 * NS;

    if (m_bDoubleSided) {
      tris[i + 3] = iVert + vp2 * NS + 1;
      tris[i + 4] = iVert + vp1 * NS + 1;
      tris[i + 5] = iVert + vp0 * NS + 1;
    }
  }

  /// Set position, normal, color of the vertex pair at offset.
  ///  iVert    pass knot.iVert
  ///  vp       index of vert pair
  protected void SetVert(int iVert, int vp, Vector3 v, Vector3 n, Color32 c, float alpha) {
    c.a = (byte)(alpha * 255);

    int i = iVert + vp * NS;
    m_geometry.m_Vertices[i] = v;
    m_geometry.m_Normals[i] = n;
    m_geometry.m_Colors[i] = c;

    if (m_bDoubleSided) {
      m_geometry.m_Vertices[i + 1] = v;
      m_geometry.m_Normals[i + 1] = -n;
      m_geometry.m_Colors[i + 1] = c;
    }
  }

  /// Set texcoord0 of the vertex pair at offset.
  ///  iVert    pass knot.iVert
  ///  vp       index of vert pair
  protected void SetUv0(int iVert, int vp, Vector2 data) {
    int i = iVert + vp * NS;
    m_geometry.m_Texcoord0.v2[i] = data;
    if (m_bDoubleSided) {
      m_geometry.m_Texcoord0.v2[i + 1] = data;
    }
  }

  /// Set texcoord0 of the vertex pair at offset.
  ///  iVert    pass knot.iVert
  ///  vp       index of vert pair
  protected void SetUv0(int iVert, int vp, Vector4 data) {
    int i = iVert + vp * NS;
    m_geometry.m_Texcoord0.v4[i] = data;
    if (m_bDoubleSided) {
      m_geometry.m_Texcoord0.v4[i + 1] = data;
    }
  }

  /// Set texcoord1 of the vertex pair at offset.
  ///  iVert    pass knot.iVert
  ///  vp       index of vert pair
  protected void SetUv1(int iVert, int vp, Vector3 data) {
    int i = iVert + vp * NS;
    m_geometry.m_Texcoord1.v3[i] = data;
    if (m_bDoubleSided) {
      m_geometry.m_Texcoord1.v3[i + 1] = data;
    }
  }

  /// Set texcoord0 of the vertex pair at offset.
  ///  iVert    pass knot.iVert
  ///  vp       index of vert pair
  protected void SetUv1(int iVert, int vp, Vector4 data) {
    int i = iVert + vp * NS;
    m_geometry.m_Texcoord1.v4[i] = data;
    if (m_bDoubleSided) {
      m_geometry.m_Texcoord1.v4[i + 1] = data;
    }
  }

  /// Set tangent of the vertex pair at offset.
  /// It will be made orthogonal to the existing normal.
  ///  iVert    pass knot.iVert
  ///  vp       index of vert pair
  protected void SetTangent(int iVert, int vp, Vector3 tangent, float w=1) {
    int i = iVert + vp * NS;
    Vector3 normal = m_geometry.m_Normals[i];
    Vector4 orthoTangent = (tangent - Vector3.Dot(tangent, normal) * normal).normalized;
    orthoTangent.w = w;
    m_geometry.m_Tangents[i] = orthoTangent;
    if (m_bDoubleSided) {
      orthoTangent.w = -w;
      m_geometry.m_Tangents[i + 1] = orthoTangent;
    }
  }

  /// Set size of geometry arrays to exactly fit what the knots need.
  /// Helper for ControlPointsChanged.
  protected void ResizeGeometry() {
    Knot k = m_knots[m_knots.Count-1];
    int nVerts = k.iVert + k.nVert;
    m_geometry.NumVerts = nVerts;
    int nTris = k.iTri + k.nTri;
    m_geometry.m_Tris.SetCount(nTris * 3);
  }

  /// Recompute tangents for all triangles. Does no blending.
  /// Verts that are part of more than one triangle use an
  /// arbitrarily-chosen triangle's tangent.
  ///
  /// Because it's brute-force, intended mainly for prototyping.
  protected void BruteForceRecomputeTangents(int iKnot0, List<Vector2> uvs) {
    for (int iTriIndex = m_knots[iKnot0].iTri*3;
         iTriIndex < m_geometry.m_Tris.Count;
         iTriIndex += 3) {
      int iv0 = m_geometry.m_Tris[iTriIndex    ];
      int iv1 = m_geometry.m_Tris[iTriIndex + 1];
      int iv2 = m_geometry.m_Tris[iTriIndex + 2];
      Vector3 vS, vT;
      ComputeST(m_geometry.m_Vertices, uvs, 0,
                iv0, iv1, iv2,
                out vS, out vT);
      SetTangent(0, iv0, vS);
      SetTangent(0, iv1, vS);
      SetTangent(0, iv2, vS);
    }
  }

  //
  // Internal utilities
  //

  // Update values in next that need smoothing (currently only smoothedPressure)
  protected void ApplySmoothing(Knot prev, ref Knot next) {
    float distance = Vector3.Distance(prev.point.m_Pos, next.point.m_Pos);
    float pressureSmoothWindowMeters_PS
        = m_bM11Compatibility ? 0.1f : kPressureSmoothWindowMeters_PS;
    float window = pressureSmoothWindowMeters_PS * App.METERS_TO_UNITS * POINTER_TO_LOCAL;
    float k = Mathf.Pow(.1f, distance / window);
    next.smoothedPressure = k * prev.smoothedPressure + (1-k) * next.point.m_Pressure;
  }
}
} // namespace TiltBrush
