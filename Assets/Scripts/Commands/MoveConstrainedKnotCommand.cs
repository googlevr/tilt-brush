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
public class MoveConstrainedKnotCommand : BaseKnotCommand {
  private CameraPath m_Path;
  private Quaternion m_StartRotation_CS;
  private Quaternion m_EndRotation_CS;
  private PathT m_StartT;
  private PathT m_EndT;
  private bool m_Final;

  public MoveConstrainedKnotCommand(CameraPath path, KnotDescriptor knotDesc, Quaternion rot_GS,
      bool mergesWithCreateCommand = false, bool final = false, BaseCommand parent = null)
      : base(knotDesc.knot, mergesWithCreateCommand, parent) {
    m_Path = path;
    m_EndRotation_CS = Quaternion.Inverse(App.Scene.Pose.rotation) * rot_GS;
    m_EndT = knotDesc.pathT.Value;
    m_Final = final;

    m_StartRotation_CS = Knot.transform.localRotation;
    m_StartT = Knot.PathT;
  }

  public override bool NeedsSave { get { return true; } }

  protected override void OnRedo() {
    Knot.PathT = m_EndT;
    m_Path.SortKnotList(Knot.KnotType);
    Knot.DistanceAlongSegment = m_Path.GetSegmentDistanceToT(m_EndT);
    Knot.transform.position = m_Path.GetPosition(m_EndT);
    Knot.transform.localRotation = m_EndRotation_CS;
    Knot.RefreshVisuals();
    if (Knot.KnotType == CameraPathKnot.Type.Rotation) {
      // Align quaternions on all rotation knots so we don't have unexpected camera flips
      // when calculating rotation as we walk the path.
      m_Path.RefreshRotationKnotPolarities();
    }
    WidgetManager.m_Instance.CameraPathsVisible = true;
  }

  protected override void OnUndo() {
    Knot.PathT = m_StartT;
    m_Path.SortKnotList(Knot.KnotType);
    Knot.DistanceAlongSegment = m_Path.GetSegmentDistanceToT(m_StartT);
    Knot.transform.position = m_Path.GetPosition(m_StartT);
    Knot.transform.localRotation = m_StartRotation_CS;
    Knot.RefreshVisuals();
    if (Knot.KnotType == CameraPathKnot.Type.Rotation) {
      m_Path.RefreshRotationKnotPolarities();
    }
    WidgetManager.m_Instance.CameraPathsVisible = true;
  }

  public override bool Merge(BaseCommand other) {
    if (base.Merge(other)) { return true; }
    if (m_Final) { return false; }

    MoveConstrainedKnotCommand move = other as MoveConstrainedKnotCommand;
    if (move != null && Knot == move.Knot) {
      m_EndRotation_CS = move.m_EndRotation_CS;
      m_EndT = move.m_EndT;
      m_Final = move.m_Final;
      return true;
    }

    return false;
  }
}
} // namespace TiltBrush
