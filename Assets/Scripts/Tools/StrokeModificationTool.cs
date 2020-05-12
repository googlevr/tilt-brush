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

public class StrokeModificationTool : BaseStrokeIntersectionTool {
  public Transform m_ToolTransform;
  public Renderer m_ToolDescriptionText;
  public Vector2 m_SizeRange;
  protected float m_CurrentSize;

  protected bool m_LockToController;

  public Material m_ToolColdMaterial;
  public Material m_ToolHotMaterial;
  protected bool m_ToolWasHot;

  public float m_HapticInterval = .1f;
  public float m_HapticSizeUp;
  public float m_HapticSizeDown;

  private enum AudioState {
    Off,
    FadingIn,
    On,
    FadingOut
  }
  private GvrAudioSource m_ToolAudio;
  private AudioState m_CurrentAudioState;
  private float m_AudioFadeRatio;
  public float m_AudioVolumeMax;
  public float m_AudioAttackSpeed;

  [SerializeField] private AudioClip[] m_ModifyStrokeSounds;
  public float m_ModifyStrokeMinTriggerTime;
  private float m_ModifyStrokeTimestamp;

  public virtual bool IsHot {
    get {
      return !m_EatInput && !m_ToolHidden &&
          InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate);
    }
  }

  override public void Init() {
    base.Init();

    m_CurrentSize = Mathf.Lerp(m_SizeRange.x, m_SizeRange.y, 0.5f);
    m_ToolTransform.localScale = Vector3.one * m_CurrentSize;
    m_ToolAudio = m_ToolTransform.GetComponent<GvrAudioSource>();

    m_LockToController = m_SketchSurface.IsInFreePaintMode();

    m_ToolWasHot = false;
    m_CurrentAudioState = AudioState.Off;
  }

  override public void HideTool(bool bHide) {
    base.HideTool(bHide);
    if (bHide) {
      RequestPlayAudio(false);
    }
  }

  override public void EnableTool(bool bEnable) {
    base.EnableTool(bEnable);
    ResetDetection();

    if (bEnable) {
      m_ToolDescriptionText.enabled = !m_LockToController;
      EatInput();
    } else {
      HardStopAudio();
    }
    SnapIntersectionObjectToController();
  }

  protected void RequestPlayAudio(bool bPlay) {
    if (m_ToolAudio) {
      if (bPlay) {
        if (m_CurrentAudioState == AudioState.Off) {
          if (AudioManager.Enabled) {
            m_ToolAudio.Play();
          }
        }
        if (m_CurrentAudioState != AudioState.On) {
          m_CurrentAudioState = AudioState.FadingIn;
        }
      } else {
        if (m_CurrentAudioState != AudioState.Off) {
          m_CurrentAudioState = AudioState.FadingOut;
        }
      }
    }
  }

  protected void HardStopAudio() {
    m_CurrentAudioState = AudioState.Off;
    m_ToolAudio.Stop();
    m_ToolAudio.volume = 0.0f;
  }

  override public bool ShouldShowTouch() {
    return false;
  }

  protected override int AdditionalGpuIntersectionLayerMasks() {
    return LayerMask.NameToLayer("SelectionCanvas");
  }

  void Update() {
    // Note this isn't in UpdateAudioVisuals() because we want it to run while the
    // tool is deactivated.
    if (m_ToolAudio) {
      // Update our audio volume if it's fading in or out
      switch (m_CurrentAudioState) {
      case AudioState.FadingIn:
        m_AudioFadeRatio += (m_AudioAttackSpeed * Time.deltaTime) / m_AudioVolumeMax;
        if (m_AudioFadeRatio >= 1f) {
          m_AudioFadeRatio = 1f;
          m_CurrentAudioState = AudioState.On;
        }

        m_ToolAudio.volume = m_AudioFadeRatio * m_AudioVolumeMax;
        break;
      case AudioState.FadingOut:
        // A Unity bug causes an audio artifact when m_ToolAudio.Stop() is called when the
        // volume is not 0, or right after it reached 0.
        // I use a small trick to fix this:
        // Make m_AudioFadeRatio decrease twice as slow,
        // and the actual volume decrease twice as fast compared to m_AudioFadeRatio.
        // This causes the volume to reach 0 before m_AudioFadeRatio
        // and creates a silence before m_ToolAudio.Stop() is called.

        m_AudioFadeRatio -= ((m_AudioAttackSpeed / m_AudioVolumeMax) / 2f) * Time.deltaTime;
        if (m_AudioFadeRatio <= 0.0f) {
          m_AudioFadeRatio = 0.0f;
          m_CurrentAudioState = AudioState.Off;
          m_ToolAudio.Stop();
        }
        float fVolume = Mathf.Max(0.0f, (m_AudioFadeRatio - .5f) * m_AudioVolumeMax * 2f);
        m_ToolAudio.volume = fVolume;
        break;
      }
    }
  }

  override public void UpdateTool() {
    base.UpdateTool();
    UpdateAudioVisuals();
    SnapIntersectionObjectToController();
    UpdateDetection();
  }

  virtual protected void UpdateAudioVisuals() {}

  override protected void SnapIntersectionObjectToController() {
    if (m_LockToController) {
      Vector3 toolPos = InputManager.Brush.Geometry.ToolAttachPoint.position +
          InputManager.Brush.Geometry.ToolAttachPoint.forward * m_PointerForwardOffset;
      m_ToolTransform.position = toolPos;
      m_ToolTransform.rotation = InputManager.Brush.Geometry.ToolAttachPoint.rotation;
    } else {
      transform.position = SketchSurfacePanel.m_Instance.transform.position;
      transform.rotation = SketchSurfacePanel.m_Instance.transform.rotation;
    }
  }

  virtual protected void UpdateDetection() {
    //toggle color and reset detection if we changed state
    bool bToolHot = IsHot;
    if (bToolHot != m_ToolWasHot) {
      ResetDetection();
    }

    //always default to resetting detection
    m_ResetDetection = true;

    //update detection if we're hot
    if (bToolHot) {
      if (App.Config.m_UseBatchedBrushes) {
        UpdateBatchedBrushDetection(m_ToolTransform.position);
      } else {
        UpdateSolitaryBrushDetection(m_ToolTransform.position);
      }
    }

    if (m_ResetDetection) {
      ResetDetection();
    }

    m_ToolWasHot = IsHot;
    //DebugDrawBounds();
  }

  override public void UpdateSize(float fAdjustAmount) {
    float fPrevRatio = GetSize01();
    float fCurrentRatio = Mathf.Clamp(fPrevRatio + fAdjustAmount, 0.0f, 1.0f);
    float fRange = m_SizeRange.y - m_SizeRange.x;
    m_CurrentSize = m_SizeRange.x + (fRange * fCurrentRatio);
    m_ToolTransform.localScale = Vector3.one * m_CurrentSize;

    //haptics for sizing
    float fHalfInterval = m_HapticInterval * 0.5f;
    int iPrevInterval = (int)((fPrevRatio + fHalfInterval) / m_HapticInterval);
    int iCurrentInterval = (int)((fCurrentRatio + fHalfInterval) / m_HapticInterval);
    if (iCurrentInterval > iPrevInterval) {
      InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, m_HapticSizeUp);
    } else if (iCurrentInterval < iPrevInterval) {
      InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, m_HapticSizeDown);
    }
  }

  override public float GetSize() {
    return m_CurrentSize;
  }

  override public float GetSize01() {
    float fRange = m_SizeRange.y - m_SizeRange.x;
    if (fRange <= 0.0f) {
      return 1.0f;
    }
    float fRatio = (m_CurrentSize - m_SizeRange.x) / fRange;
    return Mathf.Clamp(fRatio, 0.0f, 1.0f);
  }

  protected void PlayModifyStrokeSound() {
    if (Time.realtimeSinceStartup - m_ModifyStrokeTimestamp > m_ModifyStrokeMinTriggerTime) {
      int iRandIndex = UnityEngine.Random.Range(0, m_ModifyStrokeSounds.Length);
      AudioManager.m_Instance.TriggerOneShot(m_ModifyStrokeSounds[iRandIndex],
        m_ToolTransform.position, 1.0f);
      m_ModifyStrokeTimestamp = Time.realtimeSinceStartup;
    }
  }

  override public bool AllowsWidgetManipulation() {
    return !IsHot;
  }
}
}  // namespace TiltBrush
