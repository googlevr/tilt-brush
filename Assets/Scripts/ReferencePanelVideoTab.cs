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
public class ReferencePanelVideoTab : ReferencePanelTab {

  // Subclass used to display a video button within the reference tab.
  public class VideoIcon : ReferenceIcon {
    public ReferencePanel Parent { get; set; }
    public bool TextureAssigned { get; set; }

    public VideoButton VideoButton {
      get { return Button as VideoButton; }
    }

    public override void Refresh(int catalogIndex) {
      Button.SetButtonTexture(Parent.UnknownImageTexture, 1);

      var video = VideoCatalog.Instance.GetVideoAtIndex(catalogIndex);
      VideoButton.Video = video;
      VideoButton.RefreshDescription();

      // init the icon according to availability of video
      if (video != null) {
        Button.gameObject.SetActive(true);
        TextureAssigned = false;
      } else {
        Button.gameObject.SetActive(false);
        TextureAssigned = true;
      }
    }
  }

  [SerializeField] private GameObject m_VideoControls;
  [SerializeField] private BoxCollider m_VideoControlsCollider;
  [SerializeField] private GameObject m_Preview;
  [SerializeField] private VideoPositionSlider m_Scrubber;
  [SerializeField] private float m_VideoSkipTime = 10f;
  [SerializeField] private Texture2D m_ErrorTexture;
  [SerializeField] private Texture2D m_LoadingTexture;

  private bool m_AllIconTexturesAssigned;
  private VideoWidget m_SelectedVideoWidget;
  private Material m_PreviewMaterial;
  private bool m_TabActive;

  [System.Reflection.Obfuscation(Exclude=true)]
  public bool SelectedVideoIsPlaying {
    get {
      return (SelectedVideo != null)
          ? SelectedVideo.Playing
          : false;
    }
    set {
      if (SelectedVideo != null) {
        SelectedVideo.Playing = !SelectedVideo.Playing;
      }
    }
  }

  [System.Reflection.Obfuscation(Exclude=true)]
  public float SelectedVideoVolume {
    get {
      return (SelectedVideo != null)
          ? SelectedVideo.Volume
          : 0f;
    }
    set {
      if (SelectedVideo != null) {
        SelectedVideo.Volume = value;
      }
    }
  }

  public override IReferenceItemCatalog Catalog {
    get { return VideoCatalog.Instance; }
  }
  public override ReferenceButton.Type ReferenceButtonType {
    get { return ReferenceButton.Type.Videos; }
  }
  protected override Type ButtonType {
    get { return typeof(VideoButton); }
  }
  protected override Type IconType {
    get { return typeof(VideoIcon); }
  }

  protected ReferenceVideo.Controller SelectedVideo {
    get { return m_SelectedVideoWidget != null ? m_SelectedVideoWidget.VideoController : null; }
  }

  void RefreshVideoControlsVisibility() {
    if (m_VideoControls != null) {
      bool widgetActive = WidgetManager.m_Instance != null &&
          WidgetManager.m_Instance.AnyVideoWidgetActive;
      m_VideoControls.SetActive(m_TabActive && widgetActive);
    }
  }

  public override void OnTabEnable() {
    m_TabActive = true;
    RefreshVideoControlsVisibility();
  }

  public override void OnTabDisable() {
    m_TabActive = false;
    RefreshVideoControlsVisibility();
  }

  public override void RefreshTab(bool selected) {
    base.RefreshTab(selected);
    if (selected) {
      m_AllIconTexturesAssigned = false;
    }
    m_TabActive = selected;
    RefreshVideoControlsVisibility();
  }

  public override void InitTab() {
    base.InitTab();
    foreach (var icon in m_Icons) {
      (icon as VideoIcon).Parent = GetComponentInParent<ReferencePanel>();
    }
    OnTabDisable();
    App.Switchboard.VideoWidgetActivated += OnVideoWidgetActivated;
    m_PreviewMaterial = m_Preview.GetComponent<Renderer>().material;
    m_PreviewMaterial.mainTexture = Texture2D.blackTexture;
  }

  public void OnVideoWidgetActivated(VideoWidget widget) {
    m_SelectedVideoWidget = widget;
    if (widget.VideoController != null) {
      m_PreviewMaterial.mainTexture = widget.VideoController.VideoTexture;
    }
    m_Preview.transform.localScale = new Vector3(widget.Video.Aspect, 1f, 1f);
    m_Scrubber.VideoWidget = widget;
    RefreshVideoControlsVisibility();
  }

  public override void UpdateTab() {
    base.UpdateTab();
    if (!m_AllIconTexturesAssigned) {
      m_AllIconTexturesAssigned = true;

      //poll sketch catalog until icons have loaded
      for (int i = 0; i < m_Icons.Length; ++i) {
        var imageIcon = m_Icons[i] as VideoIcon;
        if (!imageIcon.TextureAssigned && imageIcon.Button.gameObject.activeSelf) {
          int catalogIndex = m_IndexOffset + i;

          var video = VideoCatalog.Instance.GetVideoAtIndex(catalogIndex);
          if (video != null) {
            if (!string.IsNullOrEmpty(video.Error)) {
              imageIcon.Button.SetButtonTexture(m_ErrorTexture,
                  m_ErrorTexture.width / m_ErrorTexture.height);
              imageIcon.TextureAssigned = true;
              imageIcon.VideoButton.SetDescriptionText(video.HumanName, "Could not load video.");
              imageIcon.VideoButton.SetButtonAvailable(false);
            } else if (video.IsInitialized) {
              imageIcon.Button.SetButtonTexture(video.Thumbnail, video.Aspect);
              imageIcon.TextureAssigned = true;
            } else {
              imageIcon.Button.SetButtonTexture(m_LoadingTexture,
                  m_LoadingTexture.width / m_LoadingTexture.height);
              imageIcon.TextureAssigned = true;
            }
          } else {
            m_AllIconTexturesAssigned = false;
          }
        }
      }
    }
  }

  public override void OnUpdateGazeBehavior(Color panelColor, bool gazeActive, bool available) {
    base.OnUpdateGazeBehavior(panelColor, gazeActive, available);
    bool? buttonsGrayscale = null;
    if (!gazeActive) {
      buttonsGrayscale = true;
    } else if (available) {
      buttonsGrayscale = false;
    } else {
      // Don't mess with grayscale-ness
    }

    if (buttonsGrayscale != null) {
      foreach (var icon in m_Icons) {
        icon.Button.SetButtonGrayscale(buttonsGrayscale.Value);
      }
    }
  }

  public override bool RaycastAgainstMeshCollider(Ray ray, out RaycastHit hitInfo, float dist) {
    if (base.RaycastAgainstMeshCollider(ray, out hitInfo, dist)) {
      return true;
    }
    if (m_VideoControlsCollider == null) {
      return false;
    }
    return m_VideoControlsCollider.Raycast(ray, out hitInfo, dist);
  }

  [System.Reflection.Obfuscation(Exclude=true)]
  public void SkipBack() {
    if (SelectedVideo == null) {
      return;
    }
    SelectedVideo.Time = Mathf.Clamp(SelectedVideo.Time - m_VideoSkipTime, 0, SelectedVideo.Length);
  }

  [System.Reflection.Obfuscation(Exclude=true)]
  public void SkipForward() {
    if (SelectedVideo == null) {
      return;
    }
    SelectedVideo.Time = Mathf.Clamp(SelectedVideo.Time + m_VideoSkipTime, 0, SelectedVideo.Length);
  }
}
} // namespace TiltBrush
