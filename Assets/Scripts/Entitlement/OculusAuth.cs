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

public class OculusAuth : MonoBehaviour {
  #if OCULUS_SUPPORTED
  private bool m_authenticated = false;
  // The App ID is a public identifier for the Tilt Brush app on the Oculus platform. It is
  // analogous to Apple's App ID, which shows up in URLs related to the app.
  private string m_TiltBrushOculusRiftAppId => App.Config.OculusSecrets?.ClientId;
  private string m_TiltBrushOculusMobileAppId => App.Config.OculusMobileSecrets?.ClientId;

  private void Awake() {
    string id = App.Config.IsMobileHardware
        ? m_TiltBrushOculusMobileAppId
        : m_TiltBrushOculusRiftAppId;
    if (string.IsNullOrEmpty(id)) {
      return;
    }
    Oculus.Platform.Core.AsyncInitialize(id);
    Oculus.Platform.Entitlements.IsUserEntitledToApplication().OnComplete(EntitlementCallback);
  }

  public void Update() {
    Oculus.Platform.Request.RunCallbacks();
  }

  private void EntitlementCallback(Oculus.Platform.Message msg)
  {
    string strMsg;
    if (msg.IsError) {
      if (msg.GetError() != null) {
        strMsg = msg.GetError().Message;
      } else {
        strMsg = "Authentication failed";
      }
    } else {
      m_authenticated = true;
      strMsg = "";
    }

    if (strMsg != string.Empty) {
      Debug.Log(strMsg, this);
    }

    if (!m_authenticated) {
      Debug.Log("User not authenticated! You must be logged in to continue.");
      Application.Quit();
    }
  }
  #endif // OCULUS_SUPPORTED
}
} // namespace TiltBrush