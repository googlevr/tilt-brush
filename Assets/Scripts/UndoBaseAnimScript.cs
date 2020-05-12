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

public class UndoBaseAnimScript : MonoBehaviour {
  protected enum HideState {
    Hiding,
    Hidden
  }
  protected HideState m_CurrentHideState;
  protected float m_HiddenAmount;
  public float m_HideSpeed = 8.0f;
  public bool m_DestroyOnHide = true;
  protected bool m_TargetMainPointer;

  protected void OnAwake() {
    //cache renderer and disable ourselves
    m_CurrentHideState = HideState.Hidden;
    gameObject.SetActive(false);
  }

  protected void InitForHiding() {
    //enable and prep for exit
    m_HiddenAmount = 0.0f;
    m_CurrentHideState = HideState.Hiding;
    gameObject.SetActive(true);

    // TODO: See if we still need m_TargetMainPointer.
    //
    // Originally, doing an undo while pointing at a panel (presumably when you press the Undo
    // button on the tools panel) would cause the stroke to disappear into the stroke's end
    // point. But now that we allow the undo / redo shortcuts to work even when focused on a panel,
    // it seemed more logical to always disappear to the pointer position.
    //m_TargetMainPointer = !SketchControlsScript.m_Instance.IsUserInteractingWithUI();
    m_TargetMainPointer = true;
  }

  void Update() {
    if (m_CurrentHideState == HideState.Hiding) {
      m_HiddenAmount += (Time.deltaTime * m_HideSpeed);
      if (m_HiddenAmount >= 1.0f) {
        m_HiddenAmount = 1.0f;
        gameObject.SetActive(false);
        m_CurrentHideState = HideState.Hidden;
        if (m_DestroyOnHide) {
          Destroy(gameObject);
        }
      }
      AnimateHiding();
    }
  }

  virtual protected void AnimateHiding() {
  }

  protected Vector3 GetAnimationTarget_CS() {
    return Coords.AsCanvas[InputManager.m_Instance.GetBrushControllerAttachPoint()].translation;
  }
}
}  // namespace TiltBrush
