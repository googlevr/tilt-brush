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
public class CustomColorButton : BaseButton {
  [SerializeField] private Texture2D m_ColorTexture;
  [SerializeField] private bool m_Trash;
  private ColorController m_ColorController;
  private int m_StorageIndex;
  private Color m_CustomColor;

  public enum State {
    ReadyForAdd,
    Set,
    Off
  }
  private State m_CurrentState;

  public bool IsReadyForAdd() { return m_CurrentState == State.ReadyForAdd; }
  public bool IsOff() { return m_CurrentState == State.Off; }
  public bool IsSet() { return m_CurrentState == State.Set; }

  override protected void Start() {
    base.Start();

    // Default to brush color controller.
    m_ColorController = App.BrushColor;

    // Walk upward and find the first ColorController relevant to us.
    for (var manager = m_Manager; manager != null; manager = manager.ParentManager) {
      ColorController controller = manager.GetComponent<ColorController>();
      if (controller != null) {
        m_ColorController = controller;
        break;
      }
    }
  }

  public void SetStorageIndex(int index) {
    m_StorageIndex = index;
  }

  public Color CustomColor {
    get { return m_CustomColor; }
    set {
      m_CustomColor = value;
      CustomColorPaletteStorage.m_Instance.SetColor(m_StorageIndex, m_CustomColor);
      SetState(State.Set);
    }
  }

  public void ClearCustomColor() {
    CustomColorPaletteStorage.m_Instance.ClearColor(m_StorageIndex);
  }

  public void SetState(State desiredState) {
    switch (desiredState) {
    case State.Off: gameObject.SetActive(false); break;
    case State.ReadyForAdd:
      gameObject.SetActive(true);
      if (!m_AtlasTexture) {
        m_ButtonRenderer.material.SetColor("_Tint", Color.white);
        m_ButtonRenderer.material.mainTexture = m_ButtonTexture;
      }
      break;
    case State.Set:
      gameObject.SetActive(true);
      if (!m_AtlasTexture) {
        m_ButtonRenderer.material.SetColor("_Tint", m_CustomColor);
        m_ButtonRenderer.material.mainTexture = m_ColorTexture;
      }
      break;
    }

    m_CurrentState = desiredState;
  }

  override public void HasFocus(RaycastHit hitInfo) {
    base.HasFocus(hitInfo);
    if (!m_Trash && m_CurrentState == State.Set) {
      m_Manager.GetComponent<CustomColorPalette>().PlaceTrash(
          transform.localPosition, m_StorageIndex);
    }
  }

  override protected void OnButtonPressed() {
    if (m_Trash) {
      // If we're a trash button, clear our storage index and notify our lord.
      CustomColorPaletteStorage.m_Instance.ClearColor(m_StorageIndex);
    } else {
      if (m_CurrentState == State.Set) {
        m_ColorController.CurrentColor = m_CustomColor;
        m_Manager.GetComponent<CustomColorPalette>().TriggerColorPicked(m_CustomColor);
      } else if (m_CurrentState == State.ReadyForAdd) {
        Color color = m_ColorController.IsHdr ?
            ColorPickerUtils.ClampColorIntensityToLdr(m_ColorController.CurrentColor) :
            m_ColorController.CurrentColor;
        CustomColorPaletteStorage.m_Instance.SetColor(m_StorageIndex, color, true);
      }
    }

    CustomColorPalette palette = m_Manager.GetComponent<CustomColorPalette>();
    if (palette == null) {
      Debug.LogWarning("CustomColorButton needs a CustomColorPalette manager.");
    }
    palette.RefreshPaletteButtons();
  }
}
} // namespace TiltBrush
