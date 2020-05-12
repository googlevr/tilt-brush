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

public class FreePaintTool : BaseTool {
  [SerializeField] private float m_AdjustSizeScalar;

  [SerializeField] private float m_HapticInterval = .1f;
  [SerializeField] private float m_HapticSizeUp;
  [SerializeField] private float m_HapticSizeDown;

  private bool m_PaintingActive;

  override public void Init() {
    base.Init();
    m_PaintingActive = false;
  }

  public override bool ShouldShowPointer() {
    return !PanelManager.m_Instance.IntroSketchbookMode;
  }

  override public void EnableTool(bool bEnable) {
    base.EnableTool(bEnable);
    if (!bEnable) {
      PointerManager.m_Instance.EnableLine(false);
      WidgetManager.m_Instance.ResetActiveStencil();
    }
    m_PaintingActive = false;
  }

  override public bool ShouldShowTouch() {
    return false;
  }

  static Quaternion sm_OrientationAdjust = Quaternion.Euler(new Vector3(0,180,0));
  override public void UpdateTool() {
    // Don't call base.UpdateTool() because we have a different 'stop eating input' check
    // for FreePaintTool.
    float brushTriggerRatio = InputManager.Brush.GetTriggerRatio();
    if (m_EatInput) {
      if (brushTriggerRatio <= 0.0f) {
        m_EatInput = false;
      }
    }
    if (m_ExitOnAbortCommand &&
        InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Abort)) {
      m_RequestExit = true;
    }

    PositionPointer();

    m_PaintingActive = !m_EatInput && !m_ToolHidden && brushTriggerRatio > 0;
    PointerManager.m_Instance.EnableLine(m_PaintingActive);
    PointerManager.m_Instance.PointerPressure = InputManager.Brush.GetTriggerRatio();
  }

  override public void LateUpdateTool() {
    // When the pointer manager is processing our line, don't stomp its position.
    if (!PointerManager.m_Instance.IsMainPointerProcessingLine()) {
      PositionPointer();
    }
  }

  override public void AssignControllerMaterials(InputManager.ControllerName controller) {
    if (controller == InputManager.ControllerName.Brush) {
      if (App.Instance.IsInStateThatAllowsPainting()) {
        if (m_PaintingActive) {
          // TODO: Make snap work with non-line shapes.
          if (PointerManager.m_Instance.StraightEdgeModeEnabled &&
              PointerManager.m_Instance.StraightEdgeGuideIsLine) {
            InputManager.Brush.Geometry.TogglePadSnapHint(
                PointerManager.m_Instance.StraightEdgeGuide.SnapEnabled,
                enabled:true);
          }
        } else {
          InputManager.Brush.Geometry.ShowBrushSizer();
        }
      }
    }
  }

  void PositionPointer() {
    // Angle the pointer according to the user-defined pointer angle.
    Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
    Vector3 pos = rAttachPoint.position;
    Quaternion rot = rAttachPoint.rotation * sm_OrientationAdjust;

    // Modify pointer position and rotation with stencils.
    WidgetManager.m_Instance.MagnetizeToStencils(ref pos, ref rot);

    PointerManager.m_Instance.SetPointerTransform(InputManager.ControllerName.Brush, pos, rot);
  }

  override public void UpdateSize(float fAdjustAmount) {
    float fPrevRatio = GetSize01();
    PointerManager.m_Instance.AdjustAllPointersBrushSize01(m_AdjustSizeScalar * fAdjustAmount);
    PointerManager.m_Instance.MarkAllBrushSizeUsed();
    float fCurrentRatio = GetSize01();

    float fHalfInterval = m_HapticInterval * 0.5f;
    int iPrevInterval = (int)((fPrevRatio + fHalfInterval) / m_HapticInterval);
    int iCurrentInterval = (int)((fCurrentRatio + fHalfInterval) / m_HapticInterval);
    if (!App.VrSdk.AnalogIsStick(InputManager.ControllerName.Brush)) {
      if (iCurrentInterval > iPrevInterval) {
        InputManager.m_Instance.TriggerHaptics(
            InputManager.ControllerName.Brush, m_HapticSizeUp);
      } else if (iCurrentInterval < iPrevInterval) {
        InputManager.m_Instance.TriggerHaptics(
            InputManager.ControllerName.Brush, m_HapticSizeDown);
      }
    }
  }

  override public float GetSize01() {
    return PointerManager.m_Instance.GetPointerBrushSize01(InputManager.ControllerName.Brush);
  }

  override public bool CanAdjustSize() {
    return App.Instance.IsInStateThatAllowsPainting();
  }
}
}  // namespace TiltBrush
