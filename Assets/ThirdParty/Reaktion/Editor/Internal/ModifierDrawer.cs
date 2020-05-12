//
// Reaktion - An audio reactive animation toolkit for Unity.
//
// Copyright (C) 2013, 2014 Keijiro Takahashi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using UnityEngine;
using UnityEditor;
using System.Collections;

namespace Reaktion {

[CustomPropertyDrawer(typeof(Modifier))]
class ModifierDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var height = EditorGUIUtility.singleLineHeight;

        // Expand (double the height and add space) if enabled.
        var propEnabled = property.FindPropertyRelative("enabled");
        if (propEnabled.hasMultipleDifferentValues || propEnabled.boolValue)
            height = height * 2 + EditorGUIUtility.standardVerticalSpacing;

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var propEnabled = property.FindPropertyRelative("enabled");
        var expand = propEnabled.hasMultipleDifferentValues || propEnabled.boolValue;

        // First line: enabler switch.
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, propEnabled, label);

        if (expand)
        {
            // Go to the next line.
            position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // Indent the line.
            position.width -= 16;
            position.x += 16;

            // Shorten the labels.
            EditorGUIUtility.labelWidth = 26;

            // Split into the three areas.
            position.width = (position.width - 8) / 3;

            // "Min"
            EditorGUI.PropertyField(position, property.FindPropertyRelative("min"), new GUIContent("Min"));

            // "Max"
            position.x += position.width + 4;
            EditorGUI.PropertyField(position, property.FindPropertyRelative("max"), new GUIContent("Max"));

            // Curve (no label)
            position.x += position.width + 4;
            EditorGUI.PropertyField(position, property.FindPropertyRelative("curve"), GUIContent.none);
        }

        EditorGUI.EndProperty();
    }
}

} // namespace Reaktion
