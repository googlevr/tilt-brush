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

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace TiltBrush {

[System.Serializable]
public class GameMusic {
  public AudioClip clip;
  public Texture2D iconImage;
  public string description;
}

// TODO: mutable struct is an accident waiting to happen; replace with class
public class AudioManager : MonoBehaviour {
  class AudioLoop {
    public GvrAudioSource m_GvrAudioSource;
    // This is null if and only if the source is not being used.
    public string m_LoopName;
  }

  static public AudioManager m_Instance;
  static public bool Enabled {
    set {
      m_Instance.m_AudioSfxEnabled = value;
      PointerManager.m_Instance.ResetPointerAudio();
      if (!value) {
        m_Instance.StopAllLoops();
      }
    }
    get {
      return m_Instance.m_AudioSfxEnabled && !App.UserConfig.Flags.DisableAudio;
    }
  }
  private bool m_AudioSfxEnabled;

  [SerializeField] private GameObject m_AudioOneShotPrefab;
  [SerializeField] private int m_NumAudioOneShots;
  private GvrAudioSource[] m_AudioOneShots;
  private int m_NextAvailableAudioOneShot;

  [SerializeField] private GameObject m_AudioLoopPrefab;
  private AudioLoop[] m_AudioLoops;
  [SerializeField] private int m_NumAudioLoops;
  private int m_RecentlyUsedAudioLoop;

  public enum FirstRunMusic {
    IntroQuiet,
    IntroLoud,
    IntroAmbient
  }
  [SerializeField] private AudioClip[] m_FirstRunMusic;
  [SerializeField] private GameMusic[] m_GameMusic;
  [SerializeField] private GameObject m_MusicPrefab;
  [SerializeField] private AudioClip m_IntroReveal;
  private AudioSource m_Music;
  private int m_ActiveGameMusicIndex;

  [SerializeField] private AudioClip m_IntroTransitionSound;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_IntroTransitionGain = 0.0f;
  [SerializeField] private AudioClip m_ActivatePanelSound;
  [SerializeField] private AudioClip m_DeactivatePanelSound;
  [SerializeField] private AudioClip m_PopUpSound;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_PopUpGain = 0.0f;
  [SerializeField] private float m_PanelActivationVolume = 0.5f;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_PanelActivationGain = 0.0f;
  public float m_PanelActivateMinTriggerTime;
  private float m_PanelActivateTimestamp;

  [SerializeField] private AudioClip[] m_ItemHoverSounds;
  [SerializeField] private float m_ItemHoverVolume = 1.0f;
  private int m_ItemHoverSoundIndex;

  [SerializeField] private AudioClip[] m_ItemSelectSounds;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_ItemSelectGain = 0.0f;

  [SerializeField] private AudioClip m_ItemDisabledSound;

  [SerializeField] private AudioClip[] m_UndoSounds;
  [SerializeField] private AudioClip[] m_RedoSounds;

  [SerializeField] private AudioClip m_InitWorldGrabSound;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_InitWorldGrabGain = 0.0f;
  [SerializeField] private float m_InitWorldGrabMinTriggerTime;
  private float m_InitWorldGrabTimestamp;
  [SerializeField] private AudioClip m_WorldGrabLoop;
  public float m_WorldGrabLoopMaxVolume = 0.2f;
  public float m_WorldGrabLoopAttenuation = 4f;
  public float m_WorldGrabLoopSmoothSpeed = 50f;

  [SerializeField] private AudioClip m_WidgetShowSound;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_WidgetShowGain = 0.0f;
  [SerializeField] private AudioClip m_WidgetHideSound;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_WidgetHideGain = 0.0f;
  [SerializeField] private float m_WidgetShowHideMinTriggerTime = 0.1f;
  private float m_WidgetShowHideTimestamp;

  [SerializeField] private AudioClip m_PanelFlipSound;
  [SerializeField] private float m_PanelFlipVolume = 1.0f;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_PanelFlipGain = 0.0f;

  [SerializeField] private AudioClip m_MagicControllerSound;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_MagicControllerGain = 0.0f;
  [SerializeField] private AudioClip m_TeleportSound;
  [Range(0.0f, 1.0f)] [SerializeField] private float m_TeleportVolume = 1.0f;
  [SerializeField] private AudioClip m_DuplicateSound;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_DuplicateGain = 0.0f;
  [SerializeField] private AudioClip m_MirrorSound;
  [SerializeField] private AudioClip m_MirrorReflectionSound;
  [SerializeField] private AudioClip m_ScreenshotSound;
  [Range(0.0f, 1.0f)] [SerializeField] private float m_ScreenshotVolume = 1.0f;
  [SerializeField] private AudioClip m_TrashSound;
  [SerializeField] private AudioClip m_TrashSoftSound;
  // TODO: Should this sound be used or removed?
  [SerializeField] [SuppressMessage("ReSharper", "NotAccessedField.Local")]
  private AudioClip m_CountdownSound;
  [SerializeField] private AudioClip m_HintAnimateSound;
  [SerializeField] private AudioClip m_SliderSound;
  [Range(0.0f, 1.0f)] [SerializeField] private float m_SliderVolume = 1.0f;
  [SerializeField] private AudioClip m_SketchLoadedSound;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_SketchLoadedGain = 0.0f;
  [SerializeField] private AudioClip m_SketchUploadCompleteSound;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_SketchUploadCompleteGain = 0.0f;
  [SerializeField] private AudioClip m_SketchUploadCanceledSound;
  [SerializeField] private AudioClip m_ControllerSwapSound;
  [SerializeField] private AudioClip m_SaveSketchSound;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_SaveSketchGain = 0.0f;
  [SerializeField] private AudioClip m_PinCushionOpenSound;
  [Range(0.0f, 1.0f)] [SerializeField] private float m_PinCushionOpenVolume = 1.0f;
  [SerializeField] private AudioClip m_PinCushionCloseSound;
  [Range(0.0f, 1.0f)] [SerializeField] private float m_PinCushionCloseVolume = 1.0f;
  [SerializeField] private AudioClip m_PinCushionHoverSound;
  [Range(0.0f, 1.0f)] [SerializeField] private float m_PinCushionHoverVolume = 1.0f;
  [SerializeField] private AudioClip m_DropperIntersectionSound;
  [SerializeField] private AudioClip m_DropperPickSound;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_DropperPickGain = 0.0f;
  [SerializeField] private AudioClip m_BasicToAdvancedModeSound;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_BasicToAdvancedModeGain = 0.0f;
  [SerializeField] private AudioClip m_AdvancedToBasicModeSound;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_AdvancedToBasicModeGain = 0.0f;
  [SerializeField] private AudioClip m_TransformResetSound;
  [Range(0.0f, 1.0f)] [SerializeField] private float m_TransformResetVolume = 1.0f;
  [SerializeField] private float m_HintAnimateMinTriggerTime;
  private float m_HintAnimateTimestamp;

  [SerializeField] private AudioClip m_PanelPaneAttachSound;
  [SerializeField] private AudioClip m_PanelPaneMoveSound;
  private float m_PanelPanelMoveTimestamp;
  [SerializeField] private float m_PanelPaneMoveMinTriggerTime = .2f;

  [SerializeField] private AudioClip m_UploadLoop;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_UploadLoopGain = 0.0f;
  [SerializeField] private AudioClip m_UploadLoopQuiet;
  [Range(0.0f, 24.0f)] [SerializeField] private float m_UploadLoopQuietGain = 0.0f;
  [SerializeField] float m_UploadLoopFadeDownDuration = 3f;
  [SerializeField] private AudioClip m_SelectionHighlightLoop;
  [Range(0.0f, 1.0f)] [SerializeField] private float m_SelectionHighlightVolume = 1.0f;
  [SerializeField] private float m_SelectionHighlightFadeDownSpeed = 0.2f;

  public enum PinSoundType {
    Enter,
    Wobble,
    Unpin
  }
  [SerializeField] private AudioClip m_PinEnterSound;
  [SerializeField] private AudioClip[] m_PinWobbleSounds;
  [SerializeField] private AudioClip m_UnpinSound;

  [Header("Selection Toggle Sounds")]
  [SerializeField] private AudioClip m_ToggleToSelect;
  [SerializeField] private AudioClip m_ToggleToDeselect;
  [SerializeField] private float m_SelectionToggleVolume;

  public int NumGameMusics() { return m_GameMusic.Length; }
  public GameMusic GetGameMusic(int index) { return m_GameMusic[index]; }
  public int GetActiveGameMusic() { return m_ActiveGameMusicIndex; }

  void Awake() {
    m_Instance = this;

    // Get that giant spam of clones out of the hierarchy
    Transform audioParent = new GameObject("AudioManager Things").transform;
    audioParent.parent = transform;

    m_AudioOneShots = new GvrAudioSource[m_NumAudioOneShots];
    for (int i = 0; i < m_AudioOneShots.Length; ++i) {
      GameObject audioObj = Instantiate(m_AudioOneShotPrefab, audioParent, true);
      GvrAudioSource audioSource = audioObj.GetComponent<GvrAudioSource>();
      audioSource.disableOnStop = true;

      m_AudioOneShots[i] = audioSource;
    }
    m_NextAvailableAudioOneShot = 0;

    m_AudioLoops = new AudioLoop[m_NumAudioLoops];
    for (int i = 0; i < m_AudioLoops.Length; ++i) {
      GameObject audioObj = Instantiate(m_AudioLoopPrefab, audioParent, true);
      GvrAudioSource audioSource = audioObj.GetComponent<GvrAudioSource>();
      audioSource.disableOnStop = true;
      audioSource.loop = true;

      m_AudioLoops[i] = new AudioLoop {
        m_GvrAudioSource = audioSource,
        m_LoopName = null
      };
    }
    m_RecentlyUsedAudioLoop = 0;

    GameObject musicObj = Instantiate(m_MusicPrefab, audioParent, true);
    m_Music = musicObj.GetComponent<AudioSource>();

    m_ItemHoverSoundIndex = 0;
    m_PanelActivateTimestamp = Time.realtimeSinceStartup;
    m_InitWorldGrabTimestamp = Time.realtimeSinceStartup;
    m_HintAnimateTimestamp = Time.realtimeSinceStartup;

    m_AudioSfxEnabled = false;

    m_ActiveGameMusicIndex = -1;
  }

  public bool StartLoop(AudioClip rClip, string sLoopName, Transform targetTransform,
      float fVolume = 1.0f, float fGain = 0.0f, float fSpatialBlend = 1.0f) {
    if (!Enabled) {
      return false;
    }
    // Search for first unused source; but also search for any source playing the same loop
    // and abort if found.
    int? available = null;
    for (int iter = 0; iter < m_AudioLoops.Length; iter++) {
      int i = (m_RecentlyUsedAudioLoop + iter) % m_AudioLoops.Length;
      if (available == null && m_AudioLoops[i].m_LoopName == null) {
        available = i;
      } else if (m_AudioLoops[i].m_LoopName == sLoopName) {
        return false;
      }
    }
    if (available == null) {
      // TODO: This is not necessarily the oldest loop!
      available = (m_RecentlyUsedAudioLoop + 1) % m_AudioLoops.Length;
    }

    m_RecentlyUsedAudioLoop = available.Value;

    m_AudioLoops[m_RecentlyUsedAudioLoop].m_GvrAudioSource.gameObject.SetActive(true);
    m_AudioLoops[m_RecentlyUsedAudioLoop].m_GvrAudioSource.volume = fVolume;
    m_AudioLoops[m_RecentlyUsedAudioLoop].m_GvrAudioSource.gainDb = fGain;
    m_AudioLoops[m_RecentlyUsedAudioLoop].m_GvrAudioSource.spatialBlend = fSpatialBlend;
    m_AudioLoops[m_RecentlyUsedAudioLoop].m_GvrAudioSource.clip = rClip;
    m_AudioLoops[m_RecentlyUsedAudioLoop].m_GvrAudioSource.transform.SetParent(targetTransform);
    m_AudioLoops[m_RecentlyUsedAudioLoop].m_GvrAudioSource.transform.localPosition = Vector3.zero;
    m_AudioLoops[m_RecentlyUsedAudioLoop].m_LoopName = sLoopName;
    m_AudioLoops[m_RecentlyUsedAudioLoop].m_GvrAudioSource.Play();
    return true;
  }

  public void ChangeLoopVolume(string sLoopName, float fVolume) {
    if (!Enabled) {
      return;
    }
    for (int i = 0; i < m_AudioLoops.Length; i++) {
      if (m_AudioLoops[i].m_LoopName == sLoopName) {
        m_AudioLoops[i].m_GvrAudioSource.volume = fVolume;
        return;
      }
    }
  }

  public void StopLoop(string sLoopName) {
    for (int i = 0; i < m_AudioLoops.Length; i++) {
      if (m_AudioLoops[i].m_LoopName == sLoopName) {
        m_AudioLoops[i].m_GvrAudioSource.Stop();
        m_AudioLoops[i].m_LoopName = null;
        m_AudioLoops[i].m_GvrAudioSource.transform.SetParent(transform);
      }
    }
  }

  public void StopAllLoops() {
    for (int i = 0; i < m_AudioLoops.Length; i++) {
      if (m_AudioLoops[i].m_LoopName != null) {
        m_AudioLoops[i].m_GvrAudioSource.Stop();
        m_AudioLoops[i].m_LoopName = null;
        m_AudioLoops[i].m_GvrAudioSource.transform.SetParent(transform);
      }
    }
  }

  public void SelectionHighlightLoop(bool bActive) {
    if (bActive) {
      if (StartLoop(m_SelectionHighlightLoop, "SelectionHighlight", InputManager.Brush.Transform,
          m_SelectionHighlightVolume, fSpatialBlend: 0.0f)) {
        StartCoroutine(SelectionHighlightFadeDown());
      }
    } else {
      StopLoop("SelectionHighlight");
    }
  }

  IEnumerator SelectionHighlightFadeDown() {
    float fVolume = m_SelectionHighlightVolume;
    while (fVolume > 0) {
      fVolume -= m_SelectionHighlightFadeDownSpeed * m_SelectionHighlightVolume * Time.deltaTime;
      ChangeLoopVolume("SelectionHighlight", fVolume);
      yield return null;
    }
  }

  public void UploadLoop(bool bActive) {
    if (bActive) {
      StartLoop(m_UploadLoop, "UploadLoop", InputManager.Wand.Transform,
          fVolume: 1.0f, fGain: m_UploadLoopGain);
      StartLoop(m_UploadLoopQuiet, "UploadLoopQuiet", InputManager.Wand.Transform,
          fVolume: 0f, fGain: m_UploadLoopQuietGain);
      StartCoroutine("UploadLoopFadeDown");
    } else {
      StopLoop("UploadLoop");
      StopLoop("UploadLoopQuiet");
      StopCoroutine("UploadLoopFadeDown");
    }
  }

  IEnumerator UploadLoopFadeDown() {
    float fRemainingDuration = m_UploadLoopFadeDownDuration;
    float fRatio = 1f;
    while (fRemainingDuration > 0) {
      fRemainingDuration -= Time.deltaTime;
      fRatio = fRemainingDuration / m_UploadLoopFadeDownDuration;
      ChangeLoopVolume("UploadLoop", fRatio);
      ChangeLoopVolume("UploadLoopQuiet", 1f - fRatio);
      yield return null;
    }
    ChangeLoopVolume("UploadLoop", 0f);
    ChangeLoopVolume("UploadLoopQuiet", 1f);
  }

  public void ItemHover(Vector3 vPos) {
    TriggerOneShot(m_ItemHoverSounds[m_ItemHoverSoundIndex], vPos, m_ItemHoverVolume);
    ++m_ItemHoverSoundIndex;
    m_ItemHoverSoundIndex %= m_ItemHoverSounds.Length;
  }

  public void PanelFlip(Vector3 vPos) {
    TriggerOneShot(m_PanelFlipSound, vPos, m_PanelFlipVolume, fGain: m_PanelFlipGain);
  }

  public void ItemSelect(Vector3 vPos) {
    int iRandIndex = UnityEngine.Random.Range(0, m_ItemSelectSounds.Length - 1);
    TriggerOneShot(m_ItemSelectSounds[iRandIndex], vPos, 1.0f, fGain: m_ItemSelectGain);
  }

  public void DisabledItemSelect(Vector3 vPos) {
    TriggerOneShot(m_ItemDisabledSound, vPos, 1.0f);
  }

  public void ActivatePanel(bool bActivate, Vector3 vPos) {
    if (Time.realtimeSinceStartup - m_PanelActivateTimestamp > m_PanelActivateMinTriggerTime) {
      if (bActivate) {
        TriggerOneShot(m_ActivatePanelSound, vPos, m_PanelActivationVolume,
            fGain: m_PanelActivationGain);
      } else {
        TriggerOneShot(m_DeactivatePanelSound, vPos, m_PanelActivationVolume,
            fGain: m_PanelActivationGain);
      }
      m_PanelActivateTimestamp = Time.realtimeSinceStartup;
    }
  }

  public void WorldGrabbed(Vector3 vPos, float fVolume = 1.0f) {
    if (Time.realtimeSinceStartup - m_InitWorldGrabTimestamp > m_InitWorldGrabMinTriggerTime) {
      TriggerOneShot(m_InitWorldGrabSound, vPos, fVolume, fGain: m_InitWorldGrabGain);
      m_InitWorldGrabTimestamp = Time.realtimeSinceStartup;
    }
  }

  public void WorldGrabLoop(bool bActive) {
    if(bActive) {
      StartLoop(m_WorldGrabLoop, "WorldGrab",
          SketchControlsScript.m_Instance.ControllerGrabVisuals.transform, 0.0f);
    } else {
      StopLoop("WorldGrab");
    }
  }

  public void PlayIntroTransitionSound(Vector3 vPos) {
    TriggerOneShot(m_IntroTransitionSound, vPos, 1.0f, fGain: m_IntroTransitionGain);
  }

  public void PlayPopUpSound(Vector3 vPos) {
    TriggerOneShot(m_PopUpSound, vPos, 1.0f, fGain: m_PopUpGain);
  }

  public void ShowHideWidget(bool bShow, Vector3 vPos, float fVolume = 1.0f) {
    if (Time.realtimeSinceStartup - m_WidgetShowHideTimestamp > m_WidgetShowHideMinTriggerTime) {
      if (bShow) {
        TriggerOneShot(m_WidgetShowSound, vPos, fVolume, fGain: m_WidgetShowGain);
      } else {
        TriggerOneShot(m_WidgetHideSound, vPos, fVolume, fGain: m_WidgetHideGain);
      }
      m_WidgetShowHideTimestamp = Time.realtimeSinceStartup;
    }
  }

  public void PlayDuplicateSound(Vector3 vPos) {
    TriggerOneShot(m_DuplicateSound, vPos, 1.0f, fGain: m_DuplicateGain);
  }

  public void PlayTeleportSound(Vector3 vPos) {
    TriggerOneShot(m_TeleportSound, vPos, m_TeleportVolume);
  }

  public void PlayScreenshotSound(Vector3 vPos) {
    TriggerOneShot(m_ScreenshotSound, vPos, m_ScreenshotVolume);
  }

  public void PlayTrashSound(Vector3 vPos) {
    TriggerOneShot(m_TrashSound, vPos, 1.0f);
  }

  public void PlayTrashSoftSound(Vector3 vPos) {
    TriggerOneShot(m_TrashSoftSound, vPos, 1.0f);
  }

  public void PlayHintAnimateSound(Vector3 vPos) {
    if (Time.realtimeSinceStartup - m_HintAnimateTimestamp > m_HintAnimateMinTriggerTime) {
      TriggerOneShot(m_HintAnimateSound, vPos, 1.0f);
      m_HintAnimateTimestamp = Time.realtimeSinceStartup;
    }
  }

  public void PlayDropperIntersectionSound(Vector3 vPos) {
    TriggerOneShot(m_DropperIntersectionSound, vPos, 1.0f);
  }

  public void PlayDropperPickSound(Vector3 vPos) {
    TriggerOneShot(m_DropperPickSound, vPos, 1.0f, fGain: m_DropperPickGain);
  }

  public void PlaySliderSound(Vector3 vPos) {
    TriggerOneShot(m_SliderSound, vPos, m_SliderVolume);
  }

  public void PlayGroupedSound(Vector3 vPos) {
    TriggerOneShot(m_SketchLoadedSound, vPos, 0.5f);
  }

  public void PlayUndoSound(Vector3 vPos) {
    int iRandIndex = UnityEngine.Random.Range(0, m_UndoSounds.Length - 1);
    TriggerOneShot(m_UndoSounds[iRandIndex], vPos, 1.0f);
  }

  public void PlayRedoSound(Vector3 vPos) {
    int iRandIndex = UnityEngine.Random.Range(0, m_RedoSounds.Length - 1);
    TriggerOneShot(m_RedoSounds[iRandIndex], vPos, 1.0f);
  }

  public void PlaySketchLoadedSound(Vector3 vPos) {
    TriggerOneShot(m_SketchLoadedSound, vPos, 1.0f, fGain: m_SketchLoadedGain);
  }

  public void PlayUploadCompleteSound(Vector3 vPos) {
    TriggerOneShot(m_SketchUploadCompleteSound, vPos, 1.0f, fGain: m_SketchUploadCompleteGain);
  }

  public void PlayUploadCanceledSound(Vector3 vPos) {
    TriggerOneShot(m_SketchUploadCanceledSound, vPos, 1.0f);
  }

  public void PlayControllerSwapSound(Vector3 vPos) {
    TriggerOneShot(m_ControllerSwapSound, vPos, 1.0f);
  }

  public void PlayPanelPaneAttachSound(Vector3 vPos) {
    TriggerOneShot(m_PanelPaneAttachSound, vPos, 1.0f);
  }

  public void PlayPanelPaneMoveSound(Vector3 vPos) {
    if(Time.realtimeSinceStartup - m_PanelPanelMoveTimestamp > m_PanelPaneMoveMinTriggerTime) {
      TriggerOneShot(m_PanelPaneMoveSound, vPos, 1.0f);
      m_PanelPanelMoveTimestamp = Time.realtimeSinceStartup;
    }
  }

  public void PlayIntroReveal() {
    TriggerOneShot(m_IntroReveal, InputManager.m_Instance.GetBrushControllerAttachPoint().position,
        1.0f, 0.0f);
  }

  public void PlayMagicControllerSound() {
    TriggerOneShot(m_MagicControllerSound, InputManager.Brush.Transform.position, 1.0f, 
        fGain: m_MagicControllerGain);
  }

  public void PlayMirrorSound(Vector3 vPos) {
    StartCoroutine(MirrorSoundCoroutine(vPos));
  }

  IEnumerator MirrorSoundCoroutine(Vector3 vPos) {
    TriggerOneShot(m_MirrorSound, vPos, .5f, .5f, 1.0f);
    yield return new WaitForSeconds(.4f);
    Vector3 vHeadtoMirror = (vPos - ViewpointScript.Head.position);
    Vector3 vPos2 = ViewpointScript.Head.position + Quaternion.Euler(0f, 120f, 0) 
        * (vHeadtoMirror / 2);
    TriggerOneShot(m_MirrorReflectionSound, vPos2, .1f, .5f, 1.0f);
    yield return new WaitForSeconds(.4f);
    Vector3 vPos3 = ViewpointScript.Head.position + Quaternion.Euler(0f, 240f, 0) 
        * (vHeadtoMirror / 4);
    TriggerOneShot(m_MirrorReflectionSound, vPos3, .03f, .5f, 1.0f);
  }

  public void PlayPinSound(Vector3 vPos, PinSoundType type) {
    switch (type) {
    case PinSoundType.Enter:
      TriggerOneShot(m_PinEnterSound, vPos, 1.0f);
      break;
    case PinSoundType.Unpin:
      TriggerOneShot(m_UnpinSound, vPos, 1.0f);
      break;
    case PinSoundType.Wobble:
      int iRandIndex = UnityEngine.Random.Range(0, m_PinWobbleSounds.Length - 1);
      TriggerOneShot(m_PinWobbleSounds[iRandIndex], vPos, 1.0f);
      break;
    }
  }

  public void PlaySaveSound(Vector3 vPos) {
    TriggerOneShot(m_SaveSketchSound, vPos, 1.0f, fGain: m_SaveSketchGain);
  }

  public void PlayToggleSelect(Vector3 vPos, bool willSelect) {
    TriggerOneShot(willSelect ? m_ToggleToSelect : m_ToggleToDeselect, vPos,
                   m_SelectionToggleVolume);
  }

  public void PlayPinCushionSound(bool bShow) {
    if (bShow) {
      TriggerOneShot(m_PinCushionOpenSound, InputManager.Brush.m_Position, m_PinCushionOpenVolume, fSpatialBlend: .5f);
    } else {
      TriggerOneShot(m_PinCushionCloseSound, InputManager.Brush.m_Position, m_PinCushionCloseVolume, fSpatialBlend: .5f);
    }
  }

  public void PlayPinCushionHoverSound() {
    TriggerOneShot(m_PinCushionHoverSound, InputManager.Brush.m_Position, m_PinCushionHoverVolume, fSpatialBlend: .5f);
  }

  public void PlayTransformResetSound() {
    Vector3 vPos = ViewpointScript.Head.position + Vector3.up * 2.0f;
    TriggerOneShot(m_TransformResetSound, vPos, m_TransformResetVolume, fSpatialBlend: 0.0f);
  }

  public void AdvancedModeSwitch(bool toAdvanced) {
    if (toAdvanced) {
      TriggerOneShot(m_BasicToAdvancedModeSound, InputManager.Wand.m_Position, 1.0f,
          fGain: m_BasicToAdvancedModeGain);
    } else {
      TriggerOneShot(m_AdvancedToBasicModeSound, InputManager.Wand.m_Position, 1.0f,
          fGain: m_AdvancedToBasicModeGain);
    }
  }

  public void PlayFirstRunMusic(FirstRunMusic style, float delay = 0.0f) {
    int iStyle = (int)style;
    if (iStyle < 0 || iStyle >= m_FirstRunMusic.Length) {
      Debug.LogError("Bad index sent to AudioManager.PlayFirstRunMusic()");
      return;
    }

    // Loose mapping between musics and style enum.
    m_Music.clip = m_FirstRunMusic[(int)style];
    if(style == FirstRunMusic.IntroAmbient) {
      m_Music.loop = false;
    }
    m_Music.PlayDelayed(delay);
  }

  public void PlayGameMusic(int index) {
    if (index < 0 || index >= m_GameMusic.Length) {
      Debug.LogError("Bad index sent to AudioManager.PlayGameMusic()");
      return;
    }

    m_ActiveGameMusicIndex = index;

    m_Music.Stop();
    m_Music.clip = m_GameMusic[m_ActiveGameMusicIndex].clip;
    m_Music.Play();
  }

  public void SetMusicVolume(float volume) {
    if (m_Music != null) {
      m_Music.volume = volume;
    }
  }

  public void StopMusic() {
    if (m_Music != null) {
      m_Music.Stop();
    }
    m_ActiveGameMusicIndex = -1;
  }

  public void TriggerOneShot(AudioClip rClip, Vector3 vPos, float fVolume,
      float fSpatialBlend = 1.0f, float fGain = 0.0f) {
    if (Enabled) {
      m_AudioOneShots[m_NextAvailableAudioOneShot].gameObject.SetActive(true);
      m_AudioOneShots[m_NextAvailableAudioOneShot].volume = fVolume;
      m_AudioOneShots[m_NextAvailableAudioOneShot].gainDb = fGain;
      m_AudioOneShots[m_NextAvailableAudioOneShot].spatialBlend = fSpatialBlend;
      m_AudioOneShots[m_NextAvailableAudioOneShot].clip = rClip;
      m_AudioOneShots[m_NextAvailableAudioOneShot].transform.position = vPos;
      m_AudioOneShots[m_NextAvailableAudioOneShot].Play();

      ++m_NextAvailableAudioOneShot;
      m_NextAvailableAudioOneShot %= m_AudioOneShots.Length;
    }
  }

  public void StopAudio() {
    foreach (GvrAudioSource s in m_AudioOneShots) {
      s.Stop();
    }
  }
}
}  // namespace TiltBrush
