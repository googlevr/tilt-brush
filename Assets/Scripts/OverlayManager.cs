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
using System.Collections.Generic;
using System.Threading.Tasks;

#if !OCULUS_SUPPORTED
using OVROverlay = UnityEngine.MonoBehaviour;
#endif // !OCULUS_SUPPORTED

namespace TiltBrush {
public enum OverlayType {
  LoadSketch,
  LoadModel,
  LoadGeneric,
  LoadImages,
  Export,
  LoadMedia,
}

public enum OverlayState {
  Hidden,
  Visible,
  Exiting
}

public class OverlayManager : MonoBehaviour {
  public static OverlayManager m_Instance;

  [SerializeField] private float m_OverlayOffsetDistance;
  [SerializeField] private float m_OverlayHeight;
  [SerializeField] private Texture m_BlackTexture;
  [SerializeField] private float m_OverlayStateTransitionDuration = 1.0f;
  
  [SerializeField] private Material m_Material;
  [SerializeField] private Font m_Font;
  [SerializeField] private Rect m_LogoArea;
  [SerializeField] private Rect m_TextArea;
  [SerializeField] private int m_FontSize;
  [SerializeField] private int m_Size;
  [SerializeField] private Color m_BackgroundColor;
  [SerializeField] private Color m_TextColor;

  private Progress<double> m_progress;
  private RenderTexture m_GUILogo;
  private bool m_RefuseProgressChanges;
  private OverlayState m_CurrentOverlayState;
  private float m_OverlayStateTransitionValue;
  private OverlayType m_CurrentOverlayType;

  private Vector3[] m_TextVertices;
  private Vector3[] m_TextUvs;
  

  /// An IProgress for you to use with your RunInCompositor tasks/coroutines
  public IProgress<double> Progress => m_progress;

  public bool CanDisplayQuickloadOverlay {
    get { return !App.VrSdk.OverlayEnabled || m_CurrentOverlayType == OverlayType.LoadSketch; }
  }

  public OverlayState CurrentOverlayState => m_CurrentOverlayState;

  void Awake() {
    m_Instance = this;
    m_progress = new Progress<double>();
    m_progress.ProgressChanged += OnProgressChanged;
    m_CurrentOverlayState = OverlayState.Hidden;
    m_OverlayStateTransitionValue = 0.0f;
    m_GUILogo = new RenderTexture(m_Size, m_Size, 0);
    SetText("");
    RenderLogo(0.45f);
  }

  void Update() {
    switch (m_CurrentOverlayState) {
    case OverlayState.Exiting:
      m_OverlayStateTransitionValue -= Time.deltaTime;
      App.VrSdk.SetOverlayAlpha(
        Mathf.Max(m_OverlayStateTransitionValue, 0.0f) / m_OverlayStateTransitionDuration);
      if (m_OverlayStateTransitionValue <= 0.0f) {
        m_OverlayStateTransitionValue = 0.0f;
        m_CurrentOverlayState = OverlayState.Hidden;
        App.VrSdk.OverlayEnabled = false;
      }
      break;
    case OverlayState.Hidden:
    case OverlayState.Visible:
    default: break;
    }
  }

  void OnGUI() {
    if (App.VrSdk.OverlayEnabled) {
      GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), m_BlackTexture);
      GUI.DrawTexture(new Rect(Screen.width / 2 - Screen.height / 4, Screen.height / 4,
        Screen.height / 2, Screen.height / 2), m_GUILogo);
    }
  }

  public void SetOverlayFromType(OverlayType type) {
    m_CurrentOverlayType = type;
    switch (type) {
    case OverlayType.LoadSketch:
      SetText("Loading Sketch...");
      RenderLogo(0);
      App.VrSdk.SetOverlayTexture(m_GUILogo);
      break;
    case OverlayType.LoadModel:
      SetText("Loading Models...");
      RenderLogo(0);
      App.VrSdk.SetOverlayTexture(m_GUILogo);
      break;
    case OverlayType.LoadGeneric:
      SetText("Loading...");
      RenderLogo(0);
      App.VrSdk.SetOverlayTexture(m_GUILogo);
      break;
    case OverlayType.LoadImages:
      SetText("Loading Images...");
      RenderLogo(0);
      App.VrSdk.SetOverlayTexture(m_GUILogo);
      break;
    case OverlayType.Export:
      SetText("Exporting...");
      RenderLogo(0);
      App.VrSdk.SetOverlayTexture(m_GUILogo);
      break;
    case OverlayType.LoadMedia:
      SetText("Loading Media...");
      RenderLogo(0);
      App.VrSdk.SetOverlayTexture(m_GUILogo);
      break;
    }
  }

  // This is used when we pass m_progress to a Task
  // It is guaranteed to be called on the Unity thread.
  private void OnProgressChanged(object sender, double value) {
    UpdateProgress((float)value);
  }

  public void UpdateProgress(float fProg, bool bForce = false) {
    if (m_RefuseProgressChanges && !bForce) { return; }
    RenderLogo(fProg);
  }

  public void RefuseProgressBarChanges(bool bRefuse) {
    m_RefuseProgressChanges = bRefuse;
  }

  /// Like RunInCompositor, but allows you to pass an IEnumerator(float).
  /// The float being yielded should be a progress value between 0 and 1.
  public IEnumerator<Null> RunInCompositorWithProgress(
      OverlayType overlayType,
      IEnumerator<float> coroutineWithProgress,
      float fadeDuration) {
    return RunInCompositor(
        overlayType,
        ConsumeAsProgress(coroutineWithProgress),
        fadeDuration,
        false);
  }

  /// Like RunInCompositor but for non-coroutines
  public IEnumerator<Null> RunInCompositor(
      OverlayType overlayType,
      System.Action action,
      float fadeDuration,
      bool bFullProgress = false,
      bool showSuccessText = false) {
    return RunInCompositor(overlayType, AsCoroutine(action), fadeDuration,
        bFullProgress, showSuccessText);
  }

  public IEnumerator<Null> RunInCompositor(
      OverlayType overlayType,
      IEnumerator<Null> action,
      float fadeDuration,
      bool bFullProgress = false,
      bool showSuccessText = false) {
    SetOverlayFromType(overlayType);
    UpdateProgress(bFullProgress ? 1.0f : 0.0f);

    App.VrSdk.SetOverlayAlpha(0);
    yield return null;

    bool routineInterrupted = true;
    try {
      App.VrSdk.FadeToCompositor(fadeDuration);
      // You can't rely on the SteamVR compositor fade being totally over in the time
      // you specified. You also can't rely on being able to get a sensible value for the fade
      // alpha, so you can't reliably wait for it to be done.
      // Therefore, we use the simple method of just waiting a bit longer than we should
      // need to.
      for (float t = 0; t < 1.1f; t += Time.deltaTime / fadeDuration) {
        SetOverlayTransitionRatio(Mathf.Clamp01(t));
        yield return null;
      }

      // Wait one additional frame for any transitions to complete (e.g. fade to black).
      SetOverlayTransitionRatio(1.0f);
      App.VrSdk.PauseRendering(true);
      yield return null;

      try {
        while (true) {
          try {
            if (!action.MoveNext()) {
              break;
            }
          } catch (System.Exception e) {
            Debug.LogException(e);
            break;
          }
          yield return action.Current;
        }
      } finally {
        action.Dispose();
      }
      yield return null; // eat a frame
      if (showSuccessText) {
        SetText("Success!");
        float successHold = 1.0f;
        while (successHold >= 0.0f) {
          successHold -= Time.deltaTime;
          yield return null;
        }
      }

      App.VrSdk.PauseRendering(false);
      App.VrSdk.FadeFromCompositor(fadeDuration);
      for (float t = 1; t > 0; t -= Time.deltaTime / fadeDuration) {
        SetOverlayTransitionRatio(Mathf.Clamp01(t));
        yield return null;
      }
      SetOverlayTransitionRatio(0);
      routineInterrupted = false;
    } finally {
      if (routineInterrupted) {
        // If the coroutine was interrupted, clean up our compositor fade.
        App.VrSdk.PauseRendering(false);
        App.VrSdk.FadeFromCompositor(0.0f);
        SetOverlayTransitionRatio(0.0f);
      }
    }
  }

  // Start or end are normally in [0, 1] but can be slightly greater if you want some room
  // to account for SteamVR latency.
  private async Task FadeCompositorAndOverlayAsync(float start, float end, float duration) {
    if (end > start) { App.VrSdk.FadeToCompositor(duration); }
    else { App.VrSdk.FadeFromCompositor(duration); }

    for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime) {
      float cur = Mathf.Lerp(start, end, elapsed / duration);
      SetOverlayTransitionRatio(Mathf.Clamp01(cur));
      await Awaiters.NextFrame;
    }
    SetOverlayTransitionRatio(Mathf.Clamp01(end));
  }

  /// Does some async work inside the compositor.
  /// It's assumed that the work will yield often enough that it makes sense to
  /// provide an IProgress callback for the Task.
  /// Like the Coroutine-based overloads, the work is only started after the
  /// fade-to-compositor completes.
  public async Task<T> RunInCompositorAsync<T>(
      OverlayType overlayType,
      Func<IProgress<double>, Task<T>> taskCreator,
      float fadeDuration,
      bool showSuccessText = false) {
    SetOverlayFromType(overlayType);
    bool bFullProgress = false;
    UpdateProgress(bFullProgress ? 1.0f : 0.0f);

    App.VrSdk.SetOverlayAlpha(0);
    await Awaiters.NextFrame;

    try {
      // You can't rely on the SteamVR compositor fade being totally over in the time
      // you specified. You also can't rely on being able to get a sensible value for the fade
      // alpha, so you can't reliably wait for it to be done.
      // Therefore, we use the simple method of just waiting a bit longer than we should
      // need to, by passing slightly-too-wide bounds.
      await FadeCompositorAndOverlayAsync(0, 1.1f, fadeDuration);
      // Wait one additional frame for any transitions to complete (e.g. fade to black).
      App.VrSdk.PauseRendering(true);
      await Awaiters.NextFrame;

      Task<T> inner = taskCreator(m_progress);
      try {
        await inner;
      } catch (Exception e) {
        Debug.LogException(e);
      }

      if (showSuccessText) {
        SetText("Success!");
        await Awaiters.Seconds(1f);
      }

      App.VrSdk.PauseRendering(false);
      await FadeCompositorAndOverlayAsync(1, 0, fadeDuration);
      return inner.Result;
    } catch (Exception) {
      App.VrSdk.PauseRendering(false);
      App.VrSdk.FadeFromCompositor(0);
      SetOverlayTransitionRatio(0);
      throw;
    }
  }

  /// Does some synchronous work inside the compositor.
  /// Some mostly-faked progress will be displayed.
  public async Task<T> RunInCompositorAsync<T>(
      OverlayType overlayType,
      Func<T> action,
      float fadeDuration,
      bool showSuccessText = false) {
    SetOverlayFromType(overlayType);
    bool bFullProgress = false;
    UpdateProgress(bFullProgress ? 1.0f : 0.0f);

    App.VrSdk.SetOverlayAlpha(0);
    await Awaiters.NextFrame;

    try {
      // You can't rely on the SteamVR compositor fade being totally over in the time
      // you specified. You also can't rely on being able to get a sensible value for the fade
      // alpha, so you can't reliably wait for it to be done.
      // Therefore, we use the simple method of just waiting a bit longer than we should
      // need to, by passing slightly-too-wide bounds.
      await FadeCompositorAndOverlayAsync(0, 1.1f, fadeDuration);
      // Wait one additional frame for any transitions to complete (e.g. fade to black).
      App.VrSdk.PauseRendering(true);
      Progress.Report(0.25);
      await Awaiters.NextFrame;

      T result = default;
      try {
        result = action();
      } catch (Exception e) {
        Debug.LogException(e);
      }

      Progress.Report(0.75);
      if (showSuccessText) {
        SetText("Success!");
        await Awaiters.Seconds(1f);
      }

      App.VrSdk.PauseRendering(false);
      await FadeCompositorAndOverlayAsync(1, 0, fadeDuration);
      return result;
    } catch (Exception) {
      App.VrSdk.PauseRendering(false);
      App.VrSdk.FadeFromCompositor(0);
      SetOverlayTransitionRatio(0);
      throw;
    }
  }

  public void SetOverlayTransitionRatio(float fRatio) {
    m_OverlayStateTransitionValue = m_OverlayStateTransitionDuration * fRatio;
    bool overlayWasActive = App.VrSdk.OverlayEnabled;
    App.VrSdk.SetOverlayAlpha(fRatio);
    if (!overlayWasActive && App.VrSdk.OverlayEnabled) {
      App.VrSdk.PositionOverlay(m_OverlayOffsetDistance, m_OverlayHeight);
    }
    m_CurrentOverlayState = OverlayState.Visible;
  }

  public void HideOverlay() {
    if (m_CurrentOverlayState == OverlayState.Visible) {
      m_CurrentOverlayState = OverlayState.Exiting;
    }
  }

  private static IEnumerator<Null> AsCoroutine(System.Action action) {
    action();
    yield break;
  }

  private IEnumerator<Null> ConsumeAsProgress(IEnumerator<float> coroutine) {
    using (var coroutineWithProgress = coroutine) {
      while (coroutineWithProgress.MoveNext()) {
        UpdateProgress(coroutineWithProgress.Current);
        yield return null;
      }
    }
  }

  public void RenderLogo(double progress) {
    RenderTexture.active = m_GUILogo;
    GL.Clear(true, true, m_BackgroundColor);
    GL.PushMatrix();
    GL.LoadOrtho();
    m_Material.SetFloat("_Progress", (float)progress);
    m_Material.SetPass(0);
    GL.Begin(GL.QUADS);
    GL.Color(Color.white);
    GL.TexCoord2(0f, 1f);
    GL.Vertex3(m_LogoArea.xMin, m_LogoArea.yMax, 0);
    GL.TexCoord2(1f, 1f);
    GL.Vertex3(m_LogoArea.xMax, m_LogoArea.yMax, 0);
    GL.TexCoord2(1f, 0f);
    GL.Vertex3(m_LogoArea.xMax, m_LogoArea.yMin, 0);
    GL.TexCoord2(0f, 0f);
    GL.Vertex3(m_LogoArea.xMin, m_LogoArea.yMin, 0);
    GL.End();
    m_Font.material.SetPass(0);
    GL.Begin(GL.QUADS);
    for (int i = 0; i < m_TextVertices.Length; ++i) {
      GL.TexCoord(m_TextUvs[i]);
      GL.Vertex(m_TextVertices[i]);
    }
    GL.End();
    GL.PopMatrix();
    RenderTexture.active = null;
  }

  public void SetText(string text) {
    var settings = new TextGenerationSettings();
    settings.font = m_Font;
    settings.color = m_TextColor;
    settings.generationExtents = Vector2.one * 1000f;
    settings.resizeTextForBestFit = true;
    settings.textAnchor = TextAnchor.MiddleCenter;
    settings.fontSize = m_FontSize;
    settings.fontStyle = FontStyle.Normal;
    settings.scaleFactor = 1f;
    settings.generateOutOfBounds = true;
    settings.horizontalOverflow = HorizontalWrapMode.Overflow;
    settings.resizeTextMaxSize = m_FontSize;
    settings.resizeTextMinSize = m_FontSize;

    var generator = new TextGenerator();
    if (generator.Populate(text, settings)) {
      var vertices = generator.GetVerticesArray();
      m_TextVertices = new Vector3[vertices.Length];
      m_TextUvs = new Vector3[vertices.Length];

      int index = 0;
      foreach (var vertex in vertices) {
        Vector3 position = vertex.position - new Vector3(500.0f, 500.0f, 0);
        position *= m_TextArea.height / m_FontSize * 0.8f;
        position += new Vector3(m_TextArea.center.x, m_TextArea.center.y, 0f);
        m_TextVertices[index] = position;
        m_TextUvs[index] = new Vector3(vertex.uv0.x, vertex.uv0.y, 0);
        ++index;
      }
    }
  }
}
} // namespace TiltBrush
