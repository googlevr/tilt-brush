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

/// A tool to pin (or unpin) widgets that intersect with its spinning orb.
///
/// Use the brush trigger to activate the orb. Intersecting widgets that can be pinned
/// will pinned if we're in pin mode, or unpin in unpin mode.
public class PinTool : ToggleStrokeModificationTool {
  private bool m_InPinningMode;
  private bool m_IntersectionThisFrame;

  public bool InPinMode {
    get { return m_InPinningMode; }
  }

  public bool CanToggle() {
    return !m_CurrentlyHot &&
        ((m_InPinningMode && WidgetManager.m_Instance.AnyWidgetsToUnpin) ||
        (!m_InPinningMode && WidgetManager.m_Instance.AnyWidgetsToPin));
  }

  override protected bool IsOn() {
    return InPinMode;
  }

  override public void Init() {
    base.Init();
    m_InPinningMode = true;
    UpdateMesh();
  }

  override public void EnableTool(bool bEnable) {
    base.EnableTool(bEnable);
    if (bEnable) {
      WidgetManager.m_Instance.RefreshPinAndUnpinAction += PinToolCallback;
      WidgetManager.m_Instance.WidgetsDormant = false;
      WidgetManager.m_Instance.RefreshPinAndUnpinLists();

      // Default to pin mode, unless the only thing we can do is unpin.
      m_InPinningMode = true;
      if (!WidgetManager.m_Instance.AnyWidgetsToPin &&
          WidgetManager.m_Instance.AnyWidgetsToUnpin) {
        m_InPinningMode = false;
      }
      RefreshPinState();

      // This is done in base.EnableTool(), but has to be done again here because our pin
      // state may have been updated by the code above.
      UpdateMesh();
    } else {
      WidgetManager.m_Instance.RefreshPinAndUnpinAction -= PinToolCallback;
    }
  }

  public void PinToolCallback() {}

  public void RefreshPinState() {
    if (!m_CurrentlyHot) {
      // If we just pinned our last object, switch to unpin mode.
      if (m_InPinningMode &&
          !WidgetManager.m_Instance.AnyWidgetsToPin &&
          WidgetManager.m_Instance.AnyWidgetsToUnpin) {
        StartToggleAnimation();
      } else if (!m_InPinningMode &&
          !WidgetManager.m_Instance.AnyWidgetsToUnpin &&
          WidgetManager.m_Instance.AnyWidgetsToPin) {
        StartToggleAnimation();
      }
    }
  }

  override protected void OnAnimationSwitch() {
    m_InPinningMode ^= true;
    AudioManager.m_Instance.PlayToggleSelect(m_ToolTransform.position, m_InPinningMode);
    InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush,
        (m_InPinningMode ? m_HapticsToggleOn : m_HapticsToggleOff));
  }

  override public void OnUpdateDetection() {
    // Check actions if we're not hot.
    if (!m_CurrentlyHot) {
      if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.ToggleSelection)) {
        if (CanToggle()) {
          StartToggleAnimation();
          SketchControlsScript.m_Instance.EatToolScaleInput();
        }
      }

      // If not hot disable highlight mesh.
      m_HighlightMesh.gameObject.SetActive(false);
    }

    // If we were hot, but we're not, clear our lists.
    if (!m_CurrentlyHot && m_ToolWasHot) {
      ResetToolRotation();
      ClearGpuFutureLists();
    }

    // Highlight all objects we can pin/unpin.
    if (!SketchControlsScript.m_Instance.IsUserAbleToInteractWithAnyWidget()) {
      WidgetManager.m_Instance.RegisterHighlightsForPinnableWidgets(m_InPinningMode);
    }
  }

  override protected int AdditionalGpuIntersectionLayerMasks() {
    return IsOn() ? WidgetManager.m_Instance.StencilLayerMask :
        WidgetManager.m_Instance.PinnedStencilLayerMask;
  }

  override protected bool HandleIntersectionWithWidget(GrabWidget widget) {
    if (widget.Pinned == m_InPinningMode) {
      return false;
    }

    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new PinWidgetCommand(widget, m_InPinningMode));
    m_LastIntersectionTime = Time.realtimeSinceStartup;
    m_IntersectionThisFrame = true;

    // Pull/add from our cached list.
    WidgetManager.m_Instance.RefreshPinAndUnpinLists();
    return true;
  }

  override public void IntersectionHappenedThisFrame() {
    // This is handled at this level because we discard brush intersection.
    if (m_IntersectionThisFrame) {
      HapticFeedback();
      m_IntersectionThisFrame = false;
    }
  }

  override public void AssignControllerMaterials(InputManager.ControllerName controller) {
    if (controller == InputManager.ControllerName.Brush) {
      InputManager.Brush.Geometry.ShowPinToggle(m_InPinningMode);
    }
  }
}
}  // namespace TiltBrush
