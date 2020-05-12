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

using UnityEditor;

namespace TiltBrush {
#if UNITY_ANDROID
[InitializeOnLoadAttribute]
public class SetStereoRenderPath {

  private static StereoRenderingPath s_OriginalStereoPath;

  static SetStereoRenderPath() {
    EditorApplication.playModeStateChanged += OnPlayModeChanged;
  }

  static void OnPlayModeChanged(PlayModeStateChange stateChange) {
    if (stateChange == PlayModeStateChange.ExitingEditMode) {
      s_OriginalStereoPath = PlayerSettings.stereoRenderingPath;
      PlayerSettings.stereoRenderingPath = StereoRenderingPath.SinglePass;
    }

    if (stateChange == PlayModeStateChange.EnteredEditMode) {
      PlayerSettings.stereoRenderingPath = s_OriginalStereoPath;
    }
  }
}
#endif
} // namespace TiltBrush
