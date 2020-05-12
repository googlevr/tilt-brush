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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using static TiltBrush.AsyncTestUtils;

namespace TiltBrush {

internal class TestVrAssetService : MathTestUtils {
  // Sketchfab asset created at Google
  const string kSketchfabTb2Published = "2b544da19f8049f0ad27a0202b437621";

  // Poly asset ids created at Google.
  const string kUserIdTb1 = "9f2k8JLwFxt";
  const string kAssetTb1Unpublished = "0UpOIIqyXpt";
  const string kAssetTb1Remixable   = "bxpNKfwcnQ9";
  const string kAssetTb1Unremixable = "95FNDeqPRKF";
  const string kAssetTb2Unpublished = "6CJZkGJ3Kj6";
  const string kAssetTb2Remixable   = "7XE3GrKQYmD";

  static Vector3 kZRight   = new Vector3(1, 0, 0);
  static Vector3 kZUp      = new Vector3(0, 1, 0);
  static Vector3 kZForward = new Vector3(0, 0,-1);

  static string Serialize<T>(T obj) {
    var settings = new JsonSerializerSettings {
      NullValueHandling = NullValueHandling.Ignore,
      DefaultValueHandling = DefaultValueHandling.Ignore
    };
    return JsonConvert.SerializeObject(obj, Formatting.None, settings);
  }

  // All tests that use VrAssetService must await one of these first
  private Task<OAuth2Identity> m_InitGoogleTask;
  private Task<OAuth2Identity> m_InitSketchfabTask;

  [OneTimeSetUp]
  public void RunBeforeAnyTests() {
    // Hack alert!
    Debug.Assert(App.Instance == null);
    App.Instance = GameObject.Find("/App").GetComponent<App>();

    Debug.Assert(Config.m_SingletonState == null);
    Config.m_SingletonState = GameObject.Find("/App/Config").GetComponent<Config>();

    // Returns the passed OAuth in an initialized state
    async Task<OAuth2Identity> InitializeAsync(OAuth2Identity oai) {
      await oai.InitializeAsync();
      return oai;
    }

    m_InitGoogleTask = InitializeAsync(App.GoogleIdentity);
    m_InitSketchfabTask = InitializeAsync(App.SketchfabIdentity);
  }

  // Ensures that the Google identity is logged in.
  // Optionally, ensure that a particular user is logged in.
  // On failure, the current unit test is skipped.
  private async Task EnsureLoggedInGoogleAsync(string asUser=null) {
    var oai = await m_InitGoogleTask;
    if (!oai.LoggedIn) { goto fail; }
    else if (asUser != null && oai.Profile.email != asUser) { goto fail; }
    return;
  fail:
    if (asUser == null) {
      throw new IgnoreException("This test requires login to Google");
    } else {
      throw new IgnoreException($"This test requires login to Google as '{asUser}'");
    }
  }

  // Ensures that the Sketchfab identity is logged in.
  // Optionally, ensure that a particular user is logged in.
  // On failure, the current unit test is skipped.
  private async Task<SketchfabService> EnsureLoggedInSketchfabAsync(string asUser=null) {
    var oai = await m_InitSketchfabTask;
    if (!oai.LoggedIn) { goto fail; }
    else if (asUser != null && oai.Profile.email != asUser) { goto fail; }
    return new SketchfabService(oai);
    fail:
    if (asUser == null) {
      throw new IgnoreException("This test requires login to Sketchfab");
    } else {
      throw new IgnoreException($"This test requires login to Sketchfab as '{asUser}'");
    }
  }

  [OneTimeTearDown]
  public void RunAfterAllTests() {
    Config.m_SingletonState = null;
    App.Instance = null;
  }

  [Test]
  public void TestConvertFruToPoly() {
    // Test that the basis-conversion transforms seem correct
    var zFromU = VrAssetService.kPolyFromUnity;
    var uFromZ = zFromU.inverse;
    Assert.AreEqual( kZForward, zFromU * Vector3.forward );
    Assert.AreEqual( kZRight  , zFromU * Vector3.right   );
    Assert.AreEqual( kZUp     , zFromU * Vector3.up      );
    Assert.AreEqual( Vector3.forward, (Vector3)(uFromZ * kZForward) );
    Assert.AreEqual( Vector3.right  , (Vector3)(uFromZ * kZRight  ) );
    Assert.AreEqual( Vector3.up     , (Vector3)(uFromZ * kZUp     ) );
  }

  [Test]
  public void TestTransformByForBasisChange() {
    // This is more a test of TrTransform.TransformBy than anything else
    var zFromU = VrAssetService.kPolyFromUnity;

    // This rotates Unity-forward to Unity-right
    TrTransform xfFwdToRt_U = TrTransform.R(Quaternion.AngleAxis(90, Vector3.up));
    AssertAlmostEqual(Vector3.right, xfFwdToRt_U * Vector3.forward);
    AssertAlmostEqual(Vector3.up,    xfFwdToRt_U * Vector3.up);

    // This should rotate Poly-forward to Poly-right (to test)
    TrTransform xfFwdToRt_Z = xfFwdToRt_U.TransformBy(zFromU);
    AssertAlmostEqual(kZRight, xfFwdToRt_Z * kZForward);
    AssertAlmostEqual(kZUp,    xfFwdToRt_Z * kZUp);
  }

  [UnityTest]
  public IEnumerator TestCanGetSketchfabAccountId() => AsUnityTest(async () => {
    var sf = await EnsureLoggedInSketchfabAsync();
    var info = await sf.GetUserInfo();
    Assert.AreNotEqual(null, info.uid, "Are you not logged in?");
  });

  [UnityTest]
  public IEnumerator TestGetSketchfabLikes() => AsUnityTest(async () => {
    var sf = await EnsureLoggedInSketchfabAsync();

    for (var task = sf.GetMeLikes(); task != null; task = sf.GetNextPageAsync(task))
    foreach (var element in (await task).results) {
      // Debug.Log($"{element.name}");
    }
  });

  [UnityTest]
  public IEnumerator TestDownloadAsset() => AsUnityTest(async () => {
    var sf = await EnsureLoggedInSketchfabAsync();
    var metadata = await sf.GetModelDownload(kSketchfabTb2Published);
    var client = new HttpClient();
    var request = new HttpRequestMessage(HttpMethod.Get, metadata.gltf.url);
    var reply = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    using (var memoryStream = new MemoryStream()) {
      using (var stream = await reply.Content.ReadAsStreamAsync()) {
        stream.CopyTo(memoryStream);
      }

      memoryStream.Seek(0, SeekOrigin.Begin);
      var zip = new ZipArchive(memoryStream);
      Assert.IsTrue(zip.Entries.Any(entry => entry.Name.EndsWith(".gltf")));
      Assert.IsTrue(zip.Entries.Any(entry => entry.Name.EndsWith(".tilt")));
    }
  });

  [Test]
  public void TestGetRelativePath() {
    var testCases = new[] {
        ("/storage/emulated/0/Android/data/com.google.tiltbrush/cache/Upload/sketch.gltf",
         "/storage/emulated/0/Android/data/com.google.tiltbrush/cache/Upload",
         "sketch.gltf"),
        ("c:/src/tb/Support/somefile.txt",
         "c:\\src/tb",
         "Support\\somefile.txt")
    };
    foreach (var tuple in testCases) {
      var (toFile, fromDir, desired) = tuple;
      Assert.AreEqual(desired, VrAssetService.GetRelativePath(fromDir, toFile));
    }
  }

}
}  // namespace TiltBrush
