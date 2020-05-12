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

public class PointerAngleSlider : BaseSlider {
  override protected void Awake() {
    base.Awake();
    m_CurrentValue = PointerManager.m_Instance.FreePaintPointerAngle / 90.0f;
    SetSliderPositionToReflectValue();
  }

  override public void UpdateValue(float fValue) {
    PointerManager.m_Instance.FreePaintPointerAngle = fValue * 90.0f;
  }

  public override void ResetState() {
    base.ResetState();
    SetAvailable(!App.VrSdk.VrControls.LogitechPenIsPresent());
  }
}
}  // namespace TiltBrush
