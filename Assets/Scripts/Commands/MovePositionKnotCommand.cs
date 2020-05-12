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

namespace TiltBrush {
public class MovePositionKnotCommand : BaseKnotCommand<CameraPathPositionKnot> {
  private CameraPath m_Path;
  private TrTransform m_StartXf_CS;
  private TrTransform m_EndXf_CS;
  private int m_KnotIndex;
  private float m_StartTangentMagnitude;
  private bool m_Final;

  public MovePositionKnotCommand(CameraPath path, KnotDescriptor knotDesc,
      TrTransform endXf_GS, bool final = false, BaseCommand parent = null)
      : base((CameraPathPositionKnot)knotDesc.knot, false, parent) {
    m_Path = path;
    m_EndXf_CS = App.Scene.Pose.inverse * endXf_GS;
    m_KnotIndex = knotDesc.positionKnotIndex.Value;
    m_StartTangentMagnitude = Knot.TangentMagnitude;
    m_Final = final;

    if (Knot == null) {
      throw new ArgumentException("MovePositionKnotCommand requires CameraPathPositionKnot");
    }
    m_StartXf_CS = TrTransform.FromLocalTransform(Knot.transform);
  }

  public override bool NeedsSave { get { return true; } }

  protected override void OnRedo() {
    Knot.transform.localPosition = m_EndXf_CS.translation;
    Knot.transform.localRotation = m_EndXf_CS.rotation;
    // If we're the tail and we've attached to the head, or vice versa, we need to match
    // our tangent magnitude to our buddy.
    m_Path.PathLoops = m_Path.ShouldPathLoop();
    if (m_Path.PathLoops) {
      if (m_KnotIndex == 0 || m_KnotIndex == m_Path.NumPositionKnots - 1) {
        int otherIndex = (m_KnotIndex == 0) ? (m_Path.NumPositionKnots - 1) : 0;
        Knot.TangentMagnitude =
            m_Path.PositionKnots[otherIndex].TangentMagnitude;
      }
    }
    Knot.RefreshVisuals();
    m_Path.RefreshFromPathKnotMovement(m_KnotIndex);
    WidgetManager.m_Instance.CameraPathsVisible = true;
  }

  protected override void OnUndo() {
    Knot.transform.localPosition = m_StartXf_CS.translation;
    Knot.transform.localRotation = m_StartXf_CS.rotation;
    m_Path.ValidatePathLooping();
    Knot.TangentMagnitude = m_StartTangentMagnitude;
    Knot.RefreshVisuals();
    m_Path.RefreshFromPathKnotMovement(m_KnotIndex);
    WidgetManager.m_Instance.CameraPathsVisible = true;
  }

  public override bool Merge(BaseCommand other) {
    if (base.Merge(other)) { return true; }
    if (m_Final) { return false; }

    MovePositionKnotCommand move = other as MovePositionKnotCommand;
    if (move != null && Knot == move.Knot) {
      m_EndXf_CS = move.m_EndXf_CS;
      m_KnotIndex = move.m_KnotIndex;
      m_Final = move.m_Final;
      return true;
    }

    return false;
  }
}
} // namespace TiltBrush
