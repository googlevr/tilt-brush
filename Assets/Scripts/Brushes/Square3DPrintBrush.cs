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
  /// Brush generates a manifold, 3D printable "tube" with a rounded square cross section
  class Square3DPrintBrush : GeometryBrush {

    const float kRingDenseDistanceMeters_LS = 0.005f;
    const float kRingSparseDistanceMeters_LS = 0.1f;
    const float kMaxCapForwardRatio = 0.01f;
    const float kMinDistKnotsMeters_LS = 0.003f;
    const float kSwingBreakValue = 0.940f;            // 20 degrees
    const float kTwistBreakValue = 0.940f;            // 20 degrees
    const float kIndicatorPlaneBreakValue = 0.0087f;  // 89.5 degrees
    const int kNumCapVerts = 4;
    const int kMaxBevelVerts = 10;

    /// Defaults defined as a reminder of brush's capabilities
    const float kDefaultThickness = 0.2f;  // Unused, thickness substituted with size (square CS)
    const float kDefaultBevelSize = 0.01f;
    const int kDefaultBevelVerts = 2;
    const float kDefaultTesselation = 1;
    const byte kDefaultTransparency = 255;

    public Square3DPrintBrush() : this(true) { }

    /// bevelRatio is the ratio of the distance to the inset edge from the origin
    /// and the distance to the outer edge from the origin, in either the rt or up
    /// direction.
    /// bevelRatio is a float in [0, 1]
    /// bevel size increases as bevelRatio decreases,
    /// bevelRatio = 0 produces a diamond, bevelRatio = 1 produces no bevel, a rect
    private float m_bevelRatio = 0.99f;
    /// Controls smoothness; must be in [1, kMaxBevelVerts]
    [Range(1, kMaxBevelVerts)]
    private int m_bevelVerts = 2;
    private int m_vertsPerRing { get { return 4 * m_bevelVerts; } }
    /// Interpolation parameter between large and small ring distances in [0,1].
    /// Higher m_tessellation means denser geometry and vice versa.
    private float m_tessellation = 1f;
    /// Alpha value of color assigned to vertices.
    [Range(0, 255)]
    private byte m_transparency = 255;
    /// Should be used only for debugging purposes.
    [SerializeField] bool m_debugShowSurfaceOrientation = false;

    /*
    FIGURE 1 - Elements of a stroke

    Take "n" as "m_bevelVerts" and "*" as symbolically denoting the vertices in
    a bevel. Strokes are comprised of 4n-vert rings and 4-vert caps as seen below.
    Their vertex numberings increase clockwise wrt stroke fwd direction.
                  ________________
    [i+2n, i+3n) *                * [i+3n, i+4n)   j+2 .____________. j+3
                |                  |                   |            |
                |                  |               j+1.|____________|.j+0
    [i+1n, i+2n) *________________* [i+0n, i+1n)

    FIGURE 2 - Knot element combinations

    A knot will have one of the following combinations of rings and caps added to it.
      - no triangles are shared between knots.
      - when start is true, flip must be false. (those cases where flip is true are omitted)

    CASE    CONDITION                 KNOT'S ELEMENTS
     1   |    start, !end, !flip   |   cap  --> ring --> ring
     2   |    start,  end, !flip   |   cap  --> ring --> ring --> cap
     3   |   !start, !end, !flip   |   ring --> ring
     4   |   !start,  end, !flip   |   ring --> cap
     5   |   !start, !end,  flip   |   ring --> ring --> ring
     6   |   !start,  end,  flip   |   ring --> ring --> ring --> cap

    */

    // Set of vectors used to define the geometry in a knot.
    private class GeometryBasis {

      public readonly Vector3 nStrokeTangent;  // Unit vector in direction of stroke fwd

      // true if stroke tangent direction and indicator plane normal are in the
      // "same general direction", or heuristically, closer to parallel then anti-parallel.
      public readonly bool strokeInlineWithPlaneNormal;

      // This knot's geometry is defined by three vectors (width, thickness, csnormal) that are:
      //  - unit length
      //  - orthogonal to each other
      //  - oriented such that "width cross thickness = csnormal" (rh cross product)

      // Orthogonal to cross section, towards (in "same general direction" as) strokeTangent
      public readonly Vector3 nCrossSectionNormal;
      // Lies in cross section, defines direction of width, parallel to knot's m_Orient right
      public readonly Vector3 nCrossSectionTangentWidth;
      // Lies in cross section, defines direction of thickness
      public readonly Vector3 nCrossSectionTangentThickness;

      public readonly Vector3 widthVectorToEdge;       // Centre to edge in dir of width
      public readonly Vector3 thicknessVectorToEdge;   // Centre to edge in dir of thickness
      public readonly Vector3 widthVectorToBevel;      // Centre to inset edge in dir of width
      public readonly Vector3 thicknessVectorToBevel;  // Centre to inset edge in dir of thickness
      public readonly Vector3 capNormalOffset;         // Cap offset in dir of cross section normal


      public GeometryBasis(Knot knot, Square3DPrintBrush brushInfo,
                           Vector3? manuallySetStrokeTangent = null) {

        nStrokeTangent = manuallySetStrokeTangent != null ?
                        (Vector3)manuallySetStrokeTangent : knot.qFrame * Vector3.forward;

        // Indicator Plane is defined by tangents m_Orient-rt, m_Orient-fwd w/ normal m_Orient-up.
        Vector3 indicatorPlaneTangentRt = knot.point.m_Orient * Vector3.right;
        Vector3 indicatorPlaneTangentFwd = knot.point.m_Orient * Vector3.forward;
        Vector3 indicatorPlaneNormal = knot.point.m_Orient * Vector3.up;

        strokeInlineWithPlaneNormal = Vector3.Dot(nStrokeTangent, indicatorPlaneNormal) > 0;

        // The stroke cross section occupies the same area as the indicator plane but is oriented
        // towards (is in "same general direction" as) the strokeTangent.
        if (strokeInlineWithPlaneNormal) {
          nCrossSectionNormal = indicatorPlaneNormal;
          nCrossSectionTangentWidth = indicatorPlaneTangentRt;
          nCrossSectionTangentThickness = -indicatorPlaneTangentFwd;  // rh system
        } else {
          nCrossSectionNormal = -indicatorPlaneNormal;
          nCrossSectionTangentWidth = indicatorPlaneTangentRt;
          nCrossSectionTangentThickness = indicatorPlaneTangentFwd;
        }

        float halfWidth = brushInfo.PressuredSize(knot.smoothedPressure) * 0.5f;
        float halfThickness = brushInfo.PressuredSize(knot.smoothedPressure) * 0.5f;

        widthVectorToEdge = halfWidth * nCrossSectionTangentWidth;
        widthVectorToBevel = widthVectorToEdge * brushInfo.m_bevelRatio;
        thicknessVectorToEdge = halfThickness * nCrossSectionTangentThickness;
        thicknessVectorToBevel = thicknessVectorToEdge * brushInfo.m_bevelRatio;

        capNormalOffset = nStrokeTangent * Mathf.Min(1 - brushInfo.m_bevelRatio,
                                                    kMaxCapForwardRatio);
      }
    }

    public Square3DPrintBrush(bool bCanBatch)
    : base(bCanBatch: bCanBatch,
            upperBoundVertsPerKnot: 2 * 4 * kMaxBevelVerts,
            bDoubleSided: false) {
    }

    protected override void InitBrush(BrushDescriptor desc, TrTransform localPointerXf) {
      base.InitBrush(desc, localPointerXf);
      m_geometry.Layout = GetVertexLayout(desc);

      PointerScript ptr = PointerManager.m_Instance.MainPointer;

      m_bevelRatio = 1 - kDefaultBevelSize;
      m_bevelVerts = kDefaultBevelVerts;
      m_tessellation = kDefaultTesselation;
      m_transparency = kDefaultTransparency;
    }

    protected override void ControlPointsChanged(int iKnot0) {
      // Updating a control point affects geometry generated by previous knot
      // (if there is any). The HasGeometry check is not a micro-optimization:
      // it also keeps us from backing up past knot 0.
      int start = (m_knots[iKnot0 - 1].HasGeometry) ? iKnot0 - 1 : iKnot0;

      // Frames knots, determines how much geometry each knot should get.
      if (OnChanged_FrameKnots(start)) {
        // If we were notified that the beginning knot turned into a break, step back a knot.
        // Note that OnChanged_MakeGeometry requires our specified knot has a previous.
        start = Mathf.Max(1, start - 1);
      }
      OnChanged_MakeGeometry(start);
      ResizeGeometry();
    }

    bool OnChanged_FrameKnots(int iKnot0) {
      bool initialKnotContainsBreak = false;

      Knot prev = m_knots[iKnot0 - 1];
      for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot) {
        Knot cur = m_knots[iKnot];

        Vector3 vStroke = cur.smoothedPos - prev.smoothedPos;
        cur.length = vStroke.magnitude;

        Vector3 prevPlaneNormal = prev.point.m_Orient * Vector3.up;
        Vector3 prevPlaneTangent = prev.point.m_Orient * Vector3.right;
        Vector3 curPlaneNormal = cur.point.m_Orient * Vector3.up;
        Vector3 curPlaneTangent = cur.point.m_Orient * Vector3.right;

        Vector3 curStrokeTangent = vStroke / cur.length;

        // Measure of similarity between current stroke tangent and plane normal
        float curNormalStrokeAlignment = Vector3.Dot(curStrokeTangent, curPlaneNormal);

        bool inPlane = Mathf.Abs(curNormalStrokeAlignment) < kIndicatorPlaneBreakValue;
        bool closeToPrev = cur.length < kMinDistKnotsMeters_LS * App.METERS_TO_UNITS;
        bool largeSwing = false;  // Rotation around pointer forward/back axis
        bool largeTwist = false;  // Rotation around pointer up/down axis
        if (prev.HasGeometry) {
          // Decompose rotation from prev to cur knot as swing and twist
          largeSwing = Vector3.Dot(prevPlaneNormal, curPlaneNormal) < kSwingBreakValue;
          largeTwist = Vector3.Dot(prevPlaneTangent, curPlaneTangent) < kTwistBreakValue;
        }

        if (largeSwing || largeTwist || closeToPrev || inPlane) {
          if (iKnot == iKnot0) {
            initialKnotContainsBreak = true;
          }
          cur.qFrame = new Quaternion(0, 0, 0, 0);
          cur.nRight = cur.nSurface = Vector3.zero;
          cur.nTri = cur.nVert = 0;
        } else {
          // Purely used to store stroke-forward direction
          cur.qFrame = Quaternion.LookRotation(curStrokeTangent);
          cur.nRight = Vector3.zero;
          cur.nSurface = Vector3.zero;
          cur.nTri = cur.nVert = 1;
        }

        m_knots[iKnot] = cur;
        prev = cur;
      }
      return initialKnotContainsBreak;
    }

    void OnChanged_MakeGeometry(int iKnot0) {
      m_Color.a = m_transparency;
      Knot prev = m_knots[iKnot0 - 1];

      for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot) {
        Knot cur = m_knots[iKnot];
        cur.iTri = prev.iTri + prev.nTri;
        cur.iVert = (ushort)(prev.iVert + prev.nVert);

        if (cur.HasGeometry) {
          cur.nVert = cur.nTri = 0;
          GeometryBasis curBasis = new GeometryBasis(cur, this);

          bool isStart = !prev.HasGeometry;
          bool isEnd = IsPenultimate(iKnot);
          bool isFlip = AlignmentParityReverses(ref cur, ref prev);

          // Stroke starting, see CASE 1
          if (isStart) {
            int startCap = 0;
            int ring0 = startCap + kNumCapVerts;

            // Add a start cap at the prev knot's pos
            AddStartCapVerts(ref cur, prev.smoothedPos, curBasis);
            AddStartCapTris(ref cur, startCap);

            // Add a ring at the prev knot's pos
            AddRingVerts(ref cur, prev.smoothedPos, curBasis);
            AddCapToRingTris(ref cur, ring0, startCap);

            // Add a ring at the cur knot's pos
            AddRingVerts(ref cur, cur.smoothedPos, curBasis);
            AddMiddleRingTris(ref cur, ring0);

            // Stroke starting and ending, see CASE 2
            if (isEnd) {
              int ring1 = ring0 + m_vertsPerRing;
              int endCap = ring1 + m_vertsPerRing;

              // Add an end cap at the cur knot's pos
              AddEndCapVerts(ref cur, cur.smoothedPos, curBasis);
              AddCapToRingTris(ref cur, ring1, endCap);
              AddEndCapTris(ref cur, endCap);
            }
          } else {  // !start
            int sharedRing = 0;

            // Add a ring at the prev knot's pos, albeit virutally (by rewinding to last ring)
            cur.iVert -= (ushort)m_vertsPerRing;
            cur.nVert += (ushort)m_vertsPerRing;

            // Stroke continuing and flipping, see CASE 5
            if (isFlip) {
              GeometryBasis prevBasisCurStrokeTangent = new GeometryBasis(
                  prev, this, manuallySetStrokeTangent: curBasis.nStrokeTangent);

              // Close off end-face of approaching segment
              AddRingFaceTris(ref cur, sharedRing, true);

              // Add a degenerate ring at the prev knot's pos in the cur orientation
              AddRingVerts(ref cur, prev.smoothedPos, prevBasisCurStrokeTangent);
              int ring0 = sharedRing + m_vertsPerRing;

              // Add a ring at the cur knot's pos in the cur orientation (a normal ring)
              AddRingVerts(ref cur, cur.smoothedPos, curBasis);
              AddMiddleRingTris(ref cur, ring0);

              // Stroke ending, see CASE 6
              if (isEnd) {
                int ring1 = ring0 + m_vertsPerRing;
                int endCap = ring1 + m_vertsPerRing;

                // Add an end cap at the cur knot's pos
                AddEndCapVerts(ref cur, cur.smoothedPos, curBasis);
                AddCapToRingTris(ref cur, ring1, endCap);
                AddEndCapTris(ref cur, endCap);
              }

              // Stroke continuing, see CASE 3
            } else {  // !flip

              // Add a ring at the cur knot's pos
              AddRingVerts(ref cur, cur.smoothedPos, curBasis);
              AddMiddleRingTris(ref cur, sharedRing);

              // Stroke ending, see CASE 4
              if (isEnd) {
                int ring0 = sharedRing + m_vertsPerRing;
                int endCap = ring0 + m_vertsPerRing;

                // Add an end cap at the cur knot's pos
                AddEndCapVerts(ref cur, cur.smoothedPos, curBasis);
                AddCapToRingTris(ref cur, ring0, endCap);
                AddEndCapTris(ref cur, endCap);
              }
            }
          }
        }
        m_knots[iKnot] = cur;
        prev = cur;
      }
    }

    // VERTS

    void AddStartCapVerts(ref Knot cur, Vector3 pos, GeometryBasis gb) {
      AppendVertSquare(
          ref cur, pos + gb.widthVectorToBevel - gb.thicknessVectorToBevel - gb.capNormalOffset,
          m_Color);
      AppendVertSquare(
          ref cur, pos - gb.widthVectorToBevel - gb.thicknessVectorToBevel - gb.capNormalOffset,
          m_Color);
      AppendVertSquare(
          ref cur, pos - gb.widthVectorToBevel + gb.thicknessVectorToBevel - gb.capNormalOffset,
          m_Color);
      AppendVertSquare(
          ref cur, pos + gb.widthVectorToBevel + gb.thicknessVectorToBevel - gb.capNormalOffset,
          m_Color);
    }

    void AddEndCapVerts(ref Knot cur, Vector3 pos, GeometryBasis gb) {
      AppendVertSquare(
          ref cur, pos + gb.widthVectorToBevel - gb.thicknessVectorToBevel + gb.capNormalOffset,
          m_Color);
      AppendVertSquare(
          ref cur, pos - gb.widthVectorToBevel - gb.thicknessVectorToBevel + gb.capNormalOffset,
          m_Color);
      AppendVertSquare(
          ref cur, pos - gb.widthVectorToBevel + gb.thicknessVectorToBevel + gb.capNormalOffset,
          m_Color);
      AppendVertSquare(
          ref cur, pos + gb.widthVectorToBevel + gb.thicknessVectorToBevel + gb.capNormalOffset,
          m_Color);
    }

    void AddRingVerts(ref Knot cur, Vector3 pos, GeometryBasis gb) {
      Color32 c1 = m_Color;
      Color32 c2 = m_Color;
      if (m_debugShowSurfaceOrientation) {
        c1 = Color.blue;
        c2 = Color.red;
      }
      AddBevelVerts(ref cur, pos, 360f, 270f, gb, c1);
      AddBevelVerts(ref cur, pos, 270f, 180f, gb, c1);
      AddBevelVerts(ref cur, pos, 180f, 90f, gb, c2);
      AddBevelVerts(ref cur, pos, 90f, 0f, gb, c2);
    }

    // Adds vertices to a specified bevel (corner) based on number of bevelSegments
    void AddBevelVerts(ref Knot cur, Vector3 pos, float startAngle, float stopAngle,
                       GeometryBasis gb, Color32 c) {
      float midAngle = (startAngle + stopAngle) / 2;
      Vector3 bevelOrigin =
          pos
          + Mathf.Sign(Mathf.Cos(midAngle * Mathf.Deg2Rad)) * gb.widthVectorToBevel
          + Mathf.Sign(Mathf.Sin(midAngle * Mathf.Deg2Rad)) * gb.thicknessVectorToBevel;

      // Distance between outer and inset edges
      float rtInsetOuterDist = (gb.widthVectorToEdge - gb.widthVectorToBevel).magnitude;
      float upInsetOuterDist = (gb.thicknessVectorToEdge - gb.thicknessVectorToBevel).magnitude;

      for (int i = 0; i < m_bevelVerts; i++) {
        float dt = 1f / (m_bevelVerts - 1);
        // in BevelVerts = 1 case, cannot place a vert at startAngle and stopAngle,
        // so opt for a single vert (angle-wise) equally between them.
        float t = (m_bevelVerts == 1) ? (.5f) : (i * dt);
        Vector3 offset = EllipseOffset(gb.nCrossSectionTangentWidth, rtInsetOuterDist,
                                       gb.nCrossSectionTangentThickness, upInsetOuterDist,
                                       Mathf.Lerp(startAngle, stopAngle, t));
        AppendVertSquare(ref cur, bevelOrigin + offset, c);
      }
    }

    // TRIS
    // See FIGURE 2 for details

    void AddStartCapTris(ref Knot cur, int cap) {
      AppendTri(ref cur, cap + 2, cap + 3, cap + 1);
      AppendTri(ref cur, cap + 1, cap + 3, cap + 0);
    }

    void AddEndCapTris(ref Knot cur, int cap) {
      AppendTri(ref cur, cap + 1, cap + 0, cap + 2);
      AppendTri(ref cur, cap + 2, cap + 0, cap + 3);
    }

    // "cw" indicates if triangle orientation goes clockwise wrt stroke fwd
    void AddRingFaceTris(ref Knot cur, int ring, bool cw) {
      if (cw) {
        for (int i = 2; i < m_vertsPerRing; i++) {
          AppendTri(ref cur, i + ring, i - 1 + ring, ring);
        }
      } else {
        for (int i = 1; i < m_vertsPerRing - 1; i++) {
          AppendTri(ref cur, i + ring, i + 1 + ring, ring);
        }
      }
    }

    void AddMiddleRingTris(ref Knot cur, int ring) {
      // Adds quads across the two rings
      for (int i = 0; i < m_vertsPerRing; i++) {
        int i0 = i + ring;
        int i1 = (i + 1) % m_vertsPerRing + ring;  // clockwise-adjacent vert to i0
        int j0 = i0 + m_vertsPerRing;              // i0's corresponding vert on next ring
        int j1 = i1 + m_vertsPerRing;              // i1's coorrespoding vert on next ring
        AppendQuad(ref cur, i1, i0, j0, j1);
      }
    }

    // Assume rings have "reversed" numbering, that is:
    //  - ring1 winds the opposite direction of ring0 (cw -> ccw and ccw -> cw)
    //  - ring0's first vertex is "across" from ring1's last vertex
    //  - ring0's last vertex is "across" from ring1's first vertex
    void AddMiddleRingTrisAcrossFlip(ref Knot cur, int ring) {
      int lastVertFlippedRing = ring + 2 * m_vertsPerRing - 1;
      int firstVertFlippedRing = ring + 1 * m_vertsPerRing;
      for (int i = 0; i < m_vertsPerRing; i++) {
        int i0 = i + ring;
        int i1 = (i + 1) % m_vertsPerRing + ring;  // clockwise-adjacent vert to i0
        int j0 = lastVertFlippedRing - i;          // i0's corresponding vert on next ring
        int j1 = j0 - 1;                           // i1's corresponding vert on next ring
        j1 = (j1 < firstVertFlippedRing) ? lastVertFlippedRing : j1;  // Wrap j1 to last vert
        AppendQuad(ref cur, i0, i1, j1, j0);
      }
    }

    void AddCapToRingTris(ref Knot cur, int ring, int cap) {
      // Check if rings are further forward (higher starting index) than cap's
      bool starting = ring > cap;
      int n = m_bevelVerts;
      int numCorners = 4;
      for (int i = 0; i < numCorners; i++) {
        int inner = cap + i;
        int fanStart0 = ring + i * n;
        int fanEnd0 = fanStart0 + (n - 1);
        int inner1 = cap + (i + 1) % numCorners;
        int fanStart1 = ring + (i + 1) % numCorners * n;
        if (starting) {  // stitch start cap to first ring
          AppendFan(ref cur, inner, fanStart0, fanEnd0);
          AppendQuad(ref cur, inner, fanEnd0, fanStart1, inner1);
        } else {         // stitch last ring to end cap
          AppendFan(ref cur, inner, fanEnd0, fanStart0);
          AppendQuad(ref cur, fanEnd0, inner, inner1, fanStart1);
        }
      }
    }

    // Takes quad verts v0, v1, v2, v3 arranged clockwise as seen from the front.
    void AppendQuad(ref Knot cur, int v0, int v1, int v2, int v3) {
      AppendTri(ref cur, v0, v1, v3);
      AppendTri(ref cur, v3, v1, v2);
    }

    // Creates a fan of triangles by:
    //   - setting rivet at vert "pivot"
    //   - setting leaves at contiguous vert pairs in range ["start", "end"]
    // Verts are specified clockwise as seen from the front.
    // ["start", "end"] can be increasing or decreasing ("start" > "end").
    // "start" == "end" indicates a degenerate fan and no geometry added.
    void AppendFan(ref Knot cur, int pivot, int start, int end) {
      int numTris = Mathf.Abs(end - start);
      int dv = end > start ? 1 : -1;
      for (int i = 0; i < numTris; i++) {
        int v0 = start + i * dv;
        int v1 = v0 + dv;
        AppendTri(ref cur, pivot, v0, v1);
      }
    }

    // rt, up - define the ellipse plane. They are orthogonal and unit length.
    // halfRt, halfUp - length of ellipse half-axes in the right and up directions.
    // theta - ellipse angle in degrees. rt is theta=0. up is theta=90.
    static Vector3 EllipseOffset(Vector3 rt, float halfRt, Vector3 up, float halfUp,
                                 float theta) {
      return halfRt * Mathf.Cos(Mathf.Deg2Rad * theta) * rt
           + halfUp * Mathf.Sin(Mathf.Deg2Rad * theta) * up;
    }

    // Custom GetVertexLayout() based on TubeBrush.cs's without UVs or tangents
    override public GeometryPool.VertexLayout GetVertexLayout(BrushDescriptor desc) {
      return new GeometryPool.VertexLayout {
        bUseColors = true,
        bUseNormals = false,
        bUseTangents = false,
        uv0Size = 0,
        uv1Size = 0
      };
    }

    // Simplified version of AppendVert() that does not require tangents or UVs
    // Increments Knot k's nVert count by 1 and adds a new vert with corresponding
    // information (color) to GeometryPool m_geometry
    void AppendVertSquare(ref Knot k, Vector3 v, Color32 c) {
      int i = k.iVert + k.nVert++;
      if (i == m_geometry.m_Vertices.Count) {
        m_geometry.m_Vertices.Add(v);
        m_geometry.m_Colors.Add(c);
      } else {
        m_geometry.m_Vertices[i] = v;
        m_geometry.m_Colors[i] = c;
      }
    }

    // Sets control points and hence rings, at a minimum distance in local space
    override public float GetSpawnInterval(float pressure01) {
      float ringDistMetres_LS = Mathf.Lerp(kRingSparseDistanceMeters_LS,
                                           kRingDenseDistanceMeters_LS, m_tessellation);
      // Prevents artefacts caused by VR sensor accuracy limits.
      float minKnotDistMetres_PS = 0.001f;
      // Prevents inverted geometry from being created at small scale perspectives.
      float maxKnotDistMetres_PS = 0.05f;
      float ringDistMin_LS = minKnotDistMetres_PS * POINTER_TO_LOCAL * App.METERS_TO_UNITS;
      float ringDistMax_LS = maxKnotDistMetres_PS * POINTER_TO_LOCAL * App.METERS_TO_UNITS;
      float ringDist_LS = ringDistMetres_LS * App.METERS_TO_UNITS;
      return Mathf.Clamp(ringDist_LS, ringDistMin_LS, ringDistMax_LS);
    }

    protected void AppendTri(ref Knot k, int t0, int t1, int t2) {
      int i = (k.iTri + k.nTri++) * 3;
      if (i == m_geometry.m_Tris.Count) {
        m_geometry.m_Tris.Add(k.iVert + t0);
        m_geometry.m_Tris.Add(k.iVert + t1);
        m_geometry.m_Tris.Add(k.iVert + t2);
      } else {
        m_geometry.m_Tris[i + 0] = k.iVert + t0;
        m_geometry.m_Tris[i + 1] = k.iVert + t1;
        m_geometry.m_Tris[i + 2] = k.iVert + t2;
      }
    }

    protected bool IsPenultimate(int iKnot) {
      return (iKnot + 1 == m_knots.Count || !m_knots[iKnot + 1].HasGeometry);
    }

    // Evaluates if the previous knot's alignment parity is opposite the current's.
    bool AlignmentParityReverses(ref Knot cur, ref Knot prev) {
      Vector3 IndicatorPlaneNormalPrev = prev.point.m_Orient * Vector3.up;
      Vector3 nStrokeTangentPrev = prev.qFrame * Vector3.forward;

      Vector3 IndicatorPlaneNormalCur = cur.point.m_Orient * Vector3.up;
      Vector3 nStrokeTangentCur = cur.qFrame * Vector3.forward;

      bool prevStrokeInlineWithPlaneNormal =
          Vector3.Dot(IndicatorPlaneNormalPrev, nStrokeTangentPrev) > 0;
      bool curStrokeInlineWithPlaneNormal =
          Vector3.Dot(IndicatorPlaneNormalCur, nStrokeTangentCur) > 0;

      return prevStrokeInlineWithPlaneNormal ^ curStrokeInlineWithPlaneNormal;
    }
  }
}  // namespace TiltBrush