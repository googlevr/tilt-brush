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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using USD.NET;
using Object = UnityEngine.Object;

namespace TiltBrush {
public class FindAllTextMeshes {
  // Uncomment to use this
  // [MenuItem("Tilt/Find all Text Meshes")]
  public static void Find() {
    FindAllInScene();
    FindAllPrefabs();
  }

  public static void FindAllInScene() {
    var scene = SceneManager.GetActiveScene();
    List<GameObject> objects = new List<GameObject>(scene.rootCount + 2);
    scene.GetRootGameObjects(objects);
    foreach (var obj in objects) {
      FindAllInGameObject("scene", obj);
    }
  }

  public static void FindAllPrefabs() {
    var prefabs = AssetDatabase.FindAssets("t:prefab")
      .Select(x => AssetDatabase.GUIDToAssetPath(x)).ToArray();
    foreach (var prefabPath in prefabs) {
      var gobj = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
      FindAllInGameObject(prefabPath, gobj);
    }
  }

  public static void FindAllInGameObject(string origin, GameObject gobj) {
    var findings = new List<(string, string)>();
    findings.AddRange(FindTextInGameObject<TextMesh>(gobj, x => x.text));
    findings.AddRange(FindTextInGameObject<TextMeshPro>(gobj, x => x.text));
    foreach (var (name, text) in findings) {
      Debug.Log($"In {origin}: {name} \"{text}\"");
    }
  }
  
  public static IEnumerable<(string, string)> 
    FindTextInGameObject<T>(GameObject gobj, Func<T, string> extractor) where T : Component {
    var components = gobj.GetComponentsInChildren<T>();
    foreach (var component in components) {
      string text = extractor?.Invoke(component);
      if (text.Contains("Tilt Brush") || text.Contains("Google")) {
        yield return (Path(component.transform), text);
      }
    }
  }

  public static string Path(Transform xform) {
    if (xform == null) {
      return "";
    }
    return $"{Path(xform.parent)}/{xform.name}";
  }
}
}
