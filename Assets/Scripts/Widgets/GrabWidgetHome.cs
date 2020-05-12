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

using System;
using UnityEngine;

namespace TiltBrush {

public class GrabWidgetHome : MonoBehaviour {
  [NonSerialized] public TrTransform m_Transform_SS;
  [SerializeField] private float m_SnapHomeDistance;
  [SerializeField] private float m_ScaleInSpeed;
  [SerializeField] private bool m_UseOrientationOfOwner;
  [SerializeField] private float m_HintDistance;
  private bool m_InHomeRange;

  private Transform m_Owner;
  private Renderer m_Renderer;
  private MeshFilter m_MeshFilter;
  private float m_BaseScale;
  private float m_ScaleValue;

  private bool m_IsFixedPosition;

  public bool ShouldSnapHome() { return m_InHomeRange; }

  public void Init() {
    m_BaseScale = transform.localScale.x;
    m_Renderer = GetComponent<Renderer>();
    if (m_Renderer == null) {
      m_Renderer = transform.GetChild(0).GetComponent<Renderer>();
    }
    m_MeshFilter = GetComponentInChildren<MeshFilter>();
    gameObject.SetActive(false);
  }

  public void SetOwner(Transform owner) {
    m_Owner = owner;
    m_InHomeRange = false;
  }

  public void SetFixedPosition(Vector3 vPos_SS) {
    m_IsFixedPosition = true;
    m_Transform_SS = TrTransform.T(vPos_SS);
  }

  public void Reset() {
    m_ScaleValue = 0.0f;
    transform.localScale = Vector3.zero;
    m_InHomeRange = false;
  }

  void Update() {
    if (m_Owner) {
      if (!m_IsFixedPosition) {
        // Position us projected on the scene yz plane.
        // TODO: Revisit this when we support fully-general scale and rotation.
        // Note MathUtils, ConstrainRotationDelta.
        m_Transform_SS = App.Scene.AsScene[m_Owner];
        m_Transform_SS.translation.x = 0.0f;
      }

      // Update rotation if we should mimic owner.
      if (m_UseOrientationOfOwner) {
        Vector3 vOwnerEulers_SS = App.Scene.AsScene[m_Owner].rotation.eulerAngles;
        vOwnerEulers_SS.y = 0.0f;
        vOwnerEulers_SS.z = 0.0f;
        m_Transform_SS.rotation = Quaternion.Euler(vOwnerEulers_SS);
      }

      // Update scale.
      if (m_ScaleValue < 1.0f) {
        m_ScaleValue += Time.deltaTime * m_ScaleInSpeed;
        m_Transform_SS.scale = (1.0f / App.Scene.Pose.scale) * m_BaseScale *
            Mathf.Min(m_ScaleValue, 1.0f);
      }

      App.Scene.AsScene[transform] = m_Transform_SS;

      // Update visuals with the distance to our owner.
      m_InHomeRange = WithinRange(m_Owner.position);
      m_Renderer.material.color = m_InHomeRange ? Color.white : Color.grey;
    }
  }

  public bool WithinRange(Vector3 pos) {
    Vector3 vPosToUs = transform.position - pos;
    return vPosToUs.magnitude < m_SnapHomeDistance;
  }

  public bool WithinHintRange(Vector3 pos) {
    Vector3 vPosToUs = transform.position - pos;
    return vPosToUs.magnitude < m_HintDistance;
  }

  public void RenderHighlight() {
    App.Instance.SelectionEffect.RegisterMesh(m_MeshFilter);
  }
}

}  // namespace TiltBrush
