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

namespace TiltBrush {

/// Stores a transient collection of strokes for implementing features such as Copy, Paste and
/// Duplicate. The ClipboardManager implicitly uses the SelectionManager as the source for brush
/// strokes during copy operations.
///
/// Future work: support other types of objects such as reference images, models, guides, mirror
/// position, etc.
public class ClipboardManager : MonoBehaviour {
  static public ClipboardManager Instance { get; private set; }

  [SerializeField] private Vector3 m_DuplicateOffset;

  /// Returns true if the current selection can be copied.
  public bool CanCopy {
    get {
      return SelectionManager.m_Instance.HasSelection;
    }
  }

  void Awake() {
    Instance = this;
  }

  /// Copies and pastes the current selection to the current canvas.
  public void DuplicateSelection(bool offsetDuplicate = false) {
    TrTransform xf = SelectionManager.m_Instance.SelectionTransform;
    if (offsetDuplicate) {
      // Scoot all the strokes and widgets.
      // TODO: Make this relative to the user's facing.
      Vector3 offset = m_DuplicateOffset / App.Scene.Pose.scale * 0.5f;
      xf.translation += offset;
    }

    // Lil' jiggle.
    var controller = InputManager.ControllerName.Brush;
    if (SketchControlsScript.m_Instance.OneHandGrabController != InputManager.ControllerName.None) {
      controller = SketchControlsScript.m_Instance.OneHandGrabController;
    }
    InputManager.m_Instance.TriggerHapticsPulse(controller, 3, 0.15f, 0.07f);
    AudioManager.m_Instance.PlayDuplicateSound(InputManager.m_Instance.GetControllerPosition(controller));

    SketchMemoryScript.m_Instance.PerformAndRecordCommand(new DuplicateSelectionCommand(xf));
  }
}

}  // namespace TiltBrush
