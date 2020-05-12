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

using System;
using System.Text.RegularExpressions;

namespace TiltBrushToolkit {

public struct Version {
  public int major;
  public int minor;

  public static Version Parse(string value) {
    Match match = Regex.Match(value, @"^([0-9]+)\.([0-9]+)");
    if (!match.Success) { throw new ArgumentException("Cannot parse"); }
    return new Version {
      major = int.Parse(match.Groups[1].Value),
      minor = int.Parse(match.Groups[2].Value)
    };
  }

  // The rest of this is boilerplate

  public static bool operator <(Version lhs, Version rhs) {
    if (lhs.major != rhs.major) {
      return lhs.major < rhs.major;
    }
    return lhs.minor < rhs.minor;
  }

  public static bool operator <=(Version lhs, Version rhs) {
    return (lhs < rhs || lhs == rhs);
  }

  public static bool operator >=(Version lhs, Version rhs) {
    return (lhs > rhs || lhs == rhs);
  }

  public static bool operator >(Version lhs, Version rhs) {
    if (lhs.major != rhs.major) {
      return lhs.major > rhs.major;
    }
    return lhs.minor > rhs.minor;
  }

  public static bool operator ==(Version lhs, Version rhs) {
    return (lhs.major == rhs.major && lhs.minor == rhs.minor);
  }

  public static bool operator !=(Version lhs, Version rhs) {
    return !(lhs == rhs);
  }

  public override string ToString() {
    return string.Format("{0}.{1}", major, minor);
  }

  public override bool Equals(object rhs) {
    return this == (Version)rhs;
  }

  public override int GetHashCode() {
    return 0;
  }
}

}