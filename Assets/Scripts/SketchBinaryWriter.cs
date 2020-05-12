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

namespace TiltBrush {

/// Helper for writing large quantities of binary data to a stream.
/// Tries to minimize memory allocation and garbage creation.
public class SketchBinaryWriter : IDisposable {
  Stream m_stream;
  byte[] m_buf16;
  byte[] m_bufBig;  // lazily-initialized

  /// Detaching the BaseStream or Disposing the Writer leaves the stream in a consistent state.
  /// SketchBinaryWriter also doesn't (currently) do any write-caching/combining.
  public Stream BaseStream {
    get { return m_stream; }
    set {
      m_stream = value;
    }
  }

  /// Pass:
  ///   stream - base stream; may be null.
  ///     Does not take ownership of the stream; it will not be closed upon Dispose().
  public SketchBinaryWriter(Stream stream) {
    m_stream = stream;
    m_buf16 = new byte[16];
  }

  public void Dispose() {
    BaseStream = null;
  }

  public void Color(Color color) {
    Float(color.r);
    Float(color.g);
    Float(color.b);
    Float(color.a);
  }

  public void Vec3(Vector3 v) {
    Float(v.x);
    Float(v.y);
    Float(v.z);
  }

  public void Quaternion(Quaternion v) {
    Float(v.x);
    Float(v.y);
    Float(v.z);
    Float(v.w);
  }

  public void Int32(int x) {
    UInt32((uint)x);
  }

  public void UInt32(uint x) {
    m_buf16[0] = (byte)(x /*>> 0*/);
    m_buf16[1] = (byte)(x >>  8);
    m_buf16[2] = (byte)(x >> 16);
    m_buf16[3] = (byte)(x >> 24);
    m_stream.Write(m_buf16, 0, 4);
  }

  public unsafe void Float(float x) {
    Int32(*(int*)&x);
  }

  public void Write(IntPtr src, long length) {
    if (m_bufBig == null) {
      m_bufBig = new byte[4096];
    }

    IntPtr cur = src;
    long remaining = length;
    while (remaining > 0) {
      int chunk = MathUtils.Min(remaining, m_bufBig.Length);
      System.Runtime.InteropServices.Marshal.Copy(cur, m_bufBig, 0, chunk);
      m_stream.Write(m_bufBig, 0, chunk);
      remaining -= chunk;
      cur = new IntPtr(cur.ToInt64() + chunk);
    }
  }

  /// Writes a list or array of unmanaged structs to the Stream.
  /// There is no length prefix; just the raw contents.
  /// Returns the number of bytes written.
  public long WriteRaw<T>(List<T> lst) where T : unmanaged {
    return WriteRaw(lst.GetBackingArray(), lst.Count);
  }

  public long WriteRaw<T>(T[] array, int count) where T : unmanaged {
    unsafe {
      long byteCount = sizeof(T) * ((long) count);
      fixed (T* ptr = array) {
        Write((IntPtr) ptr, byteCount);
      }
      return byteCount;
    }
  }

  /// Writes a list or array of unmanaged structs to the Stream.
  /// The format is:
  ///   int32 num  - The number of elements in the array
  ///   int32 size - The number of bytes per element
  ///   byte[]     - (num * size) bytes of data
  public void WriteLengthPrefixed<T>(List<T> lst) where T : unmanaged {
    unsafe {
      int count = lst.Count;
      Int32(count);
      Int32(sizeof(T));
      WriteRaw(lst);
    }
  }
}

} // namespace TiltBrush
