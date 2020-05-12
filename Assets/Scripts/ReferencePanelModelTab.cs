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

using System;

namespace TiltBrush {
public class ReferencePanelModelTab : ReferencePanelTab {

  public class ModelIcon : ReferenceIcon {
    public ModelButton ModelButton {
      get { return Button as ModelButton; }
    }
    public override void Refresh(int iCatalog) {
      ModelButton.SetPreset(ModelCatalog.m_Instance.GetModelAtIndex(iCatalog), iCatalog);
    }
  }

  private int m_LastPageIndexForLoad = -1;

  public override IReferenceItemCatalog Catalog {
    get { return ModelCatalog.m_Instance; }
  }
  public override ReferenceButton.Type ReferenceButtonType {
    get { return ReferenceButton.Type.Models; }
  }

  protected override Type ButtonType {
    get { return typeof(ModelButton); }
  }
  protected override Type IconType {
    get { return typeof(ModelIcon); }
  }

  public override void RefreshTab(bool selected) {
    base.RefreshTab(selected);
    if (selected) {
      // Destroy previews so only the thumbnail is visible.
      // Only do this when the page changes, to avoid thrashing the game state.
      if (m_LastPageIndexForLoad != PageIndex) {
        m_LastPageIndexForLoad = PageIndex;
        for (int i = 0; i < m_Icons.Length; i++) {
          (m_Icons[i].Button as ModelButton).DestroyModelPreview();
        }
      }
    }
  }
}
} // namespace TiltBrush