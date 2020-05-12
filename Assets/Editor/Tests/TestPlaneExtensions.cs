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

using Mtu = MathTestUtils;

namespace TiltBrush {

internal class TestPlaneExtensions {
  static void TestReflectHelper(Plane plane, Vector3 v) {
    // There are 2 invariants for a point and its mirror:
    // - They have the same closest point on plane
    // - They have opposite signed distance to plane
    {
      Vector3 p2 = plane.ReflectPoint(v);
      Mtu.AssertAlmostEqual(plane.ClosestPointOnPlane(v), plane.ClosestPointOnPlane(p2));
      Mtu.AssertAlmostEqual(plane.GetDistanceToPoint(v), -plane.GetDistanceToPoint(p2));
    }

    // The invariants for reflecting a vector are similar; but they apply
    // to the plane with distance=0.
    {
      Vector3 v2 = plane.ReflectVector(v);
      Plane pO = plane;
      pO.distance = 0;
      Mtu.AssertAlmostEqual(pO.ClosestPointOnPlane(v), pO.ClosestPointOnPlane(v2));
      Mtu.AssertAlmostEqual(pO.GetDistanceToPoint(v), -pO.GetDistanceToPoint(v2));
    }
  }

  [Test]
  public void TestReflectPointSimple() {
    var p = new Plane(new Vector3(1, 0, 0), -1);  // plane is at x=1
    var v = new Vector3(3, 0, 0);
    var v2 = p.ReflectPoint(v);
    Assert.AreEqual(new Vector3(-1, 0, 0), v2);
    TestReflectHelper(p, v);
  }

  [Test]
  public void TestReflectRandom() {
    for (int i = 0; i < 10; ++i) {
      Plane p = Mtu.RandomPlane();
      Vector3 v = Random.onUnitSphere * Random.Range(-10, 10);
      TestReflectHelper(p, v);
    }
  }

  static Vector3[] kBasisVectors = { Vector3.forward, Vector3.right, Vector3.up };
  static Vector3[] kTetrahedron = { Vector3.forward, Vector3.right, Vector3.up, Vector3.zero };

  // Checks invariants for the return value of Plane.ToTrTransform()
  void CheckToTrTransformInvariants(Plane plane) {
    var reflect = plane.ToTrTransform();

    // Basis vectors should map properly. Actually, we could use any 3 independent vectors.
    foreach (var basis in kBasisVectors) {
      Mtu.AssertAlmostEqual(plane.ReflectVector(basis), reflect.MultiplyVector(basis));
    }

    // Det should be -1
    Assert.AreEqual(-1, reflect.scale);

    // Points on plane should not move. We need to check at least 3 distinct points on the plane.
    // Any non-degenerate tetrahedron will project to at least 3 distinct points.
    foreach (var basis in kTetrahedron) {
      Vector3 v = plane.ClosestPointOnPlane(basis);
      Mtu.AssertAlmostEqual(v, reflect.MultiplyPoint(v));
    }

    // Points not on the plane should move.
    // Specifically, they should be reflected.
    {
      Vector3 notOnPlane = plane.ClosestPointOnPlane(Vector3.zero) + plane.normal;
      Assert.IsTrue(! Mtu.AlmostEqual(
          notOnPlane, reflect.MultiplyPoint(notOnPlane), Mtu.ABSEPS, Mtu.RELEPS));
      Mtu.AssertAlmostEqual(plane.ReflectPoint(notOnPlane), reflect.MultiplyPoint(notOnPlane));
    }
  }

  // Tests conversion of Plane -> Transform.
  [Test]
  public void TestToTrTransform() {
    for (int i = 0; i < 10; ++i) {
      Plane plane = Mtu.RandomPlane();
      CheckToTrTransformInvariants(plane);
    }
  }

  // Checks invariants for the return value of Plane.ReflectPose().
  void CheckReflectPoseInvariants(Plane plane, TrTransform pose0) {
    // Object -> world
    var pose1 = plane.ReflectPoseKeepHandedness(pose0);

    // Object x axis should be preserved.
    // Object y and z axes should be reflected. This can actually be checked for any 2 vectors
    // that are orthogonal to the preserved axis.
    Mtu.AssertAlmostEqual(-plane.ReflectVector(pose0.right),   pose1.right);
    Mtu.AssertAlmostEqual( plane.ReflectVector(pose0.forward), pose1.forward);
    Mtu.AssertAlmostEqual( plane.ReflectVector(pose0.up),      pose1.up);
    // pose.translation should be reflected.
    Mtu.AssertAlmostEqual(plane.ReflectPoint(pose0.translation), pose1.translation);

    // Handedness should not change (sign should be the same).
    // Magnitude should stay the same too.
    Assert.AreEqual(pose0.scale, pose1.scale);
  }

  [Test]
  public void TestReflectPoseKeepHandedness() {
    for (int i = 0; i < 30; ++i) {
      Plane plane = Mtu.RandomPlane();
      TrTransform pose = Mtu.RandomTr();
      CheckReflectPoseInvariants(plane, pose);
    }
  }
}

}
