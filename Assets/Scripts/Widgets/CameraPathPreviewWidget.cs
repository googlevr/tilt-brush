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

public class CameraPathPreviewWidget : GrabWidget {
  [SerializeField] private Color m_RecordColor;

  private CameraPathWidget m_CurrentPathWidget;
  private Vector3? m_LastRecordedInputXf;
  private PathT m_PathT;

  // If not null, the widget will use this PathT instead of m_PathT.
  private PathT? m_OverridePathT;

  public PathT? OverridePathT { set { m_OverridePathT = value; } }

  override protected void OnShow() {
    base.OnShow();

    // When we turn this widget on, it may not be in the right spot.  We need to refresh our
    // position here because it takes a frame to get updated and we'll get a 1-frame pop.
    CacheCurrentPathWidget();
    ValidatePathT();
    if (m_CurrentPathWidget != null && m_CurrentPathWidget.Path.NumPositionKnots > 1) {
      transform.position = m_CurrentPathWidget.Path.GetPosition(m_PathT);
      if (m_CurrentPathWidget.Path.RotationKnots.Count > 0) {
        transform.rotation = m_CurrentPathWidget.Path.GetRotation(m_PathT);
      }
    }
  }

  override protected void OnUpdate() {
    base.OnUpdate();

    CacheCurrentPathWidget();
    ValidatePathT();

    if (m_CurrentPathWidget != null && m_CurrentPathWidget.Path.NumPositionKnots > 1) {
      if (m_OverridePathT == null && !m_UserInteracting &&
          WidgetManager.m_Instance.FollowingPath) {
        // It's possible for Path.GetSpeed() to return a value <= 0, which makes the
        // camera path stop advancing.  To correct this, ensure the minimum speed is
        // the lowest speed available for a speed knot.
        float speed = Mathf.Max(m_CurrentPathWidget.Path.GetSpeed(m_PathT),
            CameraPathSpeedKnot.kMinSpeed);
        bool completed = m_CurrentPathWidget.Path.MoveAlongPath(speed * Time.deltaTime,
            m_PathT, out m_PathT);

        if (VideoRecorderUtils.ActiveVideoRecording != null && completed) {
          SketchControlsScript.m_Instance.CameraPathCaptureRig.StopRecordingPath(true);
        }
      }

      // Stay locked on the path.
      PathT t = m_OverridePathT != null ? m_OverridePathT.Value : m_PathT;
      transform.position = m_CurrentPathWidget.Path.GetPosition(t);
      if (m_CurrentPathWidget.Path.RotationKnots.Count > 0) {
        transform.rotation = m_CurrentPathWidget.Path.GetRotation(t);
      }
      float fov = m_CurrentPathWidget.Path.GetFov(t);
      SketchControlsScript.m_Instance.CameraPathCaptureRig.SetFov(fov);
      SketchControlsScript.m_Instance.CameraPathCaptureRig.UpdateCameraTransform(transform);
    }
  }

  void CacheCurrentPathWidget() {
    var data = WidgetManager.m_Instance.GetCurrentCameraPath();
    m_CurrentPathWidget = (data == null) ? null : data.WidgetScript;
  }

  void ValidatePathT() {
    if (m_CurrentPathWidget == null) {
      m_PathT.Zero();
    } else {
      m_PathT.Clamp(m_CurrentPathWidget.Path.PositionKnots.Count);
    }
  }

  override protected TrTransform GetDesiredTransform(TrTransform xf_GS) {
    if (m_CurrentPathWidget == null) {
      return xf_GS;
    }

    // Instead of testing the raw value that comes in from the controller position, test our
    // last valid spline position plus any translation that's happened the past frame.  This
    // method keeds the test positions near the spline, allowing continuous movement when the
    // user has moved beyond the intersection distance to the spline.
    Vector3 positionToProject = xf_GS.translation;
    if (m_LastRecordedInputXf.HasValue) {
      Vector3 translationDiff = xf_GS.translation - m_LastRecordedInputXf.Value;
      positionToProject = transform.position + translationDiff;
    }
    m_LastRecordedInputXf = xf_GS.translation;

    // Project transform on to the path to path t.
    Vector3 error = Vector3.zero;
    if (m_CurrentPathWidget.Path.ProjectPositionOnToPath(
        positionToProject, out PathT t, out error)) {
      m_PathT = t;
    }
    m_LastRecordedInputXf -= error;
    return xf_GS;
  }

  override protected void OnUserEndInteracting() {
    base.OnUserEndInteracting();
    m_LastRecordedInputXf = null;
  }

  override public void RegisterHighlight() {
#if !UNITY_ANDROID
    // Intentionally do not call base class.
    if (m_HighlightMeshFilters != null) {
      for (int i = 0; i < m_HighlightMeshFilters.Length; i++) {
        if (m_HighlightMeshFilters[i].gameObject.activeInHierarchy) {
          App.Instance.SelectionEffect.RegisterMesh(m_HighlightMeshFilters[i]);
        }
      }
    }
#endif
  }

  override public float GetActivationScore(
      Vector3 controllerPos, InputManager.ControllerName name) {
    if (VideoRecorderUtils.ActiveVideoRecording != null || m_OverridePathT != null) {
      return -1.0f;
    }
    return base.GetActivationScore(controllerPos, name);
  }

  public void ResetToPathStart() {
    m_PathT.Zero();
    transform.position = m_CurrentPathWidget.Path.GetPosition(m_PathT);
    if (m_CurrentPathWidget.Path.RotationKnots.Count > 0) {
      transform.rotation = m_CurrentPathWidget.Path.GetRotation(m_PathT);
    }
  }

  public void SetPathT(PathT pathT) {
    if (m_CurrentPathWidget != null) {
      m_PathT = pathT;
      m_PathT.Clamp(m_CurrentPathWidget.Path.PositionKnots.Count);
    }
  }

  public void SetCompletionAlongPath(float completion) {
    if (m_CurrentPathWidget != null) {
      SetPathT(new PathT(completion * (m_CurrentPathWidget.Path.PositionKnots.Count - 1)));
    }
  }

  /// Returns "completion" as a float, [0:1].
  public float? GetCompletionAlongPath() {
    if (m_CurrentPathWidget != null) {
      return m_CurrentPathWidget.Path.GetRatioToPathDistance(m_PathT);
    }
    return null;
  }

  public void TintForRecording(bool record) {
    if (m_TintableMeshes != null) {
      Color color = record ? m_RecordColor : m_InactiveGrey;
      for (int i = 0; i < m_TintableMeshes.Length; ++i) {
        m_TintableMeshes[i].material.color = color;
      }
    }
  }
}

} // namespace TiltBrush