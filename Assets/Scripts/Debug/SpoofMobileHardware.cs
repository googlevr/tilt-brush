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

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


namespace TiltBrush {
public static class SpoofMobileHardware {
#if UNITY_EDITOR
  private const string kSpoofMobileHardware = "Tilt/Spoof Mobile Hardware";

  public static bool MobileHardware {
    get { return EditorPrefs.GetBool(kSpoofMobileHardware, false); }
    set { EditorPrefs.SetBool(kSpoofMobileHardware, value); }
  }

  [MenuItem(kSpoofMobileHardware, validate = true)]
  private static bool ValidateMenuSpoofMobileHardware() {
    Menu.SetChecked(kSpoofMobileHardware, MobileHardware);
    return true;
  }

  [MenuItem(kSpoofMobileHardware, validate = false, priority = 14)]
  private static void MenuSpoofMobileHardware() {
    MobileHardware = !MobileHardware;
  }

#else
  public static bool MobileHardware { get { return false; } }
#endif

}
} // namespace TiltBrush

