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
using UnityEditor;

namespace TiltBrush {

/// Usage:
///
///   In your Editor subclass, create an instance of this in OnEnable.
///   in OnInspectorGUI, do {
///     serializedObject.Update();
///     list.DoLayoutList();
///     serializedObject.ApplyModifiedProperties();
///   }
///
/// For more details, see:
///   http://va.lent.in/unity-make-your-lists-functional-with-reorderablelist/
///   https://www.cnblogs.com/hont/p/5458021.html
///
class SimpleReorderableList : UnityEditorInternal.ReorderableList {

  // Utility: removes a chunk from the passed rect, and returns that chunk.
  static Rect SplitLeft(int width, ref Rect rect) {
    var ret = rect;
    ret.width = width;
    rect.x += width;
    return ret;
  }

  // If non-null, renders page numbers next to the entry, so it's easier to
  // figure out where the brush (or whatever) lands in the Tilt Brush UI.
  public int? m_elementsPerPage;

  public SimpleReorderableList(SerializedObject so, string propertyName)
    : base(so, so.FindProperty(propertyName),
           true,  // draggable
           true,  // displayHeader
           true,  // displayAddButton
           true   // displayRemoveButton
           ) {
    this.drawElementCallback = OnDrawElement;
    this.drawHeaderCallback = OnDrawHeader;
  }

  void OnDrawElement(Rect rect, int drawIndex, bool isActive, bool isFocused) {
    SerializedProperty elt = this.serializedProperty.GetArrayElementAtIndex(drawIndex);
    Rect itemRect = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
    if (m_elementsPerPage != null) {
      // add an index
      Rect indexRect = SplitLeft(30, ref itemRect);
      int page = drawIndex / m_elementsPerPage.Value;
      int indexInPage = drawIndex % m_elementsPerPage.Value;
      EditorGUI.LabelField(indexRect, string.Format("{0}.{1}", page+1, indexInPage+1));
    }

    EditorGUI.PropertyField(itemRect, elt, GUIContent.none);
  }

  void OnDrawHeader(Rect rect) {
    EditorGUI.LabelField(rect, this.serializedProperty.name);
  }
}

}  // namespace TiltBrush
