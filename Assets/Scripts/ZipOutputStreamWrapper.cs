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

using ICSharpCode.SharpZipLibUnityPort.Zip;

namespace TiltBrush {

/// ZipOutputStreamWrapper solves the converse problem as ZipSubfileReader.
///
/// ZipOutputStream is a single Stream that can only be written to
/// within paired calls to PutNextEntry()/CloseEntry(). Closing or
/// disposing this stream closes the entire file.
///
/// This wrapper exposes a more standard Stream API; the constructor
/// calls PutNextEntry(), and Close() calls CloseEntry(). The caller
/// retains the reponsibility to close the entire .zip file, when they
/// are done appending files to it.
///
/// The caller must also be responsible for ensuring that only a single
/// ZipOutputStreamWrapper is created at a time, and that they are properly
/// disposed of.
public sealed class ZipOutputStreamWrapper_SharpZipLib : WrappedStream {
  ZipOutputStream m_wrappedZip;

  public ZipOutputStreamWrapper_SharpZipLib(ZipOutputStream wrapped, ZipEntry entry) {
    SetWrapped(wrapped, false);
    m_wrappedZip = wrapped;
    // ZipOutputStream will detect poorly-paired calls to
    // PutNextEntry()/CloseEntry(), so we don't have to check it ourselves.
    m_wrappedZip.PutNextEntry(entry);
  }

  public override void Close() {
    m_wrappedZip.CloseEntry();
    base.Close();
  }
}

public sealed class ZipOutputStreamWrapper_DotNetZip : WrappedStream {
  public ZipOutputStreamWrapper_DotNetZip(Ionic.Zip.ZipOutputStream wrapped) {
    SetWrapped(wrapped, false);
  }
}
}  // namespace TiltBrush
