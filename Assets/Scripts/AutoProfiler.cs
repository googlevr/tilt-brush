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

/// Constantly writes to a (rotating) log file when enabled.
/// Stops logging and optionally pauses the game when poor performance is detected.
public class AutoProfiler : MonoBehaviour {
  const string LOG_FILE_BASE = "Temp/TiltBrushProfile";
  const int NUM_LOG_FILES = 2;

  /// In seconds: amount of data per log file
  public float m_logRotatePeriod = 4.0f;
  /// If true, use the Editor to stop the game when performance is poor.
  public bool m_pauseAppWhenPoor = true;
  /// Halt when the instantaneous dt is >= this
  public float m_poorDt = 1.0f / 30.0f;
  /// The 90%-window size, in seconds
  public float m_avgWindow = .25f;
  /// Halt when the average dt (over the window) is >= this
  public float m_poorAvgDt = .026f;
  public bool m_enableProfiling = false;
  public bool m_useLogFiles = false;

  [SerializeField]
  private float m_avgDt = 0;
  [SerializeField]
  private string m_disableReason = null;

  private int m_nextFileIndex = 0;
  private bool m_isProfiling = false;
  private float m_nextLogRotateTime = 0;
  private int m_disableAfterFrames = -1;

  void Start() {
#if false
    SketchControlsScript.m_Instance.OnStraightEdgeEnabled += () => {
      if (! m_isProfiling) {
        OutputWindowScript.m_Instance.AddNewLine("Profile: on");
        m_disableAfterFrames = -1;
        EnableProfile();
      } else {
        OutputWindowScript.m_Instance.AddNewLine("Profile: off");
        DisableProfile();
      }
    };
#endif
  }

  void Update() {
    float dt = Time.deltaTime;
    float k = Mathf.Pow(.1f, dt/m_avgWindow);
    m_avgDt = k * m_avgDt + (1-k) * dt;

    if (m_enableProfiling != m_isProfiling) {
      if (m_enableProfiling) {
        EnableProfile();
      } else {
        DisableProfile();
      }
    }

    // Wait for a few frames before pausing the app to make it easier to select
    // the problematic frame (and to avoid disturbing any surrounding frames)
    if (m_disableAfterFrames > -1) {
      m_disableAfterFrames -= 1;
      if (m_disableAfterFrames == -1) {
        OutputWindowScript.m_Instance.AddNewLine("Profile: auto-disable");
        Debug.LogFormat("Profile: auto-disable ({0})", m_disableReason);
        DisableProfile();
#if UNITY_EDITOR
        if (m_pauseAppWhenPoor) {
          UnityEditor.EditorApplication.isPaused = true;
        }
#endif
      }
    }

    if (Time.time >= m_nextLogRotateTime) {
      m_nextLogRotateTime = Time.time + m_logRotatePeriod;
      if (m_isProfiling) {
        OutputWindowScript.m_Instance.AddNewLine("Profile: rotate");
        DisableProfile();
        EnableProfile();
      }
    }

    if (dt >= m_poorDt) {
      SchedulePauseIfEnabled(string.Format("dt {0} >= {1}", dt, m_poorDt));
    } else if (m_avgDt >= m_poorAvgDt) {
      SchedulePauseIfEnabled(string.Format("avg dt {0} >= {1}", m_avgDt, m_poorAvgDt));
    }
  }

  void SchedulePauseIfEnabled(string reason) {
    if (m_isProfiling && m_disableAfterFrames == -1) {
      m_disableReason = reason;
      m_disableAfterFrames = 3;
    }
  }

  void EnableProfile() {
    if (m_isProfiling) {return;}
    m_enableProfiling = true;  // so it doesn't auto-turn-off
    m_isProfiling = true;
    UnityEngine.Profiling.Profiler.enabled = true;
    UnityEngine.Profiling.Profiler.enableBinaryLog = true;
    if (m_useLogFiles) {
      UnityEngine.Profiling.Profiler.logFile = string.Format("{0}{1}.log", LOG_FILE_BASE, m_nextFileIndex);
    } else {
      UnityEngine.Profiling.Profiler.logFile = null;
    }
    m_nextFileIndex = (m_nextFileIndex + 1) % NUM_LOG_FILES;
  }

  void DisableProfile() {
    if (! m_isProfiling) {return;}
    m_enableProfiling = true;  // so it doesn't auto-turn-off
    m_isProfiling = false;
    UnityEngine.Profiling.Profiler.enabled = false;
    UnityEngine.Profiling.Profiler.enableBinaryLog = false;
    UnityEngine.Profiling.Profiler.logFile = null;
  }

#if UNITY_EDITOR
  [UnityEditor.MenuItem("Tilt/Load Profile")]
  static void MenuItem_LoadProfiler() {
    UnityEngine.Profiling.Profiler.AddFramesFromFile(LOG_FILE_BASE);
  }
#endif

}
}  // namespace TiltBrush
