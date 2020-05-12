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
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;

namespace TiltBrush {

class BrushEditor {
  public static T CreateAsset<T>(string defaultFolder = "Assets", string basename = null)
      where T : ScriptableObject {
    T asset = ScriptableObject.CreateInstance<T>();

    if (basename == null) {
      basename = string.Format("New {0}.asset", typeof(T).Name);
    } else {
      basename += ".asset";
    }

    string folder = AssetDatabase.GetAssetPath(Selection.activeObject);
    if (string.IsNullOrEmpty(folder)) {
      folder = defaultFolder;
    } else if (! Directory.Exists(folder)) {
      folder = Path.GetDirectoryName(folder);
    }

    string fullPath = AssetDatabase.GenerateUniqueAssetPath(
        Path.Combine(folder, basename));
    AssetDatabase.CreateAsset(asset, fullPath);
    AssetDatabase.SaveAssets();
    EditorUtility.FocusProjectWindow();
    Selection.activeObject = asset;

    return asset;
  }

  [MenuItem("Tilt/New Brush")]
  static void MenuItem_CreateBrushDescriptor() {
    var asset = CreateAsset<BrushDescriptor>("Assets/Resources/Brushes");
    string guidTxt = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
    asset.m_Guid = new Guid(guidTxt);
  }

  [MenuItem("Tilt/New Environment")]
  static void MenuItem_CreateEnvironment() {
    var asset = CreateAsset<Environment>("Assets/Resources/Environments");
    string guidTxt = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
    asset.m_Guid = new Guid(guidTxt);
  }
}

}
