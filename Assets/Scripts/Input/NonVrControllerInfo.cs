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

namespace TiltBrush{

public class NonVrControllerInfo : ControllerInfo {
  public override bool IsTrackedObjectValid { get; set; }

  public NonVrControllerInfo(BaseControllerBehavior behavior) : base(behavior) { }

  public override float GetTriggerRatio() {
    return 0;
  }

  public override Vector2 GetPadValue() {
    return Vector2.zero;
  }

  public override Vector2 GetThumbStickValue() {
    return Vector2.zero;
  }

  public override Vector2 GetPadValueDelta() {
    return Vector2.zero;
  }

  public override float GetGripValue() {
    return 0.0f;
  }

  public override float GetTriggerValue() {
    return 0.0f;
  }

  public override float GetScrollXDelta() {
    return 0.0f;
  }

  public override float GetScrollYDelta() {
    return 0.0f;
  }

  // this is a specific function for oculus so always returning false
  public override bool GetVrInputTouch(VrInput button) {
    return false;
  }

  public override bool GetVrInput(VrInput input) {
    return false;
  }

  public override bool GetVrInputDown(VrInput input) {
    return false;
  }

  public override void TriggerControllerHaptics(float seconds) { }
}

}  // namespace TiltBrush
