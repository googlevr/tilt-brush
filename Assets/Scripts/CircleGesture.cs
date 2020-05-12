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

public class CircleGesture {
  private Vector3 m_InitPosition;
  private float m_MinCircleSize;
  private float m_BeginGestureDist;
  private float m_CloseLoopDist;
  private float m_StepDist;
  private float m_MaxAngle;

  private Vector3 m_LastCurrentPosition;
  private Vector3 m_WorkingPosition;
  private bool m_CircleStarted;
  private bool m_CircleSizeValid;
  private bool m_CircleAnglesValid;
  private Vector3 m_LastStepNormalized;

  public void InitGesture(Vector3 initPos, float minSize,
      float beginDist, float closeLoopDist, float step, float maxAngle) {
    m_InitPosition = initPos;
    m_MinCircleSize = minSize;
    m_BeginGestureDist = beginDist;
    m_CloseLoopDist = closeLoopDist;
    m_StepDist = step;
    m_MaxAngle = maxAngle;

    ResetGesture();
  }

  public void ResetGesture() {
    m_WorkingPosition = m_InitPosition;
    m_LastCurrentPosition = m_WorkingPosition;
    m_CircleStarted = false;
    m_CircleSizeValid = false;
    m_CircleAnglesValid = true;
    m_LastStepNormalized = Vector3.zero;
  }

  public void UpdateGesture(Vector3 currentPos) {
    // Validate sizes, if we haven't already.
    if (!m_CircleSizeValid || !m_CircleStarted) {
      Vector3 fromInitialPos = currentPos - m_InitPosition;
      if (fromInitialPos.magnitude > m_BeginGestureDist) {
        m_CircleStarted = true;
      }
      if (fromInitialPos.magnitude > m_MinCircleSize) {
        m_CircleSizeValid = true;
      }
    }

    // Check if we've gone far enough to consider this part of the gesture.
    Vector3 fromWorkingPos = currentPos - m_WorkingPosition;
    if (m_CircleStarted && fromWorkingPos.magnitude > m_StepDist) {
      m_WorkingPosition = currentPos;

      // Validate this angle, compared to the last.
      fromWorkingPos.Normalize();
      if (m_CircleAnglesValid && m_LastStepNormalized != Vector3.zero) {
        m_CircleAnglesValid = Vector3.Angle(m_LastStepNormalized, fromWorkingPos) < m_MaxAngle;
      }
      m_LastStepNormalized = fromWorkingPos;
    }

    m_LastCurrentPosition = currentPos;
  }

  public bool IsGestureComplete() {
    Vector3 initToLastCurrent = m_LastCurrentPosition - m_InitPosition;
    return m_CircleStarted && initToLastCurrent.magnitude < m_CloseLoopDist;
  }

  public bool DidGestureSucceed() {
    return m_CircleAnglesValid && m_CircleSizeValid;
  }
}

} // namespace TiltBrush