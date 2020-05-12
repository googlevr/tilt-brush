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
using System.Collections.Generic;

namespace TiltBrush {

  /// A command to toggle selection on a set of strokes and widgets.
  ///
  /// The command holds a couple collections of objects and flips them horizontally
  /// relative to the given plane.
  public class FlipSelectionCommand : BaseCommand {
    private List<Stroke> m_StrokesFlipped;
    private List<GrabWidget> m_WidgetsFlipped;
    private Plane m_FlipPlane_CS;

    // The canvas whose coordinate system the flip plane is defined in.
    private CanvasScript Canvas => App.Scene.SelectionCanvas;

    override public bool NeedsSave {
      get {
        return true;
      }
    }

    /// The command takes additional ownership over the objects passed, copying its
    /// objects into a new array that is never mutated.
    public FlipSelectionCommand(
        ICollection<Stroke> strokesFlipped,
        ICollection<GrabWidget> widgetsFlipped,
        Plane flipPlane,
        BaseCommand parent = null)
      : base(parent) {
      if (strokesFlipped != null) {
        m_StrokesFlipped = new List<Stroke>();
        m_StrokesFlipped.AddRange(strokesFlipped);
      }
      if (widgetsFlipped != null) {
        m_WidgetsFlipped = new List<GrabWidget>();
        m_WidgetsFlipped.AddRange(widgetsFlipped);
      }
      m_FlipPlane_CS = flipPlane;
    }

    protected override void OnRedo() {
      FlipSelection();
    }

    protected override void OnUndo() {
      FlipSelection();
    }

    public override bool Merge(BaseCommand other) {
      return false;
    }

    private void FlipSelection() {
      foreach (var stroke in m_StrokesFlipped) {
        for (int i = 0; i < stroke.m_ControlPoints.Length; i++) {
          var xf_CS = m_FlipPlane_CS.ReflectPoseKeepHandedness(
              TrTransform.TR(stroke.m_ControlPoints[i].m_Pos,
                             stroke.m_ControlPoints[i].m_Orient));
          stroke.m_ControlPoints[i].m_Pos = xf_CS.translation;
          stroke.m_ControlPoints[i].m_Orient = xf_CS.rotation;
        }

        // Recreate the stroke
        stroke.InvalidateCopy();
        stroke.Uncreate();
        stroke.Recreate();
      }

      foreach (var widget in m_WidgetsFlipped) {
        if (Canvas != widget.Canvas) {
          // The widget API we use only operates in the space of the widget's own canvas
          // We could get around this by transforming m_FlipPlane from the selection canvas
          // to the widget's canvas, but better to put effort into removing the localScale
          // restriction from GrabWidget.
#if false
          // aka SelectionCanvasPoseInWidgetCanvasSpace
          TrTransform widgetCanvasFromSelectionCanvas = widget.Canvas.AsCanvas[Canvas.transform];
          Plane flipPlaneInWidgetCanvasSpace = widgetCanvasFromSelectionCanvas * m_FlipPlane_CS;
#else
          Debug.LogError("Cannot currently flip widgets in other canvases");
          continue;
#endif
        }
        widget.LocalTransform = widget.SupportsNegativeSize
          ? m_FlipPlane_CS.ToTrTransform() * widget.LocalTransform
          : m_FlipPlane_CS.ReflectPoseKeepHandedness(widget.LocalTransform);
      }

      // Update the bounds for the selection widget
      SelectionManager.m_Instance.UpdateSelectionWidget();
    }
  }
} // namespace TiltBrush
