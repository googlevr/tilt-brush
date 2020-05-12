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

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
#define USE_TILT_BRUSH_CPP  // Specifies that some functions will use TiltBrushCpp.dll.
#endif

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TiltBrush {
static public class MathUtils {
  static public class TiltBrushCpp {
#if USE_TILT_BRUSH_CPP
  [DllImport("TiltBrushCpp")] unsafe public static extern void TransformVector3AsPoint(
      Matrix4x4 mat, int iVert, int iVertEnd, Vector3* v3);
  [DllImport("TiltBrushCpp")] unsafe public static extern void TransformVector3AsVector(
      Matrix4x4 mat, int iVert, int iVertEnd, Vector3* v3);
  [DllImport("TiltBrushCpp")] unsafe public static extern void TransformVector3AsZDistance(
      float scale, int iVert, int iVertEnd, Vector3* v3);
  [DllImport("TiltBrushCpp")] unsafe public static extern void TransformVector4AsPoint(
      Matrix4x4 mat, int iVert, int iVertEnd, Vector4* v4);
  [DllImport("TiltBrushCpp")] unsafe public static extern void TransformVector4AsVector(
      Matrix4x4 mat, int iVert, int iVertEnd, Vector4* v4);
  [DllImport("TiltBrushCpp")] unsafe public static extern void TransformVector4AsZDistance(
      float scale, int iVert, int iVertEnd, Vector4* v4);
  [DllImport("TiltBrushCpp")] unsafe public static extern void GetBoundsFor(
      Matrix4x4 m, int iVert, int iVertEnd, Vector3* v3, Vector3* center, Vector3* size);
#endif

  }
  /// Returns difference between two periodic values.
  /// Result range is [-period/2, period/2)
  static public float PeriodicDifference(
      float lhs, float rhs, float period) {
    // % has range (-period, period)
    float delta = (lhs - rhs) % period;
    // Convert to [0, period)
    if (delta < 0) { delta += period; }
    // Convert to [-period/2, period/2)
    if (delta >= period/2) { delta -= period; }
    return delta;
  }

  /// Decomposes a matrix into T, R, and uniform scale.
  ///
  /// It is an error to pass a matrix that cannot be decomposed this way;
  /// in particular, it is an error to pass a matrix with non-uniform scale.
  /// This error will pass undetected, and you will get undefined results.
  ///
  /// Extraction of uniform scale from the matrix will have small
  /// floating-point errors.
  static public void DecomposeMatrix4x4(
      Matrix4x4 m,
      out Vector3 translation,
      out Quaternion rotation,
      out float uniformScale) {
    translation = m.GetColumn(3);
    Vector3 fwd = m.GetColumn(2);  // shorthand for m * Vector3.forward
    Vector3 up  = m.GetColumn(1);  // shorthand for m * Vector3.up

    // Use triple product to determine if det(m) < 0 (detects a mirroring)
    float scaleSign = Mathf.Sign(Vector3.Dot(m.GetColumn(0),
                                             Vector3.Cross(m.GetColumn(1),
                                                           m.GetColumn(2))));
    rotation = Quaternion.LookRotation(fwd * scaleSign, up * scaleSign);

    // Which axis (or row) to use is arbitrary, but I'm going to standardize
    // on using the x axis.
    double x0 = m.m00;
    double x1 = m.m10;
    double x2 = m.m20;
    uniformScale = (float)Math.Sqrt(x0*x0 + x1*x1 + x2*x2) * scaleSign;
  }

  /// Returns signed angle in degrees, in [-180, 180].
  /// "stability" is a metric of how stable the calculation is.
  /// If v1 and v2 are unit-length, then "stability" will range from
  /// 0 (unstable) to 1 (stable).
  ///
  /// nAxis must be unit-length
  static public float GetAngleBetween(Vector3 v1, Vector3 v2, Vector3 nAxis, out float stability) {
    // Project v1 and v2 to plane defined by axis, then compute angle
    v1 = (v1 - Vector3.Dot(v1, nAxis) * nAxis);
    v2 = (v2 - Vector3.Dot(v2, nAxis) * nAxis);
    float lengthProduct = v1.magnitude * v2.magnitude;
    stability = lengthProduct;
    // Cannot discriminate between +ve and -ve angles.
    // Assumes acos returns [0, pi]
    float val = Mathf.Clamp(Vector3.Dot(v1, v2) / lengthProduct, -1, 1);
    val = Mathf.Acos(val);
    val *= Mathf.Sign(Vector3.Dot(nAxis, Vector3.Cross(v1, v2)));
    return val * Mathf.Rad2Deg;
  }

  /// Returns a qDelta such that:
  /// - qDelta's axis of rotation is |axis|
  /// - q1 ~= qDelta * q0 (as much as is possible, given the constraint)
  ///
  /// NOTE: for convenience, ensures quats are in the same hemisphere.
  /// This means that if you really do want to examine the "long way around" rotation
  /// (ie, delta angle > 180) then this function will do the wrong thing.
  static public Quaternion ConstrainRotationDelta(Quaternion q0, Quaternion q1, Vector3 axis) {
    // Bad things happen if they're not in the same hemisphere (rotation
    // goes the long way around and contains too much of "axis")
    if (Quaternion.Dot(q0, q1) < 0) {
      q1 = q1.Negated();
    }

    axis = axis.normalized;
    var adjust = q1 * Quaternion.Inverse(q0);
    // Constrain rotation to passed axis
    Vector3 lnAdjust = adjust.Log().Im();
    lnAdjust = axis * (Vector3.Dot(axis, lnAdjust));
    return new Quaternion(lnAdjust.x, lnAdjust.y, lnAdjust.z, 0).Exp();
  }

  // Projects a point on to a plane, provided the plane normal and plane point.
  // planeNormal should be normalized.
  static public Vector3 ProjectPosOnPlane(Vector3 planeNormal, Vector3 planePoint, Vector3 pos) {
    // Calculate the distance from the point to the plane.
    float dist = Vector3.Dot(planeNormal, (pos - planePoint)) * -1.0f;

    // Translate the point to the projection position.
    return pos + planeNormal * dist;
  }

  // Constructs an updated transform obj1 such that the object-space
  // positions of the left and right grip are invariant. More generally,
  // all points on the line L->R are invariant. Precisely:
  //
  // - inv(obj0) * L0.pos == inv(obj1) * L1.pos
  // - inv(obj0) * R0.pos == inv(obj1) * L1.pos
  //
  // If abs(R0-L0) != abs(R1-L1), then a scaling is necessary.
  // This method chooses to apply a uniform scale.
  //
  // If scale bounds kick in, only a single point on the L->R line can
  // be made invariant; see bUseLeftAsPivot for how this is chosen.
  //
  // Given 2 position deltas, there are 6 DOFs available:
  //  = 3DOF (movement of average position)
  //  + 2DOF (change of direction of vector between L and R)
  //  + 1DOF (scaling, change of length of vector between L and R)
  //
  // One more DOF is needed to fully specify the rotation. This method
  // chooses to get it from L and R's rotations about the L-R vector.
  //
  // Pass:
  //   gripL0, L1, R0, R1 -
  //     The left and right grip points, in their old (0) and new (1) positions.
  //     The scale portion of the TrTransform is ignored.
  //   obj0 -
  //     Transform of the object being manipulated.
  //   deltaScale{Min,Max} -
  //     Constrains the range of result.scale.
  //     Negative means do not constrain that endpoint.
  //   rotationAxisConstraint -
  //     Constrains the value of result.rotation. If passed, the delta rotation
  //     from obj0 -> result will only be about this axis.
  //   bUseLeftAsPivot -
  //     Controls the invariant point if scale constraints are applied.
  //     If false, uses the midpoint between L and R.
  //     If true, uses the point L.
  //
  // Returns:
  //   New position, rotation, and scale
  static public TrTransform TwoPointObjectTransformation(
      TrTransform gripL0, TrTransform gripR0, // prev
      TrTransform gripL1, TrTransform gripR1, // next
      TrTransform obj0,
      float deltaScaleMin = -1.0f, float deltaScaleMax = -1.0f,
      Vector3 rotationAxisConstraint = default(Vector3),
      bool bUseLeftAsPivot = false) {
    // Vectors from left-hand to right-hand
    Vector3 vLR0 = (gripR0.translation - gripL0.translation);
    Vector3 vLR1 = (gripR1.translation - gripL1.translation);

    // World-space position whose object-space position is used to constrain obj1
    //    inv(obj0) * vInvariant0 = inv(obj1) * vInvariant1
    Vector3 vInvariant0, vInvariant1; {
      // Use left grip or average of grips as pivot point. Maybe switch the
      // bool to be a parametric t instead, so if caller wants to use right
      // grip as pivot they don't need to swap arguments?
      float t = bUseLeftAsPivot ? 0f : 0.5f;
      vInvariant0 = Vector3.Lerp(gripL0.translation, gripR0.translation, t);
      vInvariant1 = Vector3.Lerp(gripL1.translation, gripR1.translation, t);
    }

    // Strategy:
    // 1. Move invariant point to the correct spot, with a translation.
    // 2. Rotate about that point.
    // 3. Uniform scale about that point.
    // Items 2 and 3 can happen in the same TrTransform, since rotation
    // and uniform scale commute as long as they use the same pivot.

    TrTransform xfDelta1 = TrTransform.T(vInvariant1 - vInvariant0);

    TrTransform xfDelta23; {
      // calculate worldspace scale; will adjust center-of-scale later
      float dist0 = vLR0.magnitude;
      float dist1 = vLR1.magnitude;
      float deltaScale = (dist0 == 0) ? 1 : dist1 / dist0;

      // Clamp scale if requested.
      if (deltaScaleMin >= 0) { deltaScale = Mathf.Max(deltaScale, deltaScaleMin); }
      if (deltaScaleMax >= 0) { deltaScale = Mathf.Min(deltaScale, deltaScaleMax); }

      // This gets the left-right axis pointing in the correct direction
      Quaternion qSwing0To1 = Quaternion.FromToRotation(vLR0, vLR1);
      // This applies some twist about that left-right axis. The choice of constraint axis
      // (vLR0 vs vLR1) depends on whether qTwist is right- or left-multiplied vs qReach.
      Quaternion qTwistAbout0 = Quaternion.Slerp(
          ConstrainRotationDelta(gripL0.rotation, gripL1.rotation, vLR0),
          ConstrainRotationDelta(gripR0.rotation, gripR1.rotation, vLR0),
          0.5f);
      Quaternion qDelta = qSwing0To1 * qTwistAbout0;
      // Constrain the rotation if requested.
      if (rotationAxisConstraint != default(Vector3)) {
        qDelta = ConstrainRotationDelta(Quaternion.identity, qDelta, rotationAxisConstraint);
      }

      xfDelta23 = TrTransform
          .TRS(Vector3.zero, qDelta, deltaScale)
          .TransformBy(TrTransform.T(vInvariant1));
    }

    return xfDelta23 * xfDelta1 * obj0;
  }

  // A simplified version of TwoPointObjectTransformation. The following properties are true:
  //   1. The object-local-space direction between the left and right hands remains constant.
  //   2. The object-local-space position of LerpUnclamped(left, right, constraintPositionT) remains
  //      constant.
  //   3. obj1 has the same scale as obj0.
  //   4. (Corollary of 1-3) The object-local-space positions of left and right remain constant, if
  //      the distance between them does not change.
  public static TrTransform TwoPointObjectTransformationNoScale(
      TrTransform gripL0, TrTransform gripR0,
      TrTransform gripL1, TrTransform gripR1,
      TrTransform obj0, float constraintPositionT) {
    // Vectors from left-hand to right-hand
    Vector3 vLR0 = (gripR0.translation - gripL0.translation);
    Vector3 vLR1 = (gripR1.translation - gripL1.translation);

    Vector3 pivot0;
    TrTransform xfDelta; {
      pivot0 = Vector3.LerpUnclamped(gripL0.translation, gripR0.translation, constraintPositionT);
      var pivot1 = Vector3.LerpUnclamped(gripL1.translation, gripR1.translation, constraintPositionT);
      xfDelta.translation = pivot1 - pivot0;

      xfDelta.translation = Vector3.LerpUnclamped(
          gripL1.translation - gripL0.translation,
          gripR1.translation - gripR0.translation,
          constraintPositionT);
      // TODO: check edge cases:
      // - |vLR0| or |vLR1| == 0 (ie, from and/or to are undefined)
      // - vLR1 == vLR0 * -1 (ie, infinite number of axes of rotation)
      xfDelta.rotation = Quaternion.FromToRotation(vLR0, vLR1);
      xfDelta.scale = 1;
    }

    Quaternion deltaL = ConstrainRotationDelta(gripL0.rotation, gripL1.rotation, vLR0);
    Quaternion deltaR = ConstrainRotationDelta(gripR0.rotation, gripR1.rotation, vLR0);
    xfDelta = TrTransform.R(Quaternion.Slerp(deltaL, deltaR, 0.5f)) * xfDelta;

    // Set pivot point
    xfDelta = xfDelta.TransformBy(TrTransform.T(pivot0));
    return xfDelta * obj0;
  }

  // Helper for TwoPointObjectTransformationNonUniformScale.
  // Scale the passed position along axis, about center.
  private static Vector3 ScalePosition(
      Vector3 position, float amount, Vector3 scaleCenter, Vector3 axis) {
    Vector3 relativePosition = position - scaleCenter;
    // Decompose relativePosition into vAlong and vAcross
    Vector3 vAlong = Vector3.Dot(relativePosition, axis) * axis;
    Vector3 vAcross = relativePosition - vAlong;
    // Recompose relativePosition, scaling the "vAlong" portion
    vAlong *= amount;
    relativePosition = vAlong + vAcross;
    return scaleCenter + relativePosition;
  }

  // Construct transform obj1 such that:
  // - inv(obj0) * L0.pos == inv(obj1) * L1.pos
  // - inv(obj0) * R0.pos == inv(obj1) * L1.pos
  //
  // In other words, the left and right grip points on the old object
  // are the same as the left and right grip points on the new object.
  // (Note that the optional constraints may then change the result.)
  //
  // Note that if abs(R0-L0) != abs(R1-L1), then a scaling is necessary.
  // This methods applies scale about the passed axis only.
  //
  // Given 2 position deltas, there are 6 DOFs available:
  //  = 3DOF (movement of average position)
  //  + 2DOF (change of direction of vector between L and R)
  //  + 1DOF (scaling, change of length of LR projected onto the scale axis)
  //
  // We need one more DOF to fully specify the rotation. We can get this
  // from L and R's rotations about the L-R vector (either old or
  // new, doesn't really matter).
  //
  // Pass:
  //   scaleAxis -
  //     Must be unit-length. Scaling is applied along this axis only.
  //   gripL0, L1, R0, R1 -
  //     The left and right grip points, in their old (0) and new (1) positions.
  //     The scale portion of the TrTransform is ignored.
  //   obj0 -
  //     Transform of the object being manipulated.
  //   out deltaScale -
  //     Returns the difference in scale along the given axis.
  //   finalScaleMin -
  //     Ignore scaling when gripL0 and gripR0 are closer together along scaleAxis than this.
  //     TODO: rename to finalScaleMin is a bad name for this parameter.
  //   deltaScale{Min,Max} -
  //     Constrains the range of deltaScale.
  //     Negative means do not constrain that endpoint.
  //     Note that it is very easy for zero deltaScale to be returned
  //
  // Returns:
  //   result.position, result.rotation -
  //                  new position and rotation
  //   result.scale - undefined; do not use
  //   deltaScale   - change in scale along vScaleAxis0, as a multiplier
  public static TrTransform TwoPointObjectTransformationNonUniformScale(
      Vector3 vScaleAxis0,
      TrTransform gripL0, TrTransform gripR0, // prev
      TrTransform gripL1, TrTransform gripR1, // next
      TrTransform obj0,
      out float deltaScale,
      float finalScaleMin = -1.0f,
      float deltaScaleMin = -1.0f, float deltaScaleMax = -1.0f
      ) {
    // The vectors from left -> right hand can be decomposed into
    // "along scale axis" and "across scale axis".
    // The length of "along scale axis" varies; the change is the amount of scaling.
    // The length of "across scale axis" remains constant, because it doesn't scale.

    float constraintPositionT = 0.5f;
    float along0;
    Vector3 vAcross;
    Vector3 vLR0;
    {
      vLR0 = (gripR0.translation - gripL0.translation);
      along0 = Vector3.Dot(vLR0, vScaleAxis0);

      // Prevent negative scale by ensuring Sign(along0) == Sign(along1).
      // This causes the scale to turn into rotation. Note that Sign(along1) always == 1.
      // It also simplifies the following along0 checks.
      float sign = Mathf.Sign(along0);
      along0 *= sign;
      vScaleAxis0 *= sign;

      vAcross = vLR0 - along0 * vScaleAxis0;

      // Ignore scaling along the axis when the controllers are too close together.
      // Also ignore in unstable cases.
      if (along0 < finalScaleMin || along0 < 1e-5f) {
        deltaScale = 1;
        return TwoPointObjectTransformationNoScale(gripL0, gripR0, gripL1, gripR1, obj0, constraintPositionT);
      }
    }

    float along1;
    Vector3 vLR1; {
      // Calculate |vAlong1| using the identity:
      // |vLR|^2 = |vAlong|^2 + |vAcross|^2
      vLR1 = (gripR1.translation - gripL1.translation);
      float along1Squared = vLR1.sqrMagnitude - vAcross.sqrMagnitude;
      if (along1Squared < 0) {
        // Impossible to satisfy the constraint. We can refuse to scale (along1 = along0),
        // or clamp the scale to the nearest valid value (along1 = 0).
        along1 = 0;
      } else {
        along1 = Mathf.Sqrt(along1Squared);
      }
    }

    deltaScale = along1 / along0;

    // For min scale clamping, constrain to the object center if it's between the controllers or to
    // the controller closest to the object center.
    if (deltaScaleMin > 0 && deltaScale < deltaScaleMin) {
      deltaScale = deltaScaleMin;
      // Calculate a ratio such that (ratio * gripR0 + (1 - ratio) * gripL0) lies on the plane that
      // is perpendicular to the non-uniform scale axis and goes through the object center. This is
      // used as the pivot for the transformation.
      float factorL = Vector3.Dot(vScaleAxis0, gripL0.translation);
      float factorR = Vector3.Dot(vScaleAxis0, gripR0.translation);
      float factorObj = Vector3.Dot(vScaleAxis0, obj0.translation);
      constraintPositionT = (factorObj - factorL) / (factorR - factorL);
      constraintPositionT = Mathf.Clamp01(constraintPositionT);
    }

    // For max scale clamping, constrain to the position between the two controllers.
    if (deltaScaleMax > 0 && deltaScale > deltaScaleMax) {
      deltaScale = deltaScaleMax;
      constraintPositionT = 0.5f;
    }

    // TwoPointNoScale can be used to compute T and R since those calculations are independent of S.
    // In the case where deltaScale == along1 / along0 (ie., where the scale has not been clamped),
    // using ScalePosition here will ensure |LR0| = |LR1|, so TwoPointNoScale can be used safely.
    // When scale is clamped, ScalePosition will compensate the initial positions as far as they
    // will go until the clamp and then TwoPointNoScale will compute T and R while keeping the
    // passed in constraint position fixed.
    gripL0.translation = ScalePosition(
        gripL0.translation, deltaScale, obj0.translation, vScaleAxis0);
    gripR0.translation = ScalePosition(
        gripR0.translation, deltaScale, obj0.translation, vScaleAxis0);

    return TwoPointObjectTransformationNoScale(gripL0, gripR0, gripL1, gripR1, obj0, constraintPositionT);
  }

  /// Like TwoPointNonUniformScale, but instead of scaling, adds size to an axis
  public static TrTransform TwoPointObjectTransformationAxisResize(
      Vector3 vScaleAxis0, float sizeAlongAxis,
      TrTransform gripL0, TrTransform gripR0, // prev
      TrTransform gripL1, TrTransform gripR1, // next
      TrTransform obj0,
      out float deltaScale,
      float deltaScaleMin = 0,
      float deltaScaleMax = float.PositiveInfinity) {
    // The vectors from left -> right hand can be decomposed into
    // "along scale axis" and "across scale axis".
    // The length of "along scale axis" varies; the change is the amount of scaling.
    // The length of "across scale axis" remains constant, because it doesn't scale.

    bool shrinkageSideIsLeft;
    float allowedShrinkage;
    float along0;
    Vector3 vAcross; {
      Vector3 vLR0 = (gripR0.translation - gripL0.translation);
      along0 = Vector3.Dot(vLR0, vScaleAxis0);

      // Prevent negative scale by ensuring Sign(along0) == Sign(along1).
      // This causes the scale to turn into rotation. Note that Sign(along1) always == 1.
      // It also simplifies the following along0 and shrinkage checks.
      float sign = Mathf.Sign(along0);
      along0 *= sign;
      vScaleAxis0 *= sign;

      vAcross = vLR0 - along0 * vScaleAxis0;

      Vector3 center = obj0.translation;
      float halfExtent = sizeAlongAxis / 2;
      // Allow the right edge to be pushed all the way to the left hand, but no farther;
      // and vice versa for the left edge + right hand.
      float leftShrinkage = halfExtent - Vector3.Dot(vScaleAxis0, gripL0.translation - center);
      float rightShrinkage = halfExtent - Vector3.Dot(vScaleAxis0, center - gripR0.translation);
      if (leftShrinkage < rightShrinkage) {
        allowedShrinkage = leftShrinkage;
        shrinkageSideIsLeft = true;
      } else {
        allowedShrinkage = rightShrinkage;
        shrinkageSideIsLeft = false;
      }

      // Might be < 0 if both hands are outside the extents (which won't normally happen)
      allowedShrinkage = Mathf.Max(0, allowedShrinkage);
    }

    float along1; {
      // Calculate |vAlong1| using the identity:
      // |vLR|^2 = |vAlong|^2 + |vAcross|^2
      Vector3 vLR1 = (gripR1.translation - gripL1.translation);
      float along1Squared = vLR1.sqrMagnitude - vAcross.sqrMagnitude;
      if (along1Squared < 0) {
        // Impossible to satisfy the constraint. We can refuse to scale (along1 = along0),
        // or clamp the scale to the nearest valid value (along1 = 0).
        along1 = 0;
      } else {
        along1 = Mathf.Sqrt(along1Squared);
      }
    }

    // Calculate and apply constraints to deltaScale
    {
      float sizeAlongAxis1 = sizeAlongAxis + Mathf.Max(-allowedShrinkage, along1 - along0);
      deltaScale = sizeAlongAxis1 / sizeAlongAxis;

      // If hands switch, treat that as rotation rather than inverting the object
      deltaScale = Mathf.Abs(deltaScale);
      deltaScale = Mathf.Clamp(deltaScale, deltaScaleMin, deltaScaleMax);

      // Find new hand positions. Unlike TwoPointNonUniformScale, this isn't a
      // simple scale of the positions. The invariant is that each hand is
      // a constant distance from the left/right end of the object.
      //
      // The easiest way of doing this is computing the change in position of
      // the left/right object endpoints and applying that to the left/right hand.
      // Since the object is assumed to be symmetric about the object's origin,
      // the left and right object endpoints move the same amount of deltaSize/2
      float deltaSize = sizeAlongAxis * (deltaScale - 1);
      Vector3 offset = vScaleAxis0 * deltaSize;
      // The t values of -.5f and .5f reflect the symmetry of the object;
      // if it were asymmetric they would be calculated.
      gripL0.translation += -.5f * offset;
      gripR0.translation +=  .5f * offset;
    }

    // Reuse TwoPoint to compute T and R.
    return TwoPointObjectTransformationNoScale(
        gripL0, gripR0, gripL1, gripR1, obj0,
        shrinkageSideIsLeft ? 0f : 1f);
  }

  /// Returns a perspective projection matrix whose viewpoint is
  /// potentially not centered on the projection rectangle.
  ///
  /// left, right, bottom, top are as measured at "dist".
  /// Normally, left < right, bottom < top, near < far.
  ///
  /// If left == -right and bottom == -top, this is a standard
  /// projection matrix with
  ///   hfov = 2 * atan2(right, dist)
  ///   vfov = 2 * atan2(top, dist)
  static public Matrix4x4 PerspectiveOffCenter(
      float left, float right, float bottom, float top,
      float dist,
      float near, float far) {
    float x = 2f * dist / (right - left);
    float y = 2f * dist / (top - bottom);
    float a = (right + left) / (right - left);
    float b = (top + bottom) / (top - bottom);
    float c = -(far + near) / (far - near);
    float d = -(2f * far * near) / (far - near);
    float e = -1f;
    Matrix4x4 m = new Matrix4x4();
    m[0, 0] = x;
    m[0, 1] = 0;
    m[0, 2] = a;
    m[0, 3] = 0;
    m[1, 0] = 0;
    m[1, 1] = y;
    m[1, 2] = b;
    m[1, 3] = 0;
    m[2, 0] = 0;
    m[2, 1] = 0;
    m[2, 2] = c;
    m[2, 3] = d;
    m[3, 0] = 0;
    m[3, 1] = 0;
    m[3, 2] = e;
    m[3, 3] = 0;
    return m;
  }

  /// Solve the quadratic equation.
  /// Returns false and NaNs if there are no (real) solutions.
  /// Otherwise, returns true.
  /// It's guaranteed that r0 <= r1.
  static public bool SolveQuadratic(float a, float b, float c, out float r0, out float r1) {
    // See https://people.csail.mit.edu/bkph/articles/Quadratics.pdf
    float discriminant = b*b - 4*a*c;
    if (discriminant < 0) {
      r0 = r1 = float.NaN;
      return false;
    }
    float q = -.5f * (b + Mathf.Sign(b) * Mathf.Sqrt(discriminant));
    float ra = q / a;
    float rb = c / q;
    if (ra < rb) {
      r0 = ra;
      r1 = rb;
    } else {
      r0 = rb;
      r1 = ra;
    }
    return true;
  }

  /// Returns true and t values of intersection, or false if there are
  /// no intersections.
  /// It's guaranteed t0 <= t1
  /// Ray direction does not need to be normalized.
  static public bool RaySphereIntersection(
      Vector3 rayOrigin, Vector3 rayDirection,
      Vector3 sphereCenter, float sphereRadius,
      out float t0, out float t1) {
    rayOrigin -= sphereCenter;
    return SolveQuadratic(
        Vector3.Dot(rayDirection, rayDirection),
        2 * Vector3.Dot(rayDirection, rayOrigin),
        Vector3.Dot(rayOrigin, rayOrigin) - sphereRadius * sphereRadius,
        out t0, out t1);
  }

  /// Transform a subset of an array of Vector3 elements as points.
  /// Pass:
  ///   m        - the transform to apply.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v3       - the array of vectors to transform
  public static void TransformVector3AsPoint(Matrix4x4 mat, int iVert, int iVertEnd,
                                             Vector3[] v3) {
#if USE_TILT_BRUSH_CPP
    unsafe {
      fixed (Vector3* v3Fixed = v3) {
        TiltBrushCpp.TransformVector3AsPoint(mat, iVert, iVertEnd, v3Fixed);
      }
    }
#else
    for (int i = iVert; i < iVertEnd; i++) {
      v3[i] = mat.MultiplyPoint(v3[i]);
    }
#endif
  }

  /// Transform a subset of an array of Vector3 elements as vectors.
  /// Pass:
  ///   m        - the transform to apply.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v3       - the array of vectors to transform
  public static void TransformVector3AsVector(Matrix4x4 mat, int iVert, int iVertEnd,
                                              Vector3[] v3) {
#if USE_TILT_BRUSH_CPP
    unsafe {
      fixed (Vector3* v3Fixed = v3) {
        TiltBrushCpp.TransformVector3AsVector(mat, iVert, iVertEnd, v3Fixed);
      }
    }
#else
    for (int i = iVert; i < iVertEnd; i++) {
      v3[i] = mat.MultiplyVector(v3[i]);
    }
#endif
  }

  /// Transform a subset of an array of Vector3 elements as z distances.
  /// Pass:
  ///   m        - the transform to apply.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v3       - the array of vectors to transform
  public static void TransformVector3AsZDistance(float scale, int iVert, int iVertEnd,
                                                 Vector3[] v3) {
#if USE_TILT_BRUSH_CPP
    unsafe {
      fixed (Vector3* v3Fixed = v3) {
        TiltBrushCpp.TransformVector3AsZDistance(scale, iVert, iVertEnd, v3Fixed);
      }
    }
#else
    for (int i = iVert; i < iVertEnd; i++) {
      v3[i] = new Vector3(v3[i].x, v3[i].y, scale * v3[i].z);
    }
#endif
  }

  /// Transform a subset of an array of Vector4 elements as points.
  /// Pass:
  ///   m        - the transform to apply.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v4       - the array of vectors to transform
  public static void TransformVector4AsPoint(Matrix4x4 mat, int iVert, int iVertEnd,
                                             Vector4[] v4) {
#if USE_TILT_BRUSH_CPP
    unsafe {
      fixed (Vector4* v4Fixed = v4) {
        TiltBrushCpp.TransformVector4AsPoint(mat, iVert, iVertEnd, v4Fixed);
      }
    }
#else
    for (int i = iVert; i < iVertEnd; i++) {
      Vector3 p = mat.MultiplyPoint(new Vector3(v4[i].x, v4[i].y, v4[i].z));
      v4[i] = new Vector4(p.x, p.y, p.z, v4[i].w);
    }
#endif
  }

  /// Transform a subset of a array of Vector4 elements as vectors.
  /// Pass:
  ///   m        - the transform to apply.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v4       - the array of vectors to transform
  public static void TransformVector4AsVector(Matrix4x4 mat, int iVert, int iVertEnd,
                                              Vector4[] v4) {
#if USE_TILT_BRUSH_CPP
    unsafe {
      fixed (Vector4* v4Fixed = v4) {
        TiltBrushCpp.TransformVector4AsVector(mat, iVert, iVertEnd, v4Fixed);
      }
    }
#else
    for (int i = iVert; i < iVertEnd; i++) {
      Vector3 vNew = mat.MultiplyVector(new Vector3(v4[i].x, v4[i].y, v4[i].z));
      v4[i] = new Vector4(vNew.x, vNew.y, vNew.z, v4[i].w);
    }
#endif
  }

  /// Transform a subset of a array of Vector4 elements as z distances.
  /// Pass:
  ///   m        - the transform to apply.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v4       - the array of vectors to transform
  public static void TransformVector4AsZDistance(float scale, int iVert, int iVertEnd,
                                                 Vector4[] v4) {
#if USE_TILT_BRUSH_CPP
    unsafe {
      fixed (Vector4* v4Fixed = v4) {
        TiltBrushCpp.TransformVector4AsZDistance(scale, iVert, iVertEnd, v4Fixed);
      }
    }
#else
    for (int i = iVert; i < iVertEnd; i++) {
      v4[i] = new Vector4(v4[i].x, v4[i].y, scale * v4[i].z, v4[i].w);
    }
#endif
  }

  /// Get the bounds for a transformed subset of an array of Vector3 point elements.
  /// Pass:
  ///   mat      - the transform to apply.
  ///   iVert    - start of verts to transform
  ///   iVertEnd - end (not inclusive) of verts to transform
  ///   v3       - the array of vectors to transform
  /// Output:
  ///   center   - the center of the bounds.
  ///   size     - the size of the bounds.
  public static void GetBoundsFor(Matrix4x4 mat, int iVert, int iVertEnd, Vector3[] v3,
                                  out Vector3 center, out Vector3 size) {
#if USE_TILT_BRUSH_CPP
    center = new Vector3();
    size = new Vector3();
    unsafe {
      fixed (Vector3* v3Fixed = v3)
      fixed (Vector3* centerPtr = &center)
      fixed (Vector3* sizePtr = &size) {
        TiltBrushCpp.GetBoundsFor(mat, iVert, iVertEnd, v3Fixed,
                                  centerPtr, sizePtr);
      }
    }
#else
    Vector3[] transformedVert;
    if (mat == Matrix4x4.identity) {
      transformedVert = v3;
    } else {
      transformedVert = new Vector3[v3.Length];
      v3.CopyTo(transformedVert, 0);
      MathUtils.TransformVector3AsPoint(mat, iVert, iVertEnd,
                                        transformedVert);
    }

    // We use floats here instead of Vector3 because it saves 8% of the time in this function.
    float minX = transformedVert[iVert].x;
    float maxX = minX;
    float minY = transformedVert[iVert].y;
    float maxY = minY;
    float minZ = transformedVert[iVert].z;
    float maxZ = minZ;
    for (int i = iVert + 1; i < iVertEnd; ++i) {
      if (minX > transformedVert[i].x) {
        minX = transformedVert[i].x;
      } else if (maxX < transformedVert[i].x) {
        maxX = transformedVert[i].x;
      }
      if (minY > transformedVert[i].y) {
        minY = transformedVert[i].y;
      } else if (maxY < transformedVert[i].y) {
        maxY = transformedVert[i].y;
      }
      if (minZ > transformedVert[i].z) {
        minZ = transformedVert[i].z;
      } else if (maxZ < transformedVert[i].z) {
        maxZ = transformedVert[i].z;
      }
    }

    center = new Vector3(0.5f * (minX + maxX),
                         0.5f * (minY + maxY),
                         0.5f * (minZ + maxZ));
    size = new Vector3(maxX - minX,
                       maxY - minY,
                       maxZ - minZ);
#endif
  }

  /// Returns the reciprocal of the radius of the circle that passes through the three points.
  /// See https://en.wikipedia.org/wiki/Menger_curvature
  public static float MengerCurvature(Vector3 v0, Vector3 v1, Vector3 v2) {
    // Side lengths; naming is arbitrary.
    float a = (v1 - v0).magnitude;
    float b = (v2 - v1).magnitude;
    float c = (v0 - v2).magnitude;

    // Sample implementations you find on the web use cross product here, but the web is wrong.
    // Heron's formula is more stable (and it's nicely symmetric).
    float areaTimes4; {
      float radicand = (( a +  b +  c) *
                        (-a +  b +  c) *
                        ( a + -b +  c) *
                        ( a +  b + -c));
      areaTimes4 = (radicand < 0) ? 0 : Mathf.Sqrt(radicand);
    }
    if (areaTimes4 == 0) {
      // Coincident points are considered the same as collinear points: zero curvature.
      return 0;
    }
    return areaTimes4 / (a*b*c);
  }

  /// Computes a new orientation frame using parallel transport.
  ///
  /// Pass:
  ///   tangent -
  ///     Tangent direction; usually points from the previous frame position to this one.
  ///     Must be unit-length.
  ///   previousFrame - The orientation of the previous frame; may be null.
  ///   bootstrapOrientation -
  ///     A hint, used when previousFrame == null. One of its axes will be used to calculate
  ///     one of the resulting frame's normals.
  ///
  /// Returns a new frame such that:
  ///   - Forward is aligned with tangent.
  ///   - Change in orientation is all swing, no twist (twist defined about the tangent).
  public static Quaternion ComputeMinimalRotationFrame(
      Vector3 tangent,
      Quaternion? previousFrame,
      Quaternion bootstrapOrientation) {
    Debug.Assert(Mathf.Abs(tangent.magnitude - 1) < 1e-4f);
    if (previousFrame == null) {
      // Create a new one. We need 2 vectors, so pick the 2nd from
      // the bootstrap orientation.
      Vector3 desiredUp = bootstrapOrientation * Vector3.up;
      if (Vector3.Dot(desiredUp, tangent) < .01f) {
        // Close to collinear; LookRotation will give a rubbish orientation
        desiredUp = bootstrapOrientation * Vector3.right;
      }
      return Quaternion.LookRotation(tangent, desiredUp);
    }

    Vector3 nPrevTangent = previousFrame.Value * Vector3.forward;
    Quaternion minimal = Quaternion.FromToRotation(nPrevTangent, tangent);
    return minimal * previousFrame.Value;
  }

  /// Returns a random int spanning the full range of ints.
  public static int RandomInt() {
    // It's a bit tricky to do with Random.Range -- do you pass (0x80000000, 0x7fffffff)?
    // or (0, 0xffffffff)? What about the fact that the upper end is exclusive?
    uint low = (uint)UnityEngine.Random.Range(0, 0x10000);
    uint high = (uint)UnityEngine.Random.Range(0, 0x10000);
    return unchecked((int)((high<<16) ^ low));
  }

  /// An error will be raised if a is less than int.MinValue
  public static int Min(long a, int b) {
    if (a < b) {
      checked { return (int) a; }
    } else {
      return b;
    }
  }

  // Performs a very quick-and-dirty linear resample
  public static System.Collections.Generic.IEnumerable<float>
      LinearResampleCurve(float[] samples, int newSamples) {
    // Should never happen! But just in case it does.
    if (samples == null || samples.Length == 0) {
      throw new ArgumentException("samples");
    }

    // Converts new sample index to a (floating-point) old sample index.
    // The fractional portion will be used for a lerp.
    double oldFromNew = (samples.Length - 1) / ((double)newSamples - 1);

    // The very last sample will try to do a Lerp(oldSamples[count-1], oldSamples[count], 0)
    // which reads off the end of the array (but throws away the value in the lerp).
    // Hack around it by treating that last sample specially.
    for (int idxNew = 0; idxNew < newSamples-1; ++idxNew) {
      double idxOldf = oldFromNew * idxNew;   // In general, this will be between two samples
      double idxOldFloor = Math.Floor(idxOldf);
      int idxOld = (int)idxOldFloor;
      double t = idxOldf - idxOldFloor;
      yield return Mathf.LerpUnclamped(samples[idxOld], samples[idxOld+1], (float)t);
    }
    yield return samples[samples.Length-1];
  }

}  // MathUtils
}  // TiltBrush
