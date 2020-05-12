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

namespace TiltBrush {

public class CommonValueSlider : BaseSlider {
  const float kCameraSmoothingPow = 0.2f;

  public enum ValueType {
    CameraFov,
    CameraSmoothing,
  }
  [SerializeField] private ValueType m_ValueType;

  override protected void Awake() {
    base.Awake();

    switch (m_ValueType) {
    case ValueType.CameraFov:
      m_CurrentValue = CameraConfig.Fov01;
      break;
    case ValueType.CameraSmoothing:
      m_CurrentValue = CameraConfig.Smoothing01;
      break;
    }
    SetSliderPositionToReflectValue();
    UpdateDescription();
  }

  override public void GazeRatioChanged(float gazeRatio) {
    if (m_ValueType == ValueType.CameraSmoothing) {
      SetAvailable(SketchControlsScript.m_Instance.MultiCamCaptureRig.m_ActiveStyle ==
          MultiCamStyle.Video);
    }
  }

  override public void UpdateValue(float value) {
    value = SanitizeValue(value);

    base.UpdateValue(value);
    SetSliderPositionToReflectValue();

    switch (m_ValueType) {
    case ValueType.CameraFov:
      CameraConfig.Fov01 = value;
      break;
    case ValueType.CameraSmoothing:
      CameraConfig.Smoothing01 = Mathf.Pow(value, kCameraSmoothingPow);
      break;
    }
    UpdateDescription();
  }

  void UpdateDescription() {
    switch (m_ValueType) {
    case ValueType.CameraFov:
      SetDescriptionText(m_DescriptionText, Mathf.RoundToInt(CameraConfig.Fov).ToString());
      break;
    case ValueType.CameraSmoothing: {
        string desc = null;
        double showValue = Math.Round((double)m_CurrentValue, 1);
        if (showValue <= 0.0f) {
          desc = "Off";
        } else if (showValue >= 1.0f) {
          desc = "Max";
        } else {
          desc = showValue.ToString();
        }
        SetDescriptionText(m_DescriptionText, desc);
      }
      break;
    }
  }

  float SanitizeValue(float target) {
    switch (m_ValueType) {
    case ValueType.CameraSmoothing:
      if (target <= 0.05f) {
        return 0.0f;
      } else if (target >= 0.95f) {
        return 1.0f;
      } break;
    }
    return target;
  }
}
}  // namespace TiltBrush
