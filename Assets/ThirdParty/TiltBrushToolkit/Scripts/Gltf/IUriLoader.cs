// Copyright 2017 Google Inc. All rights reserved.
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

// Don't warn about using .Handle; see comment below about SafeHandle
#pragma warning disable 618

using System;
using System.IO;

namespace TiltBrushToolkit {

public class RawImage {
  public UnityEngine.Color32[] colorData;
  public int colorWidth;
  public int colorHeight;
  public UnityEngine.TextureFormat format;
}

// ----------------------------------------------------------------------


public interface IBufferReader : IDisposable {
  void Read(IntPtr destination, int readStart, int readSize);
  /// <summary>
  /// Reads bytes into the given byte buffer.
  /// </summary>
  /// <param name="destination">The byte buffer to write the results of the read into.</param>
  /// <param name="destinationOffset">The offset in <c>destination</c> to start writing.</param>
  /// <param name="readStart">The offset in the source buffer to start reading from.</param>
  /// <param name="readSize">The number of bytes to read.</param>
  void Read(byte[] destination, int destinationOffset, int readStart, int readSize);

  /// <summary>
  /// Returns the length of the content, if known in advance.
  /// </summary>
  /// <returns>The length of the content, or -1 if not known in advance.</returns>
  long GetContentLength();
}

public interface IUriLoader {
  /// <summary>
  /// Returns an object that can read bytes from the passed uri
  /// </summary>
  /// <param name="uri">The relative uri to load. A null argument means to return
  /// the glb binary chunk.</param>
  IBufferReader Load(string uri);

  /// <summary>
  /// Returns true if the method LoadAsImage() is supported.
  /// </summary>
  bool CanLoadImages();

  /// <summary>
  /// Returns the contents of the passed uri as a decoded image.
  /// Should raise NotSupportedException if not supported.
  /// </summary>
  /// <param name="uri">The relative uri to load</param>
  RawImage LoadAsImage(string uri);

#if UNITY_EDITOR
  /// <summary>
  /// Returns the contents of the passed uri as a Texture2D such that
  /// AssetDatabase.Contains(tex) == true; or null on failure.
  /// Possible causes of failure are:
  /// - uri lies outside of Assets/
  /// - uri does not reference a texture
  /// </summary>
  /// <param name="uri">The relative uri to load</param>
  UnityEngine.Texture2D LoadAsAsset(string uri);
#endif
}


// ----------------------------------------------------------------------

public class Reader : IBufferReader {
  private byte[] data;

  public Reader(byte[] data) {
    this.data = data;
  }

  public void Dispose() { }

  public void Read(IntPtr destination, int readStart, int readSize) {
    System.Runtime.InteropServices.Marshal.Copy(
        data, readStart, destination, readSize);
  }

  public void Read(byte[] dest, int destOffset, int readStart, int readSize) {
    Buffer.BlockCopy(data, readStart, dest, destOffset, readSize);
  }

  public long GetContentLength() {
    return data.Length;
  }
}

/// Performs reads just-in-time from a FileStream.
public class BufferedStreamLoader : IUriLoader {
  private string glbPath;
  private string uriBase;
  private int bufferSize;

  /// glbPath is the .gltf or .glb file being read; or null.
  public BufferedStreamLoader(string glbPath, string uriBase, int bufferSize=4096) {
    this.glbPath = glbPath;
    this.uriBase = uriBase;
    this.bufferSize = bufferSize;
  }

  public IBufferReader Load(string uri) {
    Stream stream;
    if (uri == null) {
      var range = GlbParser.GetBinChunk(glbPath);
      stream = new SubStream(File.OpenRead(glbPath), range.start, range.length);
    } else {
      stream = File.OpenRead(Path.Combine(uriBase, uri));
    }
    return new BufferedStreamReader(stream, bufferSize, stream.Length);
  }

  public bool CanLoadImages() { return false; }

  public RawImage LoadAsImage(string uri) { throw new NotSupportedException(); }

#if UNITY_EDITOR
  public UnityEngine.Texture2D LoadAsAsset(string uri) {
    if (uri == null) {
      // Texture data is embedded within the glb; thanks jfx@
      return null;
    }
    // If we're running in the editor, we can assume that:
    // - the current directory is the project directory
    // - uriBase is relative
    // - therefore, uriBase starts with Assets/
    string path = Path.Combine(uriBase, uri);
    return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(path);
  }
#endif
}

/// Takes an arbitrary seekable stream and reads it chunk-by-chunk,
/// because there's otherwise no way to read into an IntPtr.
///
/// PRO: doesn't need to buffer the entire .bin into memory
/// CON: still needs a tiny buffer to stage the data
/// CON: an extra copy
sealed class BufferedStreamReader : IBufferReader {
  Stream stream;
  byte[] tempBuffer;
  long contentLength;

  // Takes ownership of the stream
  public BufferedStreamReader(Stream stream, int bufferSize, long contentLength) {
    this.stream = stream;
    this.tempBuffer = new byte[bufferSize];
    this.contentLength = contentLength;
  }

  public void Dispose() {
    if (stream != null) { stream.Dispose(); }
  }

  public void Read(IntPtr destination, int readStart, int readSize) {
    stream.Seek(readStart, SeekOrigin.Begin);

    // Invariant: (destination + readSize) == (currentDestination + currentReadSize)
    int currentReadSize = readSize;
    // operator + (IntPtr, int) didn't come along until .net 4
    Int64 currentDestination = destination.ToInt64();
    while (currentReadSize > 0) {
      int numRead = stream.Read(tempBuffer, 0, Math.Min(currentReadSize, tempBuffer.Length));
      if (numRead <= 0) {
        break;
      }
      System.Runtime.InteropServices.Marshal.Copy(
          tempBuffer, 0, (IntPtr)currentDestination, numRead);

      currentReadSize -= numRead;
      currentDestination += numRead;
    }
  }

  public void Read(byte[] dest, int destOffset, int readStart, int readSize) {
    stream.Seek(readStart, SeekOrigin.Begin);
    while (readSize > 0) {
      int bytesRead = stream.Read(dest, destOffset, readSize);
      if (bytesRead <= 0) break;
      destOffset += bytesRead;
      readSize -= bytesRead;
    }
  }

  public long GetContentLength() {
    return contentLength;
  }
}

}
