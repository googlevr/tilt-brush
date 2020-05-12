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

/// Teleport tool keeps these bounds objects inactive, so this is pretty much dead code.
public class DynamicBounds : MonoBehaviour {
  [SerializeField] private Transform[] m_BoundsBeams;
  [SerializeField] private Transform[] m_BoundsCorners;
  private Vector3 m_BeamBaseScale;
  private Vector3 m_BoundsScale;

  public Vector3 BoundsScale {
    get { return m_BoundsScale; }
  }

  void Awake() {
    Debug.Assert(m_BoundsBeams.Length == 4);
    Debug.Assert(m_BoundsCorners.Length == 4 || m_BoundsCorners.Length == 0);
    m_BeamBaseScale = m_BoundsBeams[0].localScale;
  }

  public void BuildBounds() {
    Bounds aabb = App.VrSdk.GetRoomBoundsAabb();
    Vector3 min = aabb.min;
    Vector3 max = aabb.max;

    // Emulate the old API, which returned an AABB as 4 points ordered this way
    // TODO: if we want to keep this code, we could rewrite this code to
    // use the aabb directly. But, it's dead code and only really useful for
    // testing the VrSdk API.
    Vector3[] bounds = new[] {
      new Vector3(min.x, 0, max.z),     // forward-left
      new Vector3(max.x, 0, max.z),     // forward-right
      new Vector3(max.x, 0, min.z),     // back-right
      new Vector3(min.x, 0, min.z),     // back-left
    };

    Vector3 extents = App.VrSdk.GetRoomExtents();

    for (int i = 0; i < 4; i++) {
      m_BoundsBeams[i].localPosition = (bounds[i] + bounds[(i+1) % 4]) * 0.5f;
    }

    m_BoundsScale = new Vector3(Mathf.Abs(extents.x), 0.0f, Mathf.Abs(extents.z));

    m_BoundsBeams[0].localScale = new Vector3(m_BoundsScale.x,   m_BeamBaseScale.y, m_BeamBaseScale.z);
    m_BoundsBeams[1].localScale = new Vector3(m_BeamBaseScale.x, m_BeamBaseScale.y, m_BoundsScale.z  );
    m_BoundsBeams[2].localScale = new Vector3(m_BoundsScale.x,   m_BeamBaseScale.y, m_BeamBaseScale.z);
    m_BoundsBeams[3].localScale = new Vector3(m_BeamBaseScale.x, m_BeamBaseScale.y, m_BoundsScale.z  );

    if (m_BoundsCorners.Length > 0) {
      for (int i = 0; i < 4; i++) {
        m_BoundsCorners[i].localPosition = bounds[i];
      }
    }
  }
}
} // namespace TiltBrush
