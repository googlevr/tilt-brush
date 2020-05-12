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
using NUnit.Framework;
using TiltBrush;

internal class MathTestUtils {
  public const float RELEPS = 1e-4f;
  public const float ABSEPS = 1e-4f;

  public class NotAlmostEqual : System.Exception {
    public string m_failure;
    public string m_path;
    public NotAlmostEqual(string failure, string label=null) : base() {
      m_failure = failure;
      m_path = label;
    }

    public void PrependToPath(object txt) {
      if (txt != null) {
        if (m_path == null) {
          m_path = txt.ToString();
        } else {
          m_path = string.Format("{0}.{1}", txt, m_path);
        }
      }
    }

    public override string Message {
      get {
        return string.Format("{0}: {1}", m_path, m_failure);
      }
    }
  }

  //
  // Float
  //

  public static bool AlmostEqual(float lhs, float rhs, float abseps, float releps) {
    float absDiff = Mathf.Abs(rhs - lhs);
    if (absDiff < abseps) {
      return true;
    }

    float divisor = (Mathf.Abs(rhs) > Mathf.Abs(lhs)) ? rhs : lhs;
    float relDiff = Mathf.Abs((rhs - lhs) / divisor);
    if (relDiff < releps) {
      return true;
    }
    return false;
  }

  /// lhs is the value to check
  /// rhs is the correct value
  public static void CheckAlmostEqual(float lhs, float rhs, float abseps, float releps,
                                      string label=null) {
    float absDiff = Mathf.Abs(rhs - lhs);
    if (absDiff < abseps) {
      return;
    }

    float divisor = rhs;
    float relDiff = Mathf.Abs((rhs - lhs) / divisor);
    if (relDiff < releps) {
      return;
    }
    string complaint = string.Format(
        "rel {0} > {1}, abs {2} > {3}",
        relDiff, releps, absDiff, abseps);
    throw new NotAlmostEqual(complaint, label);
  }

  public static void AssertAlmostEqual(float lhs, float rhs,
                                       float abseps=ABSEPS, float releps=RELEPS) {
    if (! AlmostEqual(lhs, rhs, abseps, releps)) {
      Assert.Fail("{0} !~ {1}", lhs, rhs);
    }
  }

  //
  // Matrix4x4
  //

  public static bool AlmostEqual(Matrix4x4 lhs, Matrix4x4 rhs, float abseps, float releps) {
    for (int r = 0; r < 4; ++r)
      for (int c = 0; c < 4; ++c) {
        float lf = lhs[r,c];
        float rf = rhs[r,c];
        if (! AlmostEqual(lf, rf, abseps, releps)) {
          return false;
        }
      }
    return true;
  }

  public static void CheckAlmostEqual(
      Matrix4x4 lhs, Matrix4x4 rhs, float abseps, float releps, string label=null) {
    for (int r = 0; r < 4; ++r) {
      for (int c = 0; c < 4; ++c) {
        try {
          CheckAlmostEqual(lhs[r,c], rhs[r,c], abseps, releps);
        } catch (NotAlmostEqual e) {
          e.PrependToPath(string.Format("[{0},{1}]", r, c));
          e.PrependToPath(label);
          throw;
        }
      }
    }
  }

  public static void AssertAlmostEqual(Matrix4x4 lhs, Matrix4x4 rhs,
                                       float abseps=ABSEPS, float releps=RELEPS) {
    if (! AlmostEqual(lhs, rhs, abseps, releps)) {
      Assert.Fail("{0} !~ {1}", lhs, rhs);
    }
  }

  //
  // Vector3
  //

  public static bool AlmostEqual(Vector3 lhs, Vector3 rhs,
                                 float abseps, float releps) {
    return (AlmostEqual(lhs.x, rhs.x, abseps, releps) &&
            AlmostEqual(lhs.y, rhs.y, abseps, releps) &&
            AlmostEqual(lhs.z, rhs.z, abseps, releps));
  }

  public static void CheckAlmostEqual(Vector3 lhs, Vector3 rhs,
                                      float abseps, float releps, string label=null) {
    try {
      CheckAlmostEqual(lhs.x, rhs.x, abseps, releps, "x");
      CheckAlmostEqual(lhs.y, rhs.y, abseps, releps, "y");
      CheckAlmostEqual(lhs.z, rhs.z, abseps, releps, "z");
    } catch (NotAlmostEqual e) {
      e.PrependToPath(label);
      throw;
    }
  }

  public static void AssertAlmostEqual(Vector3 lhs, Vector3 rhs,
                                       float abseps=ABSEPS, float releps=RELEPS,
                                       object message=null) {
    try {
      CheckAlmostEqual(lhs, rhs, abseps, releps);
    } catch (NotAlmostEqual e) {
      Assert.Fail("({0} {1} {2}) !~ ({3} {4} {5}) {7} {6}",
                  lhs.x, lhs.y, lhs.z,
                  rhs.x, rhs.y, rhs.z,
                  e.Message, message ?? "");
    }
  }

  //
  // Vector4
  //
  // Unfortunately, Unity has an implicit Vector4 -> Vector3 conversion so we have to create
  // explicitly different named functions here.

  public static bool AlmostEqualV4(Vector4 lhs, Vector4 rhs,
                                   float abseps, float releps) {
    return (AlmostEqual(lhs.x, rhs.x, abseps, releps) &&
            AlmostEqual(lhs.y, rhs.y, abseps, releps) &&
            AlmostEqual(lhs.z, rhs.z, abseps, releps) &&
            AlmostEqual(lhs.w, rhs.w, abseps, releps));
  }

  public static void CheckAlmostEqualV4(Vector4 lhs, Vector4 rhs,
                                        float abseps, float releps, string label=null) {
    try {
      CheckAlmostEqual(lhs.x, rhs.x, abseps, releps, "x");
      CheckAlmostEqual(lhs.y, rhs.y, abseps, releps, "y");
      CheckAlmostEqual(lhs.z, rhs.z, abseps, releps, "z");
      CheckAlmostEqual(lhs.w, rhs.w, abseps, releps, "w");
    } catch (NotAlmostEqual e) {
      e.PrependToPath(label);
      throw;
    }
  }

  public static void AssertAlmostEqualV4(Vector4 lhs, Vector4 rhs,
                                         float abseps=ABSEPS, float releps=RELEPS,
                                         object message=null) {
    try {
      CheckAlmostEqual(lhs, rhs, abseps, releps);
    } catch (NotAlmostEqual e) {
      Assert.Fail("({0} {1} {2} {3}) !~ ({4} {5} {6} {7}) {9} {8}",
                  lhs.x, lhs.y, lhs.z, lhs.w,
                  rhs.x, rhs.y, rhs.z, rhs.w,
                  e.Message, message ?? "");
    }
  }

  //
  // Quaternion
  //

  public static bool AlmostEqual(Quaternion lhs, Quaternion rhs,
                                 float abseps, float releps, bool allowFlip) {
    if (allowFlip && Quaternion.Dot(lhs, rhs) < 0) {
      rhs.x *= -1; rhs.y *= -1;
      rhs.z *= -1; rhs.w *= -1;
    }
    return (AlmostEqual(lhs.x, rhs.x, abseps, releps) &&
            AlmostEqual(lhs.y, rhs.y, abseps, releps) &&
            AlmostEqual(lhs.z, rhs.z, abseps, releps) &&
            AlmostEqual(lhs.w, rhs.w, abseps, releps));
  }

  public static void CheckAlmostEqual(
      Quaternion lhs, Quaternion rhs,
      float abseps, float releps,
      bool allowFlip,
      string label=null) {
    if (allowFlip && Quaternion.Dot(lhs, rhs) < 0) {
      rhs.x *= -1; rhs.y *= -1;
      rhs.z *= -1; rhs.w *= -1;
    }
    try {
      CheckAlmostEqual(lhs.x, rhs.x, abseps, releps, "x");
      CheckAlmostEqual(lhs.y, rhs.y, abseps, releps, "y");
      CheckAlmostEqual(lhs.z, rhs.z, abseps, releps, "z");
      CheckAlmostEqual(lhs.w, rhs.w, abseps, releps, "z");
    } catch (NotAlmostEqual e) {
      e.PrependToPath(label);
      throw;
    }
  }

  public static void AssertAlmostEqual(Quaternion lhs, float rhsAngle, Vector3 rhsAxis,
                                       float abseps=ABSEPS, float releps=RELEPS,
                                       bool allowFlip=false) {
    float lhsAngle;
    Vector3 lhsAxis;
    lhs.ToAngleAxis(out lhsAngle, out lhsAxis);
    if (allowFlip && Vector3.Dot(lhsAxis, rhsAxis) < 0) {
      lhsAngle *= -1;
      lhsAxis *= -1;
    }

    try {
      if (rhsAxis != Vector3.zero) {
        CheckAlmostEqual(lhsAxis, rhsAxis, abseps, releps, "axis");
      }
      CheckAlmostEqual(lhsAngle, rhsAngle, abseps, releps, "angle");
    } catch (NotAlmostEqual e) {
      Assert.Fail("aa({0}  {1} {2} {3}) !~ aa({4}  {5} {6} {7}) {8}",
                  lhsAngle, lhsAxis.x, lhsAxis.y, lhsAxis.z,
                  rhsAngle, rhsAxis.x, rhsAxis.y, rhsAxis.z,
                  e.Message);
    }
  }

  public static void AssertAlmostEqual(Quaternion lhs, Quaternion rhs,
                                       float abseps=ABSEPS, float releps=RELEPS,
                                       bool allowFlip=false) {
    try {
      CheckAlmostEqual(lhs, rhs, abseps, releps, allowFlip);
    } catch (NotAlmostEqual e) {
      Assert.Fail("q({0} {1} {2}  {3}) !~ q({4} {5} {6}  {7}) {8}",
                  lhs.x, lhs.y, lhs.z, lhs.w,
                  rhs.x, rhs.y, rhs.z, rhs.w,
                  e.Message);
    }
  }

  //
  // TrTransform
  //

  public static bool AlmostEqual(TrTransform lhs, TrTransform rhs,
                                 float abseps, float releps,
                                 bool allowFlip) {
    return (AlmostEqual(lhs.rotation, rhs.rotation, abseps, releps, allowFlip) &&
            AlmostEqual(lhs.translation, rhs.translation, abseps, releps) &&
            AlmostEqual(lhs.scale, rhs.scale, abseps, releps));
  }

  public static void CheckAlmostEqual(
      TrTransform lhs, TrTransform rhs,
      float abseps, float releps,
      bool allowFlip) {
    CheckAlmostEqual(lhs.translation, rhs.translation, abseps, releps, "t");
    CheckAlmostEqual(lhs.rotation, rhs.rotation, abseps, releps, allowFlip, "r");
    CheckAlmostEqual(lhs.scale, rhs.scale, abseps, releps, "s");
  }

  public static void AssertAlmostEqual(TrTransform lhs, TrTransform rhs,
                                       float abseps=ABSEPS, float releps=RELEPS,
                                       bool allowFlip=false) {
    try {
      CheckAlmostEqual(lhs, rhs, abseps, releps, allowFlip);
    } catch (NotAlmostEqual e) {
      Assert.Fail("{0}\n  !~\n{1}\n{2}", lhs, rhs, e.Message);
    }
  }

  //
  // Plane
  //

  // Ignores the fact that planes are equivalent up to a constant factor,
  // because Unity planes are oriented.
  public static void AssertAlmostEqual(
      Plane lhs, Plane rhs, float abseps=ABSEPS, float releps=RELEPS) {
    try {
      CheckAlmostEqual(lhs.normal, rhs.normal, abseps, releps);
      CheckAlmostEqual(lhs.distance, rhs.distance, abseps, releps);
    } catch (NotAlmostEqual) {
      Assert.Fail("{0} {1}  !~  {2} {3}",
                  lhs.normal, lhs.distance, rhs.normal, rhs.distance);
    }
  }

  public static TrTransform RandomTr(bool useScale=true) {
    float scale;
    if (useScale) {
      scale = Random.Range(2f, 100f);
      if (Random.value < .5f) {
        scale = 1 / scale;
      }
    } else {
      scale = 1;
    }

    return TrTransform.TRS(Random.insideUnitSphere * 10,
                           Random.rotationUniform,
                           scale);
  }

  public static List<Color32> RandomColor32List(int size) {
    var list = new List<Color32>();
    for (int i = 0; i < size; i++) {
      byte r = (byte)Random.Range(0, 256);
      byte g = (byte)Random.Range(0, 256);
      byte b = (byte)Random.Range(0, 256);
      byte a = (byte)Random.Range(0, 256);
      list.Add(new Color32(r, g, b, a));
    }
    return list;
  }

  public static List<int> RandomIntList(int size, int min, int max) {
    var list = new List<int>();
    for (int i = 0; i < size; i++) {
      list.Add(Random.Range(min, max));
    }
    return list;
  }

  public static List<Vector2> RandomVector2List(int size) {
    var list = new List<Vector2>();
    for (int i = 0; i < size; i++) {
      list.Add(new Vector2(Random.Range(-100f, 100f),
                           Random.Range(-100f, 100f)));
    }
    return list;
  }

  public static List<Vector3> RandomVector3List(int size) {
    var list = new List<Vector3>();
    for (int i = 0; i < size; i++) {
      list.Add(new Vector3(Random.Range(-100f, 100f),
                           Random.Range(-100f, 100f),
                           Random.Range(-100f, 100f)));
    }
    return list;
  }

  public static List<Vector4> RandomVector4List(int size) {
    var list = new List<Vector4>();
    for (int i = 0; i < size; i++) {
      list.Add(new Vector4(Random.Range(-100f, 100f),
                           Random.Range(-100f, 100f),
                           Random.Range(-100f, 100f),
                           Random.Range(-100f, 100f)));
    }
    return list;
  }

  public static Plane RandomPlane() {
    return new Plane(Random.onUnitSphere, Random.Range(-5, 5));
  }
}
