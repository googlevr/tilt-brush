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

using TMPro;
using UnityEngine;

namespace TiltBrush {

  public class GvrOverlay : MonoBehaviour {
    [SerializeField] MeshRenderer m_ProgressIndicator;
    [SerializeField] TextMeshPro m_Message;
    // Optional parameter for specifying which camera will have the culling
    // mask modified.  If null, we get the camera from the VrSdk.
    [SerializeField] Camera m_VrCamera;

    // We store the camera with which we've modified the culling mask because
    // on startup, m_VrCamera can be destroyed before we've reset the culling
    // mask on it.  Note that in this scenario, it doesn't matter that it
    // wasn't restored because it's gone.
    private Camera m_ModifiedCamera;

    public MeshRenderer ProgressIndicator {
      get { return m_ProgressIndicator; }
    }

    public TextMeshPro Status {
      get { return m_Message; }
    }

    public float Progress {
      set { m_ProgressIndicator.material.SetFloat("_Progress", value); }
    }

    private const int kOverlayMask = 1 << 24;

    private int m_OriginalCameraCullingMask;

    public void OnEnable() {
      m_ModifiedCamera = (m_VrCamera != null) ? m_VrCamera : App.VrSdk.GetVrCamera();  
      if (m_OriginalCameraCullingMask != kOverlayMask) {
        m_OriginalCameraCullingMask = m_ModifiedCamera.cullingMask;
      }
      m_ModifiedCamera.cullingMask = kOverlayMask;
    }

    public void OnDisable() {
      if (m_ModifiedCamera != null) {
        m_ModifiedCamera.cullingMask = m_OriginalCameraCullingMask;
      }
    }
  }

}  // namespace TiltBrush