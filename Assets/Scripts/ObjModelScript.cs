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

public class ObjModelScript : MonoBehaviour {
  private int m_NumVertsInMeshes = -1;

  // This must be public -- or at least serialized -- or it won't survive
  // past an Instantiate() operation. It contains all MeshFilters in this
  // GameObject tree such that:
  // - The mesh will be visible when the tree root is enabled
  // More specificially, this means:
  // - MeshFilter exists, and its .mesh is non-null
  // - MeshRenderer exists
  // - root.activeInHierarchy  implies  gameObject.activeInHierarchy
  public MeshFilter[] m_MeshChildren;

  public int NumMeshes {
    get { return m_MeshChildren.Length; }
  }

  public int GetNumVertsInMeshes() {
    if (m_NumVertsInMeshes <= 0) {
      for (int i = 0; i < m_MeshChildren.Length; ++i) {
        m_NumVertsInMeshes += m_MeshChildren[i].sharedMesh.vertexCount;
      }

      m_NumVertsInMeshes = Mathf.Max(1,
          (int)(m_NumVertsInMeshes * WidgetManager.m_Instance.ModelVertCountScalar));
    }
    return m_NumVertsInMeshes;
  }

  private static void GetAllMeshFilters(List<MeshFilter> filters, Transform t, bool isRoot) {
    // Only return meshes that will be visible when the hierarchy root is enabled
    if (!isRoot && !t.gameObject.activeSelf) {
      // Prune this whole section of the tree.
      // This case should be unexpected, but currently ModelWidget inserts SnapGhost
      // objects into our tree (which it should not; they should be kept separate),
      // and it calls Init() on us (which it should not, because we're already initialized).
      // TODO: re-enable this warning when those things are fixed.
      // Debug.LogWarningFormat("Unexpected: sub-object {0} is disabled", t);
      return;
    }

    var meshFilter = t.GetComponent<MeshFilter>();
    var meshRenderer = t.GetComponent<MeshRenderer>();
    if (meshFilter != null && meshRenderer != null && meshFilter.sharedMesh != null) {
      filters.Add(meshFilter);
    }

    foreach (Transform child in t) {
      GetAllMeshFilters(filters, child, isRoot: false);
    }
  }

  public void Init() {
    var filters = new List<MeshFilter>();
    GetAllMeshFilters(filters, transform, isRoot: true);
    m_MeshChildren = filters.ToArray();
  }

  void Awake() {
    // ObjReader used to destroy sub-objects even after Init().
    // We don't use ObjReader, so that behavior is probably no longer relevant; verify.
    if (m_MeshChildren != null) {
      foreach (var mf in m_MeshChildren) {
        Debug.Assert(mf != null);
      }
    }
  }

  public void RegisterHighlight() {
#if !UNITY_ANDROID
    for (int i = 0; i < m_MeshChildren.Length; i++) {
      App.Instance.SelectionEffect.RegisterMesh(m_MeshChildren[i]);
    }
#endif
  }

  public void UnregisterHighlight() {
#if !UNITY_ANDROID
    for (int i = 0; i < m_MeshChildren.Length; i++) {
      App.Instance.SelectionEffect.UnregisterMesh(m_MeshChildren[i]);
    }
#endif
  }
}
}  // namespace TiltBrush
