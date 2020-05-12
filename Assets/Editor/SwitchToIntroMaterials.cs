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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TiltBrush {
public class SwitchToIntroMaterials : MonoBehaviour {

  [MenuItem("Tilt/Convert to Intro Materials")]
  public static void SwitchMaterials() {
    string instance = " (Instance)";
    var introMaterials =
      AssetDatabase.FindAssets("t:Material", new[] {"Assets/Materials/IntroMaterials"})
        .Select(x => AssetDatabase.GUIDToAssetPath(x))
        .Select(x => AssetDatabase.LoadAssetAtPath<Material>(x)).ToArray();
    int succeeded = 0;
    int failed = 0;
    foreach (var batch in App.ActiveCanvas.BatchManager.AllBatches()) {
      string materialName = batch.InstantiatedMaterial.name;
      if (materialName.EndsWith(instance)) {
        materialName = materialName.Substring(0, materialName.Length - instance.Length);
      }
      string newMaterialName = $"Intro_{materialName}";
      var newMaterial = introMaterials.FirstOrDefault(x => x.name == newMaterialName);
      if (newMaterial == null) {
        Debug.LogWarning($"Could not find material {newMaterialName}");
        failed++;
      } else {
        batch.ReplaceMaterial(newMaterial);
        succeeded++;
      }
    }
    Debug.Log($"Converted {succeeded} materials and failed to convert {failed}.");
  }
}
}
