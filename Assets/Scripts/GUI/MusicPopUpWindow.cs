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

  public class MusicPopUpWindow : PagingPopUpWindow {
    protected override int m_DataCount {
      get { return AudioManager.m_Instance.NumGameMusics(); }
    }

    protected override void InitIcon(ImageIcon icon) {
      icon.m_Valid = true;
    }

    protected override void RefreshIcon(PagingPopUpWindow.ImageIcon icon, int index) {
      MusicButton iconButton = icon.m_IconScript as MusicButton;
      iconButton.SetPreset(index);
      iconButton.SetButtonSelected(AudioManager.m_Instance.GetActiveGameMusic() == index);
    }

    override public void Init(GameObject rParent, string sText) {
      // Find the active audio.
      int activeIndex = AudioManager.m_Instance.GetActiveGameMusic();
      if (activeIndex >= 0) {
        m_RequestedPageIndex = activeIndex / m_IconCountNavPage;
      }

      base.Init(rParent, sText);
    }
  }
}  // namespace TiltBrush