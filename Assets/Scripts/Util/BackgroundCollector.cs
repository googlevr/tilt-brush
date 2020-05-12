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

using System.Threading;

namespace TiltBrush {

  /// This class is responsible for collecting garbage on a background thread.
  /// Note that it is only appropriate to use this pattern when exactly zero allocations are
  /// expected for the duration of garbage collection (5 to 10ms). If an allocation happens sooner,
  /// the allocating thread will block until garbage collection has completed.
  ///
  /// Currently only collects generation zero.
  ///
  /// TODO: This class could use a single task rather than a long running thread.
  class BackgroundCollector {
    private AutoResetEvent m_sleeper;
    private Thread m_thread;

    public void Start() {
      if (m_thread != null) {
        UnityEngine.Debug.LogError("Start() called while already running");
        return;
      }
      m_sleeper = new AutoResetEvent(false);

      // Start status reader
      m_thread = new Thread(Run);
      m_thread.IsBackground = true;
      m_thread.Start();
    }

    public void Collect() {
      if (m_thread == null) {
        UnityEngine.Debug.LogError("Collect() called while not running");
        return;
      }
      m_sleeper.Set();
    }

    public void Stop() {
      if (m_thread == null) {
        // Redundant calls to stop are ignored.
        return;
      }

      m_thread.Interrupt();
      m_thread = null;
    }

    private void Run() {
      try {
        while (true) {
          m_sleeper.WaitOne();
          System.GC.Collect(0);
        }
      } catch (System.Threading.ThreadInterruptedException) {
        // This is fine, the render thread sent an interrupt.
      } catch (System.Exception e) {
        UnityEngine.Debug.LogError(e);
      } finally {
        m_sleeper.Close();
        m_sleeper = null;
      }
    }
  }
} // namespace TiltBrush