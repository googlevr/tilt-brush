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

public class AudioVizPopUpWindow : PopUpWindow {
  [SerializeField] private GameObject m_CloseButton;
  [SerializeField] private float m_AudioFoundDuration;
  [SerializeField] private string m_AudioFoundText;
  [SerializeField] private TextMesh m_StatusText;
  [SerializeField] private int m_MaxStatusLength;
  [SerializeField] private Renderer m_HintText;
  [SerializeField] private float m_AudioSearchHintDelay = 2.0f;

  private bool m_AudioFound;
  private float m_AudioFoundTimer;
  private float m_AudioSearchTimer;

  override public void Init(GameObject rParent, string sText) {
    base.Init(rParent, sText);
    m_HintText.enabled = false;
    m_AudioFound = false;
  }

  void Update() {
    BaseUpdate();
    UpdateVisuals();

    // Have we locked on to audio yet?
    if (!m_AudioFound) {
      string status = AudioCaptureManager.m_Instance.GetCaptureStatusMessage();
      if (status.Length > m_MaxStatusLength) {
        m_StatusText.text = status.Substring(0, m_MaxStatusLength) + "...";
      } else {
        m_StatusText.text = status;
      }
      if (m_AudioSearchTimer > m_AudioSearchHintDelay) {
        m_HintText.enabled = true;
      }

      if (AudioCaptureManager.m_Instance.IsCapturingAudio) {
        m_WindowText.text = m_AudioFoundText;
        m_AudioFoundTimer = m_AudioFoundDuration;
        m_AudioSearchTimer = 0.0f;
        m_AudioFound = true;
        m_CloseButton.SetActive(false);
      }
      m_AudioSearchTimer += Time.deltaTime;
    } else {
      // Close once we've told the user what's going on.
      m_AudioFoundTimer -= Time.deltaTime;
      m_HintText.enabled = false;
      if (m_AudioFoundTimer <= 0.0f) {
        DestroyPopUpWindow();
      }
    }
  }

  override protected void UpdateVisuals() {
    base.BaseUpdate();
    m_CloseButton.GetComponent<BaseButton>().UpdateVisuals();
  }

  override public bool RequestClose(bool bForceClose = false) {
    if (bForceClose) {
      App.Instance.ToggleAudioReactiveBrushesRequest();
    }

    BaseButton closeButton = m_CloseButton.GetComponent<BaseButton>();
    closeButton.ResetState();
    closeButton.ForceDescriptionDeactivate();

    return base.RequestClose(bForceClose || m_AudioFound);
  }
}
}  // namespace TiltBrush
