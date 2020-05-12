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
public class DeleteSelectionCommand : BaseCommand {
  private Stroke[] m_Strokes;
  private List<GrabWidget> m_Widgets;
  private TrTransform m_InitialSelectionTransform;

  public DeleteSelectionCommand(
      ICollection<Stroke> strokes,
      ICollection<GrabWidget> widgets,
      BaseCommand parent = null) : base(parent) {
    if (strokes != null) {
      m_Strokes = new Stroke[strokes.Count];
      strokes.CopyTo(m_Strokes, 0);
    }
    if (widgets != null) {
      m_Widgets = new List<GrabWidget>();
      m_Widgets.AddRange(widgets);
    }
    m_InitialSelectionTransform = SelectionManager.m_Instance.SelectionTransform;
  }

  public override bool NeedsSave { get { return true; } }

  protected override void OnRedo() {
    SelectionManager.m_Instance.SelectionTransform = TrTransform.identity;
    if (m_Strokes != null) {
      for (int i = 0; i < m_Strokes.Length; ++i) {
        Stroke stroke = m_Strokes[i];
        switch (m_Strokes[i].m_Type) {
        case Stroke.Type.BrushStroke:
          BaseBrushScript rBrushScript = stroke.m_Object.GetComponent<BaseBrushScript>();
          if (rBrushScript) {
            rBrushScript.HideBrush(true);
          }
          break;
        case Stroke.Type.BatchedBrushStroke:
          stroke.m_BatchSubset.m_ParentBatch.DisableSubset(stroke.m_BatchSubset);
          break;
        case Stroke.Type.NotCreated:
          Debug.LogError("Unexpected: redo delete NotCreated stroke");
          break;
        }
        TiltMeterScript.m_Instance.AdjustMeter(stroke, up: false);
      }
    }
    if (m_Widgets != null) {
      for (int i = 0; i < m_Widgets.Count; ++i) {
        m_Widgets[i].Hide();
      }
    }

    SelectionManager.m_Instance.DeregisterStrokesInSelectionCanvas(m_Strokes);
    SelectionManager.m_Instance.DeregisterWidgetsInSelectionCanvas(m_Widgets);
  }

  protected override void OnUndo() {
    SelectionManager.m_Instance.SelectionTransform = m_InitialSelectionTransform;
    if (m_Strokes != null) {
      for (int i = 0; i < m_Strokes.Length; ++i) {
        Stroke stroke = m_Strokes[i];
        switch (m_Strokes[i].m_Type) {
        case Stroke.Type.BrushStroke:
          BaseBrushScript rBrushScript = stroke.m_Object.GetComponent<BaseBrushScript>();
          if (rBrushScript) {
            rBrushScript.HideBrush(false);
          }
          break;
        case Stroke.Type.BatchedBrushStroke:
          stroke.m_BatchSubset.m_ParentBatch.EnableSubset(stroke.m_BatchSubset);
          break;
        case Stroke.Type.NotCreated:
          Debug.LogError("Unexpected: redo delete NotCreated stroke");
          break;
        }
        TiltMeterScript.m_Instance.AdjustMeter(stroke, up: true);
      }
    }
    if (m_Widgets != null) {
      for (int i = 0; i < m_Widgets.Count; ++i) {
        m_Widgets[i].RestoreFromToss();
      }
    }

    SelectionManager.m_Instance.RegisterStrokesInSelectionCanvas(m_Strokes);
    SelectionManager.m_Instance.RegisterWidgetsInSelectionCanvas(m_Widgets);
    SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.SelectionTool);
  }
}
} // namespace TiltBrush