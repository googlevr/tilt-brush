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
using TMPro;

namespace TiltBrush {

public class CameraPathFovKnot : CameraPathKnot {
  public enum ControlType {
    Knot = CameraPathKnot.kDefaultControl,
    FovControl,
  }

  [SerializeField] protected Color m_FovControlColor;
  [SerializeField] protected Color m_FovControlColorInactive;
  [SerializeField] protected LineRenderer m_FovLineRenderer;
  [SerializeField] protected GameObject m_FovControl;
  [SerializeField] protected Renderer m_FovControlMesh;
  [SerializeField] protected TextMeshPro m_Text;
  [SerializeField] protected Transform m_TextAnchor;
  [SerializeField] protected Vector2 m_FovRange;
  [SerializeField] protected float m_FovMaxVisualLength;

  private float m_FovValue;

  public float CameraFov {
    get => Mathf.Lerp(m_FovRange.x, m_FovRange.y, m_FovValue / m_FovMaxVisualLength);
  }

  public float FovValue {
    get => m_FovValue;
    set {
      m_FovValue = value;
      m_Text.text = CameraFov.ToString("F1");
    }
  }

  override public Transform GetGrabTransform(int control) {
    switch ((ControlType)control) {
    case ControlType.FovControl: return m_FovControl.transform;
    }
    return base.GetGrabTransform(control);
  }

  override protected void Awake() {
    base.Awake();
    m_Type = Type.Fov;
    m_FovValue = 0.0f;
    RefreshVisuals();
  }

  public float GetFovValueFromY(float y_GS) {
    float yDiff = y_GS - transform.position.y;
    return Mathf.Clamp(yDiff / Coords.CanvasPose.scale, 0.0f, m_FovMaxVisualLength);
  }

  override public void RefreshVisuals() {
    // This is a little out of place here, but doesn't hurt.
    transform.forward = Vector3.up;

    Vector3 fov_CS = Vector3.zero;
    fov_CS = transform.forward * m_FovValue * Coords.CanvasPose.scale;
    m_FovControl.transform.position = transform.position + fov_CS;

    m_FovLineRenderer.SetPosition(0, m_FovControlMesh.transform.position);
    m_FovLineRenderer.SetPosition(1, transform.position);
  }

  override public void ActivateTint(bool active) {
    Color meshColor = m_InactiveColor;
    Color tangentColor = m_FovControlColorInactive;
    if (active) {
      meshColor = m_ActiveColor;
      tangentColor = m_FovControlColor;
    }

    for (int i = 0; i < m_Meshes.Length; ++i) {
      m_Meshes[i].material.color = meshColor;
    }
    m_FovLineRenderer.material.color = tangentColor;
    m_FovControlMesh.material.color = tangentColor;
    m_Text.gameObject.SetActive(active);
    // Face text toward us if we're active.
    if (active) {
      Vector3 headToTextNoY = m_TextAnchor.position - ViewpointScript.Head.position;
      headToTextNoY.y = 0.0f;
      m_TextAnchor.forward = headToTextNoY.normalized;
    }
  }

  override public void RegisterHighlight(int control, bool showInactive = false) {
    if ((ControlType)control == ControlType.FovControl) {
      App.Instance.SelectionEffect.RegisterMesh(m_FovControlMesh.GetComponent<MeshFilter>());
    } else {
      base.RegisterHighlight(control, showInactive);
    }
  }

  override public void UnregisterHighlight() {
    App.Instance.SelectionEffect.UnregisterMesh(m_FovControlMesh.GetComponent<MeshFilter>());
    base.UnregisterHighlight();
  }

  override public float CollisionWithPoint(Vector3 point, out int control) {
    // Gather best score from knot and from fov control.
    float bestDistance = float.MaxValue;
    control = -1;

    float dist = Vector3.Distance(point, transform.position);
    if (dist < m_GrabRadius && dist < bestDistance) {
      bestDistance = dist;
      control = (int)ControlType.Knot;
    }

    // Don't register collision with controls if we're inactive.
    if (m_ActivePathVisuals.activeSelf) {
      dist = Vector3.Distance(point, m_FovControlMesh.transform.position);
      if (dist < m_GrabRadius && dist < bestDistance) {
        bestDistance = dist;
        control = (int)ControlType.FovControl;
      }
    }
    return 1.0f - (bestDistance / m_GrabRadius);
  }

  override public bool KnotCollisionWithPoint(Vector3 point) {
    return (Vector3.Distance(point, m_FovControl.transform.position) < m_GrabRadius) ||
        base.KnotCollisionWithPoint(point);
  }

  public CameraPathFovKnotMetadata AsSerializable() { 
    return new CameraPathFovKnotMetadata {
      Xf = TrTransform.FromTransform(transform),
      PathTValue = PathT.T,
      Fov = FovValue,
    };
  }
}

} // namespace TiltBrush