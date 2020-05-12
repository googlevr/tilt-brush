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

/// The 'Genius' particle brush is a replacement for the original particle brushes that uses
/// geometry rather than the Unity Particle system to render, as it is considerably faster.
/// All the particle shaders use the Particles.cginc library to do things like align the
/// quads with the camera etc. See particles.cginc for details.
class GeniusParticlesBrush : GeometryBrush {
  private const float kSpawnInterval_PS = 0.0025f * App.METERS_TO_UNITS;
  private const float kSingleParticleTriggerPressure = 0.8f;
  private const int kVertsInSolid = 4;
  private const int kTrisInSolid = 2;

  // Values used to compute "salt"; see StatelessRng docs for more detail.
  private const int kSaltMaxParticlesPerKnot = 16;
  private const int kSaltMaxSaltsPerParticle = 16;
  private const int kSaltPressure = 0;
  private const int kSaltAlpha = kSaltPressure + 1;
  private const int kSaltOnSphere = kSaltAlpha + 1;
  private const int kSaltRotation = kSaltOnSphere + 2;
  private const int kSaltRoll = kSaltRotation + 3;
  private const int kSaltAtlas = kSaltRoll + 1;
  // next is kSaltAtlas + 1 (total used is 9)

  private const int kBr = 0;   // back right  (top)
  private const int kBl = 1;   // back left   (top)
  private const int kFr = 2;   // front right (top)
  private const int kFl = 3;   // front left  (top)

  private readonly Vector4 m_TextureAtlas00 = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
  private readonly Vector4 m_TextureAtlas05 = new Vector4(0.0f, 0.5f, 0.0f, 0.0f);
  private readonly Vector4 m_TextureAtlas50 = new Vector4(0.5f, 0.0f, 0.0f, 0.0f);
  private readonly Vector4 m_TextureAtlas55 = new Vector4(0.5f, 0.5f, 0.0f, 0.0f);

  private List<float> m_DecayTimers;
  // Number of knots popped off the front by DecayBrush()
  // Used to keep from reusing RNG values
  private int m_DecayedKnots;
  private float m_DistancePointerTravelled;
  private Vector3 m_LastPos;
  private List<float> m_LengthsAtKnot; // A cache of the length of the stroke at each knot point.
  private float m_SpawnInterval;
  private float m_ParticleSizeScale;

  public GeniusParticlesBrush()
    : base(bCanBatch: true,
           upperBoundVertsPerKnot: kVertsInSolid,
           bDoubleSided: false,
           bSmoothPositions: false) {
    m_DecayTimers = new List<float>();
    m_DistancePointerTravelled = -1;
    m_LengthsAtKnot = new List<float>();
    m_LengthsAtKnot.Add(0); // Length at first knot is always zero.
    m_DecayedKnots = 0;
  }

  protected int CalculateSalt(int knotIndex, int particleIndex) {
    // Act as if the preview stroke is one very long stroke, and we're only generating
    // the geometry for the very tail end of it.
    int pretendKnotIndex = knotIndex + m_DecayedKnots;
    return kSaltMaxSaltsPerParticle * (pretendKnotIndex * kSaltMaxParticlesPerKnot + particleIndex);
  }

  public override float GetSpawnInterval(float pressure01) {
    return m_SpawnInterval;
  }

  /// The distance calculated from the knot is offset in the case that it's the most recent knot
  /// to take into account the fact that the most recent particle is probably further away than
  /// the knot itself.
  protected override float DistanceFromKnot(int knotIndex, Vector3 pos) {
    float distance = base.DistanceFromKnot(knotIndex, pos);
    if (knotIndex == m_knots.Count - 2) {
      distance = m_DistancePointerTravelled;
    }
    return distance;
  }

  public override bool AlwaysRebuildPreviewBrush() {
    return false;
  }

  protected override void InitBrush(BrushDescriptor desc,
      TrTransform localPointerXf) {
    base.InitBrush(desc, localPointerXf);
    Shader.SetGlobalFloat("_GeniusParticlePreviewLifetime", kPreviewDuration);
    m_DecayTimers.Clear();
    m_geometry.Layout = GetVertexLayout(desc);
    m_SpawnInterval = (kSpawnInterval_PS * POINTER_TO_LOCAL) / m_Desc.m_ParticleRate;
    m_ParticleSizeScale = m_Desc.m_ParticleSpeed / m_Desc.m_BrushSizeRange.x;
  }

  public override GeometryPool.VertexLayout GetVertexLayout(BrushDescriptor desc) {
    return new GeometryPool.VertexLayout {
      uv0Size = 4,
      uv0Semantic = GeometryPool.Semantic.XyIsUv,
      uv1Size = 3,
      uv1Semantic = GeometryPool.Semantic.Position,
      bUseNormals = true,
      normalSemantic = GeometryPool.Semantic.Position,
      bUseColors = true,
      bUseTangents = false,
      bUseVertexIds = true,
      bFbxExportNormalAsTexcoord1 = true,
    };
  }

  public override void DecayBrush() {
    int knotsToShift = 0;
    // Decay the preview by counting the number of m_DecayTimers that have expired
    // and deleting that many knots from the beginning.
    for (int i = 0; i < m_DecayTimers.Count; i++) {
      m_DecayTimers[i] += Time.deltaTime;
      if (m_DecayTimers[i] > kPreviewDuration) {
        knotsToShift++;
      }
    }

    Debug.Assert(m_knots.Count - 2 == m_DecayTimers.Count);
    m_DecayTimers.RemoveRange(0, knotsToShift);

    RemoveInitialKnots(knotsToShift);

    if (knotsToShift > 0) {
      // Reduce the calculated length cache only by multiples of the spawn interval.
      float lengthReduction = Mathf.Floor(m_LengthsAtKnot[knotsToShift] / m_SpawnInterval)
                              * m_SpawnInterval;
      int newCount = m_LengthsAtKnot.Count - knotsToShift;
      for (int i = 1; i < newCount; ++i) {
        m_LengthsAtKnot[i] = m_LengthsAtKnot[i + knotsToShift] - lengthReduction;
      }
      m_LengthsAtKnot.SetCount(newCount);
    }

    m_DecayedKnots += knotsToShift;
  }

  /// Bit naughty, but I'm back to overriding UpdatePositionImpl. I think in this case it's
  /// acceptable as it is simply wrapping the call so that we can trap the cursor position to
  /// work out how far the pointer has traveled.
  protected override bool UpdatePositionImpl(Vector3 pos, Quaternion ori, float pressure) {
    bool result = base.UpdatePositionImpl(pos, ori, pressure);
    if (m_DistancePointerTravelled < 0f) {
      m_DistancePointerTravelled = 0f;
    } else {
      m_DistancePointerTravelled += (pos - m_LastPos).magnitude;
    }
    m_LastPos = pos;

    if (m_PreviewMode && result) {
      m_DecayTimers.Add(0);
    }
    return result;
  }

  public override void ResetBrushForPreview(TrTransform localPointerXf) {
    base.ResetBrushForPreview(localPointerXf);
    m_DecayTimers.Clear();
  }

  /// The particles are evenly spaced along the stroke, so calculating how far along a stroke a
  /// given particle is is easy: dist = n * spacing. Given how far along a stroke the current and
  /// previous knots are, we can work out how far between them a particle will lie, and interpolate
  /// between them to get its position.
  /// NOTE: There is a little bit of oddness in here because we add an extra particle to the final
  /// knot. The reason for this is that while drawing, a particle should hang on the end of the
  /// user's pointer, so we add one on here. It is removed during finalization.
  protected override void ControlPointsChanged(int firstKnotIndex) {
    int numKnots = m_knots.Count;
    int previousIndex = firstKnotIndex == 0 ? 0 : firstKnotIndex - 1;

    InvalidateLengthsFromKnot(firstKnotIndex);

    // Set up the space in the mesh for the particles for each knot
    int particlesAtPrev = TotalParticlesAtKnot(previousIndex);
    for (int knotIndex = firstKnotIndex; knotIndex < numKnots; ++knotIndex) {
      int particlesAtCur = TotalParticlesAtKnot(knotIndex);
      int particlesForKnot = particlesAtCur - particlesAtPrev;
      // We have to create an extra particle at the end so that there is a particle that hangs
      // on the user's pointer as they draw.
      if (knotIndex == numKnots - 1) {
        particlesForKnot += 1;
      }
      SetGeometrySpaceForKnot(knotIndex, particlesForKnot);
      particlesAtPrev = particlesAtCur;
    }

    // Allocate space in the geometry for the particles
    ResizeGeometry();

    Knot prev = m_knots[previousIndex];
    particlesAtPrev = TotalParticlesAtKnot(previousIndex);
    float prevLength = StrokeLengthAtKnot(previousIndex);
    // Generate the geometry for the particles for each knot
    for (int knotIndex = firstKnotIndex; knotIndex < numKnots; ++knotIndex) {
      Knot cur = m_knots[knotIndex];
      int particlesForKnot = cur.nTri / kTrisInSolid;
      float curLength = StrokeLengthAtKnot(knotIndex);

      for (int i = 0; i < particlesForKnot; ++i) {
        // Work out position of particle by interpolating between previous and current knot.
        float particleDistOnStroke = (particlesAtPrev + i) * m_SpawnInterval;
        float lerpRatio = Mathf.InverseLerp(prevLength, curLength, particleDistOnStroke);
        Vector3 particlePos = Vector3.Lerp(prev.point.m_Pos, cur.point.m_Pos, lerpRatio);
        // Creating the extra hanging particle on the user's pointer.
        if (knotIndex == (numKnots - 1) && i == (particlesForKnot - 1)) {
          particlePos = cur.point.m_Pos;
        }
        int salt = CalculateSalt(knotIndex, i);
        float size = PressuredRandomSize(cur.smoothedPressure, salt + kSaltPressure);
        CreateParticleGeometry(knotIndex, i, particlePos, size);
      }

      particlesAtPrev = Mathf.FloorToInt(curLength / m_SpawnInterval) + 1;
      prevLength = curLength;
      prev = cur;
    }
  }

  public override bool NeedsStraightEdgeProxy() {
    return true;
  }

  public override BatchSubset FinalizeBatchedBrush() {
    FinalizeParticleMesh();
    return base.FinalizeBatchedBrush();
  }

  public override void FinalizeSolitaryBrush() {
    FinalizeParticleMesh();
    base.FinalizeSolitaryBrush();
  }

  /// Given a knot index, will return the length of the stroke at that index. Values are cached
  /// so the calculation should only be done the first time.
  private float StrokeLengthAtKnot(int knotIndex) {
    int numLengths = m_LengthsAtKnot.Count;
    // try and return cached.
    if (knotIndex < numLengths) {
      return m_LengthsAtKnot[knotIndex];
    }
    Debug.Assert(knotIndex != 0); // value for m_LengthsAtKnot[0] should always be set to 0 already.
    // ... otherwise calculate.
    m_LengthsAtKnot.SetCount(knotIndex + 1);
    Knot prev = m_knots[numLengths - 1];
    float length = m_LengthsAtKnot[numLengths - 1];
    for (int i = numLengths; i <= knotIndex; ++i) {
      Knot cur = m_knots[i];
      float interKnot = (cur.point.m_Pos - prev.point.m_Pos).magnitude;
      length += interKnot;
      m_LengthsAtKnot[i] = length;
      prev = cur;
    }
    return length;
  }

  /// Invalidates the length cache from a specific knot onwards.
  private void InvalidateLengthsFromKnot(int knotIndex) {
    m_LengthsAtKnot.SetCount(Mathf.Max(knotIndex, 1));
  }

  /// Work out how many particles there should be in a stroke once you've got as far as a
  /// specific knot.
  private int TotalParticlesAtKnot(int knotIndex) {
    // The particle for knot 0 should actually be attached to knot 1, so knot zero always returns 0.
    if (knotIndex == 0) { return 0; }
    return Mathf.FloorToInt(StrokeLengthAtKnot(knotIndex) / m_SpawnInterval) + 1;
  }

  private void FinalizeParticleMesh() {
    // Remove the last particle, as it is only there to hang on the user's pointer as they draw.
    {
      Knot final = m_knots[m_knots.Count - 1];
      if (final.nTri <= (kTrisInSolid * NS)) {
        m_knots.SetCount(m_knots.Count - 1);
      }
      else {
        final.nTri -= (ushort)(kTrisInSolid * NS);
        final.nVert -= (ushort)(kVertsInSolid * NS);
        m_knots[m_knots.Count - 1] = final;
      }
      ResizeGeometry();
    }

    // If there's only one particle, make sure the pressure is of at least a minimum value.
    if (m_knots.Count == 2) {
      Knot final = m_knots[1];
      int lastParticle = (final.nTri / kTrisInSolid) - 1;
      float pressure = Mathf.Max(kSingleParticleTriggerPressure, final.smoothedPressure);
      int salt = CalculateSalt(knotIndex: 1, particleIndex: lastParticle);
      float size = PressuredRandomSize(pressure, salt);
      // Take the position of the initial [0] knot as that will be at the actual point the
      // particle was placed, whereas the user may have moved the pointer for the [1] knot, which
      // is the one the original particle was connected to.
      Vector3 pos = m_knots[0].point.m_Pos;
      CreateParticleGeometry(1, lastParticle, pos, size);
    }
  }

  private void SetGeometrySpaceForKnot(int knotIndex, int particleCount) {
    Knot cur = m_knots[knotIndex];
    Knot prev;
    if (knotIndex > 0) {
      prev = m_knots[knotIndex - 1];
    } else {
      prev = new Knot();
    }
    cur.iTri = prev.iTri + prev.nTri;
    cur.iVert = (ushort) (prev.iVert + prev.nVert);
    cur.length = (cur.point.m_Pos - prev.point.m_Pos).magnitude;
    cur.nRight = Vector2.right;
    cur.nTri = (ushort)(kTrisInSolid * particleCount);
    cur.nVert = (ushort)(kVertsInSolid * particleCount);

    m_knots[knotIndex] = cur;
  }

  /// When creating the geometry, the center of the particle is passed through in the vertex normal.
  /// This is so that the shader can align the quad with the camera.
  private void CreateParticleGeometry(int knotIndex, int particleIndex, Vector3 pos, float size) {
    Knot cur = m_knots[knotIndex];
    int vertIndex = cur.iVert + particleIndex * kVertsInSolid * NS;
    int triIndex = cur.iTri + particleIndex * kTrisInSolid * NS;
    int salt = CalculateSalt(knotIndex, particleIndex);

    float alpha;
    if (m_Desc.m_RandomizeAlpha) {
      alpha = m_rng.In01(salt + kSaltAlpha);
    } else {
      alpha = m_Desc.m_Opacity * Mathf.Lerp(m_Desc.m_PressureOpacityRange.x,
                                            m_Desc.m_PressureOpacityRange.y, cur.smoothedPressure);
    }

    Vector3 randomOffset = m_rng.OnUnitSphere(salt + kSaltOnSphere) * size * m_ParticleSizeScale;
    Vector3 center = pos + randomOffset;
    Quaternion randomDirection = m_rng.Rotation(salt + kSaltRotation);

    Vector3 upOffset = randomDirection * (Vector3.up * size * 0.5f);
    Vector3 rightOffset = randomDirection * (Vector3.right * size * 0.5f);

    SetTri(triIndex, vertIndex, 0, kBr, kBl, kFl);
    SetTri(triIndex, vertIndex, 1, kBr, kFl, kFr);

    SetVert(vertIndex, kBr, center - upOffset + rightOffset, center, m_Color, alpha);
    SetVert(vertIndex, kBl, center - upOffset - rightOffset, center, m_Color, alpha);
    SetVert(vertIndex, kFr, center + upOffset + rightOffset, center, m_Color, alpha);
    SetVert(vertIndex, kFl, center + upOffset - rightOffset, center, m_Color, alpha);

    // When a stroke is loaded from a .tilt, m_TimestampMs is _not_ initialized from the
    // timestamp in the Stroke. Therefore we don't have to worry about it being in the future
    // and causing particle animation to look weird.
    float knotCreationTimeSinceLevelLoad =
        App.Config.m_ForceDeterministicBirthTimeForExport ? 0
            : (float)App.Instance.SketchTimeToLevelLoadTime(cur.point.m_TimestampMs * .001);
    // Time is negative in preview mode to indicate to the shader that they should fade out.
    float time = m_PreviewMode ? -knotCreationTimeSinceLevelLoad : knotCreationTimeSinceLevelLoad;
    float halfRotateRange = m_Desc.m_ParticleInitialRotationRange / 2;
    float rotation = m_rng.InRange(salt + kSaltRoll, -halfRotateRange, halfRotateRange)
        * Mathf.Deg2Rad;

    // time and rotation are packed into first texture coordinate, particle center is packed into
    // the second.
    Vector4 uv0 = new Vector4(0, 0, rotation, time);
    Vector3 uv1 = pos;

    if (m_Desc.m_TextureAtlasV > 1) {
      int rand = m_rng.InIntRange(salt + kSaltAtlas, 0, 4);
      Vector4 offset = m_TextureAtlas00;
      if (rand == 1) { offset = m_TextureAtlas50; }
      else if (rand == 2) { offset = m_TextureAtlas05; }
      else if (rand == 3) { offset = m_TextureAtlas55; }

      SetUv0(vertIndex, kBl, m_TextureAtlas00 + offset + uv0);
      SetUv0(vertIndex, kFl, m_TextureAtlas50 + offset + uv0);
      SetUv0(vertIndex, kBr, m_TextureAtlas05 + offset + uv0);
      SetUv0(vertIndex, kFr, m_TextureAtlas55 + offset + uv0);
    } else {
      SetUv0(vertIndex, kBl, uv0 + (m_TextureAtlas00 * 2));
      SetUv0(vertIndex, kFl, uv0 + (m_TextureAtlas50 * 2));
      SetUv0(vertIndex, kBr, uv0 + (m_TextureAtlas05 * 2));
      SetUv0(vertIndex, kFr, uv0 + (m_TextureAtlas55 * 2));
    }
    SetUv1(vertIndex, kBl, uv1);
    SetUv1(vertIndex, kFl, uv1);
    SetUv1(vertIndex, kBr, uv1);
    SetUv1(vertIndex, kFr, uv1);
  }
}
}  // namespace TiltBrush
