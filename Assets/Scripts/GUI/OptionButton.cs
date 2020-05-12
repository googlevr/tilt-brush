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

public class OptionButton : BaseButton {
  [SerializeField] public SketchControlsScript.GlobalCommands m_Command;
  [SerializeField] public int m_CommandParam = -1;
  [SerializeField] protected int m_CommandParam2 = -1;
  [SerializeField] protected bool m_RequiresPopup = false;
  [SerializeField] protected bool m_CenterPopupOnButton = false;
  [SerializeField] protected Vector3 m_PopupOffset;
  [SerializeField] protected string m_PopupText = "";
  [SerializeField] protected string m_ToggleOnDescription = "";
  [SerializeField] protected Texture2D m_ToggleOnTexture;
  [SerializeField] protected bool m_AllowUnavailable = false;
  [SerializeField] private GameObject m_LinkedUIObject;
  private string m_DefaultDescription;
  private Texture2D m_DefaultTexture;

  public void SetCommandParameters(int iCommandParam, int iCommandParam2 = -1) {
    m_CommandParam = iCommandParam;
    m_CommandParam2 = iCommandParam2;
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    if (m_Command == SketchControlsScript.GlobalCommands.SymmetryPlane) {
      App.Switchboard.MirrorVisibilityChanged -= UpdateVisuals;
    }
  }

  override protected void Awake() {
    base.Awake();
    m_DefaultDescription = Description;
    m_DefaultTexture = m_ButtonTexture;
    if (m_Command == SketchControlsScript.GlobalCommands.SymmetryPlane) {
      App.Switchboard.MirrorVisibilityChanged += UpdateVisuals;
    }
  }

  override protected void Start() {
    base.Start();
    // This is disabled on startup to serve the single button that uses it.  If additional
    // buttons use this member, consider making this an init flag.
    if (m_LinkedUIObject) {
      m_LinkedUIObject.SetActive(false);
    }
  }

  override public void UpdateVisuals() {
    base.UpdateVisuals();
    // Inactive and toggle buttons poll for status.
    if (m_AllowUnavailable) {
      UpdateAvailability();
    }
    if (m_ToggleButton) {
      bool bWasToggleActive = m_ToggleActive;
      m_ToggleActive = SketchControlsScript.m_Instance.IsCommandActive(m_Command, m_CommandParam);
      if (bWasToggleActive != m_ToggleActive) {
        if (m_ToggleActive) {
          SetButtonActivated(true);

          if (m_ToggleOnDescription != "") {
            SetDescriptionText(m_ToggleOnDescription);
          }
          if (m_ToggleOnTexture != null) {
            SetButtonTexture(m_ToggleOnTexture);
          }
        } else {
          SetButtonActivated(false);

          if (m_ToggleOnDescription != "") {
            SetDescriptionText(m_DefaultDescription);
          }
          if (m_ToggleOnTexture != null) {
            SetButtonTexture(m_DefaultTexture);
          }
        }
      }
      if (m_LinkedUIObject) {
        m_LinkedUIObject.SetActive(m_ToggleActive);
      }
    }
  }

  virtual protected void UpdateAvailability() {
    bool bWasAvailable = IsAvailable();
    bool bAvailable = SketchControlsScript.m_Instance.IsCommandAvailable(m_Command, m_CommandParam);
    if (bWasAvailable != bAvailable) {
      SetButtonAvailable(bAvailable);
    }
  }

  override protected void OnButtonPressed() {
    if (m_RequiresPopup) {
      if (m_Manager) {
        BasePanel panel = m_Manager.GetPanelForPopUps();
        if (panel != null) {
          panel.CreatePopUp(m_Command, m_CommandParam, m_CommandParam2, m_PopupOffset,
              m_PopupText);
          if (m_CenterPopupOnButton) {
            panel.PositionPopUp(transform.position +
                transform.forward * panel.PopUpOffset +
                panel.transform.TransformVector(m_PopupOffset));
          }
          ResetState();
        }
      }
    } else {
      SketchControlsScript.m_Instance.IssueGlobalCommand(m_Command, m_CommandParam,
          m_CommandParam2);
    }
  }
}
}  // namespace TiltBrush
