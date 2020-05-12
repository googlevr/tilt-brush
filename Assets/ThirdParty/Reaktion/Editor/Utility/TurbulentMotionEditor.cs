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

[CustomEditor(typeof(TurbulentMotion)), CanEditMultipleObjects]
public class TurbulentMotionEditor : Editor
{
    SerializedProperty propDensity;
    SerializedProperty propLinearFlow;

    SerializedProperty propDisplacement;
    SerializedProperty propRotation;
    SerializedProperty propScale;
    SerializedProperty propCoeffDisplacement;
    SerializedProperty propCoeffRotation;
    SerializedProperty propCoeffScale;

    SerializedProperty propUseLocalCoordinate;

    GUIContent labelAmplitude;
    GUIContent labelWaveNumber;
    GUIContent labelInfluence;
    GUIContent labelLocalCoordinate;
    
    void OnEnable()
    {
        propDensity    = serializedObject.FindProperty("density");
        propLinearFlow = serializedObject.FindProperty("linearFlow");

        propDisplacement      = serializedObject.FindProperty("displacement");
        propRotation          = serializedObject.FindProperty("rotation");
        propScale             = serializedObject.FindProperty("scale");
        propCoeffDisplacement = serializedObject.FindProperty("coeffDisplacement");
        propCoeffRotation     = serializedObject.FindProperty("coeffRotation");
        propCoeffScale        = serializedObject.FindProperty("coeffScale");

        propUseLocalCoordinate = serializedObject.FindProperty("useLocalCoordinate");

        labelAmplitude       = new GUIContent("Amplitude");
        labelWaveNumber      = new GUIContent("Wave Number");
        labelInfluence       = new GUIContent("Influence (≦1.0)");
        labelLocalCoordinate = new GUIContent("Local Coordinate");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Noise");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(propDensity);
        EditorGUILayout.PropertyField(propLinearFlow);
        EditorGUI.indentLevel--;

        EditorGUILayout.LabelField("Displacement");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(propDisplacement, labelAmplitude);
        EditorGUILayout.PropertyField(propCoeffDisplacement, labelWaveNumber);
        EditorGUI.indentLevel--;

        EditorGUILayout.LabelField("Rotation (Euler)");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(propRotation, labelAmplitude);
        EditorGUILayout.PropertyField(propCoeffRotation, labelWaveNumber);
        EditorGUI.indentLevel--;

        EditorGUILayout.LabelField("Scale");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(propScale, labelInfluence);
        EditorGUILayout.PropertyField(propCoeffScale, labelWaveNumber);
        EditorGUI.indentLevel--;

        EditorGUILayout.PropertyField(propUseLocalCoordinate, labelLocalCoordinate);

        serializedObject.ApplyModifiedProperties();
    }
}

} // namespace Reaktion
