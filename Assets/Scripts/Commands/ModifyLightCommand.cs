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
public class ModifyLightCommand : BaseCommand {
  private LightMode m_ModifiedLight;
  private Color m_StartColor;
  private Color m_EndColor;
  private Quaternion m_StartRot;
  private Quaternion m_EndRot;
  private bool m_Final;

  public ModifyLightCommand(LightMode light, Color endColor, Quaternion endRot,
      bool final = false, BaseCommand parent = null) : base(parent) {
    m_ModifiedLight = light;
    if (light == LightMode.Ambient) {
      m_StartColor = RenderSettings.ambientLight;
      m_StartRot = Quaternion.identity;
    } else {
      Light lightObj = App.Scene.GetLight((int)light);
      m_StartColor = lightObj.color;
      m_StartRot = lightObj.transform.localRotation;
    }
    m_EndColor = endColor;
    m_EndRot = endRot;
    m_Final = final;
  }

  override public bool NeedsSave { get { return true; } }

  override protected void OnUndo() {
    Light lightObj = null;
    switch (m_ModifiedLight) {
    case LightMode.Ambient:
      RenderSettings.ambientLight = m_StartColor;
      break;
    case LightMode.Shadow:
    case LightMode.NoShadow:
      lightObj = App.Scene.GetLight((int)m_ModifiedLight);
      lightObj.color = m_StartColor;
      lightObj.transform.localRotation = m_StartRot;
      break;
    }
    PanelManager.m_Instance.ExecuteOnPanel<LightsPanel>(x => x.Refresh());
  }

  override protected void OnRedo() {
    Light lightObj = null;
    switch (m_ModifiedLight) {
    case LightMode.Ambient:
      RenderSettings.ambientLight = m_EndColor;
      break;
    case LightMode.Shadow:
    case LightMode.NoShadow:
      lightObj = App.Scene.GetLight((int)m_ModifiedLight);
      lightObj.color = m_EndColor;
      lightObj.transform.localRotation = m_EndRot;
      break;
    }
    PanelManager.m_Instance.ExecuteOnPanel<LightsPanel>(x => x.Refresh());
  }

  override public bool Merge(BaseCommand other) {
    if (base.Merge(other)) { return true; }
    if (m_Final) { return false; }
    ModifyLightCommand lightCommand = other as ModifyLightCommand;
    if (lightCommand == null || m_ModifiedLight != lightCommand.m_ModifiedLight) { return false; }
    m_EndColor = lightCommand.m_EndColor;
    m_EndRot = lightCommand.m_EndRot;
    m_Final = lightCommand.m_Final;
    return true;
  }
}
} // namespace TiltBrush