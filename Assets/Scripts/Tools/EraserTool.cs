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

public class EraserTool : StrokeModificationTool {
  public float m_MaxSpinSpeed;
  public float m_SpinSpeedAcceleration;
  public float m_SpinSpeedDecay;
  private float m_SpinSpeed;
  private float m_SpinSpeedVel;
  private float m_SpinAmount;

  override public void HideTool(bool bHide) {
    base.HideTool(bHide);
    if (m_SketchSurface.IsInFreePaintMode()) {
      m_ToolTransform.GetComponent<Renderer>().enabled = !bHide;
    }
  }

  override protected void UpdateAudioVisuals() {
    bool bToolHot = IsHot;
    if (bToolHot != m_ToolWasHot) {
      m_ToolTransform.GetComponent<Renderer>().material =
          bToolHot ? m_ToolHotMaterial : m_ToolColdMaterial;
      RequestPlayAudio(bToolHot);
    }

    if (bToolHot) {
      m_SpinSpeedVel += (m_SpinSpeedAcceleration * Time.deltaTime);
      m_SpinSpeed = Mathf.Min(m_SpinSpeed + m_SpinSpeedVel * Time.deltaTime, m_MaxSpinSpeed);
    } else {
      m_SpinSpeed = Mathf.Max(m_SpinSpeed - m_SpinSpeedDecay * Time.deltaTime, 0.0f);
      m_SpinSpeedVel = 0.0f;
    }
    m_SpinAmount += m_SpinSpeed * Time.deltaTime;
  }

  override protected void SnapIntersectionObjectToController() {
    if (m_LockToController) {
      Vector3 toolPos = InputManager.Brush.Geometry.ToolAttachPoint.position +
        InputManager.Brush.Geometry.ToolAttachPoint.forward * m_PointerForwardOffset;
      m_ToolTransform.position = toolPos;
      m_ToolTransform.rotation = InputManager.Brush.Geometry.ToolAttachPoint.rotation *
          Quaternion.AngleAxis(m_SpinAmount, Vector3.forward);
    } else {
      transform.position = SketchSurfacePanel.m_Instance.transform.position;
      transform.rotation = SketchSurfacePanel.m_Instance.transform.rotation;
    }
  }

  override public void IntersectionHappenedThisFrame() {
    InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.05f);
  }

  override protected bool HandleIntersectionWithWidget(GrabWidget widget) {
    return false;
  }

  override protected bool HandleIntersectionWithBatchedStroke(BatchSubset rGroup) {
    if (! rGroup.m_Active) {
      // Subset has already been deleted.
      // Collision detection is async and has latency, so theoretically we should expect this case.
      // However, in practice it's currently not possible; so flag it as unexpected.
      Debug.LogWarningFormat(
          "{0}: Unexpected: deleting already-deleted stroke @ {1}",
          rGroup.m_ParentBatch.ParentPool.Name, Time.frameCount);
      return false;
    }
    SketchMemoryScript.m_Instance.MemorizeDeleteSelection(rGroup.m_Stroke);
    PlayModifyStrokeSound();
    return true;
  }

  override protected bool HandleIntersectionWithSolitaryObject(GameObject rGameObject) {
    SketchMemoryScript.m_Instance.MemorizeDeleteSelection(rGameObject);
    PlayModifyStrokeSound();
    return true;
  }

  override public void AssignControllerMaterials(InputManager.ControllerName controller) {
    if (controller == InputManager.ControllerName.Brush) {
      InputManager.Brush.Geometry.ShowBrushSizer();
    }
  }

}
}  // namespace TiltBrush
