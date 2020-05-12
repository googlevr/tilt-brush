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

//using NUnit.Framework;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace TiltBrush {

  public static class GameObjectToPrefab{
  // Variant of SaveAsSinglePrefab() from the Poly Toolkit.
  // Converts a selected game object into a prefab.

  // When replacing an existing prefab:
  // - scene objects that reference the prefab get properly updated
  // - Unless explicitly destroyed, old meshes stick around in the prefab and are
  //   not replaced (so the prefab has many meshes with duplicate names)
  // - Scene objects that reference mesh sub-assets keep referencing the same mesh
  //   (if they are left around) or get dangling mesh references (if they are destroyed)
  //   There is no known way to replace the existing meshes by name or otherwise,
  //   unless we implement it manually (eg by mutating the mesh sub-assets)
  //
  static void SaveGameObjectAsPrefab(
    GameObject targetGameObject, string prefabPath) {

    Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
    GameObject oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    GameObject prefab;
    MeshFilter[] meshFilters = targetGameObject.GetComponentsInChildren<MeshFilter>();
    List<Mesh> meshes = new List<Mesh>();
    for (int i = 0; i < meshFilters.Length; i++) {
      meshes.Add(meshFilters[i].mesh);
    }

    if (oldPrefab == null) {
      // Chicken and egg problem: the Meshes aren't assets yet, so refs to them will dangle
      prefab = PrefabUtility.CreatePrefab(prefabPath, targetGameObject);
      foreach (var mesh in meshes) {
        AssetDatabase.AddObjectToAsset(mesh, prefab);
      }
      // This fixes up the dangling refs
      prefab = PrefabUtility.ReplacePrefab(targetGameObject, prefab);
    } else {
      // ReplacePrefab only removes the GameObjects from the asset.
      // Clear out all non-prefab junk (ie, meshes), because otherwise it piles up.
      // The main difference between LoadAllAssetRepresentations and LoadAllAssets
      // is that the former returns MonoBehaviours and the latter does not.
      foreach (var obj in AssetDatabase.LoadAllAssetRepresentationsAtPath(prefabPath)) {
        if (!(obj is GameObject)) {
          Object.DestroyImmediate(obj, allowDestroyingAssets: true);
        }
      }

      foreach (var mesh in meshes) {
        AssetDatabase.AddObjectToAsset(mesh, oldPrefab);
      }
      prefab = PrefabUtility.ReplacePrefab(
          targetGameObject, oldPrefab, ReplacePrefabOptions.ReplaceNameBased);
    }

    AssetDatabase.ImportAsset(prefabPath);
  }

  [MenuItem("Tilt/Save Game Object As Prefab")]
  public static void SaveGameObjectAsSinglePrefab() {

    if (!Application.isPlaying) {
      Debug.Log("'Save Game Object As Prefab' only works when unity is in 'Play' mode.");
      return;
    }

    GameObject result = Selection.gameObjects[0];
    string prefabPath = "Assets/TestData/gameobject_to_prefab.prefab";
    SaveGameObjectAsPrefab(result, prefabPath);
    Debug.Log("Saved " + result.name + "to Assets/TestData/gameobject_to_prefab.prefab");
    Object.DestroyImmediate(result);
  }
}
}
