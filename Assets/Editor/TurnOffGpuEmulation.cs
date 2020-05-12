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
using UnityEditor;

namespace TiltBrush {

/// Unity has an irritating habit of turning on Gpu Emulation automatically when you're developing
/// for Android, and it breaks some things in Tilt Brush. This class uses [InitializeOnLoad]
/// and a static class constructor to monitor whether Gpu Emulation has been turned on and
/// turns it off again when it finds it so.
/// This behaviour can be toggled using Tilt -> Keep GPU Emulation Turned Off.
[InitializeOnLoad]
public static class TurnOffGpuEmulation {

  private const string kGpuMenuString = "Edit/Graphics Emulation/No Emulation";
  private const string kTurnOffGpuEmulation = "Tilt/Keep GPU Emulation Turned Off";

  public static bool Enabled {
    get {
      return EditorPrefs.GetBool(kTurnOffGpuEmulation, true);
    }
    set {
      EditorPrefs.SetBool(kTurnOffGpuEmulation, value);
      Execute();
    }
  }

  static TurnOffGpuEmulation() {
    EditorApplication.delayCall += Execute;
  }

  [MenuItem(kTurnOffGpuEmulation, validate = true)]
  static bool ValidateKeepGpuEmTurnedOff() {
    Menu.SetChecked(kTurnOffGpuEmulation, Enabled);
    return true;
  }

  [MenuItem(kTurnOffGpuEmulation)]
  static void KeepGpuEmTurnedOff() {
    Enabled = !Enabled;
  }

  static void Execute() {
    if (Enabled) {
      if (!Menu.GetChecked(kGpuMenuString)) {
        EditorApplication.ExecuteMenuItem(kGpuMenuString);
        Debug.LogWarning("GPU Emulation was turned on; turning off.");
      }
    }
  }

}
}
