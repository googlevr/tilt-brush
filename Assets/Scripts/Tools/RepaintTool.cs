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

public class RepaintTool : StrokeModificationTool {
  [SerializeField] private bool m_Recolor;
  [SerializeField] private bool m_Rebrush;
  public float m_SpinSpeedAcceleration;
  public float m_MaxSpinSpeed;
  public float m_SpinSpeedDecay;
  private float m_SpinSpeedVel;
  private float m_SpinSpeed;
  private float m_SpinAmount;

  override protected void Awake() {
    base.Awake();

    PointerManager.m_Instance.OnPointerColorChange += UpdateMaterialColors;
    // Modify an instance of the materials, not the original materials
    m_ToolColdMaterial = Instantiate(m_ToolColdMaterial);
    m_ToolHotMaterial = Instantiate(m_ToolHotMaterial);
    m_ToolTransform.GetComponent<Renderer>().material = m_ToolColdMaterial;
    UpdateMaterialColors();
  }

  void UpdateMaterialColors() {
    m_ToolColdMaterial.color = PointerManager.m_Instance.PointerColor;
    m_ToolHotMaterial.SetColor("_BorderColor", PointerManager.m_Instance.PointerColor);
    m_ToolHotMaterial.SetColor("_BorderColor2", PointerManager.m_Instance.PointerColor);
  }

  override public void HideTool(bool bHide) {
    base.HideTool(bHide);
    if (m_SketchSurface.IsInFreePaintMode()) {
      m_ToolTransform.GetComponent<Renderer>().enabled = !bHide;
    }
  }

  override public void IntersectionHappenedThisFrame() {
    InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.05f);
  }

  override protected bool HandleIntersectionWithWidget(GrabWidget widget) {
    return false;
  }

  override protected void UpdateDetection() {
    // If we just went cold, reset our detection lists.
    // This is done before base.UpdateDetection() because our m_ToolWasHot flag is
    // updated in that call.
    if (m_ToolWasHot && !IsHot) {
      ClearGpuFutureLists();
    }

    base.UpdateDetection();
  }

  override protected void UpdateAudioVisuals() {
    bool bToolHot = IsHot;
    if (bToolHot != m_ToolWasHot) {
      m_ToolTransform.GetComponent<Renderer>().material =
          bToolHot ? m_ToolHotMaterial : m_ToolColdMaterial;
    }

    RequestPlayAudio(bToolHot);

    if (IsHot) {
      m_SpinSpeedVel -= m_SpinSpeedAcceleration * Time.deltaTime;
      m_SpinSpeed = m_SpinSpeed + m_SpinSpeedVel * Time.deltaTime;
      if (m_SpinSpeed < -m_MaxSpinSpeed || m_SpinSpeed > m_MaxSpinSpeed) {
        m_SpinSpeed = Mathf.Clamp(m_SpinSpeed, -m_MaxSpinSpeed, m_MaxSpinSpeed);
        m_SpinSpeedVel = 0.0f;
      }
    } else {
      float speedDelta = m_SpinSpeedDecay * Time.deltaTime;
      m_SpinSpeed = Mathf.Sign(m_SpinSpeed) * Mathf.Max(Mathf.Abs(m_SpinSpeed) - speedDelta, 0.0f);
      m_SpinSpeedVel = 0.0f;
    }
    m_SpinAmount += m_SpinSpeed * Time.deltaTime;
  }

  override protected void SnapIntersectionObjectToController() {
    if (m_LockToController) {
      Vector3 toolPos = InputManager.Brush.Geometry.ToolAttachPoint.position +
          InputManager.Brush.Geometry.ToolAttachPoint.forward * m_PointerForwardOffset;
      m_ToolTransform.position = toolPos;

      Quaternion qTool = InputManager.Brush.Geometry.ToolAttachPoint.rotation *
          Quaternion.AngleAxis(m_SpinAmount, Vector3.forward);
      m_ToolTransform.rotation = qTool;
    } else {
      transform.position = SketchSurfacePanel.m_Instance.transform.position;
      transform.rotation = SketchSurfacePanel.m_Instance.transform.rotation;
    }
  }

  override protected bool HandleIntersectionWithBatchedStroke(BatchSubset rGroup) {
    var didRepaint = SketchMemoryScript.m_Instance.MemorizeStrokeRepaint(
        rGroup.m_Stroke, m_Recolor, m_Rebrush);
    if (didRepaint) { PlayModifyStrokeSound(); }
    return didRepaint;
  }

  override protected bool HandleIntersectionWithSolitaryObject(GameObject rGameObject) {
    var didRepaint = SketchMemoryScript.m_Instance.MemorizeStrokeRepaint(
        rGameObject, m_Recolor, m_Rebrush);
    PlayModifyStrokeSound();
    if (didRepaint) { PlayModifyStrokeSound(); }
    return didRepaint;
  }

  override public void AssignControllerMaterials(InputManager.ControllerName controller) {
    if (controller == InputManager.ControllerName.Brush) {
      InputManager.Brush.Geometry.ShowBrushSizer();
    }
  }

}
}  // namespace TiltBrush
