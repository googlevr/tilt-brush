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
public class SphereStencil : StencilWidget {
  public override Vector3 Extents {
    get {
      return m_Size * Vector3.one;
    }
    set {
      if (value.x == value.y && value.x == value.z) {
        SetSignedWidgetSize(value.x);
      } else {
        throw new ArgumentException("SphereStencil does not support non-uniform extents");
      }
    }
  }

  protected override void Awake() {
    base.Awake();
    m_Type = StencilType.Sphere;
  }

  public override void FindClosestPointOnSurface(Vector3 pos,
      out Vector3 surfacePos, out Vector3 surfaceNorm) {
    Vector3 vCenterToPos = pos - transform.position;
    float fRadius = Mathf.Abs(GetSignedWidgetSize()) * 0.5f * Coords.CanvasPose.scale;
    surfacePos = transform.position + vCenterToPos.normalized * fRadius;
    surfaceNorm = vCenterToPos;
  }

  override public float GetActivationScore(
      Vector3 vControllerPos, InputManager.ControllerName name) {
    float fRadius = Mathf.Abs(GetSignedWidgetSize()) * 0.5f * Coords.CanvasPose.scale;
    float baseScore = (1.0f - (transform.position - vControllerPos).magnitude / fRadius);
    // don't try to scale if invalid; scaling by zero will make it look valid
    if (baseScore < 0) { return baseScore; }
    return baseScore * Mathf.Pow(1 - m_Size / m_MaxSize_CS, 2);
  }

  protected override Axis GetInferredManipulationAxis(
      Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInside) {
    return Axis.Invalid;
  }

  protected override void RegisterHighlightForSpecificAxis(Axis highlightAxis) {
    throw new NotImplementedException();
  }

  public override Axis GetScaleAxis(
      Vector3 handA, Vector3 handB,
      out Vector3 axisVec, out float extent) {
    // Unexpected -- normally we're only called during a 2-handed manipulation
    Debug.Assert(m_LockedManipulationAxis != null);
    Axis axis = m_LockedManipulationAxis ?? Axis.Invalid;

    // Fill in axisVec, extent
    switch (axis) {
    case Axis.Invalid:
      axisVec = default(Vector3);
      extent = default(float);
      break;
    default:
      throw new NotImplementedException(axis.ToString());
    }

    return axis;
  }

  public override Bounds GetBounds_SelectionCanvasSpace() {
    if (m_Collider != null) {
      SphereCollider sphere = m_Collider as SphereCollider;
      TrTransform colliderToCanvasXf = App.Scene.SelectionCanvas.Pose.inverse *
          TrTransform.FromTransform(m_Collider.transform);
      Bounds bounds = new Bounds(colliderToCanvasXf * sphere.center, Vector3.zero);

      // Spheres are invariant with rotation, so take out the rotation from the transform and just
      // add the two opposing corners.
      colliderToCanvasXf.rotation = Quaternion.identity;
      bounds.Encapsulate(colliderToCanvasXf * (sphere.center + sphere.radius * Vector3.one));
      bounds.Encapsulate(colliderToCanvasXf * (sphere.center - sphere.radius * Vector3.one));

      return bounds;
    }
    return base.GetBounds_SelectionCanvasSpace();
  }
}
} // namespace TiltBrush
