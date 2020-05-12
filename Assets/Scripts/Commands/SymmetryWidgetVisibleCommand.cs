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
public class SymmetryWidgetVisibleCommand : BaseCommand {
  private bool m_Visible;

  public SymmetryWidgetVisibleCommand(SymmetryWidget widget, BaseCommand parent = null)
      : base(parent) {
    m_Visible = widget.Showing;
  }

  public override bool NeedsSave { get { return base.NeedsSave; } }

  protected override void OnRedo() {
    PointerManager.m_Instance.DisablePointerPreviewLine();
    PointerManager.m_Instance.SetSymmetryMode(m_Visible ?
        PointerManager.SymmetryMode.SinglePlane : PointerManager.SymmetryMode.None, false);
    if (!SketchControlsScript.m_Instance.IsUserInteractingWithUI()) {
      PointerManager.m_Instance.AllowPointerPreviewLine(true);
    }
  }

  protected override void OnUndo() {
    PointerManager.m_Instance.DisablePointerPreviewLine();
    PointerManager.m_Instance.SetSymmetryMode(m_Visible ?
        PointerManager.SymmetryMode.None : PointerManager.SymmetryMode.SinglePlane, false);
    if (!SketchControlsScript.m_Instance.IsUserInteractingWithUI()) {
      PointerManager.m_Instance.AllowPointerPreviewLine(true);
    }
  }
}
} // namespace TiltBrush