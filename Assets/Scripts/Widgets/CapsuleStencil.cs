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
using UnityEngine;

namespace TiltBrush {
public class CapsuleStencil : StencilWidget {
  [SerializeField] private Transform m_CapA;
  [SerializeField] private Transform m_CapB;
  [SerializeField] private Transform m_Body;
  [SerializeField] private Transform[] m_CapXfs;
  [SerializeField] private Transform[] m_BodyXfs;

  private MeshFilter[] m_CapMeshFilters;
  private MeshFilter[] m_BodyMeshFilters;
  private Vector3 m_CapDimensions;
  private Vector3 m_BodyDimensions;
  private CapsuleCollider m_Capsule;

  private const float CAPSULE_HEIGHT = 2;

  public override Vector3 Extents {
    get {
      return new Vector3(
          m_Size * m_Capsule.radius * 2,
          m_Size * m_Capsule.height,
          m_Size * m_Capsule.radius * 2);
    }
    set {
      if (! (value.x == value.z)) {
        throw new ArgumentException("Capsule requires x == z");
      } else if (! (value.y >= value.x)) {
        throw new ArgumentException("Capsule requires y >= x");
      }
      m_Size = 1;
      m_Capsule.radius = value.x / 2;
      m_Capsule.height = value.y;
      NormalizeHeight();
    }
  }

  public override Vector3 CustomDimension {
    get { return new Vector3(m_Capsule.radius, m_Capsule.height, m_Capsule.radius); }
    set {
      m_Capsule.radius = value.x;
      m_Capsule.height = value.y;
      NormalizeHeight();
      UpdateMaterialScale();
    }
  }

  private float BodyHeight {
    get { return m_Capsule.height - 2 * m_Capsule.radius; }
  }

  protected override void Awake() {
    base.Awake();
    m_Type = StencilType.Capsule;
    m_CapDimensions = m_CapA.localScale;
    m_BodyDimensions = m_Body.localScale;
    m_Capsule = m_Collider as CapsuleCollider;
    Debug.Assert(m_Capsule.direction == 1);

    m_CapMeshFilters = m_CapXfs.Select(xf => xf.GetComponent<MeshFilter>()).ToArray();
    m_BodyMeshFilters = m_BodyXfs.Select(xf => xf.GetComponent<MeshFilter>()).ToArray();
  }

  protected override Axis GetInferredManipulationAxis(
      Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInside) {
    if (secondaryHandInside) {
      return Axis.Invalid;
    }
    Vector3 secondaryHand_OS = transform.InverseTransformPoint(secondaryHand);
    if (Mathf.Abs(secondaryHand_OS.y) > BodyHeight / m_Capsule.height) {
      return Axis.Y;
    } else {
      return Axis.XZ;
    }
  }

  protected override void RegisterHighlightForSpecificAxis(Axis highlightAxis) {
    switch (highlightAxis) {
    case Axis.Y:
      for (int i = 0; i < m_CapXfs.Length; i++) {
        App.Instance.SelectionEffect.RegisterMesh(m_CapMeshFilters[i]);
      }
      break;
    case Axis.XZ:
      for (int i = 0; i < m_BodyXfs.Length; i++) {
        App.Instance.SelectionEffect.RegisterMesh(m_BodyMeshFilters[i]);
      }
      break;
    default:
      throw new InvalidOperationException(highlightAxis.ToString());
    }
  }

  public override Axis GetScaleAxis(
      Vector3 handA, Vector3 handB,
      out Vector3 axisVec, out float extent) {
    // Unexpected -- normally we're only called during a 2-handed manipulation
    Debug.Assert(m_LockedManipulationAxis != null);
    Axis axis = m_LockedManipulationAxis ?? Axis.Invalid;

    float parentScale = TrTransform.FromTransform(transform.parent).scale;

    // Fill in axisVec, extent
    switch (axis) {
    case Axis.Y: {
      Vector3 axisVec_LS = Vector3.zero;
      axisVec_LS[(int)axis] = 1;
      axisVec = transform.TransformDirection(axisVec_LS);
      extent = parentScale * Extents[(int)axis];
      break;
    }
    case Axis.XZ: {
      // Flatten the vector between the controllers with respect to "up" and use that as the axis
      // to perform the non-uniform scale.
      Vector3 vHands = handB - handA;
      vHands -= transform.up * Vector3.Dot(transform.up, vHands);
      axisVec = vHands.normalized;
      // Make sure caller doesn't try to do anything funny with this axis, like
      // index into Extents[], create their own axis direction, etc
      extent = parentScale * Extents[0];
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

  /// Maintains the invariants:
  ///   m_Capsule.height == 2  (adjusts m_Radius and m_Size as a result)
  ///   m_Capsule.radius is valid  (sets m_Radius to closest valid value)
  ///
  /// Adjusts sizes of sub-objects based on aspect ratio.
  private void NormalizeHeight() {
    // Capsule height includes both caps so it should never be less than 2r
    if (m_Capsule.height < 2 * m_Capsule.radius) {
      m_Capsule.height = 2 * m_Capsule.radius;
    }
    // Normalize height to equal 2
    float delta = m_Capsule.height / CAPSULE_HEIGHT;
    m_Size *= delta;
    UpdateScale();
    m_Capsule.radius /= delta;
    m_Capsule.height = CAPSULE_HEIGHT;

    // resize and position meshes
    m_CapA.localScale = m_CapDimensions / .5f /* original capsule radius */ * m_Capsule.radius;
    m_CapB.localScale = m_CapDimensions / .5f * m_Capsule.radius;
    m_CapA.localPosition = new Vector3(
      m_CapA.localPosition.x,
      m_Capsule.height / 2 - m_Capsule.radius,
      m_CapA.localPosition.z);
    m_CapB.localPosition = new Vector3(
      m_CapB.localPosition.x,
      -1 * (m_Capsule.height / 2 - m_Capsule.radius),
      m_CapB.localPosition.z);
    m_Body.localScale = new Vector3(
        m_BodyDimensions.x / .5f * m_Capsule.radius,
        m_BodyDimensions.y * BodyHeight,
        m_BodyDimensions.z / .5f * m_Capsule.radius
    );

    // Copy the scale and positions to the snap ghost parts
    for (int i = 0; i < m_Collider.transform.childCount; ++i) {
      Transform meshXf = m_Collider.transform.GetChild(i);
      Transform ghostXf = m_SnapGhost.GetChild(i);
      Debug.Assert(meshXf.name == ghostXf.name);
      ghostXf.localPosition = meshXf.localPosition;
      ghostXf.localScale = meshXf.localScale;
    }
  }

  public override void RecordAndApplyScaleToAxis(float deltaScale, Axis axis) {
    float height = m_Capsule.height;
    float radius = m_Capsule.radius;
    switch (axis) {
    case Axis.XZ:
      // Increase radius of cylidrical portion. Keep height of cylindrical portion constant. This
      // ends up increasing the overall height of the capsule.
      height -= 2 * radius;
      radius *= deltaScale;
      height += 2 * radius;
      break;
    case Axis.Y:
      height *= deltaScale;
      break;
    default:
      throw new ArgumentException("axis");
    }
    if (m_RecordMovements) {
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new MoveWidgetCommand(this, LocalTransform, new Vector3(radius, height, radius)));
    } else {
      m_Capsule.height = height;
      m_Capsule.radius = radius;
      NormalizeHeight();
    }
  }

  public override float GetActivationScore(
      Vector3 vControllerPos, InputManager.ControllerName name) {
    Vector3 localPos = transform.InverseTransformPoint(vControllerPos);

    // Early out if we're too high.
    if (Mathf.Abs(localPos.y) > m_Capsule.height * 0.5f) {
      return -1.0f;
    }

    float heightRatio = Mathf.Abs(localPos.y) / (m_Capsule.height * 0.5f);
    float halfY = (m_Capsule.height - 2.0f * m_Capsule.radius) * 0.5f;
    float distToPole = 0.0f;

    // Check against body.
    if (Mathf.Abs(localPos.y) < halfY) {
      Vector3 localNoY = localPos;
      localNoY.y = 0.0f;

      // If we're beyond the radius to the center, we're done here.
      distToPole = localNoY.magnitude;
      if (distToPole > m_Capsule.radius) {
        return -1.0f;
      }
    } else {
      // Check against end cap.
      Vector3 capCenter = new Vector3(0.0f, halfY * Mathf.Sign(localPos.y), 0.0f);
      distToPole = (localPos - capCenter).magnitude;

      // If we're beyond the radius to the cap center, we're done here.
      if (distToPole > m_Capsule.radius) {
        return -1.0f;
      }
    }

    // Score is distance to pole + distance above center.
    float baseScore = (1.0f - ((distToPole / m_Capsule.radius) * 0.5f) - (heightRatio * 0.5f));
    // don't try to scale if invalid; scaling by zero will make it look valid
    if (baseScore < 0) { return baseScore; }
    return baseScore * Mathf.Pow(1 - m_Size / m_MaxSize_CS, 2);
  }

  public override void FindClosestPointOnSurface(Vector3 pos,
      out Vector3 surfacePos, out Vector3 surfaceNorm) {
    // Convert world space position to local space.
    Vector3 localPos = transform.InverseTransformPoint(pos);

    // Check to see if we're above or below the capsule segment length.
    float halfY = (m_Capsule.height - 2.0f * m_Capsule.radius) * 0.5f;

    Vector3 closestPoint, normal;
    if (Mathf.Abs(localPos.y) < halfY) {
      // Along capsule segment, so our normal should point along XZ.
      closestPoint = new Vector3(0.0f, localPos.y, 0.0f);
      normal = new Vector3(localPos.x, 0, localPos.z).normalized;
      if (normal == Vector3.zero) {
        normal = Vector3.forward;
      }
    } else {
      // Beyond segment, so our normal should reflect an end cap.
      closestPoint = new Vector3(0.0f, halfY * Mathf.Sign(localPos.y), 0.0f);
      normal = (localPos - closestPoint).normalized;
      if (normal == Vector3.zero) {
        normal.Set(0.0f, Mathf.Sign(localPos.y), 0.0f);
      }
    }

    // Convert from closest point on segment to closest point on capsule.
    closestPoint += normal * m_Capsule.radius;
    surfacePos = transform.TransformPoint(closestPoint);
    surfaceNorm = transform.TransformVector(normal);
  }

  public override Bounds GetBounds_SelectionCanvasSpace() {
    if (m_Capsule != null) {
      TrTransform colliderToCanvasXf = App.Scene.SelectionCanvas.Pose.inverse *
          TrTransform.FromTransform(m_Capsule.transform);
      Bounds bounds = new Bounds(colliderToCanvasXf * m_Capsule.center, Vector3.zero);

      // Transform the corners of the widget bounds into canvas space and extend the total bounds
      // to encapsulate them.
      Vector3 capsuleSize = new Vector3(2*m_Capsule.radius, m_Capsule.height, 2*m_Capsule.radius);
      for (int i = 0; i < 8; i++) {
        bounds.Encapsulate(colliderToCanvasXf * (m_Capsule.center + Vector3.Scale(
            capsuleSize,
            new Vector3((i & 1) == 0 ? -0.5f : 0.5f,
                        (i & 2) == 0 ? -0.5f : 0.5f,
                        (i & 4) == 0 ? -0.5f : 0.5f))));
      }

      return bounds;
    }
    return base.GetBounds_SelectionCanvasSpace();
  }
}
} // namespace TiltBrush
