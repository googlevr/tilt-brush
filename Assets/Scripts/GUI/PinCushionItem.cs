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
public class PinCushionItem : MonoBehaviour {
  [SerializeField] public string m_ToolName;
  [SerializeField] public TMPro.TextMeshPro m_PanelText;

  [SerializeField] private Texture m_Icon;
  [SerializeField] private BaseTool.ToolType m_Tool;
  [SerializeField] private float m_HighlightScale;
  [SerializeField] private float m_MinAngle;
  [SerializeField] private float m_MaxAngle;
  [SerializeField] private Renderer m_ButtonRenderer;
  [SerializeField] private Renderer m_Border;

  private Vector3 m_BaseScale;

  public BaseTool.ToolType Tool {
    get { return m_Tool; }
  }

  public float MinAngle {
    get { return m_MinAngle; }
  }

  public float MaxAngle {
    get { return m_MaxAngle; }
  }

  void Awake() {
    m_BaseScale = transform.localScale;
  }

  void Start() {
    GetComponent<Renderer>().material.mainTexture = m_Icon;
  }

  public void Highlight(bool highlight) {
    if (highlight) {
      var currentColor = PointerManager.m_Instance.MainPointer.GetCurrentColor();
      transform.localScale = m_BaseScale * m_HighlightScale;
      m_ButtonRenderer.material.SetColor("_ActivatedColor", currentColor);
      m_ButtonRenderer.material.SetFloat("_Activated", 0.0f);
      m_Border.material.SetColor("_Color", Color.white);
      m_PanelText.text = m_ToolName;
    } else {
      transform.localScale = m_BaseScale;
      m_ButtonRenderer.material.SetColor("_ActivatedColor", Color.white);
      m_ButtonRenderer.material.SetFloat("_Activated", 1.0f);
      m_Border.material.SetColor("_Color", Color.gray);
    }
  }
}
} // namespace TiltBrush