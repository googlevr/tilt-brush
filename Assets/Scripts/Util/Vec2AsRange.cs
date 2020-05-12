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

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TiltBrush {

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(Vec2AsRangeAttribute))]
public class Vec2AsRange : PropertyDrawer {
  static float FLOAT_WIDTH = 52;
  static float PAD = 5;
  static float SOLO_LABEL_WIDTH = 30;

  public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
    Vec2AsRangeAttribute attr = attribute as Vec2AsRangeAttribute;
    EditorGUI.BeginProperty(position, label, property);

    Vector2 vec2 = property.vector2Value;
    float min = Mathf.Min(vec2.x, vec2.y);
    float max = Mathf.Max(vec2.x, vec2.y);

    if (attr.Slider) {
      var sliderRect = position;
      sliderRect.xMax -= 2*FLOAT_WIDTH + PAD;
      position.xMin = position.xMax - 2*FLOAT_WIDTH;
      EditorGUI.MinMaxSlider(sliderRect, label, ref min, ref max, attr.LowerBound, attr.UpperBound);

      var mid = position.x + position.width/2;
      var rectA = position;
      rectA.xMax = mid;
      min = EditorGUI.DelayedFloatField(rectA, GUIContent.none, min);
      var rectB = position;
      rectB.xMin = mid;
      if (! attr.HideMax) {
        max = EditorGUI.DelayedFloatField(rectB, GUIContent.none, max);
      }
    } else {
      position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

      var mid = position.x + position.width/2;
      EditorGUIUtility.labelWidth = SOLO_LABEL_WIDTH;
      var rectA = position;
      rectA.xMax = mid;
      min = EditorGUI.DelayedFloatField(rectA, "min", min);
      var rectB = position;
      rectB.xMin = mid;
      if (! attr.HideMax) {
        max = EditorGUI.DelayedFloatField(rectB, "max", max);
      }

      EditorGUIUtility.labelWidth = 0;
    }

    Vector2 newVec2 = new Vector2(min, max);
    if (newVec2 != vec2) {
      property.vector2Value = newVec2;
    }

    EditorGUI.EndProperty();
  }
}
#endif

/// Use this attribute on vec2 members to display them in the inspector
/// as an optional range slider + 2 float fields.
public class Vec2AsRangeAttribute : PropertyAttribute {
  public bool Slider { get; set; }
  public bool HideMax { get; set; }
  public float LowerBound { get; set; }
  public float UpperBound { get; set; }
  public Vec2AsRangeAttribute() {
    Slider = true;
    HideMax = false;
    LowerBound = float.MinValue;
    UpperBound = float.MaxValue;
  }
}
}  // namespace TiltBrush
