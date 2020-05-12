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
public abstract class MediaWidget : GrabWidget {
  [SerializeField] protected Renderer m_SelectionIndicatorRenderer;
  [SerializeField] protected Vector3 m_ContainerBloat;
  [SerializeField] protected bool m_UngrabbableFromInside;
  [SerializeField] protected float m_MinSize_CS;
  [SerializeField] protected float m_MaxSize_CS;

  protected float m_Size = 1.0f;
  protected bool m_LoadingFromSketch;

  public bool LoadingFromSketch { set { m_LoadingFromSketch = value; } }
  override public bool SupportsNegativeSize => true;

  protected override void Awake() {
    base.Awake();
    WidgetManager.m_Instance.WidgetsDormant = false;
    App.Scene.PoseChanged += OnScenePoseChanged;
  }

  protected override void OnHide() {
    base.OnHide();
    App.Scene.PoseChanged -= OnScenePoseChanged;
  }

  override protected void InitPin() {
    m_Pin.Init(m_BoxCollider.transform, this);
  }

  protected abstract void UpdateScale();

  /// Used to name fbx nodes
  public abstract string GetExportName();

  override public float GetSignedWidgetSize() {
    return m_Size;
  }

  override public Vector2 GetWidgetSizeRange() {
    float maxScale = m_MaxSize_CS / transform.localScale.x;
    float minScale = m_MinSize_CS / transform.localScale.x;
    return new Vector2(minScale, maxScale);
  }

  public override void RestoreFromToss() {
    m_CurrentState = State.Visible;
    m_ShowTimer = m_ShowDuration;
  }

  private void OnScenePoseChanged(TrTransform prev, TrTransform current) {
    m_ContainerBloat *= prev.scale / current.scale;
    UpdateScale();
  }

  override protected void SetWidgetSizeInternal(float fScale) {
    var sizeRange = GetWidgetSizeRange();
    m_Size = Mathf.Sign(fScale) * Mathf.Clamp(Mathf.Abs(fScale), sizeRange.x, sizeRange.y);
    UpdateScale();
  }

  override public void Activate(bool bActive) {
    // Don't call base class because it causes grip hint to show even when dormant.
    if (!WidgetManager.m_Instance.WidgetsDormant) {
      if (m_SelectionIndicatorRenderer != null) {
        m_SelectionIndicatorRenderer.enabled = bActive;
      }

      if (bActive) {
        // Render the highlight mask into the stencil buffer
        RegisterHighlight();
      }

      // Set appropriate post process values for the highlight post effecct
      if (m_UserInteracting) {
        Shader.SetGlobalFloat("_GrabHighlightIntensity", 1.0f);
      } else {
        Shader.SetGlobalFloat("_GrabHighlightIntensity", 0.5f);
      }

      // When someone tries to manipulate a pinned object,
      // show additive fill along with pin, tooltip.
      if (m_UserInteracting && Pinned) {
        Shader.SetGlobalFloat("_NoGrabHighlightIntensity", 1.0f);
      } else {
        Shader.SetGlobalFloat("_NoGrabHighlightIntensity", 0.0f);
      }
    }
  }
}

} // namespace TiltBrush
