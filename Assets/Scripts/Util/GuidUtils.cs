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
using System.Linq;
using System.Security.Cryptography;

namespace TiltBrush {

/// RFC4122 functionality missing from System.Guid
public static class GuidUtils {
  public static readonly Guid kNamespaceDns = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
  public static readonly Guid kNamespaceUrl = new Guid("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
  public static readonly Guid kNamespaceOid = new Guid("6ba7b812-9dad-11d1-80b4-00c04fd430c8");
  public static readonly Guid kNamespaceX500 = new Guid("6ba7b814-9dad-11d1-80b4-00c04fd430c8");

  /// Returns guid of version 3 (namespace+name, md5) or version 5 (namespace+name, sha1)
  static Guid MakeNamespaceGuid(Guid ns, string name, int version) {
    HashAlgorithm hasher;
    if (version == 3) {
      hasher = MD5.Create();
    } else if (version == 5) {
      hasher = SHA1.Create();
    } else {
      throw new ArgumentException("version");
    }

    {
      byte[] nsBytesBigEndian = ns.ToByteArray();
      ByteswapGuid(nsBytesBigEndian);   // from little- to big- since the RFC uses big-
      hasher.TransformBlock(nsBytesBigEndian, 0, nsBytesBigEndian.Length, null, 0);
    }

    {
      byte[] utf8Name = System.Text.Encoding.UTF8.GetBytes(name);
      hasher.TransformFinalBlock(utf8Name, 0, utf8Name.Length);
    }

    byte[] hash16 = new byte[16];
    System.Array.Copy(hasher.Hash, 0, hash16, 0, hash16.Length);

    // Variant is RFC4122
    // octet 8, clock_seq_hi_and_reserved, top 3 bits to 1 0 x
    hash16[8] = (byte)((hash16[8] & ~0xc0) | 0x80);
    // Version
    // most-significant 4 bits of time_hi_and_version (octets 6-7)
    hash16[6] = (byte)((hash16[6] & ~0xf0) | (version << 4));

    ByteswapGuid(hash16);  // from big- to little- so it can be consumed by System.Guid
    return new Guid(hash16);
  }

  /// Converts guid in-place between big- and litte-endian guid bytestreams.
  /// This is useful because RFC4122 and most of the rest of the world use
  /// big-endian layout. System.Guid's byte[]-based api is a notable exception.
  public static void ByteswapGuid(byte[] guid) {
    // The layout is 4-2-2-1-1-1-1-1-1-1-1
    byte tmp;
    tmp = guid[0]; guid[0] = guid[3]; guid[3] = tmp;
    tmp = guid[1]; guid[1] = guid[2]; guid[2] = tmp;
    tmp = guid[4]; guid[4] = guid[5]; guid[5] = tmp;
    tmp = guid[6]; guid[6] = guid[7]; guid[7] = tmp;
  }

  /// Returns a deterministic RFC4122 "version 3" uuid.
  public static Guid Uuid3(Guid namespace_, string name) {
    return MakeNamespaceGuid(namespace_, name, 3);
  }

  /// Returns a deterministic RFC4122 "version 5" uuid.
  public static Guid Uuid5(Guid namespace_, string name) {
    return MakeNamespaceGuid(namespace_, name, 5);
  }

  /// Returns a Guid, given a Unity-serialized guid string
  public static Guid DeserializeFromUnity(string txt) {
    var bytes = new byte[16];
    for (int i = 0; i < 16; ++i) {
      string hex = txt.Substring(i*2, 2);
      var b = Convert.ToByte(hex, 16);
      // Swap nybbles (easier than swapping characters)
      b = (byte)(((b & 0x0f) << 4) | ((b & 0xf0) >> 4));
      bytes[i] = b;
    }
    return new Guid(bytes);
  }

  /// Returns a Unity-style serialized guid string, given a Guid
  public static string SerializeToUnity(Guid guid) {
    // On LE machines, Unity writes Guids out by taking the standard 4 2 2 8x1 struct,
    // casting to byte[16], then writing out each byte with its nybbles reversed.
    // The bytestream 01 23 45 67 ab cd ef... comes out as "10325476badcfe..."
    //
    // Depending on the provenance of the original guid, the 4 2 2 8x1 may be in
    // BE or LE byte layout, which affects the location of the "version" field.
    // All guids will be RFC4122 style. Most often you'll see the LE layout.
    //
    // We don't really care as long as DeserializeFromUnity and SerializeToUnity
    // are inverses of each other.
    byte[] bytes = guid.ToByteArray();
    for (int i = 0; i < bytes.Length; ++i) {
      byte b = bytes[i];
      // Swap nybbles
      bytes[i] = (byte)(((b & 0x0f) << 4) | ((b & 0xf0) >> 4));
    }
    return string.Join("", bytes.Select(b => b.ToString("x2")).ToArray());
  }
}

}  // namespace TiltBrush
