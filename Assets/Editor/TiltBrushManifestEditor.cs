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

using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEngine;
using ReorderableList = UnityEditorInternal.ReorderableList;

namespace TiltBrush {
[CustomEditor(typeof(TiltBrushManifest))]
class TiltBrushManifestEditor : Editor {
  // Assumes that list contains object references; yields asset paths in the list.
  static IEnumerable<string> AssetPathsIn(ReorderableList list) {
    for (int i = 0; i < list.serializedProperty.arraySize; ++i) {
      var element = list.serializedProperty.GetArrayElementAtIndex(i);
      var path = AssetDatabase.GetAssetPath(element.objectReferenceValue);
      if (! string.IsNullOrEmpty(path)) {
        yield return path;
      }
    }
  }

  // Instance

  private SimpleReorderableList m_BrushList;
  private SimpleReorderableList m_EnvList;
  private SimpleReorderableList m_CBrushList;

  private void OnEnable() {
    m_BrushList = new SimpleReorderableList(serializedObject, "Brushes");
    m_BrushList.onAddDropdownCallback = (r, l) => OnAddBrushDropdown(r, l, "BrushDescriptor");
    m_BrushList.m_elementsPerPage = 12;

    m_EnvList = new SimpleReorderableList(serializedObject, "Environments");
    // Or maybe this is good enough.
    // m_EnvList.onAddCallback = (list) => { AppendAssetToList(list, null); };
    m_EnvList.onAddDropdownCallback = (r, l) => OnAddBrushDropdown(r, l, "Environment");
    m_EnvList.m_elementsPerPage = 12;

    m_CBrushList = new SimpleReorderableList(serializedObject, "CompatibilityBrushes");
    m_CBrushList.onAddDropdownCallback = (r, l) => OnAddBrushDropdown(r, l, "BrushDescriptor");
  }

  private void AppendAssetToList(ReorderableList list, string assetPath) {
    var index = list.serializedProperty.arraySize;
    list.serializedProperty.arraySize++;
    list.index = index;
    var element = list.serializedProperty.GetArrayElementAtIndex(index);
    Object value = assetPath == null ? null : AssetDatabase.LoadAssetAtPath<Object>(assetPath);
    element.objectReferenceValue = value;
    // I don't fully understand why this is necessary.
    serializedObject.ApplyModifiedProperties();
  }

  // Shows a pop-up menu consisting only of assets that are not already in the list.
  void OnAddBrushDropdown(Rect unused, ReorderableList list, string assetType) {
    // Everything that's not already in the list
    var notInList = new HashSet<string>(AssetDatabase.FindAssets("t:" + assetType)
                                        .Select(AssetDatabase.GUIDToAssetPath));
    notInList.ExceptWith(AssetPathsIn(list));

    // notInList & ~experimental
    var experimental = new HashSet<string>(
        notInList.Where(path => path.StartsWith("Assets/Resources/X/")));

    // notInList & experimental
    var notExperimental = new HashSet<string>(notInList);
    notExperimental.ExceptWith(experimental);

    var menu = new GenericMenu();
    foreach (var assetPath in notExperimental.OrderBy(p => p)) {
      menu.AddItem(
          new GUIContent("Not X/" + Path.GetFileNameWithoutExtension(assetPath)),
          false, (path) => { AppendAssetToList(list, (string)path); },
          assetPath);
    }
    foreach (var assetPath in experimental.OrderBy(p => p)) {
      menu.AddItem(
          new GUIContent("In X/" + Path.GetFileNameWithoutExtension(assetPath)),
          false, (path) => { AppendAssetToList(list, (string)path); },
          assetPath);
    }

    menu.ShowAsContext();
  }

  public override void OnInspectorGUI() {
    serializedObject.Update();
    m_BrushList.DoLayoutList();
    m_EnvList.DoLayoutList();
    m_CBrushList.DoLayoutList();
    serializedObject.ApplyModifiedProperties();
  }
}
}
