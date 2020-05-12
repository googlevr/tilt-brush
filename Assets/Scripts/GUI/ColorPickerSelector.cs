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

public class ColorPickerSelector : MonoBehaviour {
  const float SQRT3 = 1.7320508f;

  public Renderer m_HighlightMesh;
  private Collider m_Collider;
  private Renderer m_Renderer;
  private Mesh m_StandardMesh;
  private Mesh m_TriangleMesh;
  [SerializeField] private Transform m_CurrentValueTransform;

  private ColorPickerMode m_LocalMode;

  // Locks the selector plane so its height doesn't
  // move along with the slider
  [SerializeField] private bool m_FixedSelectorPlane;
  private Vector3 m_RawValue;

  public Collider GetCollider() { return m_Collider; }

  /// Position within the color volume.
  /// X and Y are color plane, Z is slider axis; all values range [0, 1]
  public Vector3 RawValue {
    get {
      return m_RawValue;
    }
    set {
      m_RawValue = value;
      m_CurrentValueTransform.localPosition = new Vector3(Mathf.Clamp01(value.x) * 2 - 1,
                                                          m_FixedSelectorPlane ? 0 : Mathf.Clamp01(value.z) * 2 - 1,
                                                          Mathf.Clamp01(value.y) * 2 - 1);
      transform.localPosition = new Vector3(transform.localPosition.x,
                                            m_CurrentValueTransform.localPosition.y,
                                            transform.localPosition.z);
      m_Renderer.material.SetFloat("_Slider01", value.z);
    }
  }

  public void SetLocalMode(ColorPickerMode mode) {
    m_LocalMode = mode;
  }

  void Awake() {
    m_Renderer = GetComponent<Renderer>();
    m_Collider = GetComponent<Collider>();
    m_StandardMesh = GetComponent<MeshFilter>().sharedMesh;
    Mesh m = new Mesh();
    Vector3 lowerleft = new Vector3(-0.5f, -0.5f, 0);
    m.vertices = new Vector3[] {
      lowerleft + new Vector3(0,0,0),
      lowerleft + new Vector3(0,1,0),
      lowerleft + new Vector3(SQRT3/2, .5f, 0),
    };
    m.uv = new Vector2[] {
      new Vector2(0,0),
      new Vector2(0,1),
      new Vector2(1, .5f),
    };
    m.triangles = new int[] {
      0, 1, 2
    };
    m_TriangleMesh = m;
    CustomColorPaletteStorage.m_Instance.ModeChanged += OnModeChanged;
  }

  void OnDestroy() {
    CustomColorPaletteStorage.m_Instance.ModeChanged -= OnModeChanged;
  }

  void OnModeChanged() {
    ColorPickerInfo info = ColorPickerUtils.GetInfoForMode(m_LocalMode);
    m_Renderer.material.shader = info.shader;

    if (m_LocalMode == ColorPickerMode.SL_H_Triangle) {
      GetComponent<MeshFilter>().sharedMesh = m_TriangleMesh;
    } else {
      GetComponent<MeshFilter>().sharedMesh = m_StandardMesh;
    }
    RawValue = RawValue;        // force update of material param
  }

  public Vector3 GetValueFromHit(RaycastHit hit) {
    var localHitPoint = transform.InverseTransformPoint(hit.point);
    return new Vector3(localHitPoint.x + 0.5f,
                       localHitPoint.y + 0.5f,
                       RawValue.z);
  }

  public void SetTintColor(Color rColor) {
    m_Renderer.material.SetColor("_Color", rColor);
    m_HighlightMesh.material.SetColor("_Color", rColor);
  }
}
}  // namespace TiltBrush
