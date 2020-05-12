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

public class HintObjectScript : MonoBehaviour {
  [SerializeField] private Transform m_HintObject;
  [SerializeField] private Transform m_HintObjectParent;
  [SerializeField] private float m_HintObjectScale = 1.05f;
  [SerializeField] private float m_ActiveMinShowAngle = 70.0f;
  [SerializeField] private bool m_ScaleXOnly = true;
  [SerializeField] private TextMesh m_HintText;

  private float m_ActivateSpeed = 6.0f;
  private float m_ActivateTimer;
  private Vector3? m_OriginalHintObjectPosition; // local
  private Quaternion? m_OriginalHintObjectRotation; // local
  private bool m_UnparentOnEnable = false;

  public enum State {
    Deactivated,
    Activating,
    Active,
    Deactivating
  }
  private State m_CurrentState;
  private Vector3 m_BaseScale;
  private float m_ActiveStatePrevTimer;

  public bool IsActive() { return m_CurrentState == State.Active; }

  public void SetHintText(string text) {
    if (m_HintText) {
      m_HintText.text = text;
    }
  }

  public string GetHintText() {
    return m_HintText != null ? m_HintText.text : "";
  }

  void Awake() {
    m_CurrentState = State.Deactivated;
    m_BaseScale = transform.localScale;
    m_ActivateTimer = 0.0f;
    UpdateScale(0.0f);
  }

  public void Activate(bool bActivate) {
    if (bActivate) {
      if (!gameObject.activeSelf) {
        gameObject.SetActive(true);
      }
      if (m_CurrentState != State.Active) {
        m_CurrentState = State.Activating;
      }
    } else {
      UnparentHintObject();
      if (m_CurrentState == State.Activating || m_CurrentState == State.Active) {
        m_CurrentState = State.Deactivating;
      }
    }
  }

  void OnDisable() {
    if (m_HintObject != null && HintObjectReparented) {
      m_HintObject.gameObject.SetActive(false);
      m_UnparentOnEnable = true;
    }
    m_CurrentState = State.Deactivated;
    m_ActivateTimer = 0.0f;
    UpdateScale(0.0f);
  }

  private void OnEnable() {
    if (IsActive()) {
      m_HintObject.gameObject.SetActive(true);
      ParentHintObject();
    } else if (m_UnparentOnEnable) {
      m_HintObject.gameObject.SetActive(true);
      UnparentHintObject();
      m_UnparentOnEnable = false;
    }
  }

  void OnDestroy() {
    if (m_HintObject != null && HintObjectReparented) {
      m_HintObject.gameObject.SetActive(false);
    }
  }

  void Update() {
    //update tutorial size according to state
    float fStep = m_ActivateSpeed * Time.deltaTime;
    switch (m_CurrentState) {
    case State.Activating:
      m_ActivateTimer = Mathf.Min(m_ActivateTimer + fStep, 1.0f);
      UpdateScale(m_ActivateTimer);
      if (m_HintObject != null && !HintObjectReparented) {
        m_HintObject.gameObject.SetActive(true);
        ParentHintObject();
      }
      if (m_ActivateTimer >= 1.0f) {
        m_ActiveStatePrevTimer = 1.0f;
        m_CurrentState = State.Active;
      }
      break;
    case State.Deactivating:
      m_ActivateTimer = Mathf.Max(m_ActivateTimer - fStep, 0.0f);
      UpdateScale(m_ActivateTimer);
      if (m_ActivateTimer <= 0.0f) {
        m_CurrentState = State.Deactivated;
        gameObject.SetActive(false);
      }
      break;
    case State.Active:
      UpdateActive();
      break;
    }
  }

  private void ParentHintObject() {
    if (m_HintObjectParent == null || m_HintObject == null ||
        m_HintObject.parent == m_HintObjectParent) {
      return;
    }

    var oldScale = transform.localScale;
    transform.localScale = m_BaseScale;

    if (!m_OriginalHintObjectPosition.HasValue) {
      m_OriginalHintObjectPosition = m_HintObject.localPosition;
      m_OriginalHintObjectRotation = m_HintObject.localRotation;
    } else {
      m_HintObject.localPosition = m_OriginalHintObjectPosition.Value;
      m_HintObject.localRotation = m_OriginalHintObjectRotation.Value;
    }

    var pos = m_HintObject.position;
    var rot = m_HintObject.rotation;
    m_HintObject.SetParent(m_HintObjectParent.transform);
    m_HintObject.position = pos;
    m_HintObject.rotation = rot;
    m_HintObject.localScale = Vector3.one * m_HintObjectScale;

    transform.localScale = oldScale;
  }

  private void UnparentHintObject() {
    if (m_HintObject == null || m_HintObject.parent == transform) {
      return;
    }

    m_HintObject.SetParent(transform);

    if (m_OriginalHintObjectPosition.HasValue) {
      m_HintObject.localPosition = m_OriginalHintObjectPosition.Value;
      m_HintObject.localRotation = m_OriginalHintObjectRotation.Value;
    }

    m_HintObject.localScale = Vector3.one * m_HintObjectScale;
  }

  void UpdateScale(float fScale) {
    Vector3 vScale = transform.localScale;
    if (m_ScaleXOnly) {
      vScale.x = m_BaseScale.x * fScale;
    } else {
      vScale = m_BaseScale * fScale;
    }
    transform.localScale = vScale;
  }

  void UpdateActive() {
    //if we're active but facing the wrong way, shrink us
    float fAngle = Vector3.Angle(ViewpointScript.Head.forward, transform.up);
    bool bRequestShowing = fAngle >= m_ActiveMinShowAngle;

    //manipulate our timer to adjust scale
    //  this has the added benefit of smooth transitions out of the active state
    float fStep = m_ActivateSpeed * Time.deltaTime;
    if (bRequestShowing) {
      m_ActivateTimer = Mathf.Min(m_ActivateTimer + fStep, 1.0f);
    } else {
      m_ActivateTimer = Mathf.Max(m_ActivateTimer - fStep, 0.0f);
    }

    //don't re-scale our object if it hasn't changed
    if (m_ActivateTimer != m_ActiveStatePrevTimer) {
      UpdateScale(m_ActivateTimer);
      m_ActiveStatePrevTimer = m_ActivateTimer;
    }
  }

  private bool HintObjectReparented {
    get {
      return m_HintObject != null && m_HintObject.parent == m_HintObjectParent;
    }
  }
}
}  // namespace TiltBrush
