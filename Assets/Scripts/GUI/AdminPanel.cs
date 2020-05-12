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

[System.Serializable]
public class AdminPanelAnimButton {
  public Transform button;
  [HideInInspector] public Renderer renderer;
  [HideInInspector] public Vector3 baseLocalPos;
  [HideInInspector] public Vector3 baseScale;
}

public class AdminPanel : BasePanel {
  [SerializeField] AdminPanelAnimButton[] m_AnimButtons;
  [SerializeField] GameObject m_SaveNewButton;
  [SerializeField] GameObject m_SaveOptionsButton;
  [SerializeField] GameObject m_SettingsButton;
  [SerializeField] GameObject m_MoreButton;
  [SerializeField] OptionButton m_ShareButton;
  [SerializeField] OptionButton m_ShareButton_Notify;
  [SerializeField] GameObject m_AdvancedModeBorder;
  [SerializeField] GameObject m_BeginnerModeButton;
  [SerializeField] GameObject m_AdvancedModeButton;
  [SerializeField] GameObject m_ButtonRotationContainer;
  [SerializeField] GameObject m_MemoryWarning;
  [SerializeField] GameObject m_MemoryWarningButton;
  [SerializeField] Color m_MemoryWarningColor;
  [SerializeField] float m_ButtonRotationAngle = 45f;

  [SerializeField] string m_ShareButtonLoggedOutExtraText;

  [SerializeField] HintObjectScript m_AdvancedModeHintObject;

  private bool? m_UpdateShareButtonState;  // for UpdateShareButton
  private Vector3 m_MemoryWarningBaseScale;

  public Transform ShareButton { get { return m_ShareButton.transform; } }
  public Transform AdvancedButton { get { return m_AdvancedModeButton.transform; } }
  public HintObjectScript AdvancedModeHintObject { get { return m_AdvancedModeHintObject; } }

  void UpdateShareButtonText() {
    // Skip redundant updates
    bool currentLoggedIn = App.GoogleIdentity.LoggedIn || App.SketchfabIdentity.LoggedIn;
    if (currentLoggedIn == m_UpdateShareButtonState) { return; }
    m_UpdateShareButtonState = currentLoggedIn;

    string text = currentLoggedIn ? "" : m_ShareButtonLoggedOutExtraText;
    m_ShareButton.SetExtraDescriptionText(text);
  }

  void RefreshButtonsForAdvancedMode() {
    // Settings in basic mode, More... in advanced mode.
    bool advancedMode = PanelManager.m_Instance.AdvancedModeActive();
    m_SettingsButton.SetActive(!advancedMode);
    m_MoreButton.SetActive(advancedMode);

    m_AdvancedModeButton.SetActive(!advancedMode);
    m_BeginnerModeButton.SetActive(advancedMode);

    m_Border.gameObject.SetActive(!advancedMode);
    m_AdvancedModeBorder.SetActive(advancedMode);
  }

  override public void InitPanel() {
    base.InitPanel();
    for (int i = 0; i < m_AnimButtons.Length; ++i) {
      m_AnimButtons[i].renderer = m_AnimButtons[i].button.GetComponent<Renderer>();
      m_AnimButtons[i].baseLocalPos = m_AnimButtons[i].button.localPosition;
      m_AnimButtons[i].baseScale = m_AnimButtons[i].button.localScale;
      m_AnimButtons[i].button.localScale = Vector3.zero;
      m_AnimButtons[i].renderer.enabled = false;
    }

    RefreshButtonsForAdvancedMode();
    SetShareButtonNotifyActive(false);

    UpdateShareButtonText();

    m_MemoryWarningBaseScale = m_MemoryWarning.transform.localScale;
    App.Switchboard.MemoryExceededChanged += OnMemoryExceededChanged;
    OnMemoryExceededChanged();
  }

  void Update() {
    BaseUpdate();

    // Update save buttons availability.
    bool alreadySaved = SaveLoadScript.m_Instance.SceneFile.Valid &&
                        SaveLoadScript.m_Instance.CanOverwriteSource;
    m_SaveNewButton.SetActive(!alreadySaved);
    m_SaveOptionsButton.SetActive(alreadySaved);
  }

  override public void ForceUpdatePanelVisuals() {
    base.ForceUpdatePanelVisuals();
    RefreshButtonsForAdvancedMode();
  }

  override protected void UpdateGazeBehavior() {
    // Cache this before the base update as a small optimization for only updating the
    // panel button placements and sizes during transitions.
    float fPrevPercent = m_GazeActivePercent;

    base.UpdateGazeBehavior();

    // Animation for moving buttons on hover.
    if (fPrevPercent != m_GazeActivePercent) {
      float fGazeActiveRatioEasedIn = 1f - Mathf.Pow(m_GazeActivePercent - 1f, 2f);

      float invGazePercent = 1.0f - m_GazeActivePercent;
      m_Border.transform.localScale = Vector3.one * invGazePercent;
      m_AdvancedModeBorder.transform.localScale = Vector3.one * invGazePercent;
      m_MemoryWarning.transform.localScale = m_MemoryWarningBaseScale * invGazePercent;

      m_ButtonRotationContainer.transform.localEulerAngles =
          Vector3.forward * (1f - fGazeActiveRatioEasedIn) * m_ButtonRotationAngle;

      for (int i = 0; i < m_AnimButtons.Length; ++i) {
        m_AnimButtons[i].button.localScale =
            Vector3.Lerp(Vector3.zero, m_AnimButtons[i].baseScale, fGazeActiveRatioEasedIn);
        m_AnimButtons[i].renderer.enabled = m_GazeActivePercent > 0.0f;
      }
    }
  }

  override public void OnUpdatePanel(Vector3 vToPanel, Vector3 vHitPoint) {
    base.OnUpdatePanel(vToPanel, vHitPoint);
    UpdateShareButtonText();

    float uploadProgress = VrAssetService.m_Instance.UploadProgress;

    // Update share button's availability.
    // This is a special case.  Normally, OptionButton.m_AllowUnavailable controls the availability
    // of buttons, but we want to run custom logic here to allow it to be available when the
    // sketch can't be shared, but we've got an upload in progress.
    bool bWasAvailable = m_ShareButton.IsAvailable();
    bool bAvailable = SketchControlsScript.m_Instance.IsCommandAvailable(
        SketchControlsScript.GlobalCommands.UploadToGenericCloud) || uploadProgress > 0.0f;
    if (bWasAvailable != bAvailable) {
      m_ShareButton.SetButtonAvailable(bAvailable);
    }

    // Enable the appropriate share button depending on upload state.
    // Keep both the buttons' colors and shader variables updated, regardless of activity.
    if (uploadProgress > 0.0f) {
      if (uploadProgress >= 1.0f) {
        ActivatePromoBorder(false);
        SetShareButtonNotifyActive(true);
      } else {
        SetShareButtonNotifyActive(false);
      }
    } else {
      uploadProgress = 0.0f;
      SetShareButtonNotifyActive(false);
    }

    m_ShareButton.GetComponent<Renderer>().material.SetFloat("_Ratio", uploadProgress);
    m_ShareButton_Notify.GetComponent<Renderer>().material.SetFloat("_Ratio", uploadProgress);

    Color col = GetGazeColor();
    m_ShareButton.SetColor(col);
    m_ShareButton_Notify.SetColor(col);
  }

  void SetShareButtonNotifyActive(bool active) {
    m_ShareButton.gameObject.SetActive(!active);
    m_ShareButton_Notify.gameObject.SetActive(active);
  }

  void OnMemoryExceededChanged() {
    m_MemoryWarning.SetActive(SketchMemoryScript.m_Instance.MemoryExceeded);
    m_MemoryWarningButton.SetActive(SketchMemoryScript.m_Instance.MemoryExceeded);
    m_MemoryWarning.GetComponent<Renderer>().material.SetColor("_Color", m_MemoryWarningColor);
  }
}
}  // namespace TiltBrush
