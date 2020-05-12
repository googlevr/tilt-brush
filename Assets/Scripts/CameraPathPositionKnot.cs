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

public class CameraPathPositionKnot : CameraPathKnot {
  public enum ControlType {
    Knot = CameraPathKnot.kDefaultControl,
    TangentControlForward,
    TangentControlBack
  }

  [SerializeField] protected Color m_TangentControlColor;
  [SerializeField] protected Color m_TangentControlColorInactive;
  [SerializeField] protected LineRenderer m_TangentRenderer;
  [SerializeField] protected GameObject [] m_TangentControl;
  [SerializeField] protected GameObject [] m_TangentControlMesh;
  [SerializeField] protected float m_TangentScalar;
  [SerializeField] protected Transform m_KnotMesh;

  private float m_TangentMagnitude = 0.0f;
  private Vector3 m_BaseKnotMeshLocalScale;

  public Vector3 ScaledTangent {
    get {
      return transform.forward * m_TangentScalar * m_TangentMagnitude * Coords.CanvasPose.scale;
    }
  }

  public float TangentMagnitude {
    get { return m_TangentMagnitude; }
    set { m_TangentMagnitude = value; }
  }

  override public Transform GetGrabTransform(int control) {
    switch ((ControlType)control) {
    case ControlType.TangentControlForward: return m_TangentControl[0].transform;
    case ControlType.TangentControlBack: return m_TangentControl[1].transform;
    }
    return base.GetGrabTransform(control);
  }

  override protected void Awake() {
    base.Awake();

    RefreshVisuals();
    Debug.Assert(m_TangentControl.Length == 2);
    Debug.Assert(m_TangentControlMesh.Length == 2);
    m_BaseKnotMeshLocalScale = m_KnotMesh.localScale;
    m_Type = Type.Position;
  }

  public float GetTangentMagnitudeFromControlXf(TrTransform controlXf) {
    Vector3 knotToPos = controlXf.translation - transform.position;
    return knotToPos.magnitude / Coords.CanvasPose.scale;
  }

  override public void RefreshVisuals() {
    Vector3 scaledHalfTangent = Vector3.zero;
    scaledHalfTangent = transform.forward * m_TangentMagnitude * Coords.CanvasPose.scale;
    m_TangentControl[0].transform.position = transform.position + scaledHalfTangent;
    m_TangentControl[1].transform.position = transform.position - scaledHalfTangent;

    m_TangentRenderer.SetPosition(0, m_TangentControlMesh[0].transform.position);
    m_TangentRenderer.SetPosition(1, m_TangentControlMesh[1].transform.position);
  }

  override public void ActivateTint(bool active) {
    Color meshColor = m_InactiveColor;
    Color tangentColor = m_TangentControlColorInactive;
    if (active) {
      meshColor = m_ActiveColor;
      tangentColor = m_TangentControlColor;
    }

    for (int i = 0; i < m_Meshes.Length; ++i) {
      m_Meshes[i].material.color = meshColor;
    }
    m_TangentRenderer.material.color = tangentColor;
    m_TangentControlMesh[0].GetComponent<Renderer>().material.color = tangentColor;
    m_TangentControlMesh[1].GetComponent<Renderer>().material.color = tangentColor;
  }

  override public void RegisterHighlight(int control, bool showInactive = false) {
    if ((ControlType)control == ControlType.TangentControlForward) {
      App.Instance.SelectionEffect.RegisterMesh(
          m_TangentControlMesh[0].GetComponent<MeshFilter>());
    } else if ((ControlType)control == ControlType.TangentControlBack) {
      App.Instance.SelectionEffect.RegisterMesh(
          m_TangentControlMesh[1].GetComponent<MeshFilter>());
    } else {
      base.RegisterHighlight(control, showInactive);
    }
  }

  override public void UnregisterHighlight() {
    App.Instance.SelectionEffect.UnregisterMesh(
        m_TangentControlMesh[0].GetComponent<MeshFilter>());
    App.Instance.SelectionEffect.UnregisterMesh(
        m_TangentControlMesh[1].GetComponent<MeshFilter>());
    base.UnregisterHighlight();
  }

  override public float CollisionWithPoint(Vector3 point, out int control) {
    // Gather best score from knot and from tangent controls.
    float bestDistance = float.MaxValue;
    control = -1;

    float dist = Vector3.Distance(point, transform.position);
    if (dist < m_GrabRadius && dist < bestDistance) {
      bestDistance = dist;
      control = (int)ControlType.Knot;
    }

    // Don't register collision with controls if we're inactive.
    if (m_ActivePathVisuals.activeSelf) {
      dist = Vector3.Distance(point, m_TangentControlMesh[0].transform.position);
      if (dist < m_GrabRadius && dist < bestDistance) {
        bestDistance = dist;
        control = (int)ControlType.TangentControlForward;
      }

      dist = Vector3.Distance(point, m_TangentControlMesh[1].transform.position);
      if (dist < m_GrabRadius && dist < bestDistance) {
        bestDistance = dist;
        control = (int)ControlType.TangentControlBack;
      }
    }
    return 1.0f - (bestDistance / m_GrabRadius);
  }

  public void SetVisuallySpecial(bool special) {
    m_KnotMesh.localScale = m_BaseKnotMeshLocalScale * (special ? 1.75f : 1.0f);
  }

  public CameraPathPositionKnotMetadata AsSerializable() { 
    return new CameraPathPositionKnotMetadata {
      Xf = TrTransform.FromTransform(transform),
      TangentMagnitude = TangentMagnitude,
    };
  }
}

} // namespace TiltBrush