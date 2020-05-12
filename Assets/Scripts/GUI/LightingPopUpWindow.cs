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
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush {

public class LightingPopUpWindow : PagingPopUpWindow {
  private string m_CurrentPresetDesc;
  private List<TiltBrush.Environment> m_Environments;

  protected override int m_DataCount {
    get { return m_Environments.Count; }
  }

  protected override void InitIcon(ImageIcon icon) {
    icon.m_Valid = true;
  }

  protected override void RefreshIcon(PagingPopUpWindow.ImageIcon icon, int iCatalog) {
    LightingButton iconButton = icon.m_IconScript as LightingButton;
    iconButton.SetPreset(m_Environments[iCatalog]);
    iconButton.SetButtonSelected(m_CurrentPresetDesc == m_Environments[iCatalog].m_Description);
  }

  override public void Init(GameObject rParent, string sText) {
    //build list of lighting presets we're going to show
    m_Environments = EnvironmentCatalog.m_Instance.AllEnvironments.ToList();

    //find the active lighting preset
    TiltBrush.Environment rCurrentPreset = SceneSettings.m_Instance.GetDesiredPreset();
    if (rCurrentPreset != null) {
      //find the index of our current preset in the preset list
      int iPresetIndex = -1;
      m_CurrentPresetDesc = rCurrentPreset.m_Description;
      for (int i = 0; i < m_Environments.Count; ++i) {
        if (m_Environments[i].m_Description == m_CurrentPresetDesc) {
          iPresetIndex = i;
          break;
        }
      }

      if (iPresetIndex != -1) {
        //set our current page to show the active preset if we have more than one page
        if (m_Environments.Count > m_IconCountFullPage) {
          m_RequestedPageIndex = iPresetIndex / m_IconCountNavPage;
        }
      }
    }
    SceneSettings.m_Instance.FadingToDesiredEnvironment += OnFadingToDesiredEnvironment;

    base.Init(rParent, sText);
  }

  protected void OnFadingToDesiredEnvironment() {
    TiltBrush.Environment rCurrentPreset = SceneSettings.m_Instance.GetDesiredPreset();
    if (rCurrentPreset != null) {
      m_CurrentPresetDesc = rCurrentPreset.m_Description;
    }
    RefreshPage();
  }

  void OnDestroy() {
    SceneSettings.m_Instance.FadingToDesiredEnvironment -= OnFadingToDesiredEnvironment;
  }
}
}  // namespace TiltBrush
