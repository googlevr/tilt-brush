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

using System.Reflection;
using System.Collections;
using UnityEditor;
using UnityEngine;

using UObject = UnityEngine.Object;

// Select one or more objects in the Hierarchy.
// Hit the button.
// Console will tell you what (if anything) references those objects.
// Clicking the console lines will show you the source object.
public class ReferenceFinder : EditorWindow {
  private Component m_object;
  bool m_specificComponent = false;

  [MenuItem("Window/TB Object References")]
  static void OpenWindow() {
    EditorWindow.GetWindow<ReferenceFinder>().Show();
  }

  public void OnGUI() {
    m_specificComponent = GUILayout.Toggle(
        m_specificComponent,
        "Match a single specific component");
    if (m_specificComponent) {
      m_object = EditorGUILayout.ObjectField(
          "Component referenced : ", m_object, typeof(Component), true) as Component;
      if (m_object == null) {
        return;
      }

      if (GUILayout.Button("Find References")) {
        DumpRefsTo(m_object);
      }
    } else if (GUILayout.Button("Find Objects Referencing Selected GameObjects")) {
      GameObject[] objects = Selection.gameObjects;
      if (objects==null || objects.Length < 1) {
        GUILayout.Label("Select source objects in Hierarchy.");
        return;
      }
      foreach (GameObject go in objects) {
        DumpRefsTo(go);
        foreach (Component c in go.GetComponents(typeof(Component))) {
          DumpRefsTo(c);
        }
      }
    }
  }

  private static void DumpRefsTo(UObject target) {
    Component[] allComponents = Resources.FindObjectsOfTypeAll<Component>();
    if (allComponents == null) { return; }

    foreach (Component c in allComponents) {
      FieldInfo[] fields = c.GetType().GetFields((
            BindingFlags.NonPublic
          | BindingFlags.Public
          | BindingFlags.Instance
          | BindingFlags.Static));
      foreach (FieldInfo fieldInfo in fields) {
        if (References(c, fieldInfo, target)) {
          Debug.LogFormat(
              c.gameObject,
              "\"{0} {1} {2}\" references \"{3} {4}\"",
              c.GetType(), c.name, fieldInfo.Name, target.GetType(), target.name);
        }
      }
    }
  }

  // Returns true if any of the elements in enumerable == target
  private static bool References(IEnumerable enumerable, UObject target) {
    foreach (object elt in enumerable) {
      if (object.ReferenceEquals(elt, target)) {
        return true;
      }
    }
    return false;
  }

  // Returns true if obj.*fieldInfo == target
  // Also handles cases where obj.*fieldInfo is some sort of list
  static bool References(object obj, FieldInfo fieldInfo, UObject target) {
    var val = fieldInfo.GetValue(obj);

    var enumerable = val as ICollection;
    if (enumerable != null) {
      return References(enumerable, target);
    }

    return ReferenceEquals(val, target);
  }
}
