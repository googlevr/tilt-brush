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

using NUnit.Framework;
using UnityEngine;

using Object = UnityEngine.Object;
using VertexLayout = TiltBrush.GeometryPool.VertexLayout;
using TexcoordInfo = TiltBrush.GeometryPool.TexcoordInfo;
using TexcoordData = TiltBrush.GeometryPool.TexcoordData;
using Semantic = TiltBrush.GeometryPool.Semantic;

namespace TiltBrush {

internal class TestGeometryPool {
  // Individual tests can write/read files in this directory
  private static string TemporaryData {
    get {
      var dir = string.Format("{0}/../Temp/TestGeometryPool", UnityEngine.Application.dataPath);
      Directory.CreateDirectory(dir);
      return dir;
    }
  }

  private static GeometryPool RandomGeometryPool() {
    int vertexCount = 20;
    int indexCount = 60;
    var pool = new GeometryPool();
    pool.Layout = new VertexLayout {
        texcoord0 = new TexcoordInfo { size = 2, semantic = Semantic.XyIsUv },
        bUseNormals = true,
        bUseColors = true,
        bUseTangents = true
    };
    pool.m_Vertices  = MathTestUtils.RandomVector3List(vertexCount);
    pool.m_Tris      = MathTestUtils.RandomIntList(indexCount, 0, vertexCount);
    pool.m_Normals   = MathTestUtils.RandomVector3List(vertexCount);
    pool.m_Colors    = MathTestUtils.RandomColor32List(vertexCount);
    pool.m_Tangents  = MathTestUtils.RandomVector4List(vertexCount);
    pool.m_Texcoord0.v2 = MathTestUtils.RandomVector2List(vertexCount);
    return pool;
  }

  public static bool AreEqual(GeometryPool lhs, GeometryPool rhs, ref string outWhy) {
    string why = "";
    if (! (lhs.Layout == rhs.Layout)) {
      why += "Layout,";
    }
    if (! (ElementEqual(lhs.m_Vertices, rhs.m_Vertices, ref why))) {
      why += "verts,";
    }
    if (! (ElementEqual(lhs.m_Tris, rhs.m_Tris, ref why))) {
      why += "tris,";
    }
    if (! (ElementEqual(lhs.m_Normals, rhs.m_Normals, ref why))) {
      why += "normals,";
    }
    if (! (ElementEqual(lhs.m_Colors, rhs.m_Colors, ref why))) {
      why += "colors,";
    }
    if (! (ElementEqual(lhs.m_Tangents, rhs.m_Tangents, ref why))) {
      why += "tangents,";
    }

    if (! (AreEqual(lhs.m_Texcoord0, rhs.m_Texcoord0, rhs.Layout.texcoord0.size, ref why))) {
      why += "texcoord0,";
    }
    if (! (AreEqual(lhs.m_Texcoord1, rhs.m_Texcoord1, rhs.Layout.texcoord1.size, ref why))) {
      why += "texcoord1,";
    }
    if (! (AreEqual(lhs.m_Texcoord2, rhs.m_Texcoord2, rhs.Layout.texcoord2.size, ref why))) {
      why += "texcoord2,";
    }

    outWhy += why;
    return (why == "");
  }

  static bool AreEqual(TexcoordData lhs, TexcoordData rhs, int size, ref string why) {
    switch (size) {
    case 0: return true;
    case 2: return ElementEqual(lhs.v2, rhs.v2, ref why);
    case 3: return ElementEqual(lhs.v3, rhs.v3, ref why);
    case 4: return ElementEqual(lhs.v4, rhs.v4, ref why);
    default: return false;
    }
  }

  static bool ElementEqual<T>(List<T> lhs, List<T> rhs, ref string why) where T: struct {
    if (lhs == null && rhs == null) return true;
    if (lhs == null || rhs == null) return false;
    if (lhs.Count != rhs.Count) return false;
    int c = lhs.Count;
    for (int i = 0; i < c; ++i) {
      if (! EqualityComparer<T>.Default.Equals(lhs[i], rhs[i])) {
        why += string.Format("{1} != {2} at [{0}] of ", i, lhs[i], rhs[i]);
        return false;
      }
    }
    return true;
  }

  private static void AssertAreEqual(GeometryPool expected, GeometryPool actual) {
    string why = "";
    bool equal = AreEqual(expected, actual, ref why);
    Assert.AreEqual("", why);
    Assert.IsTrue(equal);
  }

  [Test]
  public void TestGetSizeAndSemantic() {
    var layout = new VertexLayout {
        texcoord0 = new TexcoordInfo { size = 2, semantic = Semantic.XyIsUv },
        texcoord1 = new TexcoordInfo { size = 3, semantic = Semantic.Position },
        texcoord2 = new TexcoordInfo { size = 4, semantic = Semantic.Vector }
    };
    Assert.AreEqual(layout.texcoord0, layout.GetTexcoordInfo(0));
    Assert.AreEqual(layout.texcoord1, layout.GetTexcoordInfo(1));
    Assert.AreEqual(layout.texcoord2, layout.GetTexcoordInfo(2));
  }

  [Test]
  public void TestTexcoordInfoEquality() {
    var ti = new TexcoordInfo { size = 3, semantic = Semantic.Position };
    TexcoordInfo ti2;
    ti2 = ti;
    Assert.AreEqual(ti, ti2);

    ti2 = ti;
    ti2.size += 1;
    Assert.AreNotEqual(ti, ti2);

    ti2 = ti;
    ti2.semantic = Semantic.XyIsUvZIsDistance;
    Assert.AreNotEqual(ti, ti2);
  }

  [Test]
  public void TestLayoutAssignmentAndReset() {
    var pool = new GeometryPool {
      Layout = new VertexLayout {
        texcoord0 = new TexcoordInfo { size = 2 },
        texcoord1 = new TexcoordInfo { size = 3 },
        texcoord2 = new TexcoordInfo { size = 4 },
      }
    };
    Assert.IsNotNull(pool.m_Texcoord0.v2);
    Assert.IsNotNull(pool.m_Texcoord1.v3);
    Assert.IsNotNull(pool.m_Texcoord2.v4);

    int N = 30;
    pool.m_Texcoord0.v2.AddRange(MathTestUtils.RandomVector2List(N));
    pool.m_Texcoord1.v3.AddRange(MathTestUtils.RandomVector3List(N));
    pool.m_Texcoord2.v4.AddRange(MathTestUtils.RandomVector4List(N));
    pool.Reset();
    Assert.AreEqual(0, pool.m_Texcoord0.v2.Count);
    Assert.AreEqual(0, pool.m_Texcoord1.v3.Count);
    Assert.AreEqual(0, pool.m_Texcoord2.v4.Count);
  }

  [Test]
  public void TestLayoutEquality() {
    var vl = new VertexLayout {
        texcoord0 = new TexcoordInfo { size = 2, semantic = Semantic.Position },
        texcoord1 = new TexcoordInfo { size = 3, semantic = Semantic.Position },
        texcoord2 = new TexcoordInfo { size = 4, semantic = Semantic.Position }
    };
    VertexLayout vl2;
    vl2 = vl; Assert.AreEqual(vl, vl2);
    vl2 = vl; vl2.texcoord0.size += 1; Assert.AreNotEqual(vl, vl2);
    vl2 = vl; vl2.texcoord1.size += 1; Assert.AreNotEqual(vl, vl2);
    vl2 = vl; vl2.texcoord2.size += 1; Assert.AreNotEqual(vl, vl2);
    vl2 = vl; vl2.texcoord0.semantic = Semantic.Unspecified; Assert.AreNotEqual(vl, vl2);
    vl2 = vl; vl2.texcoord1.semantic = Semantic.Unspecified; Assert.AreNotEqual(vl, vl2);
    vl2 = vl; vl2.texcoord2.semantic = Semantic.Unspecified; Assert.AreNotEqual(vl, vl2);
  }

  [Test]
  public void TestGeometryPool_Clone() {
    var pool = RandomGeometryPool();
    var pool2 = pool.Clone();
    AssertAreEqual(pool, pool2);
  }

  [Test]
  public void TestGeometryPool_RoundTripToStream() {
    var pool = RandomGeometryPool();
    var stream = new MemoryStream();
    pool.SerializeToStream(stream);

    // Deserialize to already-created pool; empty-pool case is handled by the next test.
    var expected = pool.Clone();
    stream.Position = 0;
    Assert.IsTrue(pool.DeserializeFromStream(stream));
    AssertAreEqual(expected, pool);
  }

  [Test]
  public void TestGeometryPool_RoundTripToBackingFile() {
    var pool = RandomGeometryPool();
    var expected = pool.Clone();
    var filename = Path.Combine(TemporaryData, "roundtriptobackingfile.bin");
    pool.MakeGeometryNotResident(filename);
    pool.EnsureGeometryResident();
    AssertAreEqual(expected, pool);
  }

  [Test]
  public void TestStructByReference() {
    var pool = new GeometryPool();
    pool.Layout = new VertexLayout {
        texcoord0 = new TexcoordInfo { size = 2, semantic = GeometryPool.Semantic.XyIsUv }
    };
    Assert.IsNotNull(pool.m_Texcoord0.v2 != null);
    // One or the other of m_UvSetN, m_TexcoordN is a property.
    // Normally, properties can only return structs by value.
    // Check that these properties are references.
    pool.m_Texcoord1.v2 = pool.m_Texcoord0.v2;
    Assert.IsNotNull(pool.m_Texcoord1.v2);
    pool.m_Texcoord0.v2 = null;
    Assert.IsNull(pool.m_Texcoord0.v2);
  }

  [Test]
  public void TestAppendMeshFailure() {
    var mesh = new Mesh();
    mesh.vertices = new[] {Vector3.one};

    {
      var pool = new GeometryPool {Layout = new VertexLayout {bUseNormals = true}};
      Assert.Throws<InvalidOperationException>(() => pool.Append(mesh));
    }
    {
      var pool = new GeometryPool {Layout = new VertexLayout {bUseColors = true}};
      Assert.Throws<InvalidOperationException>(() => pool.Append(mesh));
    }
    {
      var pool = new GeometryPool {Layout = new VertexLayout {bUseTangents = true}};
      Assert.Throws<InvalidOperationException>(() => pool.Append(mesh));
    }
    {
      var pool = new GeometryPool {
          Layout = new VertexLayout { texcoord0 = new TexcoordInfo {size = 2} } };
      Assert.Throws<InvalidOperationException>(() => pool.Append(mesh));
    }

    Object.DestroyImmediate(mesh);
  }

  [Test]
  public void TestAppendMeshFallback() {
    var mesh = new Mesh();
    mesh.vertices = new[] {Vector3.one};

    var pool = new GeometryPool {Layout = new VertexLayout {bUseColors = true}};
    Color32 white = Color.white;
    pool.Append(mesh, fallbackColor: white);
    Assert.AreEqual(white, pool.m_Colors[0]);

    Object.DestroyImmediate(mesh);
  }

  [Test]
  public void TestAppendMeshExtraData() {
    // Mesh has all the data but pool doesn't want any of it
    var mesh = new Mesh();
    mesh.vertices = new[] {Vector3.one};
    mesh.normals = new[] {Vector3.up};
    mesh.SetUVs(0, new List<Vector2> { Vector2.zero });
    mesh.colors = new[] {Color.white};
    mesh.tangents = new[] {Vector4.one};

    var pool = new GeometryPool {Layout = new VertexLayout()};
    // This should not throw
    pool.Append(mesh);

    Object.DestroyImmediate(mesh);

  }
}

}
