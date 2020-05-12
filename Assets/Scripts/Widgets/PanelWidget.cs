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
using System.Linq;

namespace TiltBrush {

public class PanelWidget : GrabWidget {
  [SerializeField] private Transform m_Border;
  [SerializeField] private float m_GrabFixedMaxFacingAngle = 70.0f;
  private BasePanel m_PanelSibling;
  private Vector3 m_BaseScale;
  private float m_PreviousShowRatio;

  public BasePanel PanelSibling {
    get { return m_PanelSibling; }
  }

  public bool IsAnimating() {
    return m_IntroAnimState == IntroAnimState.In;
  }

  public void ForceVisibleForInit() {
    m_ShowTimer = m_ShowDuration;
    m_CurrentState = State.Visible;
  }

  public void ForceInvisibleForInit() {
    m_ShowTimer = 0.0f;
    m_CurrentState = State.Invisible;
    UpdatePanelScale(0.0f);
  }

  override protected void Awake() {
    base.Awake();

    m_PanelSibling = GetComponent<BasePanel>();
    m_BaseScale = transform.localScale;
    m_PreviousShowRatio = 0.0f;
  }

  override protected void OnUpdate() {
    // Update panel size if we're coming or going.
    float fShowRatio = GetShowRatio();
    if (m_PreviousShowRatio != fShowRatio) {
      UpdatePanelScale(fShowRatio);
    }
    m_PreviousShowRatio = fShowRatio;

    if (m_PanelSibling && IsMoving()) {
      m_PanelSibling.OnPanelMoved();
    }
  }

  override protected void UpdateIntroAnimState() {
    IntroAnimState prevState = m_IntroAnimState;
    base.UpdateIntroAnimState();

    // If we're existing the in state, notify our panel.
    if (prevState != m_IntroAnimState) {
      if (prevState == IntroAnimState.In) {
        if (m_PanelSibling) {
          m_PanelSibling.OnWidgetShowAnimComplete();
        }
      }
    }
  }

  override public bool IsCollisionEnabled() {
    return IsMoving() || IsAnimating();
  }

  override protected void OnShow() {
    base.OnShow();

    // Spin out!
    if (m_PanelSibling && !m_Restoring) {
      m_IntroAnimState = IntroAnimState.In;
      if (m_PanelSibling) {
        m_PanelSibling.OnWidgetShowAnimStart();
      }

      Debug.Assert(!IsMoving(), "Shouldn't have velocity!");
      ClearVelocities();
      m_IntroAnimValue = 0.0f;
      m_ShowTimer = 0.0f;
      UpdateIntroAnim();
      UpdatePanelScale(0.0f);
    }
    PanelManager.m_Instance.RefreshConfiguredFlag();
  }

  override protected void OnHide() {
    if (m_PanelSibling) { m_PanelSibling.OnWidgetHide(); }
    base.OnHide();
    PanelManager.m_Instance.RefreshConfiguredFlag();
  }

  override protected void OnTossComplete() {
    App.Switchboard.TriggerPanelDismissed();
  }

  void UpdatePanelScale(float fShowRatio) {
    if (m_PanelSibling != null) {
      transform.localScale = m_BaseScale * fShowRatio;
    }
  }

  public void SetActiveTintToShowError(bool bError) {
    m_ActiveTint = bError ? Color.red : Color.white;
  }

  override public float GetActivationScore(
      Vector3 vControllerPos, InputManager.ControllerName name) {
    // Not allowed to grab panels unless we're in advanced mode.
    if (!PanelManager.m_Instance.AdvancedModeActive()) {
      return -1.0f;
    }

    // If this panel is fixed and facing away from the user, don't allow it to be grabbed.
    if (m_PanelSibling.m_Fixed &&
        Vector3.Angle(transform.forward, ViewpointScript.Gaze.direction) >
          m_GrabFixedMaxFacingAngle) {
      return -1.0f;
    }
    return base.GetActivationScore(vControllerPos, name);
  }

  // Removes meshes that are used for promos to avoid assigning colors to promo materials.
  public void RemovePromoMeshesFromTintable(MeshRenderer[] meshes) {
    m_TintableMeshes = m_TintableMeshes.Where(m => !meshes.Contains(m)).ToArray();
  }

  // Returns meshes that had promo materials to tintable meshes.
  // Current mesh tint should be accounted for by the caller.
  public void AddPromoMeshesToTintable(MeshRenderer[] meshes) {
    m_TintableMeshes = m_TintableMeshes.Concat(meshes).ToArray();
  }

  protected override TrTransform GetDesiredTransform(TrTransform xf_GS) {
    var outXf_GS = base.GetDesiredTransform(xf_GS);
    if (m_PanelSibling) {
      m_PanelSibling.OnPanelMoved();
    }
    return outXf_GS;
  }

  protected override void OnUserBeginInteracting() {
    base.OnUserBeginInteracting();
    PanelSibling.WidgetSiblingBeginInteraction();
    PanelManager.m_Instance.RefreshConfiguredFlag();
    PanelManager.m_Instance.LastPanelInteractedWith = m_PanelSibling;
  }

  protected override void OnUserEndInteracting() {
    base.OnUserEndInteracting();
    PanelSibling.WidgetSiblingEndInteraction();
    PanelManager.m_Instance.RefreshConfiguredFlag();
  }

  override public void RegisterHighlight() {
#if !UNITY_ANDROID
    if (!m_PanelSibling.m_Fixed) {
      base.RegisterHighlight();
    }
#endif
  }

  override public bool CanGrabDuringDeselection() {
    return true;
  }
}
}  // namespace TiltBrush
