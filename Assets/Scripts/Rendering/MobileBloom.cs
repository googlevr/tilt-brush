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

using System.Linq;
using TiltBrush;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

// Mobile bloom uses three ideas to get the required speed for mobile:
// a) The bloom is fairly low quality, so the actual shader is cheap.
// b) The bloom only operates on a central portion of the screen - but luckily in VR the bit around
//    the outside aren't really visible to the user.
// c) We save off the bloom for an eye in a rendertexture and then use it, rather than the full
//    bloom for the next frame. Each frame we swap which eye gets the full bloom and which gets the
//    saved bloom from the last frame.
public class MobileBloom : MonoBehaviour {

  [SerializeField] private Shader m_bloomShader;
  [SerializeField] private float m_xMult = 1;
  [SerializeField] private float m_yMult = 1;
  [SerializeField] private float m_xOff = 0;
  [SerializeField] private float m_yOff = 0;
  [SerializeField] private AnimationCurve m_BackgroundBrightnessToBloom;
  [SerializeField] private bool m_AlwaysOn;
  [SerializeField] private string m_OverrideDividors;

  public float BloomAmount {
    get { return m_BloomAmount; }
    set { m_BloomAmount = value; }
  }

  private Material[] m_bloomMaterial;

  private Pose m_PreviousPose = Pose.identity;

  // Subsets of the screen that get rendered to. The subsets are per-eye as the x-offset for left
  // and right are different due to being mirrored on the x-axis.
  private int m_width;
  private int m_height;
  private int[] m_xOffset;
  private int m_yOffset;

  // The dividors are the amount that the bloom size is divided by for each lever. For instance,
  // 2, 2, 3 results in bloom levels that are 1/2, 1/4, and 1/12 of the full size. Doing it this
  // way makes it easier to tweak.
  private int[] m_dividors;

  // The saved off bloom textures that get reused for the next frame.
  public RenderTexture[] m_savedBloom;

  // Command buffers used for rendering the bloom
  private CommandBuffer[] m_MakeBloom;
  private CommandBuffer[] m_DisplayBloom;

  private int m_Eye = 1;
  private Camera m_Camera;
  private float m_BloomAmount = 1;

  private void Awake() {
    m_xOffset = new int[2];

    m_Camera = GetComponent<Camera>();
    m_savedBloom = new RenderTexture[2];

    m_bloomMaterial = new Material[2];
    for (int i = 0; i < 2; ++i) {
      m_bloomMaterial[i] = new Material(m_bloomShader);
      m_bloomMaterial[i].hideFlags = HideFlags.HideAndDontSave;
      m_bloomMaterial[i].SetInt("_Eye", i);
    }

    m_MakeBloom = new CommandBuffer[2];
    m_DisplayBloom = new CommandBuffer[2];
  }

  private void OnEnable() {
    // QualityControls can be null if we're not in the main scene.
    if (QualityControls.m_Instance == null) {
      InitializeBloom(0);
    } else {
      InitializeBloom(QualityControls.m_Instance.QualityLevel);
      QualityControls.m_Instance.OnQualityLevelChange += InitializeBloom;
    }
  }

  // Whenever we change quality levels, we recreate the command buffers to match the new settings.
  private void InitializeBloom(int qualityLevel) {
    if (m_Camera.stereoEnabled) {
      // Calculate the pixel values for the offsets into the area of the screen that receives bloom.
      m_width = (int) (XRSettings.eyeTextureWidth * m_xMult);
      m_height = (int) (XRSettings.eyeTextureHeight * m_yMult);
      m_yOffset = (int) (XRSettings.eyeTextureHeight * m_yOff);
      int xSpare = XRSettings.eyeTextureWidth - m_width;
      m_xOffset[0] = (int) (XRSettings.eyeTextureWidth * m_xOff);
      m_xOffset[1] = xSpare - m_xOffset[0];
      // When single-pass is enabled in-editor, it uses a single wide eye texture, rather than a two-
      // -deep texture array like it does on mobile.
      if (App.Config == null || !(App.Config.IsMobileHardware && !SpoofMobileHardware.MobileHardware)) {
        m_xOffset[1] += XRSettings.eyeTextureWidth;
      }
    } else {
      // When the camera isn't stereo, it means it is the screenshot camera, and we want the bloom
      // to cover the whole image.
      m_width = m_Camera.targetTexture.width;
      m_height = m_Camera.targetTexture.height;
      m_yOffset = 0;
      m_xOffset[0] = 0;
      m_xOffset[1] = 0;
    }

    m_dividors = (QualityControls.m_Instance == null) ? new int[3] {2, 2, 3} :
        QualityControls.m_Instance.AppQualityLevels[qualityLevel].BloomLevels;

    if (!string.IsNullOrEmpty(m_OverrideDividors)) {
      m_dividors = m_OverrideDividors.Split(',').Select(x => int.Parse(x)).ToArray();
    }

    for (int eye = 0; eye < 2; ++eye) {
      m_MakeBloom[eye] = CreateBloom(eye);
      m_DisplayBloom[eye] = DisplayBloom(eye);
    }
  }

  private void OnDisable() {
    if (m_Camera != null) {
      m_Camera.RemoveAllCommandBuffers();
    }
    if (QualityControls.m_Instance != null) {
      QualityControls.m_Instance.OnQualityLevelChange -= InitializeBloom;
    }
  }

  private void Update() {
    if (!m_Camera.enabled) { return; }
    int other = (m_Eye + 1) & 1;

    m_Camera.RemoveAllCommandBuffers();
    if (m_Camera.stereoEnabled) {
      // Switch the command buffers for each eye
      m_Camera.AddCommandBuffer(CameraEvent.AfterEverything, m_MakeBloom[other]);
      m_Camera.AddCommandBuffer(CameraEvent.AfterEverything, m_DisplayBloom[m_Eye]);
    } else {
      // If the camera is mono, we create the bloom for it on every single frame,
      // so we don't need to display last frame's bloom at all.
      m_Camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, m_MakeBloom[0]);
    }

    // Adjust the texture offset for the bloom for the 'off' eye.
    Vector3 newForward = m_Camera.transform.rotation * Vector3.forward;

    float dotForward = Vector3.Dot(m_PreviousPose.forward, newForward);
    float dotRight = Vector3.Dot(m_PreviousPose.right, newForward);
    float dotUp = Vector3.Dot(m_PreviousPose.up, newForward);
    float hAngle = Mathf.Atan2(dotRight, dotForward);
    float vAngle = Mathf.Atan2(dotUp, dotForward);

    Vector4 offset = new Vector4(hAngle, vAngle, 0, 0);
    m_bloomMaterial[m_Eye].SetVector("_FinalOffset", offset);
    Shader.SetGlobalFloat("_BloomEye", m_Eye);

    m_PreviousPose = new Pose(m_Camera.transform.position, m_Camera.transform.rotation);

    // We only switch eyes with stereo cameras.
    if (m_Camera.stereoEnabled) {
      m_Eye = other;
    }

    Shader.EnableKeyword("HDR_SIMPLE");
    Shader.DisableKeyword("HDR_EMULATED");

    // Fade out the bloom with very bright backgrounds
    float totalBloom = m_AlwaysOn ? 1 : m_BloomAmount;
    if (SceneSettings.m_Instance != null && SceneSettings.m_Instance.InGradient) {
      Vector3 gradUp = App.Scene.Pose.rotation * SceneSettings.m_Instance.GradientOrientation *
                       Vector3.up;
      float gradRatio = Mathf.Acos(Vector3.Dot(gradUp, ViewpointScript.Gaze.direction)) / Mathf.PI;
      Color averageSky = Color.Lerp(SceneSettings.m_Instance.SkyColorB,
          SceneSettings.m_Instance.SkyColorA, gradRatio);
      totalBloom = m_BackgroundBrightnessToBloom.Evaluate(averageSky.grayscale) * m_BloomAmount;
    }

    for (int i = 0; i < 2; ++i) {
      m_bloomMaterial[i].SetFloat("_BloomAmount", totalBloom);
    }
  }

  // Creates a bloom command buffer
  private CommandBuffer CreateBloom(int eye) {
    var cmdBuffer = new CommandBuffer();
    cmdBuffer.name = string.Format("Bloom ({0}{1})", m_Camera.name, (Camera.StereoscopicEye) eye);

    int numLevels = m_dividors.Length + 1;
    int dividor = 1;
    var levelIds = new int[numLevels];
    var renderTargetIds = new RenderTargetIdentifier[numLevels];

    // Which slice we read/write from depends on whether we are running on PC or Mobile.
    int slice =(App.Config != null) && App.Config.IsMobileHardware
                                    && !SpoofMobileHardware.MobileHardware
        ? eye : 0;

    for (int i = 0; i < numLevels; ++i) {
      // Create the temporary rendertexture for each level, unless it's level 1, which is the saved
      // bloom level.
      if (i == 1) {
        var newSavedRt = new RenderTexture(m_width / dividor, m_width / dividor, 0);
        // Copy across the old one so we don't get a frame of black
        if (m_savedBloom[eye] != null) {
          Graphics.Blit(m_savedBloom[eye], newSavedRt);
          m_savedBloom[eye].Release();
        }
        m_savedBloom[eye] = newSavedRt;
        renderTargetIds[i] = new RenderTargetIdentifier(m_savedBloom[eye]);
      } else {
        levelIds[i] = Shader.PropertyToID(string.Format("Eye{0}_Level{1}", eye, i));
        cmdBuffer.GetTemporaryRT(levelIds[i], m_width / dividor, m_height / dividor,
            0, FilterMode.Bilinear);
        renderTargetIds[i] = new RenderTargetIdentifier(levelIds[i]);
      }

      // In the first level, just copy a section of the screen buffer. Otherwise, blit from the
      // previous level to this one.
      if (i == 0) {
        // XXX
        cmdBuffer.CopyTexture(BuiltinRenderTextureType.CameraTarget, slice, 0, m_xOffset[eye],
            m_yOffset, m_width, m_height, renderTargetIds[0], 0, 0, 0, 0);
      } else {
        int pass = i == 1 ? 0 : 1;
        cmdBuffer.Blit(renderTargetIds[i - 1], renderTargetIds[i], m_bloomMaterial[eye], pass);
      }

      if (i != m_dividors.Length) {
        dividor *= m_dividors[i];
      }
    }

    // Now we have the cascade of levels, blit them on top of each other
    for (int i = m_dividors.Length; i > 0; --i) {
      int pass = i == 1 ? 3 : 2;
      cmdBuffer.Blit(renderTargetIds[i], renderTargetIds[i - 1], m_bloomMaterial[eye], pass);
      if (i != 1) {
        cmdBuffer.ReleaseTemporaryRT(levelIds[i]);
      }
    }

    // Copy the result back to the screen.
    // XXX
    cmdBuffer.CopyTexture(renderTargetIds[0], 0, 0, 0, 0, m_width, m_height,
        BuiltinRenderTextureType.CameraTarget, slice, 0, m_xOffset[eye], m_yOffset);

    cmdBuffer.ReleaseTemporaryRT(levelIds[0]);

    return cmdBuffer;
  }

  // The displaybloom command buffer copies the bloom section of the screen to a texture, blits the
  // saved bloom over the top, and then copies the texture back to that section of the screen.
  // TODO Investigate if we could do this more quickly with a direct render without
  // the copyback.
  private CommandBuffer DisplayBloom(int eye) {
    var cmdBuffer = new CommandBuffer();
    cmdBuffer.name = string.Format("Show Previous Bloom ({0})", (Camera.StereoscopicEye) eye);

    int copyBackId = Shader.PropertyToID(string.Format("Eye{0}_CopyBack", eye));
    var copyBackIdent = new RenderTargetIdentifier(copyBackId);
    var bloomIdent = new RenderTargetIdentifier(m_savedBloom[eye]);
    int slice =(App.Config != null) && App.Config.IsMobileHardware
                                    && !SpoofMobileHardware.MobileHardware
        ? eye : 0;

    cmdBuffer.GetTemporaryRT(copyBackId, m_width, m_height, 0);
    cmdBuffer.CopyTexture(BuiltinRenderTextureType.CameraTarget, slice, 0, m_xOffset[eye],
        m_yOffset, m_width, m_height, copyBackIdent, 0, 0, 0, 0);
    cmdBuffer.Blit(bloomIdent, copyBackIdent, m_bloomMaterial[eye], 3);
    cmdBuffer.CopyTexture(copyBackIdent, 0, 0, 0, 0, m_width, m_height,
        BuiltinRenderTextureType.CameraTarget, slice, 0, m_xOffset[eye], m_yOffset);
    cmdBuffer.ReleaseTemporaryRT(copyBackId);

    return cmdBuffer;
  }
} // namespace TiltBrush
