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

[CustomEditor(typeof(JitterMotionGear)), CanEditMultipleObjects]
public class JitterMotionGearEditor : Editor
{
    SerializedProperty propReaktor;
    SerializedProperty propPositionFrequency;
    SerializedProperty propRotationFrequency;
    SerializedProperty propPositionAmount;
    SerializedProperty propRotationAmount;
    GUIContent labelPositionAmount;
    GUIContent labelRotationAmount;
    GUIContent labelFrequency;

    void OnEnable()
    {
        propReaktor  = serializedObject.FindProperty("reaktor");
        propPositionFrequency = serializedObject.FindProperty("positionFrequency");
        propRotationFrequency = serializedObject.FindProperty("rotationFrequency");
        propPositionAmount = serializedObject.FindProperty("positionAmount");
        propRotationAmount = serializedObject.FindProperty("rotationAmount");
        labelPositionAmount = new GUIContent("Position Noise");
        labelRotationAmount = new GUIContent("Rotation Noise");
        labelFrequency = new GUIContent("Frequency");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(propReaktor);

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(propPositionAmount, labelPositionAmount);
        EditorGUILayout.PropertyField(propPositionFrequency, labelFrequency);

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(propRotationAmount, labelRotationAmount);
        EditorGUILayout.PropertyField(propRotationFrequency, labelFrequency);

        serializedObject.ApplyModifiedProperties();
    }
}

} // namespace Reaktion
