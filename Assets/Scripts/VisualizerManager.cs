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
using System.Collections.Generic;
using Reaktion;

namespace TiltBrush {
//
// Audio Analysis data available for shading
// _WaveFormTex.r = waveform
// _WaveFormTex.g = waveform smoothed
// _WaveFormTex.b = waveform low pass
// _WaveFormTex.a = waveform high pass
//
// _FFTTex.r = FFT
// _FFTTex.g = FFT with Power Curve
// _FFTTex.b = Peak FFT with Power Curve
// _FFTTex.a = Normalized Band Pass Levels
//
// _BeatOutput.xyzw = beat detection from Reaktor (channels 1,2,3,4 are from reaktor, reaktor alt, reaktor low pass, reaktor high pass)
// _BeatOutputAccum.xyzw = accumulated beat detection from Reaktor (channels 1,2,3,4 are from reaktor, reaktor alt, reaktor low pass, reaktor high pass)
// _AudioVolume.xyzw = Real time RMS volume (channels 1,2,3,4 are from reaktor, reaktor alt, reaktor low pass, reaktor high pass)
// _PeakBandLevels.xyzw = m_BandNormalizedLevels[0], m_BandNormalizedLevels[1], m_BandNormalizedLevels], m_BandNormalizedLevels[3],
//



public class VisualizerManager : MonoBehaviour {
  public class Fft {
    public virtual void Add(float[] samples, int count) { }
    public virtual void GetFftData(float[] resultBuffer) { }
  }

  public class Filter {
    public virtual void Process(float[] samples) { }
    public virtual double Frequency { get; set; }
  }

  static public VisualizerManager m_Instance;

  [Header("Band Levels")]
  // These parameters rescale processed audio data like FFT's and Band Levels
  [SerializeField] private float m_BandPeakDecay = .9f;
  [SerializeField] private float m_NormalizedBandPeakLerp = .9f;

  [Header("FFT")]
  [SerializeField] private float m_FFTPeakDecay = .9f;
  [SerializeField] private float m_FFTScale = 2.0f;
  [SerializeField] private float m_FFTPowerScale = 2.0f;
  [SerializeField] private float m_FFTPower = 1.5f;

  [Header("Waveform")]
  [SerializeField] private float m_WeirdWaveformLerp = .7f;
  [SerializeField] private double m_HighPassFreq = 2000;
  [SerializeField] private double m_LowPassFreq = 150;

  [Header("Reaktor")]
  [SerializeField] private SystemAudioInjector m_SystemAudioInjector;
  [SerializeField] private SystemAudioInjector m_SystemAudioInjectorAlt;
  [SerializeField] private SystemAudioInjector m_SystemAudioInjectorLowPass;
  [SerializeField] private SystemAudioInjector m_SystemAudioInjectorHighPass;

  private int m_FFTSize = 512;
  private int m_SampleRate;
  // Band levels and frequencies derived from https://github.com/keijiro/unity-audio-spectrum/blob/master/AudioSpectrum.cs
  private float[] m_Bands = new float[] { 31.5f, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };
  private float m_Bandwidth = 1.414f; // 2^(1/2)

  private float[] m_BandLevels;
  private float[] m_BandPeakLevels;
  private float[] m_BandNormalizedLevels;
  private Texture2D m_WaveFormTexture;
  private Color[] m_WaveFormRow;
  private Texture2D m_FFTTexture;
  private int m_FFTTextureSize = 256;
  private Color[] m_FFTRow;
  private Vector4 m_BandPeakLevelsOutput;
  private Vector4 m_BeatOutput;
  private Vector4 m_BeatOutputAccum;
  private Vector4 m_AudioVolume;

  private Fft m_FFT;
  private Filter m_LowPassFilter;
  private Filter m_HighPassFilter;

  private float[] m_AudioSamples;

  private float[] m_LChannelWeird;
  private float[] m_LChannelHighPass;
  private float[] m_LChannelLowPass;
  private float[] m_FFTResult;
  private float[] m_PeakFFTResult;

  private Reaktor m_Reaktor;
  private Reaktor m_ReaktorAlt;
  private Reaktor m_ReaktorLowPass;
  private Reaktor m_ReaktorHighPass;

  private List<GameObject> m_VisualizerObjects;

  private int m_VisualsRequestCount;
  private bool m_VisualsActive;

  public int FFTSize { get { return m_FFTSize; } }

  // Properties to visualize in the Editor
  public Texture2D WaveformTexture { get { return m_WaveFormTexture; } }
  public Texture2D FFTTexture { get { return m_FFTTexture; } }
  public Vector4 BandPeakLevelsOutput { get { return m_BandPeakLevelsOutput; } }
  public Vector4 BeatOutput { get { return m_BeatOutput; } }
  public Vector4 BeatOutputAccum { get { return m_BeatOutputAccum; } }
  public Vector4 AudioVolume { get { return m_AudioVolume; } }

  public bool VisualsRequested {
    get { return m_VisualsRequestCount > 0; }
  }

  public bool AreVisualsActive {
    get { return m_VisualsActive; }
  }

  void Awake() {
    m_Instance = this;
    m_VisualizerObjects = new List<GameObject>();
    Shader.DisableKeyword("AUDIO_REACTIVE");
      // Two channels, 512 values.
#if DISABLE_AUDIO_CAPTURE
    m_FFT = new Fft();
#else
    m_FFT = new VisualizerCSCoreFft(1, 512);
#endif
      m_FFTResult = new float[m_FFTSize];
    m_PeakFFTResult = new float[m_FFTSize];
    m_BandLevels = new float[m_Bands.Length];
    m_BandPeakLevels = new float[m_Bands.Length];
    m_BandNormalizedLevels = new float[m_Bands.Length];
    m_WaveFormTexture = new Texture2D(m_FFTSize, 1, TextureFormat.ARGB32, true, true);
    m_WaveFormTexture.SetPixels32(new Color32[m_FFTSize]);
    m_WaveFormRow = new Color[m_FFTSize];
    m_FFTTexture = new Texture2D(m_FFTTextureSize, 1, TextureFormat.ARGB32, true, true);
    m_FFTTexture.SetPixels32(new Color32[m_FFTTextureSize]);
    m_FFTRow = new Color[m_FFTTextureSize];

    // Visualization
    m_BandPeakLevelsOutput = Vector4.zero;
    m_BeatOutput = Vector4.zero;
    m_BeatOutputAccum = Vector4.zero;
    m_AudioVolume = Vector4.zero;

    // m_LChannelWeird is a quasi-interpolated waveform used on for shader visuals
    m_LChannelWeird = new float[m_FFTSize];
    m_LChannelHighPass = new float[m_FFTSize];
    m_LChannelLowPass = new float[m_FFTSize];

    m_AudioSamples = new float[m_FFTSize];

    m_Reaktor = m_SystemAudioInjector.GetComponent<Reaktor>();
    m_ReaktorAlt = m_SystemAudioInjectorAlt.GetComponent<Reaktor>();
    m_ReaktorLowPass = m_SystemAudioInjectorLowPass.GetComponent<Reaktor>();
    m_ReaktorHighPass = m_SystemAudioInjectorHighPass.GetComponent<Reaktor>();

    m_VisualsRequestCount = 0;
    m_VisualsActive = false;
  }

  public void RegisterVisualizerObject(GameObject rObject) {
    if (!m_VisualizerObjects.Contains(rObject)) {
      m_VisualizerObjects.Add(rObject);
      if (rObject.GetComponent<RegisterVisualizerObject>().m_ShowInReactorModeOnly) {
        rObject.SetActive(AudioCaptureManager.m_Instance.IsCapturingAudio);
      }
    }
  }

  public void UnregisterVisualizerObject(GameObject rObject) {
    for (int i = 0; i < m_VisualizerObjects.Count; ++i) {
      if (m_VisualizerObjects[i] == rObject) {
        m_VisualizerObjects.RemoveAt(i);
        return;
      }
    }
  }

  public void SetVisualizerObjectActive(bool bActivate) {
    if (bActivate) {
      foreach (GameObject g in m_VisualizerObjects) {
        g.SetActive(true);
      }
      InitializeAllReaktorComponents();
    } else {
      foreach (GameObject g in m_VisualizerObjects) {
        if (g.GetComponent<RegisterVisualizerObject>().m_ShowInReactorModeOnly) {
          g.SetActive(false);
        }
      }
    }
  }

  void InitializeAllReaktorComponents() {
    foreach (GameObject g in m_VisualizerObjects) {
      if (g.GetComponent<MaterialGear>()) {
        g.GetComponent<MaterialGear>().reaktor.Initialize(g.GetComponent<MaterialGear>());
      }
      if (g.GetComponent<TransformGear>()) {
        g.GetComponent<TransformGear>().reaktor.Initialize(g.GetComponent<TransformGear>());
      }
      if (g.GetComponent<ParticleSystemGear>()) {
        g.GetComponent<ParticleSystemGear>().reaktor.Initialize(g.GetComponent<ParticleSystemGear>());
      }
      if (g.GetComponent<LightGear>()) {
        g.GetComponent<LightGear>().reaktor.Initialize(g.GetComponent<LightGear>());
      }
      if (g.GetComponent<ConstantMotionGear>()) {
        g.GetComponent<ConstantMotionGear>().reaktor.Initialize(g.GetComponent<ConstantMotionGear>());
      }
    }
  }

  public void EnableVisuals(bool bEnable) {
    m_VisualsRequestCount += bEnable ? 1 : -1;
    Debug.Assert(m_VisualsRequestCount >= 0);

    if (m_VisualsRequestCount > 0) {
      // If we ask for visuals, don't turn on unless we've got audio.
      ActivateVisuals(AudioCaptureManager.m_Instance.IsCapturingAudio);
    } else {
      ActivateVisuals(false);
    }
  }

  public void AudioCaptureStatusChange(bool bCapturing) {
    // If our audio state changed, enable/disable the visuals accordingly.
    if (m_VisualsRequestCount > 0) {
      ActivateVisuals(bCapturing);
    }
  }

  void ActivateVisuals(bool bEnable) {
    if (bEnable) {
      Shader.EnableKeyword("AUDIO_REACTIVE");
    } else {
      Shader.DisableKeyword("AUDIO_REACTIVE");
    }

    SetVisualizerObjectActive(bEnable);
    if (m_Reaktor) {
      m_Reaktor.enabled = bEnable;
    }
    m_VisualsActive = bEnable;
  }

  public void SetSampleRate(int sampleRate) {
    m_SampleRate = sampleRate;
#if DISABLE_AUDIO_CAPTURE
    m_LowPassFilter = new Filter();
    m_HighPassFilter = new Filter();
#else
    m_LowPassFilter = new VisualizerCSCoreFilter(VisualizerCSCoreFilter.FilterType.Low,
                                                 m_SampleRate, m_LowPassFreq);
    m_HighPassFilter = new VisualizerCSCoreFilter(VisualizerCSCoreFilter.FilterType.High,
                                                  m_SampleRate, m_HighPassFreq);
#endif
    }

  public void ProcessAudio(float[] AudioData, int SampleRate) {
    Debug.Assert(AudioData.Length == m_FFTSize);

    if (SampleRate != m_SampleRate) {
      SetSampleRate(SampleRate);
    }

    m_FFT.Add(AudioData, AudioData.Length);
    m_FFT.GetFftData(m_FFTResult);

    // Calculate Band Levels.  Band Levels are much more useful
    // For visualization / system feedback.  Raw FFT is mostly useless.
    ConvertRawSpectrumToBandLevels();

    // Fill the buffers full
    for (int i = 0; i < m_FFTSize; ++i) {
      float fLChannel = AudioData[i];
      m_AudioSamples[i] = fLChannel;
      // m_RChannelTempBuffer[i] = fRChannel;
      m_LChannelWeird[i] = Mathf.Lerp(fLChannel, m_LChannelWeird[i], m_WeirdWaveformLerp);
      m_PeakFFTResult[i] = Mathf.Max(m_PeakFFTResult[i] * m_FFTPeakDecay, m_FFTResult[i]);
    }

    m_AudioSamples.CopyTo(m_LChannelLowPass, 0);
    m_AudioSamples.CopyTo(m_LChannelHighPass, 0);

    m_LowPassFilter.Frequency = m_LowPassFreq;
    m_HighPassFilter.Frequency = m_HighPassFreq;
    m_LowPassFilter.Process(m_LChannelLowPass);
    m_HighPassFilter.Process(m_LChannelHighPass);

    // Pipe waveform values in to texture.
    for (int i = 0; i < m_FFTSize; ++i) {
      m_WaveFormRow[i].r = m_AudioSamples[i] * 0.5f + 0.5f;
      m_WaveFormRow[i].g = m_LChannelWeird[i] * 0.5f + 0.5f;

      m_WaveFormRow[i].b = m_LChannelLowPass[i] * 0.5f + 0.5f;
      m_WaveFormRow[i].a = m_LChannelHighPass[i] * 0.5f + 0.5f;
    }
    // Pipe FFT values in to texture.
    // Only use the first half of the FFT. Second half is boring.
    for (int i = 0; i < m_FFTTextureSize; ++i) {
      // Mirrored index
      int fMirroredIndex = 128 - Mathf.Abs(i - 128);
      m_FFTRow[i].r = m_FFTResult[fMirroredIndex] * m_FFTScale;
      m_FFTRow[i].g = m_FFTResult[fMirroredIndex] * Mathf.Pow((fMirroredIndex / 512.0f), m_FFTPower) * m_FFTPowerScale;
      m_FFTRow[i].b = m_PeakFFTResult[fMirroredIndex] * Mathf.Pow((fMirroredIndex / 512.0f), m_FFTPower) * m_FFTPowerScale;
    }

    for (int i = 0; i < m_FFTTextureSize; ++i) {
      int band_levels_index = (int) (i * ((float) m_BandLevels.Length / (float)m_FFTTextureSize) );
      m_FFTRow[i].a = m_BandNormalizedLevels[band_levels_index];
    }

    // Pipe values into the Audio Injector for beat detection, if the component exists.
    if (m_SystemAudioInjector) {
      m_SystemAudioInjector.ProcessAudio(m_AudioSamples);
    }
    if (m_SystemAudioInjectorAlt) {
      m_SystemAudioInjectorAlt.ProcessAudio(m_AudioSamples);
    }
    if (m_SystemAudioInjectorLowPass) {
      m_SystemAudioInjectorLowPass.ProcessAudio(m_AudioSamples);
    }
    if (m_SystemAudioInjectorHighPass) {
      m_SystemAudioInjectorHighPass.ProcessAudio(m_AudioSamples);
    }

    //
    // Update Shaders
    //
    Shader.SetGlobalTexture("_WaveFormTex", m_WaveFormTexture);
    Shader.SetGlobalVector("_PeakBandLevels", m_BandPeakLevelsOutput);
    Shader.SetGlobalTexture("_FFTTex", m_FFTTexture);
    Shader.SetGlobalVector("_BeatOutput", m_BeatOutput);
    Shader.SetGlobalVector("_BeatOutputAccum", m_BeatOutputAccum);
    Shader.SetGlobalVector("_AudioVolume", m_AudioVolume);

    m_BandPeakLevelsOutput = new Vector4(m_BandPeakLevels[0], m_BandPeakLevels[1], m_BandPeakLevels[2], m_BandPeakLevels[3]);

    m_WaveFormTexture.SetPixels(0, 0, m_FFTSize, 1, m_WaveFormRow);
    m_WaveFormTexture.Apply();
    m_FFTTexture.SetPixels(0, 0, m_FFTTextureSize, 1, m_FFTRow);
    m_FFTTexture.Apply();

    m_BeatOutput = new Vector4(m_Reaktor.output, m_ReaktorAlt.output, m_ReaktorLowPass.output, m_ReaktorHighPass.output);
    //Scale the accumulated output to a value more usable in shaders (saves us a multiply)
    m_BeatOutputAccum = new Vector4(m_Reaktor.outputAccumulated, m_ReaktorAlt.outputAccumulated, m_ReaktorLowPass.outputAccumulated, m_ReaktorHighPass.outputAccumulated) * .02f;

    // XXX TO DO: Better and more useful conversion from DB to a useful 0:1 range.
    float val1 = (60.0f + m_Reaktor.outputDb) / 60.0f;
    val1 = Mathf.Clamp(val1, 0.0f, 1.0f);
    float val2 = (60.0f + m_ReaktorAlt.outputDb) / 60.0f;
    val2 = Mathf.Clamp(val2, 0.0f, 1.0f);
    float val3 = (60.0f + m_ReaktorLowPass.outputDb) / 60.0f;
    val3 = Mathf.Clamp(val3, 0.0f, 1.0f);
    float val4 = (60.0f + m_ReaktorHighPass.outputDb) / 60.0f;
    val4 = Mathf.Clamp(val3, 0.0f, 1.0f);
    m_AudioVolume = new Vector4(val1, val2, val3, val4);
  }

  int FrequencyToSpectrumIndex(float f) {
    var i = Mathf.FloorToInt(f / m_SampleRate * 2.0f * m_FFTResult.Length);
    return Mathf.Clamp(i, 0, m_FFTResult.Length - 1);
  }

  private void ConvertRawSpectrumToBandLevels() {
    for (var i = 0; i < m_Bands.Length; i++) {
      for (var bi = 0; bi < m_BandLevels.Length; bi++) {
        int imin = FrequencyToSpectrumIndex(m_Bands[bi] / m_Bandwidth);
        int imax = FrequencyToSpectrumIndex(m_Bands[bi] * m_Bandwidth);

        var bandMax = 0.0f;
        for (var fi = imin; fi <= imax; fi++) {
          bandMax = Mathf.Max(bandMax, m_FFTResult[fi]);
        }

        m_BandLevels[bi] = bandMax;
        m_BandPeakLevels[bi] = Mathf.Max(m_BandPeakLevels[bi] * m_BandPeakDecay, bandMax);
        m_BandNormalizedLevels[bi] = Mathf.Lerp(m_BandLevels[bi] / m_BandPeakLevels[bi], m_BandNormalizedLevels[bi], m_NormalizedBandPeakLerp);
      }
    }
  }
}
}  // namespace TiltBrush
