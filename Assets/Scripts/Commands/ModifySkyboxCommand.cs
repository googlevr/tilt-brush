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
public class ModifySkyboxCommand : BaseCommand {
  private Color m_StartColorA;
  private Color m_EndColorA;
  private Color m_StartColorB;
  private Color m_EndColorB;
  private Quaternion m_StartOrientation;
  private Quaternion m_EndOrientation;

  private bool m_Final;

  public ModifySkyboxCommand(Color endColorA, Color endColorB, Quaternion endOrient,
      bool final = false, BaseCommand parent = null) : base(parent) {
    m_StartColorA = SceneSettings.m_Instance.SkyColorA;
    m_StartColorB = SceneSettings.m_Instance.SkyColorB;
    m_StartOrientation = SceneSettings.m_Instance.GradientOrientation;
    m_EndColorA = endColorA;
    m_EndColorB = endColorB;
    m_EndOrientation = endOrient;
    m_Final = final;
  }

  override public bool NeedsSave { get { return true; } }

  override protected void OnUndo() {
    SceneSettings.m_Instance.SkyColorA = m_StartColorA;
    SceneSettings.m_Instance.SkyColorB = m_StartColorB;
    SceneSettings.m_Instance.GradientOrientation = m_StartOrientation;
  }

  override protected void OnRedo() {
    SceneSettings.m_Instance.SkyColorA = m_EndColorA;
    SceneSettings.m_Instance.SkyColorB = m_EndColorB;
    SceneSettings.m_Instance.GradientOrientation = m_EndOrientation;
  }

  public override bool Merge(BaseCommand other) {
    if (base.Merge(other)) { return true; }
    if (m_Final) { return false; }
    ModifySkyboxCommand skyboxCommand = other as ModifySkyboxCommand;
    if (skyboxCommand == null) { return false; }
    m_EndColorA = skyboxCommand.m_EndColorA;
    m_EndColorB = skyboxCommand.m_EndColorB;
    m_EndOrientation = skyboxCommand.m_EndOrientation;
    m_Final = skyboxCommand.m_Final;
    return true;
  }
}
} // namespace TiltBrush
