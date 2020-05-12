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
using System.Linq;

using MIConvexHull;
using UnityEngine;

namespace TiltBrush {

// TODO:
// - Why does it break so quickly?
// - Initial knot uses pressure = 0, but that's a hack. Find a better way to get initial
//   pressure. This is especially important because otherwise it's very easy to see when
//   the stroke breaks.
// - Optimize the geometry
// - Parallel transport?
public class ConcaveHullBrush : GeometryBrush {
  const double kToleranceMeters_PS = 1e-6;

  // Do not change these numbers; they are linked to determinism.
  const int kVerticesPerKnot_Rapidograph = 1;
  const int kVerticesPerKnot_QuillPen = 2;
  const int kVerticesPerKnot_Tetrahedron = 4;
  const int kVerticesPerKnot_Octahedron = 6;
  const int kVerticesPerKnot_Cube = 8;

  public class Vertex : IVertex {
    public double[] Position { get; set; }

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
    Rapidograph,
    QuillPen,
    Tetrahedron,
    Octahedron,
    Cube,
  }

  static Vector3 AsVector3(double[] ds) {
    return new Vector3((float)ds[0], (float)ds[1], (float)ds[2]);
  }

  /// Number of knots to dump into each hull. Minimum is 1, but the practical minimum is 2
  [Range(1, 40)]
  [SerializeField] int m_KnotsInHull;

  /// Temporary, for development
  /// Faceted currently looks better than smooth normals, but generates more verts and tris
  [SerializeField] bool m_Faceted;

  /// Governs how many hull input points we create per knot
  [SerializeField] KnotConversion m_KnotConversion;

  /// The set of Vertex instances created from knots.
  /// Each Knot corresponds to exactly GetNumVerticesPerKnot() Vertex instances.
  private List<Vertex> m_AllVertices;

  public ConcaveHullBrush()
    : base(bCanBatch: true,
           upperBoundVertsPerKnot: 1,
           bDoubleSided: false) {
    m_AllVertices = new List<Vertex>();
  }

  //
  // GeometryBrush API
  //

  protected override void InitBrush(BrushDescriptor desc, TrTransform localPointerXf) {
    base.InitBrush(desc, localPointerXf);
    Debug.Assert(!desc.m_RenderBackfaces); // unsupported
    m_geometry.Layout = GetVertexLayout(desc);
    FixInitialKnotSize();
    CreateVerticesFromKnots(0);
  }

  public override void ResetBrushForPreview(TrTransform localPointerXf) {
    base.ResetBrushForPreview(localPointerXf);
    FixInitialKnotSize();
    CreateVerticesFromKnots(0);
  }

  // Helper for Init/Reset
  void FixInitialKnotSize() {
    // Looks terrible if the first knot has pressure 1; try pressure 0.
    // Even better: Use the real pressure!
    for (int i = 0; i < 2; ++i) {
      Knot k = m_knots[i];
      k.point.m_Pressure = 0;
      m_knots[i] = k;
    }
  }

  public override float GetSpawnInterval(float pressure01) {
    return m_Desc.m_SolidMinLengthMeters_PS * POINTER_TO_LOCAL * App.METERS_TO_UNITS;
  }

  protected override void ControlPointsChanged(int iKnot0) {
    CreateVerticesFromKnots(iKnot0);
    OnChanged_MakeGeometry(iKnot0);
  }

  override public GeometryPool.VertexLayout GetVertexLayout(BrushDescriptor desc) {
    return new GeometryPool.VertexLayout {
      bUseColors = true,
      bUseNormals = true,
      bUseTangents = false,
    };
  }

  //
  // Brush internals
  //

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

  int GetNumVerticesPerKnot() {
    switch (m_KnotConversion) {
    case KnotConversion.Rapidograph:
      return kVerticesPerKnot_Rapidograph;

    case KnotConversion.QuillPen:
      return kVerticesPerKnot_QuillPen;

    case KnotConversion.Tetrahedron:
      return kVerticesPerKnot_Tetrahedron;

    case KnotConversion.Octahedron:
      return kVerticesPerKnot_Octahedron;

    case KnotConversion.Cube:
      return kVerticesPerKnot_Cube;

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

    // Just a point; ignores size
    case KnotConversion.Rapidograph:
      for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot) {
        m_AllVertices[iKnot].SetData(m_knots[iKnot].point.m_Pos);
      }
      break;

    // A right-left oriented line segment of "radius" size/2
    case KnotConversion.QuillPen:
      for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot) {
        Knot knot = m_knots[iKnot];
        float halfSize = 0.5f * PressuredSize(knot.point.m_Pressure);
        Vector3 halfExtent = halfSize * (knot.point.m_Orient * Vector3.right);
        int iv = iKnot * verticesPerKnot;
        m_AllVertices[iv + 0].SetData(knot.point.m_Pos - halfExtent);
        m_AllVertices[iv + 1].SetData(knot.point.m_Pos + halfExtent);
      }
      break;

    // A tetrahedron of "radius" size/2
    // TODO: compute a parallel transport frame and use that to orient the tetrahedron?
    case KnotConversion.Tetrahedron: {
      for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot) {
        Knot k = m_knots[iKnot];
        float halfSize = 0.5f * PressuredSize(k.point.m_Pressure);
        float h = halfSize / Mathf.Sqrt(3f);  // half-extent
        int iv0 = iKnot * verticesPerKnot;
        m_AllVertices[iv0 + 0].SetData(k.point.m_Pos + k.point.m_Orient * new Vector3(-h, -h, -h));
        m_AllVertices[iv0 + 1].SetData(k.point.m_Pos + k.point.m_Orient * new Vector3(+h, +h, -h));
        m_AllVertices[iv0 + 2].SetData(k.point.m_Pos + k.point.m_Orient * new Vector3(+h, -h, +h));
        m_AllVertices[iv0 + 3].SetData(k.point.m_Pos + k.point.m_Orient * new Vector3(-h, +h, +h));
      }
      break;
    }

    // An octahedron of "radius" size/2
    case KnotConversion.Octahedron: {
      for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot) {
        Knot knot = m_knots[iKnot];
        float halfSize = 0.5f * PressuredSize(knot.point.m_Pressure);

        int iv = iKnot * verticesPerKnot;
        for (int axis = 0; axis < 3; ++axis) {
          Vector3 offset = Vector3.zero;
          offset[axis] = halfSize;
          offset = knot.point.m_Orient * offset;
          m_AllVertices[iv++].SetData(knot.point.m_Pos + offset);
          m_AllVertices[iv++].SetData(knot.point.m_Pos - offset);
        }
      }
      break;
    }

    // A cube whose faces/edges are size units big.
    // This feels more natural than making size the "radius".
    case KnotConversion.Cube: {
      for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot) {
        Knot knot = m_knots[iKnot];
        float halfSize = 0.5f * PressuredSize(knot.point.m_Pressure);

        int iv = iKnot * verticesPerKnot;
        for (float xm = -1; xm <= 1; xm += 2)
        for (float ym = -1; ym <= 1; ym += 2)
        for (float zm = -1; zm <= 1; zm += 2) {
          Vector3 offset = new Vector3(xm * halfSize, ym * halfSize, zm * halfSize);
          m_AllVertices[iv++].SetData(knot.point.m_Pos + knot.point.m_Orient * offset);
        }
      }
      break;
    }
    }
  }

  void OnChanged_MakeGeometry(int iKnot0) {
    int verticesPerKnot = GetNumVerticesPerKnot();
    int knotsInHull = Mathf.Max(m_KnotsInHull, 1);
    // Laziness. It's easier to update if we have lvalues.
    for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot) {
      Knot cur = m_knots[iKnot];

      if (iKnot > 0) {
        Knot prev = m_knots[iKnot-1];
        cur.iVert = (ushort)(prev.iVert + prev.nVert);
        cur.iTri = prev.iTri + prev.nTri;
      } else {
        cur.iVert = 0;
        cur.iTri = 0;
      }
      cur.nVert = 0;
      cur.nTri = 0;

      // Attempt to create hull. It can fail if the dimensionality is too low
      // because of too few points; and maybe also if the points are collinear/coplanar.

      {
        // Add vertices from the half-open knot range [knotRange0, knotRange1)
        int knotRange0 = Mathf.Max(0, iKnot + 1 - knotsInHull);
        int knotRange1 = iKnot + 1;
        List<Vertex> input = m_AllVertices.GetRange(
            verticesPerKnot * knotRange0,
            verticesPerKnot * (knotRange1 - knotRange0));
        ConvexHull<Vertex, Face> hull = CreateHull(input, enableSimplify: false);
        if (hull != null) {
          if (m_Faceted) {
            CreateFacetedGeometry(ref cur, hull);
          } else {
            CreateSmoothGeometry(ref cur, hull);
          }
        }
      }

      m_knots[iKnot] = cur;
    }
  }

  ConvexHull<Vertex, Face> CreateHull(List<Vertex> input, bool enableSimplify) {
    if (input.Count < 3) {
      return null;
    }

    try {
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

  void CreateFacetedGeometry(ref Knot knot, ConvexHull<Vertex, Face> hull) {
    m_geometry.NumVerts = knot.iVert;
    m_geometry.NumTriIndices = knot.iTri * 3;

    foreach (var face in hull.Faces) {
      Vertex[] faceVerts = face.Vertices;

      int v0 = knot.iVert + knot.nVert;
      foreach (var vertex in faceVerts) {
        AppendVert(ref knot, AsVector3(vertex.Position), AsVector3(face.Normal));
      }

      // Tesselate the polygon into a fan
      int numFan = faceVerts.Length - 2;
      for (int iFan = 0; iFan < numFan; ++iFan) {
        AppendTri(ref knot, v0, v0 + (iFan + 1), v0 + (iFan + 2));
      }
    }
  }

  void CreateSmoothGeometry(ref Knot knot, ConvexHull<Vertex, Face> hull) {
    m_geometry.NumVerts = knot.iVert;
    m_geometry.NumTriIndices = knot.iTri * 3;

    Vertex[] hullPoints = hull.Points.ToArray();

    foreach (var vertex in hullPoints) {
      vertex.TempNormal = Vector3.zero;
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
    }

    foreach (var vertex in hullPoints) {
      vertex.TempIndex = knot.iVert + knot.nVert;
      AppendVert(ref knot, AsVector3(vertex.Position), vertex.TempNormal.normalized);
    }

    foreach (var face in hull.Faces) {
      Vertex[] vs = face.Vertices;
      // Tesselate the polygon into a fan
      int numFan = vs.Length - 2;
      for (int iFan = 0; iFan < numFan; ++iFan) {
        AppendTri(ref knot, vs[0].TempIndex, vs[1].TempIndex, vs[2].TempIndex);
        AppendTri(ref knot, vs[0].TempIndex, vs[2].TempIndex, vs[1].TempIndex);
      }
    }
  }

  void AppendVert(ref Knot k, Vector3 v, Vector3 n) {
    if ((k.iVert + k.nVert != m_geometry.m_Vertices.Count)) {
      Debug.Assert(false);
    }
    m_geometry.m_Vertices .Add(v);
    m_geometry.m_Normals  .Add(n);
    m_geometry.m_Colors   .Add(m_Color);
    k.nVert += 1;
    if (m_bDoubleSided) {
      m_geometry.m_Vertices .Add(v);
      m_geometry.m_Normals  .Add(-n);
      // TODO: backface is a different color for visualization reasons
      // Probably better to use a non-culling shader instead of doubling the geo.
      m_geometry.m_Colors.Add(m_Color);
      k.nVert += 1;
    }
  }

  /// vp{0,1,2} are indices of vertex pairs
  void AppendTri(ref Knot k, int vp0, int vp1, int vp2) {
    if ((k.iTri + k.nTri) * 3 != m_geometry.m_Tris.Count) {
      Debug.Assert(false);
    }
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
