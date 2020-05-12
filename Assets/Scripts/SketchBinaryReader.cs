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
using System.Collections.Generic;

using UnityEngine;

namespace TiltBrush {

/// Helper for reading large quantities of binary data to a stream.
/// Tries to minimize memory allocation and garbage creation.
public class SketchBinaryReader : IDisposable {
  const long kGibibyte = 1024L * 1024L * 1024L;

  Stream m_stream;
  byte[] m_buf16;
  byte[] m_bufBig;  // lazily-initialized

  /// Detaching the BaseStream or Disposing the Reader leaves the stream in a consistent state.
  /// SketchBinaryReader also doesn't (currently) do any pre-reading or caching.
  public Stream BaseStream {
    get { return m_stream; }
    set {
      // If we did any caching, this would be the place to reset the stream position
      m_stream = value;
    }
  }

  /// Pass:
  ///   stream - base stream; may be null.
  ///     Does not take ownership of the stream; it will not be closed upon Dispose().
  ///
  public SketchBinaryReader(Stream stream) {
    this.m_stream = stream;
    this.m_buf16 = new byte[16];
    this.m_bufBig = null;
  }

  public void Dispose() {
    BaseStream = null;
  }

  public uint UInt32() {
    int n = m_stream.Read(m_buf16, 0, 4);
    System.Diagnostics.Debug.Assert(n == 4);
    unchecked {
      return (uint)(m_buf16[0] | m_buf16[1] << 8 | m_buf16[2] << 16 | m_buf16[3] << 24);
    }
  }

  public unsafe float Float() {
    int n = m_stream.Read(m_buf16, 0, 4);
    System.Diagnostics.Debug.Assert(n == 4);
    unchecked {
      uint i = (uint)(m_buf16[0] | m_buf16[1] << 8 | m_buf16[2] << 16 | m_buf16[3] << 24);
      return *(float*)&i;
    }
  }

  public int Int32() {
    return (int)UInt32();
  }

  public Vector3 Vec3() {
    // Inlining these doesn't seem to affect performance at all
    return new Vector3(Float(), Float(), Float());
  }

  public Color Color() {
    return new Color(Float(), Float(), Float(), Float());
  }

  public Quaternion Quaternion() {
    return new Quaternion(Float(), Float(), Float(), Float());
  }

  private void LazyCreateBigBuffer() {
    if (m_bufBig == null) {
      m_bufBig = new byte[4096];
    }
  }

  /// Returns false on failure.
  public bool ReadInto(IntPtr dest, long length) {
    LazyCreateBigBuffer();
    IntPtr cur = dest;
    long remaining = length;
    while (remaining > 0) {
      int numRead = m_stream.Read(m_bufBig, 0, MathUtils.Min(remaining, m_bufBig.Length));
      if (numRead <= 0) {
        return false;           // should never happen
      }
      System.Runtime.InteropServices.Marshal.Copy(m_bufBig, 0, cur, numRead);
      remaining -= numRead;
      // cur += numRead;  // not in .net 2.0
      cur = new IntPtr(cur.ToInt64() + numRead);
    }
    return true;
  }

  public bool Skip(uint length) {
    if (length == 0) {
      /* nothing */
      return true;
    } else if (m_stream.CanSeek) {
      m_stream.Position += length;
      return true;
    } else if (length <= m_buf16.Length) {
      return SlowSkip(m_stream, m_buf16, length);
    } else {
      LazyCreateBigBuffer();
      return SlowSkip(m_stream, m_bufBig, length);
    }
  }

  private static bool SlowSkip(Stream stream, byte[] buffer, uint length) {
    long remaining = length;
    while (remaining > 0) {
      int desired = (remaining > buffer.Length) ? buffer.Length : (int)remaining;
      int numRead = stream.Read(buffer, 0, desired);
      if (numRead <= 0) {
        return false;
      }
      remaining -= numRead;
    }
    return true;
  }

  // This code can't use generics, because it would require the "unmanaged" constraint
  // which isn't available until C# 7. Run Support/bin/codegen.py to update.

#if USING_CODEGEN_PY
  // EXPAND($T, Vector2, Vector3, Vector4, Color32, int)
  // See SketchBinaryWriter.Write(List<T>) for a description of the format.

  // Reads exactly N elements into an existing List.
  public unsafe bool ReadIntoExact(List<$T> lst, int expectedCount) {
    int count = Int32();
    int size = Int32();
    if (count != expectedCount) {
      Debug.LogErrorFormat("Error reading List<>: {0}", "count");
      return false;
    }
    if (size != sizeof($T)) {
      Debug.LogErrorFormat("Error reading List<>: {0}", "size");
      return false;
    }

    lst.SetCount(count);
    $T[] array = lst.GetBackingArray();
    Debug.Assert(array.Length >= count);

    fixed ($T* ptr = array) {
      return ReadInto((IntPtr)ptr, ((long)count) * size);
    }
  }
#else
# region codegen
  // $T = Vector2
  public unsafe bool ReadIntoExact(List<Vector2> lst, int expectedCount) {
    int count = Int32();
    int size = Int32();
    if (count != expectedCount) {
      Debug.LogErrorFormat("Error reading List<>: {0}", "count");
      return false;
    }
    if (size != sizeof(Vector2)) {
      Debug.LogErrorFormat("Error reading List<>: {0}", "size");
      return false;
    }

    lst.SetCount(count);
    Vector2[] array = lst.GetBackingArray();
    Debug.Assert(array.Length >= count);

    fixed (Vector2* ptr = array) {
      return ReadInto((IntPtr)ptr, ((long)count) * size);
    }
  }

  // $T = Vector3
  public unsafe bool ReadIntoExact(List<Vector3> lst, int expectedCount) {
    int count = Int32();
    int size = Int32();
    if (count != expectedCount) {
      Debug.LogErrorFormat("Error reading List<>: {0}", "count");
      return false;
    }
    if (size != sizeof(Vector3)) {
      Debug.LogErrorFormat("Error reading List<>: {0}", "size");
      return false;
    }

    lst.SetCount(count);
    Vector3[] array = lst.GetBackingArray();
    Debug.Assert(array.Length >= count);

    fixed (Vector3* ptr = array) {
      return ReadInto((IntPtr)ptr, ((long)count) * size);
    }
  }

  // $T = Vector4
  public unsafe bool ReadIntoExact(List<Vector4> lst, int expectedCount) {
    int count = Int32();
    int size = Int32();
    if (count != expectedCount) {
      Debug.LogErrorFormat("Error reading List<>: {0}", "count");
      return false;
    }
    if (size != sizeof(Vector4)) {
      Debug.LogErrorFormat("Error reading List<>: {0}", "size");
      return false;
    }

    lst.SetCount(count);
    Vector4[] array = lst.GetBackingArray();
    Debug.Assert(array.Length >= count);

    fixed (Vector4* ptr = array) {
      return ReadInto((IntPtr)ptr, ((long)count) * size);
    }
  }

  // $T = Color32
  public unsafe bool ReadIntoExact(List<Color32> lst, int expectedCount) {
    int count = Int32();
    int size = Int32();
    if (count != expectedCount) {
      Debug.LogErrorFormat("Error reading List<>: {0}", "count");
      return false;
    }
    if (size != sizeof(Color32)) {
      Debug.LogErrorFormat("Error reading List<>: {0}", "size");
      return false;
    }

    lst.SetCount(count);
    Color32[] array = lst.GetBackingArray();
    Debug.Assert(array.Length >= count);

    fixed (Color32* ptr = array) {
      return ReadInto((IntPtr)ptr, ((long)count) * size);
    }
  }

  // $T = int
  public unsafe bool ReadIntoExact(List<int> lst, int expectedCount) {
    int count = Int32();
    int size = Int32();
    if (count != expectedCount) {
      Debug.LogErrorFormat("Error reading List<>: {0}", "count");
      return false;
    }
    if (size != sizeof(int)) {
      Debug.LogErrorFormat("Error reading List<>: {0}", "size");
      return false;
    }

    lst.SetCount(count);
    int[] array = lst.GetBackingArray();
    Debug.Assert(array.Length >= count);

    fixed (int* ptr = array) {
      return ReadInto((IntPtr)ptr, ((long)count) * size);
    }
  }
# endregion
#endif
}

} // namespace TiltBrush
