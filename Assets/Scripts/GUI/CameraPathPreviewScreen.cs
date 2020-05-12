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

public class CameraPathPreviewScreen : OptionButton {
  [SerializeField] private GameObject m_Mesh;
  [SerializeField] private Material m_UninitializedCameraMaterial;
  [SerializeField] private GameObject m_PauseIcon;
  [SerializeField] private GameObject m_PlayIcon;
  [SerializeField] private Collider m_ScrubBar;
  [SerializeField] private Transform m_ScrubBarCursor;
  [SerializeField] private Transform m_ScrubBarForeground;

  private ScreenshotManager m_SsMgr;
  private bool m_PreviewOn;
  private bool m_ScrubBarHasFocus;

  protected override void Awake() {
    base.Awake();
    m_SsMgr = SketchControlsScript.m_Instance.CameraPathCaptureRig.Manager;
    m_Mesh.GetComponent<Renderer>().material = m_UninitializedCameraMaterial;
    m_PreviewOn = false;

    App.Switchboard.CameraPathVisibilityChanged += UpdateScreenVisuals;
    App.Switchboard.CameraPathDeleted += UpdateScreenVisuals;
    App.Switchboard.AllWidgetsDestroyed += UpdateScreenVisuals;
    App.Switchboard.CameraPathCreated += UpdateScreenVisuals;
    App.Switchboard.CameraPathKnotChanged += UpdateScreenVisuals;
    App.Switchboard.ToolChanged += UpdateScreenVisuals;

    m_PauseIcon.SetActive(false);
    m_PlayIcon.SetActive(false);
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    App.Switchboard.CameraPathVisibilityChanged -= UpdateScreenVisuals;
    App.Switchboard.CameraPathDeleted -= UpdateScreenVisuals;
    App.Switchboard.AllWidgetsDestroyed -= UpdateScreenVisuals;
    App.Switchboard.CameraPathCreated -= UpdateScreenVisuals;
    App.Switchboard.CameraPathKnotChanged -= UpdateScreenVisuals;
    App.Switchboard.ToolChanged -= UpdateScreenVisuals;
  }

  void UpdateScreenVisuals() {
    bool previewShouldBeOn = m_SsMgr.LeftEyeMaterialRenderTextureExists &&
        WidgetManager.m_Instance.CanRecordCurrentCameraPath();
    if (previewShouldBeOn != m_PreviewOn) {
      m_PreviewOn = previewShouldBeOn;
      if (m_PreviewOn) {
        m_Mesh.GetComponent<Renderer>().material = m_SsMgr.LeftEyeMaterial;
      } else {
        m_Mesh.GetComponent<Renderer>().material = m_UninitializedCameraMaterial;
      }
    }
  }

  private void Update() {
    float? completion =
        SketchControlsScript.m_Instance.CameraPathCaptureRig.GetCompletionOfCameraAlongPath();
    m_ScrubBar.gameObject.SetActive(m_PreviewOn);
    if (completion.HasValue) {
      PositionScrubBar(completion.Value);
    }
  }

  void PositionScrubBar(float zeroToOne) {
    zeroToOne = Mathf.Clamp01(zeroToOne);

    Vector3 scrubBarForegroundScale = m_ScrubBarForeground.localScale;
    scrubBarForegroundScale.x = zeroToOne;
    m_ScrubBarForeground.localScale = scrubBarForegroundScale;

    Vector3 scrubBarLocalPos = m_ScrubBarForeground.localPosition;
    scrubBarLocalPos.x = -0.5f + (zeroToOne * 0.5f);
    m_ScrubBarForeground.localPosition = scrubBarLocalPos;

    Vector3 scrubBarCursorLocalPos = m_ScrubBarCursor.localPosition;
    scrubBarCursorLocalPos.x = -0.5f + zeroToOne;
    m_ScrubBarCursor.localPosition = scrubBarCursorLocalPos;
  }

  override public void UpdateVisuals() {
    base.UpdateVisuals();
    UpdateScreenVisuals();
    m_PauseIcon.SetActive(m_PreviewOn && WidgetManager.m_Instance.FollowingPath && m_HadFocus);
    m_PlayIcon.SetActive(m_PreviewOn && !WidgetManager.m_Instance.FollowingPath);
  }

  override public void ManagerLostFocus() {
    base.ManagerLostFocus();
    m_PauseIcon.SetActive(false);
    m_PlayIcon.SetActive(false);
  }

  override protected void OnButtonPressed() {
    Vector3 reticlePos = SketchControlsScript.m_Instance.GetUIReticlePos();
    Ray reticleRay = new Ray(reticlePos - transform.forward, transform.forward);

    // Did we hit the scrub bar?
    m_ScrubBarHasFocus = BasePanel.DoesRayHitCollider(reticleRay, m_ScrubBar, true);
    if (!m_ScrubBarHasFocus) {
      // Nope.  Default behavior.
      base.OnButtonPressed();
    }
  }

  override public void ButtonHeld(RaycastHit hitInfo) {
    if (m_ScrubBarHasFocus) {
      SetScrubBarToReticlePosition();
    }
  }

  override public void ButtonReleased() {
    m_ScrubBarHasFocus = false;
  }

  void SetScrubBarToReticlePosition() {
    // Position the scrub bar front and tell the preview screen to move.
    Vector3 reticlePos = SketchControlsScript.m_Instance.GetUIReticlePos();
    Vector3 localHitPoint = m_ScrubBar.transform.InverseTransformPoint(reticlePos);
    float clampedLocalX = Mathf.Clamp(localHitPoint.x, -0.5f, 0.5f);
    float zeroToOne = clampedLocalX + 0.5f;
    PositionScrubBar(zeroToOne);
    SketchControlsScript.m_Instance.CameraPathCaptureRig.
        SetPreviewWidgetCompletionPercent(zeroToOne);
  }
}

}  // namespace TiltBrush
