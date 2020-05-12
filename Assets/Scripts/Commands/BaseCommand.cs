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
using System.Linq;
using System.Collections.Generic;

using UnityEngine;

namespace TiltBrush {
/// Commands on the undo stack of SketchMemoryScript
///
/// Subclasses should override the virtual methods to
/// implement their custom behavior, and must either call
/// the base class or be sure to recurse into children.
public class BaseCommand : IDisposable {
  protected List<BaseCommand> m_Children;

  protected static Vector3 GetPositionForCommand(Stroke stroke) {
    var points = stroke.m_ControlPoints;
    Debug.Assert(points != null && points.Length > 0);
    return (points[0].m_Pos + points[points.Length-1].m_Pos) * .5f;
  }

  protected static Vector3 GetPositionForCommand(Stroke[] strokes) {
    Debug.Assert(strokes != null);
    if (strokes.Length == 0) { return Vector3.zero; }
    return GetPositionForCommand(strokes[0]);
  }

  /// Create this command as a child of the passed command.
  /// Commands with parents should not be placed on the undo
  /// stack; only the root parent should be.
  /// The constructor should not mutate sketch state.
  public BaseCommand(BaseCommand parent = null) {
    m_Children = new List<BaseCommand>();
    if (parent != null) {
      parent.m_Children.Add(this);
    }
  }

  /// True if this command changes the sketch in a saveable
  /// manner. This command matters if any of its children do.
  virtual public bool NeedsSave {
    get {
      return m_Children.Count > 0 && m_Children.Any(c => c.NeedsSave);
    }
  }

  /// True if this command does not change any state. This should occur
  /// only due to merging of commands. This getter is properly overridden
  /// only for SwitchEnvironmentCommand and PropVisibleCommand as they are
  /// the only commands whose merge functions can put them in no-op states.
  virtual protected bool IsNoop { get { return false; } }

  /// Undo this entire command tree.
  /// Parent is always undone after all children.
  /// Children are undone in reverse order.
  public void Undo() {
    foreach (BaseCommand comm in m_Children.AsEnumerable().Reverse()) {
      comm.Undo();
    }
    OnUndo();
  }

  virtual protected void OnUndo() { }

  /// Redo this entire command tree.
  /// Parent is always redone before all children.
  /// Children are redone in order.
  public void Redo() {
    OnRedo();
    foreach (BaseCommand comm in m_Children) {
      comm.Redo();
    }
  }

  virtual protected void OnRedo() { }

  /// API is only for undo/redo stack.
  /// Override to define how a command can merge with another.
  /// Returns true upon successful merge, otherwise false.
  /// This command is redone before other and undone after other.
  ///
  /// The base implementation discards no-op commands and should
  /// and should always be called by subclasses.
  public virtual bool Merge(BaseCommand other) {
    return other.IsNoop;
  }

  /// Dispose of this entire command tree if there are any
  /// controlled resources.
  /// Parent is always destroyed after all children.
  /// Children are destroyed in reverse order.
  public void Dispose() {
    foreach (BaseCommand comm in m_Children.AsEnumerable().Reverse()) {
      comm.Dispose();
    }
    OnDispose();
  }

  virtual protected void OnDispose() { }
}
} // namespace TiltBrush