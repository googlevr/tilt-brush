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

[CustomEditor(typeof(SelfDestruction)), CanEditMultipleObjects]
public class SelfDestructionEditor : Editor
{
    SerializedProperty propConditionType;
    SerializedProperty propReferenceType;

    SerializedProperty propMaxDistance;
    SerializedProperty propBounds;
    SerializedProperty propLifetime;

    SerializedProperty propReferencePoint;
    SerializedProperty propReferenceObject;
    SerializedProperty propReferenceName;

    GUIContent labelConditionType;
    GUIContent labelReferenceType;
    GUIContent labelReferenceObject;
    GUIContent labelReferenceName;

    GUIContent[] conditionTypeLabels = {
        new GUIContent("Distance"),
        new GUIContent("Bounding Box"),
        new GUIContent("Time"),
        new GUIContent("Particle System Liveness")
    };

    int[] conditionTypeValues = { 0, 1, 2, 3 };

    GUIContent[] referenceTypeLabels = {
        new GUIContent("Origin"),
        new GUIContent("Fixed Point"),
        new GUIContent("Initial Position"),
        new GUIContent("Game Object"),
        new GUIContent("Game Object Name")
    };

    int[] referenceTypeValues = { 0, 1, 2, 3, 4 };

    void OnEnable()
    {
        propConditionType = serializedObject.FindProperty("conditionType");
        propReferenceType = serializedObject.FindProperty("referenceType");

        propMaxDistance = serializedObject.FindProperty("maxDistance");
        propBounds      = serializedObject.FindProperty("bounds");
        propLifetime    = serializedObject.FindProperty("lifetime");

        propReferencePoint  = serializedObject.FindProperty("referencePoint");
        propReferenceObject = serializedObject.FindProperty("referenceObject");
        propReferenceName   = serializedObject.FindProperty("referenceName");

        labelConditionType   = new GUIContent("Condition");
        labelReferenceType   = new GUIContent("Reference Point");
        labelReferenceObject = new GUIContent("Game Object");
        labelReferenceName   = new GUIContent("Name");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.IntPopup(propConditionType, conditionTypeLabels, conditionTypeValues, labelConditionType);

        if (propConditionType.hasMultipleDifferentValues ||
            propConditionType.enumValueIndex == (int)SelfDestruction.ConditionType.Distance)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(propMaxDistance);
            EditorGUI.indentLevel--;
        }

        if (propConditionType.hasMultipleDifferentValues ||
            propConditionType.enumValueIndex == (int)SelfDestruction.ConditionType.Bounds)
            EditorGUILayout.PropertyField(propBounds, GUIContent.none);

        if (propConditionType.hasMultipleDifferentValues ||
            propConditionType.enumValueIndex == (int)SelfDestruction.ConditionType.Time)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(propLifetime);
            EditorGUI.indentLevel--;
        }

        if (propConditionType.hasMultipleDifferentValues ||
            (propConditionType.enumValueIndex != (int)SelfDestruction.ConditionType.ParticleSystem &&
             propConditionType.enumValueIndex != (int)SelfDestruction.ConditionType.Time))
        {
            EditorGUILayout.IntPopup(propReferenceType, referenceTypeLabels, referenceTypeValues, labelReferenceType);

            EditorGUI.indentLevel++;

            if (propReferenceType.hasMultipleDifferentValues ||
                propReferenceType.enumValueIndex == (int)SelfDestruction.ReferenceType.Point)
                EditorGUILayout.PropertyField(propReferencePoint, GUIContent.none);

            if (propReferenceType.hasMultipleDifferentValues ||
                propReferenceType.enumValueIndex == (int)SelfDestruction.ReferenceType.GameObject)
                EditorGUILayout.PropertyField(propReferenceObject, labelReferenceObject);

            if (propReferenceType.hasMultipleDifferentValues ||
                propReferenceType.enumValueIndex == (int)SelfDestruction.ReferenceType.GameObjectName)
                EditorGUILayout.PropertyField(propReferenceName, labelReferenceName);

            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}

} // namespace Reaktion
