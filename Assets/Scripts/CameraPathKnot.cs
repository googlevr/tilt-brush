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

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush {

public class CameraPathKnot : MonoBehaviour {
  public const int kDefaultControl = 0;

  public enum Type {
    Position,
    Rotation,
    Speed,
    Fov,
    Invalid
  }

  [SerializeField] protected GameObject m_ActivePathVisuals;
  [SerializeField] protected GameObject m_InactivePathVisuals;
  [SerializeField] protected float m_GrabRadius = 0.2f;
  [SerializeField] protected Color m_ActiveColor;
  [SerializeField] protected Color m_InactiveColor;

  // Used by derived classes that are fixed to the path.
  [HideInInspector] public PathT PathT;
  protected float m_DistanceAlongSegment;

  protected Type m_Type;
  protected Renderer[] m_Meshes;
  protected List<MeshFilter> m_RenderHighlights;
  protected List<MeshFilter> m_InactiveRenderHighlights;

  public Type KnotType { get { return m_Type; } }

  public float DistanceAlongSegment {
    get { return m_DistanceAlongSegment; }
    set { m_DistanceAlongSegment = value; }
  }

  public Transform KnotXf {
    get { return transform; }
  }

  virtual public Transform GetGrabTransform(int control) {
    return transform;
  }

  virtual protected void Awake() {
    App.Scene.PoseChanged += OnScenePoseChanged;

    m_RenderHighlights = new List<MeshFilter>();
    m_InactiveRenderHighlights = new List<MeshFilter>();

    // Cache the renderers in the active path visuals.
    m_Meshes = m_ActivePathVisuals.GetComponentsInChildren<Renderer>();
    for (int i = 0; i < m_Meshes.Length; ++i) {
      MeshFilter mf = m_Meshes[i].GetComponent<MeshFilter>();
      if (mf != null) {
        m_RenderHighlights.Add(mf);
      }
    }

    // Tint the inactive path visuals to our inactive color and cache renderers.
    Renderer[] inactiveMeshes = m_InactivePathVisuals.GetComponentsInChildren<Renderer>();
    for (int i = 0; i < inactiveMeshes.Length; ++i) {
      inactiveMeshes[i].material.color = m_InactiveColor;
      MeshFilter mf = inactiveMeshes[i].GetComponent<MeshFilter>();
      if (mf != null) {
        m_InactiveRenderHighlights.Add(mf);
      }
    }

    SetActivePathVisuals(true);

    // Tint our knot to initialize all the mesh colors.
    ActivateTint(false);
    m_Type = Type.Invalid;
  }

  void OnDestroy() {
    App.Scene.PoseChanged -= OnScenePoseChanged;
  }

  virtual protected void OnScenePoseChanged(TrTransform prev, TrTransform current) {
    transform.localScale = Vector3.one / current.scale;
  }

  public void SetPosition(Vector3 pos) {
    transform.position = pos;
  }

  virtual public void RefreshVisuals() { }

  public void SetActivePathVisuals(bool onActivePath) {
    m_ActivePathVisuals.SetActive(onActivePath);
    m_InactivePathVisuals.SetActive(!onActivePath);
  }

  virtual public void ActivateTint(bool active) {
    Color matColor = active ? m_ActiveColor : m_InactiveColor;
    for (int i = 0; i < m_Meshes.Length; ++i) {
      m_Meshes[i].material.color = matColor;
    }
  }

  virtual public void RegisterHighlight(int control, bool showInactive = false) {
    if (m_ActivePathVisuals.activeSelf) {
      for (int i = 0; i < m_RenderHighlights.Count; ++i) {
        App.Instance.SelectionEffect.RegisterMesh(m_RenderHighlights[i]);
      }
    } else if (showInactive && m_InactivePathVisuals.activeSelf) {
      for (int i = 0; i < m_InactiveRenderHighlights.Count; ++i) {
        App.Instance.SelectionEffect.RegisterMesh(m_InactiveRenderHighlights[i]);
      }
    }
  }

  virtual public void UnregisterHighlight() {
    for (int i = 0; i < m_RenderHighlights.Count; ++i) {
      App.Instance.SelectionEffect.UnregisterMesh(m_RenderHighlights[i]);
    }
    for (int i = 0; i < m_InactiveRenderHighlights.Count; ++i) {
      App.Instance.SelectionEffect.UnregisterMesh(m_InactiveRenderHighlights[i]);
    }
  }

  virtual public float CollisionWithPoint(Vector3 point, out int control) {
    float dist = Vector3.Distance(point, transform.position);
    if (dist < m_GrabRadius) {
      // Flag custom data as valid.
      control = 0;
      return 1.0f - (dist / m_GrabRadius);
    }
    control = -1;
    return -1.0f;
  }

  virtual public bool KnotCollisionWithPoint(Vector3 point) {
    return Vector3.Distance(point, transform.position) < m_GrabRadius;
  }
}

} // namespace TiltBrush