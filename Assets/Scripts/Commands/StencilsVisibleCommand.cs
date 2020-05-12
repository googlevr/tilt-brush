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
public class StencilsVisibleCommand : BaseCommand {
  bool m_StencilsStartDisabled;

  public StencilsVisibleCommand(BaseCommand parent = null) : base(parent) {
    m_StencilsStartDisabled = WidgetManager.m_Instance.StencilsDisabled;
  }

  protected override void OnRedo() {
    WidgetManager.m_Instance.StencilsDisabled = !m_StencilsStartDisabled;
  }

  protected override void OnUndo() {
    WidgetManager.m_Instance.StencilsDisabled = m_StencilsStartDisabled;
  }
}
} // namespace TiltBrush