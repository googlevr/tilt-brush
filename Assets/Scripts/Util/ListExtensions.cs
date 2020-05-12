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
using System.Reflection;
using System.Runtime.InteropServices;

namespace TiltBrush {

#if !(NET_4_6 && UNITY_2017_1_OR_NEWER) // Causes type not to load in 2017.1 + .net 4.6
[StructLayout(LayoutKind.Explicit)]
#endif
public struct ConvertHelper<TFrom, TTo>
    where TFrom : class
    where TTo : class {
  /// Break C#'s strong type-safety guarantees to allow type-punning between
  /// unrelated managed types. Think (and test) carefully before using.
  ///
  /// WARNING: Casting List<T> to PublicList<T> will crash Unity if T
  /// is a reference type. Only use this for value types!
  static public TTo Convert(TFrom thing) {
    // Will probably work in IL2CPP, but currently only tested on Mono.
#if ENABLE_MONO
    var helper = new ConvertHelper<TFrom, TTo> { input = thing };
    unsafe {
      long* dangerous = &helper.before;
      dangerous[2] = dangerous[1];  // ie, output = input
    }
    return helper.output;
#else
    return null;
#endif
  }

#if !(NET_4_6 && UNITY_2017_1_OR_NEWER) // Causes type not to load in 2017.1 + .net 4.6
  [FieldOffset( 0)] public long  before;
  [FieldOffset( 8)] public TFrom input;
  [FieldOffset(16)] public TTo   output;
#else
  public long before;
  public TFrom input;
  public TTo output;
#endif
}

public static class ListExtensions {
  //
  // Helpers for accessing List<> internals.
  //

  class FieldLookup {
    string sm_name;
    Dictionary<Type, FieldInfo> sm_cache;
    public FieldLookup(string name) {
      sm_name = name;
      sm_cache = new Dictionary<Type, FieldInfo>();
    }
    public FieldInfo Get(Type t) {
      try {
        return sm_cache[t];
      } catch (KeyNotFoundException) {
        var field = sm_cache[t] = t.GetField(
          sm_name,
          System.Reflection.BindingFlags.NonPublic |
          System.Reflection.BindingFlags.GetField |
          System.Reflection.BindingFlags.Instance);
        return field;
      }
    }
  }

  static FieldLookup sm_items = new FieldLookup("_items");
  static FieldLookup sm_size = new FieldLookup("_size");

  // Do not use; this is marked "public" only so it can be unit tested.
  // Instead, Use the List<> extension methods (SetCount, AddRange, GetBackingArray).
  public class PublicList<T> {
    // Match the layout of Mono's and Microsoft's List.cs.
    private T[] _items = null;
    private int _size = 0;

    /// Assigning to BackingArray resets Count to Capacity.
    public T[] BackingArray {
      get {
        return _items;
      }
      set {
        if (value == null) {
          throw new ArgumentNullException("value");
        }
        _items = value;
        _size = value.Length;
      }
    }

    /// Unlike List<T>, Capacity is not writable. Mutate the original list instead.
    public int Capacity { get { return _items.Length; } }

    /// Unlike List<T>, Count is writable.
    public int Count {
      get { return _size; }
      set {
        if (value > Capacity) {
          throw new ArgumentException("count must be <= capacity");
        }
        _size = value;
      }
    }
  }

  /// WARNING: Use only if T is a value type.
  ///
  /// Returns the List's underlying array. Modifications made to it will
  /// be reflected in the list.
  ///
  /// The reflection version of this function is not especially fast; with
  /// the added overhead, the break-even point for iterating over a []
  /// instead of a List<> while doing a tiny amount of work (eg a vector
  /// dot + multiply) is about about 40 elements.
  /// TODO: investigate https://mattwarren.org/2016/12/14/Why-is-Reflection-slow/
  ///
  /// The real win is getting to use Array.Copy, pointers, etc.
  public static T[] GetBackingArray<T>(this List<T> list) {
    var pub = ConvertHelper<List<T>, PublicList<T>>.Convert(list);
    if (pub != null) {
      return pub.BackingArray;
    } else {
      return (T[])sm_items.Get(typeof(List<T>)).GetValue(list);
    }
  }

  public static void SetBackingArray<T>(this List<T> list, T[] value) {
    var pub = ConvertHelper<List<T>, PublicList<T>>.Convert(list);
    if (pub != null) {
      pub.BackingArray = value;
    } else {
      if (value == null) {
        throw new ArgumentNullException("value");
      }
      sm_items.Get(typeof(List<T>)).SetValue(list, value);
      sm_size.Get(typeof(List<T>)).SetValue(list, value.Length);
    }
  }

  /// WARNING: Use only if T is a value type.
  ///
  /// Adds items en masse to a List<>, relaxing some List invariants in exchange
  /// for speed:
  /// - If items are added, their values are undefined instead of default-initialized
  ///   (for List<Vector3>, break-even is about 50)
  /// - If items are removed, the reclaimed space _may_ not be default-initialized.
  ///   (in practice, RemoveRange() is always faster, so we never do this)
  ///
  public static void SetCount<T>(this List<T> list, int newCount) {
    if (newCount > list.Count) {
      if (newCount > list.Capacity) {
        // Same logic as List<>
        list.Capacity = Math.Max(Math.Max(list.Capacity * 2, 4), newCount);
      }
      var pub = ConvertHelper<List<T>, PublicList<T>>.Convert(list);
      if (pub != null) {
        pub.Count = newCount;
      } else {
        sm_size.Get(typeof(List<T>)).SetValue(list, newCount);
      }
    } else {
      list.RemoveRange(newCount, list.Count - newCount);
    }
  }

  /// Like List.AddRange(), but takes a subrange of the rhs.
  public static void AddRange<T>(this List<T> list, List<T> source, int start, int length) {
    AddRange(list, source.GetBackingArray(), start, length);
  }

  /// Like List.AddRange(), but takes a subrange of the rhs.
  public static void AddRange<T>(this List<T> list, T[] source, int start, int length) {
    if (length > 100) {
      int iDest = list.Count;
      list.SetCount(iDest + length);
      T[] dest = list.GetBackingArray();
      Array.Copy(source, start, dest, iDest, length);
    } else {
      int end = start + length;
      if (end < start || end > source.Length) {
        throw new ArgumentException("bad range");
      }
      for (int i = start; i < end; ++i) {
        list.Add(source[i]);
      }
    }
  }
}

}  // namespace TiltBrush
