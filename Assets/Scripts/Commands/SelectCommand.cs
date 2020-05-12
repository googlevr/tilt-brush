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

using System.Collections.Generic;

namespace TiltBrush {

  /// A command to select or deselect a set of strokes and widgets.
  ///
  /// The command holds a collection of objects and acts as either a selection command
  /// or deselection command for these objects.
  ///
  /// The act of selecting means to move the objects from the active canvas to the
  /// selection canvas, and deselecting moves the objects from the selection canvas
  /// back to the active canvas.
  ///
  /// It also remembers the selection's transform from before the selection/deselection
  /// took place. This is used in the case of undoing a deselection of all selected
  /// objects to preserve the selection's grab widget orientation.
  public class SelectCommand : BaseCommand {
    private List<Stroke> m_Strokes;
    private List<GrabWidget> m_Widgets;
    private TrTransform m_InitialTransform;
    private bool m_Deselect;
    private bool m_Initial;
    private bool m_Final;
    private bool m_CheckForClearedSelection;
    private bool m_IsGrabbingGroup;
    private bool m_IsEndGrabbingGroup;

    override public bool NeedsSave {
      get {
        // We only need to save if objects have been moved, and that only
        // occurs when a transformed selection has been deselecting, which
        // rebakes that object into the main canvas.
        return m_Deselect && m_InitialTransform != TrTransform.identity;
      }
    }

    public void ResetInitialTransform() {
      m_InitialTransform = TrTransform.identity;
    }

    /// The command takes additional ownership over the objects passed, copying its
    /// objects into a new array that is never mutated.
    ///
    /// Initial pose is the pose of the selection from before the selection/deselection
    /// takes place. It's needed because if the action is to deselect the very last objects,
    /// the SelectionManager will automatically reset the selection transform to identity. And
    /// then if we want to undo that selection, we need to restore its initial transform so that
    /// the selection widget is appropriately rotated.
    ///
    /// Preserving the orientation of the selection widget is important for two reasons:
    /// 1: Aesthetics. If a user deselects a rotated selection then immediately undoes that
    ///    deselection, it would be jarring if the selection bounds rotated.
    /// 2: To preserve undoing transformations of the selection widget. When the user grabs and moves
    ///    the selection, the selection widget maintains its own movewidget commands on the stack, so
    ///    it'd expect the widget not to have been moved by an outside party between redoing a move
    ///    and then undoing it.
    public SelectCommand(
        ICollection<Stroke> strokes,
        ICollection<GrabWidget> widgets,
        TrTransform initialTransform,
        bool deselect = false, bool initial = false, bool checkForClearedSelection = false,
        bool isGrabbingGroup = false, bool isEndGrabbingGroup = false,
        BaseCommand parent = null)
      : base(parent) {
      var selectedGroups = new HashSet<SketchGroupTag>();

      var strokesNotGrouped = new HashSet<Stroke>();
      if (strokes != null) {
        // Get strokes that are not grouped and groups among selected strokes.
        foreach (var stroke in strokes) {
          if (stroke.Group == SketchGroupTag.None) {
            strokesNotGrouped.Add(stroke);
          } else {
            selectedGroups.Add(stroke.Group);
          }
        }
      }

      var widgetsNotGrouped = new HashSet<GrabWidget>();
      if (widgets != null) {
        // Get widgets that are not grouped and groups among selected widgets.
        foreach (var widget in widgets) {
          if (widget.Group == SketchGroupTag.None) {
            widgetsNotGrouped.Add(widget);
          } else {
            selectedGroups.Add(widget.Group);
          }
        }
      }

      // Get the grouped strokes.
      var strokesGrouped = new HashSet<Stroke>();
      foreach (var group in selectedGroups) {
        strokesGrouped.UnionWith(SelectionManager.m_Instance.StrokesInGroup(group));
      }

      // Get the grouped widgets.
      var widgetsGrouped = new HashSet<GrabWidget>();
      foreach (var group in selectedGroups) {
        widgetsGrouped.UnionWith(SelectionManager.m_Instance.WidgetsInGroup(group));
      }

      m_Strokes = new List<Stroke>();
      m_Strokes.AddRange(strokesGrouped);
      m_Strokes.AddRange(strokesNotGrouped);

      m_Widgets = new List<GrabWidget>();
      m_Widgets.AddRange(widgetsGrouped);
      m_Widgets.AddRange(widgetsNotGrouped);

      m_InitialTransform = initialTransform;
      m_Deselect = deselect;
      m_Initial = initial;
      m_CheckForClearedSelection = checkForClearedSelection;
      m_IsGrabbingGroup = isGrabbingGroup;
      m_IsEndGrabbingGroup = isEndGrabbingGroup;
    }

    protected override void OnRedo() {
      if (m_Deselect) {
        if (m_Strokes != null) {
          SelectionManager.m_Instance.DeselectStrokes(m_Strokes);
        }
        if (m_Widgets != null) {
          SelectionManager.m_Instance.DeselectWidgets(m_Widgets);
        }
      } else {
        if (m_Strokes != null) {
          SelectionManager.m_Instance.SelectStrokes(m_Strokes);
        }
        if (m_Widgets != null) {
          SelectionManager.m_Instance.SelectWidgets(m_Widgets);
        }
      }

      SelectionManager.m_Instance.UpdateSelectionWidget();

      if (m_CheckForClearedSelection) {
        if (!SelectionManager.m_Instance.HasSelection) {
          SelectionManager.m_Instance.RemoveFromSelection(false);
        }
      }

      App.Switchboard.TriggerSelectionChanged();
    }

    protected override void OnUndo() {
      // In the future, we should check for a cleared selection that happen on a redo of this
      // command.
      m_CheckForClearedSelection = true;

      SelectionManager.m_Instance.SelectionTransform = m_InitialTransform;
      if (m_Deselect) {
        if (m_Strokes != null) {
          SelectionManager.m_Instance.SelectStrokes(m_Strokes);
        }
        if (m_Widgets != null) {
          SelectionManager.m_Instance.SelectWidgets(m_Widgets);
        }
      } else {
        if (m_Strokes != null) {
          SelectionManager.m_Instance.DeselectStrokes(m_Strokes);
        }
        if (m_Widgets != null) {
          SelectionManager.m_Instance.DeselectWidgets(m_Widgets);
        }
      }

      SelectionManager.m_Instance.UpdateSelectionWidget();

      if (m_CheckForClearedSelection) {
        if (!SelectionManager.m_Instance.HasSelection) {
          SelectionManager.m_Instance.RemoveFromSelection(false);
        }
      }

      App.Switchboard.TriggerSelectionChanged();
    }

    public override bool Merge(BaseCommand other) {
      var newSelectCommand = other as SelectCommand;
      if (m_Final) { return false; }
      if (m_IsGrabbingGroup) {
        if (other is MoveWidgetCommand || other is DeleteSelectionCommand) {
          // Merge with moves and deletes while grabbing a group.
          m_Children.Add(other);
          return true;
        } else if (newSelectCommand != null && newSelectCommand.m_IsGrabbingGroup) {
          // Merge with other selections while grabbing a group.
          if (newSelectCommand.m_IsEndGrabbingGroup) {
            // We've hit the end of the grabbing group gestures, so finalize it.
            m_Final = true;
          }
          m_Children.Add(other);
          return true;
        }
      }
      if (m_Deselect) {
        if (other is GroupStrokesAndWidgetsCommand) {
          m_Children.Add(other);
          m_Final = true;
          return true;
        } else if (other is DeleteStrokeCommand) {
          m_Children.Add(other);
          return true;
        }
      }
      if (newSelectCommand != null) {
        if (newSelectCommand.m_Deselect == m_Deselect && !newSelectCommand.m_Initial) {
          m_Children.Add(other);
          return true;
        }
      }
      return false;
    }
  }
} // namespace TiltBrush
