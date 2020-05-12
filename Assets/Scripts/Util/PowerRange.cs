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

using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TiltBrush {

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(PowerRangeAttribute))]
public class PowerRangeDrawer : PropertyDrawer {
  static System.Reflection.MethodInfo s_methodInfo;
  static object[] s_params = new object[6];

  static PowerRangeDrawer() {
    s_methodInfo = typeof(EditorGUI).GetMethod(
        "PowerSlider",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
        null,
        new[] {typeof(Rect), typeof(GUIContent), typeof(float), typeof(float), typeof(float), typeof(float)},
        null);
  }

  public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
    PowerRangeAttribute attribute = (PowerRangeAttribute) this.attribute;
    if (property.propertyType != SerializedPropertyType.Float) {
      EditorGUI.LabelField(position, label.text, "Use PowerRange with float.");
      return;
    }

    Slider(position, property, attribute.min, attribute.max, attribute.power, label);
  }

  public static void Slider(
      Rect position, SerializedProperty property,
      float leftValue, float rightValue, float power, GUIContent label) {
    label = EditorGUI.BeginProperty(position, label, property);
    EditorGUI.BeginChangeCheck();
    float num = PowerSlider(position, label, property.floatValue, leftValue, rightValue, power);

    if (EditorGUI.EndChangeCheck()) {
      property.floatValue = num;
    }
    EditorGUI.EndProperty();
  }

  public static float PowerSlider(Rect position, GUIContent label, float value, float leftValue, float rightValue, float power) {
    if (s_methodInfo != null) {
      s_params[0] = position;
      s_params[1] = label;
      s_params[2] = value;
      s_params[3] = leftValue;
      s_params[4] = rightValue;
      s_params[5] = power;
      return (float)s_methodInfo.Invoke(null, s_params);
    } else {
      return leftValue;
    }
  }
}
#endif

/// <summary>
/// Not a true logarithmic range the way I'd like it; this just re-uses some internal
/// but hidden Unity functionality.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class PowerRangeAttribute : PropertyAttribute {
  public readonly float min;
  public readonly float max;
  public readonly float power;
  public PowerRangeAttribute(float min = 1e-3f, float max = 1e3f, float power = 2f) {
    if (min <= 0) {
      min = 1e-4f;
    }
    this.min = min;
    this.max = max;
    this.power = power;
  }
}

}  // namespace TiltBrush
