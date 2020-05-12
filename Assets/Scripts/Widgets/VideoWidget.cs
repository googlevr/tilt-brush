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

using System.IO;

namespace TiltBrush {
  public class VideoWidget : Media2dWidget {
    // VideoState is used to restore the state of a video when loading, or when a videowidget is
    // restored from being tossed with an undo.
    private class VideoState {
      public bool Paused;
      public float Volume;
      public float? Time;
    }

    private ReferenceVideo m_Video;
    private VideoState m_InitialState;

    public ReferenceVideo Video {
      get { return m_Video; }
    }

    public ReferenceVideo.Controller VideoController { get; private set; }

    public void SetVideo(ReferenceVideo video) {
      m_Video = video;
      ImageTexture = m_NoImageTexture;

      var size = GetWidgetSizeRange();
      if (m_Video.Aspect > 1) {
        m_Size = Mathf.Clamp(2 / m_Video.Aspect / Coords.CanvasPose.scale, size.x, size.y);
      } else {
        m_Size = Mathf.Clamp(2 * m_Video.Aspect / Coords.CanvasPose.scale, size.x, size.y);
      }

      // Create in the main canvas.
      HierarchyUtils.RecursivelySetLayer(transform, App.Scene.MainCanvas.gameObject.layer);
      HierarchyUtils.RecursivelySetMaterialBatchID(transform, m_BatchId);

      InitSnapGhost(m_ImageQuad.transform, transform);
      Play();
    }

    public override float? AspectRatio => m_Video?.Aspect;

    protected override void OnShow() {
      base.OnShow();
      Play();
    }

    public override void RestoreFromToss() {
      base.RestoreFromToss();
      Play();
    }

    protected override void OnHide() {
      base.OnHide();
      // store off the video state so that if the widget gets shown again it will reset to that.
      if (VideoController != null) {
        m_InitialState = new VideoState {
            Paused = !VideoController.Playing,
            Time = VideoController.Time,
            Volume = VideoController.Volume,
        };
        VideoController.Dispose();
        VideoController = null;
      }
    }

    protected override void OnDestroy() {
      base.OnDestroy();
      VideoController?.Dispose();
      VideoController = null;
    }

    private void Play() {
      if (m_Video == null || VideoController != null) {
        return;
      }
      // If instances of the video already exist, don't override with new state.
      if (m_Video.HasInstances) {
        m_InitialState = null;
      }
      VideoController = m_Video.CreateController();
      VideoController.OnVideoInitialized += OnVideoInitialized;
    }

    private void OnVideoInitialized() {
      ImageTexture = VideoController.VideoTexture;
      UpdateScale();
      if (m_InitialState != null) {
        VideoController.Volume = m_InitialState.Volume;
        if (m_InitialState.Time.HasValue) {
          VideoController.Time = m_InitialState.Time.Value;
        }
        if (m_InitialState.Paused) {
          VideoController.Playing = false;
        }
        m_InitialState = null;
      }
    }

    public static void FromTiltVideo(TiltVideo tiltVideo) {
      VideoWidget videoWidget = Instantiate(WidgetManager.m_Instance.VideoWidgetPrefab);
      videoWidget.m_LoadingFromSketch = true;
      videoWidget.transform.parent = App.Instance.m_CanvasTransform;
      videoWidget.transform.localScale = Vector3.one;

      var video = VideoCatalog.Instance.GetVideoByPersistentPath(tiltVideo.FilePath);
      if (video == null) {
        video = ReferenceVideo.CreateDummyVideo();
        ControllerConsoleScript.m_Instance.AddNewLine(
            $"Could not find video {App.VideoLibraryPath()}\\{tiltVideo.FilePath}.");
      }
      videoWidget.SetVideo(video);
      videoWidget.m_InitialState = new VideoState {
          Volume = tiltVideo.Volume,
          Paused = tiltVideo.Paused,
      };
      if (tiltVideo.Paused) {
        videoWidget.m_InitialState.Time = tiltVideo.Time;
      }
      videoWidget.SetSignedWidgetSize(tiltVideo.Transform.scale);
      videoWidget.Show(bShow: true, bPlayAudio: false);
      videoWidget.transform.localPosition = tiltVideo.Transform.translation;
      videoWidget.transform.localRotation = tiltVideo.Transform.rotation;
      if (tiltVideo.Pinned) {
        videoWidget.PinFromSave();
      }
      videoWidget.Group = App.GroupManager.GetGroupFromId(tiltVideo.GroupId);
      TiltMeterScript.m_Instance.AdjustMeterWithWidget(videoWidget.GetTiltMeterCost(), up: true);
      videoWidget.UpdateScale();
    }

    public override GrabWidget Clone() {
      VideoWidget clone = Instantiate(WidgetManager.m_Instance.VideoWidgetPrefab) as VideoWidget;
      clone.m_LoadingFromSketch = true;  // prevents intro animation
      clone.m_TransitionScale = 1.0f;
      clone.transform.parent = transform.parent;
      clone.SetVideo(m_Video);
      clone.SetSignedWidgetSize(m_Size);
      clone.Show(bShow: true, bPlayAudio: false);
      clone.transform.position = transform.position;
      clone.transform.rotation = transform.rotation;
      HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
      TiltMeterScript.m_Instance.AdjustMeterWithWidget(clone.GetTiltMeterCost(), up: true);
      clone.CloneInitialMaterials(this);
      clone.TrySetCanvasKeywordsFromObject(transform);
      return clone;
    }

    public override void Activate(bool bActive) {
      base.Activate(bActive);
      if (bActive) {
        App.Switchboard.TriggerVideoWidgetActivated(this);
      }
    }

    public override string GetExportName() {
      return Path.GetFileNameWithoutExtension(m_Video.AbsolutePath);
    }
  }
} // namespace TiltBrush
