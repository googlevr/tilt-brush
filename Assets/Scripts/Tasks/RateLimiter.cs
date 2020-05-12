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
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TiltBrush {
/// A mechanism for limiting events to a maximum per-frame. For example, limiting the number of
/// thumbnails or models loaded in a single frame.
class RateLimiter {

  // The last frame the limiter saw after a request, as each new frame is seen, the count is reset.
  // This is used in lieu of an Update function to reduce any potential dead weight.
  private int m_lastFrame = 0;

  // The number of events this limiter has seen this frame.
  // Resets to zero after each frame change.
  private uint m_eventCount = 0;

  /// The max number of events per frame, after this number is reached, limits all callers until the
  /// next frame.
  public uint MaxEventsPerFrame {
    get; set;
  }

  public RateLimiter() {
    MaxEventsPerFrame = 1;
  }

  public RateLimiter(uint maxEventsPerFrame) {
    MaxEventsPerFrame = maxEventsPerFrame;
  }

  /// Increments the event count and returns false while event count < MaxEventsPerFrame.
  /// E.g. if max = 1, the first call returns true, the second call returns false.
  public bool IsBlocked() {
    m_eventCount = (m_lastFrame != Time.frameCount) ? 0 : m_eventCount + 1;
    m_lastFrame = Time.frameCount;
    return m_eventCount >= MaxEventsPerFrame;
  }
}

/// Limits events to an average rate.
/// The instantaneous rate may of course be higher than the average rate; how much
/// higher depends on the maximum count.
/// This version is designed to be used with "await" rather than polling.
class AwaitableRateLimiter {
  private readonly float m_rate;
  private readonly int m_maxCount;
  private readonly SemaphoreSlim m_semaphore;
  private float m_partialItems = 0;

  /// Pass:
  ///   rate -- the rate in items / second
  ///   maxCount -- the maximum number of items that can be "queued"
  public AwaitableRateLimiter(
      float rate, int maxCount) {
    if (rate <= 0) { throw new ArgumentException("rate"); }
    if (maxCount <= 0) { throw new ArgumentException("maxCount"); }
    m_rate = rate;
    m_maxCount = maxCount;
    m_semaphore = new SemaphoreSlim(0);
  }

  public Task WaitAsync() {
    return m_semaphore.WaitAsync();
  }

  public void Tick(float dt) {
    m_partialItems += dt * m_rate;
    int produced = (int)Mathf.Floor(m_partialItems);
    m_partialItems = m_partialItems % 1;

    int toAdd = Mathf.Min(m_maxCount - m_semaphore.CurrentCount, produced);
    if (toAdd > 0) {
      m_semaphore.Release(toAdd);
    }
  }
}
} // namespace TiltBrush
