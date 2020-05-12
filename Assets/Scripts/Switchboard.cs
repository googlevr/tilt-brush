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

namespace TiltBrush {
  public class Switchboard {
    public event Action ToolChanged;
    public event Action MirrorVisibilityChanged;
    public event Action PanelDismissed;
    public event Action StencilModeChanged;
    public event Action AudioReactiveStateChanged;
    public event Action MemoryExceededChanged;
    public event Action MemoryWarningAcceptedChanged;
    public event Action CameraPathVisibilityChanged;
    public event Action CameraPathKnotChanged;
    public event Action CameraPathDeleted;
    public event Action<CameraPathTool.Mode> CameraPathModeChanged;
    public event Action CameraPathCreated;
    public event Action CurrentCameraPathChanged;
    public event Action AllWidgetsDestroyed;
    public event Action SelectionChanged;
    public event Action<VideoWidget> VideoWidgetActivated;
    public event Action VideoRecordingStopped;

    public void TriggerAdvancedPanelsChanged() {
      // Poking the current color notifies listeners, specifically the color picker,
      // which then updates to reflect the current color.
      // This keeps the beginner and advanced color pickers in sync when the mode changes.
      App.BrushColor.CurrentColor = App.BrushColor.CurrentColor;
    }

    public void TriggerToolChanged() {
      ToolChanged?.Invoke();
    }

    public void TriggerMirrorVisibilityChanged() {
      MirrorVisibilityChanged?.Invoke();
    }

    public void TriggerPanelDismissed() {
      PanelDismissed?.Invoke();
    }

    public void TriggerStencilModeChanged() {
      StencilModeChanged?.Invoke();
    }

    public void TriggerAudioReactiveStateChanged() {
      AudioReactiveStateChanged?.Invoke();
    }

    public void TriggerMemoryExceededChanged() {
      MemoryExceededChanged?.Invoke();
    }

    public void TriggerMemoryWarningAcceptedChanged() {
      MemoryWarningAcceptedChanged?.Invoke();
    }

    public void TriggerCameraPathVisibilityChanged() {
      CameraPathVisibilityChanged?.Invoke();
    }

    public void TriggerCameraPathKnotChanged() {
      CameraPathKnotChanged?.Invoke();
    }

    public void TriggerCameraPathDeleted() {
      CameraPathDeleted?.Invoke();
    }

    public void TriggerCameraPathModeChanged(CameraPathTool.Mode mode) {
      CameraPathModeChanged?.Invoke(mode);
    }

    public void TriggerCameraPathCreated() {
      CameraPathCreated?.Invoke();
    }

    public void TriggerCurrentCameraPathChanged() {
      CurrentCameraPathChanged?.Invoke();
    }

    public void TriggerAllWidgetsDestroyed() {
      AllWidgetsDestroyed?.Invoke();
    }

    public void TriggerSelectionChanged() {
      SelectionChanged?.Invoke();
    }

    public void TriggerVideoWidgetActivated(VideoWidget widget) {
      VideoWidgetActivated?.Invoke(widget);
    }

    public void TriggerVideoRecordingStopped() {
      VideoRecordingStopped?.Invoke();
    }
  }
} // namespace TiltBrush