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
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush {

public class CameraPathWidget : GrabWidget {
  [SerializeField] private float m_KnotSegmentRadius;
  [SerializeField] private float m_EndRadius;
  [SerializeField] private float m_DefaultSpeed;
  [SerializeField] private float m_DefaultFov;
  [SerializeField] private float m_KnotSnapDistanceToEnd;

  private CameraPath m_Path;

  // Array of KnotDescriptors used for testing collision with the individual knots of
  // the widget.  There is no concept of manipulating the entire CameraPathWidget with
  // the controller-- only the knots.
  // Indexed by controller type.
  private KnotDescriptor[] m_LastValidCollisionResults;
  private KnotDescriptor[] m_LastCollisionResults;
  private KnotDescriptor m_ActiveKnot;

  private bool m_EatInteractingInput;
  private Vector3? m_KnotEditingLastInputXf;
  private float m_GrabControlInitialYDiff;

  public CameraPath Path { get { return m_Path; } }

  override public Transform GrabTransform_GS {
    get {
      if (m_ActiveKnot == null) {
        throw new ArgumentNullException("m_ActiveKnot null in GrabTransform_GS");
      }
      return m_ActiveKnot.knot.GetGrabTransform(m_ActiveKnot.control);
    }
  }

  override protected void Awake() {
    base.Awake();

    m_LastValidCollisionResults = new KnotDescriptor[(int)InputManager.ControllerName.Num];
    m_LastCollisionResults = new KnotDescriptor[(int)InputManager.ControllerName.Num];
    for (int i = 0; i < m_LastValidCollisionResults.Length; ++i) {
      m_LastValidCollisionResults[i] = new KnotDescriptor();
      m_LastCollisionResults[i] = new KnotDescriptor();
    }

    m_Path = new CameraPath(transform, m_KnotSegmentRadius,
        m_EndRadius, m_DefaultSpeed, m_DefaultFov);

    App.Scene.MainCanvas.PoseChanged += OnPoseChanged;
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    m_Path.Reset();
    App.Scene.MainCanvas.PoseChanged -= OnPoseChanged;
  }

  override protected void OnShow() {
    base.OnShow();
    m_Path.SetKnotsActive(true);
  }

  override protected void OnHide() {
    base.OnHide();
    m_Path.SetKnotsActive(false);
  }

  override public void RestoreFromToss() {
    base.RestoreFromToss();
    WidgetManager.m_Instance.SetCurrentCameraPath(this);
    App.Switchboard.TriggerCameraPathCreated();
  }

  void OnPoseChanged(TrTransform prev, TrTransform current) {
    // Attempting to start a coroutine when inactive was throwing an error.
    if (gameObject.activeSelf) {
      // Refresh our visuals, but do it a frame later.
      StartCoroutine(m_Path.DelayRefresh());
    }
  }

  public override bool CanSnapToHome() { return false; }

  public void SetAsActivePath(bool active) {
    for (int i = 0; i < m_Path.AllKnots.Count; ++i) {
      m_Path.AllKnots[i].SetActivePathVisuals(active);
    }
  }

  override public void RecordAndSetPosRot(TrTransform inputXf) {
    // Don't manipulate anything if we're eating input.
    if (m_EatInteractingInput) {
      return;
    }

    SnapEnabled = (m_ActiveKnot.knot.KnotType == CameraPathKnot.Type.Rotation ||
                   m_ActiveKnot.knot.KnotType == CameraPathKnot.Type.Position) &&
                  InputManager.Controllers[(int)m_InteractingController].GetCommand(
                      InputManager.SketchCommands.MenuContextClick);
    inputXf = GetDesiredTransform(inputXf);

    if (m_ActiveKnot.knot.KnotType == CameraPathKnot.Type.Position) {
      // Move the base knot.
      if (m_ActiveKnot.control == 0) {
        // If this knot is the tail or the head and we're within snapping distance of the other
        // end, snap to the transform.
        TrTransform snappedXf = inputXf;
        int positionKnot = m_ActiveKnot.positionKnotIndex.Value;
        if (positionKnot == 0 || positionKnot == Path.NumPositionKnots - 1) {
          int otherIndex = positionKnot == 0 ? Path.NumPositionKnots - 1 : 0;
          float distToOther = Vector3.Distance(inputXf.translation,
              Path.PositionKnots[otherIndex].KnotXf.position);
          if (distToOther < m_KnotSnapDistanceToEnd) {
            snappedXf.translation = Path.PositionKnots[otherIndex].KnotXf.position;
            snappedXf.rotation = Path.PositionKnots[otherIndex].KnotXf.rotation;
          }
        }

        SketchMemoryScript.m_Instance.PerformAndRecordCommand(
            new MovePositionKnotCommand(m_Path, m_ActiveKnot, snappedXf));
      } else {
        // Modify the knot tangents.
        CameraPathPositionKnot pk = m_ActiveKnot.knot as CameraPathPositionKnot;
        if (SnapEnabled) {
          Vector3 snappedTranslation = inputXf.translation;
          snappedTranslation.y = pk.transform.position.y;
          inputXf.translation = snappedTranslation;
        }
        float tangentMag = pk.GetTangentMagnitudeFromControlXf(inputXf);
        Vector3 knotFwd = (inputXf.translation - m_ActiveKnot.knot.transform.position).normalized;
        if ((CameraPathPositionKnot.ControlType)m_ActiveKnot.control ==
            CameraPathPositionKnot.ControlType.TangentControlBack) {
          knotFwd *= -1.0f;
        }
        SketchMemoryScript.m_Instance.PerformAndRecordCommand(
            new ModifyPositionKnotCommand(m_Path, m_ActiveKnot, tangentMag, knotFwd));
      }
      return;
    }

    // Constrain rotation and speed knots to the path.
    // Instead of testing the raw value that comes in from the controller position, test our
    // last valid path position plus any translation that's happened the past frame.  This
    // method keeps the test positions near the path, allowing continuous movement when the
    // user has moved beyond the intersection distance to the path.
    Vector3 positionToProject = inputXf.translation;
    if (m_KnotEditingLastInputXf.HasValue) {
      Vector3 translationDiff = inputXf.translation - m_KnotEditingLastInputXf.Value;
      positionToProject = m_ActiveKnot.knot.KnotXf.position + translationDiff;
    }
    m_KnotEditingLastInputXf = inputXf.translation;

    // Project transform on to the path to get t.
    Vector3 error = Vector3.zero;
    if (m_Path.ProjectPositionOnToPath(positionToProject, out PathT pathT, out error)) {
      // Move the base knot.
      if (m_ActiveKnot.control == 0) {
        // Path constrained knots are a little sticky on the ends of the path.  Knots very
        // near the ends *probably* want to be on the ends, and when there are small deltas
        // near the ends, it causes unwanted erratic curves.
        m_ActiveKnot.pathT = m_Path.MaybeSnapPathTToEnd(pathT, m_KnotSnapDistanceToEnd);

        // Rotation knots allow the user to place the preview widget at their position
        // for live preview.
        if (m_ActiveKnot.knot.KnotType == CameraPathKnot.Type.Rotation) {
          CheckForPreviewWidgetOverride(pathT);
        }

        SketchMemoryScript.m_Instance.PerformAndRecordCommand(
            new MoveConstrainedKnotCommand(m_Path, m_ActiveKnot, inputXf.rotation));
      } else {
        // Alternate controls.
        BaseControllerBehavior b = InputManager.Controllers[(int)m_InteractingController].Behavior;
        float controllerY = b.PointerAttachPoint.transform.position.y;

        if (m_ActiveKnot.knot.KnotType == CameraPathKnot.Type.Speed) {
          CameraPathSpeedKnot sk = m_ActiveKnot.knot as CameraPathSpeedKnot;
          float speed = sk.GetSpeedValueFromY(controllerY - m_GrabControlInitialYDiff);
          SketchMemoryScript.m_Instance.PerformAndRecordCommand(
              new ModifySpeedKnotCommand(sk, speed));
        } else if (m_ActiveKnot.knot.KnotType == CameraPathKnot.Type.Fov) {
          CameraPathFovKnot fk = m_ActiveKnot.knot as CameraPathFovKnot;
          float fov = fk.GetFovValueFromY(controllerY - m_GrabControlInitialYDiff);
          CheckForPreviewWidgetOverride(fk.PathT);
          SketchMemoryScript.m_Instance.PerformAndRecordCommand(
              new ModifyFovKnotCommand(fk, fov));
        }
      }
    }
    m_KnotEditingLastInputXf -= error;
  }

  override protected TrTransform GetSnappedTransform(TrTransform xf_GS) {
    TrTransform outXf_GS = xf_GS;

    // Lock to horizon line (upside-down or right-side up).
    Vector3 forward = xf_GS.forward;
    forward.y = 0.0f;
    Vector3 up = (xf_GS.up.y < 0.0f) ? Vector3.down : Vector3.up;

    outXf_GS.rotation = Quaternion.LookRotation(forward.normalized, up);
    return outXf_GS;
  }

  override public float GetActivationScore(Vector3 point, InputManager.ControllerName name) {
    float nearestKnotScore = -1.0f;
    KnotDescriptor nearestResult = new KnotDescriptor();

    if (VideoRecorderUtils.ActiveVideoRecording == null &&
        !WidgetManager.m_Instance.WidgetsDormant &&
        !InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate)) {
      // Check against all knots and put our results in storage bins.
      // Note that we're walking along the sorted lists instead of using m_Path.m_AllKnots.
      // This ensures the knotIndex stored in KnotCollisionResult is correct.
      for (int i = 0; i < m_Path.PositionKnots.Count; ++i) {
        int control = -1;
        CameraPathPositionKnot pk = m_Path.PositionKnots[i];
        float knotScore = pk.gameObject.activeSelf ?
            pk.CollisionWithPoint(point, out control) : -1.0f;
        if (knotScore > nearestKnotScore) {
          nearestResult.Set(pk, control, i, null);
          nearestKnotScore = knotScore;
        }
      }
      for (int i = 0; i < m_Path.RotationKnots.Count; ++i) {
        int control = -1;
        CameraPathRotationKnot rk = m_Path.RotationKnots[i];
        float knotScore = rk.CollisionWithPoint(point, out control);
        if (knotScore > nearestKnotScore) {
          nearestResult.Set(rk, control, null, rk.PathT);
          nearestKnotScore = knotScore;
        }
      }
      for (int i = 0; i < m_Path.SpeedKnots.Count; ++i) {
        int control = -1;
        CameraPathSpeedKnot sk = m_Path.SpeedKnots[i];
        float knotScore = sk.CollisionWithPoint(point, out control);
        if (knotScore > nearestKnotScore) {
          nearestResult.Set(sk, control, null, sk.PathT);
          nearestKnotScore = knotScore;
        }
      }
      for (int i = 0; i < m_Path.FovKnots.Count; ++i) {
        int control = -1;
        CameraPathFovKnot fk = m_Path.FovKnots[i];
        float knotScore = fk.CollisionWithPoint(point, out control);
        if (knotScore > nearestKnotScore) {
          nearestResult.Set(fk, control, null, fk.PathT);
          nearestKnotScore = knotScore;
        }
      }
    }

    bool brushOrWand = (name == InputManager.ControllerName.Brush) ||
        (name == InputManager.ControllerName.Wand);
    if (!m_UserInteracting) {
      if (brushOrWand) {
        m_LastCollisionResults[(int)name].Set(nearestResult);
        if ((nearestResult.knot != null) && (nearestResult.control != -1)) {
          m_LastValidCollisionResults[(int)name].Set(nearestResult);
        }
      }
    } else if (m_InteractingController != name && brushOrWand) {
      m_LastCollisionResults[(int)name].Set(null, CameraPathKnot.kDefaultControl, null, null);
    }
    return nearestKnotScore;
  }

  public void TintSegments(Vector3 pos) {
    // Run through all the segments and do gross checks inside the bounds.
    for (int i = 0; i < m_Path.Segments.Count; ++i) {
      if (m_Path.Segments[i].extents.Contains(pos)) {
        WidgetManager.m_Instance.PathTinter.TintSegment(m_Path.Segments[i]);
      }
    }
  }

  override public void Activate(bool active) {
    base.Activate(active);
    if (active && m_LastCollisionResults != null) {
      for (int i = 0; i < m_LastCollisionResults.Length; ++i) {
        if (m_LastCollisionResults[i].knot != null) {
          WidgetManager.m_Instance.PathTinter.TintKnot(m_LastCollisionResults[i].knot);
        }
      }
    }
  }

  override public void RegisterHighlight() {
    // Intentionally do not call base class.
    for (int i = 0; i < m_LastCollisionResults.Length; ++i) {
      if (m_LastCollisionResults[i].knot != null) {
        // Don't highlight hidden knots.
        if (m_LastCollisionResults[i].knot.gameObject.activeSelf) {
          m_LastCollisionResults[i].knot.RegisterHighlight(m_LastCollisionResults[i].control);
        }
      }
    }
  }

  override protected void UnregisterHighlight() {
    // Intentionally do not call base class.
    for (int i = 0; i < m_LastCollisionResults.Length; ++i) {
      if (m_LastCollisionResults[i].knot != null) {
        m_LastCollisionResults[i].knot.UnregisterHighlight();
      }
    }
  }

  public void HighlightEntirePath() {
    CameraPathTinter t = WidgetManager.m_Instance.PathTinter;
    for (int i = 0; i < Path.PositionKnots.Count; ++i) {
      CameraPathPositionKnot pk = Path.PositionKnots[i];
      pk.RegisterHighlight(0, true);
      t.TintKnot(pk);
    }
    for (int i = 0; i < Path.RotationKnots.Count; ++i) {
      CameraPathRotationKnot rk = Path.RotationKnots[i];
      rk.RegisterHighlight(0, true);
      t.TintKnot(rk);
    }
    for (int i = 0; i < Path.SpeedKnots.Count; ++i) {
      CameraPathSpeedKnot sk = Path.SpeedKnots[i];
      sk.RegisterHighlight(0, true);
      t.TintKnot(sk);
    }
    for (int i = 0; i < Path.FovKnots.Count; ++i) {
      CameraPathFovKnot fk = Path.FovKnots[i];
      fk.RegisterHighlight(0, true);
      t.TintKnot(fk);
    }
    for (int i = 0; i < Path.Segments.Count; ++i) {
      t.TintSegment(m_Path.Segments[i]);
    }
  }

  override public void AssignControllerMaterials(InputManager.ControllerName controller) {
    if (m_InteractingController != controller) {
      return;
    }

    // Snap is allowed on rotation and position knots.
    bool show = (m_ActiveKnot != null) &&
        (m_ActiveKnot.knot.KnotType == CameraPathKnot.Type.Rotation ||
         m_ActiveKnot.knot.KnotType == CameraPathKnot.Type.Position);
    InputManager.GetControllerGeometry(m_InteractingController)
                .TogglePadSnapHint(SnapEnabled, show);
  }

  override protected void OnUserBeginInteracting() {
    GrabWidgetData data = WidgetManager.m_Instance.GetCurrentCameraPath();
    m_EatInteractingInput = (data == null) ? false : data.m_WidgetScript != this;
    WidgetManager.m_Instance.SetCurrentCameraPath(this);

    Debug.Assert(m_InteractingController == InputManager.ControllerName.Brush ||
        m_InteractingController == InputManager.ControllerName.Wand);
    m_ActiveKnot = m_LastValidCollisionResults[(int)m_InteractingController];

    // If we just grabbed a knot control, record the Y position of the controller grab point.
    if (m_ActiveKnot.control != CameraPathKnot.kDefaultControl) {
      BaseControllerBehavior b = InputManager.Controllers[(int)m_InteractingController].Behavior;
      if (m_ActiveKnot.knot.KnotType == CameraPathKnot.Type.Fov) {
        CameraPathFovKnot fovKnot = m_ActiveKnot.knot as CameraPathFovKnot;
        m_GrabControlInitialYDiff = b.PointerAttachPoint.transform.position.y -
            fovKnot.GetGrabTransform(
              (int)CameraPathFovKnot.ControlType.FovControl).position.y;
      }
      if (m_ActiveKnot.knot.KnotType == CameraPathKnot.Type.Speed) {
        CameraPathSpeedKnot speedKnot = m_ActiveKnot.knot as CameraPathSpeedKnot;
        m_GrabControlInitialYDiff = b.PointerAttachPoint.transform.position.y -
            speedKnot.GetGrabTransform(
              (int)CameraPathSpeedKnot.ControlType.SpeedControl).position.y;
      }
    }
  }

  override protected void OnUserEndInteracting() {
    base.OnUserEndInteracting();

    if (!m_EatInteractingInput) {
      // Finalize our move commands with the current state of whatever's being actively modified.
      switch (m_ActiveKnot.knot.KnotType) {
      case CameraPathKnot.Type.Position:
        if (m_ActiveKnot.control == 0) {
          SketchMemoryScript.m_Instance.PerformAndRecordCommand(
              new MovePositionKnotCommand(m_Path, m_ActiveKnot,
                TrTransform.FromTransform(m_ActiveKnot.knot.transform), true));
        } else {
          CameraPathPositionKnot pk = m_ActiveKnot.knot as CameraPathPositionKnot;
          Vector3 knotFwd = m_ActiveKnot.knot.transform.forward;
          SketchMemoryScript.m_Instance.PerformAndRecordCommand(
              new ModifyPositionKnotCommand(
                m_Path, m_ActiveKnot, pk.TangentMagnitude, knotFwd, final:true));
        }
        break;
      case CameraPathKnot.Type.Rotation:
      case CameraPathKnot.Type.Speed:
      case CameraPathKnot.Type.Fov:
        // Reset any PreviewWidget overrides.
        m_ActiveKnot.knot.gameObject.SetActive(true);
        InputManager.Wand.Geometry.PreviewKnotHint.Activate(false);
        InputManager.Brush.Geometry.PreviewKnotHint.Activate(false);
        SketchControlsScript.m_Instance.CameraPathCaptureRig.OverridePreviewWidgetPathT(null);

        if (m_ActiveKnot.control == 0) {
          SketchMemoryScript.m_Instance.PerformAndRecordCommand(
              new MoveConstrainedKnotCommand(m_Path, m_ActiveKnot,
                m_ActiveKnot.knot.transform.rotation, final:true));
        } else {
          if (m_ActiveKnot.knot.KnotType == CameraPathKnot.Type.Speed) {
            CameraPathSpeedKnot sk = m_ActiveKnot.knot as CameraPathSpeedKnot;
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ModifySpeedKnotCommand(sk, sk.SpeedValue));
          } else if (m_ActiveKnot.knot.KnotType == CameraPathKnot.Type.Fov) {
            CameraPathFovKnot fk = m_ActiveKnot.knot as CameraPathFovKnot;
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ModifyFovKnotCommand(fk, fk.FovValue));
          }
        }
        break;
      }
    }

    m_KnotEditingLastInputXf = null;
    m_ActiveKnot = null;
  }

  void CheckForPreviewWidgetOverride(PathT pathT) {
    bool input = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate);
    m_ActiveKnot.knot.gameObject.SetActive(!input);
    PathT? pt = null;
    if (input) { pt = pathT; }
    InputManager.GetControllerGeometry(m_InteractingController).PreviewKnotHint.Activate(!input);
    SketchControlsScript.m_Instance.CameraPathCaptureRig.OverridePreviewWidgetPathT(pt);
  }

  public void ExtendPath(Vector3 pos, CameraPathTool.ExtendPathType extendType) {
    Debug.Assert(extendType != CameraPathTool.ExtendPathType.None);
    int index = (extendType == CameraPathTool.ExtendPathType.ExtendAtHead) ? 0 :
        Path.NumPositionKnots;
    // If we're extending the path into a loop, ignore the passed position.
    if (extendType == CameraPathTool.ExtendPathType.Loop) {
      pos = Path.PositionKnots[0].transform.position;
    }
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(new CreatePathKnotCommand(
        this, CameraPathKnot.Type.Position, new PathT(index), TrTransform.T(pos)));
  }

  public void AddPathConstrainedKnot(CameraPathKnot.Type type, Vector3 pos, Quaternion rot) {
    // Determine the position knot and path t for this new knot.
    Vector3 error = Vector3.zero;

    if (m_Path.ProjectPositionOnToPath(pos, out PathT pathT, out error)) {
      // For PositionKnots, validT is the position in the position list.
      if (type == CameraPathKnot.Type.Position) {
        pathT = new PathT(pathT.T + 1.0f);
      }

      SketchMemoryScript.m_Instance.PerformAndRecordCommand(new CreatePathKnotCommand(
          this, type, pathT, TrTransform.TR(pos, rot)));
    }
  }

  public bool RemoveKnotAtPosition(Vector3 pos) {
    CameraPathKnot removeKnot = m_Path.GetKnotAtPosition(pos);
    if (removeKnot != null) {
      // If we're removing the last position knot, just destroy the whole path.
      if (removeKnot is CameraPathPositionKnot && m_Path.NumPositionKnots == 1) {
        WidgetManager.m_Instance.DeleteCameraPath(this);
      } else {
        TrTransform knotXf = TrTransform.TR(pos, removeKnot.transform.rotation);

        // Gather all the knots affected by this removal.
        List<CameraPathKnot> pks = m_Path.GetKnotsOrphanedByKnotRemoval(removeKnot);
        // If we have any knots affected by this change, we need to remove those first, before
        // we remove the original knot.  This is because, on undo, we need to add the parent
        // first, before adding all the orphaned knots.
        if (pks.Count > 0) {
          BaseCommand parent = new BaseCommand();
          for (int i = 0; i < pks.Count; ++i) {
            TrTransform childXf = TrTransform.TR(pos, pks[i].transform.rotation);
            new RemovePathKnotCommand(this, pks[i], childXf, parent);
          }
          new RemovePathKnotCommand(this, removeKnot, knotXf, parent);
          SketchMemoryScript.m_Instance.PerformAndRecordCommand(parent);
        } else {
          SketchMemoryScript.m_Instance.PerformAndRecordCommand(
              new RemovePathKnotCommand(this, removeKnot, knotXf));
        }
      }

      // Reset collision results after a delete.
      for (int i = 0; i < m_LastCollisionResults.Length; ++i) {
        m_LastCollisionResults[i].Set(null, CameraPathKnot.kDefaultControl, null, null);
      }
    }
    return removeKnot != null;
  }

  public bool ShouldSerialize() {
    return m_Path.PositionKnots.Count > 0;
  }

  public CameraPathMetadata AsSerializable() {
    return m_Path.SerializeToCameraPathMetadata();
  }

  static public void CreateFromSaveData(CameraPathMetadata cameraPath) {
    // Create a new widget.
    CameraPathWidget widget = Instantiate<CameraPathWidget>(
        WidgetManager.m_Instance.CameraPathWidgetPrefab);
    widget.transform.parent = App.Scene.MainCanvas.transform;

    // The scale of path widgets is arbitrary.  However, the scale should be one at creation
    // time so the knots added below have appropriate mesh scales.
    widget.transform.localScale = Vector3.one;
    widget.transform.localPosition = Vector3.zero;
    widget.transform.localRotation = Quaternion.identity;

    // Add the path knots and set their tangent speed.
    for (int i = 0; i < cameraPath.PathKnots.Length; ++i) {
      GameObject go = Instantiate<GameObject>(
          WidgetManager.m_Instance.CameraPathPositionKnotPrefab);
      go.transform.position = cameraPath.PathKnots[i].Xf.translation;
      go.transform.rotation = cameraPath.PathKnots[i].Xf.rotation;
      go.transform.parent = widget.transform;

      CameraPathPositionKnot knot = go.GetComponent<CameraPathPositionKnot>();
      knot.TangentMagnitude = cameraPath.PathKnots[i].TangentMagnitude;

      widget.m_Path.PositionKnots.Add(knot);
      widget.m_Path.AllKnots.Add(knot);

      if (i > 0) {
        widget.m_Path.Segments.Add(CameraPath.CreateSegment(widget.transform));
      }
    }

    // Refresh the path so the segment curves are correct.
    for (int i = 0; i < cameraPath.PathKnots.Length - 1; ++i) {
      widget.m_Path.RefreshSegment(i);
    }

    // Add the rotation knots.  Note this list is ordered, and they're serialized in order,
    // so we need to make sure they're created in order.
    for (int i = 0; i < cameraPath.RotationKnots.Length; ++i) {
      GameObject go = Instantiate<GameObject>(
          WidgetManager.m_Instance.CameraPathRotationKnotPrefab);
      go.transform.position = cameraPath.RotationKnots[i].Xf.translation;
      go.transform.rotation = cameraPath.RotationKnots[i].Xf.rotation;
      go.transform.parent = widget.transform;

      CameraPathRotationKnot knot = go.GetComponent<CameraPathRotationKnot>();
      knot.PathT = new PathT(cameraPath.RotationKnots[i].PathTValue);
      knot.DistanceAlongSegment = widget.m_Path.GetSegmentDistanceToT(knot.PathT);

      widget.m_Path.RotationKnots.Add(knot);
      widget.m_Path.AllKnots.Add(knot);
    }
    // Align quaternions on all rotation knots so we don't have unexpected camera flips
    // when calculating rotation as we walk the path.
    widget.m_Path.RefreshRotationKnotPolarities();

    // Add the speed knots.  Note this list is ordered, and they're serialized in order,
    // so we need to make sure they're created in order.
    for (int i = 0; i < cameraPath.SpeedKnots.Length; ++i) {
      GameObject go = Instantiate<GameObject>(
          WidgetManager.m_Instance.CameraPathSpeedKnotPrefab);
      go.transform.position = cameraPath.SpeedKnots[i].Xf.translation;
      go.transform.rotation = cameraPath.SpeedKnots[i].Xf.rotation;
      go.transform.parent = widget.transform;

      CameraPathSpeedKnot knot = go.GetComponent<CameraPathSpeedKnot>();

      knot.PathT = new PathT(cameraPath.SpeedKnots[i].PathTValue);
      knot.DistanceAlongSegment = widget.m_Path.GetSegmentDistanceToT(knot.PathT);
      knot.SpeedValue = cameraPath.SpeedKnots[i].Speed;

      widget.m_Path.SpeedKnots.Add(knot);
      widget.m_Path.AllKnots.Add(knot);
    }

    // Add the fov knots.  Note this list is ordered, and they're serialized in order,
    // so we need to make sure they're created in order.
    for (int i = 0; i < cameraPath.FovKnots.Length; ++i) {
      GameObject go = Instantiate<GameObject>(
          WidgetManager.m_Instance.CameraPathFovKnotPrefab);
      go.transform.position = cameraPath.FovKnots[i].Xf.translation;
      go.transform.rotation = cameraPath.FovKnots[i].Xf.rotation;
      go.transform.parent = widget.transform;

      CameraPathFovKnot knot = go.GetComponent<CameraPathFovKnot>();

      knot.PathT = new PathT(cameraPath.FovKnots[i].PathTValue);
      knot.DistanceAlongSegment = widget.m_Path.GetSegmentDistanceToT(knot.PathT);
      knot.FovValue = cameraPath.FovKnots[i].Fov;

      widget.m_Path.FovKnots.Add(knot);
      widget.m_Path.AllKnots.Add(knot);
    }

    // Refresh visuals on the whole path.
    for (int i = 0; i < widget.m_Path.AllKnots.Count; ++i) {
      widget.m_Path.AllKnots[i].RefreshVisuals();
      widget.m_Path.AllKnots[i].ActivateTint(false);
      widget.m_Path.AllKnots[i].SetActivePathVisuals(false);
    }

    // And turn them off.
    widget.m_Path.ValidatePathLooping();
    widget.m_Path.SetKnotsActive(false);
    App.Switchboard.TriggerCameraPathCreated();
  }

  void OnDrawGizmosSelected() {
    if (m_Path != null) {
      m_Path.DrawGizmos();
    }
  }
}

} // namespace TiltBrush