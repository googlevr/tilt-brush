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
public class VideoPositionSlider : BaseSlider {

  private VideoWidget m_VideoWidget;

  public VideoWidget VideoWidget {
    get { return m_VideoWidget; }
    set { m_VideoWidget = value; }
  }

  public override void UpdateValue(float value) {
    if (m_VideoWidget == null || m_VideoWidget.VideoController == null) {
      return;
    }
    m_VideoWidget.VideoController.Position = value;
  }

  protected virtual void Update() {
    m_IsAvailable = m_VideoWidget != null && m_VideoWidget.VideoController != null;
    if (m_IsAvailable) {
      m_CurrentValue = m_VideoWidget.VideoController.Position;
    } else {
      m_CurrentValue = 0;
    }
    SetSliderPositionToReflectValue();
  }
}
} // namespace TiltBrush