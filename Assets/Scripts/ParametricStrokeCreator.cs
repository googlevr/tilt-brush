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
using UnityEngine;

using ControlPoint = TiltBrush.PointerManager.ControlPoint;
using QuaternionE = TiltBrush.QuaternionExtensions;

// Includes some stability-debugging calculations
#pragma warning disable 168

namespace TiltBrush {

/// Interface for defining parametric brush strokes via generation of ControlPoints.
///
/// This base API requires only two transforms from the caller:
/// - The pointer transform at the start of the stroke
/// - The current pointer transform
///
/// As such, it's probably useful only for simple parametric shapes like circles and
/// straight lines.
public abstract class ParametricStrokeCreator {
  public const float kSecondsToMs = 1000;

  protected TrTransform m_initialTransform;
  private Quaternion m_previousFinalRotation;
  protected double m_initialTime;

  public TrTransform InitialTransform {
    get { return m_initialTransform; }
  }

  /// initialTransform should be a local-space (ie canvas-space) pointer transform
  public ParametricStrokeCreator(TrTransform initialTransform) {
    m_initialTransform = initialTransform;
    m_initialTime = App.Instance.CurrentSketchTime;
    m_previousFinalRotation = initialTransform.rotation;
  }

  /// Returns the control points.
  /// finalTransform is local-space (ie canvas-space).
  public IEnumerable<ControlPoint> GetPoints(TrTransform finalTransform) {
    // Do some conditioning on the input transform; don't let it have any
    // discontinuities. Rotation relative to previous finalTransform should be < 180
    // TODO: maybe some subclasses might not want this conditioning?
    if (Quaternion.Dot(finalTransform.rotation, m_previousFinalRotation) < 0) {
      finalTransform.rotation = finalTransform.rotation.Negated();
    }
    m_previousFinalRotation = finalTransform.rotation;
    return DoGetPoints(finalTransform);
  }

  /// To be implemented by subclass.
  /// Initial and final rotations are guaranteed to be at most 180 degrees apart
  /// finalTransform is local-space (ie canvas-space).
  protected abstract IEnumerable<ControlPoint> DoGetPoints(TrTransform finalTransform);

  /// For subclasses that want control over the brush size when active.
  /// Size is in room space.
  virtual public float ProcessBrushSize(float currentBrushSize_RS) {
    return currentBrushSize_RS;
  }
}

/// Create a sphere
public class SphereCreator : ParametricStrokeCreator {
  private const float kMinimumBrushSize_RS = 0.3f;
  private readonly float m_BrushSize_RS;
  private readonly float m_BrushSize_CS;

  override public float ProcessBrushSize(float currentBrushSize_RS) {
    return m_BrushSize_RS;
  }

  public SphereCreator(TrTransform initialTransform, float brushSize_RS, float canvasScale) :
      base(initialTransform) {
    float canvasFromRoom = 1.0f / canvasScale;
    m_BrushSize_RS = Mathf.Max(brushSize_RS, kMinimumBrushSize_RS);
    m_BrushSize_CS = canvasFromRoom * m_BrushSize_RS;
  }

  protected override IEnumerable<ControlPoint> DoGetPoints(TrTransform finalTransform) {
    float radius = (finalTransform.translation - m_initialTransform.translation).magnitude;
    double time0 = m_initialTime;
    double time1 = App.Instance.CurrentSketchTime;

    // The number of times that theta wraps around.
    float loops; {
      // Distance is the length of a longitude line, which is half a circumference.
      float distance = radius * Mathf.PI;
      loops = distance / m_BrushSize_CS;
    }

    // "steal" some of the pretty rotation on the butt end and put it on the front end.
    float thetaOffset = -(loops * .5f * 2 * Mathf.PI);

    int pointTotal; {
      pointTotal = (int)Mathf.Max(loops * 20, 2f);
      // Keeping this removes a very slight objectionable artifact, likely the result of
      // the thetaOffset stealing.
      if (pointTotal % 2 == 1) { pointTotal += 1; }
    }

    TrTransform spherePose; {
      var sphereForward = (finalTransform.translation - m_initialTransform.translation).normalized;
      var sphereRot = Quaternion.FromToRotation(Vector3.forward, sphereForward);
      spherePose = TrTransform.TR(m_initialTransform.translation, sphereRot);
    }

    for (int k = 0; k < pointTotal; k++) {
      float t = (float)k / (pointTotal - 1); // parametrization variable
      float theta = t * loops * 2 * Mathf.PI + thetaOffset;
      float phi = t * Mathf.PI;

      TrTransform cpPose; {
        Vector3 localPos = radius * new Vector3(
          Mathf.Cos(theta) * Mathf.Sin(phi),
          Mathf.Sin(theta) * Mathf.Sin(phi),
          Mathf.Cos(phi));
        Quaternion localRot =
            QuaternionE.AngleAxisRad(theta, Vector3.forward) *  // latitudinal motion
            QuaternionE.AngleAxisRad(phi, Vector3.up);          // longitudinal motion
        cpPose = spherePose * TrTransform.TR(localPos, localRot);
      }

      yield return new ControlPoint {
        m_Pos = cpPose.translation,
        m_Orient = cpPose.rotation,
        m_Pressure = 1,
        m_TimestampMs = (uint)(Mathf.Lerp((float)time0, (float)time1, t) * kSecondsToMs)
      };
    }
  }
}

/// Create a line
public class LineCreator : ParametricStrokeCreator {
  protected bool m_flat;
  private Quaternion m_qPreviousInput;
  protected Quaternion m_qPreviousOutput;

  public LineCreator(TrTransform initialTransform, bool flat=false)
    : base(initialTransform) {
    m_flat = flat;
    m_qPreviousInput = initialTransform.rotation;
    m_qPreviousOutput = initialTransform.rotation;
  }

  protected override IEnumerable<ControlPoint> DoGetPoints(TrTransform finalTransform) {
    double t0 = m_initialTime;
    double t1 = App.Instance.CurrentSketchTime;
    var xf0 = m_initialTransform;
    var xf1 = finalTransform;

    // Adjust the final orientation by the delta since the last update.
    Quaternion qNewRotation = finalTransform.rotation * Quaternion.Inverse(m_qPreviousInput) *
        m_qPreviousOutput;

    // Make sure the final orientation is perpendicular to the axis of the stroke.
    Vector3 vAxis = (xf1.translation - xf0.translation);
    if (vAxis == Vector3.zero) {
      // Arbitrary axis for avoid division by zero
      // Axis is always zero on first frame and non-zero in subsequent ones
      vAxis = new Vector3(0, 0.0001f, 0);
    }
    float vAxisMag = vAxis.magnitude;
    vAxis /= vAxisMag;
    float convergeFactor = 1 - 1 / (1 + vAxisMag);
    Vector3 vStrokeNormal = qNewRotation * Vector3.forward;
    Vector3 vStrokeNormalNoAxis =
        (vStrokeNormal - Vector3.Dot(vStrokeNormal, vAxis) * vAxis).normalized;
    qNewRotation = Quaternion.Slerp(
        qNewRotation,
        Quaternion.FromToRotation(vStrokeNormal, vStrokeNormalNoAxis) * qNewRotation,
        convergeFactor);

    // Converge final orientation towards the final transform.
    Vector3 vOldStrokeNormal = qNewRotation * Vector3.forward;
    Vector3 vNewStrokeNormal = finalTransform.rotation * Vector3.forward;
    Vector3 vNewStrokeNormalNoAxis =
        (vNewStrokeNormal - Vector3.Dot(vNewStrokeNormal, vAxis) * vAxis).normalized;
    if (Vector3.Dot(vOldStrokeNormal, vNewStrokeNormal) < 0) {
      vNewStrokeNormalNoAxis *= -1;
    }
    convergeFactor *= 1 - Mathf.Abs(Vector3.Dot(vNewStrokeNormal, vAxis));
    qNewRotation = Quaternion.Slerp(
        qNewRotation,
        Quaternion.FromToRotation(vNewStrokeNormal, vNewStrokeNormalNoAxis) * finalTransform.rotation,
        convergeFactor);

    m_qPreviousOutput = xf1.rotation = qNewRotation;
    m_qPreviousInput = finalTransform.rotation;

    if (m_flat) {
      xf0.rotation = xf1.rotation;
    }


    // TODO: adjust control point density based on torsion and aspect ratio?
    // TODO: second and penultimate control points should be near the start/end
    // (To get some tesselation on end caps)

    // TODO: replace with something more reasonable. Must be >= 2
    int n = 30;

    for (int i = 0; i <= n; ++i) {
      float t = (float)i / n;
      yield return new ControlPoint {
        m_Pos      = Vector3.Lerp(xf0.translation, xf1.translation, t),
        m_Orient   = Quaternion.Slerp(xf0.rotation, xf1.rotation, t),
        m_Pressure = 1f,
        m_TimestampMs = (uint)(Mathf.Lerp((float)t0, (float)t1, t) * kSecondsToMs)
      };
    }
  }
}


/// Create a circle
public class CircleCreator : ParametricStrokeCreator {
  protected Vector3 m_vPreferredTangent;
#if DEBUG_TANGENT
  ComputeTangentState? m_oldState;
#endif

  public CircleCreator(TrTransform initialTransform)
    : base(initialTransform) {
    m_vPreferredTangent = Vector3.zero;
  }

  // Returns v, flipped so it points in the same direction as desired.
  // If desired == 0, returns v.
  private static Vector3 InDirectionOf(Vector3 desired, Vector3 v) {
    return Vector3.Dot(v, desired) >= 0 ? v : -v;
  }

  // Returns component of v that is perpendicular to nPerp.
  // nPerp must be unit length.
  private static Vector3 PerpendicularPart(Vector3 nPerp, Vector3 v) {
    return v - Vector3.Dot(nPerp, v) * nPerp;
  }

  struct ComputeTangentState {
    public Vector3 nRadius;
    public Quaternion rotation;
    public Vector3 preferred;
  }
  Vector3 ComputeTangent(ComputeTangentState s) {
    // TODO: this is not production ready; it has a difficult-to-remove
    // discontinuity that may be the result of its statefulness. In fact, I
    // think BaseBrushScript.ComputeSurfaceFrameNew() also suffers from a similar
    // discontinuity.

    // One of these may be near zero length, but not both of them at the same time.
    // Both may be 1-length at the same time. In that case, we prefer to use #1.
    Vector3 vTangent1 = s.rotation * Vector3.right;
    vTangent1 = InDirectionOf(s.preferred, PerpendicularPart(s.nRadius, vTangent1));
    var stable1 = vTangent1.magnitude;
    Vector3 vTangent2 = s.rotation * Vector3.up;
    vTangent2 = InDirectionOf(s.preferred, PerpendicularPart(s.nRadius, vTangent2));
    var stable2 = vTangent2.magnitude;
    // Scale this down the more stable vTangent1 is
    vTangent2 *= Mathf.Sqrt(Mathf.Max(0, 1f - vTangent1.sqrMagnitude));
    var stable3 = vTangent2.magnitude;
    return (vTangent1 + vTangent2).normalized;
  }

  protected override IEnumerable<ControlPoint> DoGetPoints(TrTransform finalTransform) {
    double t0 = m_initialTime;
    double t1 = App.Instance.CurrentSketchTime;

    Vector3 center = m_initialTransform.translation;
    Vector3 nRadius = finalTransform.translation - center;
    float fRadius = nRadius.magnitude;
    nRadius /= fRadius;

    // Degenerate circle -- turn it into a line
    if (fRadius < 1e-5f) {
      yield return new ControlPoint {
        m_Pos      = m_initialTransform.translation,
        m_Orient   = m_initialTransform.rotation,
        m_Pressure = 1f,
        m_TimestampMs = (uint)(t0 * kSecondsToMs)
      };
      yield return new ControlPoint {
        m_Pos      = finalTransform.translation,
        m_Orient   = finalTransform.rotation,
        m_Pressure = 1f,
        m_TimestampMs = (uint)(t1 * kSecondsToMs)
      };
      yield break;
    }

    // Tangent must be perpendicular to nRadius

    var thisState = new ComputeTangentState {
      nRadius   = nRadius,
      rotation  = finalTransform.rotation,
      preferred = m_vPreferredTangent
    };
    Vector3 nTangent = ComputeTangent(thisState);
    m_vPreferredTangent = nTangent;

#if DEBUG_TANGENT
    if (m_oldState != null) {
      Vector3 nOldTangent = ComputeTangent(m_oldState.Value);
      if (fRadius > .5f && Vector3.Dot(nOldTangent, nTangent) < .966f) {
        int nn = 20;
        for (int i = 0; i < nn; ++i) {
          Vector3 v0 = ComputeTangent(thisState);
          Vector3 v1 = ComputeTangent(m_oldState.Value);
        }
      }
    }
    m_oldState = thisState;
#endif


    // Axis is perpendicular to tangent and radius
    // TODO: experiment with removing this restriction?
    Vector3 nAxis = Vector3.Cross(nTangent, nRadius).normalized;

    TrTransform xf0 = finalTransform;

    // TODO: adjust control point density

    int n = 30;  // number of points; must be >= 2
    for (int i = 0; i <= n; ++i) {
      float t = (float)i / n;

      Quaternion rot = Quaternion.AngleAxis(360 * t, nAxis);
      TrTransform delta = TrTransform.R(rot).TransformBy(TrTransform.T(center));
      TrTransform xf = delta * xf0;

      yield return new ControlPoint {
        m_Pos      = xf.translation,
        m_Orient   = xf.rotation,
        m_Pressure = 1f,
        m_TimestampMs = (uint)(Mathf.Lerp((float)t0, (float)t1, t) * 1000)
      };
    }

#if DEBUG_TANGENT
    var start = finalTransform.translation;
    foreach (var val in DrawLine(start, nTangent, 5)) yield return val;
    foreach (var val in DrawLine(start, nAxis, 7)) yield return val;
#endif
  }

#if DEBUG_TANGENT
  protected IEnumerable<ControlPoint> DrawLine(Vector3 start, Vector3 nDir, float len) {
    ControlPoint cp = new ControlPoint {
      m_Orient = m_initialTransform.rotation,
      m_Pressure = 1f,
      m_TimestampMs = (uint)(App.Instance.CurrentSketchTime * 1000),
    };

    int n = 3;
    for (int i = 0; i <= n; ++i) {
      cp.m_Pos = start + nDir * ((len * i) / n);
      yield return cp;
    }
    cp.m_Pos = start;
    yield return cp;
  }
#endif
}

} // namespace TiltBrush
