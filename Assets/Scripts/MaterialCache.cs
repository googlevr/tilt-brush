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
using System.Collections.Generic;

namespace TiltBrush {

// This class is used by the ControllerMaterialCatalog to create a mapping between catalog
// materials and instanced versions of those materials.  If a material is assigned to a
// renderer from the catalog and then altered, it is instanced.  This ensures only a single
// material is assigned per renderer.
public class MaterialCache : MonoBehaviour {
  private Renderer m_SiblingRenderer;
  private Dictionary<Material, Material> m_CachedMaterialMap;

  void Awake() {
    m_CachedMaterialMap = new Dictionary<Material, Material>(new ReferenceComparer<Material>());
    m_SiblingRenderer = GetComponent<Renderer>();
  }

  public void AssignMaterial(Material mat) {
    // Get mat from dictionary.
    if (!m_CachedMaterialMap.ContainsKey(mat)) {
      // If it doesn't, clone and add.
      m_SiblingRenderer.material = mat;

#if UNITY_EDITOR
      // Debug check to verify the material being cached is not dynamically generated.
      if (UnityEditor.AssetDatabase.GetAssetPath(mat) == null) {
        // If this error fires, someone is passing a dynamically generated material
        // in to the MaterialCache.  This should only be used by materials that are
        // resources.
        Debug.LogError("Generated material used as a key to the MaterialCache dictionary.");
      }
#endif
      m_CachedMaterialMap.Add(mat, m_SiblingRenderer.material);
    }
    m_SiblingRenderer.material = m_CachedMaterialMap[mat];
  }
}

} // namespace TiltBrush