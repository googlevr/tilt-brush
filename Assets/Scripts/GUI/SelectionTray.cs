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

using System.Collections;
using UnityEngine;

namespace TiltBrush {

public class SelectionTray : UIComponent {
  [SerializeField] private GameObject m_Mesh;
  [SerializeField] private Renderer m_Border;
  [SerializeField] private float m_AnimateSpeed;
  [SerializeField] private Vector2 m_AnimateRange;
  [SerializeField] private OptionButton m_GroupButton;

  private UIComponentManager m_UIComponentManager;
  private Coroutine m_AnimationCoroutine;
  private bool m_AnimateIn;
  private bool m_AnimateWhenEnabled;

  override protected void Awake() {
    base.Awake();
    m_UIComponentManager = GetComponent<UIComponentManager>();
    App.Switchboard.ToolChanged += OnToolChanged;
    App.Switchboard.SelectionChanged += OnSelectionChanged;
  }

  override protected void Start() {
    base.Start();

    // Begin disabled. Do this in Start() instead of Awake() so that button descriptions have a
    // chance to instantiate at the right position.
    m_AnimateIn = false;
    Vector3 localScale = transform.localScale;
    localScale.x = m_AnimateRange.x;
    transform.localScale = localScale;
    m_Mesh.SetActive(false);
    m_Collider.enabled = false;
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    App.Switchboard.ToolChanged -= OnToolChanged;
    App.Switchboard.SelectionChanged -= OnSelectionChanged;
  }

  private void OnEnable() {
    if (m_AnimateWhenEnabled) {
      m_AnimationCoroutine = StartCoroutine(Animate());
      m_AnimateWhenEnabled = false;
    }
  }

  override protected void OnDisable() {
    if (m_AnimationCoroutine != null) {
      // Skip to the end of animation
      StopCoroutine(m_AnimationCoroutine);
      Vector3 localScale = transform.localScale;
      localScale.x = m_AnimateIn ? m_AnimateRange.y : m_AnimateRange.x;
      transform.localScale = localScale;
      m_AnimationCoroutine = null;
    }
  }

  override public void SetColor(Color color) {
    base.SetColor(color);
    m_UIComponentManager.SetColor(color);
    m_Border.material.SetColor("_Color", color);
  }

  override public void UpdateVisuals() {
    base.UpdateVisuals();
    m_UIComponentManager.UpdateVisuals();
  }

  override public bool UpdateStateWithInput(bool inputValid, Ray inputRay,
        GameObject parentActiveObject, Collider parentCollider) {
    if (base.UpdateStateWithInput(inputValid, inputRay, parentActiveObject, parentCollider)) {
      if (parentActiveObject == null || parentActiveObject == gameObject) {
        if (BasePanel.DoesRayHitCollider(inputRay, GetCollider())) {
          m_UIComponentManager.UpdateUIComponents(inputRay, inputValid, parentCollider);
          return true;
        }
      }
    }
    return false;
  }

  override public void ResetState() {
    base.ResetState();
    m_UIComponentManager.Deactivate();
  }

  override public bool RaycastAgainstCustomCollider(Ray ray,
      out RaycastHit hitInfo, float dist) {
    return BasePanel.DoesRayHitCollider(ray, GetCollider(), out hitInfo);
  }

  void OnToolChanged() {
    bool isSelectionTool = SketchSurfacePanel.m_Instance.GetCurrentToolType() ==
                           BaseTool.ToolType.SelectionTool;
    if (isSelectionTool != m_AnimateIn) {
      if (m_AnimationCoroutine != null) {
        StopCoroutine(m_AnimationCoroutine);
      }
      m_AnimateIn = !m_AnimateIn;

      // If we get a callback that our tool changed while we're inactive, don't try to
      // start our coroutine until we've been enabled.
      if (isActiveAndEnabled) {
        m_AnimationCoroutine = StartCoroutine(Animate());
      } else {
        m_AnimateWhenEnabled = true;
      }
    }
  }

  void OnSelectionChanged() {
    m_GroupButton.UpdateVisuals();
  }

  IEnumerator Animate() {
    Vector3 localScale = transform.localScale;
    if (m_AnimateIn) {
      // Enable right away for animating in.
      m_Mesh.SetActive(true);
      m_Collider.enabled = true;

      float x = localScale.x;
      while (x < m_AnimateRange.y) {
        x += Time.deltaTime * m_AnimateSpeed;
        if (x >= m_AnimateRange.y) {
          x = m_AnimateRange.y;
        }
        localScale.x = x;
        transform.localScale = localScale;
        yield return null;
      }
    } else {
      // Disable collider immediately so we can't select something.
      m_Collider.enabled = false;

      float x = localScale.x;
      while (x > m_AnimateRange.x) {
        x -= Time.deltaTime * m_AnimateSpeed;
        if (x <= m_AnimateRange.x) {
          x = m_AnimateRange.x;
          // Disable mesh after animation for animating out.
          m_Mesh.SetActive(false);
        }
        localScale.x = x;
        transform.localScale = localScale;
        yield return null;
      }
    }
    m_AnimationCoroutine = null;
  }
}

} // namespace TiltBrush
