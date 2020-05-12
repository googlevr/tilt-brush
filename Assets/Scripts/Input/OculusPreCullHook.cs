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

/// This component exists only to be attached to the camera so that
/// OculusHandTrackerManager.OnPreCull() is called at the appropriate time
/// to update the controller poses.
public class OculusPreCullHook : MonoBehaviour {

  private void OnPreCull() {
    if (OculusHandTrackingManager.m_Instance) {
      OculusHandTrackingManager.m_Instance.OnPreCull();
    }
  }
}

}  // namespace TiltBrush
