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

  /// A command to toggle selection on a set of strokes and widgets.
  ///
  /// The command holds a couple collections of objects and acts inverts selection
  /// for these objects.
  ///
  /// This command borrows a lot from SelectCommand.
  public class InvertSelectionCommand : BaseCommand {
    private List<Stroke> m_StrokesOn;
    private List<Stroke> m_StrokesOff;
    private List<GrabWidget> m_WidgetsOn;
    private List<GrabWidget> m_WidgetsOff;

    override public bool NeedsSave {
      get {
        return false;
      }
    }

    /// The command takes additional ownership over the objects passed, copying its
    /// objects into a new array that is never mutated.
    public InvertSelectionCommand(
        ICollection<Stroke> strokesOn,
        ICollection<Stroke> strokesOff,
        ICollection<GrabWidget> widgetsOn,
        ICollection<GrabWidget> widgetsOff,
        BaseCommand parent = null)
      : base(parent) {
      if (strokesOn != null) {
        m_StrokesOn = new List<Stroke>();
        m_StrokesOn.AddRange(strokesOn);
      }
      if (strokesOff != null) {
        m_StrokesOff = new List<Stroke>();
        m_StrokesOff.AddRange(strokesOff);
      }
      if (widgetsOn != null) {
        m_WidgetsOn = new List<GrabWidget>();
        m_WidgetsOn.AddRange(widgetsOn);
      }
      if (widgetsOff != null) {
        m_WidgetsOff = new List<GrabWidget>();
        m_WidgetsOff.AddRange(widgetsOff);
      }
    }

    protected override void OnRedo() {
      if (m_StrokesOn != null) {
        SelectionManager.m_Instance.SelectStrokes(m_StrokesOn);
      }
      if (m_StrokesOff != null) {
        SelectionManager.m_Instance.DeselectStrokes(m_StrokesOff);
      }
      if (m_WidgetsOn != null) {
        SelectionManager.m_Instance.SelectWidgets(m_WidgetsOn);
      }
      if (m_WidgetsOff != null) {
        SelectionManager.m_Instance.DeselectWidgets(m_WidgetsOff);
      }

      SelectionManager.m_Instance.UpdateSelectionWidget();
    }

    protected override void OnUndo() {
      if (m_StrokesOn != null) {
        SelectionManager.m_Instance.DeselectStrokes(m_StrokesOn);
      }
      if (m_StrokesOff != null) {
        SelectionManager.m_Instance.SelectStrokes(m_StrokesOff);
      }
      if (m_WidgetsOn != null) {
        SelectionManager.m_Instance.DeselectWidgets(m_WidgetsOn);
      }
      if (m_WidgetsOff != null) {
        SelectionManager.m_Instance.SelectWidgets(m_WidgetsOff);
      }

      SelectionManager.m_Instance.UpdateSelectionWidget();
    }

    public override bool Merge(BaseCommand other) {
      return false;
    }
  }
} // namespace TiltBrush
