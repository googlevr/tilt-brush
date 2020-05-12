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
/// BlocksBrushScript purely exists to provide a vertex layout.
///
public class BlocksBrushScript : BaseBrushScript {

  protected BlocksBrushScript() : base(bCanBatch: true) {
  }

  public override GeometryPool.VertexLayout GetVertexLayout(BrushDescriptor desc) {
    // XXX: This doesn't work because blocks models may not always have the same vertex layout.
    //      It happens to work currently.
    var layout = new GeometryPool.VertexLayout();
    layout.bUseColors = true;
    layout.bUseNormals = true;
    layout.bUseTangents = false;
    layout.bUseVertexIds = false;
    return layout;
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