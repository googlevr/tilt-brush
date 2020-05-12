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
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush {
public abstract class ModalPanel : BasePanel {
  [SerializeField] protected GameObject m_PrevButton;
  [SerializeField] protected GameObject m_NextButton;

  protected NavButton[] m_NavigationButtons;
  protected ModeButton[] m_ModeButtons;

  protected enum PageFlipState {
    Standard,
    TransitionOut,
    TransitionIn
  }
  protected PageFlipState m_CurrentPageFlipState;
  private float m_PageFlipTransitionAmount;
  private float m_PageFlipSpeed = 24.0f;

  private List<BaseButton> m_Icons;

  private int m_PageIndex;
  protected int m_RequestedPageIndex;
  protected int m_NumPages;
  protected int m_IndexOffset;

  protected virtual int PageIndex {
    get { return m_PageIndex; }
    set { m_PageIndex = value;}
  }

  protected virtual List<BaseButton> Icons {
    get { return m_Icons; }
  }

  public abstract bool IsInButtonMode(ModeButton button);

  void Start() {
    // Find all navigation buttons.
    m_NavigationButtons = m_Mesh.GetComponentsInChildren<NavButton>()
        .Where(b => b.m_ButtonType == UIComponent.ComponentMessage.GotoPage).ToArray();
    m_ModeButtons = m_Mesh.GetComponentsInChildren<ModeButton>();

    PageIndex = 0;
    m_Icons = new List<BaseButton>();

    OnStart();
  }

  protected virtual void OnStart() { }

  protected virtual void RefreshPage() {
    if (m_NavigationButtons == null) { return; }

    // Refresh page buttons
    int iNumPageButtons = m_NavigationButtons.Length;
    int iHalfNumPageButtons = iNumPageButtons / 2;
    int iUnusedPageButtons = iNumPageButtons - m_NumPages;
    int iNumPagesMinusOne = m_NumPages - 1;

    // If we've got more pages than buttons, order the numbers correctly
    if (iUnusedPageButtons <= 0) {
      int iMinIndex = PageIndex - iHalfNumPageButtons;
      int iMaxIndex = PageIndex + iHalfNumPageButtons;
      if (iMinIndex < 0) {
        iMaxIndex += -iMinIndex;
        iMinIndex += -iMinIndex;
      } else if (iMaxIndex > iNumPagesMinusOne) {
        iMinIndex -= (iMaxIndex - iNumPagesMinusOne);
        iMaxIndex -= (iMaxIndex - iNumPagesMinusOne);
      }

      for (int i = 0; i < iNumPageButtons; ++i) {
        int iPageIndex = iMinIndex + i;
        m_NavigationButtons[i].gameObject.SetActive(true);
        m_NavigationButtons[i].SetGotoPage(iPageIndex);
        m_NavigationButtons[i].SetButtonAvailable(iPageIndex != PageIndex);
        m_NavigationButtons[i].SetButtonSelected(iPageIndex == PageIndex);
      }
    } else {
      // Custom setup for fewer pages than buttons
      if (m_NumPages == 1) {
        // Turn off all the buttons if we only have one page
        for (int i = 0; i < iNumPageButtons; ++i) {
          m_NavigationButtons[i].gameObject.SetActive(false);
        }
      } else {
        int iHalfUnusedButtons = iUnusedPageButtons / 2;
        for (int i = 0; i < iNumPageButtons; ++i) {
          int iPageIndex = i - iHalfUnusedButtons;
          if (iPageIndex >= 0 && iPageIndex < m_NumPages) {
            m_NavigationButtons[i].gameObject.SetActive(true);
            m_NavigationButtons[i].SetGotoPage(iPageIndex);
            m_NavigationButtons[i].SetButtonAvailable(iPageIndex != PageIndex);
            m_NavigationButtons[i].SetButtonSelected(iPageIndex == PageIndex);
          } else {
            // Turn off buttons that aren't within bounds
            m_NavigationButtons[i].gameObject.SetActive(false);
          }
        }
      }
    }

    m_PrevButton.SetActive(PageIndex > 0);
    m_NextButton.SetActive(PageIndex < iNumPagesMinusOne);
  }

  protected void PageFlipUpdate() {
    switch (m_CurrentPageFlipState) {
    case PageFlipState.Standard:
      // Refresh our page if our index changed
      if (m_RequestedPageIndex != PageIndex) {
        m_PageFlipTransitionAmount = 0.0f;
        m_CurrentPageFlipState = PageFlipState.TransitionOut;
      }
      break;
    case PageFlipState.TransitionOut:
      UpdateTransitionOut();
      break;
    case PageFlipState.TransitionIn:
      UpdateTransitionIn();
      break;
    }
  }

  protected virtual void UpdateIndexOffset() {
    m_IndexOffset = PageIndex * Icons.Count;
  }

  void UpdateTransitionOut() {
    float fTransitionStep = m_PageFlipSpeed * Time.deltaTime;
    m_PageFlipTransitionAmount += fTransitionStep;
    if (m_PageFlipTransitionAmount >= 1.0f) {
      // If we're done transitioning out, flip the switch to change the textures
      m_PageFlipTransitionAmount = 0.0f;
      UpdateButtonTransitionScale(0.0f);
      m_CurrentPageFlipState = PageFlipState.TransitionIn;
      PageIndex = m_RequestedPageIndex;
      UpdateIndexOffset();
      RefreshPage();
    } else {
      // Update button scales
      UpdateButtonTransitionScale(1.0f - m_PageFlipTransitionAmount);
    }
  }

  void UpdateTransitionIn() {
    float fTransitionStep = m_PageFlipSpeed * Time.deltaTime;
    m_PageFlipTransitionAmount += fTransitionStep;
    if (m_PageFlipTransitionAmount >= 1.0f) {
      m_PageFlipTransitionAmount = 0.0f;
      UpdateButtonTransitionScale(1.0f);
      m_CurrentPageFlipState = PageFlipState.Standard;
    } else {
      // Update button scales
      UpdateButtonTransitionScale(m_PageFlipTransitionAmount);
    }
  }

  protected virtual void UpdateButtonTransitionScale(float fScale) {
    foreach (BaseButton icon in Icons) {
      Transform t = icon.gameObject.transform;
      Vector3 vScale = t.localScale;
      vScale.x = icon.ScaleBase.y * fScale;
      t.localScale = vScale;
    }
  }

  override public void GotoPage(int iIndex) {
    m_RequestedPageIndex = Mathf.Clamp(iIndex, 0, m_NumPages - 1);
  }

  override public void AdvancePage(int iAmount) {
    m_RequestedPageIndex = Mathf.Clamp(PageIndex + iAmount, 0, m_NumPages - 1);
  }

  protected void ResetPageIndex() {
    PageIndex = 0;
    m_RequestedPageIndex = PageIndex;
    m_IndexOffset = 0;
  }
}
} // namespace TiltBrush
