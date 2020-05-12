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
public class CreateWidgetCommand : BaseCommand {
  private GrabWidget m_Widget;
  private float m_SpawnAggression = 0.8f;
  private int m_TiltMeterCost;
  private bool m_TiltMeterCostUndone;
  private GrabWidget m_Prefab;
  // The position the model spawns from
  private TrTransform m_SpawnXf;
  // Location where the model is grabbable by the brush controller
  private TrTransform m_EndXf;
  private Quaternion? m_DesiredEndForward;
  private CanvasScript m_Canvas;

  // Creates a new widget by instantiating the prefab and setting its transform.
  // spawnXf is in world space.
  public CreateWidgetCommand(
      GrabWidget widgetPrefab,
      TrTransform spawnXf,
      Quaternion? desiredEndForward = null,
      BaseCommand parent = null)
    : base(parent) {
    Transform controller = InputManager.m_Instance.GetController(
        InputManager.ControllerName.Brush).transform;
    m_Canvas = App.ActiveCanvas;
    m_SpawnXf = spawnXf;
    m_EndXf = TrTransform.TRS(
        Vector3.Lerp(m_SpawnXf.translation, controller.position, m_SpawnAggression),
        controller.rotation,
        m_SpawnXf.scale);
    m_Prefab = widgetPrefab;
    m_DesiredEndForward = desiredEndForward;
  }

  public GrabWidget Widget { get { return m_Widget; } }

  public override bool NeedsSave { get { return true; } }

  protected override void OnUndo() {
    m_Widget.Hide();
    WidgetManager.m_Instance.RefreshPinAndUnpinLists();
    TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_TiltMeterCost, up: false);
    m_TiltMeterCostUndone = true;

    if (m_Widget is CameraPathWidget) {
      WidgetManager.m_Instance.ValidateCurrentCameraPath();
      App.Switchboard.TriggerCameraPathDeleted();
    }
  }

  protected override void OnRedo() {
    if (m_Widget != null) {
      // If we're re-doing this command and the widget exists, it's because we had previously
      // undone a creation.  The widget will be hidden at this point, so we want to restore it,
      // much in the opposite way HideWidgetCommand works.
      // TODO: This function name is used more generally and should be renamed.
      m_Widget.gameObject.SetActive(true);
      m_Widget.RestoreFromToss();
    } else {
      m_Widget = Object.Instantiate(m_Prefab);
      m_Widget.transform.position = m_SpawnXf.translation;

      // Widget type specific initialization.
      if (m_Widget is StencilWidget) {
        m_Widget.transform.parent = m_Canvas.transform;
        m_Widget.Show(true);
      } else if (m_Widget is ModelWidget) {
        // ModelWidget.Show(true) is not called here because the model must be assigned
        // before it can be turned on.
      } else if (m_Widget is ImageWidget) {
        m_Widget.transform.parent = m_Canvas.transform;
        m_Widget.Show(true);
      } else if (m_Widget is VideoWidget) {
        m_Widget.transform.parent = m_Canvas.transform;
        m_Widget.Show(true);
      } else if (m_Widget is CameraPathWidget) {
        m_Widget.transform.parent = m_Canvas.transform;
        m_Widget.transform.localPosition = Vector3.zero;
        m_Widget.transform.localRotation = Quaternion.identity;
        m_Widget.Show(true);
        App.Switchboard.TriggerCameraPathCreated();
        WidgetManager.m_Instance.CameraPathsVisible = true;
      }

      m_Widget.InitIntroAnim(m_SpawnXf, m_EndXf, false, m_DesiredEndForward);
      m_TiltMeterCost = m_Widget.GetTiltMeterCost();
    }

    WidgetManager.m_Instance.RefreshPinAndUnpinLists();
    TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_TiltMeterCost, up: true);
    m_TiltMeterCostUndone = false;
  }

  // Only destroy game objects in create command otherwise it is possible to still
  // access the widgets via other commands still on the stack.
  protected override void OnDispose() {
    if (m_Widget.gameObject) {
      WidgetManager.m_Instance.UnregisterGrabWidget(m_Widget.gameObject);
      Object.Destroy(m_Widget.gameObject);
    }
  }

  public void SetWidgetCost(int iCost) {
    // Remove old cost and add new if we're not sitting undone.
    if (!m_TiltMeterCostUndone) {
      TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_TiltMeterCost, up: false);
      TiltMeterScript.m_Instance.AdjustMeterWithWidget(iCost, up: true);
    }
    m_TiltMeterCost = iCost;
  }
}
} // namespace TiltBrush
