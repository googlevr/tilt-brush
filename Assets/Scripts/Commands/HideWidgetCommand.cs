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
public class HideWidgetCommand : BaseCommand {
  private GrabWidget m_Widget;
  private TrTransform m_WidgetTransform;
  private int m_TiltMeterCost;

  public GrabWidget Widget { get { return m_Widget; } }

  public HideWidgetCommand(GrabWidget widget, BaseCommand parent = null)
    : base(parent) {
    m_Widget = widget;
    if (widget is StencilWidget) {
      m_WidgetTransform = TrTransform.FromLocalTransform(widget.transform);
    } else if (widget is MediaWidget) {
      m_WidgetTransform = TrTransform.FromLocalTransform(widget.transform);
      m_WidgetTransform.scale = widget.GetSignedWidgetSize();
    }
    m_TiltMeterCost = m_Widget.GetTiltMeterCost();
    TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_TiltMeterCost, up: false);
  }

  public override bool NeedsSave { get { return true; } }

  protected override void OnRedo() {
    m_Widget.Hide();
    WidgetManager.m_Instance.RefreshPinAndUnpinLists();
    TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_TiltMeterCost, up: false);

    if (m_Widget is CameraPathWidget) {
      WidgetManager.m_Instance.ValidateCurrentCameraPath();
      App.Switchboard.TriggerCameraPathDeleted();
    }
  }

  protected override void OnUndo() {
    if (m_Widget != null) {
      m_Widget.gameObject.SetActive(true);
      m_Widget.RestoreFromToss();
      m_Widget.transform.localPosition = m_WidgetTransform.translation;
      m_Widget.transform.localRotation = m_WidgetTransform.rotation;
      m_Widget.SetSignedWidgetSize(m_WidgetTransform.scale);
      WidgetManager.m_Instance.RefreshPinAndUnpinLists();
      TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_TiltMeterCost, up: true);
      if (m_Widget is CameraPathWidget) {
        App.Switchboard.TriggerCameraPathCreated();
      }
    } else {
      Debug.LogError("Widget in undo stack was destroyed.");
    }
  }
}
} // namespace TiltBrush
