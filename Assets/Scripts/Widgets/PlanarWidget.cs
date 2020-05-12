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

/// PlanarWidget is a type of GrabWidget that defines the interaction bounds by the
///   object's BoxCollider, extruded along local z by m_GrabDistance;
///   In addition, it allows for scaling and custom placement on Show(true).
public class PlanarWidget : GrabWidget {
  [SerializeField] protected Transform m_LeftBorder;
  [SerializeField] protected Transform m_RightBorder;
  [SerializeField] protected Transform m_Title;
  [SerializeField] protected Transform m_DismissMessage;
  [SerializeField] protected float m_MeshScalar = 1.0f;
  [SerializeField] protected float m_BorderOffset;
  [SerializeField] protected float m_BorderScale = 1.0f;
  [SerializeField] protected Vector2 m_ScaleRange;
  [SerializeField] protected float m_SpawnPlacementHeightPercent;
  [SerializeField] protected float m_SpawnPlacementDistance;
  [SerializeField] protected float m_ColliderBloat;

  private Vector2 m_DismissTextSize;
  private float m_CurrentScale;
  private Vector3 m_BaseScale;
  private float m_TransitionScale;
  protected float m_AspectRatio;

  override protected void Awake() {
    base.Awake();

    m_CurrentScale = m_ScaleRange.x;
    m_BaseScale = m_Mesh.localScale;
    if (m_DismissMessage) {
      TextMesh mesh = m_DismissMessage.GetComponentInChildren<TextMesh>();
      m_DismissTextSize.x = TextMeasureScript.GetTextWidth(mesh);
      m_DismissTextSize.y = TextMeasureScript.GetTextHeight(mesh);
    }
    m_TransitionScale = 0.0f;
    m_AspectRatio = 1.0f;
    UpdateScale();
  }

  override public void Show(bool bShow, bool bPlayAudio = true) {
    //if we're hidden and we're asked to show, position in front of the user
    if (bShow && m_CurrentState == State.Hiding || m_CurrentState == State.Invisible) {
      Transform head = ViewpointScript.Head;
      Vector3 vHeadForwardNoY = head.forward;
      vHeadForwardNoY.y = 0.0f;
      vHeadForwardNoY.Normalize();

      //place in front of user
      Vector3 vPanelPlacement = head.position;
      vPanelPlacement.y *= m_SpawnPlacementHeightPercent;
      vPanelPlacement += vHeadForwardNoY * m_SpawnPlacementDistance;

      //face us toward user
      Vector3 vToPanel = vPanelPlacement - head.position;

      transform.position = vPanelPlacement;
      transform.forward = vToPanel.normalized;
      m_CurrentScale = m_ScaleRange.x;
      UpdateScale();
    }

    base.Show(bShow, bPlayAudio);
  }

  override protected void OnUpdate() {
    //if our transform changed, update the beams
    float fShowRatio = GetShowRatio();
    if (m_TransitionScale != fShowRatio) {
      m_TransitionScale = fShowRatio;
      UpdateScale();
    }
  }

  protected void UpdateScale() {
    //scale texture mesh
    Vector3 vScale = m_BaseScale;
    vScale.x *= m_CurrentScale * m_TransitionScale * m_AspectRatio;
    vScale.y *= m_CurrentScale * m_TransitionScale;
    m_Mesh.localScale = vScale;

    //set collider bounds
    Vector3 vColliderBounds = m_BoxCollider.size;
    vColliderBounds.x = (vScale.x * m_MeshScalar) + m_ColliderBloat;
    vColliderBounds.y = (vScale.y * m_MeshScalar) + m_ColliderBloat;
    m_BoxCollider.size = vColliderBounds;

    //set border positions
    Vector3 vBorderPos = m_LeftBorder.localPosition;
    vBorderPos.x = -((vScale.x * m_BorderScale) + m_BorderOffset);
    m_LeftBorder.localPosition = vBorderPos;

    vBorderPos.x *= -1.0f;
    m_RightBorder.localPosition = vBorderPos;

    //scale borders to match image
    Vector3 vBorderScale = m_LeftBorder.localScale;
    vBorderScale.y = (vScale.y * m_BorderScale) + m_BorderOffset;
    m_LeftBorder.localScale = vBorderScale;
    m_RightBorder.localScale = vBorderScale;

    //title in the top left
    if (m_Title) {
      Vector3 vTitlePos = m_Title.localPosition;
      vTitlePos.x = -vBorderPos.x;
      vTitlePos.y = (vScale.y * m_BorderScale);
      m_Title.localPosition = vTitlePos;
    }

    //dismiss on the backside bottom left
    if (m_DismissMessage) {
      Vector3 vDismissPos = m_DismissMessage.localPosition;
      vDismissPos.x = -vBorderPos.x;
      if (vScale.x * m_MeshScalar < m_DismissTextSize.x) {
        vDismissPos.y = -((vScale.y * m_BorderScale) + m_DismissTextSize.y);
      } else {
        vDismissPos.y = -(vScale.y * m_BorderScale);
      }
      m_DismissMessage.localPosition = vDismissPos;
    }
  }

  override public Vector2 GetWidgetSizeRange() {
    return m_ScaleRange;
  }

  override public float GetSignedWidgetSize() {
    return m_CurrentScale;
  }

  override protected void SetWidgetSizeInternal(float fScale) {
    m_CurrentScale = Mathf.Clamp(fScale, m_ScaleRange.x, m_ScaleRange.y);
    UpdateScale();
  }
}
}  // namespace TiltBrush
