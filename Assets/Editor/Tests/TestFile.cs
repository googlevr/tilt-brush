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
using System.IO;
using UnityEngine;
using NUnit.Framework;

namespace TiltBrush {

internal class TestFile {
  private Stream GetReadStream(string zipfile, string subfile, bool useSharpZipLib) {
    if (useSharpZipLib) {
      return new ZipSubfileReader_SharpZipLib(zipfile, subfile);
    } else {
      return new ZipSubfileReader_DotNetZip(zipfile, subfile);
    }
  }

  // Test SketchBinaryReader.Skip() on non-seekable stream
  [Test]
  public void TestSkip([Values(0u, 1u, 4u, 18u)] uint amount,
                       [Values(false, true)] bool useSharpZipLib) {
    string zipfile = Path.Combine(Application.dataPath, "Editor/Tests/TestData/data.zip");
    uint a, b;

    // Skip forward, then read
    using (Stream instream = GetReadStream(zipfile, "data.bin", useSharpZipLib)) {
      SketchBinaryReader reader = new SketchBinaryReader(instream);
      reader.UInt32();          // start test at non-zero offset
      bool ok = reader.Skip(amount);
      Assert.IsTrue(ok);
      a = reader.UInt32();
    }

    // Read forward, then read
    using (Stream instream = GetReadStream(zipfile, "data.bin", useSharpZipLib)) {
      SketchBinaryReader reader = new SketchBinaryReader(instream);
      byte[] buf = new byte[amount+4];
      instream.Read(buf, 0, buf.Length);
      b = reader.UInt32();
    }

    Assert.AreEqual(a, b);
  }

  // return n bytes of random data
  private byte[] MakeData(int n) {
    var r = new System.Random();
    byte[] ret = new byte[n];
    r.NextBytes(ret);
    return ret;
  }

  private unsafe void WriteBuf(SketchBinaryWriter w, byte[] buf) {
    fixed (byte* pbuf = buf) {
      w.Write((IntPtr)pbuf, buf.Length);
    }
  }

  // Test SketchBinaryWriter against BinaryWriter
  [Test]
  public unsafe void TestSketchBinaryWriter() {
    byte[] b0 = MakeData(0);
    byte[] b10 = MakeData(10);
    byte[] b5000 = MakeData(5000);

    using (var astr = new MemoryStream())
    using (var bstr = new MemoryStream())
    using (var aw = new BinaryWriter(astr, System.Text.Encoding.UTF8)) {
      var bw = new SketchBinaryWriter(bstr);
      Quaternion q1 = new Quaternion(4.1f, 4.2f, 4.3f, 4.4f);
      Quaternion q2 = new Quaternion(2.5f, 3.5f, 4.5f, 5.3f);

      aw.Write(0x7123abcd);
      aw.Write(0xdb1f117eu);
      aw.Write(-0f); aw.Write(-1f); aw.Write(1e27f);
      aw.Write(q1.x); aw.Write(q1.y); aw.Write(q1.z); aw.Write(q1.w);
      aw.Write(q2.x); aw.Write(q2.y); aw.Write(q2.z); aw.Write(q2.w);
      aw.Flush();
      astr.Write(b0, 0, b0.Length);
      astr.Write(b10, 0, b10.Length);
      astr.Write(b5000, 0, b5000.Length);

      bw.Int32(0x7123abcd);
      bw.UInt32(0xdb1f117eu);
      bw.Vec3(new Vector3(-0f, -1f, 1e27f));
      bw.Quaternion(q1);
      Quaternion* pq2 = &q2;
      bw.Write((IntPtr)pq2, sizeof(Quaternion));
      WriteBuf(bw, b0);
      WriteBuf(bw, b10);
      WriteBuf(bw, b5000);

      Assert.AreEqual(astr.ToArray(), bstr.ToArray());
    }
  }
}

}
