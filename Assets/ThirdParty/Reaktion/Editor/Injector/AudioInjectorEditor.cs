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

[CustomEditor(typeof(AudioInjector)), CanEditMultipleObjects]
public class AudioInjectorEditor : Editor
{
    SerializedProperty propMute;

    // Assets for drawing level meters.
    Texture2D bgTexture;
    Texture2D fgTexture;

    void OnEnable()
    {
        propMute = serializedObject.FindProperty("mute");
    }

    void OnDisable()
    {
        if (bgTexture != null)
        {
            DestroyImmediate(bgTexture);
            DestroyImmediate(fgTexture);
            bgTexture = fgTexture = null;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(propMute);

        serializedObject.ApplyModifiedProperties();

        // Draw a level meter if the target is active.
        if (EditorApplication.isPlaying && !serializedObject.isEditingMultipleObjects)
        {
            var source = target as AudioInjector;
            if (source.enabled && source.gameObject.activeInHierarchy)
            {
                DrawLevelMeter(source.DbLevel);
                EditorUtility.SetDirty(target); // Make it dirty to update the view.
            }
        }
    }

    static Texture2D NewBarTexture(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    void DrawLevelMeter(float level)
    {
        if (bgTexture == null)
        {
            bgTexture = NewBarTexture(new Color(55.0f / 255, 53.0f / 255, 45.0f / 255));    // gray
            fgTexture = NewBarTexture(new Color(250.0f / 255, 249.0f / 255, 248.0f / 255)); // white
        }

        // Draw BG.
        var rect = GUILayoutUtility.GetRect(18, 16, "TextField");
        GUI.DrawTexture(rect, bgTexture);

        // Draw level bar.
        var barRect = rect;
        barRect.width *= Mathf.Clamp01((level + 60) / (3 + 60));
        GUI.DrawTexture(barRect, fgTexture);

        // Draw dB value.
        EditorGUI.LabelField(rect, level.ToString("0.0") + " dB");
    }
}

} // namespace Reaktion
