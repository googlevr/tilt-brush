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

namespace TiltBrush {
public class ControllerBehaviorBrush : BaseControllerBehavior {
  public void EnablePointAtPanelsHintObject(bool bEnable) {
    ControllerGeometry.PointAtPanelsHintObject.Activate(bEnable);
    if (bEnable) {
      AudioManager.m_Instance.PlayHintAnimateSound(
          ControllerGeometry.PointAtPanelsHintObject.transform.position);
    }
  }

  override public void ActivateHint(bool bActivate) {
    ControllerGeometry.PaintHintObject.Activate(bActivate);
  }
}
}  // namespace TiltBrush
