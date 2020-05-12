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

using System.Text;

namespace TiltBrush {

static class TextUtils {
  static bool IsRfc5987AttrChar(char c) {
    return ( ('a' <= c && c <= 'z') ||
             ('A' <= c && c <= 'Z') ||
             ('0' <= c && c <= '9') ||
             c == '!' ||
             c == '#' ||
             c == '$' ||
             c == '&' ||
             c == '+' ||
             c == '-' ||
             c == '.' ||
             c == '^' ||
             c == '_' ||
             c == '`' ||
             c == '|' ||
             c == '~' );
  }

  /// Encode a string according to RFC 5987. The result will contain only the
  /// characters
  ///   a-z A-Z 0-9 !#$&+-.^_`|~
  /// The rest will be %-encoded (so, % may be returned too)
  public static string Rfc5987Encode(string s) {
    // see https://tools.ietf.org/html/rfc5987
    StringBuilder sb = new StringBuilder();
    sb.Append("UTF-8''");
    foreach (byte b in Encoding.UTF8.GetBytes(s)) {
      // Strictly speaking the rfc speaks in terms of characters, not encoded bytes.
      // We take advantage of these facts:
      // - attr-char characters, when encoded as utf-8, turn into single bytes
      //   whose value is the codepoint
      // - non attr-char characters, when encoded as utf-8, turn into one or more bytes,
      //   none of which have values identical to attr-char codepoints
      // Thus it's safe to cast the byte to a char
      if (IsRfc5987AttrChar((char)b)) {
        sb.Append((char)b);
      } else {
        sb.Append(string.Format("%{0:x2}", b));
      }
    }
    return sb.ToString();
  }
}

} // namespace TiltBrush
