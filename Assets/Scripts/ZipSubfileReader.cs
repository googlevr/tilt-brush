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

/// ZipFile allows multiple reader streams to be created from it.  All the
/// streams share the same underlying file handle, and share a mutex to
/// prevent concurrent use of the shared handle.  Because of the sharing,
/// closing a stream does not close the ZipFile or its file handle.
///
/// This class encapsulates a ZipFile and a single reader stream.  When the
/// stream is closed, the zipfile is also closed. The main intent is to
/// emulate the API one expects from a FileStream, but an additional
/// benefit is it allows true concurrent reading of the underlying file.
public sealed class ZipSubfileReader_SharpZipLib : TiltBrush.WrappedStream {
  ZipFile m_file;

  public ZipSubfileReader_SharpZipLib(string zipPath, string subPath) {
    ZipFile zipfile = new ZipFile(zipPath);
    try {
      ZipEntry entry = zipfile.GetEntry(subPath);
      if (entry == null) {
        throw new System.IO.FileNotFoundException("Cannot find subfile");
      }

      SetWrapped(zipfile.GetInputStream(entry), true);
      m_file = zipfile;
      zipfile = null;
    } finally {
      if (zipfile != null) {
        zipfile.Close();
      }
    }
  }

  public override void Close() {
    base.Close();
    if (m_file != null) {
      m_file.Close();
      m_file = null;
    }
  }
}

public sealed class ZipSubfileReader_DotNetZip : WrappedStream {
  Ionic.Zip.ZipFile m_file;
  public ZipSubfileReader_DotNetZip(string zipPath, string subPath) {
    var zipfile = new Ionic.Zip.ZipFile(zipPath);
    try {
      Ionic.Zip.ZipEntry entry = zipfile[subPath];
      if (entry == null) {
        throw new System.IO.FileNotFoundException("Cannot find subfile");
      }

      SetWrapped(entry.OpenReader(), true);
      m_file = zipfile;
      zipfile = null;
    } finally {
      if (zipfile != null) {
        zipfile.Dispose();
      }
    }
  }

  public override void Close() {
    base.Close();
    if (m_file != null) {
      m_file.Dispose();
      m_file = null;
    }
  }
}
}  // namespace TiltBrush
