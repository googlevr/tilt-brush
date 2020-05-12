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

[CustomPropertyDrawer(typeof(PanelMapKey))]
public class PanelMapKeyDrawer : PropertyDrawer {
  Rect drawRect = new Rect();
  float baseX;

  public override float GetPropertyHeight(SerializedProperty prop, GUIContent label) {
    return base.GetPropertyHeight(prop, label) * 2.25f;
  }

  public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label) {
    // Using BeginProperty / EndProperty on the parent property means that
    // prefab override logic works on the entire property.
    EditorGUI.BeginProperty(pos, label, prop);
    // Draw label
    pos = EditorGUI.PrefixLabel(pos, GUIUtility.GetControlID(FocusType.Passive), label);
    float baseHeight = base.GetPropertyHeight(prop, label);
    baseX = pos.x;

    // Don't make child fields be indented
    var indent = EditorGUI.indentLevel;
    EditorGUI.indentLevel = 0;

    // Calculate rects
    var type = new Rect(baseX, pos.y, 150, baseHeight);
    var prefab = new Rect(baseX + 155, pos.y, 150, baseHeight);

    string panelTypeString = new System.String(label.text.Where(System.Char.IsDigit).ToArray());
    BasePanel.PanelType panelType = (BasePanel.PanelType)System.Convert.ToInt32(panelTypeString);
    EditorGUI.LabelField(type, new GUIContent(panelType.ToString()));

    if (panelType != BasePanel.PanelType.SketchSurface) {
      EditorGUI.PropertyField(prefab, prop.FindPropertyRelative("m_PanelPrefab"), GUIContent.none);

      pos.y += baseHeight;

      NextRect(baseX, pos.y, 10, baseHeight, 12);
      EditorGUI.PropertyField(drawRect, prop.FindPropertyRelative("m_ModeVr"), GUIContent.none);
      NextRect(baseX, pos.y, 30, baseHeight, 30);
      EditorGUI.LabelField(drawRect, new GUIContent("VR"));

      NextRect(baseX, pos.y, 10, baseHeight, 12);
      EditorGUI.PropertyField(drawRect, prop.FindPropertyRelative("m_ModeVrExperimental"),
          GUIContent.none);
      NextRect(baseX, pos.y, 30, baseHeight, 30);
      EditorGUI.LabelField(drawRect, new GUIContent("Exp"));

      NextRect(baseX, pos.y, 10, baseHeight, 12);
      EditorGUI.PropertyField(drawRect, prop.FindPropertyRelative("m_ModeMono"), GUIContent.none);
      NextRect(baseX, pos.y, 30, baseHeight, 30);
      EditorGUI.LabelField(drawRect, new GUIContent("Mo", "Monoscopic"));

      NextRect(baseX, pos.y, 10, baseHeight, 12);
      EditorGUI.PropertyField(drawRect, prop.FindPropertyRelative("m_ModeQuest"),
          GUIContent.none);
      NextRect(baseX, pos.y, 30, baseHeight, 30);
      EditorGUI.LabelField(drawRect, new GUIContent("OQ", "Oculus Quest"));

      NextRect(baseX, pos.y, 10, baseHeight, 12);
      EditorGUI.PropertyField(drawRect, prop.FindPropertyRelative("m_ModeGvr"), GUIContent.none);
      NextRect(baseX, pos.y, 30, baseHeight, 30);
      EditorGUI.LabelField(drawRect, new GUIContent("GVR"));

      NextRect(baseX, pos.y, 10, baseHeight, 12);
      EditorGUI.PropertyField(drawRect, prop.FindPropertyRelative("m_Basic"), GUIContent.none);
      NextRect(baseX, pos.y, 40, baseHeight, 40);
      EditorGUI.LabelField(drawRect, new GUIContent("Basic"));

      NextRect(baseX, pos.y, 10, baseHeight, 12);
      EditorGUI.PropertyField(drawRect, prop.FindPropertyRelative("m_Advanced"), GUIContent.none);
      NextRect(baseX, pos.y, 60, baseHeight, 60);
      EditorGUI.LabelField(drawRect, new GUIContent("Advanced"));
    }

    // Set indent back to what it was
    EditorGUI.indentLevel = indent;

    EditorGUI.EndProperty();
  }

  void NextRect(float x, float y, float width, float height, float advanceX) {
    drawRect = new Rect(x, y, width, height);
    baseX += advanceX;
  }
}

}
