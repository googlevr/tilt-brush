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
using UnityEditor;
using System.Collections;

namespace TiltBrush {
[CustomEditor(typeof(VisualizerManager))]
public class VisualizerManagerEditor : Editor {
  public override void OnInspectorGUI() {
    base.OnInspectorGUI();
    var t = target as VisualizerManager;
    if (Application.isPlaying && t.gameObject.activeSelf) {
      EditorGUILayout.Space();
      InspectorUtils.LayoutCustomLabel("Visualization Data", 12, FontStyle.Bold, TextAnchor.MiddleCenter);
      EditorGUILayout.Space();

      AudioCaptureManager a = AudioCaptureManager.m_Instance;
      InspectorUtils.LayoutCustomLabel(string.Format("Status: <color={0}>{1}</color>",
        (a != null && a.CaptureRequested ? (a.IsCapturingAudio ? "green" : "orange") : "brown"),
        (a != null && a.CaptureRequested ? (a.IsCapturingAudio ? "Capturing" : "Waiting for Audio") : "Inactive")),
        11, FontStyle.Bold, TextAnchor.MiddleLeft
      );

      EditorGUILayout.Space();
      InspectorUtils.LayoutTexture("Waveform texture:", t.WaveformTexture);
      InspectorUtils.LayoutTexture("FFT texture:", t.FFTTexture);

      InspectorUtils.LayoutBarVec4("BandPeakLevelsOutput", t.BandPeakLevelsOutput, Color.blue);
      InspectorUtils.LayoutBarVec4("BeatOutput", t.BeatOutput, Color.blue);
      InspectorUtils.LayoutBarVec4("BeatOutputAccum", t.BeatOutputAccum, Color.blue, false);
      InspectorUtils.LayoutBarVec4("AudioVolume", t.AudioVolume, Color.blue);

      Repaint();
    } else {
      EditorGUILayout.HelpBox("Hit Play to visualize the output data here", MessageType.Info);
    }
  }
}
}
