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

#if !DISABLE_AUDIO_CAPTURE
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using CSCore.Streams;
using CSCore;
#endif
using System.Collections.Generic;
using System;
using UnityEngine;

namespace TiltBrush {

/// <summary>
/// Searches for sound input within the system's audio devices and feeds the
/// incoming data to VisualizerManager to be used in audio reactive brushes.
///
/// Should be treated as one of several types of audio input the user may use
/// at a time, available or not depending on the platform.
/// </summary>
public class SystemAudioMonitor : MonoBehaviour {
  public enum State { Disabled, Looking, Capturing };

  // Twin ring buffers. For simplicity, they are always considered full.
  public class StereoBuffer {
    private float[] m_LChannelValues;
    private float[] m_RChannelValues;
    private int m_iOldestValue;

    public int Capacity { get { return m_LChannelValues.Length; } }
    public int OldestIndex { get { return m_iOldestValue; } }
    public float[] LChannel { get { return m_LChannelValues; } }
    public float[] RChannel { get { return m_RChannelValues; } }

    public StereoBuffer(int iBufferSize) {
      m_LChannelValues = new float[iBufferSize];
      m_RChannelValues = new float[iBufferSize];
      m_iOldestValue = 0;
    }

    public void Add(float left, float right) {
      m_LChannelValues[m_iOldestValue] = left;
      m_RChannelValues[m_iOldestValue] = right;
      m_iOldestValue = (m_iOldestValue + 1) % Capacity;
    }

    public void CopyFrom(StereoBuffer source) {
      Debug.Assert(Capacity == source.Capacity);
      // TODO: might as well rotate the ring buffer so element 0 is the oldest
      // element; would keep us from having to do an extra copy into m_LChannelTempBuffer
      m_iOldestValue = source.m_iOldestValue;
      Array.Copy(source.m_LChannelValues, m_LChannelValues, Capacity);
      Array.Copy(source.m_RChannelValues, m_RChannelValues, Capacity);
    }

    public float GetValuesSum() {
      float fSum = 0.0f;
      for (int i = 0; i < Capacity; ++i) {
        fSum += Mathf.Abs(m_LChannelValues[i]);
        fSum += Mathf.Abs(m_RChannelValues[i]);
      }
      return fSum;
    }

    public void Clear() {
      Array.Clear(m_LChannelValues, 0, Capacity);
      Array.Clear(m_RChannelValues, 0, Capacity);
      m_iOldestValue = 0;
    }
  }

  [Header("Audio Source")]
  [SerializeField] private float m_AttachToDeviceDataThreshold = 0.1f;
  [SerializeField] private float m_DetachFromDeviceDataThreshold = 0.05f;
  [SerializeField] private float m_SelectDeviceTimeoutDuration;
  [SerializeField] private float m_AbandonDeviceTimeoutDuration;

  // This parameter controls the incoming audio renormalization rate
  [SerializeField] private float m_NormalizationDecayWindowSecs = 5.0f;
  [SerializeField] private float m_NormalizationMaxMultiplier = 100;

  private State m_State;
  private float m_SourcePeak = 0;

  private float[] m_LChannelTempBuffer;
  private float[] m_RChannelTempBuffer;

#if !DISABLE_AUDIO_CAPTURE
  // Data that is only valid in State.Looking
  private Future< Queue<WasapiCapture> > m_CapturesFuture;

  // Data that is valid in State.Looking and State.Capturing
  private WasapiCapture m_AudioCapture;
  private ISampleSource m_FinalSource;
#endif

  // Number found by the most-recent search, or -1
  private int m_nCapturesFound;
  private float m_SelectNextCaptureTimer;
  private bool m_SearchFoundNoAudio;

  // Data that is only valid in State.Capturing
  private float m_AbandondDeviceTimeoutTimer;

  // Data that is valid in State.Looking and State.Capturing
  private StereoBuffer m_HotValues;
  private StereoBuffer m_OperateValues;
  private int m_SampleRate;
  private string m_FriendlyName = null;

  private static bool sm_FoundDevice = false;

  public static bool FoundDevice() {
    return sm_FoundDevice;
  }

  public bool AudioDeviceSelected() {
    return m_State == State.Capturing;
  }

  public int GetAudioDeviceSampleRate() {
    return m_SampleRate;
  }

  void Awake() {
    m_State = State.Disabled;
#if !DISABLE_AUDIO_CAPTURE
    int size = VisualizerManager.m_Instance.FFTSize;
    m_HotValues = new StereoBuffer(size);
    m_OperateValues = new StereoBuffer(size);

    m_LChannelTempBuffer = new float[size];
    m_RChannelTempBuffer = new float[size];
#endif
  }

  /// Return status message of state "Looking", attached device if "Capturing",
  /// or blank if in some other state.
  public string GetCaptureStatusMessage() {
    if (m_State == State.Disabled) { return ""; }
    if (m_FriendlyName != null) {
      return m_FriendlyName;
    } else if (m_nCapturesFound < 0) {
      return "Finding audio sources.";
    } else {
      return m_SearchFoundNoAudio ? "No audio sources available." : "";
    }
  }

#if !DISABLE_AUDIO_CAPTURE

  public void Activate(float delaySeconds) {
    TransitionToLooking(delaySeconds);
  }

  public void Deactivate() {
    TransitionToDisabled();
  }

  void OnApplicationQuit() {
    // Make sure we don't leave an open stream.
    StopCapture();
  }

  void Update() {
    // Early out if the app doesn't have focus.
    if (App.VrSdk.IsAppFocusBlocked()) {
      return;
    }

    switch (m_State) {
    case State.Looking:
      m_OperateValues.CopyFrom(m_HotValues);
      Update_StateLooking();
      break;
    case State.Capturing:
      m_OperateValues.CopyFrom(m_HotValues);
      Update_StateCapturing();
      break;
    }
  }

  // ----------------------------------------------------------------------
  // State transition code
  // ----------------------------------------------------------------------

  // Transition to State.Disabled
  // Valid source states are Disabled, Capturing, Looking.
  void TransitionToDisabled() {
    // Clean up whatever state we were in before
    StopCapture();
    StopFuture();

    // Initialize Disabled
    VisualizerManager.m_Instance.AudioCaptureStatusChange(false);
    m_State = State.Disabled;
  }

  // Transition to State.Looking
  // Valid source states are Disabled, Capturing, Looking.
  // If already in Looking, state will be reset (ie, search will restart from scratch)
  void TransitionToLooking(float delaySeconds=0) {
    // Clean up whatever state we were in before
    // (except the Looking bits, which will be re-initialized)
    StopCapture();
    StopFuture();

    // Initalize Looking
    m_SelectNextCaptureTimer = delaySeconds;
    m_HotValues.Clear();
    VisualizerManager.m_Instance.AudioCaptureStatusChange(false);
    m_State = State.Looking;
  }

  // Transition to State.Capturing
  // Only valid source state is Looking
  void TransitionToCapturing() {
    Debug.Assert(m_State == State.Looking);
    Debug.Assert(m_AudioCapture != null);

    // Clean up Looking
    ControllerConsoleScript.m_Instance.AddNewLine(
        string.Format("Using audio source {0}", m_AudioCapture.Device.FriendlyName));
    StopFuture();

    // Initialize Capturing
    sm_FoundDevice = true;
    m_AbandondDeviceTimeoutTimer = m_AbandonDeviceTimeoutDuration;
    VisualizerManager.m_Instance.AudioCaptureStatusChange(true);
    m_State = State.Capturing;
  }

  // ----------------------------------------------------------------------
  // State code: Looking
  // ----------------------------------------------------------------------

  void Update_StateLooking() {
    Debug.Assert(m_State == State.Looking);
    // Continue looking for device if needed.
    // Currently, this only exits State.Looking when it finds
    // a capture that contains valid data.
    m_SelectNextCaptureTimer -= Time.deltaTime;
    if (m_SelectNextCaptureTimer <= 0.0f) {
      StopCapture();
      m_HotValues.Clear();
      SelectNextCapture();
      if (m_AudioCapture != null) {
        try {
          StartCapture();
        } catch (CSCore.CoreAudioAPI.CoreAudioAPIException e) {
          var line = string.Format("Invalid source ({0})", e);
          ControllerConsoleScript.m_Instance.AddNewLine(line);
          // Keep the timer running so we don't spin rapidly racking up failures
        }
      }
    } else {
      // If the device returned values, see if they're reasonable.
      float fValueSum = m_OperateValues.GetValuesSum();
      if (fValueSum > m_AttachToDeviceDataThreshold) {
        TransitionToCapturing();
      }
    }
  }

  // These WasapiCapture objects _and_ their WasapiCapture.Device members must be
  // Dispose()d when you're done with them.
  static Queue<WasapiCapture> GetAudioCaptures() {
    // This is run on another thread; it should not use UnityEngine APIs
    var q = new Queue<WasapiCapture>();
    using (var deviceEnumerator = new MMDeviceEnumerator())
    using (var activeDevices = deviceEnumerator.EnumAudioEndpoints(
               DataFlow.Render, DeviceState.Active)) {
      foreach (MMDevice device in activeDevices) {
        var audioCapture = new WasapiLoopbackCapture();
        audioCapture.Device = device;
        try {
          audioCapture.Initialize();
          q.Enqueue(audioCapture);
        } catch (CSCore.CoreAudioAPI.CoreAudioAPIException) {
          audioCapture.Device.Dispose();
          audioCapture.Dispose();
        }
      }
    }
    return q;
  }

  static void CleanupAudioCaptures(Queue<WasapiCapture> captures) {
    // This is run on another thread; it should not use UnityEngine APIs
    if (captures != null) {
      while (captures.Count > 0) {
        var capture = captures.Dequeue();
        capture.Device.Dispose();
        capture.Dispose();
      }
    }
  }

  // Helper for State.Looking
  // Updates:
  // - m_CapturesFuture
  // - m_SelectNextCaptureTimer
  // - m_AudioCapture
  // On failure, m_AudioCapture is unchanged (still null).
  void SelectNextCapture() {
    // So we don't have to worry about calling Dispose() on these things
    Debug.Assert(m_AudioCapture == null);
    Debug.Assert(m_FinalSource == null);

    Queue<WasapiCapture> captures;
    if (m_CapturesFuture == null) {
      m_CapturesFuture = new Future<Queue<WasapiCapture>>(GetAudioCaptures, CleanupAudioCaptures);
      m_nCapturesFound = -1;
      m_SearchFoundNoAudio = false;
    }

    if (! m_CapturesFuture.TryGetResult(out captures)) {
      // Future not ready yet.  Retry again next frame; it should complete quickly.
      m_SelectNextCaptureTimer = 0;
      return;
    }

    // The count decrements as we consume, but m_nCapturesFound will be the high water mark
    m_nCapturesFound = Mathf.Max(m_nCapturesFound, captures.Count);

    if (m_nCapturesFound == 0) {
      // Future is ready, but it says there are no sources
      ControllerConsoleScript.m_Instance.AddNewLine("No audio sources available.");
      m_SearchFoundNoAudio = true;
      StopFuture();  // Re-query for captures after timeout
      m_SelectNextCaptureTimer = m_SelectDeviceTimeoutDuration;
      return;
    }

    if (captures.Count == 0) {
      // Future is ready, and there were sources, but we've tried them all
      StopFuture();  // Re-query for captures next frame
      m_SelectNextCaptureTimer = 0;
      return;
    }

    var capture = captures.Dequeue();
    string line = string.Format(
        "Trying {1}/{2}: {0}", capture.Device.FriendlyName,
        m_nCapturesFound - captures.Count, m_nCapturesFound);
    ControllerConsoleScript.m_Instance.AddNewLine(line);
    m_AudioCapture = capture;
    m_SampleRate = m_AudioCapture.WaveFormat.SampleRate;
    m_FriendlyName = m_AudioCapture.Device.FriendlyName;
    m_SelectNextCaptureTimer = m_SelectDeviceTimeoutDuration;
  }

  // Helper for State.Looking
  void StartCapture() {
    Debug.Assert(m_State == State.Looking);
    Debug.Assert(m_AudioCapture != null);

    // TODO: This starts as a WaveSource (raw bytes), converts to floats
    // so we can notify once for each sample.
    // The SingleBlockNotificationStream is very garbagey; we should use our own
    // wrapper that grabs all the samples read and pushes them into m_HotValues
    // en masse instead of one-at-a-time.
    var soundInSource = new SoundInSource(m_AudioCapture);
    var sampleSource = soundInSource.ToSampleSource();
    var singleBlockNotificationStream = new SingleBlockNotificationStream(sampleSource);
    m_FinalSource = singleBlockNotificationStream;

    // Consume and discard any bytes when they come in. We do this for
    // its side effects (firing the SingleBlockNotificationStream event).
    // buffer is closed-over by the lambda.
    float[] buffer = new float[m_FinalSource.WaveFormat.BytesPerSecond / 4];
    soundInSource.DataAvailable += (s, e) => {
      int read;
      do {
        read = m_FinalSource.Read(buffer, 0, buffer.Length);
      } while (read > 0);
    };

    singleBlockNotificationStream.SingleBlockRead += SingleBlockNotificationStreamOnSingleBlockRead;
    m_AudioCapture.Start();
  }

  // Safe to call even if not capturing
  void StopCapture() {
    // IXxxSource wrappers dispose of their wrapped IXxxSource instances,
    // but SoundInSource won't dispose of the WasapiCapture m_AudioCapture that it wraps.
    // WasapiCapture doesn't dispose of its .Device.

    // Make copies, because C# lambda captures act like references
    var finalSource = m_FinalSource;
    var audioCapture = m_AudioCapture;

    m_FinalSource = null;
    m_AudioCapture = null;
    m_SampleRate = 0;
    m_FriendlyName = null;

    if (finalSource != null || audioCapture != null) {
      // Don't dispose on our precious main thread; Wasapi's .Stop() has a thread join :(
      new Future<object>(() => {
          if (finalSource != null) { finalSource.Dispose(); }
          if (audioCapture != null) { audioCapture.Dispose(); }
          return null;
        });
    }
  }

  void SingleBlockNotificationStreamOnSingleBlockRead(object sender, SingleBlockReadEventArgs e) {
    // Normalize incoming values.
    m_SourcePeak = Mathf.Max(m_SourcePeak, e.Left);
    m_SourcePeak = Mathf.Max(m_SourcePeak, e.Right);

    float renormalizationMultiplier = 1.0f / m_SourcePeak;
    renormalizationMultiplier = Mathf.Min(renormalizationMultiplier, m_NormalizationMaxMultiplier);
    float leftNormalized = e.Left * renormalizationMultiplier;
    float rightNormalized = e.Right * renormalizationMultiplier;

    m_HotValues.Add(leftNormalized, rightNormalized);
    if (VideoRecorderUtils.ActiveVideoRecording != null) {
      VideoRecorderUtils.ActiveVideoRecording.ProcessAudio(leftNormalized, rightNormalized);
    }
  }

  // Safe to call even if no Future
  void StopFuture() {
    if (m_CapturesFuture != null) {
      m_CapturesFuture.Close();
    }
    m_CapturesFuture = null;
  }

  // ----------------------------------------------------------------------
  // State code: Capturing
  // ----------------------------------------------------------------------

  void Update_StateCapturing() {
    Debug.Assert(m_State == State.Capturing);
    // If we've found a device, process the data and update our shaders.
    {
      // m_LChannelTempBuffer gets the rotated version of the m_OperateValues ring buffer
      // Fill the buffers full
      // TODO: re-use the channel-detect logic for channel-disconnect, and use RMS instad of sum
      float sum = 0;
      int iOperateValueIndex = m_OperateValues.OldestIndex;
      for (int i = 0; i < VisualizerManager.m_Instance.FFTSize; ++i) {
        int iIndex = (iOperateValueIndex + i) % VisualizerManager.m_Instance.FFTSize;
        float fLChannel = m_OperateValues.LChannel[iIndex];
        float fRChannel = m_OperateValues.RChannel[iIndex];
        m_LChannelTempBuffer[i] = fLChannel;
        m_RChannelTempBuffer[i] = fRChannel;
        sum += Mathf.Abs(fLChannel);
      }

      VisualizerManager.m_Instance.ProcessAudio(m_LChannelTempBuffer,
          AudioCaptureManager.m_Instance.SampleRate);

      // Check for abandoning this device due to inactivity.
      if (sum < m_DetachFromDeviceDataThreshold) {
        m_AbandondDeviceTimeoutTimer -= Time.deltaTime;
        if (m_AbandondDeviceTimeoutTimer <= 0.0f) {
          // This device has been silent for too long.  Look for a new one.
          ControllerConsoleScript.m_Instance.AddNewLine("Audio source abandoned. Unresponsive " +
              m_AbandonDeviceTimeoutDuration.ToString() + " seconds.");
          TransitionToLooking();
        }
      } else {
        m_AbandondDeviceTimeoutTimer = m_AbandonDeviceTimeoutDuration;
      }

      // Check for abandoning due to connection.
      if (m_AudioCapture == null
          || m_AudioCapture.Device == null
          || m_AudioCapture.Device.DeviceState != DeviceState.Active) {
        ControllerConsoleScript.m_Instance.AddNewLine("Audio source disconnected.");
        TransitionToLooking();
      }

      // Decay the maximum volume level used for renormalizing incoming audio
      float k = Mathf.Pow(.1f, Time.deltaTime / m_NormalizationDecayWindowSecs);
      float desiredPeak = 0;
      m_SourcePeak = k * m_SourcePeak + (1 - k) * desiredPeak;
    }
  }

#else

  public void Activate(float unused) { }

  public void Deactivate() { }

#endif

}
}  // namespace TiltBrush
