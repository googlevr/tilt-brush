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

public static class HierarchyUtils {
  /// Sets object and all children to a layer.
  public static void RecursivelySetLayer(Transform obj, int layer) {
    if (obj == null) { return; }
    obj.gameObject.layer = layer;
    for (int i = 0; i < obj.childCount; ++i) {
      RecursivelySetLayer(obj.GetChild(i), layer);
    }
  }

  /// Disables object and all children's shadow casting and receiving.
  public static void RecursivelyDisableShadows(Transform obj) {
    if (obj == null) { return; }
    Renderer r = obj.GetComponent<Renderer>();
    if (r) {
      r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      r.receiveShadows = false;
    }
    for (int i = 0; i < obj.childCount; ++i) {
      RecursivelyDisableShadows(obj.GetChild(i));
    }
  }

  /// Sets material on object and all children.
  public static void RecursivelySetMaterial(Transform obj, Material mat) {
    if (obj == null) { return; }
    Renderer r = obj.GetComponent<Renderer>();
    if (r) {
      r.material = mat;
    }
    for (int i = 0; i < obj.childCount; ++i) {
      RecursivelySetMaterial(obj.GetChild(i), mat);
    }
  }

  /// Sets material batchId for GPU intersection on object and all children.
  public static void RecursivelySetMaterialBatchID(Transform obj, float batchId) {
    if (obj == null) { return; }

    Renderer r = obj.GetComponent<Renderer>();
    if (r) {
      var propertyBlock = new MaterialPropertyBlock();
      r.GetPropertyBlock(propertyBlock);
      propertyBlock.SetFloat("_BatchID", batchId);
      r.SetPropertyBlock(propertyBlock);
    }
    for (int i = 0; i < obj.childCount; ++i) {
      RecursivelySetMaterialBatchID(obj.GetChild(i), batchId);
    }
  }
}

} // namespace TiltBrush
