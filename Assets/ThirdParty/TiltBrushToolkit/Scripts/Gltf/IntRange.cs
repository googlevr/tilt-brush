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

namespace TiltBrushToolkit {

/// Range, half-open on the right
public struct IntRange {
  public static IntRange Union(IntRange? a, IntRange b) {
    return (a != null) ? Union(a.Value, b) : b;
  }

  public static IntRange Union(IntRange a, IntRange? b) {
    return (b != null) ? Union(a, b.Value) : a;
  }

  public static IntRange? Union(IntRange? a, IntRange? b) {
    if (a != null && b != null) {
      return Union(a.Value, b.Value);
    } else {
      return a ?? b;
    }
  }

  public static IntRange Union(IntRange a, IntRange b) {
    return new IntRange {
      min = (a.min < b.min) ? a.min : b.min,
      max = (a.max > b.max) ? a.max : b.max,
    };
  }

  public static bool operator ==(IntRange lhs, IntRange rhs) {
    return (lhs.min == rhs.min) && (lhs.max == rhs.max);
  }

  public static bool operator !=(IntRange lhs, IntRange rhs) {
    return !(lhs == rhs);
  }

  public override bool Equals(object obj) {
    return obj is IntRange && this == (IntRange)obj;
  }

  public override int GetHashCode() {
    return min.GetHashCode() ^ max.GetHashCode();
  }

  public override string ToString() {
    return string.Format("[{0}, {1})", min, max);
  }

  public int Size {
    // This works for the null case too
    get { return max - min; }
  }

  public int min, max;
}

}
