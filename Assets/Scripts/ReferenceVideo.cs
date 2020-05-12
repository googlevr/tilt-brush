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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;

namespace TiltBrush {
[System.Serializable]
public class ReferenceVideo {
  /// The controller is used as a handle for controlling the video - videos stay instantiated as
  /// long as there are controllers in existence that reference them. For this reason it is
  /// important to Dispose() of controllers once they are no longer needed.
  ///
  /// Properties of videos that can change (playing state, volume, scrub position) are all accessed
  /// through the Controller - properties of videos that are unchanging are accessed through the
  /// ReferenceVideo.
  public class Controller : IDisposable {
    private Action m_OnVideoInitialized;
    private ReferenceVideo m_ReferenceVideo;
    private bool m_VideoInitialized;

    public bool Initialized => m_VideoInitialized;

    /// Videos do not start playing immediately; this event is triggered when the video is ready.
    /// However, as several controllers may point at the same video, if a controller is made to
    /// point at an already playing video, when a user adds a value to OnVideoInitialized, the event
    /// will be made to trigger immediately. The event is always cleared after triggering so this
    /// will not cause OnVideoInitialized functions to be called more than once.
    public event Action OnVideoInitialized {
      add {
        m_OnVideoInitialized += value;
        if (m_VideoInitialized) {
          OnInitialization();
        }
      }
      remove { m_OnVideoInitialized -= value; }
    }

    private VideoPlayer VideoPlayer => m_ReferenceVideo.m_VideoPlayer;

    public Texture VideoTexture {
      get { return m_VideoInitialized ? VideoPlayer.texture : null; }
    }

    public bool Playing {
      get => m_VideoInitialized ? VideoPlayer.isPlaying : false;
      set {
        if (m_VideoInitialized) {
          if (VideoPlayer.isPlaying) {
            VideoPlayer.Pause();
          } else {
            VideoPlayer.Play();
          }
        }
      }
    }

    public float Volume {
      get => (!m_VideoInitialized || VideoPlayer.GetDirectAudioMute(0))
          ? 0f : VideoPlayer.GetDirectAudioVolume(0);
      set {
        if (m_VideoInitialized) {
          if (value <= 0.005f) {
            VideoPlayer.SetDirectAudioVolume(0, 0f);
            VideoPlayer.SetDirectAudioMute(0, true);
          } else {
            VideoPlayer.SetDirectAudioMute(0, false);
            VideoPlayer.SetDirectAudioVolume(0, value);
          }
        }
      }
    }

    public float Position {
      get => m_VideoInitialized ? (float) (VideoPlayer.time / VideoPlayer.length) : 0f;
      set {
        if (m_VideoInitialized) {
          VideoPlayer.time = VideoPlayer.length * Mathf.Clamp01(value);
        }
      }
    }

    public float Time {
      get => m_VideoInitialized ? (float) VideoPlayer.time : 0f;
      set {
        if (m_VideoInitialized) {
          VideoPlayer.time = Mathf.Clamp(value, 0, (float)VideoPlayer.length);
        }
      }
    }

    public float Length => m_VideoInitialized ? (float) VideoPlayer.length : 0f;

    public Controller(ReferenceVideo referenceVideo) {
      m_ReferenceVideo = referenceVideo;
      if (m_ReferenceVideo.m_VideoPlayer != null) {
        m_VideoInitialized = m_ReferenceVideo.m_VideoPlayer.isPrepared;
      }
    }

    public Controller(Controller other) {
      m_ReferenceVideo = other.m_ReferenceVideo;
      m_VideoInitialized = other.m_VideoInitialized;
      m_ReferenceVideo.m_Controllers.Add(this);
    }

    public void Dispose() {
      if (m_ReferenceVideo != null) {
        m_ReferenceVideo.OnControllerDisposed(this);
        m_ReferenceVideo = null;
      }
    }

    public void OnInitialization() {
      m_VideoInitialized = true;
      m_OnVideoInitialized?.Invoke();
      m_OnVideoInitialized = null;
    }
  }

  public static ReferenceVideo CreateDummyVideo() {
    return new ReferenceVideo();
  }

  private VideoPlayer m_VideoPlayer;
  private HashSet<Controller> m_Controllers = new HashSet<Controller>();

  /// Persistent path is relative to the Tilt Brush/Media Library/Videos directory, if it is a
  /// filename.
  public string PersistentPath { get; }
  public string AbsolutePath { get; }
  public bool NetworkVideo { get; }
  public string HumanName { get; }

  public Texture2D Thumbnail { get; private set; }

  public uint Width { get; private set; }

  public uint Height { get; private set; }

  public float Aspect { get; private set; }

  public bool IsInitialized { get; private set; }

  public bool HasInstances => m_Controllers.Count > 0;

  public string Error { get; private set; }

  public ReferenceVideo(string filePath) {
    NetworkVideo = filePath.EndsWith(".txt");
    PersistentPath = filePath.Substring(App.VideoLibraryPath().Length + 1);
    HumanName = System.IO.Path.GetFileName(PersistentPath);
    AbsolutePath = filePath;
  }

  // Dummy ReferenceVideo - this is used when a video referenced in a sketch cannot be found.
  private ReferenceVideo() {
    IsInitialized = false;
    Width = 160;
    Height = 90;
    Aspect = 16 / 9f;
    PersistentPath = "";
    AbsolutePath = "";
    NetworkVideo = false;
    HumanName = "";
  }

  /// Creates a controller for this reference video. Controllers are Disposable and it is important
  /// to Dispose a controller after it is finished with. If disposal does not happen, then the
  /// video decoder will keep decoding, using up memory and bandwidth. If the audio is turned on
  /// then the audio will continue. DISPOSE OF YOUR CONTROLLERS.
  public Controller CreateController() {
    Controller controller = new Controller(this);
    bool alreadyPrepared = HasInstances;
    m_Controllers.Add(controller);
    if (!alreadyPrepared) {
      VideoCatalog.Instance.StartCoroutine(PrepareVideoPlayer(InitializeControllers));
    }
    return controller;
  }

  private void InitializeControllers() {
    foreach (var controller in m_Controllers) {
      controller.OnInitialization();
    }
  }

  private void OnControllerDisposed(Controller controller) {
    m_Controllers.Remove(controller);
    if (!HasInstances && m_VideoPlayer != null) {
      m_VideoPlayer.Stop();
      UnityEngine.Object.Destroy(m_VideoPlayer.gameObject);
      m_VideoPlayer = null;
    }
  }

  private IEnumerator<Null> PrepareVideoPlayer(Action onCompletion) {
    Error = null;
    var gobj = new GameObject(HumanName);
    gobj.transform.SetParent(VideoCatalog.Instance.gameObject.transform);
    try {
      m_VideoPlayer = gobj.AddComponent<VideoPlayer>();
      m_VideoPlayer.playOnAwake = false;
      if (NetworkVideo) {
        if (System.IO.File.Exists(AbsolutePath)) {
          m_VideoPlayer.url = System.IO.File.ReadAllText(AbsolutePath);
        }
      } else {
        string fullPath = System.IO.Path.Combine(App.VideoLibraryPath(), PersistentPath);
        m_VideoPlayer.url = $"file:///{fullPath}";
      }
      m_VideoPlayer.isLooping = true;
      m_VideoPlayer.renderMode = VideoRenderMode.APIOnly;
      m_VideoPlayer.skipOnDrop = true;
      m_VideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
      m_VideoPlayer.Prepare();
      m_VideoPlayer.errorReceived += OnError;
    } catch (Exception ex) {
      Debug.LogException(ex);
      Error = ex.Message;
      yield break;
    }

    while (!m_VideoPlayer.isPrepared) {
      if (Error != null) {
        yield break;
      }
      yield return null;
    }

    // This code is *super* useful for testing the reference video panel, and I've written it at
    // least five times, so I'd like to just leave it here as it may well be useful in the future.
#if false
    // Delays the video load by two seconds
    for (var wait = DateTime.Now + TimeSpan.FromSeconds(2); wait > DateTime.Now;) {
      yield return null;
    }
#endif
    
    Width = m_VideoPlayer.width;
    Height = m_VideoPlayer.height;
    Aspect = Width / (float) Height;

    for (int i = 0; i < m_VideoPlayer.audioTrackCount; ++i) {
      m_VideoPlayer.SetDirectAudioMute((ushort)i, true);
    }

    m_VideoPlayer.Play();

    if (onCompletion != null) {
      onCompletion();
    }
  }

  private void OnError(VideoPlayer player, string error) {
    Error = error;
  }

  public IEnumerator<Null> Initialize() {
    Controller thumbnailExtractor = CreateController();
    while (!thumbnailExtractor.Initialized) {
      if (Error != null) {
        thumbnailExtractor.Dispose();
        yield break;
      }
      yield return null;
    }
    int width, height;
    if (Aspect > 1) {
      width = 128;
      height = Mathf.RoundToInt(width / Aspect);
    } else {
      height = 128;
      width = Mathf.RoundToInt(height * Aspect);
    }
    // A frame does not always seem to be immediately available, so wait until we've hit at least
    // the second frame before continuing.
    while (m_VideoPlayer.frame < 1) {
      yield return null;
    }
    // Because the Thumbnail needs to be a Texture2D, we need to do the little dance of copying
    // the rendertexture over to the Texture2D.
    var rt = RenderTexture.GetTemporary(width, height, 0);
    Graphics.Blit(m_VideoPlayer.texture, rt);
    Thumbnail = new Texture2D(width, height, TextureFormat.RGB24, false);
    var oldActive = RenderTexture.active;
    RenderTexture.active = rt;
    Thumbnail.ReadPixels(new Rect(0,0, width, height),0, 0 );
    RenderTexture.active = oldActive;
    Thumbnail.Apply(false);
    RenderTexture.ReleaseTemporary(rt);
    thumbnailExtractor.Dispose();
    IsInitialized = true;
  }

  public void Dispose() {
    if (m_VideoPlayer != null) {
      Debug.Assert(m_Controllers.Count > 0,
          "There should be controllers if the VideoPlayer is not null.");
      foreach (var controller in m_Controllers.ToArray()) {
        // Controller.Dispose handles removing itself from m_Controllers, so we don't do it here.
        controller.Dispose();
      }
    }
    if (Thumbnail != null) {
      UnityEngine.Object.Destroy(Thumbnail);
    }
  }

  public override string ToString() {
    return $"{HumanName}: {Width}x{Height} {Aspect}";
  }
}
} // namespace TiltBrush
