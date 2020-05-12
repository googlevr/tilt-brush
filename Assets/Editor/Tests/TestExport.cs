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

#if FBX_SUPPORTED
using System;
using System.Collections.Generic;
using System.IO;

using Autodesk.Fbx;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace TiltBrush {

internal class TestExport {
  [Test]
  public void TestFbxQuaternion() {
    var uq = new Quaternion(1, 2, 3, 4);
    var fq = uq.ToFbxQuaternion();

    // Basic round-tripping
    var uq2 = fq.ToUQuaternion();
    MathTestUtils.AssertAlmostEqual(uq, uq2);

    // Check that [3] is w, the real part (docs don't say anything about this)
    var fqc = new FbxQuaternion(fq);
    fqc.Conjugate();
    // [3] is the real part
    MathTestUtils.AssertAlmostEqual((float)fq.GetAt(3), (float)fqc.GetAt(3));
    // and 0-2 are the imaginary part
    MathTestUtils.AssertAlmostEqual((float)fq.GetAt(1), -(float)fqc.GetAt(1));
  }

  [Test]
  public void TestPositionAndVectorSemanticsHaveAtLeast3Elements() {
    foreach (var guid in AssetDatabase.FindAssets("t:BrushDescriptor")) {
      var path = AssetDatabase.GUIDToAssetPath(guid);
      var desc = AssetDatabase.LoadAssetAtPath<BrushDescriptor>(path);
      GeometryPool.VertexLayout layout;
      try {
        layout = desc.VertexLayout;
      } catch (Exception e) {
        throw new Exception("Bad descriptor " + path, e);
      }
      for (int channel = 0; channel < GeometryPool.kNumTexcoords; ++channel) {
        var info = layout.GetTexcoordInfo(channel);
        if ((   info.semantic == GeometryPool.Semantic.Position
             || info.semantic == GeometryPool.Semantic.Vector)
            && info.size < 3) {
          throw new Exception(
              string.Format("{0} channel {1} is {2} and size {3} != 3",
                            desc.m_DurableName, channel, info.semantic, info.size));
        }
      }
    }
  }

  [Test]
  public void TestAxisConvention() {
    AxisConvention[] acs = {
      AxisConvention.kUnity,
      AxisConvention.kGltf2,
      AxisConvention.kUsd,
      AxisConvention.kStl,
      AxisConvention.kUnreal
    };

    foreach (var ac1 in acs)
    foreach (var ac2 in acs) {
      Matrix4x4 ac1FromAc2 = AxisConvention.GetToDstFromSrc(ac1, ac2);
      Assert.AreEqual(ac1.forward, ac1FromAc2.MultiplyVector(ac2.forward));
      Assert.AreEqual(ac1.right,   ac1FromAc2.MultiplyVector(ac2.right  ));
      Assert.AreEqual(ac1.up,      ac1FromAc2.MultiplyVector(ac2.up     ));
    }

    foreach (var ac in acs) {
      Matrix4x4 fromUnity = AxisConvention.GetFromUnity(ac);
      Assert.AreEqual(ac.forward, fromUnity.MultiplyVector(Vector3.forward));
      Assert.AreEqual(ac.right,   fromUnity.MultiplyVector(Vector3.right  ));
      Assert.AreEqual(ac.up,      fromUnity.MultiplyVector(Vector3.up     ));

      Matrix4x4 toUnity = AxisConvention.GetToUnity(ac);
      Assert.AreEqual(Vector3.forward, toUnity.MultiplyVector(ac.forward));
      Assert.AreEqual(Vector3.right,   toUnity.MultiplyVector(ac.right  ));
      Assert.AreEqual(Vector3.up,      toUnity.MultiplyVector(ac.up     ));
    }
  }

  [Test]
  public void TestGltfDispose() {
    string tempDir = Path.Combine(Application.dataPath, "..", "Temp", "TiltBrushUnitTests");
    using (var globals = new GlTF_Globals(tempDir, gltfVersion: 1)) {
      globals.binary = true;
      globals.OpenFiles(Path.Combine(tempDir, "foo.glb1"));
      globals.CloseFiles();
    }
  }

  [Test]
  public void TestGetOrCreateSafeLocal() {
    var dp = Application.dataPath;
    var ctx = new ExportFileReference.DisambiguationContext();
    var paper = ExportFileReference.GetOrCreateSafeLocal(
        ctx, "main.png", dp + @"\Resources\Brushes\Basic\Paper");
    var paper2 = ExportFileReference.GetOrCreateSafeLocal(
        ctx, "main.png", dp + @"\Resources\Brushes\Basic\Paper");
    Assert.AreEqual(paper, paper2);  // reference equality
    Assert.AreEqual("main.png", paper.m_uri);
    Assert.AreEqual("main.png", paper2.m_uri);

    var ductTape = ExportFileReference.GetOrCreateSafeLocal(
        ctx, "main.png", dp + @"\Resources\Brushes\Basic\DuctTape");
    Assert.AreNotEqual(paper, ductTape);
    Assert.AreEqual("main_1.png", ductTape.m_uri);

    var peverse = ExportFileReference.GetOrCreateSafeLocal(
        ctx, "main_1.png", dp + @"\Editor\Tests\TestData");
    Assert.AreEqual("main_1_1.png", peverse.m_uri);

    var withNamespace = ExportFileReference.GetOrCreateSafeLocal(
        ctx, "main.png", dp + @"\Resources\Brushes\Basic\Hypercolor",
        @"subdirectory\brush_main.png");
    Assert.AreEqual("brush_main.png", withNamespace.m_uri);
  }

  [Test]
  public void TestCreateUniqueName() {
    HashSet<string> names = new HashSet<string>();
    string Create(string name) => ExportUtils.CreateUniqueName(name, names);
    Assert.AreEqual("main.png", Create("main.png"));
    Assert.AreEqual("main_1.png", Create("main.png"));
    Assert.AreEqual("main_1_1.png", Create("main_1.png"));  // perverse
    Assert.AreEqual("main_2.png", Create("main.png"));

    Assert.AreEqual("initialShadingGroup", Create("initialShadingGroup"));
    Assert.AreEqual("initialShadingGroup_1", Create("initialShadingGroup"));
  }
}

}
#endif
