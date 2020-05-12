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

[CustomEditor(typeof(Reaktor)), CanEditMultipleObjects]
public class ReaktorEditor : Editor
{
    // Audio input settings.
    SerializedProperty propInjector;
    SerializedProperty propAudioCurve;

    // Remote controls.
    SerializedProperty propGain;
    SerializedProperty propOffset;

    // General option.
    SerializedProperty propSensitivity;
    SerializedProperty propDecaySpeed;

    // Audio input options.
    SerializedProperty propShowAudioOptions;
    SerializedProperty propHeadroom;
    SerializedProperty propDynamicRange;
    SerializedProperty propLowerBound;
    SerializedProperty propFalldown;

    // Texutres for drawing level bars.
    Texture2D[] barTextures;

    void OnEnable()
    {
        // Audio input settings.
        propInjector         = serializedObject.FindProperty("injector");
        propAudioCurve       = serializedObject.FindProperty("audioCurve");

        // Remote controls.
        propGain             = serializedObject.FindProperty("gain");
        propOffset           = serializedObject.FindProperty("offset");

        // General option.
        propSensitivity      = serializedObject.FindProperty("sensitivity");
        propDecaySpeed       = serializedObject.FindProperty("decaySpeed");
        
        // Audio input options.
        propShowAudioOptions = serializedObject.FindProperty("showAudioOptions");
        propHeadroom         = serializedObject.FindProperty("headroom");
        propDynamicRange     = serializedObject.FindProperty("dynamicRange");
        propLowerBound       = serializedObject.FindProperty("lowerBound");
        propFalldown         = serializedObject.FindProperty("falldown");
    }
    
    void OnDisable()
    {
        if (barTextures != null)
        {
            // Destroy the bar textures.
            foreach (var texture in barTextures)
                DestroyImmediate(texture);
            barTextures = null;
        }
    }

    public override void OnInspectorGUI()
    {
        // Update the references.
        serializedObject.Update();

        // Audio input settings.
        EditorGUILayout.PropertyField(propInjector);
        EditorGUILayout.PropertyField(propAudioCurve, new GUIContent("Amplitude Curve"));

        // Remote controls.
        EditorGUILayout.PropertyField(propGain, new GUIContent("Gain Control"));
        EditorGUILayout.PropertyField(propOffset, new GUIContent("Offset Control"));

        // General option.
        EditorGUILayout.Slider(propSensitivity, 0, 1);
        EditorGUILayout.Slider(propDecaySpeed, 0, 1);

        // Audio input options.
        if (!propShowAudioOptions.hasMultipleDifferentValues)
        {
            propShowAudioOptions.boolValue = EditorGUILayout.Foldout(propShowAudioOptions.boolValue, "Audio Input Options");
        }
        else
        {
            EditorGUILayout.LabelField("Audio Input Options");
        }

        if (propShowAudioOptions.hasMultipleDifferentValues || propShowAudioOptions.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(propHeadroom, 0.0f, 20.0f, "Headroom [dB]");
            EditorGUILayout.Slider(propDynamicRange, 1.0f, 60.0f, "Dyn. Range [dB]");
            EditorGUILayout.Slider(propLowerBound, -100.0f, -10.0f, "Lower Bound [dB]");
            EditorGUILayout.Slider(propFalldown, 0.0f, 10.0f, "Falldown [dB/Sec]");
            EditorGUI.indentLevel--;
        }

        // Apply modifications.
        serializedObject.ApplyModifiedProperties();
        
        // Draw the level bar on play mode.
        if (EditorApplication.isPlaying && !serializedObject.isEditingMultipleObjects)
        {
            var reaktor = target as Reaktor;
            if (reaktor.enabled && reaktor.gameObject.activeInHierarchy)
            {
                EditorGUILayout.Space();
                DrawInputLevelBars(reaktor);
                EditorUtility.SetDirty(target); // Make it dirty to update the view.
            }
        }
    }

    // Make a texture which contains only one pixel.
    Texture2D NewBarTexture(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    // Draw the input level bar.
    void DrawInputLevelBars(Reaktor reaktor)
    {
        if (barTextures == null)
        {
            // Make textures for drawing level bars.
            barTextures = new Texture2D[] {
                NewBarTexture(new Color(55.0f / 255, 53.0f / 255, 45.0f / 255)),
                NewBarTexture(new Color(250.0f / 255, 249.0f / 255, 248.0f / 255)),
                NewBarTexture(new Color(110.0f / 255, 192.0f / 255, 91.0f / 255, 0.8f)),
                NewBarTexture(new Color(226.0f / 255, 0, 7.0f / 255, 0.8f)),
                NewBarTexture(new Color(249.0f / 255, 185.0f / 255, 22.0f / 255))
            };
        }
        
        // Get a rectangle as a text field and fill it.
        var rect = GUILayoutUtility.GetRect(18, 16, "TextField");
        GUI.DrawTexture(rect, barTextures[0]);

        // Draw the raw input bar.
        var temp = rect;
        temp.width *= Mathf.Clamp01((reaktor.RawInput - reaktor.lowerBound) / (3 - reaktor.lowerBound));
        GUI.DrawTexture(temp, barTextures[1]);

        // Draw the dynamic range.
        temp.x += rect.width * (reaktor.Peak - reaktor.lowerBound - reaktor.dynamicRange - reaktor.headroom) / (3 - reaktor.lowerBound);
        temp.width = rect.width * reaktor.dynamicRange / (3 - reaktor.lowerBound);
        GUI.DrawTexture(temp, barTextures[2]);

        // Draw the headroom.
        temp.x += temp.width;
        temp.width = rect.width * reaktor.headroom / (3 - reaktor.lowerBound);
        GUI.DrawTexture(temp, barTextures[3]);

        // Display the peak level value.
        EditorGUI.LabelField(rect, "Peak: " + reaktor.Peak.ToString ("0.0") + " dB");

        // Draw the gain level.
        DrawLevelBar("Gain", reaktor.Gain, barTextures[0], barTextures[1]);

        // Draw the offset level.
        DrawLevelBar("Offset", reaktor.Offset, barTextures[0], barTextures[1]);

        // Draw the output level.
        DrawLevelBar("Out", reaktor.Output, barTextures[0], barTextures[4]);
    }

    void DrawLevelBar(string label, float value, Texture bg, Texture fg)
    {
        // Get a rectangle as a text field and fill it.
        var rect = GUILayoutUtility.GetRect(18, 16, "TextField");
        GUI.DrawTexture(rect, bg);
        
        // Draw a level bar.
        var temp = rect;
        temp.width *= value;
        GUI.DrawTexture(temp, fg);
        
        // Display the level value in percentage.
        EditorGUI.LabelField(rect, label + ": " + (value * 100).ToString("0.0") + " %");
    }
}

} // namespace Reaktion
