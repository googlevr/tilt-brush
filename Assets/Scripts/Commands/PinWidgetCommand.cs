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

namespace TiltBrush {
public class PinWidgetCommand : BaseCommand {
  private GrabWidget m_Widget;
  private bool m_Pinning;

  public GrabWidget Widget { get { return m_Widget; } }

  public bool IsPinning { get { return m_Pinning; } }

  public PinWidgetCommand(GrabWidget widget, bool pin, BaseCommand parent = null)
    : base(parent) {
    m_Widget = widget;
    m_Pinning = pin;
  }

  protected override void OnRedo() {
    m_Widget.SetPinned(bPin: m_Pinning);
    WidgetManager.m_Instance.RefreshPinAndUnpinLists();
  }

  protected override void OnUndo() {
    m_Widget.SetPinned(bPin: !m_Pinning);
    WidgetManager.m_Instance.RefreshPinAndUnpinLists();
  }

  // We treat the pin action as a user indicating a position of importance, and one that the user
  // may want to undo to.
  //
  // Possible sequences:
  // 1. Move -> Pin
  //    The Pin command is a child of the Move command, and m_Pinning = true.
  // 2. Upin -> Move (-> Pin)
  //                 (-> Toss)
  //    The Pin command is the parent of one or two children, and m_Pinning = false.
  //    The first child is always a Move command that is updated as the user moves the widget
  //    following the unpinning. The second child is can be a Pin command with m_Pinning = true
  //    and only exists if the user repins the widget or a Hide command if the user tosses it.
  public override bool Merge(BaseCommand other) {
    if (base.Merge(other)) { return true; }
    if (m_Pinning) { return false; }
    MoveWidgetCommand move = other as MoveWidgetCommand;
    if (move != null && m_Widget == move.Widget) {
      if (m_Children.Count == 0) {
        m_Children.Add(move);
        return true;
      } else if (m_Children.Count == 1 && m_Children[0] is MoveWidgetCommand) {
        return m_Children[0].Merge(move);
      }
      return false;
    }
    HideWidgetCommand hide = other as HideWidgetCommand;
    if (hide != null && m_Widget == hide.Widget) {
      if (m_Children.Count == 1 && m_Children[0] is MoveWidgetCommand) {
        return m_Children[0].Merge(hide);
      }
    }
    PinWidgetCommand pin = other as PinWidgetCommand;
    if (pin != null && m_Widget == pin.m_Widget && pin.m_Pinning &&
        (m_Children.Count == 1 && m_Children[0] is MoveWidgetCommand)) {
      m_Children.Add(pin);
      return true;
    }
    return false;
  }
}
} // namespace TiltBrush