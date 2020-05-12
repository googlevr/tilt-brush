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
using Valve.VR;

namespace TiltBrush {
  public class DetectLogitechVrPen : MonoBehaviour {
    [SerializeField] private ControllerGeometry m_LogiPenControllerGeometryPrefab;

    public bool IsPen {
      get { return m_IsPen; }
      private set {
        if (value != m_IsPen) {
          BaseControllerBehavior behavior = GetComponent<BaseControllerBehavior>();
          var prefab = value ? m_LogiPenControllerGeometryPrefab : null;
          behavior.InstantiateControllerGeometryFromPrefab(prefab);
          m_IsPen = value;
          if (m_IsPen) {
            Debug.AssertFormat(
                behavior.ControllerGeometry.Style == ControllerStyle.LogitechPen,
                "Try re-importing {0}", prefab);
          }
        }
      }
    }

    private bool m_TryToSwap;
    private bool m_IsPen;

    private void LateUpdate() {
      if (m_TryToSwap) {
        var brushPenDetector = InputManager.Brush.Behavior.GetComponent<DetectLogitechVrPen>();
        if (brushPenDetector != null && !brushPenDetector.IsPen) {
          InputManager.m_Instance.WandOnRight = !InputManager.m_Instance.WandOnRight;
        }
        m_TryToSwap = false;
      }
    }

    public void Initialize(int deviceIndex) {
      if (IsLogitechPen((uint)deviceIndex)) {
        IsPen = true;

        if (GetComponent<BaseControllerBehavior>().ControllerName ==
            InputManager.ControllerName.Wand) {
          // This controller is the LogiTech VR Pen but it's also set to be the wand. Try to swap
          // the controllers in the late update, after all the indices have been set.
          m_TryToSwap = true;
        }
      } else {
        IsPen = false;
      }
    }

    public static bool IsLogitechPen(uint deviceIndex) {
      ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
      uint bufferCapacity = OpenVR.System.GetStringTrackedDeviceProperty(
          deviceIndex,
          ETrackedDeviceProperty.Prop_ControllerType_String,
          null,
          0,
          ref error);
      System.Text.StringBuilder buffer = new System.Text.StringBuilder((int)bufferCapacity);
      OpenVR.System.GetStringTrackedDeviceProperty(
          deviceIndex,
          ETrackedDeviceProperty.Prop_ControllerType_String,
          buffer,
          bufferCapacity,
          ref error);
      string s = buffer.ToString();
      if (error == ETrackedPropertyError.TrackedProp_Success) {
        if (s.StartsWith("logitech_stylus")) {
          return true;
        }
      }
      return false;
    }
  }
} // namespace TiltBrush
