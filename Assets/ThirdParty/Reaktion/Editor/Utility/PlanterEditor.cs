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

[CustomEditor(typeof(Planter)), CanEditMultipleObjects]
public class PlanterEditor : Editor
{
    SerializedProperty propPrefabs;
    SerializedProperty propMaxObjects;
    SerializedProperty propDistributionMode;
    SerializedProperty propDistributionRange;
    SerializedProperty propGridSpace;
    SerializedProperty propRotationMode;
    SerializedProperty propIntervalMode;
    SerializedProperty propInterval;

    void OnEnable()
    {
        propPrefabs           = serializedObject.FindProperty("prefabs");
        propMaxObjects        = serializedObject.FindProperty("_maxObjects");
        propDistributionMode  = serializedObject.FindProperty("distributionMode");
        propDistributionRange = serializedObject.FindProperty("_distributionRange");
        propGridSpace         = serializedObject.FindProperty("_gridSpace");
        propRotationMode      = serializedObject.FindProperty("rotationMode");
        propIntervalMode      = serializedObject.FindProperty("intervalMode");
        propInterval          = serializedObject.FindProperty("_interval");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(propPrefabs, true);
        EditorGUILayout.PropertyField(propMaxObjects);

        EditorGUILayout.PropertyField(propDistributionMode);
        if (propDistributionMode.hasMultipleDifferentValues ||
            propDistributionMode.enumValueIndex != (int)Planter.DistributionMode.Single)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(propDistributionRange, GUIContent.none);
            EditorGUI.indentLevel--;
        }

        if (propDistributionMode.hasMultipleDifferentValues ||
            propDistributionMode.enumValueIndex == (int)Planter.DistributionMode.Grid)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(propGridSpace);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(propRotationMode);

        EditorGUILayout.PropertyField(propIntervalMode);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(propInterval);
        EditorGUI.indentLevel--;

        serializedObject.ApplyModifiedProperties();
    }
}

} // namespace Reaktion
