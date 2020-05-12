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
using UnityEngine;

namespace TiltBrush {

public class StrokeData {
  public Color m_Color;
  public Guid m_BrushGuid;
  // The room-space size of the brush when the stroke was laid down
  public float m_BrushSize;
  // The size of the pointer, relative to  when the stroke was laid down.
  // AKA, the "pointer to local" scale factor.
  // m_BrushSize * m_BrushScale = size in local/canvas space
  public float m_BrushScale;
  public PointerManager.ControlPoint[] m_ControlPoints;
  public SketchMemoryScript.StrokeFlags m_Flags;
  // Seed for deterministic pseudo-random numbers for geometry generation.
  // Not currently serialized.
  public int m_Seed;
  public SketchGroupTag m_Group = SketchGroupTag.None;

  /// This creates a copy of the given stroke.
  public StrokeData(StrokeData existing = null) {
    if (existing != null) {
      this.m_Color = existing.m_Color;
      this.m_BrushGuid = existing.m_BrushGuid;
      this.m_BrushSize = existing.m_BrushSize;
      this.m_BrushScale = existing.m_BrushScale;
      this.m_Flags = existing.m_Flags;
      this.m_Seed = existing.m_Seed;
      this.m_Group = existing.m_Group;
      this.m_ControlPoints = new PointerManager.ControlPoint[existing.m_ControlPoints.Length];
      Array.Copy(existing.m_ControlPoints, this.m_ControlPoints, this.m_ControlPoints.Length);
    }
  }
}
} // namespace TiltBrush