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

// This class currently contains logic for text and color swatch behavior.
// The ideal scenario would be for BasePanel to reference a base class (PanelFlair) and for
// the text logic to be in a PanelFlairText class, and color swatch logic to be in a
// PanelFlairColorSwatch class.  However, in practice, we're not using the text path at
// the moment, so I'm leaving these classes merged.
public class PanelFlair : MonoBehaviour {
  [SerializeField] private bool m_AlwaysShow;
  [SerializeField] private Color m_TextColor;
  [SerializeField] private Renderer m_Renderer;
  [SerializeField] private TextMeshPro m_TextMesh;
  [SerializeField] private Material m_DefaultMaterial;
  [SerializeField] private Material m_BloomMaterial;

  private Vector3 m_Offset;

  public bool AlwaysShow { get { return m_AlwaysShow; } }

  void Awake() {
    Hide();
    PointerManager.m_Instance.OnMainPointerBrushChange += OnMainPointerBrushChange;
  }

  void OnDestroy() {
    PointerManager.m_Instance.OnMainPointerBrushChange -= OnMainPointerBrushChange;
  }

  public void ParentToPanel(Transform parent, Vector3 panelOffset) {
    transform.position = parent.position;
    transform.rotation = parent.rotation;
    transform.SetParent(parent);
    m_Offset = panelOffset;
  }

  public void Hide() {
    if (m_Renderer != null) {
      m_Renderer.enabled = false;
    }
  }

  public void Show() {
    if (m_Renderer != null) {
      m_Renderer.enabled = true;
    }
  }

  public void UpdateAnimationOnPanel(Vector3 panelBounds, Quaternion orient, float alpha) {
    Vector3 itemDescriptionOffset = panelBounds;
    itemDescriptionOffset.x *= m_Offset.x;
    itemDescriptionOffset.y *= m_Offset.y;
    Vector3 transformedOffset = transform.parent.rotation * itemDescriptionOffset;
    transform.position = transform.parent.position + transformedOffset;
    transform.rotation = orient;

    if (m_TextMesh != null) {
      Color color = m_TextColor;
      color.a = alpha;
      m_TextMesh.color = color;
    }
    if (m_Renderer) {
      var color = App.BrushColor.CurrentColor;
      color.a = alpha;
      m_Renderer.material.SetColor("_Color", color);
      m_Renderer.material.SetColor("_TintColor", color);
    }
  }

  public void SetText(string text) {
    if (m_TextMesh != null && text != null) {
      m_TextMesh.text = text;
    }
  }

  void OnMainPointerBrushChange(TiltBrush.BrushDescriptor brush) {
    if (brush.m_UseBloomSwatchOnColorPicker) {
      m_Renderer.material = m_BloomMaterial;
    } else {
      m_Renderer.material = m_DefaultMaterial;
    }
  }
}

} // namespace TiltBrush