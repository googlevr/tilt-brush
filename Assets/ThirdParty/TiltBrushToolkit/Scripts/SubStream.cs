// Copyright 2019 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;

namespace TiltBrushToolkit {

/// A read-only, seekable stream.
/// The underlying stream must also be readable and seekable.
public class SubStream : Stream {
  private static int Min(int val, long max) {
    return (val <= max) ? val : (int)max;
  }

  private Stream m_baseStream;
  private readonly long m_start;
  private readonly long m_length;

  /// Takes ownership of the passed Stream.
  public SubStream(Stream baseStream, long start, long length) {
    if (baseStream == null
        || !baseStream.CanSeek
        || !baseStream.CanRead) {
      throw new ArgumentException("baseStream");
    }
    if (start < 0) {
      throw new ArgumentOutOfRangeException("start");
    }
    if (length < 0 || start + length > baseStream.Length) {
      throw new ArgumentOutOfRangeException("length");
    }
    m_baseStream = baseStream;
    m_start = start;
    m_length = length;
    // Position = 0;    avoid virtual call in constructor
    m_baseStream.Position = m_start;
  }

  // Stream API

  protected override void Dispose(bool disposing) {
    if (disposing) {
      if (m_baseStream != null) {
        m_baseStream.Dispose();
        m_baseStream = null;
      }
    }
  }

  public override void Flush() { return; }

  public override int Read(byte[] buffer, int offset, int count) {
    long remaining = m_length - Position;
    // Stream API says that it is OK to seek beyond the end of a file
    if (remaining < 0) { return 0; }
    // Let the base stream handle all the error checking (eg, count < 0)
    return m_baseStream.Read(buffer, offset, Min(count, remaining));
  }

  public override long Seek(long offset, SeekOrigin seekOrigin) {
    long origin;
    if      (seekOrigin == SeekOrigin.Begin)   { origin = 0; }
    else if (seekOrigin == SeekOrigin.Current) { origin = Position; }
    else if (seekOrigin == SeekOrigin.End)     { origin = m_length; }
    else { throw new ArgumentException("origin"); }

    Position = origin + offset;
    return Position;
  }

  public override void SetLength(long value) {
    throw new NotImplementedException();
  }

  public override void Write(byte[] buffer, int offset, int count) {
    throw new NotImplementedException();
  }

  public override bool CanRead {
    get { return true; }
  }

  public override bool CanSeek {
    get { return true; }
  }

  public override bool CanWrite {
    get { return false; }
  }

  public override long Length {
    get { return m_length; }
  }

  public override long Position {
    get {
      long ret = m_baseStream.Position - m_start;
      if (ret < 0) {
        throw new InvalidOperationException("invalid state");
      }
      return ret;
    }
    set {
      if (value < 0) { throw new ArgumentOutOfRangeException("negative"); }
      m_baseStream.Position = m_start + value;
    }
  }
}

}
