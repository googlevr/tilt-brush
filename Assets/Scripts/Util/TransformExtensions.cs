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

public static class TransformExtensions {
  /// If Transform has global uniform scale, returns exactly that scale.
  /// If it has non-uniform or non-axis-aligned scale, results are
  /// undefined.
  public static float GetUniformScale(this Transform xf) {
    // This works:
    //   return xf.lossyScale.x;
    // but .lossyScale can cause little floating-point distortions in the
    // case that we care about (pure uniform scale).  This implementation
    // avoids those distortions, at the expense of being "more incorrect"
    // if xf has nonuniform scale.
    float uniformScale = xf.localScale.x;
    for (Transform cur = xf.parent; cur != null; cur = cur.parent) {
      uniformScale *= cur.localScale.x;
    }
    return uniformScale;
  }

  /// Sets global scale to be uniform.
  /// If parent exists and has non-uniform scale, results are undefined.
  public static void SetUniformScale(this Transform xf, float scale) {
    Transform parent = xf.parent;
    if (parent != null) {
      scale /= parent.GetUniformScale();
    }
    xf.localScale = new Vector3(scale, scale, scale);
  }

  /// A helper object that wraps TrTransform.From/ToLocalTransform for
  /// a slightly more succinct syntax.
  ///
  /// Usage:
  ///   public static LocalAccessor AsLocal = new LocalAccessor();
  ///   AsLocal[gameObj.transform] = TrTransform.identity;  // zero out local transform
  ///
  /// C# does not allow extension properties; otherwise this could have been
  ///   gameObj.Transform.local = TrTransform.identity;
  ///
  public struct LocalAccessor {
    public TrTransform this[Transform t] {
      get { return TrTransform.FromLocalTransform(t); }
      set { value.ToLocalTransform(t); }
    }
  }

  /// A helper object that wraps TrTransform.From/ToTransform for
  /// a slightly more succinct syntax.
  ///
  /// Usage:
  ///   public static GlobalAccessor AsGlobal = new GlobalAccessor();
  ///   AsGlobal[gameObj.transform] = TrTransform.identity;  // zero out world transform
  ///
  /// C# does not allow extension properties; otherwise this could have been
  ///   gameObj.Transform.global = TrTransform.identity;
  ///
  public struct GlobalAccessor {
    public TrTransform this[Transform t] {
      get { return TrTransform.FromTransform(t); }
      set { value.ToTransform(t); }
    }
  }

  /// A helper object that allows trasforms to be get/set using
  /// data relative to the specified transform.
  /// Usage:
  ///   public static RelativeAccessor AsCanvas = new RelativeAccessor(canvas);
  ///   AsCanvas[pointer.transform] = <a TrTransform in canvas coordinates>
  ///
  public struct RelativeAccessor {
    readonly Transform m_parent;
    GlobalAccessor AsGlobal;
    LocalAccessor AsLocal;

    public RelativeAccessor(Transform parent) {
      m_parent = parent;
      AsGlobal = new GlobalAccessor();
      AsLocal = new LocalAccessor();
    }

    public TrTransform this[Transform target] {
      // Value being get/set is relative to m_parent; the fundamental invariant is:
      //   target.global = m_parent.global * value
      get {
        // This works, but loses precision
        // return AsGlobal[m_parent].inverse * AsGlobal[target];
        if (target == m_parent) {
          return TrTransform.identity;
        }
        // Concatenate up to the root, or to m_parent, whichever comes first.
        var concatenated = AsLocal[target];
        for (var ancestor = target.parent; ancestor != null; ancestor = ancestor.parent) {
          if (ancestor == m_parent) {
            return concatenated;
          } else {
            concatenated = AsLocal[ancestor] * concatenated;
          }
        }
        // And project down into m_parent's coordinate system
        return TrTransform.InvMul(AsGlobal[m_parent], concatenated);
      }

      set {
        // This works, but loses precision
        // AsGlobal[target] = AsGlobal[m_parent] * value;
        if (target == m_parent) {
          throw new System.InvalidOperationException("Can't set transform relative to self");
        }
        // Concatenate up to the root, or to m_parent, whichever comes first.
        // Skip target's local transform, because we're replacing it
        var concatenated = TrTransform.identity;
        for (var ancestor = target.parent; ancestor != null; ancestor = ancestor.parent) {
          if (ancestor == m_parent) {
            AsLocal[target] = TrTransform.InvMul(concatenated, value);
            return;
          } else {
            concatenated = AsLocal[ancestor] * concatenated;
          }
        }
        AsLocal[target] = TrTransform.InvMul(concatenated, AsGlobal[m_parent] * value);
      }
    }
  }
}

} // namespace TiltBrush
