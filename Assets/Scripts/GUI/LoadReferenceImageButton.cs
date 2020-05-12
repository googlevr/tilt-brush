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

namespace TiltBrush {

public class LoadReferenceImageButton : BaseButton {
  public ReferenceImage ReferenceImage { get; set; }

  public void RefreshDescription() {
    if (ReferenceImage != null) {
      SetDescriptionText(ReferenceImage.FileName);
    }
  }

  override protected void OnButtonPressed() {
    if (ReferenceImage == null) {
      return;
    }

    if (ReferenceImage.NotLoaded) {
      // Load-on-demand.
      ReferenceImage.SynchronousLoad();
    } else {
      CreateWidgetCommand command = new CreateWidgetCommand(
          WidgetManager.m_Instance.ImageWidgetPrefab, TrTransform.FromTransform(transform));
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
      ImageWidget widget = command.Widget as ImageWidget;
      widget.ReferenceImage = ReferenceImage;
      SketchControlsScript.m_Instance.EatGazeObjectInput();
      SelectionManager.m_Instance.RemoveFromSelection(false);
    }
  }

  override public void ResetState() {
    base.ResetState();

    // Make ourselves unavailable if our image has an error.
    bool available = false;
    if (ReferenceImage != null) {
      available = ReferenceImage.NotLoaded || ReferenceImage.Valid;
    }

    if (available != IsAvailable()) {
      SetButtonAvailable(available);
    }
  }
}
}  // namespace TiltBrush
