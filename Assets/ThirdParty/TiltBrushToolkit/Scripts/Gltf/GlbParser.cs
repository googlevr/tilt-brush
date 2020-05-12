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

public class GlbError : Exception {
  public GlbError(string message) : base(message) {}
}

/// Parses glb versions 1 and 2
public static class GlbParser {
  const UInt32 kFourCC_glTF = 0x46546c67;  // 'glTF'
  const UInt32 kFourCC_JSON = 0x4E4F534A;  // 'JSON'
  // FourCC generally space-pads these codes, but this is NUL-padded
  const UInt32 kFourCC_BIN_ = 0x004E4942;  // 'BIN\0'

  private static void Err(string fmt, params object[] args) {
    throw new GlbError(string.Format(fmt, args));
  }

  public struct Range {
    public long start;
    public long length;
  }

  /* Cheat sheet:

     Version 1:
       'glTF'
       uint32 version : 0x00000001
       uint32 glbLength : everything, including headers etc

       uint32 n : n % 4 == 0
       uint32 format : 0x00000000
       <n bytes of json>

       <remaining bytes are binary>

     Version 2:
       'glTF'
       uint32 version : 0x00000002
       uint32 glbLength : everything, including headers etc

       uint32 n : n % 4 == 0
       'JSON'
       <n bytes of json>

       uint32 n : n % 4 == 0
       'BIN\0'
       <n bytes of binary>
  */

  /// Throws GlbError on parse errors.
  public static Range GetJsonChunk(string glbPath) {
    using (BinaryReader reader = new BinaryReader(File.OpenRead(glbPath))) {
      if (reader.ReadUInt32() != kFourCC_glTF) {
        Err("magic");
      }
      UInt32 glbFormatVersion = reader.ReadUInt32();
      if (glbFormatVersion == 1 || glbFormatVersion == 2) {
        long glbLength = reader.ReadUInt32();
        if (glbLength > reader.BaseStream.Length) { Err("glb length"); }

        long jsonLength = reader.ReadUInt32();
        if (jsonLength < 0 || jsonLength > glbLength) { Err("json length"); }
        UInt32 jsonFormat = reader.ReadUInt32();
        if (jsonFormat != (glbFormatVersion == 1 ? 0x0 : kFourCC_JSON)) {
          Err("no 'JSON' chunk");
        }
        if ((jsonLength % 4) != 0) { Err("json length%4"); }
        return new Range { start = reader.BaseStream.Position, length = jsonLength };
      } else {
        throw new GlbError("glb format version");
      }
    }
  }

  /// Throws GlbError on parse errors.
  public static string GetJsonChunkAsString(string glbPath) {
    Range range = GetJsonChunk(glbPath);
    using (var stream = File.OpenRead(glbPath)) {
      stream.Position = range.start;
      byte[] buffer = new byte[range.length];
      stream.Read(buffer, 0, buffer.Length);
      return System.Text.Encoding.UTF8.GetString(buffer);
    }
  }

  /// Throws GlbError on parse errors.
  public static Range GetBinChunk(string glbPath) {
    using (BinaryReader reader = new BinaryReader(File.OpenRead(glbPath))) {
      if (reader.ReadUInt32() != kFourCC_glTF) {
        Err("magic");
      }
      UInt32 glbFormatVersion = reader.ReadUInt32();
      if (glbFormatVersion == 1 || glbFormatVersion == 2) {
        UInt32 glbLength = reader.ReadUInt32();
        if (glbLength > reader.BaseStream.Length) { Err("glb length"); }

        // Skip the 0th chunk, which is always json
        long jsonLength = reader.ReadUInt32();
        reader.BaseStream.Position += (4 + jsonLength);

        long binStart, binLength;
        if (glbFormatVersion == 1) {
          // length is implicit as "what's left over"
          binLength = glbLength - jsonLength - 20;
          binStart = reader.BaseStream.Position;
        } else {
          // length is stored explicitly
          binLength = reader.ReadUInt32();
          if ((binLength % 4) != 0) { Err("bin length%4"); }
          if (reader.ReadUInt32() != kFourCC_BIN_) { Err("no 'BIN ' chunk"); }
          binStart = reader.BaseStream.Position;
        }
        if (binLength < 0) { Err("bin length underflow"); }
        if (binStart + binLength > reader.BaseStream.Length) { Err("bin length overflow"); }

        return new Range { start = binStart, length = binLength };
      } else {
        throw new GlbError("glb format version");
      }
    }
  }

  // Returns null if this doesn't look like a glb
  public static uint? GetGlbVersion(string glbPath) {
    using (BinaryReader reader = new BinaryReader(File.OpenRead(glbPath))) {
      if (reader.ReadUInt32() != kFourCC_glTF) {
        return null;
      }
      return reader.ReadUInt32();
    }
  }
}

}
