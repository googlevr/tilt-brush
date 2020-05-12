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
///
/// PbrBrushScript purely exists to provide a vertex layout.
///
public class PbrBrushScript : BaseBrushScript {

  protected PbrBrushScript() : base(bCanBatch: true) {
  }

  public override GeometryPool.VertexLayout GetVertexLayout(BrushDescriptor desc) {
    return new GeometryPool.VertexLayout {
      uv0Size = 2,
      uv0Semantic = GeometryPool.Semantic.XyIsUv,
      bUseNormals = true,
      bUseColors = true,
      bUseTangents = false
    };
  }

  // -------------------------------------------------------------------------------------------- //
  // No-op overrides.
  // -------------------------------------------------------------------------------------------- //
  // This is OK because this isn't a real brush, yet required because these functions are abstract.
  //
  override protected bool UpdatePositionImpl(Vector3 vPos, Quaternion ori, float fPressure) {
    return true;
  }
  override public BatchSubset FinalizeBatchedBrush() {
    return null;
  }
  override public int GetNumUsedVerts() { return 0; }
  override public float GetSpawnInterval(float pressure01) { return 0f; }
  override protected void InitUndoClone(GameObject clone) { }
  override public void FinalizeSolitaryBrush() { }

  override public void ApplyChangesToVisuals() { }
  // -------------------------------------------------------------------------------------------- //
}

} // namespace TiltBrush