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

/// Add this class to an object in order to inherit Canvas/Scene position + rotation.
///
/// If you use this component, call OnPosRotChanged() after modifying the
/// object's global position or rotation. Alternatively, you can assign to
/// this component's position/rotation/forward properties.
///
/// For scale, you should use this component's globalScale property instead
/// of accessing transform.localScale/lossyScale
///
public class NonScaleChild : MonoBehaviour {
  public enum Parent {
    Canvas,
    Scene
  }

  [SerializeField] private Parent m_Parent;

  // This class can be implemented a couple ways:
  // 1. parent to the Canvas/Scene: tweak localScale when canvas/scene changes,
  //    intercept get/set of localScale
  // 2. remain parented in global coordinates: tweak pos/rot when
  //    canvas/scene changes, intercept get/set of pos and rot.
  //
  // 1. seems slightly more invasive since it changes the parent,
  // so let's try going with 2. for now.
  //
  TrTransform m_xfLocal;
  CanvasScript m_canvas;

  // Returns only position and rotation. On get,
  // scale is always zero. On set, scale is ignored.
  // Analogous to localPosition and localRotation.
  public TrTransform PositionRotationInParentSpace {
    get {
      TrTransform xfLocal = m_xfLocal;
      xfLocal.scale = 0.0f;
      return xfLocal;
    }
    set {
      Vector3 localScale = transform.localScale;
      m_xfLocal = value;
      if (m_Parent == Parent.Scene) {
        App.Scene.AsScene[transform] = m_xfLocal;
      } else {
        m_canvas.AsCanvas[transform] = m_xfLocal;
      }
      transform.localScale = localScale;
    }
  }

  public Quaternion localRotation {
    get { return m_xfLocal.rotation; }
    set {
      m_xfLocal.rotation = value;
      transform.rotation = parent.rotation * m_xfLocal.rotation;
    }
  }

  /// Controls which canvas we're "parented" to.
  /// May only be used if m_Parent is Parent.Canvas.
  public CanvasScript ParentCanvas {
    get {
      if (m_Parent != Parent.Canvas) { throw new InvalidOperationException(); }
      return m_canvas;
    }
    set {
      if (m_Parent != Parent.Canvas) { throw new InvalidOperationException(); }
      if (value != m_canvas) {
        if (m_canvas != null) { m_canvas.PoseChanged -= OnParentPoseChanged; }
        m_canvas = value;
        m_canvas.PoseChanged += OnParentPoseChanged;
        OnPosRotChanged();
      }
    }
  }

  void Start() {
    if (m_Parent == Parent.Scene) {
      App.Scene.PoseChanged += OnParentPoseChanged;
      OnPosRotChanged();
    } else {
      ParentCanvas = App.ActiveCanvas;
    }
  }

  void OnDestroy() {
    if (m_Parent == Parent.Scene) {
      App.Scene.PoseChanged -= OnParentPoseChanged;
    } else {
      m_canvas.PoseChanged -= OnParentPoseChanged;
    }
  }

  void OnParentPoseChanged(TrTransform prev, TrTransform current) {
    if (enabled) {
      var xfNewGlobal = current * m_xfLocal;
      transform.position = xfNewGlobal.translation;
      transform.rotation = xfNewGlobal.rotation;
    }
  }

  public Transform parent {
    get {
      if (m_Parent == Parent.Scene) {
        return App.Instance.m_SceneTransform;
      } else {
        return App.Instance.m_CanvasTransform;
      }
    }
  }

  // In this implementation, no need to intercept localScale
  public Vector3 globalScale {
    get { return transform.localScale; }
    set { transform.localScale = value; }
  }

  /// Call this after you have changed the global transform, to update
  /// the parent-relative transform.
  ///
  /// NOTE: For objects whose scene-local position you care about
  /// (ie, the mirror, which is the last user of this object), you should
  /// in general not write to the global transform; only write to the scene-local
  /// transform, and let the global be updated. This means you should not
  /// be calling this function.
  public void OnPosRotChanged() {
    if (m_Parent == Parent.Scene) {
      m_xfLocal = App.Scene.AsScene[transform];
    } else {
      m_xfLocal = m_canvas.AsCanvas[transform];
    }
  }
}

} // namespace TiltBrush
