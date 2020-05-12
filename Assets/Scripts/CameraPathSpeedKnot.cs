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

public class CameraPathSpeedKnot : CameraPathKnot {
  public enum ControlType {
    Knot = CameraPathKnot.kDefaultControl,
    SpeedControl,
  }

  static public float kMinSpeed = 0.05f;

  [SerializeField] protected Color m_SpeedControlColor;
  [SerializeField] protected Color m_SpeedControlColorInactive;
  [SerializeField] protected LineRenderer m_SpeedLineRenderer;
  [SerializeField] protected GameObject m_SpeedControl;
  [SerializeField] protected Renderer m_SpeedControlMesh;
  [SerializeField] protected TextMeshPro m_Text;
  [SerializeField] protected Transform m_TextAnchor;
  [SerializeField] protected float m_MaxSpeed;
  [SerializeField] protected float m_SpeedMaxVisualLength;

  private float m_SpeedValue;

  public float CameraSpeed {
    get => Mathf.Lerp(kMinSpeed, m_MaxSpeed, m_SpeedValue / m_SpeedMaxVisualLength);
  }

  public float SpeedValue {
    get => m_SpeedValue;
    set {
      m_SpeedValue = value;
      m_Text.text = CameraSpeed.ToString("F1");
    }
  }

  override public Transform GetGrabTransform(int control) {
    if (control == (int)ControlType.SpeedControl) {
      return m_SpeedControl.transform;
    }
    return base.GetGrabTransform(control);
  }

  override protected void Awake() {
    base.Awake();
    m_Type = Type.Speed;
    RefreshVisuals();
  }

  public float GetSpeedValueFromY(float y_GS) {
    float yDiff = y_GS - transform.position.y;
    return Mathf.Clamp(yDiff / Coords.CanvasPose.scale, 0.0f, m_SpeedMaxVisualLength);
  }

  override public void RefreshVisuals() {
    // This is a little out of place here, but doesn't hurt.
    transform.forward = Vector3.up;

    Vector3 speed_CS = Vector3.zero;
    speed_CS = transform.forward * m_SpeedValue * Coords.CanvasPose.scale;
    m_SpeedControl.transform.position = transform.position + speed_CS;

    m_SpeedLineRenderer.SetPosition(0, m_SpeedControlMesh.transform.position);
    m_SpeedLineRenderer.SetPosition(1, transform.position);
  }

  override public void ActivateTint(bool active) {
    Color meshColor = m_InactiveColor;
    Color tangentColor = m_SpeedControlColorInactive;
    if (active) {
      meshColor = m_ActiveColor;
      tangentColor = m_SpeedControlColor;
    }

    for (int i = 0; i < m_Meshes.Length; ++i) {
      m_Meshes[i].material.color = meshColor;
    }
    m_SpeedLineRenderer.material.color = tangentColor;
    m_SpeedControlMesh.material.color = tangentColor;
    m_Text.gameObject.SetActive(active);
    // Face text toward us if we're active.
    if (active) {
      Vector3 headToTextNoY = m_TextAnchor.position - ViewpointScript.Head.position;
      headToTextNoY.y = 0.0f;
      m_TextAnchor.forward = headToTextNoY.normalized;
    }
  }

  override public void RegisterHighlight(int control, bool showInactive = false) {
    if ((ControlType)control == ControlType.SpeedControl) {
      App.Instance.SelectionEffect.RegisterMesh(m_SpeedControlMesh.GetComponent<MeshFilter>());
    } else {
      base.RegisterHighlight(control, showInactive);
    }
  }

  override public void UnregisterHighlight() {
    App.Instance.SelectionEffect.UnregisterMesh(m_SpeedControlMesh.GetComponent<MeshFilter>());
    base.UnregisterHighlight();
  }

  override public float CollisionWithPoint(Vector3 point, out int control) {
    // Gather best score from knot and from speed control.
    float bestDistance = float.MaxValue;
    control = -1;

    float dist = Vector3.Distance(point, transform.position);
    if (dist < m_GrabRadius && dist < bestDistance) {
      bestDistance = dist;
      control = (int)ControlType.Knot;
    }

    // Don't register collision with controls if we're inactive.
    if (m_ActivePathVisuals.activeSelf) {
      dist = Vector3.Distance(point, m_SpeedControlMesh.transform.position);
      if (dist < m_GrabRadius && dist < bestDistance) {
        bestDistance = dist;
        control = (int)ControlType.SpeedControl;
      }
    }
    return 1.0f - (bestDistance / m_GrabRadius);
  }

  override public bool KnotCollisionWithPoint(Vector3 point) {
    return (Vector3.Distance(point, m_SpeedControl.transform.position) < m_GrabRadius) ||
        base.KnotCollisionWithPoint(point);
  }

  public CameraPathSpeedKnotMetadata AsSerializable() { 
    return new CameraPathSpeedKnotMetadata {
      Xf = TrTransform.FromTransform(transform),
      PathTValue = PathT.T,
      Speed = SpeedValue,
    };
  }
}

} // namespace TiltBrush