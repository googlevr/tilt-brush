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

// COORDINATE SYSTEMS
//
// Compared to the rest of Tilt Brush, BaseBrushScript and its subclasses
// use a simplified set of coordinate systems. Instead of Scene, Canvas,
// and Room, brushes only need to worry about their parent's local coordinate
// system and the coordinate system of the Pointer (the tool which is drawing
// the stroke). tldr: Local ~= Canvas and Pointer ~= Room.
//
// POINTER COORDINATES
//
// Brushes only care about the Pointer coordinate system for scale-invariance: a
// stroke drawn on a shrunk-down canvas should generate the same geometry as that
// same stroke drawn on an unscaled canvas. This is easy -- do everything in
// local space. However, we also want a way to independently alter the geometry
// density:
//
// - A long stroke drawn on a very scaled-down canvas would generate an
//   unnecessarily dense amount of geometry. Practically speaking, we want
//   geometry density to be specified in room space, not canvas space.
//
// - We want to be able to move strokes between canvases of different scales
//   without regenerating geometry. Or put another way, when redrawn in its new
//   canvas, the stroke should generate the same geometry.
//
// We do this by paying attention to the relative scale between Room (ie Pointer)
// and Canvas (ie Local).  If the user is a dinosaur relative to the canvas, the
// line density is lowered; if they are a mouse, the line density is increased.
//
// The suffix "_PS" indicates a distance or velocity measured in the pointer
// coordinate system. Lack of suffix implies the local coordinate system.
//
// TODO: we could probably simplify this so everything is in Canvas, by
// replacing everything relating to Pointer space with a "density" parameter.
// That's essentially how things work now, except "density" is encoded as
// 1/m_LastSpawnXf.scale -- which we assume never changes, even though the
// translation and rotation do change.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace TiltBrush {

public abstract class BaseBrushScript : MonoBehaviour {
#region Static public
  public const float kPreviewDuration = 0.2f; // Must be > 0 for the particles shader to work.

  /// Creates and properly initializes a new line.
  /// Pass the initial transform in parent-local (Canvas) space.
  /// Pass the size in pointer (Room) space.
  public static BaseBrushScript Create(
      Transform parent,
      TrTransform xfInParentSpace,
      BrushDescriptor desc, Color color, float size_PS) {
    GameObject line = Instantiate(desc.m_BrushPrefab);
    line.transform.SetParent(parent);
    Coords.AsLocal[line.transform] = TrTransform.identity;
    line.name = desc.m_Description;

    BaseBrushScript currentLine = line.GetComponent<BaseBrushScript>();
    // TODO: pass this into InitBrush and do it there
    currentLine.m_Color = color;
    currentLine.m_BaseSize_PS = size_PS;
    currentLine.InitBrush(desc, xfInParentSpace);
    return currentLine;
  }

  // used for batched strokes. not overridable
  public static float GetStrokeCost(BrushDescriptor desc, int verts, float size) {
    return (verts / 6)
           * QualityControls.m_Instance.AppQualityLevels.GetWeightForBrush(desc.m_Guid)
           * size;
  }
#endregion

  readonly public bool m_bCanBatch;

  // Brush descriptor; immutable; never changes
  protected BrushDescriptor m_Desc;
  protected bool m_EnableBackfaces = false;
  protected bool m_PreviewMode = false;
  // True if the control points are coming from a file, as opposed to user input.
  // This is currently useful for debugging, but for determinism we may want to
  // always accept CPs when IsLoading.
  [JetBrains.Annotations.UsedImplicitly]
  protected bool m_IsLoading = false;
  protected Color32 m_Color;
  protected TrTransform m_LastSpawnXf;
  protected Vector3 m_LastSpawnPos { get { return m_LastSpawnXf.translation; } }
  protected float m_BaseSize_PS;
  protected StatelessRng m_rng;

  protected BaseBrushScript(bool bCanBatch) {
    m_bCanBatch = bCanBatch;
  }

#region Accessors
  /// The size of the pointer when it created the stroke.
  /// Larger pointers create larger lines with a lower density of control points.
  /// This can also be thought of as the "Pointer to Local" scale factor
  public float StrokeScale {
    get { return m_LastSpawnXf.scale; }
  }

  /// Convert a distance/velocity from local to pointer coordinates
  public float LOCAL_TO_POINTER {
    get { return 1f / m_LastSpawnXf.scale; }
  }

  /// Convert a distance/velocity from pointer to local coordinates (ie Room to Canvas).
  public float POINTER_TO_LOCAL {
    get { return m_LastSpawnXf.scale; }
  }

  /// The size of the brush, in the pointer (ie Room) coordinate system.
  public float BaseSize_PS {
    get { return m_BaseSize_PS; }
    set { m_BaseSize_PS = value; }
  }

  /// The size of the brush, in the parent-local (Canvas) coordinate system
  public float BaseSize_LS {
    get { return m_BaseSize_PS * POINTER_TO_LOCAL; }
  }

  /// Canvas that this stroke is a part of.
  public CanvasScript Canvas {
    get {
      // Currently, all strokes are created directly under a Canvas node.
      // If that changes, we'll have to find a different way of inferring Canvas.
      return transform.parent.GetComponent<CanvasScript>();
    }
  }

  public BrushDescriptor Descriptor {
    get { return m_Desc; }
  }

  public Color CurrentColor {
    get { return m_Color; }
  }

  /// The setter should only be used during initialization.
  public int RandomSeed {
    get { return m_rng.Seed; }
    set { m_rng = new StatelessRng(value); }
  }

  public Stroke Stroke { get; set; }
#endregion

#region Initialization, Update, Destruction
  /// This should only be used during initialization.
  public void SetIsLoading() { m_IsLoading = true; }

  /// This should only be used during initialization.
  public void SetPreviewMode() { m_PreviewMode = true; }

  /// Returns an object that implements the Undo animation
  public GameObject CloneAsUndoObject() {
    GameObject clone = Instantiate(gameObject);
    clone.name = "Undo " + clone.name;
    clone.transform.parent = gameObject.transform.parent;
    Coords.AsLocal[clone.transform] = Coords.AsLocal[gameObject.transform];
    clone.SetActive(true);
    Destroy(clone.GetComponent<BaseBrushScript>());
    InitUndoClone(clone);
    return clone;
  }

  /// Returns true if permanent geometry was generated.
  /// Transform should be in the local coordinates of the stroke
  public bool UpdatePosition_LS(TrTransform xf, float fPressure) {
    if (IsOutOfVerts()) { return false; }

    bool ret = UpdatePositionImpl(xf.translation, xf.rotation, fPressure);
    if (ret) {
      m_LastSpawnXf = xf;
      // m_LastSpawnPressure = fPressure;
    }
    return ret;
  }

  // A subset of InitBrush() that modifies only those things that can be
  // safely changed after the brush has been created.
  // Only used by preview brushes.
  public void SetPreviewProperties(Color rColor, float fSize) {
    m_Color = rColor;
    m_BaseSize_PS = fSize;
    // TODO: do preview brushes really need this?
    GetComponent<Renderer>().material = m_Desc.Material;
  }

  public void DestroyMesh() {
    MeshFilter mf = GetComponent<MeshFilter>();
    if (mf != null) {
      Destroy(mf.mesh);
    }
  }
  #endregion

#region To override
  // Passed transform is relative to the stroke
  protected virtual void InitBrush(BrushDescriptor desc, TrTransform localPointerXf) {
    Debug.Assert(m_BaseSize_PS != 0, "Set size and color first");
    m_Desc = desc;
    GetComponent<Renderer>().material = m_Desc.Material;
    m_EnableBackfaces = desc.m_RenderBackfaces;
    m_rng = new StatelessRng(MathUtils.RandomInt());

    m_LastSpawnXf = localPointerXf;
  }

  // Called every frame, if AlwaysRebuildPreviewBrush()==true.
  // Also used for straightedge, so even if the subclass uses DecayBrush it _might_ still need this.
  // A toned-down version of InitBrush that initializes less stuff
  // Passed transform is relative to the stroke
  public virtual void ResetBrushForPreview(TrTransform localPointerXf) {
    m_LastSpawnXf = localPointerXf;
  }

  /// Returns the mesh vertex layout for this brush.
  /// The value returned may not change per brush descriptor.
  /// Non-geometric types will return a layout indicating no data requirements.
  public abstract GeometryPool.VertexLayout GetVertexLayout(BrushDescriptor desc);

  /// Take any stored-up changes (to geometry, particles, etc) and update
  /// a Mesh or other graphics resource. Guaranteed to be called on the
  /// render thread.
  public abstract void ApplyChangesToVisuals();

  public virtual void HideBrush(bool bHide) {
    gameObject.SetActive(!bHide);
  }

  // Subclass should return the amount of MasterBrush vert storage used.
  // Must always be <= MasterBrush.NumVerts
  public abstract int GetNumUsedVerts();

  // Return a distance, in distance-units, representing the desired distance between solids
  public abstract float GetSpawnInterval(float pressure01);

  /// Subclass should return true to rebuild preview every frame, false to use brush decay.
  /// If true, subclass must also implement ResetBrushForPreview()
  /// If false, subclass must also implement DecayBrush()
  ///
  /// Decay should be used by brushes...
  /// - which have state that should not be reset every frame. eg, particles keep a
  ///   particle creation timestamp in the vertex buffer, initialized to the current time
  /// - which have nondeterministic generation code. The previous example of timestamps
  ///   can be seen as a kind of nondeterminism.
  /// - which use randomness at the knot level, or are otherwise sensitive to knot indices
  ///   changing.
  ///
  /// Even when using DecayBrush(), brushes should take care about knot-based randomness.
  /// When preview strokes get into a steady state, new geometry will be continually generated
  /// at a fixed knot index, meaning that the same random numbers are continually reused.
  /// See GeniusParticlesBrush for one possible solution.
  public virtual bool AlwaysRebuildPreviewBrush() { return false; }

  // Called every frame, if AlwaysRebuildPreviewBrush()==false
  public virtual void DecayBrush() { }

  public virtual bool NeedsStraightEdgeProxy() {
    return false;
  }

  protected abstract void InitUndoClone(GameObject clone);

  // Return true if a new solid was created.
  protected abstract bool UpdatePositionImpl(
    Vector3 vPos, Quaternion ori,
    float fPressure);

  // This function is a sanity check for making sure we don't overrun our allocated vertex buffers
  //  when creating new geometry.  It is used at low levels as a safeguard.
  public virtual bool IsOutOfVerts() { return false; }

  // This function is used during user creation to determine if a brush stroke should end,
  // beginning a new stroke.  It is not used during playback, so does not need to be
  // deterministic.
  public virtual bool ShouldCurrentLineEnd() { return IsOutOfVerts(); }

  // Line is finished; return true if line should not be kept
  public virtual bool ShouldDiscard() { return false; }

  // Returns geometry generated by the brush.
  // Do not mutate the returned arrays.
  // Brushes that don't generate geometry will return nulls and 0s
  public virtual void DebugGetGeometry(
      out Vector3[] verts, out int nVerts,
      out Vector2[] uv0s,
      out int[] tris, out int nTris) {
    verts = null; nVerts = 0;
    uv0s = null;
    tris = null; nTris = 0;
  }

  public abstract void FinalizeSolitaryBrush();

  public abstract BatchSubset FinalizeBatchedBrush();
#endregion

#region Helpers for subclasses
  // Returns brush size in local (ie canvas) space with pressure factored in.
  protected float PressuredSize(float pressure01) {
    float multiplier = Mathf.Lerp(m_Desc.PressureSizeMin(m_PreviewMode), 1f, pressure01);
    return m_BaseSize_PS * POINTER_TO_LOCAL * multiplier;
  }

  // Returns brush size in local space with pressure and variance factored in.
  protected float PressuredRandomSize(float pressure01, int salt) {
    float randomness = 1f + m_rng.In01(salt) * m_Desc.m_SizeVariance;
    return PressuredSize(pressure01) * randomness;
  }

  // Returns an opacity with pressure factored in.
  // Return value is in [0,1]
  protected float PressuredOpacity(float pressure01) {
    Vector2 range = m_Desc.m_PressureOpacityRange;
    float multiplier = Mathf.Lerp(range.x, range.y, pressure01);
    return Mathf.Clamp01(m_Desc.m_Opacity * multiplier);
  }

  #endregion

#region Helpers for subclasses (static)
  // Returns v, flipped so it points in the same direction as desired.
  static Vector3 InDirectionOf(Vector3 desired, Vector3 v) {
    return Vector3.Dot(v, desired) >= 0 ? v : -v;
  }

  // Returns unit right and normal vectors such that they form an orthogonal
  // reference frame with nMove.
  //   vPreferredR      preferred right direction (ok if non-unit; ok if zero)
  //   nMove            Unit-length direction of movement
  //   ori              Orientation of brush
  //
  // Note: always returns a left-handed coordinate system.
  //
  // To be fully correct, we should return a right-handed basis if in a
  // mirrored coordinate system; but that requires changes to a lot of
  // downstream code to either reverse winding in the shader (with batching
  // implications), or to generate frontfacing winding all the time, etc.
  //
  // It's a lot of complication and the only tangible benefit is that UVs
  // will look mirrored on mirrored strokes: very subtle.
  //
  // The isOriMirrored flag is passed through just in case we need to be
  // correct at some point in the future.
  protected static void ComputeSurfaceFrameNew(Vector3 vPreferredR,
      Vector3 nMove, Quaternion ori,
      out Vector3 nRight, out Vector3 nNormal) {
    Vector3 nPointerF = ori * Vector3.forward;
    Vector3 nPointerU = ori * Vector3.up;

    // surface-normal  x  surface-forward  =  surface-right
    Vector3 vRight1 = InDirectionOf(vPreferredR, Vector3.Cross(nPointerF, nMove));

    // If nPointerF and nMove are near parallel (abs(dot) ~= 1), the direction
    // of vRight1 is unstable.  This happens when pulling the brush.  In this
    // case, brush-up is a good choice for the normal.
    Vector3 vRight2 = InDirectionOf(vPreferredR, Vector3.Cross(nPointerU, nMove));
    // Scale this component up the more unstable vRight1 is.
    vRight2 *= Mathf.Abs(Vector3.Dot(nPointerF, nMove));

    nRight = (vRight1 + vRight2).normalized;
    nNormal = Vector3.Cross(nMove, nRight);
  }

  private static double SetExponent(double x, int exp) {
    // IEEE 64-bit float is 1.11.52; exp bias is 1023
    long expMask = ((1L << 11) - 1) << 52;
    long expBits = ((long)(exp + 1023) << 52) & expMask;
    unsafe {
      long bits = *((long*)&x);
      bits = (bits & ~expMask) | expBits;
      return *((double*)&bits);
    }
  }

  /// Hash a float into a float in [0,1)
  protected static float HashFloat01(float f) {
    // See CLR 11.3.2 "the multiplication method"
    // https://books.google.com/books?id=NLngYyWFl_YC&pg=PA232
    // This floating-point variant (not rigorously analyzed) is the same
    // algorithm, if you think of floats as 23-bit ints (which they are,
    // if you ignore the exponent and the implicit leading 1)

    double K = 0.6180339887498949; // (sqrt(5)-1)/2; somewhat arbitrary
    double val = K * f;
    // "shift" away the first 23 bits of mantissa, throw away sign
    val = Math.Abs(SetExponent(val, 23) % 1);
    return (float)val;
  }

  /// Copy two triangles starting at given index to subsequent location, reversing winding order.
  /// Pass the index of the 0th vertex of a quad
  protected static void MirrorQuadFace<T>(T[] array, int index) {
    int dstIndex = index + 6;
    array[dstIndex    ] = array[index    ];
    array[dstIndex + 1] = array[index + 2];
    array[dstIndex + 2] = array[index + 1];
    array[dstIndex + 3] = array[index + 3];
    array[dstIndex + 4] = array[index + 5];
    array[dstIndex + 5] = array[index + 4];
  }

  protected static void MirrorQuadFace<T>(List<T> array, int index) {
    int dstIndex = index + 6;
    array[dstIndex    ] = array[index    ];
    array[dstIndex + 1] = array[index + 2];
    array[dstIndex + 2] = array[index + 1];
    array[dstIndex + 3] = array[index + 3];
    array[dstIndex + 4] = array[index + 5];
    array[dstIndex + 5] = array[index + 4];
  }

  /// Pass the index of the 0th vertex of a quad
  protected static void MirrorTangents(Vector4[] array, int index) {
    // Frame is [N,T,B]. T, B are the same as front; but N is flipped.
    // Therefore backface frame's handedness (stored in T.w) is flipped.
    MirrorQuadFace(array, index);
    int dstIndex = index + 6;
    for (int i = 0; i < 6; ++i) {
      array[dstIndex + i].w *= -1.0f;
    }
  }

  protected static void CreateDuplicateQuad(Vector3[] aVertArray, Vector3[] aNormArray,
                                            int iQuadIndex, Vector3 vQuadNormal) {
    int iPrevVertIndex = (iQuadIndex - 1) * 6;
    int iCurrVertIndex = iQuadIndex * 6;
    MirrorQuadFace(aVertArray, iPrevVertIndex);
    for (int i = 0; i < 6; ++i) {
      aNormArray[iCurrVertIndex + i] = -vQuadNormal;
    }
  }

  /// Compute tangent space basis for an entire chain of quads at once.
  /// Performs averaging, so pass an entire segment rather than calling this
  /// once per quad.
  ///
  /// aVertices  [in]
  /// aUVs       [in]
  /// aNormals   [in]
  /// aTangents  [out]
  /// stride     stride (in vertices) between front-facing quads
  /// iVert0,1   Half-open range of verts to process
  ///
  /// Assumes the following ordering of verts:
  ///
  ///             0--1  4
  /// trailing    | / / |     leading
  ///             2  3--5
  ///
  /// Assumptions made:
  /// - Side-by-side vertices (eg 0,2 and 4,5) have the same normals.
  ///   Breaking this will cause lighting seams.
  /// - Tangent is pretty much the same across the quad; ie S_012 ~= S_345
  ///   If this doesn't mostly hold, lighting will look "strange".
  /// - Tangent is pretty much the same from the previous quad.
  ///   ie previous S_345 ~= current S_012.
  ///   If this doesn't mostly hold, lighting will look "strange".
  /// - Tangent basis handedness does not change from quad to quad
  ///   (this is an assumption on UV layout)
  ///   Breaking this will make the bumps invert.
  ///
  protected static void ComputeTangentSpaceForQuads(
    Vector3[] aVertices,
    Vector2[] aUVs,
    Vector3[] aNormals,
    Vector4[] aTangents,
    int stride,
    int iVert0, int iVert1) {
    // See http://www.terathon.com/code/tangent.html

    // For each front-facing-quad in the range
    Debug.Assert( (iVert1-iVert0) % stride == 0 );
    for (int iCur = iVert0; iCur < iVert1; iCur += stride) {
      // Each vertex will have a slightly-modified S and T, adjusted so they
      // are orthogonal to the blended vertex normal.
      Vector3 n023 = aNormals[iCur    ];  // 0,2,3 have the same normal
      Vector3 n145 = aNormals[iCur + 1];  // 1,4,5 have the same normal

      int iPrev = iCur - stride;
      bool bHavePreviousQuad = (iPrev >= iVert0);
      bool bHaveNextQuad = (iCur + stride) < iVert1;

      // 0,1,2 and 3,4,5 have the same S and T directions
      Vector3 vS_012, vS_345, vT_012;
      float w;
      if (bHavePreviousQuad) {
        ComputeS(aVertices, aUVs, iCur    , out vS_012);
        // ComputeS(aVertices, aUVs, iCur + 3, out vS_345);
        w = aTangents[iPrev].w;
      } else {
        ComputeST(aVertices, aUVs, iCur    , out vS_012, out vT_012);
        // ComputeS (aVertices, aUVs, iCur + 3, out vS_345);
        w = (Vector3.Dot(Vector3.Cross(n023, vS_012), vT_012) < 0.0f) ? -1.0f : 1.0f;
      }
      vS_345 = vS_012;

      // Make tangents orthogonal to the vert's normal
      Vector3 tmp;

      // Trailing edge: 0, 2, 3 (and previous quad's 1, 4, 5)
      {
        Vector3 t02 = (vS_012 - Vector3.Dot(vS_012, n023) * n023);
        Vector3 t3  = (vS_345 - Vector3.Dot(vS_345, n023) * n023);
        tmp = (t02 + t3).normalized;
        aTangents[iCur + 2].Set(tmp.x, tmp.y, tmp.z, w);
        aTangents[iCur + 3] = aTangents[iCur + 2];
        t02.Normalize();
        aTangents[iCur    ].Set(t02.x, t02.y, t02.z, w);

        if (bHavePreviousQuad) {
          aTangents[iPrev + 1] = aTangents[iPrev + 4] = aTangents[iCur];
          aTangents[iPrev + 5] = aTangents[iCur + 2];
        }
      }

      // Leading edge: 1, 4, 5
      if (! bHaveNextQuad) {
        Vector3 t1  = (vS_012 - Vector3.Dot(vS_012, n145) * n145);
        Vector3 t45 = (vS_345 - Vector3.Dot(vS_345, n145) * n145);
        tmp = (t1 + t45).normalized;
        aTangents[iCur + 1].Set(tmp.x, tmp.y, tmp.z, w);
        aTangents[iCur + 4] = aTangents[iCur + 1];
        t45.Normalize();
        aTangents[iCur + 5].Set(t45.x, t45.y, t45.z, w);
      }
    }
  }

  // Given:
  //   positions and UVs for a triangle
  // Compute:
  //   s and t (also called T and B, or Tangent and bitangent)
  protected static void ComputeST(
      Vector3[] aVert, Vector2[] aUV, int iBaseVert,
      out Vector3 vS, out Vector3 vT) {
    Vector3 v1 = aVert[iBaseVert    ];
    Vector3 v2 = aVert[iBaseVert + 1];
    Vector3 v3 = aVert[iBaseVert + 2];

    Vector2 w1 = aUV[iBaseVert    ];
    Vector2 w2 = aUV[iBaseVert + 1];
    Vector2 w3 = aUV[iBaseVert + 2];

    float x1 = v2.x - v1.x;
    float x2 = v3.x - v1.x;
    float y1 = v2.y - v1.y;
    float y2 = v3.y - v1.y;
    float z1 = v2.z - v1.z;
    float z2 = v3.z - v1.z;

    float s1 = w2.x - w1.x;
    float s2 = w3.x - w1.x;
    float t1 = w2.y - w1.y;
    float t2 = w3.y - w1.y;

    float r = 1.0f / (s1*t2 - s2*t1);

    vS = new Vector3(r*(t2*x1 - t1*x2), r*(t2*y1 - t1*y2), r*(t2*z1 - t1*z2));
    vT = new Vector3(r*(s1*x2 - s2*x1), r*(s1*y2 - s2*y1), r*(s1*z2 - s2*z1));
  }

  protected static void ComputeST(
      IList<Vector3> aVert, IList<Vector2> aUV, int iBaseVert, int iv0, int iv1, int iv2,
      out Vector3 vS, out Vector3 vT) {
    Vector3 v1 = aVert[iBaseVert + iv0];
    Vector3 v2 = aVert[iBaseVert + iv1];
    Vector3 v3 = aVert[iBaseVert + iv2];

    Vector2 w1 = aUV[iBaseVert + iv0];
    Vector2 w2 = aUV[iBaseVert + iv1];
    Vector2 w3 = aUV[iBaseVert + iv2];

    float x1 = v2.x - v1.x;
    float x2 = v3.x - v1.x;
    float y1 = v2.y - v1.y;
    float y2 = v3.y - v1.y;
    float z1 = v2.z - v1.z;
    float z2 = v3.z - v1.z;

    float s1 = w2.x - w1.x;
    float s2 = w3.x - w1.x;
    float t1 = w2.y - w1.y;
    float t2 = w3.y - w1.y;

    float r = 1.0f / (s1*t2 - s2*t1);

    vS = new Vector3(r*(t2*x1 - t1*x2), r*(t2*y1 - t1*y2), r*(t2*z1 - t1*z2));
    vT = new Vector3(r*(s1*x2 - s2*x1), r*(s1*y2 - s2*y1), r*(s1*z2 - s2*z1));
  }

  protected static void ComputeS(
      Vector3[] aVert, Vector2[] aUV, int iBaseVert,
      out Vector3 vS) {
    Vector3 v1 = aVert[iBaseVert    ];
    Vector3 v2 = aVert[iBaseVert + 1];
    Vector3 v3 = aVert[iBaseVert + 2];

    Vector2 w1 = aUV[iBaseVert    ];
    Vector2 w2 = aUV[iBaseVert + 1];
    Vector2 w3 = aUV[iBaseVert + 2];

    float x1 = v2.x - v1.x;
    float x2 = v3.x - v1.x;
    float y1 = v2.y - v1.y;
    float y2 = v3.y - v1.y;
    float z1 = v2.z - v1.z;
    float z2 = v3.z - v1.z;

    float s1 = w2.x - w1.x;
    float s2 = w3.x - w1.x;
    float t1 = w2.y - w1.y;
    float t2 = w3.y - w1.y;

    float r = 1.0f / (s1*t2 - s2*t1);

    vS = new Vector3(r*(t2*x1 - t1*x2), r*(t2*y1 - t1*y2), r*(t2*z1 - t1*z2));
  }
#endregion
}
}  // namespace TiltBrush
