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
using System.Collections;
using System.Threading.Tasks;

using UnityEngine;
using NUnit.Framework;
using UnityEngine.TestTools;

using static TiltBrush.AsyncTestUtils;

namespace TiltBrush {

internal class TestMisc {
  [Test]
  public void TestStreamlineStackTrace() {
    string input = "UnityEngine.Debug:Assert(Boolean, String)\r\nTiltBrush.SketchMemoryScript:SanityCheckGeometryGeneration(MemoryBrushStroke) (at Assets/Scripts/SketchMemoryScript.cs:709)";
    string output = ExceptionRenderScript.StreamlineStackTrace(input);
    Assert.AreEqual(output, "Debug.Assert \r\nSketchMemoryScript.SanityCheckGeometryGeneration 709");
  }

  // Embedded
  [TestCase(.1f,.1f,.1f,        3,.1f,.1f,     1,  0,  0)]
  // Face tests
  [TestCase( 4,  1,  1,         3,  1,  1,     1,  0,  0)]  // +x face
  [TestCase(-4,  1,  1,        -3,  1,  1,    -1,  0,  0)]  // -x face
  [TestCase( 1,  8,  2,         1,  4,  2,     0,  1,  0)]  // +y face
  [TestCase( 1, -8,  2,         1, -4,  2,     0, -1,  0)]  // -y face
  [TestCase(-1, -2,  8,        -1, -2,  5,     0,  0,  1)]  // +z face
  [TestCase(-1, -2, -8,        -1, -2, -5,     0,  0, -1)]  // -z face
  // Edge tests
  [TestCase( 8,  8,  2,         3,  4,  2,  0,0,0)]  // +x+y edge
  [TestCase(-8,  8,  2,        -3,  4,  2,  0,0,0)]  // -x+y edge
  // Vert tests
  [TestCase( 8, -8, -8,         3, -4, -5,  0,0,0)]  // +x-y-z vert
  public void TestClosestPointOnBox(
      float px, float py, float pz,
      float spx, float spy, float spz,
      float nx, float ny, float nz) {
    var pos = new Vector3(px, py, pz);
    var halfWidth = new Vector3(3, 4, 5);
    var expectedSurfacePos = new Vector3(spx, spy, spz);
    var expectedSurfaceNorm = new Vector3(nx, ny, nz);

    Vector3 surfacePos, surfaceNorm;
    CubeStencil.FindClosestPointOnBoxSurface(pos, halfWidth,
        out surfacePos, out surfaceNorm);
    MathTestUtils.AssertAlmostEqual(expectedSurfacePos, surfacePos);
    if (expectedSurfaceNorm != Vector3.zero) {
      MathTestUtils.AssertAlmostEqual(expectedSurfaceNorm, surfaceNorm);
    } else {
      // Test case wants us to calculate it from scratch
      Vector3 diff = pos - expectedSurfacePos;
      if (diff != Vector3.zero) {
        MathTestUtils.AssertAlmostEqual(diff.normalized, surfaceNorm);
      }
    }
  }

  [Test]
  public void TestClosestPointOnBoxEdgeCases() {
    var halfWidth = new Vector3(3, 4, 5);
    // Try all permutations of points directly on verts, faces, edges
    for (int xsign=-1; xsign<=1; ++xsign)
    for (int ysign=-1; ysign<=1; ++ysign)
    for (int zsign=-1; zsign<=1; ++zsign) {
      int numFaces = Mathf.Abs(xsign) + Mathf.Abs(ysign) + Mathf.Abs(zsign);
      // Only care about running tests where the point is on 2 or 3 faces (ie edge, or vert)
      if (numFaces <= 1) { continue; }
      var pos = new Vector3(xsign * halfWidth.x,
                            ysign * halfWidth.y,
                            zsign * halfWidth.z);

      Vector3 surfacePos, surfaceNorm;
      CubeStencil.FindClosestPointOnBoxSurface(pos, halfWidth,
          out surfacePos, out surfaceNorm);
      MathTestUtils.AssertAlmostEqual(pos, surfacePos);
      MathTestUtils.AssertAlmostEqual(1, surfaceNorm.magnitude);
      for (int axis=0; axis<3; ++axis) {
        float p = pos[axis];
        float h = halfWidth[axis];
        float n = surfaceNorm[axis];
        // The normal is not well defined, but it should at least point away from the box
        if (p == h) {
          Assert.GreaterOrEqual(n, 0, "Axis {0}", axis);
        } else if (p == -h) {
          Assert.LessOrEqual(n, 0, "Axis {0}", axis);
        } else if (-h < p & p < h) {
          // Should have no component parallel to the edge we're on
          Assert.AreEqual(n, 0, "Axis {0}", axis);
        } else {
          Assert.Fail("Bad test");
        }
      }
    }
  }

  [Test]
  public static void TestUuid3() {
    // Test values computed using python uuid.uuid3
    Assert.AreEqual(GuidUtils.Uuid3(Guid.Empty, "fancy"),
                    new Guid("e033f80d-b580-3145-8f4b-3d90f8c0da30"));
    Assert.AreEqual(GuidUtils.Uuid3(GuidUtils.kNamespaceDns, "fancy"),
                    new Guid("5d5c7347-4e4b-3a86-a58e-3615d4aa6b2e"));
  }

  [Test]
  public static void TestUuid5() {
    // Test values computed using python uuid.uuid5
    Assert.AreEqual(GuidUtils.Uuid5(Guid.Empty, "fancy"),
                    new Guid("f8893632-dedc-566d-83f1-afda8c3bbd31"));
    Assert.AreEqual(GuidUtils.Uuid5(GuidUtils.kNamespaceDns, "fancy"),
                    new Guid("750c4490-5470-5f19-a5e9-98c1ce534c7e"));
  }

  static void TestUnityGuidHelper(string rfcGuid_, string unityGuid) {
    Guid rfcGuid = new Guid(rfcGuid_);
    Assert.AreEqual(unityGuid, GuidUtils.SerializeToUnity(rfcGuid));
    Assert.AreEqual(rfcGuid, GuidUtils.DeserializeFromUnity(unityGuid));
  }
  [Test]
  public static void TestUnityGuidSerialization() {
    TestUnityGuidHelper("3b63530f-b85e-4dca-b52a-e7926a3a97a3", "f03536b3e58bacd45ba27e29a6a3793a");
    TestUnityGuidHelper("71581662-6a39-214c-9576-ee104ce08228", "2661851793a6c4125967ee01c40e2882");
    TestUnityGuidHelper("e46f7048-3b57-4e5e-a34b-b633730b2796", "8407f64e75b3e5e43ab46b3337b07269");
  }

  static void RfcTestHelper(string input, string expectedAsciiOutput) {
    Assert.AreEqual(expectedAsciiOutput, TextUtils.Rfc5987Encode(input));
  }
  [Test]
  public void TestRfc5987Encode() {
    // all of the attr-char characters (plus space)
    RfcTestHelper("abcdefghijkl mnopqrstuvwxyz", "UTF-8''abcdefghijkl%20mnopqrstuvwxyz");
    RfcTestHelper("ABCDEFGHIJKL MNOPQRSTUVWXYZ", "UTF-8''ABCDEFGHIJKL%20MNOPQRSTUVWXYZ");
    RfcTestHelper("0123456789",   "UTF-8''0123456789");
    RfcTestHelper("!#$&+-.^_`|~", "UTF-8''!#$&+-.^_`|~");
    // ascii and displayable but not in attr-char
    RfcTestHelper(" \"%'()*,/:;<=>?@[\\]{}", "UTF-8''%20%22%25%27%28%29%2a%2c%2f%3a%3b%3c%3d%3e%3f%40%5b%5c%5d%7b%7d");
    // some control characters
    RfcTestHelper("\x00\x01\x02\x03\x04\x05\r\n\t", "UTF-8''%00%01%02%03%04%05%0d%0a%09");

    // some 2-, 3-, and 4-byte utf-8 sequences
    RfcTestHelper("_\u00fc-",     "UTF-8''_%c3%bc-");
    RfcTestHelper("_\u20ac-",     "UTF-8''_%e2%82%ac-");
    RfcTestHelper("_\U0001d306-", "UTF-8''_%f0%9d%8c%86-");
  }

  // Tests that SetImageUrlOptions works whether or not there is an options section
  // in the input URL.
  [Test]
  public void TestSetImageUrlOptions() {
    // Just a quick test that SetImageUrl
    Assert.AreEqual(
        OAuth2Identity.SetImageUrlOptions(
            "https://lh3.googleusercontent.com/a-/AN66SAy7y9N4Do-folxEcPjtglaoHtam6THlfn8wk8aF=s100"),
        "https://lh3.googleusercontent.com/a-/AN66SAy7y9N4Do-folxEcPjtglaoHtam6THlfn8wk8aF=s128-c-k-no-rj");
    Assert.AreEqual(
        OAuth2Identity.SetImageUrlOptions(
            "https://lh3.googleusercontent.com/a-/AN66SAy7y9N4Do-folxEcPjtglaoHtam6THlfn8wk8aF"),
        "https://lh3.googleusercontent.com/a-/AN66SAy7y9N4Do-folxEcPjtglaoHtam6THlfn8wk8aF=s128-c-k-no-rj");
  }

  [UnityTest]
  public IEnumerator TestAsUnityTestStartsOnUnityThread() => AsUnityTest(async () => {
    Assert.AreEqual(
        System.Threading.SynchronizationContext.Current,
        UnityAsyncAwaitUtil.SyncContextUtil.UnitySynchronizationContext);
    await new WaitForBackgroundThread();
    Assert.AreNotEqual(
        System.Threading.SynchronizationContext.Current,
        UnityAsyncAwaitUtil.SyncContextUtil.UnitySynchronizationContext);
  });

  // A test that our integration of AsyncAwaitUtil and com.unity.editorcoroutines works.
  // Also serves as an example of how to test async code, because the Unity version of NUnit
  // doesn't have direct support for async test methods.
  [UnityTest]
  public IEnumerator TestAsyncAwaitUtilWorksAtEditTime() => AsUnityTest(async () => {

    const int kNumFrames = 3;
    float dt = 0;
    // This should put us on the Unity thread...
    await Awaiters.NextFrame;
    for (int i = 0; i < kNumFrames; ++i) {
      dt -= Time.realtimeSinceStartup;  // ...and this will throw if we're not.
      await Awaiters.NextFrame;
      dt += Time.realtimeSinceStartup;
    }
    Assert.Less(dt / kNumFrames, 2f);

  });

  [UnityTest]
  public IEnumerator TestImageUtils_DownloadTextureAsync() => AsUnityTest(async () => {

      const string kUrl = "https://lh3.googleusercontent.com/a-/AN66SAy7y9N4Do-folxEcPjtglaoHtam6THlfn8wk8aF=s100";
      //await Awaiters.NextFrame;  // DownloadTextureAsync is a main-thread-only API
      Texture2D tex = await ImageUtils.DownloadTextureAsync(kUrl);
      Assert.AreEqual(100, tex.width);
      Assert.AreEqual(100, tex.height);

  });
}

}
