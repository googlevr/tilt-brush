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

public class TutorialPanel : BasePanel {
  [SerializeField] private float m_TipTransitionSpeed = 8.0f;
  [SerializeField] private GameObject m_Title;
  [SerializeField] private TextMeshPro m_TipNumber;

  private enum TransitionState {
    Out,
    In,
    None
  }
  private TransitionState m_TipTransitionState;
  private float m_TipTransitionValue;
  private GameObject m_TutorialObject;
  private TutorialType m_CurrentTutorialType;
  private TutorialType m_DesiredTutorialType;

  override public void InitPanel() {
    base.InitPanel();
    ShowTutorialOfType((TutorialType)0);
  }

  override public void OnWidgetShowAnimStart() {
    m_Title.SetActive(false);
  }

  override public void OnWidgetShowAnimComplete() {
    m_Title.SetActive(true);
  }

  override protected void OnEnablePanel() {
    base.OnEnablePanel();
    m_TipTransitionState = TransitionState.In;
  }

  void Update() {
    BaseUpdate();

    switch (m_TipTransitionState) {
    case TransitionState.Out:
      // Shrink our current tutorial until 0, then spawn the desired next.
      m_TipTransitionValue -= Time.deltaTime * m_TipTransitionSpeed;
      if (m_TipTransitionValue <= 0.0f) {
        SetTutorial(m_DesiredTutorialType);
        m_TipTransitionValue = 0.0f;
        m_TipTransitionState = TransitionState.In;
      } else {
        m_TutorialObject.transform.localScale = Vector3.one * m_TipTransitionValue;
      }
      break;
    case TransitionState.In:
      // Grow our current tutorial until 1.
      m_TipTransitionValue += Time.deltaTime * m_TipTransitionSpeed;
      if (m_TipTransitionValue >= 1.0f) {
        m_TipTransitionValue = 1.0f;
        m_TipTransitionState = TransitionState.None;
      }

      if (m_TutorialObject) {
        m_TutorialObject.transform.localScale = Vector3.one * m_TipTransitionValue;
      }
      break;
    }
  }

  void SetTutorial(TutorialType type) {
    // Clean up old tutorial.
    Destroy(m_TutorialObject);
    m_TutorialObject = null;

    // Find the appropriate prefab.
    GameObject prefab = TutorialManager.m_Instance.GetTutorialPrefab(type);
    if (prefab) {
      m_TutorialObject = (GameObject)Instantiate(prefab, transform.position, transform.rotation);
      m_TutorialObject.transform.parent = transform;
      m_TutorialObject.transform.localScale = Vector3.zero;
    } else {
      // Nothing mapped.  Jump straight to standard.
      m_TipTransitionState = TransitionState.None;
    }

    m_CurrentTutorialType = m_DesiredTutorialType;

    int iNumTypes = (int)TutorialType.Num;
    int iCurrentType = (int)m_CurrentTutorialType + 1;
    m_TipNumber.text = iCurrentType.ToString() + "  of  " + iNumTypes.ToString();
  }

  void ShowTutorialOfType(TutorialType type) {
    m_DesiredTutorialType = type;

    // Fade out any active tutorial.
    if (m_TutorialObject) {
      m_TipTransitionState = TransitionState.Out;
    } else {
      // No active tutorial?  Just straight to showing this one.
      SetTutorial(m_DesiredTutorialType);
      m_TipTransitionState = TransitionState.In;
    }
  }

  override public void AdvancePage(int iAmount) {
    int iCurrent = (int)m_CurrentTutorialType;
    int iNum = (int)TutorialType.Num;
    iCurrent += iAmount;
    if (iCurrent < 0) {
      iCurrent += iNum;
    }
    if (iCurrent >= iNum) {
      iCurrent -= iNum;
    }
    ShowTutorialOfType((TutorialType)iCurrent);
  }
}

} // namespace TiltBrush