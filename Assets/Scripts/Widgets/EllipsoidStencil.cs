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

// Internal invariants:
// - transform.localScale == Extents == m_Size * m_AspectRatio
// - Object-space sphere radius is 0.5 (this is just the previous invariant, restated)
// - m_AspectRatio.Max() == 1
//
public class EllipsoidStencil : StencilWidget {
  struct AxisDirection {
    public Axis axis;
    public Vector3 direction;
  }

  // Kind of arbitrary -- this is based on the dimensions of a rugby ball
  const float kInitialWidth = .7073f;

  static AxisDirection[] sm_AxisDirections = {
    new AxisDirection { axis = Axis.X,  direction = new Vector3(1, 0, 0) },
    new AxisDirection { axis = Axis.Y,  direction = new Vector3(0, 1, 0) },
    new AxisDirection { axis = Axis.Z,  direction = new Vector3(0, 0, 1) },
    new AxisDirection { axis = Axis.XY, direction = new Vector3(1, 1, 0).normalized },
    new AxisDirection { axis = Axis.XZ, direction = new Vector3(1, 0, 1).normalized },
    new AxisDirection { axis = Axis.YZ, direction = new Vector3(0, 1, 1).normalized }
  };

  private Vector3 m_AspectRatio;

  // Radius of our attached sphere mesh.
  // Because Extent == scale, a scale of 1 means extent of 1; therefore this must be .5.
  // (This has no bearing on the collision math)
  private const float kRadiusInObjectSpace = .5f;

  private const float kMinAspectRatio = 0.2f;

  public override Vector3 Extents {
    get {
      return m_Size * m_AspectRatio;
    }
    set {
      m_Size = 1;
      m_AspectRatio = value;
      UpdateScale();
    }
  }

  public override Vector3 CustomDimension {
    get { return m_AspectRatio; }
    set {
      m_AspectRatio = value;
      UpdateScale();
    }
  }

  protected override Vector3 HomeSnapOffset {
    get { return Vector3.zero; }
  }

  protected override void UpdateScale() {
    float maxAspect = m_AspectRatio.Max();
    m_AspectRatio /= maxAspect;
    m_Size *= maxAspect;
    m_AspectRatio = CMax(m_AspectRatio, Vector3.one * kMinAspectRatio);
    Vector3 extent_GS = m_Size * m_AspectRatio;
    float extent_OS = kRadiusInObjectSpace * 2;
    transform.localScale = extent_GS / extent_OS;
    UpdateMaterialScale();
  }

  override protected void SpoofScaleForShowAnim(float showRatio) {
    Vector3 extent_GS = m_Size * m_AspectRatio;
    float extent_OS = kRadiusInObjectSpace * 2;
    transform.localScale = (extent_GS / extent_OS) * showRatio;
  }

  protected override void Awake() {
    base.Awake();
    m_Type = StencilType.Ellipsoid;
    m_AspectRatio = new Vector3(1, kInitialWidth, kInitialWidth);
    UpdateScale();
  }

  // Collision

  // Helpers: component-wise vector functions

  private static Vector3 CMax(Vector3 va, Vector3 vb) {
    return new Vector3(Mathf.Max(va.x, vb.x),
                       Mathf.Max(va.y, vb.y),
                       Mathf.Max(va.z, vb.z));
  }

  private static Vector3 CMul(Vector3 va, Vector3 vb) {
    return new Vector3(va.x * vb.x, va.y * vb.y, va.z * vb.z);
  }

  private static Vector3 CDiv(Vector3 va, Vector3 vb) {
    return new Vector3(va.x / vb.x, va.y / vb.y, va.z / vb.z);
  }

  public override void FindClosestPointOnSurface(Vector3 pos,
      out Vector3 surfacePos, out Vector3 surfaceNorm) {
    // The closest-point functions operate on an unrotated ellipsoid at the origin.
    // I'll call that coordinate system "ellipse space (ES)".
    // "object space (OS)" is the true scale-compensated object-space coordinate system.

    // Take (uniform) scale from parent, but no scale from this object.
    // Our local scale is instead treated as Extents
    TrTransform xfWorldFromEllipse =
        TrTransform.FromTransform(transform.parent) *
        TrTransform.TR(transform.localPosition, transform.localRotation);

    TrTransform xfEllipseFromWorld = xfWorldFromEllipse.inverse;

    Vector3 halfExtent = Extents * .5f;
    Vector3 pos_OS = transform.InverseTransformPoint(pos);
    Vector3 pos_ES = xfEllipseFromWorld * pos;
    Vector3 closest_ES = MathEllipsoidAnton.ClosestPointEllipsoid(halfExtent, pos_ES);

    // Transform from ES -> OS, get the OS normal, then transform OS -> WS.
    // Normals are axial, so OS -> WS uses the inv-transpose. That all ends
    // up simplifying to this:
    surfaceNorm = transform.rotation *
        CDiv(closest_ES,
             CMul(halfExtent, halfExtent)).normalized;
    surfacePos = xfWorldFromEllipse * closest_ES;
  }

  override public float GetActivationScore(
      Vector3 vControllerPos, InputManager.ControllerName name) {
    Vector3 pos_OS = transform.InverseTransformPoint(vControllerPos);
    float baseScore = 1f - pos_OS.magnitude / kRadiusInObjectSpace;
    // don't try to scale if invalid; scaling by zero will make it look valid
    if (baseScore < 0) { return baseScore; }
    return baseScore * Mathf.Pow(1 - m_Size / m_MaxSize_CS, 2);
  }

  // Manipulation, highlight

  protected override Axis GetInferredManipulationAxis(
      Vector3 primary, Vector3 secondary, bool secondaryInside)  {
    if (secondaryInside) {
      return Axis.Invalid;
    }

    Vector3 vHandsInObjectSpace = transform.InverseTransformDirection(primary - secondary);
    Vector3 vAbs = vHandsInObjectSpace.Abs();

    Axis bestAxis = Axis.Invalid;
    float bestDot = 0;
    for (int i = 0; i < sm_AxisDirections.Length; ++i) {
      float dot = Vector3.Dot(vAbs, sm_AxisDirections[i].direction);
      if (dot > bestDot) {
        bestDot = dot;
        bestAxis = sm_AxisDirections[i].axis;
      }
    }

    return bestAxis;
  }

  public override Axis GetScaleAxis(
      Vector3 handA, Vector3 handB,
      out Vector3 axisVec, out float extent) {
    // Unexpected -- normally we're only called during a 2-handed manipulation
    Debug.Assert(m_LockedManipulationAxis != null);
    Axis axis = m_LockedManipulationAxis ?? Axis.Invalid;

    float parentScale = TrTransform.FromTransform(transform.parent).scale;

    Vector3 delta = handB - handA;
    Vector3 extents = Extents;

    // Fill in axisVec, extent
    switch (axis) {
    case Axis.X: case Axis.Y: case Axis.Z: {
      Vector3 axisVec_OS = Vector3.zero;
      axisVec_OS[(int)axis] = 1;
      axisVec = transform.TransformDirection(axisVec_OS);
      extent = parentScale * extents[(int)axis];
      break;
    }

    case Axis.YZ: {
      Vector3 plane = transform.rotation * new Vector3(1, 0, 0);
      axisVec = (delta - Vector3.Dot(delta, plane) * plane).normalized;
      extent = parentScale * Mathf.Max(extents[1], extents[2]);
      break;
    }

    case Axis.XZ: {
      Vector3 plane = transform.rotation * new Vector3(0, 1, 0);
      axisVec = (delta - Vector3.Dot(delta, plane) * plane).normalized;
      extent = parentScale * Mathf.Max(extents[0], extents[2]);
      break;
    }

    case Axis.XY: {
      Vector3 plane = transform.rotation * new Vector3(0, 0, 1);
      axisVec = (delta - Vector3.Dot(delta, plane) * plane).normalized;
      extent = parentScale * Mathf.Max(extents[0], extents[1]);
      break;
    }

    case Axis.Invalid:
      axisVec = default(Vector3);
      extent = default(float);
      break;
    default:
      throw new NotImplementedException(axis.ToString());
    }

    return axis;
  }

  public override void RecordAndApplyScaleToAxis(float deltaScale, Axis axis) {
    Vector3 aspectRatio = m_AspectRatio;
    switch (axis) {
    case Axis.X: case Axis.Y: case Axis.Z:
      aspectRatio[(int)axis] *= deltaScale;
      break;
    case Axis.YZ:
      aspectRatio[1] *= deltaScale;
      aspectRatio[2] *= deltaScale;
      break;
    case Axis.XZ:
      aspectRatio[0] *= deltaScale;
      aspectRatio[2] *= deltaScale;
      break;
    case Axis.XY:
      aspectRatio[0] *= deltaScale;
      aspectRatio[1] *= deltaScale;
      break;
    default:
      throw new NotImplementedException(axis.ToString());
    }
    if (m_RecordMovements) {
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new MoveWidgetCommand(this, LocalTransform, aspectRatio));
    } else {
      m_AspectRatio = aspectRatio;
      UpdateScale();
    }
  }

  protected override void RegisterHighlightForSpecificAxis(Axis highlightAxis) {
    if (m_HighlightMeshFilters != null) {
      for (int i = 0; i < m_HighlightMeshFilters.Length; i++) {
        App.Instance.SelectionEffect.RegisterMesh(m_HighlightMeshFilters[i]);
      }
    }
  }

  public override Bounds GetBounds_SelectionCanvasSpace() {
    if (m_Collider != null) {
      SphereCollider sphere = m_Collider as SphereCollider;
      TrTransform colliderToCanvasXf = App.Scene.SelectionCanvas.Pose.inverse *
          TrTransform.FromTransform(m_Collider.transform);
      Bounds bounds = new Bounds(colliderToCanvasXf * sphere.center, Vector3.zero);

      // Transform the corners of the widget bounds into canvas space and extend the total bounds
      // to encapsulate them.
      for (int i = 0; i < 8; i++) {
        bounds.Encapsulate(colliderToCanvasXf * (sphere.center +
            sphere.radius *
            new Vector3((i & 1) == 0 ? -1.0f : 1.0f,
                        (i & 2) == 0 ? -1.0f : 1.0f,
                        (i & 4) == 0 ? -1.0f : 1.0f)));
      }

      return bounds;
    }
    return base.GetBounds_SelectionCanvasSpace();
  }
}

} // namespace TiltBrush
