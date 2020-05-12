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

public class CameraPathTool : BaseTool {
  public enum Mode {
    AddPositionKnot,
    AddRotationKnot,
    AddSpeedKnot,
    AddFovKnot,
    RemoveKnot,
    Recording
  }

  public enum ExtendPathType {
    None,
    ExtendAtHead,
    ExtendAtTail,
    Loop,
  }
  [SerializeField] private GameObject m_PositionKnot;
  [SerializeField] private GameObject m_RotationKnot;
  [SerializeField] private GameObject m_SpeedKnot;
  [SerializeField] private GameObject m_FovKnot;
  [SerializeField] private GameObject m_RemoveKnot;

  private Mode m_Mode;
  private CameraPathWidget m_PrevLastValidPath;
  private CameraPathWidget m_LastValidPath;
  private Vector3 m_LastValidPosition;

  private CameraPathWidget m_ExtendPath;
  private ExtendPathType m_ExtendPathType;

  private KnotDescriptor m_LastPlacedKnot;
  private CameraPathWidget m_LastPlacedKnotPath;
  private TrTransform m_LastPlacedKnotXf_LS;

  private KnotSegment m_PreviewSegment;

  public Mode CurrentMode { get { return m_Mode; } }

  override protected void Awake() {
    base.Awake();
    App.Switchboard.CameraPathModeChanged += OnCameraPathModeChanged;
    m_PreviewSegment = CameraPath.CreateSegment(null);
    m_PreviewSegment.renderer.material.color = Color.white;
    m_Mode = Mode.AddPositionKnot;
    RefreshMeshVisibility();
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    App.Switchboard.CameraPathModeChanged -= OnCameraPathModeChanged;
  }

  override public void HideTool(bool hide) {
    base.HideTool(hide);
    RefreshMeshVisibility();
    m_PreviewSegment.renderer.enabled = false;
    m_ExtendPath = null;
    m_ExtendPathType = ExtendPathType.None;
  }

  override public void EnableTool(bool bEnable) {
    base.EnableTool(bEnable);
    m_PreviewSegment.renderer.enabled = false;
    m_ExtendPath = null;
    m_ExtendPathType = ExtendPathType.None;
  }

  override public void AssignControllerMaterials(InputManager.ControllerName controller) {
    if (CurrentMode == Mode.Recording) {
      if (controller == InputManager.ControllerName.Brush) {
        InputManager.Brush.Geometry.ToggleCancelOnly(enabled: true, enableFillTimer: false);
      } else if (controller == InputManager.ControllerName.Wand) {
        InputManager.Wand.Geometry.ResetAll();
      }
    }
  }

  override public void UpdateTool() {
    base.UpdateTool();

    // If we're in the recording state, just look for cancel and get out.
    if (CurrentMode == Mode.Recording) {
      if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.MenuContextClick)) {
        SketchControlsScript.m_Instance.CameraPathCaptureRig.StopRecordingPath(false);
      }
      return;
    }

    var widgets = WidgetManager.m_Instance.CameraPathWidgets;
    Transform toolAttachXf = InputManager.Brush.Geometry.ToolAttachPoint;

    bool input = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate);
    bool inputDown = InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate);

    // Tint any path we're intersecting with.
    if (!input && m_LastValidPath != null && m_Mode != Mode.RemoveKnot) {
      m_LastValidPath.TintSegments(m_LastValidPosition);
    }

    // Initiating input.
    if (inputDown) {
      // We clicked, but the path we clicked on isn't the active path.  In that case,
      // switch it to the active path and eat up this input.
      // Don't do this for removing knots.  That input should be explicit.
      if (m_Mode != Mode.RemoveKnot && m_LastValidPath != null) {
        GrabWidgetData data = WidgetManager.m_Instance.GetCurrentCameraPath();
        bool lastValidIsCurrent = (data == null) ? false : data.m_WidgetScript == m_LastValidPath;
        if (!lastValidIsCurrent) {
          WidgetManager.m_Instance.SetCurrentCameraPath(m_LastValidPath);
          return;
        }
      }

      switch (m_Mode) {
      case Mode.AddPositionKnot:
        // Create a new path if none exists or if we're trying to add a position point
        // in a place where we're not extending an existing path.
        if (!WidgetManager.m_Instance.AnyCameraPathWidgetsActive ||
            (m_LastValidPath == null && m_ExtendPath == null)) {
          m_ExtendPath = WidgetManager.m_Instance.CreatePathWidget();
          m_ExtendPathType = ExtendPathType.ExtendAtHead;
          WidgetManager.m_Instance.SetCurrentCameraPath(m_ExtendPath);
        }

        if (m_LastValidPath != null) {
          m_LastValidPath.AddPathConstrainedKnot(
              CameraPathKnot.Type.Position, m_LastValidPosition, toolAttachXf.rotation);
          m_LastPlacedKnot = m_LastValidPath.Path.LastPlacedKnotInfo;
          m_LastPlacedKnotPath = m_LastValidPath;
        } else if (m_ExtendPath != null) {
          // Manipulation of a path we wish to extend.
          m_ExtendPath.ExtendPath(toolAttachXf.position, m_ExtendPathType);

          // Remember the index of the path we just added to, so we can manipulate it
          // while input is held.
          // Don't record this if we just made our path loop.
          if (!m_ExtendPath.Path.PathLoops) {
            m_LastPlacedKnot = m_ExtendPath.Path.LastPlacedKnotInfo;
            m_LastPlacedKnotPath = m_ExtendPath;
          }
        }
        break;
      case Mode.AddRotationKnot:
        if (m_LastValidPath != null) {
          m_LastValidPath.AddPathConstrainedKnot(
              CameraPathKnot.Type.Rotation, m_LastValidPosition, toolAttachXf.rotation);
          m_LastPlacedKnot = m_LastValidPath.Path.LastPlacedKnotInfo;
          m_LastPlacedKnotPath = m_LastValidPath;
        }
        break;
      case Mode.AddSpeedKnot:
        if (m_LastValidPath != null) {
          m_LastValidPath.AddPathConstrainedKnot(
              CameraPathKnot.Type.Speed, m_LastValidPosition, toolAttachXf.rotation);
          m_LastPlacedKnot = m_LastValidPath.Path.LastPlacedKnotInfo;
          m_LastPlacedKnotPath = m_LastValidPath;
        }
        break;
      case Mode.AddFovKnot:
        if (m_LastValidPath != null) {
          m_LastValidPath.AddPathConstrainedKnot(
              CameraPathKnot.Type.Fov, m_LastValidPosition, toolAttachXf.rotation);
          m_LastPlacedKnot = m_LastValidPath.Path.LastPlacedKnotInfo;
          m_LastPlacedKnotPath = m_LastValidPath;
        }
        break;
      case Mode.RemoveKnot:
        CheckToRemoveKnot(toolAttachXf.position);
        break;
      }

      // Remember what our controller looked like so we can manipulate this knot.
      if (m_LastPlacedKnot != null) {
        Transform controller = InputManager.Brush.Transform;
        Transform knotXf = m_LastPlacedKnot.knot.transform;
        TrTransform newWidgetXf = Coords.AsGlobal[knotXf];
        m_LastPlacedKnotXf_LS = Coords.AsGlobal[controller].inverse * newWidgetXf;
        HideAllMeshes();
      }
    } else if (input) {
      if (m_Mode == Mode.RemoveKnot) {
        CheckToRemoveKnot(toolAttachXf.position);
      } else if (m_LastPlacedKnot != null) {
        // Holding input from last frame can allow us to manipulate a just placed position knot.
        WidgetManager.m_Instance.PathTinter.TintKnot(m_LastPlacedKnot.knot);

        TrTransform controllerXf = Coords.AsGlobal[InputManager.Brush.Transform];
        TrTransform inputXf = controllerXf * m_LastPlacedKnotXf_LS;

        switch (m_LastPlacedKnot.knot.KnotType) {
        case CameraPathKnot.Type.Position:
          if (m_LastPlacedKnot.control != 0) {
            CameraPathPositionKnot pk = m_LastPlacedKnot.knot as CameraPathPositionKnot;
            float tangentMag = pk.GetTangentMagnitudeFromControlXf(inputXf);
            Vector3 knotFwd =
              (inputXf.translation - m_LastPlacedKnot.knot.transform.position).normalized;
            if ((CameraPathPositionKnot.ControlType)m_LastPlacedKnot.control ==
                CameraPathPositionKnot.ControlType.TangentControlBack) {
              knotFwd *= -1.0f;
            }

            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ModifyPositionKnotCommand(
                  m_LastPlacedKnotPath.Path, m_LastPlacedKnot, tangentMag, knotFwd,
                  mergesWithCreateCommand:true));
          }
          break;
        case CameraPathKnot.Type.Rotation:
          // Rotation knots hide when we grab them, and in their place, we set the preview widget.
          m_LastPlacedKnot.knot.gameObject.SetActive(false);
          SketchControlsScript.m_Instance.CameraPathCaptureRig.OverridePreviewWidgetPathT(
              m_LastPlacedKnot.knot.PathT);
          SketchMemoryScript.m_Instance.PerformAndRecordCommand(
              new MoveConstrainedKnotCommand(m_LastPlacedKnotPath.Path, m_LastPlacedKnot,
                inputXf.rotation, mergesWithCreateCommand: true));
          break;
        case CameraPathKnot.Type.Speed:
          CameraPathSpeedKnot sk = m_LastPlacedKnot.knot as CameraPathSpeedKnot;
          float speed = sk.GetSpeedValueFromY(
              InputManager.Brush.Behavior.PointerAttachPoint.transform.position.y);
          SketchMemoryScript.m_Instance.PerformAndRecordCommand(
              new ModifySpeedKnotCommand(sk, speed, mergesWithCreateCommand: true));
          break;
        case CameraPathKnot.Type.Fov:
          CameraPathFovKnot fk = m_LastPlacedKnot.knot as CameraPathFovKnot;
          float fov = fk.GetFovValueFromY(
              InputManager.Brush.Behavior.PointerAttachPoint.transform.position.y);
          SketchMemoryScript.m_Instance.PerformAndRecordCommand(
              new ModifyFovKnotCommand(fk, fov, mergesWithCreateCommand: true));
          break;
        }
      }
    } else {
      // No input to work with.  Forget we had anything and make sure our meshes are showing.
      if (m_LastPlacedKnot != null) {
        RefreshMeshVisibility();

        // Rotation knots hide when we grab them, make sure it's enabled.
        if (m_LastPlacedKnot.knot.KnotType == CameraPathKnot.Type.Rotation) {
          m_LastPlacedKnot.knot.gameObject.SetActive(true);
          SketchControlsScript.m_Instance.CameraPathCaptureRig.OverridePreviewWidgetPathT(null);
        }
      }
      m_LastPlacedKnot = null;
      m_LastPlacedKnotPath = null;
    }
  }

  override public void LateUpdateTool() {
    Transform xf = InputManager.Brush.Geometry.ToolAttachPoint;
    m_RemoveKnot.transform.position = xf.position;
    m_RemoveKnot.transform.rotation = xf.rotation;

    if (m_LastPlacedKnot == null) {
      // Detect point nearest to path and jump to path if close enough.
      float bestDistance = float.MaxValue;
      m_PrevLastValidPath = m_LastValidPath;
      m_LastValidPosition = xf.position;
      m_LastValidPath = null;

      GrabWidgetData currentData = WidgetManager.m_Instance.GetCurrentCameraPath();
      var datas = WidgetManager.m_Instance.CameraPathWidgets;
      foreach (TypedWidgetData<CameraPathWidget> data in datas) {
        CameraPathWidget widget = data.WidgetScript;
        Debug.AssertFormat(widget != null, "Non-CameraPathWidget in CameraPathWidget list");

        // Check our tool attach point against the path.  If there is a collision, we're going
        // to jump the position of our mesh to the point on the path.
        Vector3? projected = widget.Path.ProjectPositionOntoPath(xf.position);
        if (projected != null) {
          float distToProjected = Vector3.Distance(projected.Value, xf.position);
          if (distToProjected < bestDistance) {
            bestDistance = distToProjected;
            m_LastValidPosition = projected.Value;
            m_LastValidPath = widget;

            // We reset this here and not above (with m_LastValidPath) because we want the value
            // to retain as the user moves around beyond the end.  It's only when hiding and
            // interacting with a path do we want it to reset.
            m_ExtendPath = null;
          }
        }

        // In addition to checking collision with the path, check collision with the end points
        // of the paths.  If the user comes near an end point, but is *not* colliding with the
        // path, they should be able to extend the length of the path.  That is, add a new knot
        // off the respective end.
        bool currentWidget = (currentData == null) ? false : currentData.m_WidgetScript == widget;
        if (currentWidget && m_Mode == Mode.AddPositionKnot) {
          if (widget.Path.PathLoops) {
            // We never want to show an extended path when the path loops.
            m_ExtendPath = null;
            m_ExtendPathType = ExtendPathType.None;
          } else {
            // logic for extending off one end of the path.
            CameraPath.EndType end = widget.Path.IsPositionNearEnd(xf.position);
            // If we're not near an end point but we're in loop mode, break out of loop mode.
            if (end == CameraPath.EndType.None && m_ExtendPathType == ExtendPathType.Loop) {
              m_ExtendPath = null;
              m_ExtendPathType = ExtendPathType.None;
            } else if (end != CameraPath.EndType.None && m_ExtendPathType != ExtendPathType.Loop) {
              m_ExtendPath = widget;
              // If we're currently extending our path and we're now close to the other end,
              // set our extend type to looping.
              if (widget.Path.NumPositionKnots > 1 &&
                  (m_ExtendPathType == ExtendPathType.ExtendAtHead &&
                   end == CameraPath.EndType.Tail) ||
                  (m_ExtendPathType == ExtendPathType.ExtendAtTail &&
                   end == CameraPath.EndType.Head)) {
                m_ExtendPathType = ExtendPathType.Loop;
              } else {
                m_ExtendPathType = (end == CameraPath.EndType.Head) ? ExtendPathType.ExtendAtHead :
                    ExtendPathType.ExtendAtTail;
              }
            }
          }
        }
      }

      m_PositionKnot.transform.position = m_LastValidPosition;
      m_PositionKnot.transform.rotation = xf.rotation;
      m_RotationKnot.transform.position = m_LastValidPosition;
      m_RotationKnot.transform.rotation = xf.rotation;
      m_SpeedKnot.transform.position = m_LastValidPosition;
      m_SpeedKnot.transform.rotation = Quaternion.identity;
      m_FovKnot.transform.position = m_LastValidPosition;
      m_FovKnot.transform.rotation = Quaternion.identity;

      // If we're not colliding with a path, but we are colliding with an end, show the preview
      // segment.
      if (m_LastValidPath == null && m_ExtendPath != null) {
        m_PreviewSegment.renderer.enabled = true;
        m_ExtendPath.Path.RefreshSegmentVisuals(xf.position, m_PreviewSegment, m_ExtendPathType);
      } else {
        m_PreviewSegment.renderer.enabled = false;
      }
    } else {
      m_PreviewSegment.renderer.enabled = false;
      m_PrevLastValidPath = m_LastValidPath;
      m_LastValidPath = null;
    }
  }

  void CheckToRemoveKnot(Vector3 pos) {
    var currentPath = WidgetManager.m_Instance.GetCurrentCameraPath();
    if (currentPath != null) {
      bool knotRemoved = currentPath.WidgetScript.RemoveKnotAtPosition(pos);
      if (knotRemoved) {
        // If we removed the last knot, switch out of this tool mode.
        if (!WidgetManager.m_Instance.AnyActivePathHasAKnot()) {
          App.Switchboard.TriggerCameraPathModeChanged(Mode.RemoveKnot);
        }
      }
    }
  }

  void OnCameraPathModeChanged(Mode newMode) {
    m_Mode = newMode;
    RefreshMeshVisibility();
  }

  void HideAllMeshes() {
    m_PositionKnot.SetActive(false);
    m_RotationKnot.SetActive(false);
    m_SpeedKnot.SetActive(false);
    m_FovKnot.SetActive(false);
    m_RemoveKnot.SetActive(false);
  }

  void RefreshMeshVisibility() {
    HideAllMeshes();
    if (m_ToolHidden) {
      return;
    }

    switch (m_Mode) {
    case Mode.AddPositionKnot:
      m_PositionKnot.SetActive(true);
      break;
    case Mode.AddRotationKnot:
      m_RotationKnot.SetActive(true);
      break;
    case Mode.AddSpeedKnot:
      m_SpeedKnot.SetActive(true);
      break;
    case Mode.AddFovKnot:
      m_FovKnot.SetActive(true);
      break;
    case Mode.RemoveKnot:
      m_RemoveKnot.SetActive(true);
      break;
    }
  }

  override public bool AllowsWidgetManipulation() {
    return CurrentMode != Mode.Recording;
  }

  override public bool CanAdjustSize() {
    return CurrentMode != Mode.Recording;
  }

  override public bool InputBlocked() {
    return CurrentMode == Mode.Recording;
  }

  override public bool AllowWorldTransformation() {
    return CurrentMode != Mode.Recording;
  }

  override public bool HidePanels() {
    return CurrentMode == Mode.Recording;
  }

  override public bool BlockPinCushion() {
    return CurrentMode == Mode.Recording;
  }

  override public bool AllowDefaultToolToggle() {
    return CurrentMode != Mode.Recording;
  }
}
}  // namespace TiltBrush

