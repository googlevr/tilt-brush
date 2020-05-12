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

using UnityEngine;
using UObject = UnityEngine.Object;

#if UNITY_ANDROID
// In the android build you get warnings about unused variables, because they code that uses them 
// is only enabled for desktop. This disables those warnings.
#pragma warning disable 649, 414
#endif

namespace TiltBrush {

/// A helper class to enable floating point rendering with MSAA enabled. This is necessary
/// because Unity does not allow MSAA with float or half precision framebuffers.
///
[RequireComponent(typeof(Camera))]
public class RenderWrapper : MonoBehaviour {

  // An event indicating a good time at which to read back textures and avoid GPU stalls.
  // Generally happens just before rendering.
  public event System.Action ReadBackTextures;

  public Material m_StencilToMaskMaterial;

  // Used to generate a texture mask from the stencil buffer
  // That is used for selection visuals
  [NonSerialized] public RenderTexture m_MaskTarget;

  // Counter to ensure we only callback to read textures once per frame.
  private int m_readbackTextureFrame = -1;

  // Because rendering to an off screen buffer seems unsupported from a
  // main/screen cam we must make a temp copy of the current active cam to
  // populate the bloom render buffer.
  private Camera m_camCopy;
  private int m_cameraCullingMask = 0;
  private CameraClearFlags m_cameraClearFlags;

  // Cached to avoid garbage.
  private SelectionEffect m_selectionEffect;

  private RenderTexture m_hdrTarget;

  // Minimum SuperSampling factor.
  public const float SSAA_MIN = 0.125f;
  public const float SSAA_MAX = 4.0f;

  // SSAA quantization factor. A value of 8.0 quantizes SSAA values to 1/8, etc.
  public const float SSAA_QUANTUM = 8.0f;

  // SuperSampling factor. A value of 1.0 = disabled, 4.0 = 4x SSAA, etc.
  private float m_superSampling = 1.0f;

  // The Quality Level for which the renderer was configured.
  // This allows us to refresh components when quality level changes.
  int m_configuredFor = -1;

  bool m_isRecording = false;
  bool m_isRecorder = false;

  private Material m_blitWithScale;

  List<Feature> m_features = new List<Feature>();
  LightShadows[] m_shadows = new LightShadows[0];

  private Dictionary<RenderTextureFormat, RenderTextureFormat> m_GetCreatedFormatMemo =
      new Dictionary<RenderTextureFormat, RenderTextureFormat>();

  struct Feature {
    public MonoBehaviour behaviour;
    public bool defaultState;
  }

  public float SuperSampling {
    get { return m_superSampling; }
    set {
      if (System.Math.Abs(value - 1.0) >= SSAA_MIN) {
        // Clamp value to [min,max] and quantize to nearest 1 / Quantum.
        m_superSampling = Mathf.Clamp(Mathf.Round(SSAA_QUANTUM * value) / SSAA_QUANTUM,
                                      SSAA_MIN,
                                      SSAA_MAX);
      } else {
        m_superSampling = 1.0f;
      }
    }
  }

  // -------------------------------------------------------------------------------------------- //
  // Quality Control Helpers
  // -------------------------------------------------------------------------------------------- //

  void AddFeature<T>() {
    MonoBehaviour feature = GetComponent<T>() as MonoBehaviour;
    if (feature == null) {
      return;
    }
    m_features.Add(new Feature{behaviour=feature, defaultState=feature.enabled});
  }

  // Toggle the state of all known features.
  // When enable is false, disable all features, when true, set to default state.
  void ToggleFeatures(bool enable) {
    for (int i = 0; i < m_features.Count; i++) {
      m_features[i].behaviour.enabled = enable
                                    ? m_features[i].defaultState
                                    : false;
    }
  }

  RenderTextureFormat GetTargetFormat() {
    RenderTextureFormat fmt = QualityControls.m_Instance.FramebufferFormat;
    if (m_isRecording && !m_isRecorder) {
      fmt = RenderTextureFormat.ARGB32;
    }
    return fmt;
  }

  // -------------------------------------------------------------------------------------------- //
  // Pre/Post Render Hooks to Setup Camera
  // -------------------------------------------------------------------------------------------- //

  void Awake() {
    m_blitWithScale = new Material(Shader.Find("Hidden/BlitDownsample"));
    m_blitWithScale.SetFloat("_Scale", 1.0f);
    m_selectionEffect = GetComponent<SelectionEffect>();
  }

  void Update() {
    if (!Application.isPlaying) {
      m_isRecording = false;
    }

    if (!m_isRecorder) {
      if (m_configuredFor != QualitySettings.GetQualityLevel()) {
        m_isRecorder = GetComponent<VideoRecorder>() != null;
        m_features.Clear();

        AddFeature<FXAA>();
        AddFeature<SENaturalBloomAndDirtyLens>();

        m_configuredFor = QualitySettings.GetQualityLevel();
      }

      // Lights may change with the environment, though they likely do not in practice.
      if (m_shadows.Length != App.Scene.GetNumLights()) {
        m_shadows = new LightShadows[App.Scene.GetNumLights()];
      }
      for (int i = 0; i < App.Scene.GetNumLights(); i++) {
        m_shadows[i] = App.Scene.GetLight(i).shadows;
      }
    }
  }

  void OnPreCull() {
    if (!Application.isPlaying) {
      return;
    }

#if !UNITY_ANDROID
    Camera srcCam = GetComponent<Camera>();

    // Store the clear and culling mask to restore after rendering.
    m_cameraClearFlags = srcCam.clearFlags;
    m_cameraCullingMask = srcCam.cullingMask;
    srcCam.cullingMask = 0;
    srcCam.clearFlags = CameraClearFlags.Nothing;
    srcCam.allowHDR = GetTargetFormat() != RenderTextureFormat.ARGB32;
#endif

    if (ReadBackTextures != null && m_readbackTextureFrame != Time.frameCount) {
      ReadBackTextures();
    }

    if (m_isRecorder && m_isRecording && m_readbackTextureFrame != Time.frameCount) {
      GetComponent<VideoRecorder>().ReadbackCapture();
    }

    m_readbackTextureFrame = Time.frameCount;
  }

  public void OnPreRender() {
    if (!Application.isPlaying || !QualityControls.m_Instance) {
      Shader.DisableKeyword("HDR_EMULATED");
      m_isRecording = false;
      return;
    }

    m_isRecording = (VideoRecorderUtils.ActiveVideoRecording != null) &&
        VideoRecorderUtils.ActiveVideoRecording.IsCapturing;

#if UNITY_ANDROID
    int msaa = QualityControls.m_Instance.MSAALevel;
    // MSAA disabled in QualityControls = 0, but render texture wants 1.
    if (msaa == 0) {
      msaa = 1;
    }
    if (msaa != 1 && msaa != 2 && msaa != 4 && msaa != 8) {
      UnityEngine.Debug.LogWarningFormat("Invalid MSAA {0} != [1,2,4,8]", msaa);
      msaa = 1;
    }

    if (msaa != QualitySettings.antiAliasing) {
      QualitySettings.antiAliasing = msaa;
    }
    GetComponent<Camera>().allowMSAA = msaa > 1;

    // Use a single camera on Android.
    return;
#else

    Camera srcCam = GetComponent<Camera>();

    // One extra camera is all we need, we'll copy the camera state on every render.
    if (!m_camCopy) {
      var go = new GameObject("(RenderWrapper Camera)",
                              typeof(Camera));

      m_camCopy = go.GetComponent<Camera>();
      m_camCopy.transform.parent = srcCam.transform;
    }

    // Make sure we pickup the current camera transform, etc.
    m_camCopy.CopyFrom(srcCam);
    m_camCopy.enabled = false;
    m_camCopy.cullingMask = m_cameraCullingMask;
    m_camCopy.clearFlags = m_cameraClearFlags;

    int width = m_camCopy.pixelWidth;
    int height = m_camCopy.pixelHeight;

    if (m_camCopy.pixelHeight == 0) {
      width = Screen.width;
      height = Screen.height;
    }

    // We downsample here instead of in SteamVR because SteamVR was causing flickering.
    // In our current (old) version of SteamVR, it also resizes the render target, which causes a
    // hitch (the new version does not do this).
    if (m_isRecording && !m_isRecorder) {
      float downSample = .75f;
      m_blitWithScale.SetFloat("_Scale", downSample);
      m_camCopy.rect = new Rect(0, 0, downSample, downSample);
    } else {
      m_blitWithScale.SetFloat("_Scale", 1.0f);
      m_camCopy.rect = new Rect(0.0f, 0.0f, 1.0f, 1.0f);
    }

    // Apply SuperSampling factor, if any.
    // Note that SSAA factor has already been sanitized.
    width = (int)(width * m_superSampling);
    height = (int)(height * m_superSampling);

    // Apply MSAA factor, if any.
    int msaa = QualityControls.m_Instance.MSAALevel;
    // MSAA disabled in QualityControls = 0, but render texture wants 1.
    if (msaa == 0) {
      msaa = 1;
    }
    if (msaa != 1 && msaa != 2 && msaa != 4 && msaa != 8) {
      UnityEngine.Debug.LogWarningFormat("Invalid MSAA {0} != [1,2,4,8]", msaa);
      msaa = 1;
    }

    if (m_isRecording && !m_isRecorder) {
      msaa = 1;
      ToggleFeatures(false);
      for (int i = 0; i < App.Scene.GetNumLights(); i++) {
        App.Scene.GetLight(i).shadows = LightShadows.None;
      }
    } else {
      ToggleFeatures(true);
    }

    // Setup the render texture framebuffer.
    // Get the actual platform-specific format so we can use it for comparisons
    RenderTextureFormat fmt = GetCreatedFormat(GetTargetFormat());
    if (!m_hdrTarget ||
        m_hdrTarget.width != width ||
        m_hdrTarget.height != height ||
        m_hdrTarget.antiAliasing != msaa ||
        m_hdrTarget.format != fmt) {

      if (m_hdrTarget) {
        UObject.Destroy(m_hdrTarget);
      }
      m_hdrTarget = new RenderTexture(width, height, 24, fmt);

      m_hdrTarget.antiAliasing = msaa;
    }

    if (!m_MaskTarget && m_selectionEffect != null) {
      // Adding 5px to make sure the selection mask is not the same resolution as
      // the render texture, as the resampling is what allows us to do the selection outline effect
      int maskWidth = Mathf.Min(2048, width + 5);
      int maskHeight = maskWidth;

      if (m_MaskTarget) {
        UObject.Destroy(m_MaskTarget);
      }
      m_MaskTarget = new RenderTexture(maskWidth, maskHeight, 0, RenderTextureFormat.RFloat);
    }

    // Set the shader variant based on the framebuffer format.
    // Set shader variant for alpha-exponent encoding. Only encode if
    // - it's needed (ie, non-float target)
    // - there's a decode pass
    //
    // There are cases where we omit the decode pass for performance reasons;
    // eg, mobile quality setting, or when recording video.
    if (fmt == RenderTextureFormat.ARGB32 && HasHdrDecodePass()) {
      Shader.EnableKeyword("HDR_EMULATED");  // RGBAE: turn on alpha-exp encoding
    } else {
      Shader.DisableKeyword("HDR_EMULATED"); // RGBA: no encoding
    }

    // We have to manually clear the alpha when using fake HDR as surface shaders do not write into
    // the alpha channel the way we'd like them to.
    // Previously we used to rely on the skybox to do this for us, but something changed between
    // Unity 5.3.5 and 5.4.2, and it can't be relied upon to do that any more.
    RenderTexture.active = m_hdrTarget;
    GL.Clear(true, true, Color.black);
    RenderTexture.active = null;

    m_camCopy.targetTexture = m_hdrTarget;
    m_camCopy.Render();
#endif
  }

  // Returns the platform-specific value for RenderTextureFormat.Default,
  // RenderTextureFormat.DefaultHDR, etc
  private RenderTextureFormat GetCreatedFormat(RenderTextureFormat fmt) {
    if (m_GetCreatedFormatMemo.TryGetValue(fmt, out var actual)) {
      return actual;
    }
    var rt = new RenderTexture(128, 128, 24, fmt);
    m_GetCreatedFormatMemo[fmt] = rt.format;
    UObject.Destroy(rt);
    return m_GetCreatedFormatMemo[fmt];
  }

  void OnPostRender() {
    if (m_camCopy == null) {
      // If the camCopy is null, we should not attempt to restore values to the source cam, since
      // a null camCopy indicates we didn't store the culling & clear flags.
      return;
    }

    Camera srcCam = GetComponent<Camera>();

    srcCam.cullingMask = m_cameraCullingMask;
    srcCam.clearFlags = m_cameraClearFlags;
  }

  bool ExistsAndIsEnabled<T>() where T : MonoBehaviour {
    T c = GetComponent<T>();
    return (c != null && c.enabled);
  }

  bool HasHdrDecodePass() {
    return (ExistsAndIsEnabled<SENaturalBloomAndDirtyLens>());
  }

  // -------------------------------------------------------------------------------------------- //
  // Post-effect to blit temp framebuffer to downstream effects.
  // -------------------------------------------------------------------------------------------- //
  // This must be excluded from the build, even a no-op post effect negatively impacts performance.
#if !UNITY_ANDROID
  public void OnRenderImage(RenderTexture source, RenderTexture destination) {
    // We could skip the offscreen render when anti-aliasing is disabled.
    if (m_hdrTarget == null) {
      Graphics.Blit(source, destination);
    } else {
      if (m_isRecording && !m_isRecorder) {
        Graphics.Blit(m_hdrTarget, destination, m_blitWithScale);
        Graphics.Blit(Texture2D.blackTexture, m_MaskTarget, m_blitWithScale);
      } else {
        Graphics.Blit(m_hdrTarget, destination);
        // Generate the outline mask for later use in the post fx chain
        if (m_StencilToMaskMaterial) {

          //
          // TO DO: This is a lot of blitting, mostly just to get access to the stencil buffer
          // is there a better way?  Hopefully we can find a better way.
          //

          // Only need the selection mask if the selection effect is enabled
          if (App.Instance.SelectionEffect.RenderHighlight()) {
            Graphics.Blit(m_hdrTarget, destination);
            Graphics.Blit(destination, m_hdrTarget, m_StencilToMaskMaterial);
            Graphics.Blit(m_hdrTarget, m_MaskTarget);
          }
        }
      }
    }

    if (m_isRecording && !m_isRecorder) {
      for (int i = 0; i < App.Scene.GetNumLights(); i++) {
        App.Scene.GetLight(i).shadows = m_shadows[i];
      }
    }
  }
#endif
}

} // namespace TiltBrush

#if UNITY_ANDROID
#pragma warning restore 649, 414
#endif

