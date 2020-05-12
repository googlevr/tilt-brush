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
using UnityEngine;

namespace TiltBrush {

// Brush Controller is an unfortunate name for this class, as it refers to the MVC "Controller"
// for storing information about the app state regarding the currently active brush.  It also
// maintains actions for objects to register with for status change notifications.
public class BrushController : MonoBehaviour {
  static public BrushController m_Instance;

  public event Action<Stroke> StrokeSelected;
  public event Action<BrushDescriptor> BrushChanged;
  public event Action BrushSetToDefault;

  private BrushDescriptor m_ActiveBrush;

  public BrushDescriptor ActiveBrush { get { return m_ActiveBrush; } }

  void Awake() {
    m_Instance = this;
  }

  public void SetActiveBrush(BrushDescriptor brush) {
    PointerManager.m_Instance.SetBrushForAllPointers(brush);
    AudioClip buttonAudio = BrushCatalog.m_Instance.GetBrush(brush.m_Guid).m_ButtonAudio;
    if (buttonAudio != null) {
      AudioManager.m_Instance.TriggerOneShot(buttonAudio,
          SketchControlsScript.m_Instance.GetUIReticlePos(), 1.0f);
    }
    m_ActiveBrush = brush;

    // Reset our tool when the user picks a new brush unless it is repainting.
    if (SketchSurfacePanel.m_Instance.GetCurrentToolType() != BaseTool.ToolType.RepaintTool &&
        SketchSurfacePanel.m_Instance.GetCurrentToolType() != BaseTool.ToolType.RebrushTool) {
      SketchSurfacePanel.m_Instance.EnableDefaultTool();
    }

    if (BrushChanged != null) {
      BrushChanged(brush);
    }
  }

  public void SetBrushToDefault() {
    m_ActiveBrush = BrushCatalog.m_Instance.DefaultBrush;

    if (BrushSetToDefault != null) {
      BrushSetToDefault();
    }
  }

  public void TriggerStrokeSelected(Stroke stroke) {
    if (stroke != null) {
      m_ActiveBrush = BrushCatalog.m_Instance.GetBrush(stroke.m_BrushGuid);
    }

    if (StrokeSelected != null) {
      StrokeSelected(stroke);
    }
  }
}
}  // namespace TiltBrush