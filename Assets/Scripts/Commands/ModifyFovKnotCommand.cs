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
public class ModifyFovKnotCommand : BaseKnotCommand<CameraPathFovKnot> {
  private float m_StartFov;
  private float m_EndFov;
  private bool m_Final;

  public ModifyFovKnotCommand(CameraPathFovKnot knot, float endFov,
      bool mergesWithCreateCommand = false, bool final = false,
      BaseCommand parent = null) : base(knot, mergesWithCreateCommand, parent) {
    m_EndFov = endFov;
    m_Final = final;
    m_StartFov = knot.FovValue;
  }

  override public bool NeedsSave { get { return true; } }

  override protected void OnUndo() {
    Knot.FovValue = m_StartFov;
    Knot.RefreshVisuals();
    WidgetManager.m_Instance.CameraPathsVisible = true;
  }

  override protected void OnRedo() {
    Knot.FovValue = m_EndFov;
    Knot.RefreshVisuals();
    WidgetManager.m_Instance.CameraPathsVisible = true;
  }

  override public bool Merge(BaseCommand other) {
    if (base.Merge(other)) { return true; }
    if (m_Final) { return false; }

    ModifyFovKnotCommand fkc = other as ModifyFovKnotCommand;
    if (fkc == null) { return false; }

    m_EndFov = fkc.m_EndFov;
    m_Final = fkc.m_Final;
    return true;
  }
}
} // namespace TiltBrush
