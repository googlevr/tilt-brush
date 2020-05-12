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
using UnityEngine.Serialization;

namespace TiltBrush {

  public class TutorialTapScript : MonoBehaviour {
    [FormerlySerializedAs("m_ControllerWand")]
    [SerializeField] private Transform m_GeometryWandPanel;
    [FormerlySerializedAs("m_ControllerBrush")]
    [SerializeField] private Transform m_GeometryBrushStroke;
    [SerializeField] private Transform m_GeometryViveLeft;
    [SerializeField] private Transform m_GeometryViveRight;
    [FormerlySerializedAs("m_ControllerRiftWand")]
    [SerializeField] private Transform m_GeometryRiftLeft;
    [FormerlySerializedAs("m_ControllerRiftBrush")]
    [SerializeField] private Transform m_GeometryRiftRight;
    [SerializeField] private Transform m_GeometryWmrLeft;
    [SerializeField] private Transform m_GeometryWmrRight;
    [SerializeField] private Transform m_GeometryQuestLeft;
    [SerializeField] private Transform m_GeometryQuestRight;
    [SerializeField] private Transform m_GeometryKnucklesLeft;
    [SerializeField] private Transform m_GeometryKnucklesRight;

    [SerializeField] private float m_AnimationSpeedClick = 2.0f;
    [SerializeField] private float m_AnimationSpeedRepel = 1.0f;
    [SerializeField] private float m_ClickTime = 1.0f;
    [SerializeField] private float m_MovementScale = 1.0f;

    private Transform m_ControllerGeometryLeft;
    private Transform m_ControllerGeometryRight;
    private float m_AnimationValue;
    private Vector3 m_ControllerBasePosA;
    private Vector3 m_ControllerBasePosB;
    private bool m_Flipped;
    private float m_RepelTime;

    void Start() {
      m_AnimationValue = 0.0f;
      m_ControllerBasePosA = m_GeometryWandPanel.localPosition;
      m_ControllerBasePosB = m_GeometryBrushStroke.localPosition;
      m_Flipped = false;
      m_RepelTime = m_ClickTime * 2.0f;

      // Ignore the Logitech Pen.
      ControllerStyle style = App.VrSdk.VrControls.BaseControllerStyle;

      switch (style) {
      case ControllerStyle.OculusTouch:
        if (App.Config.VrHardware == VrHardware.Rift) {
          m_ControllerGeometryLeft = m_GeometryRiftLeft;
          m_ControllerGeometryRight = m_GeometryRiftRight;
        } else if (App.Config.VrHardware == VrHardware.Quest) {
          // TODO(b/135950527): rift-s also uses quest controllers.
          m_ControllerGeometryLeft = m_GeometryQuestLeft;
          m_ControllerGeometryRight = m_GeometryQuestRight;
        }
        break;
      case ControllerStyle.Wmr:
        m_ControllerGeometryLeft = m_GeometryWmrLeft;
        m_ControllerGeometryRight = m_GeometryWmrRight;
        break;
      case ControllerStyle.Knuckles:
        m_ControllerGeometryLeft = m_GeometryKnucklesLeft;
        m_ControllerGeometryRight = m_GeometryKnucklesRight;
        break;
      case ControllerStyle.Vive:
      default:
        m_ControllerGeometryLeft = m_GeometryViveLeft;
        m_ControllerGeometryRight = m_GeometryViveRight;
        break;
      }
    }

    void Update() {
      // Animate at a speed according to state.
      float fPrevValue = m_AnimationValue;
      if (m_AnimationValue < m_ClickTime) {
        m_AnimationValue += Time.deltaTime * m_AnimationSpeedClick;
      } else {
        m_AnimationValue += Time.deltaTime * m_AnimationSpeedRepel;
      }

      // Flip controllers if we just hit our click time.
      if (fPrevValue < m_ClickTime && m_AnimationValue >= m_ClickTime) {
        m_Flipped ^= true;

        Vector3 vTempEulers = m_GeometryBrushStroke.localEulerAngles;
        m_GeometryBrushStroke.localRotation = Quaternion.Euler(m_GeometryWandPanel.localEulerAngles);
        m_GeometryWandPanel.localRotation = Quaternion.Euler(vTempEulers);
      }
      if (m_AnimationValue > m_RepelTime) {
        m_AnimationValue = 0.0f;
      }

      // Position according to time.
      if (m_AnimationValue <= m_ClickTime) {
        float fMovementDist = (m_ClickTime - m_AnimationValue) * m_MovementScale;
        Vector3 vWandLocalPos = m_ControllerBasePosA;
        vWandLocalPos.x -= fMovementDist;
        Vector3 vBrushLocalPos = m_ControllerBasePosB;
        vBrushLocalPos.x += fMovementDist;

        if (!m_Flipped) {
          m_GeometryWandPanel.localPosition = vWandLocalPos;
          m_GeometryBrushStroke.localPosition = vBrushLocalPos;
        } else {
          m_GeometryWandPanel.localPosition = vBrushLocalPos;
          m_GeometryBrushStroke.localPosition = vWandLocalPos;
        }
        m_ControllerGeometryLeft.localPosition = vWandLocalPos;
        m_ControllerGeometryRight.localPosition = vBrushLocalPos;
      } else {
        float fMovementDist = (m_AnimationValue - m_ClickTime) * m_MovementScale;
        Vector3 vWandLocalPos = m_ControllerBasePosA;
        vWandLocalPos.x -= fMovementDist;
        Vector3 vBrushLocalPos = m_ControllerBasePosB;
        vBrushLocalPos.x += fMovementDist;

        if (!m_Flipped) {
          m_GeometryWandPanel.localPosition = vWandLocalPos;
          m_GeometryBrushStroke.localPosition = vBrushLocalPos;
        } else {
          m_GeometryWandPanel.localPosition = vBrushLocalPos;
          m_GeometryBrushStroke.localPosition = vWandLocalPos;
        }
        m_ControllerGeometryLeft.localPosition = vWandLocalPos;
        m_ControllerGeometryRight.localPosition = vBrushLocalPos;
      }
    }
  }

} // namespace TiltBrush
