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
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush {
public static class EnumerableExtensions {
  /// Like Python's enumerate()
  /// foreach (var (thing, index) in MyEnumerable.WithIndex()) {
  ///   Debug.Log($"{index}: {thing}");
  /// }
  public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> this_) {
    return Enumerable.Select(this_, (item, index) => (item, index));
  }

  /// Partitions an enumerable into elements that pass and fail a given predicate.
  /// If you need something more general, use ToLookup() or GroupBy() instead.
  public static (List<T> pass, List<T> fail) Partition<T>(
      this IEnumerable<T> this_, Func<T, bool> predicate) {
    var lookup = this_.ToLookup(predicate);
    // ToLookup is more eager than GroupBy, but it doesn't document exactly how eager;
    // there might be laziness left in the enumerables. So force them.
    return (lookup[true].ToList(), lookup[false].ToList());
  }

  // An equivalent of Array.IndexOf, which will work with any IEnumerable.
  public static int IndexOf<T>(this IEnumerable<T> this_, T item) where T: IComparable {
    var enumerator = this_.GetEnumerator();
    int index = 0;
    while (enumerator.MoveNext()) {
      if (item.Equals(item)) {
        return index;
      }
      index++;
    }
    return -1;
  }
}
} // namespace TiltBrush
