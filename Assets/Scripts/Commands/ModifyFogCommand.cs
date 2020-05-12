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
public class ModifyFogCommand : BaseCommand {
  private Color m_StartColor;
  private Color m_EndColor;
  private float m_StartDensity;
  private float m_EndDensity;
  private bool m_Final;

  public ModifyFogCommand(Color endColor, float endDensity, bool final = false,
      BaseCommand parent = null) : base(parent) {
    m_StartColor = SceneSettings.m_Instance.FogColor;
    m_StartDensity = SceneSettings.m_Instance.FogDensity;
    m_EndColor = endColor;
    m_EndDensity = endDensity;
    m_Final = final;
  }

  override public bool NeedsSave { get { return true; } }

  override protected void OnUndo() {
    SceneSettings.m_Instance.FogColor = m_StartColor;
    SceneSettings.m_Instance.FogDensity = m_StartDensity;
  }

  override protected void OnRedo() {
    SceneSettings.m_Instance.FogColor = m_EndColor;
    SceneSettings.m_Instance.FogDensity = m_EndDensity;
  }

  override public bool Merge(BaseCommand other) {
    if (base.Merge(other)) { return true; }
    if (m_Final) { return false; }
    ModifyFogCommand fogCommand = other as ModifyFogCommand;
    if (fogCommand == null) { return false; }
    m_EndColor = fogCommand.m_EndColor;
    m_EndDensity = fogCommand.m_EndDensity;
    m_Final = fogCommand.m_Final;
    return true;
  }
}
} // namespace TiltBrush
