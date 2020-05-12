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
public class CreatePathKnotCommand : BaseCommand {
  private CameraPathWidget m_Widget;
  private CameraPathKnot.Type m_KnotType;
  private TrTransform m_SpawnXf;
  private CameraPathKnot m_CreatedKnot;
  private PathT m_PathT;

  // Adds a path knot of type knotType to the path owned by widget at the
  // transform defined by spawnXf.
  public CreatePathKnotCommand(CameraPathWidget widget, CameraPathKnot.Type knotType,
      PathT pathT, TrTransform spawnXf,
      BaseCommand parent = null)
    : base(parent) {
    m_Widget = widget;
    m_KnotType = knotType;
    m_SpawnXf = spawnXf;
    m_PathT = pathT;
  }

  public override bool NeedsSave { get { return true; } }

  protected override void OnUndo() {
    m_Widget.Path.RemoveKnot(m_CreatedKnot);
    m_CreatedKnot.gameObject.SetActive(false);
    WidgetManager.m_Instance.ValidateCurrentCameraPath();
    App.Switchboard.TriggerCameraPathKnotChanged();
    WidgetManager.m_Instance.CameraPathsVisible = true;
  }

  protected override void OnRedo() {
    // The scale of path widgets is arbitrary.  However, the scale should be one at knot creation
    // time so newly added knots have appropriate mesh scales.
    m_Widget.transform.localScale = Vector3.one;
    if (m_CreatedKnot == null) {
      switch (m_KnotType) {
      case CameraPathKnot.Type.Position:
        m_CreatedKnot = m_Widget.Path.CreatePositionKnot(m_SpawnXf.translation);
        break;
      case CameraPathKnot.Type.Rotation:
        m_CreatedKnot = m_Widget.Path.CreateRotationKnot(m_PathT, m_SpawnXf.rotation);
        break;
      case CameraPathKnot.Type.Speed:
        m_CreatedKnot = m_Widget.Path.CreateSpeedKnot(m_PathT);
        break;
      case CameraPathKnot.Type.Fov:
        m_CreatedKnot = m_Widget.Path.CreateFovKnot(m_PathT);
        break;
      default:
        Debug.Log("CreatePathKnotCommand knot type unsupported.");
        break;
      }
    }

    switch (m_KnotType) {
    case CameraPathKnot.Type.Position:
      int knotIndex = m_PathT.Floor();
      // If we're inserting a point and it's at the head, take on the characteristics of
      // the head knot.  This will cause InsertPositionKnot to register the path as looping,
      // which is what we want.
      if (m_Widget.Path.IsPositionNearHead(m_CreatedKnot.transform.position) &&
          knotIndex == m_Widget.Path.NumPositionKnots) {
        CameraPathPositionKnot cppkCreated = (CameraPathPositionKnot)m_CreatedKnot;
        CameraPathPositionKnot cppkHead = m_Widget.Path.PositionKnots[0];
        cppkCreated.transform.rotation = cppkHead.transform.rotation;
        cppkCreated.TangentMagnitude = cppkHead.TangentMagnitude;
      }
      m_Widget.Path.InsertPositionKnot((CameraPathPositionKnot)m_CreatedKnot, knotIndex);
      break;
    case CameraPathKnot.Type.Rotation:
      m_Widget.Path.AddRotationKnot((CameraPathRotationKnot)m_CreatedKnot, m_PathT);
      break;
    case CameraPathKnot.Type.Speed:
      m_Widget.Path.AddSpeedKnot((CameraPathSpeedKnot)m_CreatedKnot, m_PathT);
      break;
    case CameraPathKnot.Type.Fov:
      m_Widget.Path.AddFovKnot((CameraPathFovKnot)m_CreatedKnot, m_PathT);
      break;
    default:
      Debug.Log("CreatePathKnotCommand knot type unsupported.");
      break;
    }

    m_CreatedKnot.gameObject.SetActive(true);
    App.Switchboard.TriggerCameraPathKnotChanged();
    WidgetManager.m_Instance.CameraPathsVisible = true;
  }

  protected override void OnDispose() {
    if (m_CreatedKnot != null) {
      Object.Destroy(m_CreatedKnot.gameObject);
    }
  }

  override public bool Merge(BaseCommand other) {
    if (base.Merge(other)) { return true; }

    if (other is BaseKnotCommand bkc && bkc.MergesWithCreateCommand && bkc.Knot == m_CreatedKnot) {
      if (m_Children.Count == 0) {
        m_Children.Add(other);
      } else {
        return m_Children[m_Children.Count - 1].Merge(other);
      }
      return true;
    }

    return false;
  }
}
} // namespace TiltBrush
