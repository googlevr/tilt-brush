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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class ProfileDisplay : MonoBehaviour {
  [SerializeField] private Text m_Fps;
  [SerializeField] private Text m_FrameTime;
  [SerializeField] private float m_Smoothing;

  private float m_SmothedFrameTime = 0;

  private Dictionary<int, int> m_histBuckets = new Dictionary<int, int>();
  private int m_histTotal = 0;

  public static ProfileDisplay Instance { get; private set; }

  private void Awake() {
    Instance = this;
  }

  private void Start() {
    if (!Debug.isDebugBuild) {
      gameObject.SetActive(false);
    }
  }

  string BuildHistogram() {
    int total = m_histTotal;
    var sb = new System.Text.StringBuilder();

    int i = 0;
    foreach (var kvp in m_histBuckets.AsEnumerable().OrderByDescending((kvp) => kvp.Value/ (float)total)) {
      if (i++ > 4) { break; }
      int pct = Mathf.RoundToInt(kvp.Value / (float)total * 100);
      if (pct < 2) { break; }
      sb.Append(kvp.Key + "ms: " + pct + "% ");
    }

    return sb.ToString();
  }

  private void Update() {
    float frameTime = Time.deltaTime * 1000f;

    int ms = Mathf.RoundToInt(frameTime);
    if (!m_histBuckets.ContainsKey(ms)) {
      m_histBuckets.Add(ms, 0);
    }
    m_histBuckets[ms] = m_histBuckets[ms] + 1;
    m_histTotal++;

    m_FrameTime.text = BuildHistogram();
    if (Time.frameCount % 300 == 0) {
      m_histTotal = 0;
      foreach (int bucket in m_histBuckets.Keys.ToList()) {
        m_histBuckets[bucket] = Mathf.RoundToInt(m_histBuckets[bucket] / 2f);
        m_histTotal += m_histBuckets[bucket];
      }
    }

    m_SmothedFrameTime = Mathf.Lerp(frameTime, m_SmothedFrameTime, m_Smoothing);
    //m_FrameTime.text = string.Format("FrameTime: {0} ms", Mathf.RoundToInt(m_SmothedFrameTime));
    float fps = 1000f / m_SmothedFrameTime;
    m_Fps.text = string.Format("FPS: {0} fps", Mathf.RoundToInt(fps));
  }
} // namespace TiltBrush
