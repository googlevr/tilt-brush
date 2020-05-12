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

public enum PolySetType {
  User,
  Liked,
  Featured
}

public class PolyPanel : ModalPanel {
  [SerializeField] private TextMesh m_PanelText;
  [SerializeField] private TextMesh m_PanelTextSubtitle;
  [SerializeField] private TextMesh m_PanelTextUserSubtitle;
  [SerializeField] private string m_PanelTextStandard;
  [SerializeField] private string m_PanelTextFeatured;
  [SerializeField] private string m_PanelTextLiked;
  [SerializeField] private Renderer m_PolyGalleryRenderer;
  [SerializeField] private GameObject m_NoObjectsMessage;
  [SerializeField] private GameObject m_InternetError;
  [SerializeField] private GameObject m_NoAuthoredModelsMessage;
  [SerializeField] private GameObject m_NoLikesMessage;
  [SerializeField] private GameObject m_NotLoggedInMessage;
  [SerializeField] private GameObject m_OutOfDateMessage;
  [SerializeField] private GameObject m_NoPolyConnectionMessage;

  private PolySetType m_CurrentSet;
  private bool m_LoggedIn;

  // State for automatically loading models.
  int m_LastPageIndexForLoad = -1;
  PolySetType m_LastSetTypeForLoad = PolySetType.User;

  public bool ShowingFeatured { get { return m_CurrentSet == PolySetType.Featured; } }
  public bool ShowingLikes { get { return m_CurrentSet == PolySetType.Liked; } }
  public bool ShowingUser { get { return m_CurrentSet == PolySetType.User; } }

  override public void OnWidgetShowAnimComplete() {
    SetVisiblePolySet(m_CurrentSet);
  }

  public override void InitPanel() {
    base.InitPanel();

    m_LoggedIn = App.GoogleIdentity.LoggedIn;

    m_NoObjectsMessage.SetActive(false);
    m_InternetError.SetActive(false);
    m_NoAuthoredModelsMessage.SetActive(false);
    m_NoLikesMessage.SetActive(false);
    m_NotLoggedInMessage.SetActive(false);
    m_OutOfDateMessage.SetActive(false);
    m_NoPolyConnectionMessage.SetActive(false);
  }

  public override bool IsInButtonMode(ModeButton button) {
    var polySetButton = button as PolySetButton;
    return polySetButton && polySetButton.m_ButtonType == m_CurrentSet;
  }

  protected override void OnStart() {
    // Initialize icons.
    PolyModelButton[] rPanelButtons = m_Mesh.GetComponentsInChildren<PolyModelButton>();
    foreach (PolyModelButton icon in rPanelButtons) {
      GameObject go = icon.gameObject;
      icon.SetButtonGrayscale(icon);
      go.SetActive(false);
      Icons.Add(icon);
    }

    m_CurrentSet = PolySetType.Featured;

    // Make sure Poly gallery button starts at greyscale when panel is initialized
    m_PolyGalleryRenderer.material.SetFloat("_Grayscale", 1);

    App.PolyAssetCatalog.RequestRefresh(m_CurrentSet);
    App.PolyAssetCatalog.CatalogChanged += OnPolyAssetCatalogChanged;
  }

  void SetVisiblePolySet(PolySetType type) {
    m_CurrentSet = type;
    App.PolyAssetCatalog.RequestRefresh(m_CurrentSet);
    ResetPageIndex();
    RefreshPage();
  }

  void OnPolyAssetCatalogChanged() {
    RefreshPage();
  }

  protected override void RefreshPage() {
    m_NoLikesMessage.SetActive(false);
    m_NoAuthoredModelsMessage.SetActive(false);
    m_NotLoggedInMessage.SetActive(false);
    if (VrAssetService.m_Instance.NoConnection) {
      m_NoPolyConnectionMessage.SetActive(true);
      RefreshPanelText();
      base.RefreshPage();
      return;
    }
    if (!VrAssetService.m_Instance.Available) {
      m_OutOfDateMessage.SetActive(true);
      RefreshPanelText();
      base.RefreshPage();
      return;
    }

    m_NumPages = ((App.PolyAssetCatalog.NumCloudModels(m_CurrentSet) - 1) / Icons.Count) + 1;
    int numCloudModels = App.PolyAssetCatalog.NumCloudModels(m_CurrentSet);

    if (m_LastPageIndexForLoad != PageIndex || m_LastSetTypeForLoad != m_CurrentSet) {
      // Unload the previous page's models.

      // This function may be called multiple times as icons load, only unload the old models once,
      // otherwise the current page's models will be thrashed.
      m_LastPageIndexForLoad = PageIndex;
      m_LastSetTypeForLoad = m_CurrentSet;

      // Destroy previews so only the thumbnail is visible.
      for (int i = 0; i < Icons.Count; i++) {
        ((ModelButton)Icons[i]).DestroyModelPreview();
      }

      App.PolyAssetCatalog.UnloadUnusedModels();
    }

    for (int i = 0; i < Icons.Count; i++) {
      PolyModelButton icon = (PolyModelButton)Icons[i];
      // Set sketch index relative to page based index
      int iMapIndex = m_IndexOffset + i;

      // Init icon according to availability of sketch
      GameObject go = icon.gameObject;
      if (iMapIndex < numCloudModels) {
        PolyAssetCatalog.AssetDetails asset =
            App.PolyAssetCatalog.GetPolyAsset(m_CurrentSet, iMapIndex);
        go.SetActive(true);

        if (icon.Asset != null && asset.AssetId != icon.Asset.AssetId) {
          icon.DestroyModelPreview();
        }
        icon.SetPreset(asset, iMapIndex);

        // Note that App.UserConfig.Flags.PolyModelPreload falls through to
        // App.PlatformConfig.EnablePolyPreload if it isn't set in Tilt Brush.cfg.
        if (App.UserConfig.Flags.PolyModelPreload) {
          icon.RequestModelPreload(PageIndex);
        }
      } else {
        go.SetActive(false);
      }
    }

    bool internetError =
        App.PolyAssetCatalog.NumCloudModels(PolySetType.Featured) == 0;
    m_InternetError.SetActive(internetError);

    RefreshPanelText();
    switch (m_CurrentSet) {
    case PolySetType.User:
      if (!internetError) {
        if (App.GoogleIdentity.LoggedIn) {
          if (numCloudModels == 0) {
            m_NoAuthoredModelsMessage.SetActive(true);
          }
        } else {
          m_NotLoggedInMessage.SetActive(true);
        }
      }
      break;
    case PolySetType.Liked:
      if (!internetError) {
        if (App.GoogleIdentity.LoggedIn) {
          if (numCloudModels == 0) {
            m_NoLikesMessage.SetActive(true);
          }
        } else {
          m_NotLoggedInMessage.SetActive(true);
        }
      }
      break;
    }

    base.RefreshPage();
  }
  
  void RefreshPanelText() {
    switch (m_CurrentSet) {
    case PolySetType.User:
      m_PanelText.text = m_PanelTextStandard;
      m_PanelTextSubtitle.gameObject.SetActive(false);
      m_PanelTextUserSubtitle.gameObject.SetActive(true);
      break;
    case PolySetType.Featured:
      m_PanelText.text = m_PanelTextFeatured;
      m_PanelTextSubtitle.gameObject.SetActive(false);
      m_PanelTextUserSubtitle.gameObject.SetActive(false);
      break;
    case PolySetType.Liked:
      m_PanelText.text = m_PanelTextLiked;
      m_PanelTextSubtitle.gameObject.SetActive(true);
      m_PanelTextUserSubtitle.gameObject.SetActive(false);
      break;
    }
  }

  void Update() {
    BaseUpdate();

    // Update share button's text.
    bool loggedIn = App.GoogleIdentity.LoggedIn;
    if (loggedIn != m_LoggedIn) {
      m_LoggedIn = loggedIn;
      RefreshPage();
    }

    PageFlipUpdate();
  }

  override protected void OnUpdateActive() {
    // If we're not active, hide all our preview panels
    if (!m_GazeActive) {
      m_PolyGalleryRenderer.material.SetFloat("_Grayscale", 1);
      foreach (var baseButton in Icons) {
        PolyModelButton icon = (PolyModelButton)baseButton;
        icon.ResetState();
        icon.SetButtonGrayscale(icon);
      }

    } else if (m_CurrentState == PanelState.Available) {
      m_PolyGalleryRenderer.material.SetFloat("_Grayscale", 0);
      foreach (var baseButton in Icons) {
        PolyModelButton icon = (PolyModelButton)baseButton;
        icon.SetButtonGrayscale(false);
      }
    }
  }

  // Works specifically with PolySetButtons.
  public void ButtonPressed(PolySetType rType) {
    SetVisiblePolySet(rType);
  }

}
}  // namespace TiltBrush
