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

using UnityEngine;
using NUnit.Framework;

namespace TiltBrush {

// Also tests GeometryPool serialization
internal class TestBinaryReaderWriter {
  private static string TemporaryData {
    get {
      var dir = string.Format("{0}/../Temp/TestBinaryReaderWriter", Application.dataPath);
      Directory.CreateDirectory(dir);
      return dir;
    }
  }

  [Test]
  public void TestBaseStream() {
    // Quick test that BaseStream works as advertised

    MemoryStream stream = new MemoryStream();
    using (var writer = new SketchBinaryWriter(null)) {
      writer.BaseStream = stream;
      writer.Int32(12);
      Assert.IsTrue(stream.Position == 4);
      writer.BaseStream = null;
      Assert.Catch<Exception>(() => writer.Int32(12));
    }

    stream.Position = 0;
    using (var reader = new SketchBinaryReader(null)) {
      reader.BaseStream = stream;
      Assert.AreEqual(12, reader.Int32());
      reader.BaseStream = null;
      Assert.Catch<Exception>(() => reader.Int32());
    }
  }

  [Test]
  public void TestListRoundTrip() {
    // Test a list going into and out of the stream.
    List<Vector3> lst = MathTestUtils.RandomVector3List(20);

    MemoryStream stream = new MemoryStream();
    using (var writer = new SketchBinaryWriter(stream)) {
      writer.WriteLengthPrefixed(lst);
      writer.BaseStream = null;
    }

    stream.Position = 0;
    List<Vector3> lst2 = new List<Vector3>();
    using (var reader = new SketchBinaryReader(stream)) {
      Assert.IsTrue(reader.ReadIntoExact(lst2, lst.Count));
    }
    Assert.AreEqual(lst, lst2);
  }

  static MemoryStream GetStreamWithData(params int[] ints) {
    var ret = new MemoryStream();
    using (var writer = new SketchBinaryWriter(ret)) {
      foreach (var i in ints) { writer.Int32(i); }
    }
    ret.Position = 0;
    return ret;
  }

  [Test]
  public void TestInvalidStream_Size() {
    // Tests that we detect invalid element size properly
    UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

    MemoryStream stream = GetStreamWithData(0, 3);  // 0 elements, 3 bytes per element
    Assert.IsFalse(new SketchBinaryReader(stream).ReadIntoExact(new List<int>(), 0));

    stream.Position = 0;
    Assert.IsFalse(new SketchBinaryReader(stream).ReadIntoExact(new List<Vector4>(), 0));
  }

  [Test]
  public void TestInvalidStream_NumBytes() {
    // Test that a valid element size but absurd number of elements fails
    // Big enough that that it overflows an int32 when elementsize = 4
    const int kAbsurdNumElements = 800 * 1000 * 1000;
    UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
    MemoryStream stream = GetStreamWithData(kAbsurdNumElements, 4);
    Assert.IsFalse(new SketchBinaryReader(stream).ReadIntoExact(
                       new List<int>(), kAbsurdNumElements));
  }

  [Test]
  public void TestZeroSize() {
    // Test that valid element size and zero elements succeeds
    MemoryStream stream = GetStreamWithData(0, 4); // 0 elements, 4 bytes per element
    Assert.IsTrue(new SketchBinaryReader(stream).ReadIntoExact(new List<int>(), 0));

    stream.Position = 0;
    Assert.IsTrue(new SketchBinaryReader(stream).ReadIntoExact(new List<Color32>(), 0));
  }
}

}
