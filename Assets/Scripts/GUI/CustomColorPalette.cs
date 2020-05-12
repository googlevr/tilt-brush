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

public class CustomColorPalette : UIComponent {
  public event Action<Color> ColorPicked;

  [SerializeField] private CustomColorButton[] m_Buttons;
  [SerializeField] private CustomColorButton m_TrashButton;
  [SerializeField] private Vector3 m_TrashButtonPlacementOffset;

  private float m_TrashButtonLocalZ;

  private UIComponentManager m_UIComponentManager;

  override protected void Awake() {
    base.Awake();

    m_UIComponentManager = GetComponent<UIComponentManager>();

    for (int i = 0; i < m_Buttons.Length; ++i) {
      m_Buttons[i].SetStorageIndex(i);
    }
    m_TrashButtonLocalZ = m_TrashButton.transform.localPosition.z;
  }

  override public void SetColor(Color color) {
    base.SetColor(color);
    for (int i = 0; i < m_Buttons.Length; ++i) {
      m_Buttons[i].SetColor(color);
    }
    m_TrashButton.SetColor(color);
  }

  override protected void Start() {
    base.Start();
    RefreshPaletteButtons();
    CustomColorPaletteStorage.m_Instance.StoredColorsChanged += RefreshPaletteButtons;
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    CustomColorPaletteStorage.m_Instance.StoredColorsChanged -= RefreshPaletteButtons;
  }

  override public void HasFocus(RaycastHit hitInfo) {
    // If the trash is active and we're hovering over it, don't hide it.
    Ray hoverRay = new Ray(hitInfo.point - transform.forward, transform.forward);
    if (m_TrashButton.gameObject.activeSelf &&
        !BasePanel.DoesRayHitCollider(hoverRay, m_TrashButton.GetCollider())) {
      HideTrash();
    }
  }

  public void PlaceTrash(Vector3 parentPos_LS, int storageIndex) {
    m_TrashButton.gameObject.SetActive(true);
    Vector3 vTrashPos = parentPos_LS;
    vTrashPos += m_TrashButtonPlacementOffset;
    vTrashPos.z = m_TrashButtonLocalZ;
    m_TrashButton.transform.localPosition = vTrashPos;
    m_TrashButton.SetStorageIndex(storageIndex);
  }

  void HideTrash() {
    m_TrashButton.gameObject.SetActive(false);
  }

  public void RefreshPaletteButtons() {
    CustomColorPaletteStorage.m_Instance.RefreshStoredColors();

    // Set all colors.
    int iNumColors = CustomColorPaletteStorage.m_Instance.GetNumValidColors();
    for (int i = 0; i < iNumColors && i < m_Buttons.Length; ++i) {
      m_Buttons[i].CustomColor = CustomColorPaletteStorage.m_Instance.GetColor(i);
    }

    // Ensure that we only have one ready button and it's the last.
    if (iNumColors < m_Buttons.Length) {
      m_Buttons[iNumColors].SetState(CustomColorButton.State.ReadyForAdd);
    }

    // Disable remaining buttons.
    for (int i = iNumColors + 1; i < m_Buttons.Length; ++i) {
      m_Buttons[i].SetState(CustomColorButton.State.Off);
    }

    HideTrash();
  }

  override public bool UpdateStateWithInput(bool inputValid, Ray inputRay,
        GameObject parentActiveObject, Collider parentCollider) {
    if (base.UpdateStateWithInput(inputValid, inputRay, parentActiveObject, parentCollider)) {
      if (parentActiveObject == null || parentActiveObject == gameObject) {
        if (BasePanel.DoesRayHitCollider(inputRay, GetCollider())) {
          m_UIComponentManager.UpdateUIComponents(inputRay, inputValid, parentCollider);
          return true;
        }
      }
    }
    return false;
  }

  override public void ResetState() {
    for (int i = 0; i < m_Buttons.Length; ++i) {
      m_Buttons[i].ResetState();
    }
    HideTrash();
  }

  public void TriggerColorPicked(Color color) {
    if (ColorPicked != null) {
      ColorPicked(color);
    }
  }
}

} // namespace TiltBrush