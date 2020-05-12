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
public class CandyCane : ParentBrush {
  [SerializeField] protected int m_numGrossParents = 5;

  // The number of elements here dictates the number of fine strands
  [SerializeField] protected Color32[] kCaneColors = {
    new Color32(255, 30, 30, 255),
    new Color32(230, 200, 200, 255),
    new Color32(20, 180, 20, 255),
  };
  [SerializeField] protected BrushDescriptor[] m_caneBrushes;

  [SerializeField] protected float m_grossRotationsPerRadian = .1f;
  [SerializeField] protected float m_fineRotationsPerRadian = -.01f;

  // These are all as %ages of brush size; they should add up to roughly 1
  [SerializeField] protected float m_caneGrossRadiusPct = 0.425f;
  [SerializeField] protected float m_caneFineRadiusPct = 0.075f;
  [SerializeField] protected float m_strandSizePercent = 0.5f;

  protected override void MaybeCreateChildrenImpl() {
    // Only create children once
    if (m_children.Count > 1) { return; }

    if (m_recursionLevel == 0) {
      float radiusMeters = m_caneGrossRadiusPct * BaseSize_LS * App.UNITS_TO_METERS;
      Vector3 offsetT = m_caneGrossRadiusPct * Vector3.right;
      for (int i = 0; i < m_numGrossParents; ++i) {
        Quaternion rotation = Quaternion.AngleAxis(i*360.0f / m_numGrossParents, Vector3.forward);
        TrTransform offset = TrTransform.T(rotation * offsetT);
        float degreesPerMeter = m_grossRotationsPerRadian / radiusMeters * 360;
        InitializeAndAddChild(
            new PbChildWithOffset(-1, AttachFrame.LineTangent, offset, degreesPerMeter),
            m_Desc,  // Recurse with same brush
            Color.white);
      }
    } else {
      float radiusMeters = m_caneFineRadiusPct * BaseSize_LS * App.UNITS_TO_METERS;
      Vector3 offsetT = m_caneFineRadiusPct * Vector3.right;
      for (int i = 0; i < kCaneColors.Length; ++i) {
        Quaternion rotation = Quaternion.AngleAxis(i*360.0f / kCaneColors.Length, Vector3.forward);
        TrTransform offset = TrTransform.T(rotation * offsetT);
        float degreesPerMeter = m_fineRotationsPerRadian / radiusMeters * 360;
        InitializeAndAddChild(
            new PbChildWithOffset(-1, AttachFrame.LineTangent, offset, degreesPerMeter),
            m_caneBrushes[i % m_caneBrushes.Length],
            kCaneColors[i],
            m_strandSizePercent);
      }
    }
  }
}
} // namespace TiltBrush
