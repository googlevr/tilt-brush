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
public class ModifyPositionKnotCommand : BaseKnotCommand<CameraPathPositionKnot> {
  private CameraPath m_Path;
  private CameraPathPositionKnot m_KnotLoopPartner;
  private Quaternion m_StartRotation_CS;
  private Quaternion m_EndRotation_CS;
  private Quaternion m_PartnerStartRotation_CS;
  private float m_StartSpeed;
  private float m_EndSpeed;
  private float m_PartnerStartSpeed;
  private int m_KnotIndex;
  private int m_PartnerKnotIndex;
  private bool m_Final;

  public ModifyPositionKnotCommand(CameraPath path, KnotDescriptor knotDesc, float endSpeed,
      Vector3 endForward_GS, bool mergesWithCreateCommand = false, bool final = false,
      BaseCommand parent = null)
      : base((CameraPathPositionKnot)knotDesc.knot, mergesWithCreateCommand, parent) {
    m_Path = path;
    m_EndSpeed = endSpeed;
    m_KnotIndex = knotDesc.positionKnotIndex.Value;
    m_Final = final;

    // Store the local space rotation because the knot's parent (the canvas) may change
    // between undo/redos.
    m_EndRotation_CS = Quaternion.Inverse(App.Scene.Pose.rotation) *
        Quaternion.LookRotation(endForward_GS);
    m_StartRotation_CS = Knot.transform.localRotation;
    m_StartSpeed = Knot.TangentMagnitude;

    // Cache our dance partner if the path loops and we're an end knot.
    if (m_Path.PathLoops) {
      if (m_KnotIndex == 0 || m_KnotIndex == m_Path.NumPositionKnots - 1) {
        m_PartnerKnotIndex = (m_KnotIndex == 0) ? (m_Path.NumPositionKnots - 1) : 0;
        m_KnotLoopPartner = m_Path.PositionKnots[m_PartnerKnotIndex];
        m_PartnerStartRotation_CS = m_KnotLoopPartner.transform.localRotation;
        m_PartnerStartSpeed = m_KnotLoopPartner.TangentMagnitude;
      }
    }
  }

  override public bool NeedsSave { get { return true; } }

  override protected void OnUndo() {
    Knot.transform.localRotation = m_StartRotation_CS;
    Knot.TangentMagnitude = m_StartSpeed;
    if (m_KnotLoopPartner != null) {
      m_KnotLoopPartner.transform.localRotation = m_PartnerStartRotation_CS;
      m_KnotLoopPartner.TangentMagnitude = m_PartnerStartSpeed;
      m_KnotLoopPartner.RefreshVisuals();
      m_Path.RefreshFromPathKnotMovement(m_PartnerKnotIndex);
    }
    Knot.RefreshVisuals();
    m_Path.RefreshFromPathKnotMovement(m_KnotIndex);
    WidgetManager.m_Instance.CameraPathsVisible = true;
  }

  override protected void OnRedo() {
    Knot.transform.localRotation = m_EndRotation_CS;
    Knot.TangentMagnitude = m_EndSpeed;
    if (m_KnotLoopPartner != null) {
      m_KnotLoopPartner.transform.localRotation = m_EndRotation_CS;
      m_KnotLoopPartner.TangentMagnitude = m_EndSpeed;
      m_KnotLoopPartner.RefreshVisuals();
      m_Path.RefreshFromPathKnotMovement(m_PartnerKnotIndex);
    }
    Knot.RefreshVisuals();
    m_Path.RefreshFromPathKnotMovement(m_KnotIndex);
    WidgetManager.m_Instance.CameraPathsVisible = true;
  }

  override public bool Merge(BaseCommand other) {
    if (base.Merge(other)) { return true; }
    if (m_Final) { return false; }

    ModifyPositionKnotCommand skc = other as ModifyPositionKnotCommand;
    if (skc == null || skc.Knot != Knot) { return false; }

    m_EndRotation_CS = skc.m_EndRotation_CS;
    m_EndSpeed = skc.m_EndSpeed;
    m_Final = skc.m_Final;
    return true;
  }
}
} // namespace TiltBrush
