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

class DemoManager : MonoBehaviour {
  public static DemoManager m_Instance;

  private enum DemoState {
    WaitingForFirstStroke,
    Standard,
    FadingOut,
  }

  [Tooltip("How many seconds to freeze before resetting.")]
  [SerializeField] private float m_TimesUpFadeOutDuration;

  [Tooltip("How many seconds to fade to black during a manual reset.")]
  [SerializeField] private float m_FadeOutDuration;

  [Tooltip("How many seconds to fade from black after a reset.")]
  [SerializeField] private float m_FadeInDuration;

  [SerializeField] private GameObject m_DemoGuiPrefab;

  private DemoManagerGui m_DemoGui;

  private DemoState m_DemoState;
  private float m_SecondsRemainingInState;

  public bool DemoModeEnabled {
    get {
      return App.UserConfig.Demo.Enabled;
    }
  }

  public bool ShouldUseCountdownTimer {
    get {
      return DemoModeEnabled &&
          App.UserConfig.Demo.Duration.HasValue && App.UserConfig.Demo.Duration.Value > 0;
    }
  }

  public string TimeRemaining {
    get {
      int secondsRemaining = (int) m_SecondsRemainingInState;
      if (m_DemoState == DemoState.Standard) {
        secondsRemaining++; // Round up while we're counting down.
      }
      if (m_DemoState == DemoState.FadingOut) {
        secondsRemaining = 0;
      }
      int minutes = secondsRemaining / 60;
      int seconds = secondsRemaining % 60;
      return minutes + ":" + seconds.ToString().PadLeft(2, '0');
    }
  }

  void Awake() {
    m_Instance = this;

    if (DemoModeEnabled) {
      GameObject go = (GameObject)Instantiate(m_DemoGuiPrefab);
      m_DemoGui = go.GetComponent<DemoManagerGui>();
    } else {
      // Disable this component on startup if it's not enabled.
      this.enabled = false;
    }
  }

  void Start() {
    if (!DemoModeEnabled) {
      return;
    }
    OnSessionStart();
  }

  void Update() {
    if (!DemoModeEnabled) {
      return;
    }

    switch (m_DemoState) {
    case DemoState.WaitingForFirstStroke:
      if (PointerManager.m_Instance.IsMainPointerCreatingStroke()) {
        m_DemoState = DemoState.Standard;
      }
      break;
    case DemoState.Standard:
      if (ShouldUseCountdownTimer) {
        m_SecondsRemainingInState -= Time.deltaTime;

        // If the timer is up, immediately lock the sketch and start a slow fade out.
        if (m_SecondsRemainingInState <= 0.0f) {
          FadeOut(m_TimesUpFadeOutDuration);
        }
      }
      break;
    case DemoState.FadingOut:
      m_SecondsRemainingInState -= Time.deltaTime;
      if (m_SecondsRemainingInState <= 0.0f) {
        m_SecondsRemainingInState = 0.0f;
        App.Instance.SetDesiredState(App.AppState.Standard);
        OnSessionStart();
      }
      break;
    }

    UpdateKeyboardCommands();

    // Update on-screen counter.
    m_DemoGui.m_CountdownTimerUI.SetActive(ShouldUseCountdownTimer);
    if (ShouldUseCountdownTimer) {
      m_DemoGui.m_TimeRemainingText.text = TimeRemaining;
    }
  }

  private void UpdateKeyboardCommands() {
    if (m_DemoState != DemoState.WaitingForFirstStroke &&
        m_DemoState != DemoState.Standard) {
      return;
    }

    // Hitting the delete key in demo mode resets everything, making the app ready
    // for the next demo guest.
    if (InputManager.m_Instance.GetKeyboardShortcutDown(
            InputManager.KeyboardShortcut.ResetEverything)) {
      FadeOut(m_FadeOutDuration);
    }

    // Hitting P in demo mode resets the scene transform to the initial transform,
    // that is, the position saved in the sketch, not the origin.
    if (InputManager.m_Instance.GetKeyboardShortcutDown(
            InputManager.KeyboardShortcut.GotoInitialPosition)) {
      SketchControlsScript.m_Instance.RequestWorldTransformReset(toSavedXf: true);
    }

    // Hitting E in demo mode extends the countdown timer (if active) by 30 seconds.
    if (InputManager.m_Instance.GetKeyboardShortcutDown(
            InputManager.KeyboardShortcut.ExtendDemoTimer)) {
      m_SecondsRemainingInState += 30.0f;
    }
  }

  /// Called on start, and after any reset has completed.
  private void OnSessionStart() {
    m_DemoState = ShouldUseCountdownTimer ? DemoState.WaitingForFirstStroke
                                          : DemoState.Standard;
    if (ShouldUseCountdownTimer) {
      m_SecondsRemainingInState = App.UserConfig.Demo.Duration.Value;
    }
    ViewpointScript.m_Instance.FadeToScene(1.0f / m_FadeInDuration);
  }

  private void FadeOut(float duration) {
    m_DemoState = DemoState.FadingOut;
    App.Instance.SetDesiredState(App.AppState.Reset);
    m_SecondsRemainingInState = duration;
    ViewpointScript.m_Instance.FadeToColor(Color.black, 1.0f / duration);
  }
}
}  // namespace TiltBrush
