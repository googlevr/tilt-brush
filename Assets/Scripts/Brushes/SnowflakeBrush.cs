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

/// This brush illustrates 6-fold symmetry.
/// - First layer does 3 rotations
/// - Second layer does a reflection for each rotation
///
/// The coordinate frame for the rotations + reflections is (necessarily) fixed
/// at stroke start time.
public class SnowflakeBrush : ParentBrush {
  protected class PbChildKnotBasedMirror : PbChildWithKnotBasedFrame {
    protected Plane m_plane;  // in knot space

    /// Plane is in the frame of the knot
    public PbChildKnotBasedMirror(int frameKnot, AttachFrame frame, Plane plane)
      : base(frameKnot, frame) {
      m_plane = plane;
    }

    protected override TrTransform CalculateChildXf(List<PbKnot> parentKnots) {
      TrTransform canvasFromKnot = GetAttachTransform(parentKnots);
      Plane plane_CS = canvasFromKnot * m_plane;
      TrTransform pointerPose_CS = parentKnots[parentKnots.Count-1].m_pointer;
      return plane_CS.ReflectPoseKeepHandedness(pointerPose_CS);
    }
  }

  static Plane kReflectionPlane = new Plane(Vector3.right, 0);

  [SerializeField] protected BrushDescriptor m_baseBrush;
  [Range(0, .5f)]
  [SerializeField] protected float m_saturationDelta = .1f;
  [Range(0, 1)]
  [SerializeField] protected float m_hueDeltaPct = .2f;
  // It looks _really cool_ when you crank this up
  [Range(1, 50)]
  [SerializeField] protected int m_numRotations = 6;

  protected override void MaybeCreateChildrenImpl() {
    // Only create children once
    if (m_children.Count > 1) { return; }

    BrushDescriptor childDesc = m_Desc;
    if (m_recursionLevel == 1) { childDesc = m_baseBrush; }

    // Since we're doing these color modifications, doing reflection or rotation first
    // changes the look.
    if (m_recursionLevel == 0) {
      // Reflection
      HSLColor reflectedColor = m_Color;
      reflectedColor.s += m_saturationDelta * ((reflectedColor.s > 0.5f) ? -1 : 1);

      InitializeAndAddChild(new PbChildIdentityXf(), childDesc, m_Color);
      InitializeAndAddChild(
          new PbChildKnotBasedMirror(0, AttachFrame.Pointer, kReflectionPlane),
          childDesc, (Color)reflectedColor);
    } else if (m_recursionLevel == 1) {
      // Rotation
      float rotationDeltaDegrees = 360f / m_numRotations;
      for (int i = 0; i < m_numRotations; ++i) {
        int iWrapped = i + ((i*2 > m_numRotations) ? -m_numRotations : 0);
        HSLColor rotatedColor = m_Color;
        float angle = iWrapped * rotationDeltaDegrees;
        rotatedColor.HueDegrees += m_hueDeltaPct * angle;
        TrTransform offset = TrTransform.R(angle, Vector3.forward);
        InitializeAndAddChild(
            new PbChildWithOffset(0, AttachFrame.Pointer, offset, 0),
            childDesc, (Color)rotatedColor);
      }
    }
  }
}
} // namespace TiltBrush
