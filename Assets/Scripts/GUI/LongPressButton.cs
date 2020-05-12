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
public class LongPressButton : OptionButton {
  [SerializeField] private float m_LongPressDuration = 0.3f;
  private float m_PressTimer;

  override protected void Awake() {
    base.Awake();
    m_HoldFocus = false;
  }

  void OnLongPress() {
    if (m_Manager) {
      BasePanel panel = m_Manager.GetPanelForPopUps();
      panel.CreatePopUp(m_Command, m_CommandParam, -1, m_PopupText);
      panel.PositionPopUp(transform.position +
          transform.forward * panel.PopUpOffset +
          panel.transform.TransformVector(m_PopupOffset));
      ResetState();
    }
  }

  override public void ButtonPressed(RaycastHit rHitInfo) {
    AdjustButtonPositionAndScale(m_ZAdjustClick, m_HoverScale, m_HoverBoxColliderGrow);
    m_CurrentButtonState = ButtonState.Held;
    m_PressTimer = 0;
  }

  override public void ButtonHeld(RaycastHit rHitInfo) {
    if (m_CurrentButtonState == ButtonState.Held) {
      // If we've held for the required amount of time, trigger long press.
      m_PressTimer += Time.deltaTime;
      if (m_PressTimer > m_LongPressDuration) {
        m_PressTimer = 0;
        OnLongPress();
        m_CurrentButtonState = ButtonState.Untouched;
      }
    }
  }

  override public void ButtonReleased() {
    if (m_CurrentButtonState == ButtonState.Held) {
      if (IsAvailable()) {
        OnButtonPressed();

        if (m_ButtonHasPressedAudio) {
          AudioManager.m_Instance.ItemSelect(transform.position);
        }

        m_CurrentButtonState = ButtonState.Pressed;
      } else {
        m_CurrentButtonState = ButtonState.Untouched;
      }
    } else if (m_CurrentButtonState != ButtonState.Untouched && IsAvailable()) {
      m_CurrentButtonState = ButtonState.Untouched;
    }
    ResetScale();
  }

  override public void GainFocus() {
    AdjustButtonPositionAndScale(m_ZAdjustHover, m_HoverScale, m_HoverBoxColliderGrow);

    if (m_CurrentButtonState != ButtonState.Pressed) {
      AudioManager.m_Instance.ItemHover(transform.position);
    }

    m_CurrentButtonState = ButtonState.Hover;
    SetDescriptionActive(true);
    m_PressTimer = 0;
  }
}
}  // namespace TiltBrush
