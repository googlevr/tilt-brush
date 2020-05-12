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

/// Variant of StrokeModificationTool that has two states, one for the "on" behavior and
/// one for the "off" behavior.
public class ToggleStrokeModificationTool : StrokeModificationTool {
  [SerializeField] protected Material m_ToolHotOffMaterial;
  [SerializeField] protected Renderer m_OnMesh;
  [SerializeField] protected Renderer m_OffMesh;
  [SerializeField] protected Renderer m_HighlightMesh;
  [SerializeField] protected Renderer m_OnRotationMesh;
  [SerializeField] protected Renderer m_OffRotationMesh;
  [SerializeField] private float m_ToggleAnimationDuration = 0.2f;
  [SerializeField] protected float m_HapticsToggleOn;
  [SerializeField] protected float m_HapticsToggleOff;
  [SerializeField] private float m_HapticsIntersection;
  [SerializeField] private bool m_AllowDoubleTap;
  [SerializeField] private float m_DoubleTapDuration = 0.4f;
  [SerializeField] private float m_MaxSpinSpeed;
  [SerializeField] private float m_SpinSpeedAcceleration;
  [SerializeField] private float m_SpinSpeedDecay;
  private float m_SpinSpeed;
  private float m_SpinSpeedVel;
  private float m_SpinAmount;

  protected bool m_CurrentlyHot;
  private bool m_ToggleAnimationSwitched = true;
  private float m_ToggleAnimationRemaining = 0f;

  private Queue<float> m_DoubleTapTimes;
  private bool m_EatDoubleTapInput;
  protected float m_LastIntersectionTime;

  private Vector3 m_OnRotationMeshBaseLocalPosition;
  private Vector3 m_OffRotationMeshBaseLocalPosition;

  void ClearDoubleTapTimes() {
    m_DoubleTapTimes.Clear();
    m_DoubleTapTimes.Enqueue(0.0f);
    m_DoubleTapTimes.Enqueue(0.0f);
  }

  virtual protected bool IsOn() {
    return true;
  }

  override public void Init() {
    base.Init();
    m_DoubleTapTimes = new Queue<float>();
    ClearDoubleTapTimes();
    m_OnRotationMeshBaseLocalPosition = m_OnRotationMesh.transform.localPosition;
    m_OffRotationMeshBaseLocalPosition = m_OffRotationMesh.transform.localPosition;
  }

  override public void EnableTool(bool bEnable) {
    base.EnableTool(bEnable);
    UpdateMesh();
    ResetDetection();
    ResetToggleAnimation();
  }

  override public void HideTool(bool bHide) {
    base.HideTool(bHide);
    SnapIntersectionObjectToController();
    if (m_SketchSurface.IsInFreePaintMode()) {
      if (bHide) {
        m_OnMesh.gameObject.SetActive(false);
        m_OffMesh.gameObject.SetActive(false);
        m_HighlightMesh.gameObject.SetActive(false);
      } else {
        m_ToolTransform.localScale = Vector3.one * m_CurrentSize;
        UpdateMesh();
        ResetToggleAnimation();
      }
    }
  }

  override public void LateUpdateTool() {
    SnapIntersectionObjectToController();
  }

  public void StartToggleAnimation() {
    m_ToggleAnimationRemaining = m_ToggleAnimationDuration;
    m_ToggleAnimationSwitched = false;
  }

  protected void ResetToggleAnimation() {
    // If we're in the middle of an animation, make sure we come out the other side.
    if (!m_ToggleAnimationSwitched) {
      OnAnimationSwitch();
      UpdateMesh();
    }
    m_ToggleAnimationRemaining = 0;
    m_ToggleAnimationSwitched = true;
  }

  /// Animates the selection tool when toggling:
  /// * Shrinks to zero                  - first half of the animation
  /// * Toggles the mode                 - midpoint
  /// * Grows back to its proper size.   - second half
  void UpdateToggleAnimation() {
    float halftime = m_ToggleAnimationDuration * .5f;
    m_ToggleAnimationRemaining -= Time.deltaTime;
    if (m_ToggleAnimationRemaining < 0) {
      m_ToggleAnimationRemaining = 0;
    }

    float scale;
    if (!m_ToggleAnimationSwitched) {
      if (m_ToggleAnimationRemaining >= halftime) {
        // Shrink
        scale = Mathf.Clamp01((m_ToggleAnimationRemaining - halftime) / halftime);
      } else {
        // Switch, and make a sound and haptic pulse
        scale = 0;
        m_ToggleAnimationSwitched = true;
        OnAnimationSwitch();
        UpdateMesh();
      }
    } else {
      // Grow
      scale = 1f - Mathf.Clamp01(m_ToggleAnimationRemaining / halftime);
    }
    m_ToolTransform.localScale = Vector3.one * scale * m_CurrentSize;
  }

  virtual protected void OnAnimationSwitch() { }

  protected void UpdateMesh() {
    bool on = IsOn();
    m_OnMesh.gameObject.SetActive(on);
    m_OffMesh.gameObject.SetActive(!on);
    m_HighlightMesh.gameObject.SetActive(false);
  }

  protected void ResetToolRotation() {
    m_SpinSpeed = 0.0f;
    m_SpinSpeedVel = 0.0f;
    m_SpinAmount = 0.0f;

    Vector3 resetPos = InputManager.Brush.Geometry.ToolAttachPoint.position +
        InputManager.Brush.Geometry.ToolAttachPoint.forward * m_PointerForwardOffset;

    m_OnRotationMesh.transform.rotation = InputManager.Brush.Geometry.ToolAttachPoint.rotation *
        Quaternion.AngleAxis(m_SpinAmount, Vector3.forward);
    m_OffRotationMesh.transform.rotation = InputManager.Brush.Geometry.ToolAttachPoint.rotation *
        Quaternion.AngleAxis(m_SpinAmount, Vector3.forward);
    m_ToolTransform.position = resetPos;

    m_OnRotationMesh.transform.localPosition = m_OnRotationMeshBaseLocalPosition;
    m_OffRotationMesh.transform.localPosition = m_OffRotationMeshBaseLocalPosition;
  }

  override protected void UpdateAudioVisuals() {
    RequestPlayAudio(m_CurrentlyHot);

    bool on = IsOn();
    var hotMaterial = on ? m_ToolHotMaterial : m_ToolHotOffMaterial;
    Material [] onMeshMaterials = m_OnMesh.materials;
    for (int i = 0; i < onMeshMaterials.Length; ++i) {
      onMeshMaterials[i] = m_CurrentlyHot ? hotMaterial : m_ToolColdMaterial;
    }
    m_OnMesh.materials = onMeshMaterials;
    m_OffMesh.material = m_CurrentlyHot ? hotMaterial : m_ToolColdMaterial;
    m_OnRotationMesh.material = IsHot ? hotMaterial : m_ToolColdMaterial;
    m_OffRotationMesh.material = IsHot ? hotMaterial : m_ToolColdMaterial;

    if (IsHot) {
      int direction = on ? -1 : 1;
      m_SpinSpeedVel += direction * (m_SpinSpeedAcceleration * Time.deltaTime);
      m_SpinSpeed = m_SpinSpeed + m_SpinSpeedVel * Time.deltaTime;
      if (m_SpinSpeed < -m_MaxSpinSpeed || m_SpinSpeed > m_MaxSpinSpeed) {
        m_SpinSpeed = Mathf.Clamp(m_SpinSpeed, -m_MaxSpinSpeed, m_MaxSpinSpeed);
        m_SpinSpeedVel = 0.0f;
      }
    } else {
      float speedDelta = m_SpinSpeedDecay * Time.deltaTime;
      m_SpinSpeed = Mathf.Sign(m_SpinSpeed) * Mathf.Max(Mathf.Abs(m_SpinSpeed) - speedDelta, 0.0f);
      m_SpinSpeedVel = 0.0f;
    }
    m_SpinAmount += m_SpinSpeed * Time.deltaTime;

    if (m_ToggleAnimationRemaining > 0) {
      UpdateToggleAnimation();
    } else {
      m_ToolTransform.localScale = Vector3.one * m_CurrentSize;
    }
  }

  override protected void SnapIntersectionObjectToController() {
    if (m_LockToController) {
      Vector3 toolPos = InputManager.Brush.Geometry.ToolAttachPoint.position +
          InputManager.Brush.Geometry.ToolAttachPoint.forward * m_PointerForwardOffset;
      m_ToolTransform.position = toolPos;
      m_ToolTransform.rotation = InputManager.Brush.Geometry.ToolAttachPoint.rotation;

      Quaternion qTool = InputManager.Brush.Geometry.ToolAttachPoint.rotation *
          Quaternion.AngleAxis(m_SpinAmount, Vector3.forward);
      m_OnRotationMesh.transform.rotation = qTool;
      m_OffRotationMesh.transform.rotation = qTool;
    } else {
      transform.position = SketchSurfacePanel.m_Instance.transform.position;
      transform.rotation = SketchSurfacePanel.m_Instance.transform.rotation;
    }
  }

  override protected void UpdateDetection() {
    // Look for double tap.
    m_CurrentlyHot = IsHot;
    if (m_AllowDoubleTap && m_CurrentlyHot && !m_EatDoubleTapInput) {
      // Store the current time and lose the oldest time.
      m_DoubleTapTimes.Enqueue(Time.realtimeSinceStartup);
      m_DoubleTapTimes.Dequeue();

      // See if we've tapped enough in the time duration.
      bool doubleTapGood = Time.realtimeSinceStartup - m_LastIntersectionTime > m_DoubleTapDuration;
      foreach (float time in m_DoubleTapTimes) {
        if (Time.realtimeSinceStartup - time > m_DoubleTapDuration) {
          doubleTapGood = false;
          break;
        }
      }
      if (doubleTapGood) {
        OnDoubleTap();
        m_EatInput = true;
        m_CurrentlyHot = false;
        ClearDoubleTapTimes();
      }

      // Protect against coming in until we've let off the hotness.
      m_EatDoubleTapInput = true;
    } else {
      m_EatDoubleTapInput = m_CurrentlyHot;
    }

    OnUpdateDetection();

    if (m_CurrentlyHot) {
      if (App.Config.m_UseBatchedBrushes) {
        UpdateBatchedBrushDetection(m_ToolTransform.position);
      } else {
        UpdateSolitaryBrushDetection(m_ToolTransform.position);
      }

      // If hot, always disable highlight mesh
      m_HighlightMesh.gameObject.SetActive(false);
    }

    m_ToolWasHot = m_CurrentlyHot;
  }

  virtual public void OnUpdateDetection() { }

  virtual public void OnDoubleTap() { }

  override public void IntersectionHappenedThisFrame() {
    HapticFeedback();
  }

  protected void HapticFeedback() {
    InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush,
        m_HapticsIntersection);
  }

  override public bool CanAdjustSize() {
    return !m_CurrentlyHot;
  }
}
}  // namespace TiltBrush
