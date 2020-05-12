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

namespace TiltBrush {

public class CameraPathTinter : MonoBehaviour {
  private List<KnotSegment> m_TintedSegments;
  private List<CameraPathKnot> m_TintedKnots;

  private void Awake() {
    m_TintedKnots = new List<CameraPathKnot>();
    m_TintedSegments = new List<KnotSegment>();
  }

  /// Note that after this Update is called, the component is disabled.  If no other
  /// component adds an object to our lists, we won't be ticked.
  void Update() {
    // Untint and turn ourselves off.
    for (int i = 0; i < m_TintedKnots.Count; ++i) {
      m_TintedKnots[i].ActivateTint(false);
    }
    m_TintedKnots.Clear();

    for (int i = 0; i < m_TintedSegments.Count; ++i) {
      m_TintedSegments[i].renderer.material.color = GrabWidget.InactiveGrey;
    }
    m_TintedSegments.Clear();
    enabled = false;
  }

  public void TintKnot(CameraPathKnot knot) {
    knot.ActivateTint(true);
    m_TintedKnots.Add(knot);
    enabled = true;
  }

  public void TintSegment(KnotSegment segment) {
    segment.renderer.material.color = Color.white;
    m_TintedSegments.Add(segment);
    enabled = true;
  }
}
} // namespace TiltBrush