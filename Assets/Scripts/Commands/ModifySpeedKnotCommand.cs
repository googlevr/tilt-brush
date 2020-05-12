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
public class ModifySpeedKnotCommand : BaseKnotCommand<CameraPathSpeedKnot> {
  private float m_StartSpeed;
  private float m_EndSpeed;
  private bool m_Final;

  public ModifySpeedKnotCommand(CameraPathSpeedKnot knot, float endSpeed,
      bool mergesWithCreateCommand = false, bool final = false,
      BaseCommand parent = null) : base(knot, mergesWithCreateCommand, parent) {
    m_EndSpeed = endSpeed;
    m_Final = final;
    m_StartSpeed = knot.SpeedValue;
  }

  override public bool NeedsSave { get => true; }

  override protected void OnUndo() {
    Knot.SpeedValue = m_StartSpeed;
    Knot.RefreshVisuals();
    WidgetManager.m_Instance.CameraPathsVisible = true;
  }

  override protected void OnRedo() {
    Knot.SpeedValue = m_EndSpeed;
    Knot.RefreshVisuals();
    WidgetManager.m_Instance.CameraPathsVisible = true;
  }

  override public bool Merge(BaseCommand other) {
    if (base.Merge(other)) { return true; }
    if (m_Final) { return false; }

    ModifySpeedKnotCommand skc = other as ModifySpeedKnotCommand;
    if (skc == null) { return false; }

    m_EndSpeed = skc.m_EndSpeed;
    m_Final = skc.m_Final;
    return true;
  }
}
} // namespace TiltBrush
