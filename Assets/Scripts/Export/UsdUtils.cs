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

public static class UsdUtils {
  private static bool m_Initialized = false;

  public static bool InitializeUsd() {
#if USD_SUPPORTED
    if (m_Initialized) {
      return true;
    }
    try {
      // Initializes native USD plugins and ensures plugins are discoverable on the system path.
      SetupUsdPath();

      // Type registration enables automatic conversion from Unity-native types to USD types (e.g.
      // Vector3[] -> VtVec3fArray).
      USD.NET.Unity.UnityTypeBindings.RegisterTypes();

      // The DiagnosticHandler propagates USD native errors, warnings and info up to C# exceptions
      // and Debug.Log[Warning] respectively.
      USD.NET.Unity.DiagnosticHandler.Register();
    } catch (Exception ex) {
      // Make sure failed USD initialization doesn't torpedo all of Tilt Brush.
      Debug.LogException(ex);
      return false;
    }
    m_Initialized = true;
    return true;
#else
    m_Initialized = false;
    return m_Initialized;
#endif
  }

#if USD_SUPPORTED
  // USD has several auxillary C++ plugin discovery files which must be discoverable at run-time.
  // We store those libs in Support/ThirdParty/Usd and then call a pxr API to let USD's libPlug
  // know where to find them.
  private static void SetupUsdPath() {
    var supPath = UnityEngine.Application.dataPath.Replace("\\", "/");

#if (UNITY_EDITOR)
    supPath += @"/ThirdParty/Usd/Plugins/x86_64/share/";
#else
    supPath += @"/Plugins/share/";
#endif

    Debug.LogFormat("Registering plugins: {0}", supPath);
    pxr.PlugRegistry.GetInstance().RegisterPlugins(supPath);
    SetupUsdSysPath();
  }

  /// Adds the USD plugin paths to the system path.
  private static void SetupUsdSysPath() {
    var pathVar = "PATH";
    var target = EnvironmentVariableTarget.Process;
    var pathvar = System.Environment.GetEnvironmentVariable(pathVar, target);
    var supPath = UnityEngine.Application.dataPath + @"\Plugins;";
    supPath += UnityEngine.Application.dataPath + @"\ThirdParty\Usd\Plugins\x86_64;";
    var value = pathvar + @";" + supPath;
    Debug.LogFormat("Adding to sys path: {0}", supPath);
    System.Environment.SetEnvironmentVariable(pathVar, value, target);
  }
#endif
}
