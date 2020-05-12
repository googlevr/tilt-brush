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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TiltBrush {

// Usage:
// - Editor: Drop a TestPackInt.prefab in a scene, and look at it.
// - Editor: Use the menu item, or otherwise call RunTests().
// - Standalone: prefab.GetComponent<TestPackInt>().PrefabRunTests(). No need to instantiate it.
//
// See go/tbcl/96730
[ExecuteInEditMode]
class TestPackInt : MonoBehaviour {
  // Static API

  /// Enumerates the passes in TestIntersection.shader
  const int kSelfContainedTest = 0;
  const int kTwoPassTestPack = 1;
  const int kTwoPassTestUnpackAndCheck = 2;
  const int kRenderUvPass = 3;
  const int kCopyPass = 4;
  const int kUnpackRepack = 5;

#if UNITY_EDITOR && false
  [UnityEditor.MenuItem("Tilt/Run PackInt unit test")]
  static void UnitTestMenuItem() {
    Material material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
        "Assets/PlaymodeTests/PackInt/TestPackInt.mat");
    if (material == null) { throw new Exception("Can't find my shaders"); }
    RunTests(material, null);
  }
#endif

  /// Time.frameCount increases only sporadically in the editor, so roll our own.
  static int sm_runCount = 0;

  /// Used for the Blit hack.
  static Material sm_copyTextureMat = null;

  /// On Android, Graphics.Blit only seems to be writing to the top-right quadrant
  /// of the RenderTexture. uv=(0,0) is in the middle (rather than bottom-left),
  /// and uv=(1,1) is at the upper-right as expected.
  ///
  /// I haven't been able to figure out why this is, so this is a hacky reimplementation
  /// of Blit().
  private static void MyBlit(Texture source, RenderTexture destination) {
    MyBlit(source, destination, sm_copyTextureMat, kCopyPass);
  }

  private static void MyBlit(
      Texture source, RenderTexture destination, Material mat, int pass) {
    var prev = RenderTexture.active;
    try {
      RenderTexture.active = destination;
      mat.SetTexture("_MainTex", source);
      GL.PushMatrix();
      GL.LoadOrtho();
      GL.invertCulling = true;
      mat.SetPass(pass);
      GL.Begin(GL.QUADS);
      GL.MultiTexCoord2(0, 0.0f, 0.0f);
      GL.Vertex3(0.0f, 0.0f, 0.0f);
      GL.MultiTexCoord2(0, 1.0f, 0.0f);
      GL.Vertex3(1.0f, 0.0f, 0.0f);
      GL.MultiTexCoord2(0, 1.0f, 1.0f);
      GL.Vertex3(1.0f, 1.0f, 1.0f);
      GL.MultiTexCoord2(0, 0.0f, 1.0f);
      GL.Vertex3(0.0f, 1.0f, 0.0f);
      GL.End();
      GL.invertCulling = false;
      GL.PopMatrix();
    } finally {
      RenderTexture.active = prev;
    }
  }

  /// Extracts color data from the passed RenderTexture
  private static Color32[] GetColors(RenderTexture rt) {
    RenderTexture prev = RenderTexture.active;
    try {
      RenderTexture.active = rt;
      Color32[] colors; {
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0, false);
        tex.Apply();
        colors = tex.GetPixels32();
        DestroyImmediate(tex);
        return colors;
      }
    } finally {
      RenderTexture.active = prev;
    }
  }

  /// Writes the raw rgba bytes of the passed RenderTexture to disk
  private static unsafe void WriteTextureToDisk(RenderTexture rt, string name) {
    Color32[] colors = GetColors(rt);
    byte[] bytes = new byte[colors.Length * 4];
    fixed (Color32* pColors = colors)
    fixed (byte* pDst = bytes) {
      byte* pSrc = (byte*)pColors;
      for (int i = 0; i < bytes.Length; ++i) {
        pDst[i] = pSrc[i];
      }
    }

    string filename = string.Format("{0}/{1}", Application.temporaryCachePath, name);
    Debug.Log("Writing "+filename);
    using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write)) {
      fs.Write(bytes, 0, bytes.Length);
    }
  }

  /// Checks that the texels in rt are all (0,1,0, 1).
  /// If output is non-null, delegate the checking to a human by copying rt to output.
  /// (It's assumed that this will be displayed to the user)
  /// If output is null, checks the texels immediately and raises an exception on error.
  private static unsafe void CheckAllGreen(RenderTexture rt, RenderTexture output) {
    if (output != null) {
      MyBlit(rt, output);
      return;
    }

    RenderTexture prev = RenderTexture.active;
    try {
      RenderTexture.active = rt;
      Color32[] colors; {
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0, false);
        tex.Apply();
        colors = tex.GetPixels32();
        DestroyImmediate(tex);
      }

      UInt32 desired; {
        Color32[] green = { Color.green };
        fixed (Color32* pGreen = green) { desired = *(UInt32*)pGreen; }
      }

      // Can you believe Color32 doesn't have operator==?
      int badTexels = 0;
      fixed (Color32* pColors = colors) {
        UInt32* pInts = (UInt32*)pColors;
        int length = colors.Length;
        for (int i = 0; i < length; ++i) {
          badTexels += (pInts[i] != desired) ? 1 : 0;
        }
      }

      if (badTexels > 0) {
        throw new Exception(string.Format("{0} non-green texels", badTexels));
      }
    } finally {
      RenderTexture.active = prev;
    }
  }

  /// Returns a target for use with the test shader
  private static RenderTexture GetTemporaryTarget() {
    var desc = new RenderTextureDescriptor(256, 256, RenderTextureFormat.ARGB32, 0) {
      sRGB = false,
      // I thought this would resolve my Graphics.Blit() issues, but it doesn't
      vrUsage = VRTextureUsage.None
    };
    var rt = RenderTexture.GetTemporary(desc);
    rt.filterMode = FilterMode.Point;
    return rt;
  }

  /// Pass a material containing the shaders being tested.
  ///
  /// If a RenderTexture is passed, the unit test results are copied to it and
  /// it's assumed a human will judge the test validity.
  /// Otherwise, the target will be examined programmatically.
  ///
  /// This is a static function to make it clear that no live GameObject is necessary.
  public static void RunTests(Material testMaterial, RenderTexture output) {
    if (testMaterial == null) {
      throw new NullReferenceException("material");
    }

    Texture2D kUnusedInput = Texture2D.whiteTexture;
    int kHoldTime = 60;
    int kNumTests = 4;

    int currentTest = ((sm_runCount++) % (kHoldTime * kNumTests)) / kHoldTime;

    var rt1 = GetTemporaryTarget();
    var rt2 = GetTemporaryTarget();
    var prev = RenderTexture.active;
    sm_copyTextureMat = testMaterial;
    try {
      if (output != null) {
        MyBlit(Texture2D.blackTexture, output);
      }
      // If output non-null, run a single test and display output to the user
      // Otherwise, run all tests and assert the test output is correct.
      IEnumerable<int> testsToRun = (output != null)
          ? Enumerable.Range(currentTest, 1)
          : Enumerable.Range(0, kNumTests);

      foreach (int test in testsToRun) {
        MyBlit(Texture2D.blackTexture, rt1);
        MyBlit(Texture2D.blackTexture, rt2);
        switch (test) {
        case 0:
          MyBlit(kUnusedInput, rt1, testMaterial, kSelfContainedTest);
          CheckAllGreen(rt1, output);
          break;
        case 1:
          MyBlit(kUnusedInput, rt1, testMaterial, kTwoPassTestPack);
          MyBlit(rt1,          rt2, testMaterial, kTwoPassTestUnpackAndCheck);
          CheckAllGreen(rt2, output);
          break;
        case 2:
          // Check whether Blit() works normally on this target.
          // On Quest in single-pass mode, it does not.
          Graphics.Blit(kUnusedInput, output, testMaterial, kRenderUvPass);
          break;
        case 3:
          MyBlit(Texture2D.whiteTexture, output);
          break;
        }
      }
    } finally {
      sm_copyTextureMat = null;
      // Not sure why this is necessary; but the editor otherwise complains that
      // the active rendertexture is getting released
      RenderTexture.active = prev;
      RenderTexture.ReleaseTemporary(rt2);
      RenderTexture.ReleaseTemporary(rt1);
    }
  }

  // Instance API

  /// Material containing the shader(s) implementing the test
  [SerializeField] private Material m_testMaterial;

  private RenderTexture m_debugRt = null;  // lazily-created

  void Awake() {
#if !UNITY_EDITOR
    // Run test once and spam the result
    try {
      RunTests(m_testMaterial, null);
      Debug.Log("TEST: OK");
    } catch (Exception e) {
      Debug.LogError("TEST: Got exception");
      Debug.LogException(e);
    }
#endif
  }

  // For interactive use in the editor; or at runtime, I suppose.
  // Runs the tests and visualizes them on a quad.
  void OnRenderObject() {
    if (m_debugRt == null) {
      var desc = new RenderTextureDescriptor(256, 256, RenderTextureFormat.ARGB32, 0) {
        sRGB = false,
        vrUsage = VRTextureUsage.None
      };
      m_debugRt = new RenderTexture(desc);
      m_debugRt.filterMode = FilterMode.Point;
      m_debugRt.name = "Human-viewable test results";

      MeshRenderer mr = GetComponent<MeshRenderer>();
      if (mr != null && mr.sharedMaterial != null) {
        // Do it this way to avoid Unity yelling at us about leaking materials at edit time
        var mat = new Material(mr.sharedMaterial);
        mat.mainTexture = m_debugRt;
        mr.sharedMaterial = mat;
      } else {
        Debug.LogWarning("Cannot set up debug material", this);
      }
    }

    RunTests(m_testMaterial, m_debugRt);
  }
}

}  // namespace TiltBrush
