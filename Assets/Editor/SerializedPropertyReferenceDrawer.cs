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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TiltBrush {

// This class is used to allow the user to set up classes derived from
// SerializedPropertyReference<T> in the inspector.
[CustomPropertyDrawer(typeof(SerializedPropertyReferenceBool))]
[CustomPropertyDrawer(typeof(SerializedPropertyReferenceInt))]
[CustomPropertyDrawer(typeof(SerializedPropertyReferenceFloat))]
[CustomPropertyDrawer(typeof(SerializedPropertyReferenceString))]
public class SerializedPropertyReferenceDrawer : PropertyDrawer {

  private string[] m_ComponentNames;
  private UnityEngine.Component[] m_Components;
  private int m_selectedComponent;

  private string[] m_PropertyNames;
  private int m_SelectedProperty;
  private const int kWarningBoxHeight = 32;

  public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
    EditorGUI.BeginProperty(position, label, property);
    position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
    var indent = EditorGUI.indentLevel;
    EditorGUI.indentLevel = 0;

    // Set up the rectangles used for GUI rendering. If we need a warning box, the height is
    // enlarged.
    float height = position.height - kWarningBoxHeight;
    var thirdWidth = position.width / 3;
    var gobjRect = new Rect(position.x, position.y, thirdWidth, height);
    var componentRect = new Rect(position.x + thirdWidth, position.y, thirdWidth, height);
    var propRect = new Rect(position.x + 2 * thirdWidth, position.y, thirdWidth, height);
    var helpRect = new Rect(position.x, position.y + height, position.width, kWarningBoxHeight);


    // sub-properties and the target component
    var targetProperty = property.FindPropertyRelative("m_Target");
    var propertyNameProperty = property.FindPropertyRelative("m_PropertyName");
    var targetComponent = targetProperty.objectReferenceValue as UnityEngine.Component;

    // If we have a component set, gather a list of the other components and the available
    // properties.
    if (m_Components == null && targetComponent != null) {
      UpdateComponentsAndProperties(targetComponent.gameObject,targetProperty,
          propertyNameProperty);
    }

    // Although we store the component, in the inspector, we have to select a gameobject to access
    // the components on it.
    GameObject targetGameObject = targetComponent == null ? null : targetComponent.gameObject;
    var newTargetGameObject =
        EditorGUI.ObjectField(gobjRect, targetGameObject, typeof(GameObject), true) as GameObject;
    if (targetGameObject != newTargetGameObject) {
      UpdateComponentsAndProperties(newTargetGameObject, targetProperty,  propertyNameProperty);
    }

    if (m_Components != null && targetComponent != null) {
      int newComponent = EditorGUI.Popup(componentRect, m_selectedComponent, m_ComponentNames);
      if (newComponent != m_selectedComponent) {
        m_selectedComponent = newComponent;
        UpdateComponentsAndProperties(targetGameObject, targetProperty,  propertyNameProperty);
      }
    }

    if (m_PropertyNames != null && targetComponent != null) {
      int newProperty = EditorGUI.Popup(propRect, m_SelectedProperty, m_PropertyNames);
      if (newProperty != m_SelectedProperty) {
        propertyNameProperty.stringValue = m_PropertyNames[newProperty];
        m_SelectedProperty = newProperty;
      }
    }

    EditorGUI.indentLevel = indent;
    EditorGUI.EndProperty();
  }

  private void UpdateComponentsAndProperties(GameObject gameObject,
                                             SerializedProperty componentProperty,
                                             SerializedProperty propertyProperty) {
    m_Components = null;
    m_selectedComponent = 0;
    m_PropertyNames = null;
    m_SelectedProperty = 0;
    if (gameObject == null) {
      return;
    }
    var compopnentNames = new List<string>();
    var components = new List<UnityEngine.Component>();
    var componentProperties = new Dictionary<Component, string[]>();
    // We need to get the generic argument of the base SerializedPropertyReference<T>
    var propertyType = fieldInfo.FieldType.BaseType.GenericTypeArguments[0];

    foreach (var component in gameObject.GetComponents<UnityEngine.Component>()) {
      componentProperties[component] =
          component.GetType().GetProperties().Where(x => x.PropertyType == propertyType)
              .Select(x => x.Name).ToArray();
      if (componentProperties[component].Any()) {
        compopnentNames.Add(component.GetType().Name);
        components.Add(component);
      }
    }
    if (components.Count == 0) {
      componentProperty.objectReferenceValue = null;
      return;
    }
    m_Components = components.ToArray();
    m_ComponentNames = compopnentNames.ToArray();
    m_selectedComponent = m_Components.Select(x => x.GetInstanceID()).
        IndexOf(componentProperty.objectReferenceInstanceIDValue);
    if (m_selectedComponent == -1) {
      m_selectedComponent = 0;
    }
    componentProperty.objectReferenceValue = m_Components[m_selectedComponent];

    m_PropertyNames = componentProperties[m_Components[m_selectedComponent]];
    if (m_PropertyNames.Length == 0) {
      m_PropertyNames = null;
      return;
    }
    m_SelectedProperty = m_PropertyNames.IndexOf(propertyProperty.stringValue);
    if (m_SelectedProperty == -1) {
      m_SelectedProperty = 0;
    }
    propertyProperty.stringValue = m_PropertyNames[m_SelectedProperty];
  }
}
}
