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

using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush {

[CanEditMultipleObjects]
[CustomEditor(typeof(BrushDescriptor))]
public class BrushDescriptorEditor : Editor {
  HashSet<string> m_disabledPropertyNames;
  bool m_editingAllowed;
  string m_noEditWarning;
  string m_appVersion;

  [MenuItem("Tilt/Set Brush release version")]
  static void MenuItem_SetBrushReleaseVersions() {
    // Go through brushes we're about to release, and set their "first released version"

    // Assume the release version is current version, minus the "beta" tag
    string desiredVersion; {
      var config = GameObject.Find("/App/Config").GetComponent<Config>();
      desiredVersion = config.m_VersionNumber;
      if (desiredVersion.EndsWith("b")) {
        desiredVersion = desiredVersion.Substring(0, desiredVersion.Length - 1);
      } else {
        Debug.LogError(
          $"This doesn't look like an in-development build of {App.kAppDisplayName}");
        return;
      }
    }

    TiltBrushManifest manifest = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>(
        "Assets/Manifest.asset");
    foreach (BrushDescriptor desc in manifest.Brushes.Concat(manifest.CompatibilityBrushes)) {
      if (string.IsNullOrEmpty(desc.m_CreationVersion)) {
        Debug.LogFormat("Brush {0} -> version {1}", desc.name, desiredVersion);
        desc.m_CreationVersion = desiredVersion;
        EditorUtility.SetDirty(desc);
      }
    }
  }

  void OnEnable() {
    // Init m_disabledPropertyNames.
    // Really only needs to be done once, rather than once per object, but... whatever.
    {
      m_disabledPropertyNames = new HashSet<string>();
      System.Type objectType = serializedObject.targetObject.GetType();

      // Unity's cursor API is kind of tricky. The iterator starts at an invalid
      // sentinel property (which is reasonable). However, the first iteration
      // must use enterChildren:true.
      bool enterChildren = true;
      SerializedProperty property = serializedObject.GetIterator();
      while (property.NextVisible(enterChildren)) {
        enterChildren = false;
        var field = objectType.GetField(property.name);
        if (field != null
            && field.GetCustomAttributes(typeof(DisabledPropertyAttribute), true).Any()) {
          m_disabledPropertyNames.Add(property.name);
        }
      }
    }

    // Init m_appVersion
    {
      m_appVersion = null;
      var configObj = GameObject.Find("/App/Config");
      if (configObj != null) {
        Config config = configObj.GetComponent<Config>();
        if (config != null) {
          m_appVersion = config.m_VersionNumber;
        }
      }
    }

    m_editingAllowed = IsEditingAllowed(out m_noEditWarning);
    if (m_noEditWarning != null) {
      m_noEditWarning = string.Format("Some fields locked: {0}", m_noEditWarning);
    }
  }

  bool IsEditingAllowed(out string reason) {
    var brushVersionProperty = serializedObject.FindProperty("m_CreationVersion");
    if (m_appVersion == null) {
      reason = "App version cannot be determined! " +
          "Please load the 'Main.unity' scene to edit disabled fields";
      return false;
    }
    if (brushVersionProperty.hasMultipleDifferentValues) {
      reason = "Selected brushes have different versions.";
      return false;
    }
    string brushVersion = brushVersionProperty.stringValue;
    if (brushVersion == null) {
      // Should never happen?
      reason = "Cannot get brush version (???).";
      return false;
    }

    if (brushVersion == "") {
      // Looks like a new/experimental brush.
      reason = null;
      return true;
    }

    // Really, this should be "app version < brush's release-version", but in practice
    // that works out to app version == X.0b, brush version == X.0.
    if (brushVersion + "b" == m_appVersion) {
      reason = null;
      return true;
    }

    reason = string.Format(
        "App version '{0}' is later than the brush's release version '{1}'",
        m_appVersion, brushVersion);
    return false;
  }

  static void DuplicateBrush(string assetPath) {
    string duplicatePath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
    if (!AssetDatabase.CopyAsset(assetPath, duplicatePath)) {
      return;
    }
    var duplicate = AssetDatabase.LoadAssetAtPath<BrushDescriptor>(duplicatePath);
    duplicate.m_Guid = System.Guid.NewGuid();
    duplicate.m_CreationVersion = "";
  }

  public override void OnInspectorGUI() {
    if (!m_editingAllowed) {
      EditorGUILayout.HelpBox(m_noEditWarning, MessageType.Warning);
    } else {
      EditorGUILayout.HelpBox("Dangerous fields are unlocked", MessageType.Info);
    }

    // Unlock and Duplicate buttons
    EditorGUILayout.BeginHorizontal();
    {
      GUI.enabled = !m_editingAllowed;

      string assetPath = null;
      if (! serializedObject.isEditingMultipleObjects) {
        assetPath = AssetDatabase.GetAssetPath(serializedObject.targetObject);
      }

      GUI.enabled = (assetPath != null);
      if (GUILayout.Button("Duplicate Brush")) {
        DuplicateBrush(assetPath);
      }

      if (GUILayout.Button("Unlock Fields")) {
        m_editingAllowed = true;
      }
      if (GUILayout.Button("README")) {
        ShowReadmeText();
      }
      if (GUILayout.Button("Copy GUID")) {
        BrushDescriptor brush = serializedObject.targetObject as BrushDescriptor;
        string guid = brush.m_Guid.ToString();
        EditorGUIUtility.systemCopyBuffer = guid;
      }

      GUI.enabled = true;
    }
    EditorGUILayout.EndHorizontal();

    // Loop through visible properties. A bit tricky; see above.
    bool enterChildren = true;
    SerializedProperty property = serializedObject.GetIterator();
    while (property.NextVisible(enterChildren)) {
      enterChildren = false;
      GUI.enabled = (m_editingAllowed || ! m_disabledPropertyNames.Contains(property.name));
      EditorGUILayout.PropertyField(property, true);
    }

    serializedObject.ApplyModifiedProperties();
  }

  private static void ShowReadmeText() {
    EditorUtility.DisplayDialog(
      "Locked Brush Fields",
      "Most brush fields only affect new art created with that brush. Other fields affect existing art if they're changed. For safety, these fields are read-only if a brush has already been released, t\n\n" +
      "Use the 'Unlock' button to bypass these safety checks, but definitely have someone review your change.\n\n" +
      "Alternatively use the 'Duplicate' button and modify a copy.",
      "OK");
  }
}

/// Renders a string as a BrushDescriptor if possible; otherwise as a plain string.
[CustomPropertyDrawer(typeof(BrushDescriptor.AsStringGuidAttribute))]
class BrushDescriptorAsStringGuidDrawer : PropertyDrawer {
  Dictionary<Guid, BrushDescriptor> m_GuidToBrush;

  public BrushDescriptorAsStringGuidDrawer() {
    m_GuidToBrush = AssetDatabase.FindAssets("t:BrushDescriptor")
        .Select(name => AssetDatabase.LoadAssetAtPath<BrushDescriptor>(
                    AssetDatabase.GUIDToAssetPath(name)))
        .ToDictionary(desc => (Guid)desc.m_Guid);
  }

  /// Returns true if the guid was successfully converted to a descriptor.
  /// The null string, empty string, and empty guid all refer to the null descriptor.
  private BrushDescriptor GuidToDescriptor(string guid) {
    try {
      // There is no TryParse in .net 2.0
      BrushDescriptor desc;
      m_GuidToBrush.TryGetValue(new Guid(guid), out desc);
      return desc;
    } catch (Exception) {
      return null;  // Guid parse error
    }
  }

  public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
    position = EditorGUI.PrefixLabel(position, label);

    Rect leftPos = position; leftPos.width /= 2;
    Rect rightPos = leftPos; rightPos.x = leftPos.xMax;

    var indent = EditorGUI.indentLevel;
    EditorGUI.indentLevel = 0;

    EditorGUI.PropertyField(leftPos, property, GUIContent.none);

    BrushDescriptor desc = GuidToDescriptor(property.stringValue);
    var newDesc = EditorGUI.ObjectField(rightPos, desc, typeof(BrushDescriptor), false);
    if (newDesc != desc) {
      property.stringValue = ((BrushDescriptor)newDesc).m_Guid.ToString();
    }

    EditorGUI.indentLevel = indent;
  }
}

}