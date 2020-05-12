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

[CustomEditor(typeof(MaterialGear)), CanEditMultipleObjects]
public class MaterialGearEditor : Editor
{
    SerializedProperty propReaktor;
    SerializedProperty propMaterialIndex;
    SerializedProperty propTargetType;
    SerializedProperty propTargetName;
    SerializedProperty propThreshold;
    SerializedProperty propColorGradient;
    SerializedProperty propColorGradientMultiplier;
    SerializedProperty propFloatCurve;
    SerializedProperty propVectorFrom;
    SerializedProperty propVectorTo;
    SerializedProperty propTextureLow;
    SerializedProperty propTextureHigh;

    GUIContent labelFrom;
    GUIContent labelTo;
    GUIContent labelLow;
    GUIContent labelHigh;

    void OnEnable()
    {
        propReaktor       = serializedObject.FindProperty("reaktor");
        propMaterialIndex = serializedObject.FindProperty("materialIndex");
        propTargetType    = serializedObject.FindProperty("targetType");
        propTargetName    = serializedObject.FindProperty("targetName");
        propThreshold     = serializedObject.FindProperty("threshold");
        propColorGradient = serializedObject.FindProperty("colorGradient");
        propColorGradientMultiplier = serializedObject.FindProperty("colorGradientMultiplier");
        propFloatCurve    = serializedObject.FindProperty("floatCurve");
        propVectorFrom    = serializedObject.FindProperty("vectorFrom");
        propVectorTo      = serializedObject.FindProperty("vectorTo");
        propTextureLow    = serializedObject.FindProperty("textureLow");
        propTextureHigh   = serializedObject.FindProperty("textureHigh");

        labelFrom = new GUIContent("From");
        labelTo   = new GUIContent("To");
        labelLow  = new GUIContent("Low");
        labelHigh = new GUIContent("High");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(propReaktor);
        EditorGUILayout.PropertyField(propMaterialIndex);
        EditorGUILayout.PropertyField(propTargetType);
        EditorGUILayout.PropertyField(propTargetName);

        if (!propTargetType.hasMultipleDifferentValues &&
            propTargetType.enumValueIndex == (int)MaterialGear.TargetType.Color)
        {
            EditorGUILayout.PropertyField(propColorGradient);
            EditorGUILayout.PropertyField(propColorGradientMultiplier);
      }

        if (!propTargetType.hasMultipleDifferentValues &&
            propTargetType.enumValueIndex == (int)MaterialGear.TargetType.Float)
        {
            EditorGUILayout.PropertyField(propFloatCurve);
        }

        if (!propTargetType.hasMultipleDifferentValues &&
            propTargetType.enumValueIndex == (int)MaterialGear.TargetType.Vector)
        {
            EditorGUILayout.PropertyField(propVectorFrom, labelFrom, true);
            EditorGUILayout.PropertyField(propVectorTo, labelTo, true);
        }

        if (!propTargetType.hasMultipleDifferentValues &&
            propTargetType.enumValueIndex == (int)MaterialGear.TargetType.Texture)
        {
            EditorGUILayout.PropertyField(propThreshold);
            EditorGUILayout.PropertyField(propTextureLow, labelLow);
            EditorGUILayout.PropertyField(propTextureHigh, labelHigh);
        }

        serializedObject.ApplyModifiedProperties();
    }
}

} // namespace Reaktion
