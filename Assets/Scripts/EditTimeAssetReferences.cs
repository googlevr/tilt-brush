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

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TiltBrush {

/// Used to store references to assets that we only need at edit or build time.
/// These things could be dumped into Config (ie, Main.unity) but then Unity
/// would bundle the data into builds, load it at runtime, etc.
public class EditTimeAssetReferences : ScriptableObject {
#if UNITY_EDITOR
  private static EditTimeAssetReferences sm_instance = null;
  public static EditTimeAssetReferences Instance {
    get {
      if (sm_instance == null) {
        sm_instance = AssetDatabase.LoadAssetAtPath<EditTimeAssetReferences>(
            "Assets/EditTimeAssetReferences.asset");
      }
      return sm_instance;
    }
  }
#endif

  // Probably we won't ever need more than these two pieces of data; any other
  // per-platform asset references can go into PlatformConfig.

  public PlatformConfig m_PlatformConfigMobile;
  public PlatformConfig m_PlatformConfigPc;

#if UNITY_EDITOR
  public PlatformConfig GetConfigForBuildTarget(BuildTarget target) {
    switch (target) {
    case BuildTarget.Android:
      return m_PlatformConfigMobile;
    case BuildTarget.StandaloneWindows:
    case BuildTarget.StandaloneWindows64:
    case BuildTarget.StandaloneLinux:
    case BuildTarget.StandaloneLinux64:
    case BuildTarget.StandaloneLinuxUniversal:
    case BuildTarget.StandaloneOSX:
      return m_PlatformConfigPc;
    default:
      throw new ArgumentException("target");
    }
  }
#endif
}

} // namespace TiltBrush
