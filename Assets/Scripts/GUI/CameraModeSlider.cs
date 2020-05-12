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

public class CameraModeSlider : BaseSlider {
  private readonly int kCameraModeCount = System.Enum.GetNames(typeof(DropCamWidget.Mode)).Length;
  private DropCamWidget m_DropCam;

  override protected void Awake() {
    base.Awake();

    m_DropCam = SketchControlsScript.m_Instance.GetDropCampWidget();
    Debug.Assert(m_DropCam);
    m_CurrentValue = (float)m_DropCam.GetMode() / (kCameraModeCount - 1);
    SetSliderPositionToReflectValue();

    // Make sure slider description is initialized properly.
    int iValue = (int)(m_CurrentValue * (kCameraModeCount - 1) + 0.5f);
    SetDescriptionText(m_DescriptionText, DropCamWidget.GetModeName((DropCamWidget.Mode)iValue));
  }

  override public void UpdateValue(float fValue) {
    int iValue = (int)(fValue * (kCameraModeCount - 1) + 0.5f);
    m_DropCam.SetMode((DropCamWidget.Mode)iValue);

    // Label the nob.
    SetDescriptionText(m_DescriptionText, DropCamWidget.GetModeName((DropCamWidget.Mode)iValue));

    // Reposition the nob appropriately.
    Vector3 vLocalPos = m_Nob.transform.localPosition;
    vLocalPos.x = Mathf.Clamp((float)iValue / (kCameraModeCount - 1) - 0.5f, -0.5f, 0.5f) * m_MeshScale.x;
    m_Nob.transform.localPosition = vLocalPos;
  }
}
}  // namespace TiltBrush
