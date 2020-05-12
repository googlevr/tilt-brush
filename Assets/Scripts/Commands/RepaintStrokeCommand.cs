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
using UnityEngine;

namespace TiltBrush {
public class RepaintStrokeCommand : BaseCommand {
  private Stroke m_TargetStroke;
  private Color m_StartColor;
  private Guid m_StartGuid;
  private Color m_EndColor;
  private Guid m_EndGuid;

  public RepaintStrokeCommand(
      Stroke stroke, Color newcolor, Guid newGuid, BaseCommand parent = null) : base(parent) {
    m_TargetStroke = stroke;
    m_StartColor = stroke.m_Color;
    m_StartGuid = stroke.m_BrushGuid;
    m_EndColor = newcolor;
    m_EndGuid = newGuid;
  }

  public override bool NeedsSave { get { return true; } }

  private void ApplyColorAndBrushToObject(Color color, Guid brushGuid) {
    m_TargetStroke.m_Color = ColorPickerUtils.ClampLuminance(
        color, BrushCatalog.m_Instance.GetBrush(brushGuid).m_ColorLuminanceMin);
    m_TargetStroke.m_BrushGuid = brushGuid;
    m_TargetStroke.InvalidateCopy();
    m_TargetStroke.Uncreate();
    m_TargetStroke.Recreate();
  }

  protected override void OnRedo() {
    ApplyColorAndBrushToObject(m_EndColor, m_EndGuid);
  }

  protected override void OnUndo() {
    ApplyColorAndBrushToObject(m_StartColor, m_StartGuid);
  }
}
} // namespace TiltBrush

