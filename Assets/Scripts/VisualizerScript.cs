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
using System.IO;
using System;
using UnityEngine;
using UnityEngine.Audio;
using Reaktion;

namespace TiltBrush {

public class VisualizerScript : MonoBehaviour {
  public bool m_Active;

  private enum LoadMusicState {
    Playing,
    WaitingForWWW,
    WaitingForAudio,
    WaitingForStart,
    MicMode
  }
  private LoadMusicState m_CurrentLoadMusicState;
  private int m_LoadMusicIndex;
  private WWW m_LoadMusicWWW;

  public float m_SmoothLerp = .2f;

  //wave form and fft captures
  private int m_CaptureSize;
  private float[] m_WaveFormFloats;
  private float[] m_WaveFormFloatsSmooth;
  private float[] m_SpectrumFloats;
  private float[] m_SpectrumFloatsTempArray;
  private float[] m_SpectrumFloatsAccumulated;
  private Texture2D m_WaveFormTexture;
  private int m_WaveFormTextureWidth;
  private int m_WaveFormTextureHeight;
  private Color[] m_WaveFormRow;

  //microphone input
  public int m_MicResetDuration;
  private float m_MicResetCountdown;

  public float m_SpectrumAccumulatedFactor;
  private AudioSource m_AudioSource;

  public AudioMixerGroup m_MasterAudioMixerGroup;
  public AudioMixerGroup m_MutedAudioMixerGroup;

  [NonSerialized]
  public Reaktor m_Reaktor;

  void Awake() {
    m_Reaktor = GetComponent<Reaktor>();
  }

  void Start() {
    m_AudioSource = GetComponent<AudioSource>();
    Activate(false);

    m_CaptureSize = 512;

    m_WaveFormTextureWidth = 512;
    m_WaveFormTextureHeight = 1;
    m_WaveFormTexture = new Texture2D(m_WaveFormTextureWidth, m_WaveFormTextureHeight, TextureFormat.ARGB32, true);
    m_WaveFormTexture.SetPixels32(new Color32[m_WaveFormTextureWidth * m_WaveFormTextureHeight]);
    m_WaveFormRow = new Color[m_WaveFormTextureWidth];

    m_WaveFormFloats = new float[m_CaptureSize];
    m_WaveFormFloatsSmooth = new float[m_CaptureSize];
    m_SpectrumFloats = new float[m_CaptureSize];
    m_SpectrumFloatsTempArray = new float[m_CaptureSize];
    m_SpectrumFloatsAccumulated = new float[m_CaptureSize];
  }

  public bool IsActive() { return m_Active; }

  public void Activate(bool bActive) {
    m_Active = bActive;
    if (bActive) {
      //start by looking for music
      m_CurrentLoadMusicState = LoadMusicState.Playing;
      LoadNextSong();
      m_Reaktor.enabled = true;
    } else {
      //turn off audio and the mic
      m_AudioSource.Stop();
      EnableMic(false);
      m_Reaktor.enabled = false;
    }
  }

  void Update() {
    if (m_Active) {
      //get fft and wave form data from unity/fmod
      m_AudioSource.GetOutputData(m_WaveFormFloats, 0);
      m_AudioSource.GetSpectrumData(m_SpectrumFloats, 0, FFTWindow.BlackmanHarris);
      m_SpectrumFloats.CopyTo(m_SpectrumFloatsTempArray, 0);

      int iHalfCapture = m_CaptureSize / 2;
      float fHalfFFTScale = 0.5f;
      for (int i = 0; i < iHalfCapture; ++i) {
        float fNormalized = (m_SpectrumFloatsTempArray[i * 2] + m_SpectrumFloatsTempArray[i * 2 + 1]) * fHalfFFTScale;
        m_SpectrumFloats[i] = fNormalized;
        m_SpectrumFloats[i + iHalfCapture] = fNormalized;
      }

      for (int i = 0; i < m_CaptureSize; ++i) {
        // Maybe the envelope should be a lookup function
        float fEnvelope = 1.0f - Mathf.Pow(Mathf.Abs(((float)i / (float)m_CaptureSize) - 0.5f) * 2, 3.0f);
        m_WaveFormFloatsSmooth[i] = Mathf.Lerp(m_WaveFormFloatsSmooth[i], m_WaveFormFloats[i], m_SmoothLerp) * fEnvelope;
        m_WaveFormRow[i].a = m_WaveFormFloatsSmooth[i] + 0.5f;
      }

      UpdateShaders();

      if (m_CurrentLoadMusicState == LoadMusicState.Playing) {
        if (!m_AudioSource.isPlaying) {
          //loop music
          LoadNextSong();
        }
      } else if (m_CurrentLoadMusicState == LoadMusicState.WaitingForWWW) {
        if (m_LoadMusicWWW.isDone) {
          if (m_LoadMusicWWW.GetAudioClip() == null) {
            LoadNextSong();
          } else {
            m_AudioSource.clip = m_LoadMusicWWW.GetAudioClip(false);
            m_CurrentLoadMusicState = LoadMusicState.WaitingForAudio;
          }
        }
      } else if (m_CurrentLoadMusicState == LoadMusicState.WaitingForAudio) {
        if (m_AudioSource.clip.loadState == AudioDataLoadState.Loaded) {
          if (AudioManager.Enabled) {
            m_AudioSource.Play();
          }
          m_CurrentLoadMusicState = LoadMusicState.WaitingForStart;
        }
      } else if (m_CurrentLoadMusicState == LoadMusicState.WaitingForStart) {
        if (m_AudioSource.isPlaying) {
          m_CurrentLoadMusicState = LoadMusicState.Playing;
        }
      } else if (m_CurrentLoadMusicState == LoadMusicState.MicMode) {
        m_MicResetCountdown -= Time.deltaTime;
        if (m_MicResetCountdown <= 0.0f) {
          EnableMic(false);
          EnableMic(true);
        }

#if !UNITY_ANDROID
        //start playing on audio source when mic is ready to go
        if (Microphone.IsRecording("") && (Microphone.GetPosition("") > 0) && !m_AudioSource.isPlaying) {
          m_AudioSource.Play();
        }
#endif
      }

      m_WaveFormTexture.SetPixels(0, 0, m_WaveFormTextureWidth, 1, m_WaveFormRow);
      m_WaveFormTexture.Apply();

      Shader.SetGlobalTexture("_WaveFormTex", m_WaveFormTexture);
    }
  }

  void EnableMic(bool bEnable) {
#if !UNITY_ANDROID
    if (bEnable) {
      m_AudioSource.Stop();
      m_AudioSource.clip = Microphone.Start("", true, m_MicResetDuration + 1, 44100);
      m_MicResetCountdown = (float)m_MicResetDuration;
      m_AudioSource.outputAudioMixerGroup = m_MutedAudioMixerGroup;
    } else {
      Microphone.End("");
      m_AudioSource.outputAudioMixerGroup = m_MasterAudioMixerGroup;
    }
#endif
  }

  void UpdateShaders() {
    // Update shaders
    Shader.SetGlobalVector("_FFT", new Vector4(m_SpectrumFloats[4], m_SpectrumFloats[16], m_SpectrumFloats[64], m_SpectrumFloats[256]));
    Shader.SetGlobalVector("_FFTAccumulated", new Vector4(m_SpectrumFloatsAccumulated[4], m_SpectrumFloatsAccumulated[16], m_SpectrumFloatsAccumulated[64], m_SpectrumFloatsAccumulated[256]));
    Shader.SetGlobalFloat("_BeatOutput", m_Reaktor.output);
    Shader.SetGlobalFloat("_BeatOutputAccum", m_Reaktor.outputAccumulated * .02f);
    Shader.SetGlobalFloat("_AudioVolume", Mathf.SmoothStep(-80.0f, 10.0f, m_Reaktor.outputDb));
    for (int i = 0; i < m_SpectrumFloatsAccumulated.Length; ++i) {
      // We get crazy values when the mic resets
      if (m_SpectrumFloats[i] < 1.0f) {
        m_SpectrumFloatsAccumulated[i] += m_SpectrumFloats[i] * m_SpectrumAccumulatedFactor;
      }
    }
  }

  void LoadNextSong() {
    string sMusicDirectory = Path.Combine(App.UserPath(), "Music");

    if (Directory.Exists(sMusicDirectory)) {
      string[] aFiles = Directory.GetFiles(sMusicDirectory);
      for (int i = 0; i < aFiles.Length; ++i) {
        int iIndex = m_LoadMusicIndex + i;
        iIndex %= aFiles.Length;
        ++m_LoadMusicIndex;

        if (File.Exists(aFiles[iIndex])) {
          print("Loading: " + aFiles[iIndex]);
          EnableMic(false);
          m_LoadMusicWWW = new WWW("file:///" + aFiles[iIndex]);
          m_CurrentLoadMusicState = LoadMusicState.WaitingForWWW;
          return;
        }
      }
    }

    // No clips found-- enable the mic
    m_CurrentLoadMusicState = LoadMusicState.MicMode;
    EnableMic(true);
  }
}
}  // namespace TiltBrush
