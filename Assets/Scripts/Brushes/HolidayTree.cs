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
public class HolidayTree : ParentBrush {
  [SerializeField] protected BrushDescriptor m_trunkBrush;
  [SerializeField] protected Color m_trunkColor;

  [SerializeField] protected BrushDescriptor m_branchBrush;
  [SerializeField] protected Color m_branchColor;
  [SerializeField] protected float m_branchRelativeSize;
  [SerializeField] protected float m_branchFrequency;
  [SerializeField] protected float m_branchScale;

  [SerializeField] protected BrushDescriptor m_frondBrush;
  [SerializeField] protected Color m_frondColor;
  [SerializeField] protected float m_frondRelativeSize;
  [SerializeField] protected float m_frondFrequency;
  [SerializeField] protected float m_frondScale;

  [SerializeField] protected BrushDescriptor m_decoBrush;
  [SerializeField] protected float m_decoTwist = 1800;
  [SerializeField] protected Vector3 m_decoOffset = new Vector3(0.1f, 0, 0);

  protected override void MaybeCreateChildrenImpl() {
    // TODO: when too many children, starve an old one in order to create another.

    int kBranchCount = 12;
    int kFrondCount = 16;
    if (m_recursionLevel == 0) {
      // Trunk
      if (m_children.Count == 0) {
        InitializeAndAddChild(new PbChildIdentityXf(), m_trunkBrush, m_trunkColor);
      }
      if (DistanceSinceLastKnotBasedChild() > m_branchFrequency
          && m_children.Count < kBranchCount + 1) {
        int salt = m_children.Count;
        // Children 1 - 13 are the branches; early ones grow faster.
        float growthPercent = (kBranchCount + 1 - (float)m_children.Count) / kBranchCount;
        TrTransform offset =
            // Branches don't extend as quickly as the trunk
            TrTransform.S(m_branchScale * growthPercent) *
            // Randomly place around the tree
            TrTransform.R(m_rng.InRange(salt, 0f, 360f), Vector3.forward) *
            // Angle the branches backwards (away from the stroke tip)
            TrTransform.R(120, Vector3.right);
        InitializeAndAddChild(
            new PbChildWithOffset(m_knots.Count-1, AttachFrame.LineTangent, offset, 0),
            m_Desc,  // Recurse with same brush
            Color.white, m_branchRelativeSize);
      }
    } else if (m_recursionLevel == 1) {
      // Branch
      if (m_children.Count == 0) {
        InitializeAndAddChild(new PbChildIdentityXf(), m_branchBrush, m_branchColor);
      }
      // TODO: would like this frequency to be higher for tinier branches
      if (DistanceSinceLastKnotBasedChild() > m_frondFrequency
          && m_children.Count < kFrondCount + 1) {
        float growthPercent = 1; // (kFrondCount + 1 - (float)m_children.Count) / kFrondCount;
        for (int deg = -30; deg <= 30; deg += 60) {
          TrTransform offset =
              // Fronds don't grow as quickly as the branch
              TrTransform.S(m_frondScale * growthPercent) *
              TrTransform.R(deg, Vector3.up);
          InitializeAndAddChild(
              new PbChildWithOffset(m_knots.Count-1, AttachFrame.LineTangent, offset, 0),
              m_frondBrush, m_frondColor, m_frondRelativeSize);
          TrTransform decoOffset = TrTransform.T(m_decoOffset);
          InitializeAndAddChild(
              new PbChildWithOffset(-1, AttachFrame.LineTangent, decoOffset, m_decoTwist),
              m_decoBrush, Color.white, m_frondRelativeSize);
        }
      }
    }
  }
}
} // namespace TiltBrush
