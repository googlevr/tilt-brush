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

public abstract class PagingPopUpWindow : PopUpWindow {
  public class ImageIcon {
    public GameObject m_Icon;
    public BaseButton m_IconScript;
    public bool m_TextureAssigned;
    public bool m_Valid;
  }

  [SerializeField] private TextMesh m_NoDataText;
  [SerializeField] protected GameObject m_ButtonPrefab;
  [SerializeField] protected Texture2D m_UnknownImageTexture;
  [SerializeField] protected float m_IconSpacing;
  [SerializeField] protected int m_IconGridWidthFullPage = 1;
  [SerializeField] protected int m_IconGridHeightFullPage = 1;
  [SerializeField] protected int m_IconCountNavPage;
  protected ImageIcon[] m_Icons;
  protected int m_IconCountFullPage;
  protected int m_BaseIndex;

  protected abstract int m_DataCount { get; }
  protected virtual void InitIcon(ImageIcon icon) { }
  protected abstract void RefreshIcon(ImageIcon icon, int iCatalog);

  override public void Init(GameObject rParent, string sText) {
    if (m_CurrentState == State.Uninitialized) {
      //create buttons for worst case
      m_IconCountFullPage = m_IconGridHeightFullPage * m_IconGridWidthFullPage;
      m_Icons = new ImageIcon[m_IconCountFullPage];

      var xfWithUniformScale = TrTransform.FromTransform(transform);
      xfWithUniformScale.scale = transform.localScale.x;
      Vector3 vBaseIconPosition = xfWithUniformScale * m_BaseButtonOffset;

      for (int y = 0; y < m_IconGridHeightFullPage; ++y) {
        for (int x = 0; x < m_IconGridWidthFullPage; ++x) {
          int iIndex = (m_IconGridWidthFullPage * y) + x;
          m_Icons[iIndex] = new ImageIcon();

          m_Icons[iIndex].m_Icon = Instantiate(m_ButtonPrefab);
          m_Icons[iIndex].m_IconScript = m_Icons[iIndex].m_Icon.GetComponent<BaseButton>();
          InitIcon(m_Icons[iIndex]);

          //scale icon for pop-up, and then late-init it so the scale is cached correctly
          Vector3 vIconScale = m_Icons[iIndex].m_Icon.transform.localScale;
          vIconScale.Set(m_ButtonWidth, m_ButtonWidth, m_ButtonWidth);
          vIconScale *= transform.localScale.x;
          m_Icons[iIndex].m_Icon.transform.localScale = vIconScale;
          m_BaseIconScale = vIconScale.x;

          Vector3 vPos = vBaseIconPosition;
          vPos += (transform.right * (float)x * m_IconSpacing * transform.localScale.x);
          vPos += (-transform.up * (float)y * m_IconSpacing * transform.localScale.x);
          m_Icons[iIndex].m_Icon.transform.position = vPos;
          m_Icons[iIndex].m_Icon.transform.rotation = transform.rotation;
          m_Icons[iIndex].m_Icon.transform.parent = transform;
          // Register after parent has been assisgned.
          m_Icons[iIndex].m_IconScript.RegisterComponent();
        }
      }

      //default to first page highlighted
      m_PageIndex = 0;
      m_BaseIndex = 0;
      if (m_DataCount <= m_IconCountFullPage) {
        m_NumPages = 1;
      } else {
        m_NumPages = ((m_DataCount - 1) / m_IconCountNavPage) + 1;
      }
    }

    //base modifies scale, so we want to do this after we create our icons
    base.Init(rParent, sText);

    RefreshPage();
  }

  override protected void UpdateTransitionOut() {
    float fTransitionStep = m_PageFlipSpeed * Time.deltaTime;
    m_PageFlipTransitionAmount += fTransitionStep;
    if (m_PageFlipTransitionAmount >= 1.0f) {
      //if we're done transitioning out, flip the switch to change the textures
      m_PageFlipTransitionAmount = 0.0f;
      UpdateIconTransitionScale(0.0f);
      m_CurrentPageFlipState = PageFlipState.TransitionIn;
      m_PageIndex = m_RequestedPageIndex;
      m_BaseIndex = m_PageIndex * m_IconCountNavPage;
      RefreshPage();
    } else {
      //update icon scales
      UpdateIconTransitionScale(1.0f - m_PageFlipTransitionAmount);
    }
  }

  override protected void UpdateTransitionIn() {
    float fTransitionStep = m_PageFlipSpeed * Time.deltaTime;
    m_PageFlipTransitionAmount += fTransitionStep;
    if (m_PageFlipTransitionAmount >= 1.0f) {
      m_PageFlipTransitionAmount = 0.0f;
      UpdateIconTransitionScale(1.0f);
      m_CurrentPageFlipState = PageFlipState.Standard;
    } else {
      //update icon scales
      UpdateIconTransitionScale(m_PageFlipTransitionAmount);
    }
  }

  void UpdateIconTransitionScale(float fScale) {
    for (int i = 0; i < m_IconCountFullPage; ++i) {
      Vector3 vScale = m_Icons[i].m_Icon.transform.localScale;
      vScale.x = m_BaseIconScale * fScale;
      m_Icons[i].m_Icon.transform.localScale = vScale;
    }
  }

  protected void RefreshPage() {
    //if we can fit all the icons on one page, turn off the nav buttons and do that
    if (m_DataCount <= m_IconCountFullPage) {
      //init icons
      for (int i = 0; i < m_Icons.Length; ++i) {
        if (i < m_DataCount) {
          RefreshIcon(m_Icons[i], i);
          m_Icons[i].m_Icon.SetActive(true);
        } else {
          m_Icons[i].m_Icon.SetActive(false);
        }
      }

      //turn off nav buttons
      for (int i = 0; i < m_OrderedPageButtons.Length; ++i) {
        m_OrderedPageButtons[i].gameObject.SetActive(false);
      }

      m_PrevButton.SetActive(false);
      m_NextButton.SetActive(false);
    } else {
      //disable extra icons
      for (int i = 0; i < m_IconCountNavPage; ++i) {
        int iIndex = m_BaseIndex + i;

        //show icon according to availability
        if (iIndex < m_DataCount) {
          RefreshIcon(m_Icons[i], iIndex);
          m_Icons[i].m_Icon.SetActive(true);
        } else {
          m_Icons[i].m_Icon.SetActive(false);
        }
      }

      for (int i = m_IconCountNavPage; i < m_IconCountFullPage; ++i) {
        m_Icons[i].m_Icon.SetActive(false);
      }

      //refresh page buttons
      int iNumPageButtons = m_OrderedPageButtons.Length;
      int iHalfNumPageButtons = iNumPageButtons / 2;
      int iUnusedPageButtons = iNumPageButtons - m_NumPages;
      int iNumPagesMinusOne = m_NumPages - 1;

      //if we've got more buttons than pages, order the numbers correctly
      if (iUnusedPageButtons <= 0) {
        int iMinIndex = m_PageIndex - iHalfNumPageButtons;
        int iMaxIndex = m_PageIndex + iHalfNumPageButtons;
        if (iMinIndex < 0) {
          iMaxIndex += -iMinIndex;
          iMinIndex += -iMinIndex;
        } else if (iMaxIndex > iNumPagesMinusOne) {
          iMinIndex -= (iMaxIndex - iNumPagesMinusOne);
          iMaxIndex -= (iMaxIndex - iNumPagesMinusOne);
        }

        for (int i = 0; i < iNumPageButtons; ++i) {
          int iPageIndex = iMinIndex + i;
          m_OrderedPageButtons[i].gameObject.SetActive(true);
          m_OrderedPageButtons[i].SetGotoPage(iPageIndex);
          m_OrderedPageButtons[i].SetButtonAvailable(iPageIndex != m_PageIndex);
          m_OrderedPageButtons[i].SetButtonSelected(iPageIndex == m_PageIndex);
        }
      } else {
        //custom setup for no pages
        int iHalfUnusedButtons = iUnusedPageButtons / 2;
        for (int i = 0; i < iNumPageButtons; ++i) {
          int iPageIndex = i - iHalfUnusedButtons;
          if (iPageIndex >= 0 && iPageIndex < m_NumPages) {
            m_OrderedPageButtons[i].gameObject.SetActive(true);
            m_OrderedPageButtons[i].SetGotoPage(iPageIndex);
            m_OrderedPageButtons[i].SetButtonAvailable(iPageIndex != m_PageIndex);
            m_OrderedPageButtons[i].SetButtonSelected(iPageIndex == m_PageIndex);
          } else {
            //turn off buttons that aren't within bounds
            m_OrderedPageButtons[i].gameObject.SetActive(false);
          }
        }
      }

      m_PrevButton.SetActive(m_PageIndex > 0);
      m_NextButton.SetActive(m_PageIndex < iNumPagesMinusOne);
    }

    if (m_NoDataText != null) {
      m_NoDataText.gameObject.SetActive(
         m_DataCount <= 0);
    }
  }

  override protected void UpdateVisuals() {
    base.UpdateVisuals();

    for (int i = 0; i < m_Icons.Length; ++i) {
      m_Icons[i].m_IconScript.UpdateVisuals();
    }
  }

  override public void UpdateUIComponents(Ray rCastRay, bool inputValid, Collider parentCollider) {
    bool bButtonsAvailable = m_CurrentPageFlipState == PageFlipState.Standard;

    //update sketch icons
    for (int i = 0; i < m_Icons.Length; ++i) {
      bool bThisIconActive = false;
      ImageIcon rIcon = m_Icons[i];

      if (bButtonsAvailable && rIcon.m_Valid &&
          BasePanel.DoesRayHitCollider(rCastRay, rIcon.m_IconScript.GetCollider())) {
        bool bWasButtonPressed = rIcon.m_IconScript.IsPressed();
        rIcon.m_IconScript.UpdateButtonState(inputValid);
        if (rIcon.m_IconScript.IsPressed() && !bWasButtonPressed) {
          //on press, refresh the buttons
          RefreshPage();
        }

        bThisIconActive = true;
      }

      if (!bThisIconActive) {
        //reset state of button because we're not messing with it
        rIcon.m_IconScript.ResetState();
      }
    }

    base.UpdateUIComponents(rCastRay, inputValid, parentCollider);
  }

  override public void CalculateReticleCollision(Ray castRay, ref Vector3 pos, ref Vector3 forward) {
    RaycastHit rHitInfo;
    for (int i = 0; i < m_Icons.Length; ++i) {
      if (BasePanel.DoesRayHitCollider(castRay, m_Icons[i].m_IconScript.GetCollider(), out rHitInfo)) {
        pos = rHitInfo.point;
        return;
      }
    }

    base.CalculateReticleCollision(castRay, ref pos, ref forward);
  }

}
}  // namespace TiltBrush
