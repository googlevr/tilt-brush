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

public class MusicButton : BaseButton {
  private int m_GameMusicIndex;

  override protected void ConfigureTextureAtlas() {
    if (SketchControlsScript.m_Instance.AtlasIconTextures) {
      // Music icons are assigned later.  We want atlasing on all our
      // buttons, so just set it to the default for now.
      RefreshAtlasedMaterial();
    } else {
      base.ConfigureTextureAtlas();
    }
  }

  public void SetPreset(int index) {
    m_GameMusicIndex = index;
    GameMusic mz = AudioManager.m_Instance.GetGameMusic(m_GameMusicIndex);
    SetButtonTexture(mz.iconImage);
    SetDescriptionText(mz.description);
  }

  override protected void OnButtonPressed() {
    bool musicPlaying = false;
    if (AudioManager.m_Instance.GetActiveGameMusic() == m_GameMusicIndex) {
      // Stop current music.
      AudioManager.m_Instance.StopMusic();
    } else {
      // Switch music.
      AudioManager.m_Instance.PlayGameMusic(m_GameMusicIndex);
      musicPlaying = true;
    }

    if (musicPlaying != App.Instance.RequestingAudioReactiveMode) {
      App.Instance.ToggleAudioReactiveBrushesRequest();
    }
  }
}
}  // namespace TiltBrush
