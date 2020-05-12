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

#if UNITY_ANDROID
static class AndroidUtils {
  public static AndroidJavaObject GetContext() {
    if (Application.platform != RuntimePlatform.Android) {
      return null;
    }

    // Unity doesn't document UnityPlayer.currentActivity (or UnityPlayer at all), but it
    // does mention it in passing on these two pages:
    //   https://docs.unity3d.com/2017.3/Documentation/ScriptReference/AndroidJavaRunnable.html
    //   https://docs.unity3d.com/2017.3/Documentation/ScriptReference/AndroidJavaProxy.html
    AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
    AndroidJavaObject context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
    return context;
  }

  /// Returns versionCode from AndroidManifest.xml.
  public static int GetVersionCode() {
    if (Application.platform != RuntimePlatform.Android) {
      return 13;  // just some placeholder
    }

    var context = GetContext();
    string packageName = context.Call<string>("getPackageName");
    var packageMgr = context.Call<AndroidJavaObject>("getPackageManager");
    var packageInfo = packageMgr.Call<AndroidJavaObject>("getPackageInfo", packageName, 0);
    return packageInfo.Get<int>("versionCode");
  }
  
  /// Returns versionName from AndroidManifest.xml.
  ///
  /// Unity fills in Manifest.versionName from PlayerSettings.bundleVersion.
  /// BuildTiltBrush fills in PlayerSettings.bundleVersion from Config.m_VersionNumber
  /// and m_BuildStamp.
  /// This should therefore have the same info as you'd find in Config.
  /// Sample return values: "19.0b-(menuitem)", "18.3-d8239842"
  public static string GetVersionName() {
    if (Application.platform != RuntimePlatform.Android) {
      return "versionNamePlaceholder";
    }

    var context = GetContext();
    string packageName = context.Call<string>("getPackageName");
    var packageMgr = context.Call<AndroidJavaObject>("getPackageManager");
    var packageInfo = packageMgr.Call<AndroidJavaObject>("getPackageInfo", packageName, 0);
    return packageInfo.Get<string>("versionName");
  }

  /// Returns package name.
  public static string GetPackageName() {
    if (Application.platform != RuntimePlatform.Android) {
      return "com.placeholder.packagename";
    }

    var context = GetContext();
    return context.Call<string>("getPackageName");
  }
}
#endif
