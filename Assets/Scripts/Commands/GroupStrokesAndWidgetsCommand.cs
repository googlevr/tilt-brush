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

using System.Collections.Generic;
using System.Linq;

namespace TiltBrush {

/// A command to group or ungroup a set of strokes.
public class GroupStrokesAndWidgetsCommand : BaseCommand {
  private Stroke[] m_Strokes;
  private GrabWidget[] m_Widgets;
  private SketchGroupTag[] m_InitialStrokesGroups;
  private SketchGroupTag[] m_InitialWidgetsGroups;
  private SketchGroupTag m_TargetGroup;

  override public bool NeedsSave {
    // We save groups.
    get => true;
  }

  /// <summary>
  /// Move strokes and widgets other things to a group.
  /// </summary>
  /// <param name="strokes">Strokes to group</param>
  /// <param name="widgets">Widgets to group</param>
  /// <param name="targetGroup">Group to move to, or null to move to a newly-created group</param>
  /// <param name="parent">parent command</param>
  public GroupStrokesAndWidgetsCommand(
      ICollection<Stroke> strokes,
      ICollection<GrabWidget> widgets,
      SketchGroupTag? targetGroup,
      BaseCommand parent = null)
      : base(parent) {
    m_Strokes = strokes.ToArray();
    m_Widgets = widgets.ToArray();
    m_InitialStrokesGroups = m_Strokes.Select(s => s.Group).ToArray();
    m_InitialWidgetsGroups = m_Widgets.Select(s => s.Group).ToArray();
    m_TargetGroup = targetGroup ?? App.GroupManager.NewUnusedGroup();
  }

  protected override void OnRedo() {
    for (int i = 0; i < m_Strokes.Length; i++) {
      m_Strokes[i].Group = m_TargetGroup;
    }
    for (int i = 0; i < m_Widgets.Length; i++) {
      m_Widgets[i].Group = m_TargetGroup;
    }
  }

  protected override void OnUndo() {
    for (int i = 0; i < m_Strokes.Length; i++) {
      m_Strokes[i].Group = m_InitialStrokesGroups[i];
    }
    for (int i = 0; i < m_Widgets.Length; i++) {
      m_Widgets[i].Group = m_InitialWidgetsGroups[i];
    }
  }
}
} // namespace TiltBrush
