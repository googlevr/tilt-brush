// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace TiltBrush {
/// <summary>
/// Utilities for custom Inspector GUI scripts
/// </summary>
public class InspectorUtils {

  /// <summary>
  ///  Takes a texture (usually with height 1) and stretches it into single line height for debugging
  /// </summary>
  public static void LayoutCustomLabel(string Label, int FontSize = 11, FontStyle Style = FontStyle.Normal, TextAnchor Anchor = TextAnchor.MiddleLeft) {
    var gs = new GUIStyle(GUI.skin.label);
    gs.fontStyle = Style;
    gs.fontSize = FontSize;
    gs.alignment = Anchor;
    gs.richText = true;
    EditorGUILayout.LabelField(Label, gs);
  }

  /// <summary>
  ///  Takes a texture (usually with height 1) and stretches it into single line height for debugging
  /// </summary>
  public static void LayoutTexture(string Label, Texture2D Texture) {
    EditorGUILayout.LabelField(Label);
    var r = EditorGUILayout.BeginVertical(GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2));
    EditorGUILayout.Space();
    var r_img = new Rect(r.x, r.y, r.width, r.height * .75f);
    GUI.DrawTexture(r_img, Texture, ScaleMode.StretchToFill, false);
    EditorGUI.DrawTextureAlpha(new Rect(r.x, r.y + r.height * .75f, r.width, r.height * .25f), Texture, ScaleMode.StretchToFill);

    //EditorGUI.DrawTextureTransparent(r,t.WaveFormTexture, ScaleMode.StretchToFill);
    EditorGUILayout.EndVertical();

    // Draw value when hovering with mouse
    if (r_img.Contains(Event.current.mousePosition)) {
      var coords = new Vector2(
        (r.x - Event.current.mousePosition.x) / (float)r.width,
        (r.y - Event.current.mousePosition.y) / (float)r.height
      );
      var col = Texture.GetPixel(Mathf.FloorToInt(coords.x * Texture.width), Mathf.FloorToInt(coords.y * Texture.height));
      var gs = new GUIStyle(GUI.skin.box);
      gs.alignment = TextAnchor.LowerRight;
      EditorGUI.LabelField(new Rect(r.x + r.width * 0.4f, r.y - 18, r.width * .6f, 18),
        string.Format("({0:F}, {1:F}, {2:F}, {3:F})", col.r, col.g, col.b, col.a),
        gs);
    }
  }

  public static void LayoutBar(string Label, float Value, Color Color) {
    Value = Mathf.Clamp01(Value);
    EditorGUILayout.Space();
    var r = EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
    EditorGUILayout.Space();
    EditorGUI.LabelField(new Rect(r.x, r.y, r.width * 0.5f, r.height), Label);
    DrawBar(new Rect(r.x + r.width * .5f, r.y, r.width * .5f, r.height), Value, Color);
    EditorGUILayout.EndHorizontal();
  }

  public static void LayoutBarVec4(string Label, Vector4 Value, Color Color, bool ClampTo01 = true) {
    Value.x = ClampTo01 ? Mathf.Clamp01(Value.x) : Value.x % 1f;
    Value.y = ClampTo01 ? Mathf.Clamp01(Value.y) : Value.y % 1f;
    Value.z = ClampTo01 ? Mathf.Clamp01(Value.z) : Value.z % 1f;
    Value.w = ClampTo01 ? Mathf.Clamp01(Value.w) : Value.w % 1f;
    EditorGUILayout.Space();
    var r = EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2f));
    EditorGUILayout.Space();
    var gs = new GUIStyle(GUI.skin.label);
    gs.alignment = TextAnchor.UpperRight;
    EditorGUI.LabelField(new Rect(r.x, r.y, r.width * 0.5f, r.height), Label + " ", gs);
    DrawBar(new Rect(r.x + r.width * .5f, r.y + (r.height / 4f) * 0, r.width * .5f, r.height / 4f - 2), Value.x, Color);
    DrawBar(new Rect(r.x + r.width * .5f, r.y + (r.height / 4f) * 1, r.width * .5f, r.height / 4f - 2), Value.y, Color);
    DrawBar(new Rect(r.x + r.width * .5f, r.y + (r.height / 4f) * 2, r.width * .5f, r.height / 4f - 2), Value.z, Color);
    DrawBar(new Rect(r.x + r.width * .5f, r.y + (r.height / 4f) * 3, r.width * .5f, r.height / 4f - 2), Value.w, Color);
    EditorGUILayout.EndHorizontal();
  }

  static void DrawBar(Rect r, float Value, Color Color) {
    EditorGUI.DrawRect(r, new Color(0.7f, 0.7f, 0.7f));
    EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * Value, r.height), Color * new Color(0.8f, 0.8f, 0.8f));
  }
}
} // namespace TiltBrush
#endif
