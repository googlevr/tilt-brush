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

// Ordering:
// - Viewpoint must come before InputManager (InputManager uses Viewpoint)
public class ViewpointScript : MonoBehaviour {
  static public ViewpointScript m_Instance;

  /// On VR platforms, this may be distinct from the eye camera(s)
  /// On non-VR platforms, this is the same thing as the active camera.
  public static Transform Head {
    get {
      return m_Instance.GetHeadTransform();
    }
  }

  public static Ray Gaze {
    get {
      Transform head = Head;
      return new Ray(head.position, head.forward);
    }
  }

  public Transform m_UserHeadMesh;
  [SerializeField] private GameObject m_DropCamHeadMesh;
  [SerializeField] private Renderer m_FullScreenOverlay;
  [SerializeField] private GameObject m_GroundPlaneOverlay;
  [SerializeField] private Transform m_ExceptionOverlay;

  private bool m_MirrorModeEnabled = false;

  // A solid color fullscreen overlay which can be faded in.
  private enum FullScreenFadeState {
    Default,
    FadingToColor,
    FadingToScene
  }
  private FullScreenFadeState m_FullScreenFadeState;
  private float m_FullScreenFadeAmount;
  private Color m_FullScreenTargetColor;
  private float m_FullScreenFadeSpeed;
  private bool m_FullScreenFadeEatFrame;

  // A ground plane grid which can be faded in.
  private enum GroundPlaneFadeState {
    Default,
    FadingIn,
    FadingOut
  }
  private GroundPlaneFadeState m_GroundPlaneFadeState;
  private float m_GroundPlaneFadeAmount;
  private Color m_GroundPlaneTargetColor;
  private float m_GroundPlaneFadeSpeed;

  // Disallow requests to fade if already in the middle of fading to or from black.
  public bool AllowsFading { get { return m_FullScreenFadeState == FullScreenFadeState.Default; } }

  void Awake() {
    m_Instance = this;
    SetOverlayToBlack();
  }

  void Start() {
    // Rescale the ground plane overlay if we have chaperone bounds.
    Vector3 roomExtents = App.VrSdk.GetRoomExtents();
    Renderer rndr = m_GroundPlaneOverlay.GetComponentInChildren<Renderer>();
    rndr.material.SetFloat("_ChaperoneScaleX", roomExtents.x);
    rndr.material.SetFloat("_ChaperoneScaleZ", roomExtents.z);
  }

  public void Init() {
    // Setparent changed the userhead's local position and rotation to keep it absolutely the same
    // with regards to the world origin, so we have to reset the local transformation to have
    // the mesh properly track with the parent.
    m_UserHeadMesh.SetParent(GetEyeTransform());
    m_UserHeadMesh.localPosition = Vector3.zero;
    m_UserHeadMesh.localRotation = Quaternion.identity;
    m_UserHeadMesh.gameObject.SetActive(true);

    // Position full screen overlay in front of the camera.
    m_FullScreenOverlay.transform.parent = GetEyeTransform();
    Vector3 vLocalPos = Vector3.zero;
    vLocalPos.z += App.VrSdk.GetVrCamera().nearClipPlane + 0.001f;
    m_FullScreenOverlay.transform.localPosition = vLocalPos;
    m_FullScreenOverlay.transform.localRotation = Quaternion.LookRotation(-Vector3.forward);

    // Make the full screen overlay 20% bigger than the center fov so it covers both eyes.
    float fSize = 1.2f
                * Mathf.Tan(App.VrSdk.GetVrCamera().fieldOfView * Mathf.Deg2Rad)
                * vLocalPos.z;
    m_FullScreenOverlay.transform.localScale = Vector3.one * fSize;

    // Same with exception overlay.
    var headTransform = GetHeadTransform();
    m_ExceptionOverlay.parent = headTransform;
    m_ExceptionOverlay.localPosition = Vector3.zero;
    m_ExceptionOverlay.localRotation = Quaternion.identity;
  }

  public void SetOverlayToBlack() {
    m_FullScreenOverlay.material.color = Color.black;
    m_FullScreenFadeAmount = 1.0f;
    m_FullScreenFadeState = FullScreenFadeState.Default;
    m_FullScreenOverlay.enabled = true;
  }

  void Update() {
    // Update the full screen overlay.
    if (m_FullScreenFadeState != FullScreenFadeState.Default) {
      Color overlayColor = m_FullScreenTargetColor;
      if (m_FullScreenFadeState == FullScreenFadeState.FadingToColor) {
        m_FullScreenFadeAmount += m_FullScreenFadeSpeed * Time.deltaTime;
        if (m_FullScreenFadeAmount >= 1.0f) {
          m_FullScreenFadeAmount = 1.0f;
          m_FullScreenFadeState = FullScreenFadeState.Default;
        }
      } else if (m_FullScreenFadeState == FullScreenFadeState.FadingToScene) {
        if (m_FullScreenFadeEatFrame) {
          m_FullScreenFadeEatFrame = false;
        } else {
          m_FullScreenFadeAmount -= m_FullScreenFadeSpeed * Time.deltaTime;
        }
        if (m_FullScreenFadeAmount <= 0.0f) {
          m_FullScreenFadeAmount = 0.0f;
          m_FullScreenFadeState = FullScreenFadeState.Default;
        }
      }
      if (m_FullScreenFadeAmount > 0.0f) {
        overlayColor.a = m_FullScreenFadeAmount;
        m_FullScreenOverlay.material.color = overlayColor;
        m_FullScreenOverlay.enabled = true;
      } else {
        m_FullScreenOverlay.enabled = false;
      }
    }

    // Update the ground plane grid overlay.
    if (m_GroundPlaneFadeState != GroundPlaneFadeState.Default) {
      Color overlayColor = m_GroundPlaneTargetColor;
      if (m_GroundPlaneFadeState == GroundPlaneFadeState.FadingIn) {
        m_GroundPlaneFadeAmount += m_GroundPlaneFadeSpeed * Time.deltaTime;
        if (m_GroundPlaneFadeAmount >= 1.0f) {
          m_GroundPlaneFadeAmount = 1.0f;
          m_GroundPlaneFadeState = GroundPlaneFadeState.Default;
        }
      } else if (m_GroundPlaneFadeState == GroundPlaneFadeState.FadingOut) {
        m_GroundPlaneFadeAmount -= m_GroundPlaneFadeSpeed * Time.deltaTime;
        if (m_GroundPlaneFadeAmount <= 0.0f) {
          m_GroundPlaneFadeAmount = 0.0f;
          m_GroundPlaneFadeState = GroundPlaneFadeState.Default;
        }
      }
      if (m_GroundPlaneFadeAmount > 0.0f) {
        overlayColor.a *= m_GroundPlaneFadeAmount;
        m_GroundPlaneOverlay.GetComponentInChildren<Renderer>().material.color = overlayColor;
        m_GroundPlaneOverlay.SetActive(true);
      } else {
        m_GroundPlaneOverlay.SetActive(false);
      }
    }
  }

  /// Returns transform of the head. On non-VR platforms, this is the same
  /// thing as the active camera's transform.
  Transform GetHeadTransform() {
    return App.VrSdk.GetVrCamera().transform;
  }

  Transform GetEyeTransform() {
    return App.VrSdk.GetVrCamera().transform;
  }

  public void ToggleScreenMirroring() {
    m_MirrorModeEnabled = !m_MirrorModeEnabled;
    App.VrSdk.SetScreenMirroring(m_MirrorModeEnabled);
  }

  // speed must be > 0
  public void FadeToColor(Color overlayColor, float speed) {
    m_FullScreenFadeAmount = 0.0f;
    m_FullScreenTargetColor = overlayColor;
    m_FullScreenFadeSpeed = speed;
    m_FullScreenFadeState = FullScreenFadeState.FadingToColor;
  }

  // speed must be > 0
  public void FadeToScene(float speed) {
    if (m_FullScreenFadeAmount > 0) {
      m_FullScreenFadeSpeed = speed;
      m_FullScreenFadeState = FullScreenFadeState.FadingToScene;
      m_FullScreenFadeEatFrame = true;
    }
  }

  public void FadeGroundPlaneIn(Color groundPlaneColor, float speed) {
    // The chaperone can also be forced on by uncommenting out the following 4 lines:
    //var chaperone = OpenVR.Chaperone;
    //if (chaperone != null ) {
    //  chaperone.ForceBoundsVisible(true);
    //}

    // Also, I tried to get the bounds color with the following, but it always returned white with
    // alpha of 0.2:
    //var chaperone = OpenVR.Chaperone;
    //if (chaperone != null) {
    //  HmdColor_t[] pOutputColorArray = new HmdColor_t[1];
    //  HmdColor_t[] pOutputCameraColor = new HmdColor_t[1];
    //  chaperone.GetBoundsColor(ref pOutputColorArray[0], 1, 0, ref pOutputCameraColor[0]);
    //  m_GroundPlaneTargetColor = new Color(pOutputColorArray[0].r,
    //                                       pOutputColorArray[0].g,
    //                                       pOutputColorArray[0].b,
    //                                       pOutputColorArray[0].a);
    //}

    m_GroundPlaneTargetColor = groundPlaneColor;
    m_GroundPlaneFadeSpeed = speed;
    m_GroundPlaneFadeState = GroundPlaneFadeState.FadingIn;
  }

  public void FadeGroundPlaneOut(float speed) {
    // The chaperone can also be forced off by uncommenting out the following 4 lines:
    //var chaperone = OpenVR.Chaperone;
    //if (chaperone != null ) {
    //  chaperone.ForceBoundsVisible(false);
    //}
    m_GroundPlaneFadeSpeed = speed;
    m_GroundPlaneFadeState = GroundPlaneFadeState.FadingOut;
  }

  public void SetHeadMeshVisible(bool bShow) {
    m_DropCamHeadMesh.gameObject.SetActive(bShow);
  }

}
}  // namespace TiltBrush
