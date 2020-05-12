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

public class OptionsPopUpWindow : PopUpWindow {
  [SerializeField] private float m_ColorTransitionDuration;
  private float m_ColorTransitionValue;
  private Material m_ColorBackground;

  override public void Init(GameObject rParent, string sText) {
    m_ColorBackground = m_Background.GetComponent<MeshRenderer>().material;
    base.Init(rParent, sText);
  }

  override protected void BaseUpdate() {
    base.BaseUpdate();

    m_UIComponentManager.SetColor(Color.white);

    // TODO: Make linear into smooth step!
    if (m_ColorBackground &&
        m_TransitionValue == m_TransitionDuration &&
        m_ColorTransitionValue < m_ColorTransitionDuration) {
      m_ColorTransitionValue += Time.deltaTime;
      if (m_ColorTransitionValue > m_ColorTransitionDuration) {
        m_ColorTransitionValue = m_ColorTransitionDuration;
      }
      float greyVal = 1 - m_ColorTransitionValue / m_ColorTransitionDuration;
      m_ColorBackground.color = new Color(greyVal, greyVal, greyVal);
    }
  }

  protected override void UpdateOpening() {
    if (m_ColorBackground && m_TransitionValue == 0) {
      m_ColorBackground.color = Color.white;
    }
    base.UpdateOpening();
  }

  protected override void UpdateClosing() {
    if (m_ColorBackground) {
      float greyVal = 1 - m_TransitionValue / m_TransitionDuration;
      m_ColorBackground.color = new Color(greyVal, greyVal, greyVal);
    }
    base.UpdateClosing();
  }

  override public void UpdateUIComponents(Ray rCastRay, bool inputValid, Collider parentCollider) {
    if (m_IsLongPressPopUp) {
      // Don't bother updating the popup if we're a long press and we're closing.
      if (m_CurrentState == State.Closing) {
        return;
      }
      // If this is a long press popup and we're done holding the button down, get out.
      if (m_CurrentState == State.Standard && !inputValid) {
        RequestClose();
      }
    }

    base.UpdateUIComponents(rCastRay, inputValid, parentCollider);
  }
}
}  // namespace TiltBrush
