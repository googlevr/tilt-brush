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
using System.Reflection;

using JetBrains.Annotations;
using UnityEngine;

namespace TiltBrush {

// Script Ordering:
// - does not need to come after anything
// - must come before everything that uses the material catalog, specifically
//   BaseControllerBehavior.RefreshControllerFace(), which can eventually be
//   called as a result of InputManager.Start().
//
// Used to store global materials.
//
[Serializable] // < Serializable attr is required because we use reflection on class members.
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class ControllerMaterialCatalog : MonoBehaviour {
  public static ControllerMaterialCatalog m_Instance;

  [AttributeUsage(AttributeTargets.Property)]
  public class CatalogMaterialAttribute : Attribute {
  }

  // When adding materials, follow the pattern below.
  // NOTE: the Property name must match the Field name, but without the m_ prefix.

  [SerializeField] private Material m_Standard;
  [CatalogMaterial] public Material Standard { get; private set; }

  [SerializeField] private Material m_Blank;
  [CatalogMaterial] public Material Blank { get; private set; }

  [SerializeField] private Material m_SnapOn;
  [CatalogMaterial] public Material SnapOn { get; private set; }

  [SerializeField] private Material m_SnapOff;
  [CatalogMaterial] public Material SnapOff { get; private set; }

  [SerializeField] private Material m_PanelsRotate;
  [CatalogMaterial] public Material PanelsRotate { get; private set; }

  [SerializeField] private Material m_UndoRedo;
  [CatalogMaterial] public Material UndoRedo { get; private set; }

  [SerializeField] private Material m_UndoRedo_Undo;
  [CatalogMaterial] public Material UndoRedo_Undo { get; private set; }

  [SerializeField] private Material m_UndoRedo_Redo;
  [CatalogMaterial] public Material UndoRedo_Redo { get; private set; }

  [SerializeField] private Material m_Undo;
  [CatalogMaterial] public Material Undo { get; private set; }

  [SerializeField] private Material m_Redo;
  [CatalogMaterial] public Material Redo { get; private set; }

  [SerializeField] private Material m_WorldTransformReset;
  [CatalogMaterial] public Material WorldTransformReset { get; private set; }

  [SerializeField] private Material m_PinCushion;
  [CatalogMaterial] public Material PinCushion { get; private set; }

  [SerializeField] private Material m_Cancel;
  [CatalogMaterial] public Material Cancel { get; private set; }

  [SerializeField] private Material m_Cancel_Rot180;
  [CatalogMaterial] public Material Cancel_Rot180 { get; private set; }

  [SerializeField] private Material m_Multicam;
  [CatalogMaterial] public Material Multicam { get; private set; }

  [SerializeField] private Material m_MulticamActive;
  [CatalogMaterial] public Material MulticamActive { get; private set; }

  [SerializeField] private Material m_MulticamSwipeHint;
  [CatalogMaterial] public Material MulticamSwipeHint { get; private set; }

  [SerializeField] private Material m_TutorialPad;
  [CatalogMaterial] public Material TutorialPad { get; private set; }

  [SerializeField] private Material m_YesOrCancel;
  [CatalogMaterial] public Material YesOrCancel { get; private set; }

  [SerializeField] private Material m_YesOrCancel_Yes;
  [CatalogMaterial] public Material YesOrCancel_Yes { get; private set; }

  [SerializeField] private Material m_YesOrCancel_Cancel;
  [CatalogMaterial] public Material YesOrCancel_Cancel { get; private set; }

  [SerializeField] private Material m_YesOrCancel_Rot180;
  [CatalogMaterial] public Material YesOrCancel_Rot180 { get; private set; }

  [SerializeField] private Material m_YesOrCancel_Yes_Rot180;
  [CatalogMaterial] public Material YesOrCancel_Yes_Rot180 { get; private set; }

  [SerializeField] private Material m_YesOrCancel_Cancel_Rot180;
  [CatalogMaterial] public Material YesOrCancel_Cancel_Rot180 { get; private set; }

  [SerializeField] private Material m_Yes;
  [CatalogMaterial] public Material Yes { get; private set; }

  [SerializeField] private Material m_Yes_Rot180;
  [CatalogMaterial] public Material Yes_Rot180 { get; private set; }

  [SerializeField] private Material m_ShareYt;
  [CatalogMaterial] public Material ShareYt { get; private set; }

  [SerializeField] private Material m_ShareYtActive;
  [CatalogMaterial] public Material ShareYtActive { get; private set; }

  [SerializeField] private Material m_ShareYtActive_Rot180;
  [CatalogMaterial] public Material ShareYtActive_Rot180 { get; private set; }

  [SerializeField] private Material m_Trash;
  [CatalogMaterial] public Material Trash { get; private set; }

  [SerializeField] private Material m_BrushPage;
  [CatalogMaterial] public Material BrushPage { get; private set; }

  [SerializeField] private Material m_BrushPageActive;
  [CatalogMaterial] public Material BrushPageActive { get; private set; }

  [SerializeField] private Material m_BrushPageActive_LogitechPen;
  [CatalogMaterial] public Material BrushPageActive_LogitechPen { get; private set; }

  [SerializeField] private Material m_BrushSizer;
  [CatalogMaterial] public Material BrushSizer { get; private set; }

  [SerializeField] private Material m_BrushSizer_LogitechPen;
  [CatalogMaterial] public Material BrushSizer_LogitechPen { get; private set; }

  [SerializeField] private Material m_BrushSizerActive;
  [CatalogMaterial] public Material BrushSizerActive { get; private set; }

  [SerializeField] private Material m_BrushSizerActive_LogitechPen;
  [CatalogMaterial] public Material BrushSizerActive_LogitechPen { get; private set; }

  [SerializeField] private Material m_ToggleSelectionOn;
  [CatalogMaterial] public Material ToggleSelectionOn { get; private set; }

  [SerializeField] private Material m_ToggleSelectionOff;
  [CatalogMaterial] public Material ToggleSelectionOff { get; private set; }

  [SerializeField] private Material m_SelectionOptions;
  [CatalogMaterial] public Material SelectionOptions { get; private set; }

  void Awake() {
    m_Instance = this;

    // Instantiate materials to keep from modifying the on-disk asset.
    BindingFlags propFlags = BindingFlags.Public | BindingFlags.Instance;
    BindingFlags fieldFlags = BindingFlags.NonPublic | BindingFlags.Instance;

    // Loop over all material properties and assign them to the associated property.
    // This requires that this class not be obfuscated.
    foreach (var prop in this.GetType().GetProperties(propFlags)) {
      var attrs = prop.GetCustomAttributes(typeof(CatalogMaterialAttribute), false);
      if (attrs.Length == 0) {
        continue;
      }

      // Every material property must have a matching field with the same name, prefixed by "m_".
      var field = this.GetType().GetField("m_" + prop.Name, fieldFlags);
      prop.SetValue(this, Instantiate(field.GetValue(this) as Material), null);
    }
  }

  // This function assigns the material to the renderer, using a MaterialCache component to ensure
  // only one material is instanced per catalog material.
  // Without the material cache, if a caller assigns a material from the catalog, then alters it,
  // the material is instanced.  Our pattern is to liberally assign from the catalog, so this
  // function prevents orphaned materials.
  public void Assign(Renderer renderer, Material catalogMat) {
    MaterialCache mc = renderer.GetComponent<MaterialCache>();
    if (mc == null) {
      mc = renderer.gameObject.AddComponent<MaterialCache>();
    }
    mc.AssignMaterial(catalogMat);
  }
}
}  // namespace TiltBrush
