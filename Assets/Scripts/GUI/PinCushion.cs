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
public class PinCushion : MonoBehaviour {
  public enum VisualState {
    Hidden,
    HiddenToShowing,
    Showing,
    ShowingToHidden,
  }

  [SerializeField] private float m_TransitionSpeed;
  [SerializeField] private float m_ShowDelay;
  [SerializeField] private Transform m_RootObject;
  [SerializeField] private Transform m_SelectionObject;
  [SerializeField] private PinCushionItem m_DefaultItem;
  [SerializeField] private SphereCollider m_DefaultCollider;
  [SerializeField] private float m_TimerAudioThreshold = .2f;

  private PinCushionItem[] m_Items;

  private Vector3 m_BaseSelectionObjectOffset;
  private Vector3 m_SelectionOffset;
  private Quaternion m_ControllerBaseRotation;
  private int m_HighlightedItem;
  private VisualState m_CurrentVisualState;
  private float m_VisualScaleTimer;

  public bool IsShowing() { return m_CurrentVisualState != VisualState.Hidden; }

  void Start() {
    m_Items = GetComponentsInChildren<PinCushionItem>();
    m_BaseSelectionObjectOffset = m_SelectionObject.position - m_RootObject.position;
    m_VisualScaleTimer = -m_ShowDelay * m_TransitionSpeed;
    RefreshVisualsForAnimations();
  }

  public void ShowPinCushion(bool show) {
    if (show) {
      if (m_CurrentVisualState == VisualState.Hidden ||
          m_CurrentVisualState == VisualState.ShowingToHidden) {
        // If we're currently hidden, update our transform.
        if (m_CurrentVisualState == VisualState.Hidden) {
          Transform pinCushionXf = InputManager.m_Instance.GetPinCushionSpawn();
          if (pinCushionXf != null) {
            m_RootObject.position = pinCushionXf.position;
            m_RootObject.rotation = pinCushionXf.rotation;

            m_DefaultCollider.transform.position = pinCushionXf.position;
            m_DefaultCollider.transform.rotation = pinCushionXf.rotation;

            // Reset selection.
            m_SelectionObject.position = m_RootObject.position + m_BaseSelectionObjectOffset;
            m_SelectionOffset = m_SelectionObject.position -
                InputManager.m_Instance.GetBrushControllerAttachPoint().position;
            m_ControllerBaseRotation = InputManager.m_Instance.GetControllerRotation(
                InputManager.ControllerName.Brush);

            AudioManager.m_Instance.PlayPinCushionSound(show);
          }
        }

        // Transition.
        m_CurrentVisualState = VisualState.HiddenToShowing;
      }
    } else {
      if (m_CurrentVisualState == VisualState.Showing ||
          m_CurrentVisualState == VisualState.HiddenToShowing) {
        // Transition.
        m_CurrentVisualState = VisualState.ShowingToHidden;

        // Play Close sound only if button has been pressed long enough
        if (m_VisualScaleTimer >= m_TimerAudioThreshold) {
          AudioManager.m_Instance.PlayPinCushionSound(show);
        }
      }
    }
  }

  void RefreshVisualsForAnimations() {
    float clampedTimer = Mathf.Clamp01(m_VisualScaleTimer);
    Vector3 scale = m_RootObject.localScale;
    scale.Set(clampedTimer, clampedTimer, clampedTimer);
    m_RootObject.localScale = scale;
    m_RootObject.gameObject.SetActive(m_VisualScaleTimer > 0.0f);
    m_SelectionObject.localScale = scale;
    m_SelectionObject.gameObject.SetActive(m_VisualScaleTimer > 0.0f);
  }

  void Update() {
    // Update animations for mode changes.
    switch (m_CurrentVisualState) {
    case VisualState.Hidden: break;
    case VisualState.Showing:
      UpdateSelection();
      UpdateHighlightedItem();
      break;
    case VisualState.HiddenToShowing:
      m_VisualScaleTimer += (m_TransitionSpeed * Time.deltaTime);
      RefreshVisualsForAnimations();
      UpdateSelection();
      UpdateHighlightedItem();

      if (m_VisualScaleTimer >= 1.0f) {
        m_VisualScaleTimer = 1.0f;
        m_CurrentVisualState = VisualState.Showing;
      }
      break;
    case VisualState.ShowingToHidden:
      m_VisualScaleTimer -= (m_TransitionSpeed * Time.deltaTime);
      RefreshVisualsForAnimations();
      UpdateHighlightedItem();

      if (m_VisualScaleTimer <= 0.0f) {
        // Delay show timer.
        m_VisualScaleTimer = -m_ShowDelay * m_TransitionSpeed;

        // Set selection.
        if (m_HighlightedItem == -1) {
          SketchSurfacePanel.m_Instance.EnableDefaultTool();
        } else {
          SketchSurfacePanel.m_Instance.EnableSpecificTool(m_Items[m_HighlightedItem].Tool);
        }
        m_HighlightedItem = -1;
        m_CurrentVisualState = VisualState.Hidden;
      }
      break;
    }
  }

  void UpdateSelection() {
    int prevItem = m_HighlightedItem;
    m_HighlightedItem = -1;

    Quaternion controllerRot = InputManager.m_Instance.GetControllerRotation(
        InputManager.ControllerName.Brush);
    Quaternion rotDiff = controllerRot * Quaternion.Inverse(m_ControllerBaseRotation);
    Vector3 transformedOffset = rotDiff * m_SelectionOffset;
    m_SelectionObject.rotation = InputManager.m_Instance.GetControllerRotation(
        InputManager.ControllerName.Brush);

    // Project the selection position on to the plane of the pin cushion.
    Vector3 selectionPos =
        InputManager.m_Instance.GetBrushControllerAttachPoint().position +
        transformedOffset;
    Vector3 projectedPos = MathUtils.ProjectPosOnPlane(m_RootObject.forward, m_RootObject.position,
        selectionPos);
    m_SelectionObject.position = projectedPos;

    // See if this position is inside the default item.
    Vector3 localPos = m_DefaultCollider.transform.InverseTransformPoint(projectedPos);
    if (localPos.magnitude < m_DefaultCollider.radius) {
      TriggerFeedbackOnItemChange(prevItem);
      return;
    }

    // Figure out what angle this position is to the transform's right.
    Vector3 xfToProjected = projectedPos - m_RootObject.position;
    xfToProjected.Normalize();
    float angle = Vector3.Angle(m_RootObject.right, xfToProjected);

    // Get cross product of projected offset and transform's right.
    Vector3 projectedCross = Vector3.Cross(m_RootObject.right, xfToProjected);

    // Check the cross product against the transform's forward.  If they're pointing in the
    // same direction, we're ok.  If they're pointing opposite, the winding of our angle
    // is backwards, so mirror it around the circle.
    if (Vector3.Angle(projectedCross, m_RootObject.forward) > 90.0f) {
      float diff180 = 180.0f - angle;
      angle = 180.0f + diff180;
    }

    // See what item we're gesturing toward.
    for (int i = 0; i < m_Items.Length; ++i) {
      if (m_Items[i].MinAngle >= 0.0f && m_Items[i].MaxAngle >= 0.0f) {
        // If we're out of range from the start, assume we're a period off and adjust.
        float adjAngle = angle;
        if (adjAngle < m_Items[i].MinAngle) {
          adjAngle += 360.0f;
        } else if (adjAngle > m_Items[i].MaxAngle) {
          adjAngle -= 360.0f;
        }
        if (adjAngle > m_Items[i].MinAngle && adjAngle <= m_Items[i].MaxAngle) {
          m_HighlightedItem = i;
          break;
        }
      }
    }

    TriggerFeedbackOnItemChange(prevItem);
  }

  void UpdateHighlightedItem() {
    for (int i = 0; i < m_Items.Length; ++i) {
      m_Items[i].Highlight(i == m_HighlightedItem);
    }
    if (m_HighlightedItem == -1) {
      m_DefaultItem.Highlight(true);
    }
  }

  void TriggerFeedbackOnItemChange(int prevItem) {
    if (prevItem != m_HighlightedItem) {
      InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.05f);
      AudioManager.m_Instance.PlayPinCushionHoverSound();
    }
  }

  void OnDrawGizmosSelected() {
    if (m_RootObject) {
      Gizmos.color = Color.green;
      PinCushionItem[] aItems = GetComponentsInChildren<PinCushionItem>();
      if (aItems != null) {
        for (int i = 0; i < aItems.Length; ++i) {
          if (aItems[i].MinAngle >= 0.0f && aItems[i].MaxAngle >= 0.0f) {
            Quaternion qMin = Quaternion.AngleAxis(aItems[i].MinAngle, m_RootObject.forward);
            Quaternion qMax = Quaternion.AngleAxis(aItems[i].MaxAngle, m_RootObject.forward);
            Vector3 rayMin = qMin * m_RootObject.right * 3.0f;
            Vector3 rayMax = qMax * m_RootObject.right * 3.0f;
            Gizmos.DrawRay(m_RootObject.position, rayMin);
            Gizmos.DrawRay(m_RootObject.position, rayMax);
          }
        }
      }
    }
  }
}
} // namespace TiltBrush