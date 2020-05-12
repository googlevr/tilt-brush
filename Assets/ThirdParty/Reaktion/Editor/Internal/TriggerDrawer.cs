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

[CustomPropertyDrawer(typeof(Trigger))]
class TriggerDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var height = EditorGUIUtility.singleLineHeight;

        // Expand if enabled.
        var propEnabled = property.FindPropertyRelative("enabled");
        if (propEnabled.hasMultipleDifferentValues || propEnabled.boolValue)
            height = height * 3 + EditorGUIUtility.standardVerticalSpacing * 2;

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
            EditorGUIUtility.labelWidth -= 16;

            // "Threshold"
            EditorGUI.Slider(position, property.FindPropertyRelative("threshold"), 0.01f, 0.99f, new GUIContent("Threshold"));
            position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // "Minimum Interval"
            EditorGUI.PropertyField(position, property.FindPropertyRelative("interval"), new GUIContent("Minimum Interval"));
        }

        EditorGUI.EndProperty();
    }
}

} // namespace Reaktion
