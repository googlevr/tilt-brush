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
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;

namespace TiltBrush {

public class SketchbookPanel : ModalPanel {
  // Index of the "local sketches" button in m_GalleryButtons
  const int kElementNumberGalleryButtonLocal = 0;
  // Amount of extra space to put below the "local sketches" gallery button
  const float kGalleryButtonLocalPadding = .15f;

  [SerializeField] private Texture2D m_LoadingImageTexture;
  [SerializeField] private Texture2D m_UnknownImageTexture;
  [SerializeField] private TextMesh m_PanelText;
  [SerializeField] private TextMeshPro m_PanelTextPro;
  [SerializeField] private string m_PanelTextStandard;
  [SerializeField] private string m_PanelTextShowcase;
  [SerializeField] private string m_PanelTextLiked;
  [SerializeField] private string m_PanelTextDrive;
  [SerializeField] private GameObject m_NoSketchesMessage;
  [SerializeField] private GameObject m_NoDriveSketchesMessage;
  [SerializeField] private GameObject m_NoLikesMessage;
  [SerializeField] private GameObject m_NotLoggedInMessage;
  [SerializeField] private GameObject m_NotLoggedInDriveMessage;
  [SerializeField] private GameObject m_NoShowcaseMessage;
  [SerializeField] private GameObject m_ContactingServerMessage;
  [SerializeField] private GameObject m_OutOfDateMessage;
  [SerializeField] private GameObject m_NoPolyConnectionMessage;
  [SerializeField] private Renderer m_OnlineGalleryButtonRenderer;
  [SerializeField] private GameObject[] m_IconsOnFirstPage;
  [SerializeField] private GameObject[] m_IconsOnNormalPage;
  [SerializeField] private GameObject m_CloseButton;
  [SerializeField] private GameObject m_NewSketchButton;
  // Gallery buttons will automatically reposition based on how many are visible so they must be
  // added to this array in order from top to bottom.
  [SerializeField] private GalleryButton[] m_GalleryButtons;
  [SerializeField] private int m_ElementNumberGalleryButtonDrive = 3;
  [SerializeField] private float m_GalleryButtonHeight = 0.3186f;
  [SerializeField] private Renderer m_ProfileButtonRenderer;
  [SerializeField] private GameObject m_LoadingGallery;
  [SerializeField] private GameObject m_DriveSyncProgress;
  [SerializeField] private GameObject m_SyncingDriveIcon;
  [SerializeField] private GameObject m_DriveEnabledIcon;
  [SerializeField] private GameObject m_DriveDisabledIcon;
  [SerializeField] private GameObject m_DriveFullIcon;
  [SerializeField] private Vector2 m_SketchIconUvScale = new Vector2(0.7f, 0.7f);
  [SerializeField] private Vector3 m_ReadOnlyPopupOffset;

  private float m_ImageAspect;
  private Vector2 m_HalfInvUvScale;

  private SceneFileInfo m_FirstSketch;

  private bool m_AllIconTexturesAssigned;
  private bool m_AllSketchesAreAvailable;
  private SketchSetType m_CurrentSketchSet;
  private SketchSet m_SketchSet;
  private OptionButton m_NewSketchButtonScript;
  private OptionButton m_PaintButtonScript;
  private List<BaseButton> m_IconScriptsOnFirstPage;
  private List<BaseButton> m_IconScriptsOnNormalPage;
  private bool m_DriveSetHasSketches;
  private bool m_ReadOnlyShown = false;

  public float ImageAspect { get { return m_ImageAspect; } }

  override public void SetInIntroMode(bool inIntro) {
    m_NewSketchButton.SetActive(inIntro);
    m_CloseButton.SetActive(!inIntro);

    // When we switch in to intro mode, make our panel colorful, even if it doesn't have focus,
    // to help attract attention.
    if (inIntro) {
      for (int i = 0; i < m_IconScriptsOnFirstPage.Count; ++i) {
        m_IconScriptsOnFirstPage[i].SetButtonGrayscale(false);
      }
      for (int i = 0; i < m_IconScriptsOnNormalPage.Count; ++i) {
        m_IconScriptsOnNormalPage[i].SetButtonGrayscale(false);
      }
    }
  }

  protected override List<BaseButton> Icons {
    get {
      return (PageIndex == 0 ? m_IconScriptsOnFirstPage : m_IconScriptsOnNormalPage);
    }
  }

  public override bool IsInButtonMode(ModeButton button) {
    GalleryButton galleryButton = button as GalleryButton;
    return galleryButton &&
      ((galleryButton.m_ButtonType == GalleryButton.Type.Liked && m_CurrentSketchSet == SketchSetType.Liked) ||
      (galleryButton.m_ButtonType == GalleryButton.Type.Local && m_CurrentSketchSet == SketchSetType.User) ||
      (galleryButton.m_ButtonType == GalleryButton.Type.Showcase && m_CurrentSketchSet == SketchSetType.Curated) ||
      (galleryButton.m_ButtonType == GalleryButton.Type.Drive && m_CurrentSketchSet == SketchSetType.Drive));
  }

  override public void InitPanel() {
    base.InitPanel();

    m_NewSketchButtonScript = m_NewSketchButton.GetComponent<OptionButton>();
    m_PaintButtonScript = m_CloseButton.GetComponent<OptionButton>();
    m_IconScriptsOnFirstPage = new List<BaseButton>();
    for (int i = 0; i < m_IconsOnFirstPage.Length; ++i) {
      m_IconScriptsOnFirstPage.Add(m_IconsOnFirstPage[i].GetComponent<BaseButton>());
    }
    m_IconScriptsOnNormalPage = new List<BaseButton>();
    for (int i = 0; i < m_IconsOnNormalPage.Length; ++i) {
      m_IconScriptsOnNormalPage.Add(m_IconsOnNormalPage[i].GetComponent<BaseButton>());
    }
    SetInIntroMode(false);

    Debug.Assert(m_SketchIconUvScale.x >= 0.0f && m_SketchIconUvScale.x <= 1.0f &&
        m_SketchIconUvScale.y >= 0.0f && m_SketchIconUvScale.y <= 1.0f);
    m_HalfInvUvScale.Set(1.0f - m_SketchIconUvScale.x, 1.0f - m_SketchIconUvScale.y);
    m_HalfInvUvScale *= 0.5f;
  }

  protected override void OnStart() {
    // Initialize icons.
    LoadSketchButton[] rPanelButtons = m_Mesh.GetComponentsInChildren<LoadSketchButton>();
    foreach (LoadSketchButton icon in rPanelButtons) {
      GameObject go = icon.gameObject;
      go.SetActive(false);
    }

    // GameObject is active in prefab so the button registers.
    m_NoLikesMessage.SetActive(false);
    m_NotLoggedInMessage.SetActive(false);
    m_NotLoggedInDriveMessage.SetActive(false);

    // Dynamically position the gallery buttons.
    OnDriveSetHasSketchesChanged();

    // Set the sketch set var to Liked, then function set to force state.
    m_CurrentSketchSet = SketchSetType.Liked;
    SetVisibleSketchSet(SketchSetType.Curated);

    Action refresh = () => {
      if (m_ContactingServerMessage.activeSelf ||
          m_NoShowcaseMessage.activeSelf ||
          m_LoadingGallery.activeSelf) {
        // Update the overlays more frequently when these overlays are shown to reflect whether
        // we are actively trying to get sketches from Poly.
        RefreshPage();
      }
    };
    SketchCatalog.m_Instance.GetSet(SketchSetType.Liked).OnSketchRefreshingChanged += refresh;
    SketchCatalog.m_Instance.GetSet(SketchSetType.Curated).OnSketchRefreshingChanged += refresh;
    SketchCatalog.m_Instance.GetSet(SketchSetType.Drive).OnSketchRefreshingChanged += refresh;
    App.GoogleIdentity.OnLogout += refresh;
  }

  void OnDestroy() {
    if (m_SketchSet != null) {
      m_SketchSet.OnChanged -= OnSketchSetDirty;
    }
  }

  override protected void OnEnablePanel() {
    base.OnEnablePanel();
    if (m_SketchSet != null) {
      m_SketchSet.RequestRefresh();
    }
  }

  void SetVisibleSketchSet(SketchSetType type) {
    if (m_CurrentSketchSet != type) {
      // Clean up our old sketch set.
      if (m_SketchSet != null) {
        m_SketchSet.OnChanged -= OnSketchSetDirty;
      }

      // Cache new set.
      m_SketchSet = SketchCatalog.m_Instance.GetSet(type);
      m_SketchSet.OnChanged += OnSketchSetDirty;
      m_SketchSet.RequestRefresh();

      // Tell all the icons which set to reference when loading sketches.
      IEnumerable<LoadSketchButton> allIcons = m_IconsOnFirstPage.Concat(m_IconsOnNormalPage)
          .Select(icon => icon.GetComponent<LoadSketchButton>())
          .Where(icon => icon != null);
      foreach (LoadSketchButton icon in allIcons) {
        icon.SketchSet = m_SketchSet;
      }

      ComputeNumPages();
      ResetPageIndex();
      m_CurrentSketchSet = type;
      RefreshPage();

      switch (m_CurrentSketchSet) {
      case SketchSetType.User:
        if (m_PanelText) {
          m_PanelText.text = m_PanelTextStandard;
        }
        if (m_PanelTextPro) {
          m_PanelTextPro.text = m_PanelTextStandard;
        }
        break;
      case SketchSetType.Curated:
        if (m_PanelText) {
          m_PanelText.text = m_PanelTextShowcase;
        }
        if (m_PanelTextPro) {
          m_PanelTextPro.text = m_PanelTextShowcase;
        }
        break;
      case SketchSetType.Liked:
        if (m_PanelText) {
          m_PanelText.text = m_PanelTextLiked;
        }
        if (m_PanelTextPro) {
          m_PanelTextPro.text = m_PanelTextLiked;
        }
        break;
      case SketchSetType.Drive:
        if (m_PanelText) {
          m_PanelText.text = m_PanelTextDrive;
        }
        if (m_PanelTextPro) {
          m_PanelTextPro.text = m_PanelTextDrive;
        }
        break;
      }
    }
  }

  private void ComputeNumPages() {
    if (m_SketchSet.NumSketches <= m_IconsOnFirstPage.Length) {
      m_NumPages = 1;
      return;
    }
    int remainingSketches = m_SketchSet.NumSketches - m_IconsOnFirstPage.Length;
    int normalPages = ((remainingSketches - 1) / m_IconsOnNormalPage.Length) + 1;
    m_NumPages = 1 + normalPages;
  }

  List<int> GetIconLoadIndices() {
    var ret = new List<int>();
    for (int i = 0; i < Icons.Count; i++) {
      int sketchIndex = m_IndexOffset + i;
      if (sketchIndex >= m_SketchSet.NumSketches) {
        break;
      }
      ret.Add(sketchIndex);
    }
    return ret;
  }

  protected override void RefreshPage() {
    m_SketchSet.RequestOnlyLoadedMetadata(GetIconLoadIndices());
    m_AllIconTexturesAssigned = false;
    m_AllSketchesAreAvailable = false;

    // Disable all.
    foreach (var i in m_IconsOnFirstPage) {
      i.SetActive(false);
    }
    foreach (var i in m_IconsOnNormalPage) {
      i.SetActive(false);
    }

    // Base Refresh updates the modal parts of the panel, and we always want those refreshed.
    base.RefreshPage();

    bool polyDown = VrAssetService.m_Instance.NoConnection
        && (m_CurrentSketchSet == SketchSetType.Curated
        || m_CurrentSketchSet == SketchSetType.Liked);
    m_NoPolyConnectionMessage.SetActive(polyDown);

    bool outOfDate = !polyDown && !VrAssetService.m_Instance.Available
        && (m_CurrentSketchSet == SketchSetType.Curated
        || m_CurrentSketchSet == SketchSetType.Liked);
    m_OutOfDateMessage.SetActive(outOfDate);

    if (outOfDate || polyDown) {
      m_NoSketchesMessage.SetActive(false);
      m_NoDriveSketchesMessage.SetActive(false);
      m_NotLoggedInMessage.SetActive(false);
      m_NoLikesMessage.SetActive(false);
      m_ContactingServerMessage.SetActive(false);
      m_NoShowcaseMessage.SetActive(false);
      return;
    }

    bool refreshIcons = m_SketchSet.NumSketches > 0;

    // Show no sketches if we don't have sketches.
    m_NoSketchesMessage.SetActive(
        (m_CurrentSketchSet == SketchSetType.User) && (m_SketchSet.NumSketches <= 0));
    m_NoDriveSketchesMessage.SetActive(
        (m_CurrentSketchSet == SketchSetType.Drive) && (m_SketchSet.NumSketches <= 0));

    // Show sign in popup if signed out for liked or drive sketchsets
    bool showNotLoggedIn = !App.GoogleIdentity.LoggedIn &&
                           (m_CurrentSketchSet == SketchSetType.Liked ||
                            m_CurrentSketchSet == SketchSetType.Drive);
    refreshIcons = refreshIcons && !showNotLoggedIn;
    m_NotLoggedInMessage.SetActive(showNotLoggedIn && m_CurrentSketchSet == SketchSetType.Liked);
    m_NotLoggedInDriveMessage.SetActive(showNotLoggedIn &&
                                        m_CurrentSketchSet == SketchSetType.Drive);

    // Show no likes text & gallery button if we don't have liked sketches.
    m_NoLikesMessage.SetActive(
      (m_CurrentSketchSet == SketchSetType.Liked) &&
      (m_SketchSet.NumSketches <= 0) &&
      !m_SketchSet.IsActivelyRefreshingSketches &&
      App.GoogleIdentity.LoggedIn);

    // Show Contacting Server if we're talking to Poly.
    m_ContactingServerMessage.SetActive(
      (m_CurrentSketchSet == SketchSetType.Curated ||
       m_CurrentSketchSet == SketchSetType.Liked ||
       m_CurrentSketchSet == SketchSetType.Drive) &&
      (m_SketchSet.NumSketches <= 0) &&
      (m_SketchSet.IsActivelyRefreshingSketches && App.GoogleIdentity.LoggedIn));

    // Show Showcase error if we're in Showcase and don't have sketches.
    m_NoShowcaseMessage.SetActive(
      (m_CurrentSketchSet == SketchSetType.Curated) &&
      (m_SketchSet.NumSketches <= 0) &&
      !m_SketchSet.IsActivelyRefreshingSketches);

    // Refresh all icons if necessary.
    if (!refreshIcons) {
      return;
    }

    for (int i = 0; i < Icons.Count; i++) {
      LoadSketchButton icon = Icons[i] as LoadSketchButton;
      // Default to loading image
      icon.SetButtonTexture(m_LoadingImageTexture);
      icon.ThumbnailLoaded = false;

      // Set sketch index relative to page based index
      int iSketchIndex = m_IndexOffset + i;
      if (iSketchIndex >= m_SketchSet.NumSketches) {
        iSketchIndex = -1;
      }
      icon.SketchIndex = iSketchIndex;
      icon.ResetScale();

      // Init icon according to availability of sketch
      GameObject go = icon.gameObject;
      if (m_SketchSet.IsSketchIndexValid(iSketchIndex)) {
        string sSketchName = m_SketchSet.GetSketchName(iSketchIndex);
        icon.SetDescriptionText(App.ShortenForDescriptionText(sSketchName));
        SceneFileInfo info = m_SketchSet.GetSketchSceneFileInfo(iSketchIndex);
        if (info.Available) {
          m_SketchSet.PrecacheSketchModels(iSketchIndex);
        }

        if (info.TriangleCount is int triCount) {
          icon.WarningVisible = triCount >
              QualityControls.m_Instance.AppQualityLevels.WarningPolySketchTriangles;
        } else {
          icon.WarningVisible = false;
        }
        go.SetActive(true);
      } else {
        go.SetActive(false);
      }
    }
  }

  void Update() {
    BaseUpdate();
    PageFlipUpdate();

    // Refresh icons while they are in flux
    if (m_SketchSet.IsReadyForAccess &&
        (!m_SketchSet.RequestedIconsAreLoaded ||
          !m_AllIconTexturesAssigned || !m_AllSketchesAreAvailable)) {
      UpdateIcons();
    }

    // Set icon uv offsets relative to head position.
    Vector3 head_LS = m_Mesh.transform.InverseTransformPoint(ViewpointScript.Head.position);
    float angleX = Vector3.Angle(Vector3.back, new Vector3(head_LS.x, 0.0f, head_LS.z));
    angleX *= (head_LS.x > 0.0f) ? -1.0f : 1.0f;

    float angleY = Vector3.Angle(Vector3.back, new Vector3(0.0f, head_LS.y, head_LS.z));
    angleY *= (head_LS.y > 0.0f) ? -1.0f : 1.0f;

    float maxAngleXRatio = angleX / 90.0f;
    float maxAngleYRatio = angleY / 90.0f;
    Vector2 offset = new Vector2(
        m_HalfInvUvScale.x + (m_HalfInvUvScale.x * maxAngleXRatio),
        m_HalfInvUvScale.y + (m_HalfInvUvScale.y * maxAngleYRatio));
    for (int i = 0; i < Icons.Count; i++) {
      LoadSketchButton icon = Icons[i] as LoadSketchButton;
      icon.UpdateUvOffsetAndScale(offset, m_SketchIconUvScale);
    }

    switch(m_CurrentSketchSet) {
    case SketchSetType.Curated:
      m_LoadingGallery.SetActive(m_SketchSet.IsActivelyRefreshingSketches);
      m_DriveSyncProgress.SetActive(false);
      m_SyncingDriveIcon.SetActive(false);
      m_DriveEnabledIcon.SetActive(false);
      m_DriveDisabledIcon.SetActive(false);
      m_DriveFullIcon.SetActive(false);
      break;
    case SketchSetType.Liked:
      m_LoadingGallery.SetActive(false);
      m_DriveSyncProgress.SetActive(false);
      m_SyncingDriveIcon.SetActive(false);
      m_DriveEnabledIcon.SetActive(false);
      m_DriveDisabledIcon.SetActive(false);
      m_DriveFullIcon.SetActive(false);
      break;
    case SketchSetType.User:
    case SketchSetType.Drive:
      bool sketchSetRefreshing = m_CurrentSketchSet == SketchSetType.Drive &&
                                 m_SketchSet.IsActivelyRefreshingSketches;
      bool driveSyncing = App.DriveSync.Syncing;
      bool syncEnabled = App.DriveSync.SyncEnabled;
      bool googleLoggedIn = App.GoogleIdentity.LoggedIn;
      bool driveFull = App.DriveSync.DriveIsLowOnSpace;
      m_LoadingGallery.SetActive(sketchSetRefreshing && !driveSyncing);
      m_DriveSyncProgress.SetActive(driveSyncing && !driveFull);
      m_SyncingDriveIcon.SetActive(driveSyncing && !driveFull);
      m_DriveEnabledIcon.SetActive(!driveFull && !driveSyncing && syncEnabled && googleLoggedIn);
      m_DriveDisabledIcon.SetActive(!syncEnabled && googleLoggedIn);
      m_DriveFullIcon.SetActive(driveFull && syncEnabled && googleLoggedIn);
      break;
    }

    // Check to see if whether "drive set has sketches" has changed.
    bool driveSetHasSketches =
        SketchCatalog.m_Instance.GetSet(SketchSetType.Drive).NumSketches != 0;
    if (m_DriveSetHasSketches != driveSetHasSketches) {
      m_DriveSetHasSketches = driveSetHasSketches;
      OnDriveSetHasSketchesChanged();
    }
  }

  // Whether or not the Google Drive set has any sketches impacts how the gallery buttons are
  // laid out.
  private void OnDriveSetHasSketchesChanged() {
    // Only show the Google Drive gallery tab if there are sketches in there.
    int galleryButtonAvailable = m_GalleryButtons.Length;
    int galleryButtonN;
    if (m_DriveSetHasSketches) {
      m_GalleryButtons[m_ElementNumberGalleryButtonDrive].gameObject.SetActive(true);
      galleryButtonN = galleryButtonAvailable;
    } else {
      m_GalleryButtons[m_ElementNumberGalleryButtonDrive].gameObject.SetActive(false);
      galleryButtonN = galleryButtonAvailable - 1;

      if (m_CurrentSketchSet == SketchSetType.Drive) {
        // We were on the Drive tab but it's gone away so switch to the local tab by simulating
        // the user pressing the local tab button.
        ButtonPressed(GalleryButton.Type.Local);
      }
    }

    // Position the gallery buttons so that they're centered.
    float buttonPosY = (0.5f * (galleryButtonN - 1) * m_GalleryButtonHeight
                        + kGalleryButtonLocalPadding);
    for (int i = 0; i < galleryButtonAvailable; i++) {
      if (i == m_ElementNumberGalleryButtonDrive && !m_DriveSetHasSketches) {
        continue;
      }
      Vector3 buttonPos = m_GalleryButtons[i].transform.localPosition;
      buttonPos.y = buttonPosY;
      m_GalleryButtons[i].transform.localPosition = buttonPos;
      buttonPosY -= m_GalleryButtonHeight;
      if (i == kElementNumberGalleryButtonLocal) {
        buttonPosY -= kGalleryButtonLocalPadding;
      }
    }
  }

  // UpdateIcons() is called repeatedly by Update() until these three conditions are met:
  // 1: The SketchSet has loaded all the requested icons
  // 2: The textures for all the buttons have been set
  // 3: (Cloud only) The SketchSet has downloaded the corresponding .tilt files.
  //    Until the .tilt file is downloaded we set a fade on the button, and need to keep updating
  //    until the file is downloaded.
  private void UpdateIcons() {
    m_AllIconTexturesAssigned = true;
    m_AllSketchesAreAvailable = true;

    // Poll sketch catalog until icons have loaded
    foreach (BaseButton baseButton in Icons) {
      LoadSketchButton icon = baseButton as LoadSketchButton;
      if (icon == null) { continue; }
      int iSketchIndex = icon.SketchIndex;
      if (m_SketchSet.IsSketchIndexValid(iSketchIndex)) {
        icon.FadeIn = m_SketchSet.GetSketchSceneFileInfo(iSketchIndex).Available ? 1f : 0.5f;

        if (!icon.ThumbnailLoaded) {
          Texture2D rTexture = null;
          string[] authors;
          string description;
          if (m_SketchSet.GetSketchIcon(iSketchIndex, out rTexture, out authors, out description)) {
            if (rTexture != null) {
              // Pass through aspect ratio of image so we don't get squished
              // thumbnails from Poly
              m_ImageAspect = (float)rTexture.width / rTexture.height;
              float aspect = m_ImageAspect;
              icon.SetButtonTexture(rTexture,aspect);
            } else {
              icon.SetButtonTexture(m_UnknownImageTexture);
            }

            // Mark the texture as assigned regardless of actual bits being valid
            icon.ThumbnailLoaded = true;;
            List<string> lines = new List<string>();
            lines.Add(icon.Description);

            SceneFileInfo info = m_SketchSet.GetSketchSceneFileInfo(iSketchIndex);
            if (info is PolySceneFileInfo polyInfo &&
                polyInfo.License != VrAssetService.kCreativeCommonsLicense) {
              lines.Add(String.Format("© {0}", authors[0]));
              lines.Add("All Rights Reserved");
            } else {
              // Include primary author in description if available
              if (authors != null && authors.Length > 0) {
                lines.Add(authors[0]);
              }
              // Include an actual description
              if (description != null) {
                lines.Add(App.ShortenForDescriptionText(description));
              }
            }
            icon.SetDescriptionText(lines.ToArray());
          } else {
            // While metadata has not finished loading, check if this file is valid
            bool bFileValid = false;
            SceneFileInfo rInfo = m_SketchSet.GetSketchSceneFileInfo(iSketchIndex);
            if (rInfo != null) {
              bFileValid = rInfo.Exists;
            }

            // If this file isn't valid, just keep the defaults and move on
            if (!bFileValid) {
              icon.SetButtonTexture(m_UnknownImageTexture);
              icon.ThumbnailLoaded = true;
            } else {
              m_AllIconTexturesAssigned = false;
            }
            if (!rInfo.Available) {
              m_AllSketchesAreAvailable = false;
            }
          }
        }
      }
    }
  }

  override public void OnUpdatePanel(Vector3 vToPanel, Vector3 vHitPoint) {
    base.OnUpdatePanel(vToPanel, vHitPoint);

    // Icons are active when animations aren't.
    bool bButtonsAvailable =
      (m_CurrentPageFlipState == PageFlipState.Standard) && (m_ActivePopUp == null);

    if (!PanelManager.m_Instance.IntroSketchbookMode) {
      if (bButtonsAvailable &&
          DoesRayHitCollider(m_ReticleSelectionRay, m_PaintButtonScript.GetCollider())) {
        m_PaintButtonScript.UpdateButtonState(m_InputValid);
      } else {
        m_PaintButtonScript.ResetState();
      }
    } else {
      if (bButtonsAvailable &&
          DoesRayHitCollider(m_ReticleSelectionRay, m_NewSketchButtonScript.GetCollider())) {
        m_NewSketchButtonScript.UpdateButtonState(m_InputValid);
      } else {
        m_NewSketchButtonScript.ResetState();
      }
    }
  }

  override protected void OnUpdateActive() {
    // If we're not active, hide all our preview panels
    if (!m_GazeActive) {
      m_OnlineGalleryButtonRenderer.material.SetFloat("_Grayscale", 1);
      m_ProfileButtonRenderer.material.SetFloat("_Grayscale", 1);

      for (int i = 0; i < m_IconScriptsOnFirstPage.Count; ++i) {
        m_IconScriptsOnFirstPage[i].ResetState();
      }
      for (int i = 0; i < m_IconScriptsOnNormalPage.Count; ++i) {
        m_IconScriptsOnNormalPage[i].ResetState();
      }
      if (m_NewSketchButtonScript) {
        m_NewSketchButtonScript.ResetState();
      }
      if (m_PaintButtonScript) {
        m_PaintButtonScript.ResetState();
      }
    } else if (m_CurrentState == PanelState.Available) {
      m_OnlineGalleryButtonRenderer.material.SetFloat("_Grayscale", 0);
      m_ProfileButtonRenderer.material.SetFloat("_Grayscale", 0);
      m_SketchSet.RequestRefresh();
    }
  }

  override protected void OnUpdateGazeBehavior(Color rPanelColor) {
    // Set the appropriate dim value for all our buttons and sliders
    if (Icons != null) {
      foreach (BaseButton icon in Icons) {
        icon.SetColor(rPanelColor);
      }
    }

    if (m_NewSketchButtonScript != null) {
      m_NewSketchButtonScript.SetColor(rPanelColor);
    }

    if (m_NavigationButtons != null) {
      for (int i = 0; i < m_NavigationButtons.Length; ++i) {
        m_NavigationButtons[i].SetColor(rPanelColor);
      }
    }
  }

  override public bool RaycastAgainstMeshCollider(Ray rRay, out RaycastHit rHitInfo, float fDist) {
    if (m_NewSketchButton.GetComponent<Collider>().Raycast(rRay, out rHitInfo, fDist)) {
      return true;
    }
    return base.RaycastAgainstMeshCollider(rRay, out rHitInfo, fDist);
  }

  // Works specifically with GalleryButtons.
  public void ButtonPressed(GalleryButton.Type rType, BaseButton button = null) {
    switch (rType) {
    case GalleryButton.Type.Exit:
      SketchSurfacePanel.m_Instance.EnableDefaultTool();
      PointerManager.m_Instance.EatLineEnabledInput();
      break;
    case GalleryButton.Type.Showcase:
      SetVisibleSketchSet(SketchSetType.Curated);
      break;
    case GalleryButton.Type.Local:
      SetVisibleSketchSet(SketchSetType.User);
      break;
    case GalleryButton.Type.Liked:
      SetVisibleSketchSet(SketchSetType.Liked);
      break;
    case GalleryButton.Type.Drive:
      SetVisibleSketchSet(SketchSetType.Drive);
      if (!m_ReadOnlyShown) {
        CreatePopUp(SketchControlsScript.GlobalCommands.ReadOnlyNotice,
            -1, -1, m_ReadOnlyPopupOffset);
        if (button != null) {
          button.ResetState();
        }
        m_ReadOnlyShown = true;
      }
      break;
    default:
      break;
    }
  }

  private void OnSketchSetDirty() {
    ComputeNumPages();

    SceneFileInfo first = (m_SketchSet.NumSketches > 0) ?
        m_SketchSet.GetSketchSceneFileInfo(0) : null;
    // If first sketch changed, return to first page.
    if (m_FirstSketch != null && !m_FirstSketch.Equals(first)) {
      PageIndex = 0;
    } else {
      PageIndex = Mathf.Min(PageIndex, m_NumPages - 1);
    }
    m_FirstSketch = first;
    GotoPage(PageIndex);
    UpdateIndexOffset();
    RefreshPage();
  }

  override protected void UpdateIndexOffset() {
    m_IndexOffset = PageIndex == 0 ? 0 : m_IconsOnFirstPage.Length + (PageIndex - 1) * Icons.Count;
  }
}
}  // namespace TiltBrush
