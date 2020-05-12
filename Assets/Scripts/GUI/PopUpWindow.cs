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

using System;
using UnityEngine;

namespace TiltBrush {

[System.Serializable]
public struct PopUpButton {
  public Texture2D m_ButtonTexture;
  public GameObject m_ButtonPrefab;
  [NonSerialized]
  public BaseButton m_ButtonScript;
}

public class PopUpWindow : MonoBehaviour {
  [SerializeField] protected GameObject m_Background;
  [SerializeField] protected GameObject m_TopBorder;
  [SerializeField] protected GameObject m_BottomBorder;
  [SerializeField] protected TextMesh m_WindowText;
  [SerializeField] protected TextMesh m_WindowSubText;
  [SerializeField] protected float m_CharacterWidth = 0.175f;
  [SerializeField] protected float m_SubtitleCharacterWidth = 0.05625f;
  [SerializeField] protected float m_ButtonWidth = 0.5f;
  [SerializeField] protected Vector3 m_BaseButtonOffset;
  [SerializeField] protected Vector3 m_ReticleBounds;
  [SerializeField] protected float m_PopUpForwardOffset;
  [SerializeField] protected PopUpButton[] m_AutoPlaceButtons;
  [SerializeField] protected float m_TransitionDuration;

  [SerializeField] protected float m_OpenDelay = 0.0f;
  [SerializeField] protected bool m_Persistent = false;
  [SerializeField] protected bool m_AudioOnOpen = true;
  [SerializeField] protected bool m_BlockUndoRedo = false;
  [SerializeField] protected bool m_IsLongPressPopUp;

  [SerializeField] protected NavButton[] m_OrderedPageButtons;
  [SerializeField] protected GameObject m_PrevButton;
  [SerializeField] protected GameObject m_NextButton;

  protected BoxCollider m_WindowCollider;
  protected BasePanel m_ParentPanel;

  protected enum State {
    Uninitialized,
    Opening,
    Standard,
    Closing,
    Closed
  }
  protected State m_CurrentState;
  protected float m_TransitionValue;
  protected bool m_SpoofTransitionValue;
  protected Vector3 m_BaseScale;

  protected int m_PageIndex;
  protected int m_RequestedPageIndex;
  protected int m_NumPages;

  protected UIComponentManager m_UIComponentManager;

  protected enum PageFlipState {
    Standard,
    TransitionOut,
    TransitionIn
  }
  protected PageFlipState m_CurrentPageFlipState;
  protected float m_PageFlipTransitionAmount;
  protected float m_PageFlipSpeed = 24.0f;
  protected float m_BaseIconScale;

  public Action m_OnClose;

  public Collider GetCollider() { return m_WindowCollider; }
  public BasePanel GetParentPanel() { return m_ParentPanel; }
  public void SpoofTransitionValue() { m_SpoofTransitionValue = true; }
  public float GetPopUpForwardOffset() { return m_PopUpForwardOffset; }
  public Vector3 GetReticleBounds() { return m_ReticleBounds; }
  public bool IsLongPressPopUp() { return m_IsLongPressPopUp; }
  public bool BlockUndoRedo() { return m_BlockUndoRedo; }

  void Awake() {
    m_CurrentState = State.Uninitialized;
  }

  void CreateAutoPlaceButtons() {
    if (m_AutoPlaceButtons.Length > 0) {
      float buttonSpacing = m_ButtonWidth * 0.25f;
      float baseButtonLeftBuffer = m_ButtonWidth + buttonSpacing;
      float totalWindowWidth = transform.localScale.x;

      Vector3 vTransformedBase = transform.TransformPoint(m_BaseButtonOffset);
      for (int i = 0; i < m_AutoPlaceButtons.Length; ++i) {
        GameObject rButton = (GameObject)Instantiate(m_AutoPlaceButtons[i].m_ButtonPrefab);

        Vector3 vOffset = transform.right;
        vOffset *= (totalWindowWidth * -0.5f) + baseButtonLeftBuffer +
            ((m_ButtonWidth + buttonSpacing) * (float)i);

        Vector3 vButtonPos = vTransformedBase + vOffset;
        rButton.transform.position = vButtonPos;

        Vector3 vButtonScale = rButton.transform.localScale;
        vButtonScale.Set(m_ButtonWidth, m_ButtonWidth, m_ButtonWidth);
        rButton.transform.localScale = vButtonScale;

        rButton.transform.rotation = transform.rotation;
        rButton.transform.parent = transform;

        Renderer rButtonRenderer = rButton.GetComponent<Renderer>();
        rButtonRenderer.material.mainTexture = m_AutoPlaceButtons[i].m_ButtonTexture;

        BaseButton rButtonScript = rButton.GetComponent<BaseButton>();
        m_AutoPlaceButtons[i].m_ButtonScript = rButtonScript;
        m_AutoPlaceButtons[i].m_ButtonScript.RegisterComponent();
      }
    }
  }

  virtual public void Init(GameObject rParent, string sText) {
    if (m_CurrentState == State.Uninitialized) {
      m_WindowCollider = GetComponent<BoxCollider>();
      if (m_WindowText && sText != null) {
        m_WindowText.text = sText;
      }

      CreateAutoPlaceButtons();
      m_BaseScale = transform.localScale;
    }

    Debug.Assert(rParent != null, "Why is the popup's parent null?");
    m_ParentPanel = rParent.GetComponent<BasePanel>();

    m_UIComponentManager = GetComponent<UIComponentManager>();
    m_UIComponentManager.SetColor(Color.white);

    m_CurrentState = State.Opening;
    m_TransitionValue = -m_OpenDelay;
    UpdateOpening();

    if (m_AudioOnOpen) {
      AudioManager.m_Instance.PlayPopUpSound(transform.position);
    }
  }

  // Set command parameters, for popups that support using command parameters
  virtual public void SetPopupCommandParameters(int commandParam, int commandParam2) {
    // TODO: Fix this hangnail.
    // if (commandParam != -1 || commandParam2 != -1) {
    //   Debug.LogWarning("This popup doesn't support command parameters");
    // }
  }

  virtual public bool RequestClose(bool bForceClose = false) {
    if (bForceClose) {
      m_CurrentState = State.Closing;
      return true;
    } else {
      if (m_Persistent) {
        return false;
      } else {
        m_CurrentState = State.Closing;
        return true;
      }
    }
  }

  public bool InputObjectHasFocus() { return m_UIComponentManager.ActiveInputUIComponent != null; }

  public bool IsOpen() {
    return m_CurrentState == State.Standard;
  }

  public bool IsClosingOrClosed() {
    return m_CurrentState == State.Closing || m_CurrentState == State.Closed;
  }

  public float GetTransitionRatioForVisuals() {
    if (m_SpoofTransitionValue) {
      return 1.0f;
    }
    return GetTransitionRatio();
  }

  float GetTransitionRatio() {
    if (m_TransitionDuration == 0f) {
      return 1f;
    }
    return Mathf.Clamp(m_TransitionValue / m_TransitionDuration, 0.0f, 1.0f);
  }

  public GameObject DuplicateCollider() {
    GameObject go = new GameObject("DisposableCollider");
    go.transform.position = transform.position;
    go.transform.rotation = transform.rotation;
    go.transform.localScale = transform.localScale;
    go.transform.parent = transform.parent;
    BoxCollider goCollider = go.AddComponent<BoxCollider>();
    goCollider.center = m_WindowCollider.center;
    goCollider.size = m_WindowCollider.size;
    return go;
  }

  void Update() {
    BaseUpdate();
    UpdateVisuals();
  }

  virtual protected void BaseUpdate() {
    if (m_CurrentState != State.Standard) {
      switch (m_CurrentState) {
      case State.Opening: UpdateOpening(); break;
      case State.Closing: UpdateClosing(); break;
      }
    }

    switch (m_CurrentPageFlipState) {
    case PageFlipState.Standard:
      //refresh our page if our index changed
      if (m_RequestedPageIndex != m_PageIndex) {
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

  // This function is used to update the animation state of the PopUp's UI components.
  virtual protected void UpdateVisuals() {
    m_UIComponentManager.UpdateVisuals();
  }

  virtual protected void UpdateTransitionOut() {
    m_CurrentPageFlipState = PageFlipState.TransitionIn;
  }

  virtual protected void UpdateTransitionIn() {
    m_CurrentPageFlipState = PageFlipState.Standard;
  }

  protected virtual void UpdateOpening() {
    m_TransitionValue += Time.deltaTime;
    m_TransitionValue = Mathf.Min(m_TransitionValue, m_TransitionDuration);

    float fTransitionRatio = GetTransitionRatio();
    Vector3 vScale = m_BaseScale;
    vScale.x *= fTransitionRatio;
    vScale.y *= fTransitionRatio;
    transform.localScale = vScale;

    //switch to standard state when we're done opening
    if (fTransitionRatio >= 1.0f) {
      m_TransitionValue = m_TransitionDuration;
      m_CurrentState = State.Standard;
      m_SpoofTransitionValue = false;
    }
  }

  protected virtual void UpdateClosing() {
    m_TransitionValue -= Time.deltaTime;

    float fTransitionRatio = GetTransitionRatio();
    Vector3 vScale = m_BaseScale;
    vScale.x *= fTransitionRatio;
    vScale.y *= fTransitionRatio;
    transform.localScale = vScale;

    //quit when we've hit our ratio
    if (m_TransitionValue <= 0.0f) {
      m_CurrentState = State.Closed;
      if (m_OnClose != null) {
        m_OnClose();
        m_OnClose = null;
      }

      DestroyPopUpWindow();
    }
  }

  virtual protected void DestroyPopUpWindow() {
    m_ParentPanel.InvalidateIfActivePopup(this);
    Destroy(gameObject);
  }

  // This function is used to update the behavior state of the PopUp's components in response to
  // an updated input "gaze" direction from the user.
  virtual public void UpdateUIComponents(Ray rCastRay, bool inputValid, Collider parentCollider) {
    m_UIComponentManager.UpdateUIComponents(rCastRay, inputValid, parentCollider);
  }

  virtual public void CalculateReticleCollision(Ray castRay, ref Vector3 pos, ref Vector3 forward) {
    RaycastHit hitInfo;
    // Take a step back since the ray's origin could be on the surface of the popup.
    castRay.origin -= castRay.direction;
    if (BasePanel.DoesRayHitCollider(castRay, GetCollider(), out hitInfo)) {
      pos = hitInfo.point;
    }

    m_UIComponentManager.CalculateReticleCollision(castRay, ref pos, ref forward);
  }

  public void GotoPage(int iIndex) {
    m_RequestedPageIndex = Mathf.Clamp(iIndex, 0, m_NumPages - 1);
  }

  public void AdvancePage(int iAmount) {
    m_RequestedPageIndex = Mathf.Clamp(m_PageIndex + iAmount, 0, m_NumPages - 1);
  }
}
}  // namespace TiltBrush
