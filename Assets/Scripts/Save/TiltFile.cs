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
using System.Runtime.InteropServices;
using UnityEngine;

#if USE_DOTNETZIP
using ZipSubfileReader = ZipSubfileReader_DotNetZip;
using ZipLibrary = Ionic.Zip;
#else
using ZipSubfileReader = TiltBrush.ZipSubfileReader_SharpZipLib;
using ZipLibrary = ICSharpCode.SharpZipLibUnityPort.Zip;
#endif

namespace TiltBrush {

public class TiltFile {

  private const uint TILT_SENTINEL = 0x546c6974;   // 'tilT'
  private const uint PKZIP_SENTINEL = 0x04034b50;

  // These are the only valid subfile names for GetStream()
  public const string FN_METADATA = "metadata.json";
  public const string FN_METADATA_LEGACY = "main.json";  // used pre-release only
  public const string FN_SKETCH = "data.sketch";
  public const string FN_THUMBNAIL = "thumbnail.png";
  public const string FN_HI_RES = "hires.png";

  public const string THUMBNAIL_MIME_TYPE = "image/png";
  public const string TILT_MIME_TYPE = "application/vnd.google-tiltbrush.tilt";

    [StructLayout(LayoutKind.Sequential, Pack=1, Size=16)]
  private struct TiltZipHeader {
    public uint sentinel;
    public ushort headerSize;
    public ushort headerVersion;
    public uint unused1;
    public uint unused2;
  }
  public unsafe static ushort HEADER_SIZE = (ushort)sizeof(TiltZipHeader);
  public static ushort HEADER_VERSION = 1;

  /// Writes .tilt files and directories in as atomic a fashion as possible.
  /// Use in a using() block, and call Commit() or Rollback() when done.
  sealed public class AtomicWriter : IDisposable {
    private string m_destination;
    private string m_temporaryPath;
    private bool m_finished = false;

    private ZipLibrary.ZipOutputStream m_zipstream = null;

    public AtomicWriter(string path) {
      m_destination = path;
      m_temporaryPath = path + "_part";
      Destroy(m_temporaryPath);

      bool useZip;
      switch (DevOptions.I.PreferredTiltFormat) {
      case TiltFormat.Directory: useZip = false; break;
      case TiltFormat.Inherit: useZip = ! Directory.Exists(path); break;
      default:
      case TiltFormat.Zip: useZip = true; break;
      }
      if (useZip) {
        Directory.CreateDirectory(Path.GetDirectoryName(m_temporaryPath));
        FileStream tmpfs = new FileStream(m_temporaryPath, FileMode.Create, FileAccess.Write);
        var header = new TiltZipHeader {
          sentinel = TILT_SENTINEL,
          headerSize = HEADER_SIZE,
          headerVersion = HEADER_VERSION,
        };
        WriteTiltZipHeader(tmpfs, header);
        m_zipstream = new ZipLibrary.ZipOutputStream(tmpfs);
#if USE_DOTNETZIP
        // Ionic.Zip documentation says that using compression level None produces
        // zip files that cannot be opened with the default OSX zip reader.
        // But compression _method_ none seems fine?
        m_zipstream.CompressionMethod = ZipLibrary.CompressionMethod.None;
        m_zipstream.EnableZip64 = ZipLibrary.Zip64Option.Never;
#else
        m_zipstream.SetLevel(0); // no compression
        // Since we don't have size info up front, it conservatively assumes 64-bit.
        // We turn it off to maximize compatibility with wider ecosystem (eg, osx unzip).
        m_zipstream.UseZip64 = ZipLibrary.UseZip64.Off;
#endif
      } else {
        Directory.CreateDirectory(m_temporaryPath);
      }
    }

    private static void WriteTiltZipHeader(Stream s, TiltZipHeader header) {
      unsafe {
        // This doesn't work because we need a byte[] to pass to Stream
        // byte* bufp = stackalloc byte[sizeof(TiltZipHeader)];

        // This doesn't work because the type is also byte*, not byte[]
        // unsafe struct Foo { fixed byte buf[N]; }

        Debug.Assert(
            HEADER_SIZE == Marshal.SizeOf(header),
            "Reference types detected in TiltZipHeader");

        byte[] buf = new byte[HEADER_SIZE];
        fixed (byte* bufp = buf) {
          IntPtr bufip = (IntPtr)bufp;
          // Copy from undefined CLR layout to explicitly-defined layout
          Marshal.StructureToPtr(header, bufip, false);
          s.Write(buf, 0, buf.Length);
          // Need this if there are reference types, but in that case
          // there are other complications (like demarshaling)
          // Marshal.DestroyStructure(bufip, typeof(TiltZipHeader));
        }
      }
    }

    /// Returns a writable stream to an empty subfile.
    public Stream GetWriteStream(string subfileName) {
      Debug.Assert(! m_finished);
      if (m_zipstream != null) {
#if USE_DOTNETZIP
        var entry = m_zipstream.PutNextEntry(subfileName);
        entry.LastModified = System.DateTime.Now;
        // entry.CompressionMethod = DotNetZip.CompressionMethod.None; no need; use the default
        return new ZipOutputStreamWrapper_DotNetZip(m_zipstream);
#else
        var entry = new ZipLibrary.ZipEntry(subfileName);
        entry.DateTime = System.DateTime.Now;
        // There is such a thing as "Deflated, compression level 0".
        // Explicitly use "Stored".
        entry.CompressionMethod = (m_zipstream.GetLevel() == 0)
          ? ZipLibrary.CompressionMethod.Stored
          : ZipLibrary.CompressionMethod.Deflated;
        return new ZipOutputStreamWrapper_SharpZipLib(m_zipstream, entry);
#endif
      } else {
        Directory.CreateDirectory(m_temporaryPath);
        string fullPath = Path.Combine(m_temporaryPath, subfileName);
        return new FileStream(fullPath, FileMode.Create, FileAccess.Write);
      }
    }

    /// Raises exception on failure.
    /// On failure, existing file is untouched.
    public void Commit() {
      if (m_finished) { return; }
      m_finished = true;

      if (m_zipstream != null) {
        m_zipstream.Dispose();
        m_zipstream = null;
      }

      string previous = m_destination + "_previous";
      Destroy(previous);
      // Don't destroy previous version until we know the new version is in place.
      try { Rename(m_destination, previous); }
      // The *NotFound exceptions are benign; they happen when writing a new file.
      // Let the other IOExceptions bubble up; they probably indicate some problem
      catch (FileNotFoundException) {}
      catch (DirectoryNotFoundException) {}
      Rename(m_temporaryPath, m_destination);
      Destroy(previous);
    }

    public void Rollback() {
      if (m_finished) { return; }
      m_finished = true;

      if (m_zipstream != null) {
        m_zipstream.Dispose();
        m_zipstream = null;
      }

      Destroy(m_temporaryPath);
    }

    // IDisposable support

    ~AtomicWriter() { Dispose(); }
    public void Dispose() {
      if (! m_finished) { Rollback(); }
      GC.SuppressFinalize(this);
    }

    // Static API

    // newpath must not already exist
    private static void Rename(string oldpath, string newpath) {
      Directory.Move(oldpath, newpath);
    }

    // Handles directories, files, and read-only flags.
    private static void Destroy(string path) {
      if (File.Exists(path)) {
        File.SetAttributes(path, FileAttributes.Normal);
        File.Delete(path);
      } else if (Directory.Exists(path)) {
        RecursiveUnsetReadOnly(path);
        Directory.Delete(path, true);
      }
    }

    private static void RecursiveUnsetReadOnly(string directory) {
      foreach (string sub in Directory.GetFiles(directory)) {
        File.SetAttributes(Path.Combine(directory, sub), FileAttributes.Normal);
      }
      foreach (string sub in Directory.GetDirectories(directory)) {
        RecursiveUnsetReadOnly(Path.Combine(directory, sub));
      }
    }
  }

  private string m_Fullpath;

  public TiltFile(string fullpath) {
    m_Fullpath = fullpath;
  }

  private static TiltZipHeader ReadTiltZipHeader(Stream s) {
    byte[] buf = new byte[HEADER_SIZE];
    s.Read(buf, 0, buf.Length);
    unsafe {
      fixed (byte* bufp = buf) {
        return *(TiltZipHeader*)bufp;
      }
    }
  }

  /// Returns a readable stream to a pre-existing subfile,
  /// or null if the subfile does not exist,
  /// or null if the file format is invalid.
  public Stream GetReadStream(string subfileName) {
    if (File.Exists(m_Fullpath)) {
      // It takes a long time to figure out a file isn't a .zip, so it's worth the
      // price of a quick check up-front
      if (! IsHeaderValid()) {
        return null;
      }
      try {
        return new ZipSubfileReader(m_Fullpath, subfileName);
      } catch (ZipLibrary.ZipException e) {
        Debug.LogFormat("{0}", e);
        return null;
      } catch (FileNotFoundException) {
        return null;
      }
    }

    string fullPath = Path.Combine(m_Fullpath, subfileName);
    try {
      return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
    } catch (FileNotFoundException) {
      return null;
    }
  }

  public bool IsHeaderValid() {
    if (File.Exists(m_Fullpath)) {
      try {
        using (var stream = new FileStream(m_Fullpath, FileMode.Open, FileAccess.Read)) {
          var header = ReadTiltZipHeader(stream);
          if (header.sentinel != TILT_SENTINEL || header.headerVersion != HEADER_VERSION) {
            Debug.LogFormat("Bad .tilt sentinel or header: {0}", m_Fullpath);
            return false;
          }
          if (header.headerSize < HEADER_SIZE) {
            Debug.LogFormat("Unexpected header length: {0}", m_Fullpath);
            return false;
          }
          stream.Seek(header.headerSize - HEADER_SIZE, SeekOrigin.Current);
          if ((new BinaryReader(stream)).ReadUInt32() != PKZIP_SENTINEL) {
            Debug.LogFormat("Zip sentinel not found: {0}", m_Fullpath);
            return false;
          }
          return true;
        }
      } catch (UnauthorizedAccessException) {
        Debug.LogFormat("File does not have read permissions: {0}", m_Fullpath);
        return false;
      } catch (IOException) {
        // Might be a temporary thing (eg sharing violation); being conservative for now
        return false;
      }
    }

    if (Directory.Exists(m_Fullpath)) {
      // Directories don't have a header but we can do some roughly-equivalent
      // sanity-checking
      return (File.Exists(Path.Combine(m_Fullpath, FN_METADATA)) &&
              File.Exists(Path.Combine(m_Fullpath, FN_SKETCH)) &&
              File.Exists(Path.Combine(m_Fullpath, FN_THUMBNAIL)));
    }
    return false;
  }

}

}  // namespace TiltBrush
