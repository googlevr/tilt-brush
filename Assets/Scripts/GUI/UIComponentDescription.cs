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

public class UIComponentDescription : MonoBehaviour {
  [SerializeField] private Transform m_BG;
  [SerializeField] private GameObject m_RightCap;
  [SerializeField] private Renderer[] m_TintVisuals;
  [SerializeField] private TextMesh[] m_Text;
  [SerializeField] private Color m_UnavailableColor;
  [Tooltip ("Position the origin of the description on the right edge " +
      "of UI component rather than at it's center.")]
  [SerializeField] private bool m_PlaceOnRightEdge;
  [SerializeField] private float m_DefaultScale = .4235f;

  public float YOffset { set { m_YOffset = value; } }
  public int NumberOfLines => m_Text.Length;

  private Color m_StandardColor = Color.white;
  private float m_YOffset = 0;

  public void SetDescription(string[] strings) {
    // Measure length of description by getting render bounds when mesh is axis-aligned.
    float fTextWidth = 0;
    for (int i = 0; i < m_Text.Length; i++) {
      if (i >= strings.Length) {
        m_Text[i].text = null;
        continue;
      }
      string s = strings[i];
      TextMesh textMesh = m_Text[i];
      textMesh.text = s;
      fTextWidth = Mathf.Max(fTextWidth,
          TextMeasureScript.m_Instance.GetTextWidth(
              textMesh.characterSize, textMesh.fontSize, textMesh.font, ("  " + s)));
    }

    if (m_PlaceOnRightEdge) {
      // Buttons are all (1,1) shapes that are scaled up or down,
      // so the right edge is always at Vector3.right * .5f
      transform.localPosition = Vector3.right * .5f;
      transform.localPosition = new Vector3(.5f, m_YOffset, 0f);
    }

    Vector3 vBGScale = m_BG.localScale;
    vBGScale.x = fTextWidth;
    m_BG.localScale = vBGScale;

    if (m_RightCap) {
      m_RightCap.transform.localPosition = Vector3.right * fTextWidth;
    }

    AdjustDescriptionScale();
  }

  public void AdjustDescriptionScale() {
    var lossyScale = transform.parent.lossyScale;
    transform.localScale = new Vector3(
        lossyScale.x == 0 ? 1 : m_DefaultScale / lossyScale.x,
        lossyScale.y == 0 ? 1 : m_DefaultScale / lossyScale.y,
        transform.localScale.z);
  }

  public void SetAvailabilityVisuals(bool available) {
    Color col = available ? m_StandardColor : m_UnavailableColor;
    for (int i = 0; i < m_TintVisuals.Length; ++i) {
      m_TintVisuals[i].material.color = col;
    }
  }
}
}  // namespace TiltBrush
