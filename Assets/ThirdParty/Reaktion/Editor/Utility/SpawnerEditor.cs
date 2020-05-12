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

[CustomEditor(typeof(Spawner)), CanEditMultipleObjects]
public class SpawnerEditor : Editor
{
    SerializedProperty propPrefabs;
    SerializedProperty propSpawnRate;
    SerializedProperty propSpawnRateRandomness;
    SerializedProperty propDistribution;
    SerializedProperty propSphereRadius;
    SerializedProperty propBoxSize;
    SerializedProperty propSpawnPoints;
    SerializedProperty propRandomRotation;
    SerializedProperty propParent;

    static GUIContent[] distributionLabels = {
        new GUIContent("In Sphere"),
        new GUIContent("In Box"),
        new GUIContent("At Points")
    };

    static int[] distributionValues = { 0, 1, 2 };

    void OnEnable()
    {
        propPrefabs             = serializedObject.FindProperty("prefabs");
        propSpawnRate           = serializedObject.FindProperty("spawnRate");
        propSpawnRateRandomness = serializedObject.FindProperty("spawnRateRandomness");
        propDistribution        = serializedObject.FindProperty("distribution");
        propSphereRadius        = serializedObject.FindProperty("sphereRadius");
        propBoxSize             = serializedObject.FindProperty("boxSize");
        propSpawnPoints         = serializedObject.FindProperty("spawnPoints");
        propRandomRotation      = serializedObject.FindProperty("randomRotation");
        propParent              = serializedObject.FindProperty("parent");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(propPrefabs, true);

        EditorGUILayout.PropertyField(propSpawnRate);
        EditorGUILayout.Slider(propSpawnRateRandomness, 0, 1, "Randomness");

        EditorGUILayout.IntPopup(propDistribution, distributionLabels, distributionValues);

        EditorGUI.indentLevel++;

        Spawner.Distribution dist = (Spawner.Distribution)propDistribution.enumValueIndex;

        if (propDistribution.hasMultipleDifferentValues || dist == Spawner.Distribution.InSphere)
            EditorGUILayout.PropertyField(propSphereRadius);

        if (propDistribution.hasMultipleDifferentValues || dist == Spawner.Distribution.InBox)
            EditorGUILayout.PropertyField(propBoxSize);

        if (propDistribution.hasMultipleDifferentValues || dist == Spawner.Distribution.AtPoints)
            EditorGUILayout.PropertyField(propSpawnPoints, true);

        EditorGUI.indentLevel--;

        EditorGUILayout.PropertyField(propRandomRotation);
        EditorGUILayout.PropertyField(propParent, new GUIContent("Set Parent"));

        serializedObject.ApplyModifiedProperties();
    }
}

} // namespace Reaktion
