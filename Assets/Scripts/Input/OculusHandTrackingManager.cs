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

#if !OCULUS_SUPPORTED
using OVRInput_Controller = System.Int32;
#else // !OCULUS_SUPPORTED
  using OVRInput_Controller = OVRInput.Controller;
#endif // OCULUS_SUPPORTED

namespace TiltBrush {

/// <summary>
/// Oculus does not have the equivalent of SteamVR_TrackedObject. Instead, the poses must be updated
/// manually on every frame.
/// </summary>
public class OculusHandTrackingManager : MonoBehaviour {
  static public OculusHandTrackingManager m_Instance;
  static public event Action NewPosesApplied;

  public void SwapLeftRight() {
    // Swap which controller type (LTouch or RTouch) is used by which controller info.
    OculusControllerInfo wandInfo = InputManager.Wand as OculusControllerInfo;
    OculusControllerInfo brushInfo = InputManager.Brush as OculusControllerInfo;
    OVRInput_Controller tmpType = wandInfo.m_ControllerType;
    wandInfo.m_ControllerType = brushInfo.m_ControllerType;
    brushInfo.m_ControllerType = tmpType;
  }

  private void Awake() {
    m_Instance = this;
  }

  // This class tries to mimic the tracking done in SteamVR's SteamVR_TrackedObject, which updates
  // poses in response to the event "new_poses". This event is sent in
  // SteamVR_UpdatePoses.OnPreCull(). But OnPreCull() is only available to components attached to
  // the camera, which this class is not. So this public OnPreCull() is called exactly once
  // from OculusPreCullHook.OnPreCull() which is attached to the camera.
  public void OnPreCull() {
    // There are checks for each controller instead of a single check for
    // both so the player will know if either controller is having problems.
    foreach (ControllerInfo baseInfo in InputManager.Controllers) {
      OculusControllerInfo info = baseInfo as OculusControllerInfo;
      if (info == null) {
        continue;  // should never happen
      }

      info.UpdatePosesAndValidity();
    }

    if (NewPosesApplied != null) {
      NewPosesApplied();
    }
  }
}

}  // namespace TiltBrush
