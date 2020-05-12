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

using UnityEditor;
using UnityEngine;
using System.Linq;

namespace TiltBrush {

[CustomPropertyDrawer(typeof(MultiCamAttach))]
public class MultiCamAttachDrawer : PropertyDrawer {
  Rect drawRect = new Rect();
  float baseY;

  public override float GetPropertyHeight(SerializedProperty prop, GUIContent label) {
    return base.GetPropertyHeight(prop, label) * 4.25f;
  }

  public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label) {
    // Using BeginProperty / EndProperty on the parent property means that
    // prefab override logic works on the entire property.
    EditorGUI.BeginProperty(pos, label, prop);
    // Draw label
    pos = EditorGUI.PrefixLabel(pos, GUIUtility.GetControlID(FocusType.Passive), label);
    float baseHeight = base.GetPropertyHeight(prop, label);
    baseY = pos.y;

    // Don't make child fields be indented
    var indent = EditorGUI.indentLevel;
    EditorGUI.indentLevel = 0;

    // Calculate rects
    var type = new Rect(pos.x, pos.y, 150, baseHeight);

    string styleString = new System.String(label.text.Where(System.Char.IsDigit).ToArray());
    MultiCamStyle style = (MultiCamStyle)System.Convert.ToInt32(styleString);
    EditorGUI.LabelField(type, new GUIContent(style.ToString()));

    {
      NextRect(pos.x, baseY, 150, baseHeight, baseHeight);
      EditorGUI.PropertyField(drawRect, prop.FindPropertyRelative("m_JointTransform"), GUIContent.none);
      LabelRect(pos.x + 150, baseY, 150, baseHeight);
      EditorGUI.LabelField(drawRect, new GUIContent("JointTransform"));

      NextRect(pos.x, baseY, 150, baseHeight, baseHeight);
      EditorGUI.PropertyField(drawRect, prop.FindPropertyRelative("m_OffsetTransform"), GUIContent.none);
      LabelRect(pos.x + 150, baseY, 150, baseHeight);
      EditorGUI.LabelField(drawRect, new GUIContent("OffsetTransform"));

      NextRect(pos.x, baseY, 150, baseHeight, baseHeight);
      EditorGUI.PropertyField(drawRect, prop.FindPropertyRelative("m_AttachPoint"), GUIContent.none);
      LabelRect(pos.x + 150, baseY, 150, baseHeight);
      EditorGUI.LabelField(drawRect, new GUIContent("AttachPoint"));
    }

    // Set indent back to what it was
    EditorGUI.indentLevel = indent;

    EditorGUI.EndProperty();
  }

  void NextRect(float x, float y, float width, float height, float advanceY) {
    baseY += advanceY;
    drawRect = new Rect(x, baseY, width, height);
  }

  void LabelRect(float x, float y, float width, float height) {
    drawRect = new Rect(x, baseY, width, height);
  }
}

}
