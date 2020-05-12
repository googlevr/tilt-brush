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
using System.Collections.Generic;

namespace TiltBrush {

  public class HintArrowLineScript : MonoBehaviour {
    [SerializeField] private GameObject m_ArrowPrefab;
    [SerializeField] private float m_ArrowSpacing = 0.5f;
    [SerializeField] private float m_MovementSpeed = 2.0f;
    [SerializeField] private float m_ScaleDistance = 0.5f;
    [SerializeField] private float m_DisappearDistance = 0f;
    // Global mode is when the arrow line points at a panel globally,
    // rather than at a precise point
    [SerializeField] private float m_GlobalModeScaleDistance = 1.2f;
    [SerializeField] private float m_GlobalModeDisappearDistance = 0.8f;

    [SerializeField] private bool m_AutoUpdate;
    [SerializeField] private Vector3 m_AutoPositionOffset;
    [SerializeField] private Vector3 m_AutoRotationOrigin;
    [SerializeField] private Vector3 m_AutoRotationTarget;


    private bool m_GlobalMode;
    public bool GlobalMode{ get { return m_GlobalMode; } set { m_GlobalMode = value; } }
    private float DisappearDistance {
      get {
        return m_GlobalMode ? m_GlobalModeDisappearDistance : m_DisappearDistance;
      }
    }
    private float ScaleDistance {
      get {
        return m_GlobalMode ? m_GlobalModeScaleDistance : m_ScaleDistance;
      }
    }

  private class Arrow {
    public float m_DistanceToTarget;
    public GameObject m_Arrow;
    public Vector3 m_BaseScale;

    public void SetScale(float scale) {
      m_Arrow.transform.localScale = m_BaseScale * scale;
    }
  }
  private List<Arrow> m_Arrows;

  void Awake() {
    m_Arrows = new List<Arrow>();
  }

  void Update() {
    if (m_AutoUpdate) {
      TrTransform target = TrTransform.FromTransform(transform);

      Vector3 vTransformedOffset = transform.TransformPoint(m_AutoPositionOffset);
      TrTransform origin = new TrTransform();
      origin.translation = vTransformedOffset;

      origin.rotation = transform.rotation * Quaternion.Euler(m_AutoRotationOrigin);
      target.rotation = transform.rotation * Quaternion.Euler(m_AutoRotationTarget);

      UpdateLine(origin, target);
    }
  }

  public void UpdateLine(TrTransform origin, TrTransform target) {
    Vector3 vLine = target.translation - origin.translation;
    float fLineDistance = vLine.magnitude;

    // Move all arrows down the line.
    float fMovementStep = m_MovementSpeed * Time.deltaTime;
    float fFarthestArrow = 0.0f;
    for (int i = m_Arrows.Count - 1; i >= 0; --i) {
      m_Arrows[i].m_DistanceToTarget -= fMovementStep;
      float fDistToTarget = m_Arrows[i].m_DistanceToTarget;
      fFarthestArrow = Mathf.Max(fFarthestArrow, fDistToTarget);

      // Dismiss any arrow beyond the ends of the line.
      if (fDistToTarget <= 0.0f || fDistToTarget > fLineDistance) {
        Destroy(m_Arrows[i].m_Arrow);
        m_Arrows.RemoveAt(i);
      } else {
        // Set new arrow position and rotation.
        float fLineT = fDistToTarget / fLineDistance;
        m_Arrows[i].m_Arrow.transform.position =
            Vector3.Lerp(target.translation, origin.translation, fLineT);
        m_Arrows[i].m_Arrow.transform.rotation =
            Quaternion.Slerp(target.rotation, origin.rotation, fLineT);

        // Update scale.
        if (fDistToTarget < ScaleDistance) {
          float fScale = (fDistToTarget - DisappearDistance) / (ScaleDistance - DisappearDistance);
          m_Arrows[i].SetScale(fScale >= 0f ? fScale : 0f);
        } else if (fDistToTarget > fLineDistance - m_ScaleDistance) {
          m_Arrows[i].SetScale((fLineDistance - fDistToTarget) / m_ScaleDistance);
        } else {
          m_Arrows[i].SetScale(1.0f);
        }
      }
    }

    // Spawn a new arrows if there's room in our line.
    if (fFarthestArrow < fLineDistance - m_ArrowSpacing) {
      Arrow newArrow = new Arrow();
      newArrow.m_Arrow = (GameObject)Instantiate(m_ArrowPrefab);
      newArrow.m_BaseScale = newArrow.m_Arrow.transform.localScale;
      newArrow.m_Arrow.transform.parent = transform;
      newArrow.m_DistanceToTarget = fFarthestArrow + m_ArrowSpacing;
      newArrow.SetScale(0.0f);
      m_Arrows.Add(newArrow);

      fFarthestArrow += m_ArrowSpacing;
    }
  }

  public void Reset() {
    for (int i = 0; i < m_Arrows.Count; ++i) {
      Destroy(m_Arrows[i].m_Arrow);
    }
    m_Arrows.Clear();
    GlobalMode = false;
  }

  void OnDrawGizmosSelected() {
    if (m_AutoUpdate) {
      Gizmos.color = Color.blue;

      // Draw start and end points.
      TrTransform target = TrTransform.FromTransform(transform);
      Vector3 vTransformedOffset = transform.TransformPoint(m_AutoPositionOffset);
      TrTransform origin = new TrTransform();
      origin.translation = vTransformedOffset;
      origin.rotation = transform.rotation;
      Gizmos.DrawLine(origin.translation, target.translation);

      // Draw normal at start.
      Gizmos.color = Color.green;
      Quaternion qStartRot = transform.rotation * Quaternion.Euler(m_AutoRotationOrigin);
      Vector3 vStartForward = qStartRot * Vector3.forward;
      Gizmos.DrawLine(origin.translation, origin.translation + (vStartForward * 0.25f));

      // Draw normal at end.
      Gizmos.color = Color.red;
      Quaternion qEndRot = transform.rotation * Quaternion.Euler(m_AutoRotationTarget);
      Vector3 vEndForward = qEndRot * Vector3.forward;
      Gizmos.DrawLine(target.translation, target.translation + (vEndForward * 0.25f));
    }
  }
}

} // namespace TiltBrush
