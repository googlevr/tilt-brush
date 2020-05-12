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
public class BatchSubset {
  public Stroke m_Stroke;
  public Batch m_ParentBatch;

  public Bounds m_Bounds;
  public int m_StartVertIndex;
  public int m_VertLength;
  /// First entry in index-buffer. Always a multiple of 3.
  public int m_iTriIndex;
  /// Number of index-buffer entries.
  public int m_nTriIndex;
  public bool m_Active;
  /// Lazily-created
  public ushort[] m_TriangleBackup;

  /// The canvas associated with this stroke
  public CanvasScript Canvas {
    get {
      return m_ParentBatch.ParentPool.Owner.Canvas;
    }
  }
}
} // namespace TiltBrush
