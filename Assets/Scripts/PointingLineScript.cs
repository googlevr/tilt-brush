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

public class PointingLineScript : MonoBehaviour {
  public int m_NumLinePositions = 2;
  private int m_LinePositionMidIndex;
  private LineRenderer m_LineRenderer;

  private float m_PreviousLineLength;
  private Vector3 m_PrevForward;
  private bool m_HadFocus;

  public float m_BendMovementScalar = 1.0f;
  private Vector3 m_BendNormal;
  private float m_BendVelocity;
  private float m_BendValue;
  public float m_BendInitialPop;
  public float m_BendMaxVelocity;
  public float m_BendValueDampen = 1.0f;
  public float m_BendValueK = 1.0f;

  void Awake() {
    m_NumLinePositions = Mathf.Max(m_NumLinePositions, 2);
    m_LineRenderer = GetComponent<LineRenderer>();
    m_LineRenderer.positionCount = m_NumLinePositions;
    m_LinePositionMidIndex = m_NumLinePositions / 2;
    m_PreviousLineLength = 0.0f;
    m_PrevForward = transform.forward;
    m_HadFocus = true;
  }

  void Update() {
    bool bGazeObjectHasFocus = SketchControlsScript.m_Instance.IsUserInteractingWithUI();
    if (bGazeObjectHasFocus != m_HadFocus) {
      if (bGazeObjectHasFocus) {
        m_BendVelocity = m_BendInitialPop;
        m_BendValue = 0.0f;
      } else {
        m_BendVelocity = 0.0f;
        m_BendValue = 0.0f;
      }

      m_HadFocus = bGazeObjectHasFocus;
    }

    m_LineRenderer.enabled = bGazeObjectHasFocus;
    if (bGazeObjectHasFocus) {
      Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
      Vector3 vStartPos = rAttachPoint.position;
      Vector3 vEndPos = SketchControlsScript.m_Instance.GetUIReticlePos();
      Vector3 vLine = vEndPos - vStartPos;

      //if we're being deactivated, pull the pointing line in
      float fActivationRatio = SketchControlsScript.m_Instance.GetGazePanelActivationRatio();
      if (fActivationRatio < 1.0f) {
        vEndPos = vStartPos + (m_PrevForward * vLine.magnitude * fActivationRatio);
        vLine = vEndPos - vStartPos;
      } else {
        m_PrevForward = vLine.normalized;
      }

      if (m_NumLinePositions > 2) {
        float fLineLength = vLine.magnitude;
        float fIntervalDistance = fLineLength / (m_NumLinePositions - 1);
        Vector3 vLineStep = vLine.normalized * fIntervalDistance;

        //add to the bend according to how much we've shrunk
        float fLengthDelta = fLineLength - m_PreviousLineLength;
        if (fLengthDelta < 0.0f) {
          m_BendVelocity += Mathf.Abs(fLengthDelta) * m_BendMovementScalar * Time.deltaTime;
          m_BendVelocity = Mathf.Clamp(m_BendVelocity, -m_BendMaxVelocity, m_BendMaxVelocity);
        }

        //set bend normal as cross between line and projected right
        Transform rGazeObjectTransform = SketchControlsScript.m_Instance.GazeObjectTransform();
        if (rGazeObjectTransform != null) {
          Vector3 vLineProjectedOnToPlane = Vector3.ProjectOnPlane(vLine, rGazeObjectTransform.forward);
          Vector3 vPlaneRight = Vector3.Cross(rGazeObjectTransform.forward, vLineProjectedOnToPlane.normalized);
          Vector3 vNewNorm = Vector3.Cross(vLine.normalized, vPlaneRight);
          if (vNewNorm.sqrMagnitude > 0.000001f) {
            m_BendNormal = vNewNorm;
          }
        }

        //set all the positions in the middle to bow values
        for (int i = 0; i < m_NumLinePositions; ++i) {
          Vector3 vControlPointPos = vStartPos;
          vControlPointPos += (vLineStep * (float)i);

          //set bow amount according to distance from center
          int iDistFromMid = Mathf.Abs(i - m_LinePositionMidIndex);
          iDistFromMid = Mathf.Min(iDistFromMid, m_LinePositionMidIndex);
          float fPercentToMid = 1.0f - ((float)iDistFromMid / (float)m_LinePositionMidIndex);
          float fBendAmount = m_BendValue * Mathf.Sin(fPercentToMid * Mathf.PI * 0.5f);
          vControlPointPos += m_BendNormal * fBendAmount;

          m_LineRenderer.SetPosition(i, vControlPointPos);
        }

        //bend value spring
        float fToBendHome = -m_BendValue;
        fToBendHome *= m_BendValueK;
        float fDampenedVel = m_BendVelocity * m_BendValueDampen;
        float fSpringForce = fToBendHome - fDampenedVel;
        m_BendVelocity += fSpringForce;
        m_BendValue += (m_BendVelocity * Time.deltaTime);

        //backup previous length for delta next frame
        m_PreviousLineLength = fLineLength;
      } else {
        m_LineRenderer.SetPosition(0, vStartPos);
        m_LineRenderer.SetPosition(m_NumLinePositions - 1, vEndPos);
      }
    }
  }
}
}  // namespace TiltBrush
