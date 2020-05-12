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
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TiltBrush {

#if UNITY_EDITOR
// See http://docs.unity3d.com/ScriptReference/PropertyDrawer.html
[CustomPropertyDrawer(typeof(SerializableGuid))]
public class SerializableGuidDrawer : PropertyDrawer {
  static bool DO_VALIDATION = true;
  public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
    EditorGUI.BeginProperty(position, label, property);
    position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
    var indent = EditorGUI.indentLevel;
    EditorGUI.indentLevel = 0;

    var storageProp = property.FindPropertyRelative("m_storage");
    if (DO_VALIDATION) {
      var oldval = storageProp.stringValue;
      var newval = EditorGUI.DelayedTextField(position, oldval);
      if (oldval != newval) {
        try {
          storageProp.stringValue = new System.Guid(newval).ToString("D");
        } catch (System.FormatException) {}
      }
    } else {
      EditorGUI.PropertyField(position, storageProp, GUIContent.none);
    }
    EditorGUI.indentLevel = indent;
    EditorGUI.EndProperty();
  }
}
#endif

/// Mostly a drop-in replacement for System.Guid.
/// Adds the expense of conversions to/from native System.Guid,
/// but enables Unity serialization.
[System.Serializable]
public struct SerializableGuid : IFormattable {
  [SerializeField]
  private string m_storage;

  public static implicit operator SerializableGuid(System.Guid rhs) {
    return new SerializableGuid { m_storage = rhs.ToString("D") };
  }

  public static implicit operator System.Guid(SerializableGuid rhs) {
    if (rhs.m_storage == null) {
      return System.Guid.Empty;
    }

    try {
      return new System.Guid(rhs.m_storage);
    } catch (System.FormatException) {
      return System.Guid.Empty;
    }
  }

  public override string ToString() {
    return ToString("D");
  }

  public string ToString(string format, IFormatProvider provider=null) {
    return ((System.Guid)this).ToString(format);
  }
}

}  // namespace TiltBrush