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

[CustomPropertyDrawer(typeof(Remote))]
class RemoteDrawer : PropertyDrawer
{
    // Labels and values for the control types.
    static GUIContent[] controlLabels = {
        new GUIContent("Off"),
        new GUIContent("MIDI CC"),
        new GUIContent("MIDI Note"),
        new GUIContent("Input Axis")
    };
    static int[] controlValues = { 0, 1, 2, 3 };

    int GetRowCount(SerializedProperty property)
    {
        // Fully expand if it has multiple mode values.
        var propControl = property.FindPropertyRelative("_control");
        if (propControl.hasMultipleDifferentValues) return 6;

        // Uses four rows when it has MIDI options.
        var control = (Remote.Control)propControl.intValue;

        // Expand if it's enabled.
        return control > 0 ? 3 : 1;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var rowCount = GetRowCount(property);
        return EditorGUIUtility.singleLineHeight * rowCount +
               EditorGUIUtility.standardVerticalSpacing * (rowCount - 1);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        position.height = EditorGUIUtility.singleLineHeight;

        // First line: Control selector.
        var propControl = property.FindPropertyRelative("_control");
        EditorGUI.IntPopup(position, propControl, controlLabels, controlValues, label);

        var nextLine = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        position.y += nextLine;

        // Indent the line.
        var indent = 16;
        position.width -= indent;
        position.x += indent;
        EditorGUIUtility.labelWidth -= indent;

        // MIDI channel selector.
        var control = (Remote.Control)propControl.intValue;
        if (propControl.hasMultipleDifferentValues )
        {
            EditorGUI.PropertyField(position, property.FindPropertyRelative("_midiChannel"), new GUIContent("MIDI Channel"));
            position.y += nextLine;
        }

        // Input axis name box.
        if (propControl.hasMultipleDifferentValues || control == Remote.Control.InputAxis)
        {
            EditorGUI.PropertyField(position, property.FindPropertyRelative("_inputAxis"));
            position.y += nextLine;
        }

        // Curve editor.
        if (propControl.hasMultipleDifferentValues || control != Remote.Control.Off)
            EditorGUI.PropertyField(position, property.FindPropertyRelative("_curve"));

        EditorGUI.EndProperty();
    }
}

} // namespace Reaktion
