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

public class NavButton : BaseButton {
  public Texture2D m_SelectionTexture;
  public UIComponent.ComponentMessage m_ButtonType;
  public int m_GotoPage;
  private TextMesh m_TextMesh;
  private Renderer m_TextMeshRenderer;
  private TextMeshPro m_TextMeshPro;
  public Color m_InactiveColor;

  override protected void OnRegisterComponent() {
    base.OnRegisterComponent();
    m_TextMesh = GetComponentInChildren<TextMesh>();
    if (m_TextMesh) {
      m_TextMeshRenderer = m_TextMesh.GetComponent<Renderer>();
    }
    m_TextMeshPro = GetComponentInChildren<TextMeshPro>();
  }

  override protected void OnButtonPressed() {
    if (m_Manager) {
      m_Manager.SendMessageToComponents(m_ButtonType, m_GotoPage);
    }
  }

  override public void SetButtonSelected(bool bSelected) {
    m_ButtonSelected = bSelected;
    m_ButtonRenderer.enabled = !bSelected;
    if (m_TextMeshRenderer) {
      m_TextMeshRenderer.material.SetColor("_Color", (bSelected ? Color.white : m_InactiveColor));
    }
    if (m_TextMeshPro) {
      m_TextMeshPro.color = (bSelected ? Color.white : m_InactiveColor);
    }
  }

  public void SetGotoPage(int iPage) {
    m_GotoPage = iPage;
    if (m_TextMesh) {
      m_TextMesh.text = (m_GotoPage + 1).ToString();
    }
    if (m_TextMeshPro) {
      m_TextMeshPro.text = (m_GotoPage + 1).ToString();
    }
  }
}
}  // namespace TiltBrush
