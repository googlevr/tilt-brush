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

namespace TiltBrush {

/// Maintain stats on frame timing.
public class FrameTimingInfo : MonoBehaviour {
  public static FrameTimingInfo m_Instance;

  /// max frame timing history to retain (Update count)
  public int m_FrameTimingHistorySize;

  private Queue<int> m_DroppedFramesQueue;
  private int m_RollingDroppedFrameCount = 0;
  private int m_LifetimeDroppedFrameCount = 0;

  /// aggregate dropped frame count over app lifetime
  public int LifetimeDroppedFrameCount { get { return m_LifetimeDroppedFrameCount; } }

  /// aggregate dropped frame count over history window
  public int RollingDroppedFrameCount { get { return m_RollingDroppedFrameCount; } }

  void Awake() {
    m_Instance = this;
  }

  void Start() {
    m_DroppedFramesQueue = new Queue<int>();
  }

  void Update() {
    if (m_DroppedFramesQueue.Count >= m_FrameTimingHistorySize) {
      var oldItem = m_DroppedFramesQueue.Dequeue();
      m_RollingDroppedFrameCount -= oldItem;
    }
    int? droppedFramesNullable = App.VrSdk.GetDroppedFrames();
    if (droppedFramesNullable.HasValue) {
      int droppedFrames = droppedFramesNullable.Value;
      m_RollingDroppedFrameCount += droppedFrames;
      m_LifetimeDroppedFrameCount += droppedFrames;
      m_DroppedFramesQueue.Enqueue(droppedFrames);
      if (droppedFrames > 0) {
        OnDroppedFrames();
      }
    }

    // Oculus only computes cumulative frames dropped, so we reset the perf stats
    // each frame after recording.
    if (App.Config.m_SdkMode == SdkMode.Oculus) {
      App.VrSdk.ResetPerfStats();
    }
  }

  public event Action OnDroppedFrames = delegate {};
}
}  // namespace TiltBrush
