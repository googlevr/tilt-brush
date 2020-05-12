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

#if UNITY_ANDROID
# undef  FEATURE_CUSTOM_MESH_RENDER
# define FEATURE_MOBILE_SELECTION
#else
# define FEATURE_CUSTOM_MESH_RENDER
# undef  FEATURE_MOBILE_SELECTION
#endif

using System;
using System.Collections.Generic;

using UnityEngine;

namespace TiltBrush {

public class SelectionEffect : MonoBehaviour {
#if FEATURE_CUSTOM_MESH_RENDER
  private enum SelectionEffectPass {
    OutlineComposite, // 0
    Downsample,       // 1
    VerticalBlur,     // 2
    HorizontalBlur    // 3
  }
#endif

  // Only for FEATURE_CUSTOM_MESH_RENDER, but don't #if out serialized data.
  // #pragma is needed because of the default values
  // ReSharper disable NotAccessedField.Local
#pragma warning disable 0414
  [SerializeField] private Material m_GrabHighlightMaskMaterial;
  [SerializeField] private Material m_WidgetSelectionPostEffect;
  [SerializeField] private Material m_StrokeSelectionPostEffect;
  [SerializeField] private float m_BlurWidth = 1.0f;
#pragma warning restore 0414
  // ReSharper restore NotAccessedField.Local

  // Not sure if these are specific to FEATURE_CUSTOM_MESH_RENDER
  [Header("Selection Color Settings")]
  [SerializeField] private float m_SelectionSaturation = 0.5f;
  [SerializeField] private float m_SelectionValue = 1f;
  [SerializeField] private float m_SelectionHueSpeed = 0.5f;
  [SerializeField] private float m_SelectionHueStereoOffset = 0.2f;

  // Only for FEATURE_MOBILE_SELECTION, but don't #if out serialized data.
  // #pragma is needed because of the default values
  // ReSharper disable NotAccessedField.Local
#pragma warning disable 0414
  [Header("Mobile Selection Shader Settings")]
  [SerializeField] private float m_NoiseSparkleSpeed = 2f;
#pragma warning restore 0414
  // ReSharper restore NotAccessedField.Local

#if FEATURE_CUSTOM_MESH_RENDER
  private RenderWrapper m_CmrRenderWrapper;
  private bool m_CmrRenderHighlight;
  private bool m_CmrUseStrokePostEffect;
  private List<MeshFilter> m_CmrRequestedMeshes;
  private bool m_bCmrPosesApplied;
  private bool m_bCmrPreCulled;
#endif

  void Start() {
#if FEATURE_CUSTOM_MESH_RENDER
    m_CmrRenderWrapper = GetComponent<RenderWrapper>();
    m_CmrRequestedMeshes = new List<MeshFilter>();
#endif
    InputManager.m_Instance.ControllerPosesApplied += OnPosesApplied;
    Shader.SetGlobalColor("_LeftEyeSelectionColor", Color.white);
    Shader.SetGlobalColor("_RightEyeSelectionColor", Color.white);
  }

  void OnPreCull() {
#if FEATURE_CUSTOM_MESH_RENDER
    m_bCmrPreCulled = true;
    RenderHighlightsIfReady();
#endif
  }

  void OnPosesApplied() {
#if FEATURE_CUSTOM_MESH_RENDER
    m_bCmrPosesApplied = true;
#endif

    // Set the selection colors for each eye
    float leftHue = Mathf.Repeat(Time.realtimeSinceStartup * m_SelectionHueSpeed, 1f);
    float rightHue = Mathf.Repeat(
        Time.realtimeSinceStartup * m_SelectionHueSpeed + m_SelectionHueStereoOffset, 1f);

    Color left = Color.HSVToRGB(leftHue, m_SelectionSaturation, m_SelectionValue);
    Color right = Color.HSVToRGB(rightHue, m_SelectionSaturation, m_SelectionValue);

    Shader.SetGlobalColor("_LeftEyeSelectionColor", left);
    Shader.SetGlobalColor("_RightEyeSelectionColor", right);

#if FEATURE_MOBILE_SELECTION
    TrTransform worldToTransform = App.Scene.SelectionCanvas.Pose;
    // Keep the scale within 0.5 and 2
    if (worldToTransform.scale > 2) {
      worldToTransform.scale /= Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(worldToTransform.scale));
    } else if (worldToTransform.scale < 0.5) {
      worldToTransform.scale *=
          Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(1f / worldToTransform.scale));
    }

    Shader.SetGlobalMatrix("_InverseLimitedScaleSceneMatrix", worldToTransform.inverse.ToMatrix4x4());
    Shader.SetGlobalFloat("_PatternSpeed", m_NoiseSparkleSpeed);
#endif

#if FEATURE_CUSTOM_MESH_RENDER
    RenderHighlightsIfReady();
#endif
  }

  // Public API for FEATURE_CUSTOM_MESH_RENDER

  /// Causes selection effect to be rendered for the passed mesh, this frame only
  public void RegisterMesh(MeshFilter meshFilter) {
#if FEATURE_CUSTOM_MESH_RENDER
    m_CmrRequestedMeshes.Add(meshFilter);
#endif
  }

  public void UnregisterMesh(MeshFilter meshFilter) {
#if FEATURE_CUSTOM_MESH_RENDER
    m_CmrRequestedMeshes.RemoveAll(x => x == meshFilter);
#endif
  }

  public bool RenderHighlight() {
#if FEATURE_CUSTOM_MESH_RENDER
    return m_CmrRenderHighlight;
#else
    throw new InvalidOperationException();  // Don't know what to return
#endif
  }

  public void HighlightForGrab(bool grabAvailable) {
#if FEATURE_CUSTOM_MESH_RENDER
    m_CmrUseStrokePostEffect = !grabAvailable;
#endif
  }

  // Internal API for FEATURE_CUSTOM_MESH_RENDER

  // If highlight meshes have been populated, render them using our special grab mask material
  // This material renders objects into the stencil buffer for outline generation
#if FEATURE_CUSTOM_MESH_RENDER
  private Material ActivePostEffect() {
    return m_CmrUseStrokePostEffect ? m_StrokeSelectionPostEffect : m_WidgetSelectionPostEffect;
  }

  private void RenderHighlightsIfReady() {
    if (Application.isMobilePlatform) {
      return;
    }
    if (m_bCmrPreCulled && m_bCmrPosesApplied) {
      m_CmrRenderHighlight = false;

      try {
        if (App.VrSdk.OverlayEnabled) { return; }
        for (int i = 0; i < m_CmrRequestedMeshes.Count; i++) {
          InternalDrawMesh(m_CmrRequestedMeshes[i]);
        }
      } finally {
        // Highlights are populated each frame, so we don't need to keep these lists around.
        // Also, we clear the m_bPosesApplied and m_bPreCulled flags later on OnRenderImage()
        // to guarantee that poses and cull have been done before clearing those flags.
        m_CmrRequestedMeshes.Clear();
      }
    }
  }

  void InternalDrawMesh(MeshFilter meshFilter) {
    // Object might have been destroyed after the request was made.
    // This happens and is expected, eg with batches auto-destroying themselves.
    if (meshFilter == null) {
      return;
    }
    // This is not known to happen, but is theoretically possible.
    var mesh = meshFilter.sharedMesh;
    if (mesh == null) {
      return;
    }
    var xf = meshFilter.transform;
    m_CmrRenderHighlight = true;

    Material selectMaterial = m_GrabHighlightMaskMaterial;

    for (int iSubMesh = 0; iSubMesh < mesh.subMeshCount; iSubMesh++) {
      Graphics.DrawMesh(
        mesh, xf.localToWorldMatrix,
        selectMaterial,
        xf.gameObject.layer,
        null, iSubMesh);
    }
  }

  // This must be excluded if unused; even a no-op post effect negatively impacts performance.
  // Postprocess the image
  void OnRenderImage(RenderTexture source, RenderTexture destination) {
    // Clear flags for the next frame.
    m_bCmrPosesApplied = false;
    m_bCmrPreCulled = false;

    // Early out, if possible
    Material postEffect = ActivePostEffect();
    bool isRecording = (VideoRecorderUtils.ActiveVideoRecording != null) &&
        VideoRecorderUtils.ActiveVideoRecording.IsCapturing;
    if (isRecording || !m_CmrRenderHighlight || !postEffect) {
      Graphics.Blit(source, destination);
      return;
    }

    RenderTextureFormat fmt = RenderTextureFormat.RFloat;
    int rtWidth = 1024;
    int rtHeight = 1024;

    // Downsample the mask

    // TODO:
    // We could steal another bit of alpha/exponent to squirrel away the stencil/mask.
    // Then we could do a single down sample pass along with bloom.

    RenderTexture maskTarget = m_CmrRenderWrapper.m_MaskTarget;

    RenderTexture rt = RenderTexture.GetTemporary(rtWidth, rtHeight, 0, fmt);
    rt.filterMode = FilterMode.Bilinear;
    // 4 tap downsample
    Graphics.Blit(maskTarget, rt, postEffect, (int)SelectionEffectPass.Downsample);

    RenderTexture rt2 = RenderTexture.GetTemporary(rtWidth / 2, rtWidth / 2, 0, fmt);
    rt2.filterMode = FilterMode.Bilinear;
    // 4 tap downsample
    Graphics.Blit(rt, rt2, postEffect, (int)SelectionEffectPass.Downsample);
    RenderTexture.ReleaseTemporary(rt);
    rt = rt2;

    //  Vertical / Horizontal Blur

    postEffect.SetFloat("_BlurSize", m_BlurWidth);

    // vertical blur
    rt2 = RenderTexture.GetTemporary(rtWidth / 2, rtWidth / 2, 0, fmt);
    rt2.filterMode = FilterMode.Bilinear;
    Graphics.Blit(rt, rt2, postEffect, (int)SelectionEffectPass.VerticalBlur);
    RenderTexture.ReleaseTemporary(rt);
    rt = rt2;

    // horizontal blur
    rt2 = RenderTexture.GetTemporary(rtWidth / 2, rtWidth / 2, 0, fmt);
    rt2.filterMode = FilterMode.Bilinear;
    Graphics.Blit(rt, rt2, postEffect, (int)SelectionEffectPass.HorizontalBlur);
    RenderTexture.ReleaseTemporary(rt);
    rt = rt2;

    // Using the previously created mask, composite it over the scene,
    // but only where the newly inflated outline is not drawing on top of
    // stencil ref 1 (highlight object).

    // Note that stencil ref 2 is reserved for ui objects.

    postEffect.SetTexture("_BlurredSelectionMask", rt2);
    postEffect.SetTexture("_SelectionMask", maskTarget);
    Graphics.Blit(source, destination, postEffect, (int)SelectionEffectPass.OutlineComposite);
    RenderTexture.ReleaseTemporary(rt);
  }
#endif  // FEATURE_CUSTOM_MESH_RENDER
}
}  // namespace TiltBrush
