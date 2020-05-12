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

using MIConvexHull;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush {

// TODO:
// - Don't keep control points that are interior to the hull (requires API changes so
//   brushes can better specify which control points they keep)
// - Tune the plane distance tolerance
// - Recreating geometry shouldn't run O(n) hull computations
// - Modify MIConvexHull to allow the computation to be incremental?
// - Modify MIConvexHull to support a "simplification" tunable?
// - Experiment with texture mapping
// - GPU collision only checks verts and edges, not faces. That doesn't interact
//   well with this brush, which generates larger faces than other brushes.
public class HullBrush : GeometryBrush {
  const double kToleranceMeters_PS = 1e-6;

  // Do not change these numbers; they are linked to determinism.
  const int kVerticesPerKnot_Point = 1;
  const int kVerticesPerKnot_Tetrahedron = 4;
  const int kDirectedSphereRingPoints = 4;
  const int kDirectedSphereRings = 2;
  const float kDirectedSphereRingAngleDegrees = 45;
  const int kVerticesPerKnot_DirectedSphere = 1 + kDirectedSphereRingPoints * kDirectedSphereRings;

  public class Vertex : IVertex {
    public double[] Position { get; set; }
    /// If set, this Vertex will never be on the hull.
    public bool DefinitelyInterior { get; set; }

    /// Temporary storage; only used during geometry creation
    public int TempIndex { get; set; }
    public Vector3 TempNormal { get; set; }

    public Vertex() {
      Position = new double[3];
    }

    // Same as the constructor, but re-use a previously-constructed instance.
    public void SetData(Vector3 v) {
      Position[0] = v.x;
      Position[1] = v.y;
      Position[2] = v.z;
      DefinitelyInterior = false;
    }
  }

  public class Face : ConvexFace<Vertex, Face> {
    // This empty constructor is needed to work around a mono compiler bug.
    // Without it, the compiler thinks this class doesn't satisfy the "new()" constraint.
    // ReSharper disable once EmptyConstructor
    public Face() {}
  }

  [Serializable]
  public enum KnotConversion {
    Point,
    Tetrahedron,
    DirectedSphere,
  }

  static Vector3 AsVector3(double[] ds) {
    return new Vector3((float)ds[0], (float)ds[1], (float)ds[2]);
  }

  /// Temporary, for development
  /// Faceted currently looks better than smooth normals, but generates more verts
  [SerializeField] bool m_Faceted;

  /// For optimization.  If set, keep track of knots that we know to be interior
  /// to the hull.  These knots will be filtered out when computing future hulls.
  [SerializeField] bool m_TrackInterior;

  /// Governs how many hull input points we create per knot
  [SerializeField] KnotConversion m_KnotConversion;

  /// Larger is simpler; units are pointer-space (ie, room-space) meters.
  /// Simplification is disabled if <= 0
  /// Useful range is maybe .1 mm to 10 mm.
  /// WARNING: this option makes the brush nondeterministic if !m_SimplifyAtEnd
  [SerializeField] float m_Simplification_PS;


  [Serializable]
  public enum SimplifyMode {
    Disabled,
    SimplifyAtEnd,
    SimplifyInteractively,
  }

  /// If set, run a simplification pass when finalizing the brush. Otherwise,
  /// simplify every time a control point is kept.
  [SerializeField] SimplifyMode m_SimplifyMode;

  /// The set of Vertex instances created from knots.
  /// Each Knot corresponds to exactly GetNumVerticesPerKnot() Vertex instances.
  private List<Vertex> m_AllVertices;

  public HullBrush()
    : base(bCanBatch: true,
           upperBoundVertsPerKnot: 1,
           bDoubleSided: false) {
    m_AllVertices = new List<Vertex>();
  }

  //
  // GeometryBrush API
  //

  override public bool ShouldCurrentLineEnd() {
    // Reminder: it's ok for this method to be nondeterministic.
    int localMaxKnotCount = App.PlatformConfig.HullBrushMaxKnots;
    int maxVertInputCount = App.PlatformConfig.HullBrushMaxVertInputs;
    int hullInputSize = m_AllVertices.Count(v => !v.DefinitelyInterior);

    // For Hull Brush, the limiting factor is the number of points we send to the hull
    // generator.  We want to protect against overloading that.
    // The knot count limit is not really all that useful and maybe we should remove it.
    return m_knots.Count > localMaxKnotCount
        || hullInputSize > maxVertInputCount
        || base.ShouldCurrentLineEnd();
  }

  protected override void InitBrush(BrushDescriptor desc, TrTransform localPointerXf) {
    base.InitBrush(desc, localPointerXf);
    SetDoubleSided(desc);
    m_geometry.Layout = GetVertexLayout(desc);
    CreateVerticesFromKnots(0);
  }

  public override float GetSpawnInterval(float pressure01) {
    return m_Desc.m_SolidMinLengthMeters_PS * POINTER_TO_LOCAL * App.METERS_TO_UNITS;
  }

  protected override void ControlPointsChanged(int iKnot0) {
    OnChanged_FrameKnots(iKnot0);
    CreateVerticesFromKnots(iKnot0);
    OnChanged_MakeGeometry();
  }

  public override void ResetBrushForPreview(TrTransform localPointerXf) {
    base.ResetBrushForPreview(localPointerXf);
    CreateVerticesFromKnots(0);
  }

  public override BatchSubset FinalizeBatchedBrush() {
    if (m_SimplifyMode == SimplifyMode.SimplifyAtEnd) {
      OnChanged_MakeGeometry(isEnd: true);
    }
    return base.FinalizeBatchedBrush();
  }

  void ResizeVertices(int desired) {
    // Be less garbagey: re-use the Vertex instances when possible.
    if (m_AllVertices.Count > desired) {
      m_AllVertices.RemoveRange(desired, m_AllVertices.Count - desired);
    } else {
      while (m_AllVertices.Count < desired) {
        m_AllVertices.Add(new Vertex());
      }
    }
  }

  void OnChanged_FrameKnots(int iKnot0) {
    Knot prev = m_knots[iKnot0 - 1];
    for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot) {
      Knot cur = m_knots[iKnot];
      Vector3 vMove = cur.point.m_Pos - prev.point.m_Pos;
      ComputeSurfaceFrameNew(prev.nRight, vMove.normalized, cur.point.m_Orient,
          out cur.nRight, out cur.nSurface);

      m_knots[iKnot] = cur;
      prev = cur;
    }
  }

  int GetNumVerticesPerKnot() {
    switch (m_KnotConversion) {
    case KnotConversion.Point:
      return kVerticesPerKnot_Point;

    case KnotConversion.Tetrahedron:
      return kVerticesPerKnot_Tetrahedron;

    case KnotConversion.DirectedSphere:
      return kVerticesPerKnot_DirectedSphere;

    default:
      return 0;
    }
  }

  // Creates verts for knots >= iKnot0
  // Reads from m_knots; writes to m_AllVertices.
  void CreateVerticesFromKnots(int iKnot0) {
    // TODO: sanity-check that we fill in exactly this many values
    int verticesPerKnot = GetNumVerticesPerKnot();
    ResizeVertices(m_knots.Count * verticesPerKnot);
    switch (m_KnotConversion) {

    case KnotConversion.Point:
      for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot) {
        m_AllVertices[iKnot].SetData(m_knots[iKnot].point.m_Pos);
      }
      break;

    case KnotConversion.Tetrahedron: {
      // Inscribe a tetrahedon inside the cube. Treat BaseSize as the "radius"
      // of the tetrahedron/cube.
      // Learnings: it feels good to start with a full 3d shape.

      //   BaseSize = sqrt(3 * halfwidth^2)
      //   halfwidth = BaseSize / sqrt(3)
      float hw = m_BaseSize_PS / Mathf.Sqrt(3f);
      for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot) {
        Vector3 p = m_knots[iKnot].point.m_Pos;
        int iv0 = iKnot * verticesPerKnot;
        m_AllVertices[iv0 + 0].SetData(p + new Vector3(-hw, -hw, -hw));
        m_AllVertices[iv0 + 1].SetData(p + new Vector3(+hw, +hw, -hw));
        m_AllVertices[iv0 + 2].SetData(p + new Vector3(+hw, -hw, +hw));
        m_AllVertices[iv0 + 3].SetData(p + new Vector3(-hw, +hw, +hw));
      }
      break;
    }

    case KnotConversion.DirectedSphere: {
      // Tesselate a chunk of the sphere, in the direction of the pointer motion.
      // Learnings:
      // - I think probably these constants should be based on the size of the
      //   brush -- to keep the triangle size relative to the user roughly constant.
      //   If the brush is larger, tesselate more finely, etc.
      //   If the brush is very tiny, don't tesselate at all.
      //   This can get tricky if we respond to size changes during drawing (ie
      //   as a result of pressure)
      // - Both small and large brush sizes are useful. Gross detail seems to be
      //   more easily done by resizing the brush up, as opposed to by resizing the
      //   user up.
      // - Note that these values are tuned to be asymmetric, meaning, they will
      //   mirror correctly.  If you tune them, make sure the result looks identical
      //   in the mirror, after save/load, and after recolor.
      for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot) {
        Vector3 center = m_knots[iKnot].point.m_Pos;
        if (iKnot == 0) {
          // For indexing simplicity, put 'em all in the same place
          for (int i = 0; i < verticesPerKnot; ++i) {
            m_AllVertices[i].SetData(center);
          }
        } else {
          float pressure = m_PreviewMode ? m_knots[iKnot].smoothedPressure :
              m_knots[iKnot].point.m_Pressure;
          float radius = PressuredSize(pressure) * .5f;
          int iv0 = iKnot * verticesPerKnot;
          Vector3 dir = (center - m_knots[iKnot-1].point.m_Pos).normalized;
          Vector3 ortho = m_knots[iKnot].nRight;
          Vector3 p = dir * radius;

          // One point at the tip
          m_AllVertices[iv0 + 0].SetData(center + p);

          // And rings of points around that tip.
          // phi is the angle with dir; theta is the angle around the ring.
          Quaternion qPhi = Quaternion.AngleAxis(kDirectedSphereRingAngleDegrees, ortho);
          Quaternion qHalfTheta = Quaternion.AngleAxis(360f / kDirectedSphereRingPoints / 2, dir);
          Quaternion qTheta = qHalfTheta * qHalfTheta;

          for (int iRing = 0; iRing < kDirectedSphereRings; ++iRing) {
            p = qPhi * p;
            for (int i = 0; i < kDirectedSphereRingPoints; ++i) {
              m_AllVertices[iv0 + 1 + iRing * kDirectedSphereRingPoints + i].SetData(center + p);
              p = qTheta * p;
            }
          }
        }
      }
      break;
    }

    }
  }

  void OnChanged_MakeGeometry(bool isEnd=false) {
    // We'd like to be able to record which control points will never be on the
    // hull, to avoid the extra work of hulling them. However, we don't know if
    // the most-recent knot is a keeper. If it's not a keeper, we can't say for
    // sure that any verts not on the hull won't reappear on the hull when the
    // most-recent knot changes to some other location.
    //
    // Thus, only trust the "not on hull" result if all the input knots are
    // keepers. But there's no exposed way of determining if all the knots are
    // keepers. One conservative thing we can do is: check whether the last two
    // knots are identical; if so, don't hull the last knot. This is safe because
    // identical knots are fungible (for purposes of hulling). It's an invariant
    // that every knot's a keeper except (maybe) the last knot; thus, we're only
    // hulling keepers and the not-in-hull result can be trusted.
    //
    // This then raises the question: are the last 2 knots ever identical? If not,
    // we'll lose out on our optimization. It turns out that when GeometryBrush
    // decides to keep a knot, it duplicates it before going to geometry generation.
    // So currently it's an okay assumption, but maybe a little more fragile than
    // we'd like. Worth revisiting once we do the work to give brushes more control
    // over which control points are kept.
    bool recordInterior = false;
    UnityEngine.Profiling.Profiler.BeginSample("Track Interior");
    if (m_TrackInterior && m_knots.Count >= 2) {
      int last = m_knots.Count-1;
      if (m_knots[last].point.m_Pos == m_knots[last-1].point.m_Pos) {
        recordInterior = true;
        // Only hull vertices which are guaranteed not to change or disappear.
        //
        // This removal is _required_ (in order to recordInterior), because otherwise
        // we might generate incorrect values for DefinitelyInterior. False negatives
        // are benign; false positives can cause vertices to be permanently omitted
        // from the hull.
        //
        // This removal is _safe_ because removal of the last N vertices does not
        // change the hull. Each of those vertices has a duplicate that is kept.
        // Proof: the last knot is identical to the last-1 knot; identical knots
        // generate identical Vertices.
        //
        // This temporarily breaks the invariant that # vertices = # knots * vertsPerKnot,
        // but the alternative is linq shenanigans (ie, m_AllVertices.Take(Count-N)).
        // We don't really rely on that invariant, and the invariant is re-established
        // periodically by ResizeVertices().
        int toRemove = GetNumVerticesPerKnot();
        m_AllVertices.RemoveRange(m_AllVertices.Count - toRemove, toRemove);
      }
    }
    UnityEngine.Profiling.Profiler.EndSample();

    // Don't bother hulling the ones that we've proven will never be on the hull
    UnityEngine.Profiling.Profiler.BeginSample("Remove Interior");
    var input = m_AllVertices.Where(v => !v.DefinitelyInterior).ToList();
    UnityEngine.Profiling.Profiler.EndSample();

    Knot knot = m_knots[1];

    // Clear geometry because we recreate from scratch
    knot.iVert = 0;
    knot.nVert = 0;
    knot.iTri = 0;
    knot.nTri = 0;
    m_geometry.m_Vertices.SetCount(0);
    m_geometry.m_Normals.SetCount(0);
    m_geometry.m_Colors.SetCount(0);
    m_geometry.m_Texcoord0.v3.SetCount(0);
    m_geometry.m_Tris.SetCount(0);

    // Straightedge is very WYSIWYG, so don't simplify if it's enabled.
    bool simplify = !PointerManager.m_Instance.StraightEdgeModeEnabled &&
      ((isEnd && m_SimplifyMode == SimplifyMode.SimplifyAtEnd) ||
       (!isEnd && m_SimplifyMode == SimplifyMode.SimplifyInteractively && recordInterior));

    // Attempt to create hull. It can fail if the dimensionality is too low
    // because of too few points; and maybe also if the points are collinear/coplanar.
    // Only simplify after keeping a knot, to try to keep things feeling a bit more
    // like what a production implementation would do -- if simplification happens
    // every frame we get very jittery behavior that feels uncontrollable.
    UnityEngine.Profiling.Profiler.BeginSample("Create Hull");
    ConvexHull<Vertex, Face> hull = CreateHull(input, enableSimplify: simplify);
    UnityEngine.Profiling.Profiler.EndSample();

    if (hull != null) {
      if (recordInterior) {
        foreach (var v in input) {
          v.DefinitelyInterior = true;
        }
        foreach (var v in hull.Points) {
          v.DefinitelyInterior = false;
        }
      }

      UnityEngine.Profiling.Profiler.BeginSample("Create Geometry");
      if (m_Faceted) {
        CreateFacetedGeometry(ref knot, hull);
      } else {
        CreateSmoothGeometry(ref knot, hull);
      }
      UnityEngine.Profiling.Profiler.EndSample();
    }

    m_knots[1] = knot;
  }

  ConvexHull<Vertex, Face> CreateHull(List<Vertex> input, bool enableSimplify) {
    if (input.Count < 3) {
      return null;
    }

    try {
      if (m_Simplification_PS > 0 && enableSimplify) {
        // Abuse the "tolerance" parameter of the convex hull generator. This generates
        // simpler geometry that is sometimes nonmanifold, especially as you crank up the
        // tolerance. Thus, this pass is used solely to strip points.
        var simpleHull = ConvexHull.Create<Vertex, Face>(
            input, m_Simplification_PS * App.METERS_TO_UNITS * POINTER_TO_LOCAL);
        input = simpleHull.Points.ToList();
      }
      return ConvexHull.Create<Vertex, Face>(
          input, kToleranceMeters_PS * App.METERS_TO_UNITS * POINTER_TO_LOCAL);
    } catch (ArgumentOutOfRangeException) {
      // Too much degeneracy to create a hull (this is the exception we actually get)
      // The docs say Create() throws ArgumentException in this case, but instead it
      // reads off the end of a List<>, causing this range exception.
      return null;
    } catch (ArgumentException) {
      // Too much degeneracy to create a hull
      return null;
    }
  }

  static Color32 FlipColor(Color32 c) {
    return new Color32(c.g, c.b, c.r, c.a);
  }

  // Geometry pool is empty. Create all-new geometry, associated with the passed knot,
  // for the given hull.
  void CreateFacetedGeometry(ref Knot knot, ConvexHull<Vertex, Face> hull) {
    foreach (var face in hull.Faces) {
      // This is the index of a vertex pair, not a vertex
      int v0 = m_geometry.m_Vertices.Count / NS;
      Vector3 normal = AsVector3(face.Normal);
      foreach (var vertex in face.Vertices) {
        AppendVert(ref knot, AsVector3(vertex.Position), normal);
      }

      // Tesselate the polygon into a fan
      int numFan = face.Vertices.Length - 2;
      for (int iFan = 0; iFan < numFan; ++iFan) {
        AppendTri(ref knot, v0, v0 + (iFan + 1), v0 + (iFan + 2));
      }
    }
  }

  // Geometry pool is empty. Create all-new geometry, associated with the passed knot,
  // for the given hull.
  void CreateSmoothGeometry(ref Knot knot, ConvexHull<Vertex, Face> hull) {
    int i = 0;
    foreach (var v in hull.Points) {
      v.TempNormal = Vector3.zero;
      v.TempIndex = i++;
    }

    foreach (var face in hull.Faces) {
      Vector3 normal = AsVector3(face.Normal);
      Vertex[] vs = face.Vertices;
      int nv = vs.Length;
      for (int iv = 0; iv < nv; ++iv) {
        // Use the angle at this vertex of the face to weight this face's
        // contribution to the vert's smoothed normal
        Vector3 vprev = AsVector3(vs[(iv-1+nv) % nv].Position);
        Vector3 vnext = AsVector3(vs[(iv+1+nv) % nv].Position);
        Vector3 vcur = AsVector3(vs[iv].Position);
        float angle = Vector3.Angle(vprev-vcur, vnext-vcur);
        vs[iv].TempNormal += normal * angle;
      }

      // Tesselate the polygon into a fan
      int numFan = face.Vertices.Length - 2;
      for (int iFan = 0; iFan < numFan; ++iFan) {
        AppendTri(ref knot, vs[0].TempIndex, vs[1].TempIndex, vs[2].TempIndex);
      }
    }

    foreach (var vertex in hull.Points) {
      AppendVert(ref knot, AsVector3(vertex.Position), vertex.TempNormal.normalized);
    }
  }

  override public GeometryPool.VertexLayout GetVertexLayout(BrushDescriptor desc) {
    return new GeometryPool.VertexLayout {
      bUseColors = true,
      bUseNormals = true,
      bUseTangents = false,
      uv0Size = 3,
      uv0Semantic = GeometryPool.Semantic.XyIsUvZIsDistance,
    };
  }

  void AppendVert(ref Knot k, Vector3 v, Vector3 n) {
    Debug.Assert(k.iVert + k.nVert == m_geometry.m_Vertices.Count);
    Vector3 uv = new Vector3(0, 0, m_BaseSize_PS);
    m_geometry.m_Vertices .Add(v);
    m_geometry.m_Normals  .Add(n);
    m_geometry.m_Colors   .Add(m_Color);
    m_geometry.m_Texcoord0.v3.Add(uv);
    k.nVert += 1;
    if (m_bDoubleSided) {
      m_geometry.m_Vertices .Add(v);
      m_geometry.m_Normals  .Add(-n);
      // TODO: backface is a different color for visualization reasons
      // Probably better to use a non-culling shader instead of doubling the geo.
      m_geometry.m_Colors.Add(m_Color);
      m_geometry.m_Texcoord0.v3.Add(uv);
      k.nVert += 1;
    }
  }

  /// vp{0,1,2} are indices of vertex pairs
  void AppendTri(ref Knot k, int vp0, int vp1, int vp2) {
    Debug.Assert((k.iTri + k.nTri) * 3 == m_geometry.m_Tris.Count);
    m_geometry.m_Tris.Add(vp0 * NS);
    m_geometry.m_Tris.Add(vp1 * NS);
    m_geometry.m_Tris.Add(vp2 * NS);
    k.nTri += 1;
    if (m_bDoubleSided) {
      m_geometry.m_Tris.Add(vp0 * NS + 1);
      m_geometry.m_Tris.Add(vp2 * NS + 1);
      m_geometry.m_Tris.Add(vp1 * NS + 1);
      k.nTri += 1;
    }
  }
}

} // namespace TiltBrush
