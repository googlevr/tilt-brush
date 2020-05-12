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
public class MoveWidgetCommand : BaseCommand {
  enum Type {
    Guide,
    Media,
    Symmetry,
    Selection,
  }

  private Type m_Type;
  private GrabWidget m_Widget;
  private TrTransform m_StartTransform;
  private TrTransform m_EndTransform;
  private TrTransform m_StartSelectionTransform;
  private TrTransform m_EndSelectionTransform;
  private CustomDimension m_CustomDimension;
  private bool m_Final;

  private struct CustomDimension {
    public Vector3 startState;
    public Vector3 endState;
  }

  public GrabWidget Widget { get { return m_Widget; } }

  /// There is no reasonable way for a caller to know what to pass for "endCustomDimension",
  /// because moving or scaling the widget (with some change in endXf) may cause the widget's
  /// CustomDimension values to change, too.
  ///
  /// Typically, a caller will pass widget.CustomDimension and assume that this
  /// is the correct value. Or, callers will go through some custom API on the widget
  /// (see CubeStencil.RecordAndApplyScaleToAxis) which has knowledge of its own internals
  /// and can compute the proper endCustomDimension to pass.
  public MoveWidgetCommand(GrabWidget widget, TrTransform endXf, Vector3 endCustomDimension,
                           bool final = false, BaseCommand parent = null) : base(parent) {
    m_Widget = widget;
    m_Final = final;
    m_StartTransform = widget.LocalTransform;
    if (widget is StencilWidget) {
      m_Type = Type.Guide;
    } else if (widget is MediaWidget) {
      m_Type = Type.Media;
      m_StartTransform.scale = widget.GetSignedWidgetSize();
    } else if (widget is SymmetryWidget) {
      m_Type = Type.Symmetry;
    } else if (widget is SelectionWidget) {
      m_Type = Type.Selection;
      m_StartSelectionTransform = SelectionManager.m_Instance.SelectionTransform;
      m_EndSelectionTransform = SelectionManager.m_Instance.SelectionTransform;
    }
    m_CustomDimension.startState = widget.CustomDimension;
    m_EndTransform = endXf;
    m_CustomDimension.endState = endCustomDimension;
  }

  public override bool NeedsSave { get { return true; } }

  protected override void OnRedo() {
    m_Widget.LocalTransform = m_EndTransform;
    if (m_Type == Type.Symmetry) {
      PointerManager.m_Instance.DisablePointerPreviewLine();
      if (!SketchControlsScript.m_Instance.IsUserInteractingWithUI()) {
        PointerManager.m_Instance.AllowPointerPreviewLine(true);
      }
    } else if (m_Type == Type.Selection) {
      SelectionManager.m_Instance.SelectionTransform = m_EndSelectionTransform;
    }

    // Update custom dimension after size because non-uniform scaling depends on it.
    m_Widget.CustomDimension = m_CustomDimension.endState;
  }

  protected override void OnUndo() {
    m_Widget.LocalTransform = m_StartTransform;
    if (m_Type == Type.Symmetry) {
      PointerManager.m_Instance.DisablePointerPreviewLine();
      if (!SketchControlsScript.m_Instance.IsUserInteractingWithUI()) {
        PointerManager.m_Instance.AllowPointerPreviewLine(true);
      }
    } else if (m_Type == Type.Selection) {
      // This needs to be set explicitly here because assigning to m_Widget.LocalTransform
      // will propagate down to the SelectionTransform and leave it in a bad state.
      SelectionManager.m_Instance.SelectionTransform = m_StartSelectionTransform;
    }

    // Update custom dimension after size because non-uniform scaling depends on it.
    m_Widget.CustomDimension = m_CustomDimension.startState;

    if (!m_Final) {
      if (!m_Widget.IsSpinningFreely) {
        m_Widget.HaltDrift();
      }
      m_Widget.VerifyVisibleState(this);
      m_Final = true;
    }
  }

  public override bool Merge(BaseCommand other) {
    if (base.Merge(other)) { return true; }
    if (m_Final) { return false; }
    MoveWidgetCommand move = other as MoveWidgetCommand;
    if (move != null && m_Widget == move.m_Widget) {
      m_EndTransform = move.m_EndTransform;
      m_CustomDimension.endState = move.m_CustomDimension.endState;
      // Not used if (m_Type != Type.Selection)
      m_EndSelectionTransform = move.m_EndSelectionTransform;
      m_Final = move.m_Final;
      return true;
    }
    HideWidgetCommand hide = other as HideWidgetCommand;
    if (hide != null && m_Widget == hide.Widget) {
      m_Children.Add(hide);
      m_Final = true;
      return true;
    }
    PinWidgetCommand pin = other as PinWidgetCommand;
    if (pin != null && m_Widget == pin.Widget) {
      m_Children.Add(pin);
      if (pin.IsPinning) {
        m_Final = true;
      }
      return true;
    }
    // Strokes are deleted right after a move if the SelectionWidget is tossed.
    DeleteSelectionCommand delete = other as DeleteSelectionCommand;
    if (delete != null && m_Type == Type.Selection) {
      m_Children.Add(delete);
      m_Final = true;
      return true;
    }
    // If a widget has been tossed but the animation is not yet complete, finish it here.
    if (m_Widget.IsTossed() || m_Widget.IsHiding()) {
      if (m_Type == Type.Selection) {
        // Additional "hide now" logic if the there is a selection that needs to be deleted
        // when the selection widget hides.
        DeleteSelectionCommand del = SelectionManager.m_Instance.CurrentDeleteSelectionCommand();
        del.Redo();
        m_Children.Add(del);
        SelectionManager.m_Instance.ResolveChanges();
      }

      m_Widget.HideNow();
      HideWidgetCommand hideComm = new HideWidgetCommand(m_Widget);
      hideComm.Redo();
      m_Children.Add(hideComm);

      if (m_Type == Type.Selection) {
        // If a selection is made while the selection widget is animating out, the transform
        // passed into the SelectCommand constructor is incorrect. It should always be the identity
        // because it is the transform of a new selection since the previous selection was deleted.
        SelectCommand select = other as SelectCommand;
        if (select != null) {
          select.ResetInitialTransform();
        }
      }
    }
    // Mark as final if a merge is failed, because a non-compatible command was pushed on top.
    // This will prevent future move commands from merging if the non-compatible command is undone.
    m_Final = true;
    return false;
  }
}
} // namespace TiltBrush
