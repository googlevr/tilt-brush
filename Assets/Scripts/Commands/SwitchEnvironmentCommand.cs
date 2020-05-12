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
public class SwitchEnvironmentCommand : BaseCommand {
  private CustomLights m_PrevLights;
  private CustomEnvironment m_PrevBackdrop;
  private Environment m_PrevEnvironment;
  private Environment m_NextEnvironment;

  public SwitchEnvironmentCommand(Environment nextEnv, BaseCommand parent = null) : base(parent) {
    m_NextEnvironment = nextEnv;
    m_PrevBackdrop = SceneSettings.m_Instance.CustomEnvironment;
    if (SceneSettings.m_Instance.IsTransitioning) {
      m_PrevEnvironment = SceneSettings.m_Instance.GetDesiredPreset();
    } else {
      m_PrevLights = LightsControlScript.m_Instance.CustomLights;
      m_PrevEnvironment = SceneSettings.m_Instance.CurrentEnvironment;
    }
  }

  public override bool NeedsSave {
    get {
      return m_PrevEnvironment != m_NextEnvironment ||
        m_PrevBackdrop != null || m_PrevLights != null;
    }
  }

  protected override bool IsNoop { get { return !NeedsSave; } }

  protected override void OnRedo() {
    SceneSettings.m_Instance.RecordSkyColorsForFading();
    SceneSettings.m_Instance.SetDesiredPreset(m_NextEnvironment,
      keepSceneTransform: true, forceTransition: true, hasCustomLights: false);
  }

  protected override void OnUndo() {
    SceneSettings.m_Instance.RecordSkyColorsForFading();
    if (m_PrevBackdrop != null) {
      SceneSettings.m_Instance.SetCustomEnvironment(m_PrevBackdrop, m_PrevEnvironment);
    }
    SceneSettings.m_Instance.SetDesiredPreset(m_PrevEnvironment,
      keepSceneTransform: true,
      forceTransition:
        m_PrevEnvironment == m_NextEnvironment && m_PrevBackdrop == null && m_PrevLights == null,
      hasCustomLights: m_PrevLights != null);
    if (m_PrevLights != null) {
      LightsControlScript.m_Instance.CustomLights = m_PrevLights;
    }
  }

  override public bool Merge(BaseCommand other) {
    if (base.Merge(other)) { return true; }
    SwitchEnvironmentCommand command = other as SwitchEnvironmentCommand;
    if (command == null) { return false; }
    m_NextEnvironment = command.m_NextEnvironment;
    return true;
  }
}
} // namespace TiltBrush
