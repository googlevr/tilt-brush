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

/// A tool to select (or deselect) strokes that intersect with its spinning orb.
///
/// Use the brush trigger to activate the orb. Intersecting strokes will call to the
/// global SelectionManager and move them to the selection canvas, highlighting them and
/// making them grabbable by the user.
public class SelectionTool : ToggleStrokeModificationTool {
  [SerializeField] private float m_DuplicateHoldDuration = .15f;
  private bool m_ActiveSelectionHasAtLeastOneObject;

  public override float ButtonHoldDuration { get { return m_DuplicateHoldDuration; } }

  override protected bool IsOn() {
    return !SelectionManager.m_Instance.ShouldRemoveFromSelection();
  }

  override public void Init() {
    base.Init();
    SelectionManager.m_Instance.CacheSelectionTool(this);
    PromoManager.m_Instance.RequestPromo(PromoType.Duplicate);
  }

  override public void EnableTool(bool bEnable) {
    if (bEnable) {
      // When enabling, ensure we're not in deselect mode.
      SelectionManager.m_Instance.RemoveFromSelection(false);
      WidgetManager.m_Instance.WidgetsDormant = false;
    } else {
      EndSelection();

      // Make sure the main canvas is visible when we switch out of this tool.
      App.Scene.MainCanvas.gameObject.SetActive(true);
    }

    // Call this after setting up our tool's state.
    base.EnableTool(bEnable);
    HideTool(!bEnable);
  }

  public override float GetSizeRatio(
      InputManager.ControllerName controller, VrInput input) {
    if (SketchControlsScript.m_Instance.IsUserInteractingWithSelectionWidget()) {
      return InputManager.Controllers[(int)controller].GetCommandHoldProgress();
    }
    if (controller == InputManager.ControllerName.Brush &&
        SketchControlsScript.m_Instance.IsUserIntersectingWithSelectionWidget()) {
      return InputManager.Brush.GetLastHeldInput() == null ? 0 :
        InputManager.Brush.GetCommandHoldProgress();
    }
    return base.GetSizeRatio(controller, input);
  }

  override protected void OnAnimationSwitch() {
    bool selecting = !SelectionManager.m_Instance.ShouldRemoveFromSelection();
    AudioManager.m_Instance.PlayToggleSelect(m_ToolTransform.position, selecting);
    InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush,
        (selecting ? m_HapticsToggleOn : m_HapticsToggleOff));
  }

  override public void OnDoubleTap() {
    // Audio is dismissed in other places, but on double tap, we need to make sure we don't
    // linger in case there's a large hitch and the audio stalls.
    HardStopAudio();
    AudioManager.m_Instance.SelectionHighlightLoop(false);
    EndSelection();
  }

  override public void OnUpdateDetection() {
    // Check actions if we're not hot.
    if (!m_CurrentlyHot) {
      // If intersecting with selection widget
      if (SketchControlsScript.m_Instance.IsUsersBrushIntersectingWithSelectionWidget()) {
        if (InputManager.m_Instance.GetCommandDown(
            InputManager.SketchCommands.DuplicateSelection)) {
          InputManager.Brush.LastHeldInput =
            InputManager.Brush.GetCommandHoldInput(InputManager.SketchCommands.DuplicateSelection);
        }

        if (InputManager.Brush.LastHeldInput != null &&
          InputManager.m_Instance.GetCommandHeld(InputManager.SketchCommands.DuplicateSelection)) {
          SketchControlsScript.m_Instance.IssueGlobalCommand(
            SketchControlsScript.GlobalCommands.Duplicate);
        }

        // If not hot, but intersecting, show higlight mesh
        m_HighlightMesh.gameObject.SetActive(true);
      } else {
        // Toggle selection/deselection if we have a selection, or if we're currently on deselect.
        bool bShouldRemoveFromSelection = SelectionManager.m_Instance.ShouldRemoveFromSelection();
        if (SelectionManager.m_Instance.HasSelection || bShouldRemoveFromSelection) {
          // Show selection promo 'til the user toggles to Deselect mode.
          PromoManager.m_Instance.RequestPromo(PromoType.Selection);
          if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.ToggleSelection)) {
            // Show deselect promo 'til the user toggles out
            PromoManager.m_Instance.RequestPromo(PromoType.Deselection);
            PromoManager.m_Instance.RecordCompletion(PromoType.Selection);
            SelectionManager.m_Instance.RemoveFromSelection(!bShouldRemoveFromSelection);
            SketchControlsScript.m_Instance.EatToolScaleInput();
          }
        }

        // If not hot, but and not intersecting, disable highlight mesh
        m_HighlightMesh.gameObject.SetActive(false);
      }
    }

    // If we were hot, but we're not, finalize our selection.
    if (!m_CurrentlyHot && m_ToolWasHot) {
      FinalizeSelectionBatch();
      ResetToolRotation();
      ClearGpuFutureLists();
    }

    bool removeFromSelection = SelectionManager.m_Instance.ShouldRemoveFromSelection();
    m_CurrentCanvas = removeFromSelection ? App.Scene.SelectionCanvas : App.ActiveCanvas;
  }


  override protected int AdditionalGpuIntersectionLayerMasks() {
    return WidgetManager.m_Instance.StencilLayerMask;
  }

  override protected bool HandleIntersectionWithWidget(GrabWidget widget) {
    // Can't select a pinned widget.
    if (widget.Pinned) {
      return false;
    }

    var isSelected = SelectionManager.m_Instance.IsWidgetSelected(widget);
    bool removeFromSelection = SelectionManager.m_Instance.ShouldRemoveFromSelection();
    if ((removeFromSelection && !isSelected) || (!removeFromSelection && isSelected)) {
      Debug.LogWarning(
          "Attempted to " + (removeFromSelection ? "deselect" : "select") +
          " a widget that's already " + (isSelected ? "selected" : "deselected") + ".");
      return true;
    }

    PlayModifyStrokeSound();

    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new SelectCommand(null,
                          new [] { widget },
                          SelectionManager.m_Instance.SelectionTransform,
                          initial: !m_ActiveSelectionHasAtLeastOneObject,
                          deselect: removeFromSelection));
    m_ActiveSelectionHasAtLeastOneObject = true;
    m_LastIntersectionTime = Time.realtimeSinceStartup;

    // If we're selecting something while an existing selection has been transformed,
    // create a new selection and consolidate the command for selecting the future strokes
    // with the command to deselect the prior selection.
    if (!removeFromSelection && SelectionManager.m_Instance.SelectionWasTransformed) {
      EndSelection();
    }

    return true;
  }

  override protected bool HandleIntersectionWithBatchedStroke(BatchSubset rGroup) {
    var stroke = rGroup.m_Stroke;
    var isSelected = SelectionManager.m_Instance.IsStrokeSelected(stroke);
    bool removeFromSelection = SelectionManager.m_Instance.ShouldRemoveFromSelection();
    if ((removeFromSelection && !isSelected) || (!removeFromSelection && isSelected)) {
      // I think it's actually expected that this happens every now and then.
      // The intersection results are from some time in the past.
      Debug.LogWarning(
          "Attempted to " + (removeFromSelection ? "deselect" : "select") +
          " a stroke that's already " + (isSelected ? "selected" : "deselected") + ".");
      return true;
    }

    PlayModifyStrokeSound();

    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new SelectCommand(new [] { stroke },
                          null,
                          SelectionManager.m_Instance.SelectionTransform,
                          initial: !m_ActiveSelectionHasAtLeastOneObject,
                          deselect: removeFromSelection));
    m_ActiveSelectionHasAtLeastOneObject = true;
    m_LastIntersectionTime = Time.realtimeSinceStartup;

    // If we're selecting strokes while an existing selection has been transformed,
    // create a new selection and consolidate the command for selecting the future strokes
    // with the command to deselect the prior selection.
    if (!removeFromSelection && SelectionManager.m_Instance.SelectionWasTransformed) {
      EndSelection();
    }

    return true;
  }

  private void EndSelection() {
    if (SelectionManager.m_Instance.HasSelection) {
      HapticFeedback();
    }
    SelectionManager.m_Instance.ClearActiveSelection();
  }

  /// To be called after a series of (de)selections to store the series as a single
  /// command on the undo stack. It also updates the selection widget box.
  private void FinalizeSelectionBatch() {
    // If we've asked to finalize a batch, deselect if nothing was selected and we're in move mode.
    if (SelectionManager.m_Instance.SelectionWasTransformed &&
        !SelectionManager.m_Instance.ShouldRemoveFromSelection()) {
      EndSelection();
    } else {
      SelectionManager.m_Instance.UpdateSelectionWidget();
    }
    m_ActiveSelectionHasAtLeastOneObject = false;

    // Only allow deselection if we have strokes.
    if (!SelectionManager.m_Instance.HasSelection) {
      SelectionManager.m_Instance.RemoveFromSelection(false);
    }
  }

  override public void AssignControllerMaterials(InputManager.ControllerName controller) {
    if (controller == InputManager.ControllerName.Brush) {
      InputManager.Brush.Geometry.ShowSelectionToggle();
      if (SketchControlsScript.m_Instance.IsUsersBrushIntersectingWithSelectionWidget()) {
        InputManager.Brush.Geometry.ShowDuplicateOption();
      }
    }
  }
}
}  // namespace TiltBrush
