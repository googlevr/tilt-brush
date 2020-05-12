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
using System.IO;

namespace TiltBrush {
public class WrappedStream : Stream {
  private Stream m_wrapped = null;
  private bool m_ownsStream = false;

  public void SetWrapped(Stream wrapped, bool ownsStream) {
    m_wrapped = wrapped;
    m_ownsStream = ownsStream;
  }

  public override void Close() {
    if (m_wrapped != null) {
      if (m_ownsStream) {
        m_wrapped.Close();
      }
      m_wrapped = null;
    }
    base.Close();
  }

  // https://msdn.microsoft.com/en-us/library/system.io.stream(v=vs.110).aspx
  //
  // When you implement a derived class of Stream, you must provide implementations for
  // the Read and Write methods. [...] The default implementations of ReadByte and
  // WriteByte create a new single-element byte array, and then call your
  // implementations of Read and Write. When you derive from Stream, we recommend that
  // you override these methods [...]
  //
  // You must also provide implementations of CanRead, CanSeek, CanWrite, Flush,
  // Length, Position, Seek, and SetLength.

  public override int Read(byte[] buffer, int offset, int count) {
    return m_wrapped.Read(buffer, offset, count);
  }
  public override int ReadByte() {
    return m_wrapped.ReadByte();
  }
  public override void Write(byte[] buffer, int offset, int count) {
    m_wrapped.Write(buffer, offset, count);
  }
  public override void WriteByte(byte value) {
    m_wrapped.WriteByte(value);
  }

  public override bool CanRead  { get { return m_wrapped.CanRead; } }
  public override bool CanSeek  { get { return m_wrapped.CanSeek; } }
  public override bool CanWrite { get { return m_wrapped.CanWrite; } }
  public override void Flush() { m_wrapped.Flush(); }
  public override long Length { get { return m_wrapped.Length; } }
  public override long Position {
    get { return m_wrapped.Position; }
    set { m_wrapped.Position = value; }
  }
  public override long Seek(long offset, SeekOrigin origin) {
    return m_wrapped.Seek(offset, origin);
  }
  public override void SetLength(long value) {
    m_wrapped.SetLength(value);
  }
}
}

