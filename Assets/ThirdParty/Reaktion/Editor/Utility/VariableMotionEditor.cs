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

// Custom property drawer for TransformElement.
[CustomPropertyDrawer(typeof(VariableMotion.TransformElement))]
class VariableMotionElementDrawer : PropertyDrawer
{
    // Labels and values for TransformMode.
    static GUIContent[] modeLabels = {
        new GUIContent("Off"),
        new GUIContent("X Axis"),
        new GUIContent("Y Axis"),
        new GUIContent("Z Axis"),
        new GUIContent("Arbitrary Vector"),
        new GUIContent("Random Vector")
    };
    static int[] modeValues = { 0, 1, 2, 3, 4, 5 };

    static int GetExpansionLevel(SerializedProperty property)
    {
        var mode = property.FindPropertyRelative("mode");
        // Fully expand if it has different values.
        if (mode.hasMultipleDifferentValues) return 2;
        // "Off"
        if (mode.enumValueIndex == 0) return 0;
        // Fully expand if it's in Arbitrary mode.
        if (mode.enumValueIndex == (int)ConstantMotion.TransformMode.Arbitrary) return 2;
        // Expand one level.
        return 1;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int rowCount = new int[]{1, 5, 6}[GetExpansionLevel(property)];
        return EditorGUIUtility.singleLineHeight * rowCount +
               EditorGUIUtility.standardVerticalSpacing * (rowCount - 1);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        position.height = EditorGUIUtility.singleLineHeight;
        var rowHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        // Transform mode selector drop-down.
        EditorGUI.IntPopup(position, property.FindPropertyRelative("mode"), modeLabels, modeValues, label);
        position.y += rowHeight;

        var expansion = GetExpansionLevel(property);
        if (expansion > 0)
        {
            // Insert an indent.
            position.x += 16;
            position.width -= 16;
            EditorGUIUtility.labelWidth -= 16;

            if (expansion == 2)
            {
                // Vector box.
                EditorGUI.PropertyField(position, property.FindPropertyRelative("arbitraryVector"), GUIContent.none);
                position.y += rowHeight;
            }

            // Curve.
            EditorGUI.PropertyField(position, property.FindPropertyRelative("curve"), new GUIContent("Curve"));
            position.y += rowHeight;

            // Amplitude box.
            EditorGUI.PropertyField(position, property.FindPropertyRelative("amplitude"), new GUIContent("Amplitude"));
            position.y += rowHeight;

            // Time scale box.
            EditorGUI.Slider(position, property.FindPropertyRelative("speed"), 0, 5, new GUIContent("Speed"));
            position.y += rowHeight;

            // Randomness slider.
            EditorGUI.Slider(position, property.FindPropertyRelative("randomness"), 0, 1, new GUIContent("Randomness"));
        }

        EditorGUI.EndProperty();
    }
}

[CustomEditor(typeof(VariableMotion)), CanEditMultipleObjects]
public class VariableMotionEditor : Editor
{
    SerializedProperty propPosition;
    SerializedProperty propRotation;
    SerializedProperty propScale;

    SerializedProperty propUseLocalCoordinate;
    SerializedProperty propUseDifferentials;

    void OnEnable()
    {
        propPosition = serializedObject.FindProperty("position");
        propRotation = serializedObject.FindProperty("rotation");
        propScale    = serializedObject.FindProperty("scale");

        propUseLocalCoordinate = serializedObject.FindProperty("useLocalCoordinate");
        propUseDifferentials   = serializedObject.FindProperty("useDifferentials");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(propPosition);
        EditorGUILayout.PropertyField(propRotation);
        EditorGUILayout.PropertyField(propScale);

        EditorGUILayout.PropertyField(propUseLocalCoordinate, new GUIContent("Local Coordinate"));
        EditorGUILayout.PropertyField(propUseDifferentials);

        serializedObject.ApplyModifiedProperties();
    }
}

} // namespace Reaktion
