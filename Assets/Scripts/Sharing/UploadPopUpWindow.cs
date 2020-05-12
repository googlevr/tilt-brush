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
using GlobalCommands = TiltBrush.SketchControlsScript.GlobalCommands;

namespace TiltBrush {

class UploadPopUpWindow : OptionsPopUpWindow {

  private enum DisplayMode {
    Blank,
    Loggedout,
    Confirming,
    Uploading,
    UploadComplete,
    UploadFailed,
    UploadingDenied,
    Waiting,
    EmbeddedMediaWarningPoly,
    EmbeddedMediaWarningSketchfab,
    NothingToUploadWarning,
    ConnectionError,
    OutOfDate,
  }

  [SerializeField] private Renderer m_Progress;

  // Things that should be visible when the user should be logging in [outside Tilt Brush].
  [SerializeField] private GameObject m_LoginOnDesktopObjects;

  // Things that should be visible when confirming upload.
  [SerializeField] private GameObject m_ConfirmObjects;
  [SerializeField] private GameObject m_PolyLoggedInObjects;
  [SerializeField] private GameObject m_PolyLoggedOutObjects;
  [SerializeField] private GameObject m_SketchfabLoggedInObjects;
  [SerializeField] private GameObject m_SketchfabLoggedOutObjects;
  [SerializeField] private TMPro.TextMeshPro m_PolyUserName;
  [SerializeField] private TMPro.TextMeshPro m_SketchfabUserName;
  [SerializeField] private Renderer m_GooglePhoto;
  [SerializeField] private Renderer m_SketchfabPhoto;

  // Things that should be visible when uploading.
  [SerializeField] private GameObject m_UploadObjects;

  // Things that should be visible after upload completes.
  [SerializeField] private GameObject m_UploadCompleteObjects;

  // Things that should be visible when upload fails.
  [SerializeField] private GameObject m_UploadFailedObjects;
  [SerializeField] private TMPro.TextMeshPro m_UploadFailedMessage;

  // Things that should be visible when uploading is not allowed.
  [SerializeField] private GameObject m_UploadingDeniedObjects;

  // Things that should be visible while waiting.
  [SerializeField] private GameObject m_WaitObjects;

  // Things that should be visible when media library content is in the scene.
  [SerializeField] private GameObject m_EmbeddedMediaWarningPoly;
  [SerializeField] private GameObject m_EmbeddedMediaWarningSketchfab;

  // Things that should be visible when there's nothing to upload.
  [SerializeField] private GameObject m_NothingToUploadWarning;

  // Things that should be visible when there has been a connection error.
  [SerializeField] private GameObject m_ConnectionErrorObjects;

  // Things that should be visible when Tilt Brush can't talk to Poly because it is
  // out of date.
  [SerializeField] private GameObject m_OutOfDateObjects;

  // Close button (should only be visible in some states)
  [SerializeField] private GameObject m_CloseButton;

  private VrAssetService m_AssetService;
  private Cloud m_LoggingInType;

  public override void Init(GameObject rParent, string sText) {
    base.Init(rParent, sText);

    m_AssetService = VrAssetService.m_Instance;

    InitUI();

    OAuth2Identity.ProfileUpdated += OnProfileUpdated;
    RefreshUploadButton(Cloud.Poly);
    RefreshUploadButton(Cloud.Sketchfab);
    m_OnClose += OnClose;

    SketchMemoryScript.m_Instance.OperationStackChanged += OnOperationStackChanged;

    if (App.GoogleIdentity.LoggedIn){
      PromoManager.m_Instance.RecordCompletion(PromoType.ShareSketch);
    }
  }

  /// Turning on/off display modes all happens from here to make sure you can't have more than one
  /// mode visible at once by forgetting to turn one off.
  private void SetMode(DisplayMode displayMode) {
    m_LoginOnDesktopObjects.SetActive(displayMode == DisplayMode.Loggedout);
    m_ConfirmObjects.SetActive(displayMode == DisplayMode.Confirming);
    m_UploadObjects.SetActive(displayMode == DisplayMode.Uploading);
    m_UploadCompleteObjects.SetActive(displayMode == DisplayMode.UploadComplete);
    m_UploadFailedObjects.SetActive(displayMode == DisplayMode.UploadFailed);
    m_UploadingDeniedObjects.SetActive(displayMode == DisplayMode.UploadingDenied);
    m_WaitObjects.SetActive(displayMode == DisplayMode.Waiting);
    m_EmbeddedMediaWarningPoly.SetActive(displayMode == DisplayMode.EmbeddedMediaWarningPoly);
    m_EmbeddedMediaWarningSketchfab.SetActive(
        displayMode == DisplayMode.EmbeddedMediaWarningSketchfab);
    m_NothingToUploadWarning.SetActive(displayMode == DisplayMode.NothingToUploadWarning);
    m_ConnectionErrorObjects.SetActive(displayMode == DisplayMode.ConnectionError);
    m_OutOfDateObjects.SetActive(displayMode == DisplayMode.OutOfDate);

    bool closeButtonVisible =
        (displayMode == DisplayMode.Blank) ||
        (displayMode == DisplayMode.Loggedout) ||
        (displayMode == DisplayMode.Confirming) ||
        (displayMode == DisplayMode.Uploading) ||
        (displayMode == DisplayMode.UploadComplete) ||
        (displayMode == DisplayMode.UploadFailed) ||
        (displayMode == DisplayMode.Waiting);
    m_CloseButton.SetActive(closeButtonVisible);
  }

  private void InitUI() {
    // Default everything off.
    m_LoggingInType = Cloud.None;
    SetMode(DisplayMode.Blank);

    { // Turn off the poly option
      (var name, var inEl, var outEl, var photo) = GetUiFor(Cloud.Poly);
      name.gameObject.SetActive(false);
      inEl.SetActive(false);
      outEl.SetActive(false);
      photo.gameObject.SetActive(false);
    }

    if (m_AssetService.UploadProgress <= 0.0f) {
      // Ask user to choose a cloud service to upload to, which also serves as confirmation to
      // upload to that service.
      SetMode(DisplayMode.Confirming);
    } else if (m_AssetService.UploadProgress < 1.0f) {
      // Upload in progress
      SetMode(DisplayMode.Uploading);
    } else if (m_AssetService.LastUploadFailed) {
      // We got an error message, so we're busted.
      m_UploadFailedMessage.text = m_AssetService.LastUploadErrorMessage;
      SetMode(DisplayMode.UploadFailed);
    } else {
      // Success!
      SetMode(DisplayMode.UploadComplete);
    }
  }

  void OnDestroy() {
    OAuth2Identity.ProfileUpdated -= OnProfileUpdated;
    SketchMemoryScript.m_Instance.OperationStackChanged -= OnOperationStackChanged;
  }

  override public void SetPopupCommandParameters(int commandParam, int commandParam2) {
    // Overrideen so we don't get an irritating warning from base class.
    // Command that pops us up is GlobalCommands.UploadToGenericCloud(Cloud param), but it's not
    // intended to actually be used with IssueGlobalCommand(), so param should be Cloud.None
    Debug.Assert((Cloud)commandParam == Cloud.None);
  }

  override protected void BaseUpdate() {
    base.BaseUpdate();

    // Logged out state.
    if (m_LoginOnDesktopObjects.activeSelf) {
      // Check to see if we just logged in.
      if ((m_LoggingInType == Cloud.Poly && App.GoogleIdentity.LoggedIn) ||
          (m_LoggingInType == Cloud.Sketchfab && App.SketchfabIdentity.LoggedIn)) {
        SetMode(DisplayMode.Loggedout);
        // It's easy to get the logic wrong for how to re-initialize the UI, so just go through a
        // fresh initialization instead of trying to recreate the init logic.
        // See b/64718584 for additional details.
        InitUI();
        return;
      }
    } else {
      // Logged in states.
      float progress = m_AssetService.UploadProgress;
      if (progress == 0) {
        // If upload has not started and sketch is undone until upload is no longer available,
        // close the popup.
        if (!SketchControlsScript.m_Instance.IsCommandAvailable(
            GlobalCommands.UploadToGenericCloud)) {
          RequestClose(bForceClose: true);
        }
      } else if (progress > 0f && progress < 1f) {
        // Upload in progress.
        if (!m_UploadObjects.activeSelf) {
          SetMode(DisplayMode.Uploading);
        }
        // Send progress to shader.
        m_Progress.material.SetFloat("_Ratio", progress);
      } else if (progress >= 1f && m_UploadObjects.activeSelf) {
        // Finished.
        if (m_AssetService.LastUploadFailed) {
          // Failed.
          m_UploadFailedMessage.text = m_AssetService.LastUploadErrorMessage;
          SetMode(DisplayMode.UploadFailed);
        } else {
          SetMode(DisplayMode.UploadComplete);
        }
      }
    }
  }

  (TMPro.TextMeshPro name, GameObject loggedInElements, GameObject loggedOutElements,
      Renderer photo) GetUiFor(Cloud cloud) {
    switch (cloud) {
      case Cloud.Sketchfab: return (m_SketchfabUserName,
          m_SketchfabLoggedInObjects, m_SketchfabLoggedOutObjects, m_SketchfabPhoto);
      case Cloud.Poly: return (m_PolyUserName, m_PolyLoggedInObjects,
          m_PolyLoggedOutObjects, m_GooglePhoto);
      default: throw new InvalidOperationException($"{cloud}");
    }
  }

  void RefreshUploadButton(Cloud backend) {
    if (backend == Cloud.Poly) { return; }
    var ui = GetUiFor(backend);
    OAuth2Identity.UserInfo profile = App.GetIdentity(backend).Profile;
    ui.loggedInElements.SetActive(profile != null);
    ui.loggedOutElements.SetActive(profile == null);
 
    if (profile != null) {
      ui.name.text = profile.name;
      ui.photo.material.mainTexture = profile.icon;
    }
  }

  void OnProfileUpdated(OAuth2Identity _) {
    RefreshUploadButton(Cloud.Poly);
    RefreshUploadButton(Cloud.Sketchfab);
  }

  void OnClose() {
    m_AssetService.ConsumeUploadResults();
  }

  void OnOperationStackChanged() {
    Debug.Log($"Checking state");
    // An embedded media warning only shows up if the user has tried to upload and there was non-
    // exportable content. So we only need to be concerned with the case that the embedded warning
    // is showing and is no longer relevant.
    if (m_EmbeddedMediaWarningPoly.activeSelf &&
        !WidgetManager.m_Instance.HasNonExportableContent(Cloud.Poly)) {
      SetMode(DisplayMode.Confirming);
    }
    if (m_EmbeddedMediaWarningSketchfab.activeSelf &&
        !WidgetManager.m_Instance.HasNonExportableContent(Cloud.Sketchfab)) {
      SetMode(DisplayMode.Confirming);
    }
  }

  public void UserPressedLoginButton(int param) {
    SetMode(DisplayMode.Loggedout);
    m_LoggingInType = (Cloud)param;
  }

  /// Check to see if it's safe to upload. If it is, execute the action that's passed in. Otherwise,
  /// show a warning or error.
  public void UserPressedUploadButton(Cloud cloud, Action onSafeToUpload) {
    Debug.Assert(cloud == Cloud.Sketchfab);
    // User attempted to upload, but make sure there's actually something to upload.
    if (!WidgetManager.m_Instance.HasExportableContent(cloud)) {
      SetMode(DisplayMode.NothingToUploadWarning);
      return;
    }

    // See if we've got invalid content.
    if (WidgetManager.m_Instance.HasNonExportableContent(cloud)) {
      var panel = m_UIComponentManager.GetPanelForPopUps();
      panel.UpdateDelayedCommandParameter((int) cloud);
      SetMode(DisplayMode.EmbeddedMediaWarningSketchfab);
      return;
    }

    // We haven't errored out, so we're safe to execute the action now.
    onSafeToUpload?.Invoke();
  }
}

}  // namespace TiltBrush
