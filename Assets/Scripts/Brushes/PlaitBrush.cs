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
// N children, each of which follows the same periodic path through space.
// Each child is offset by some phase; phases are distributed equally.
public class PlaitBrush : ParentBrush {
  const float TWOPI = 2 * Mathf.PI;

  protected class PbChildPlait : PbChild {
    protected PlaitBrush m_owner;
    // Phase is a number in [0, 1)
    protected int m_strand;
    protected PlaitBrush O { get { return m_owner; } }

    public PbChildPlait(PlaitBrush owner, int strand) {
      m_owner = owner;
      m_strand = strand;
    }

    // This is the where the magic lies.
    // It should be some function with a period of 1.
    protected Vector2 SomePeriodicFunction(float t01) {
      if (O.NumStrands % 2 == 0) {
        // The default doesn't look very good with an even # of strands
        return new Vector2(Mathf.Sin(TWOPI * t01),
                           Mathf.Sin(TWOPI * t01 * 1.5f));
      } else {
        return new Vector2(Mathf.Sin(TWOPI * t01),
                           Mathf.Sin(TWOPI * t01 * 2));
      }
    }

    protected override TrTransform CalculateChildXf(List<PbKnot> parentKnots) {
      PbKnot lastKnot = parentKnots[parentKnots.Count-1];

      float distanceMeters = lastKnot.m_distance * App.UNITS_TO_METERS;
      float t = O.CyclesPerMeter * distanceMeters + (float)m_strand / O.NumStrands;
      // Our periodic function makes the plait look pretty square; maybe add
      // some rotation to break things up a bit?
      float rotations = (O.CyclesPerMeter * distanceMeters) * O.RotationsPerCycle;

      float amplitude = lastKnot.m_pressuredSize / 2;  // /2 because size is diameter, not radius.
      TrTransform action =
          TrTransform.R(rotations * 360, Vector3.forward) *
          TrTransform.T(SomePeriodicFunction(t) * amplitude);

      TrTransform actionInCanvasSpace = action.TransformBy(lastKnot.GetFrame(AttachFrame.LineTangent));
      return actionInCanvasSpace * lastKnot.m_pointer;
    }
  }

  [SerializeField] protected BrushDescriptor m_baseBrush;
  [Range(3, 9)]
  [SerializeField] protected float m_numStrands = 3;
  [Range(0, 3)]
  [SerializeField] protected int m_recursionLimit = 0;
  [SerializeField] protected Color32[] m_colors = {
    new Color32(255, 30, 30, 255),
    new Color32(230, 200, 200, 255),
    new Color32(20, 180, 20, 255),
  };
  // Child size is determined mostly automatically; this fine-tunes it.
  [Range(0.25f, 4)]
  [SerializeField] protected float m_childScaleFineTune = 1;
  [SerializeField] protected float m_cyclesPerMeterAtUnitSize = 3;
  // This adds some extra rotation to the periodic function; it can help break
  // up the silhouette.
  [SerializeField] protected float m_rotationsPerCycle = 0;

  // Cycles per meter per brushsize
  public float CyclesPerMeter { get { return m_cyclesPerMeterAtUnitSize / BaseSize_LS; } }
  public float RotationsPerCycle { get { return m_rotationsPerCycle; } }
  public float NumStrands { get { return m_numStrands; } }

  protected override void MaybeCreateChildrenImpl() {
    // Only create children once
    if (m_children.Count > 0) { return; }
    if (m_recursionLevel >= m_recursionLimit) {
      for (int i = 0; i < NumStrands; ++i) {
        InitializeAndAddChild(
            new PbChildPlait(this, i),
            m_baseBrush, m_colors[i % m_colors.Length],
            relativeSize: m_childScaleFineTune / NumStrands);
      }
    } else {
      int numMiddleStrands = 3 + m_recursionLimit - m_recursionLevel;
      for (int i = 0; i < numMiddleStrands; ++i) {
        InitializeAndAddChild(
            new PbChildPlait(this, i),
            m_Desc, m_colors[i % m_colors.Length],
            relativeSize: 0.7f * m_childScaleFineTune / numMiddleStrands);
      }
    }
  }
}
} // namespace TiltBrush
