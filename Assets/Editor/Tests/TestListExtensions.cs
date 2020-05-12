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

using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using NUnit.Framework;

namespace TiltBrush {

internal class TestListExtensions {
  delegate float Thunk();
  static bool sm_checkPerformance = true;

  // Check that a is faster than b
  static void SpeedTester(Thunk a, Thunk b, int iters, string label, int warmup = 20) {
    // There is enough noise in this data that these asserts aren't reliable;
    // so only do them when testing performance.
    if (! sm_checkPerformance) { return; }

    // warm up JIT
    for (int i = 0; i < warmup; ++i) {
      a(); b();
    }
    var watch = new System.Diagnostics.Stopwatch();
    watch.Reset();
    watch.Start();
    for (int i = 0; i < iters; ++i) {
      a();
    }
    watch.Stop();
    var ta = watch.Elapsed;

    watch.Reset();
    watch.Start();
    for (int i = 0; i < iters; ++i) {
      b();
    }
    watch.Stop();
    var tb = watch.Elapsed;

    Debug.LogFormat("{0}: x{3:f4}   {1:e5} vs {2:e5}", label,
                    ta.TotalSeconds,
                    tb.TotalSeconds,
                    ta.TotalSeconds / tb.TotalSeconds);
    Assert.IsTrue(ta < tb, "{2}: Expected {0} < {1}", ta, tb, label);
  }

  [Test]
  [Ignore("This crashes Unity")]
  public void TestGetBackingArrayReferenceType() {
    var elt = new object();
    var list = new List<object> { elt };
    var backing = list.GetBackingArray();
    Assert.AreEqual(backing[0], elt);
  }

  [Test]
  public void TestGetBackingArray() {
    var list = new List<uint>();
    for (uint i = 0; i < 10; ++i) {
      list.Add(i * 3 + 2);
    }

    var backing = list.GetBackingArray();
    backing[2] = 0xDB1F117E;
    Assert.AreEqual(0xDB1F117E, list[2]);

    list[2] = 0xF00D;
    Assert.AreEqual(0xF00D, backing[2]);
  }

  [Test]
  public void TestSetBackingArray() {
    var array = new uint[] { 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89 };
    var list = new List<uint>(array);
    var list2 = new List<uint>();
    list2.SetBackingArray(array);
    Assert.AreEqual(list, list2);
  }

  [Test]
  public void TestSetCountHigher() {
    var list = new List<int>(5);
    list.SetCount(3);
    Assert.AreEqual(3, list.Count);
    Assert.AreEqual(5, list.Capacity);
  }

  [Test]
  public void TestSetCountAboveCapacity() {
    var list = new List<int>(5);
    list.SetCount(7);
    Assert.AreEqual(7, list.Count);
    Assert.IsTrue(list.Capacity >= 7);
  }

  [Test]
  public void TestSetCountLower() {
    var list = new List<int>();
    for (int i = 0; i < 10; ++i) list.Add(i);
    list.SetCount(0);
    Assert.AreEqual(0, list.Count);
    Assert.IsTrue(list.Capacity > 0);
  }

  [Test]
  public void TestAddRange() {
    // The GetBackingArray() version
    var list = new List<int>() { 0, 1, 2 };
    var array = new int[100];
    for (int i = 0; i < array.Length; ++i) array[i] = i + 100;
    list.AddRange(array, 5, 95);
    Assert.AreEqual(105, list[3]);
    Assert.AreEqual(199, list[97]);
    Assert.AreEqual(98, list.Count);
  }

  [Test]
  public void TestAddRange2() {
    // The fallback case
    var list = new List<int>() { 0, 1, 2 };
    var array = new int[10];
    for (int i = 0; i < array.Length; ++i) array[i] = i + 100;
    list.AddRange(array, 5, 1);
    Assert.AreEqual(105, list[list.Count-1]);
    list.AddRange(array, 0, 10);
    Assert.AreEqual(14, list.Count);
  }

  [Test]
  public void TestBadAddRange() {
    var list = new List<int>() { 0, 1, 2 };
    var array = new int[100];
    for (int i = 0; i < array.Length; ++i) array[i] = i + 100;
    Assert.That(() => list.AddRange(array, 50, 100), Throws.ArgumentException);
  }
  [Test]
  public void TestBadAddRange2() {
    // The fallback using .Skip().Take()
    var list = new List<int>() { 0, 1, 2 };
    var array = new int[10];
    for (int i = 0; i < array.Length; ++i) array[i] = i + 100;
    Assert.That(() => list.AddRange(array, 5, 10), Throws.ArgumentException);
  }

  [Test]
  public void TestSpeed_GetBackingArray() {
    var list = new List<Vector3>(100);
    for (int i = 0; i < list.Capacity; ++i) {
      list.Add(Vector3.one);
    }

    Thunk standard = () => {
      Vector3 sum = Vector3.zero;
      List<Vector3> mylist = list;
      int n = mylist.Count;
      for (int i = 0; i < n; ++i) {
        sum += mylist[i] * Vector3.Dot(mylist[i], mylist[i]);
      }
      return sum.x;
    };

    Thunk backing = () => {
      Vector3 sum = Vector3.zero;
      Vector3[] mylist = list.GetBackingArray();
      int n = mylist.Length;
      for (int i = 0; i < n; ++i) {
        sum += mylist[i] * Vector3.Dot(mylist[i], mylist[i]);
      }
      return sum.x;
    };

    SpeedTester(backing, standard, 20, "vs standard");
  }

  [Test]
  public void TestSpeed_AddRange() {
    // Break-even point is about 100
    Vector3[] source = new Vector3[120];
    List<Vector3> dest = new List<Vector3>(500);

    // Our new overload, that internally uses GetBackingArray
    Thunk backing = () => {
      List<Vector3> mydest = dest;
      mydest.Clear();
      mydest.AddRange(source, 5, source.Length-10);
      return 0;
    };

    // Uses linq (and internally optimized, maybe?)
    Thunk linq = () => {
      List<Vector3> mydest = dest;
      mydest.Clear();
      mydest.AddRange(source.Skip(5).Take(source.Length-10));
      return 0;
    };

    // Write it out by hand
    Thunk byhand = () => {
      Vector3[] mysource = source;
      List<Vector3> mydest = dest;
      mydest.Clear();
      int max = source.Length - 10;
      for (int i = 5; i < max; ++i) {
        mydest.Add(mysource[i]);
      }
      return 0;
    };

    int iters = 30;
    SpeedTester(backing, linq,    iters, "vs linq");
    SpeedTester(backing, byhand,  iters, "vs byhand");
  }

  [Test]
  public void TestSpeed_SetCountUp() {
    List<Vector3> list = new List<Vector3>(70);

    Thunk setcount = () => {
      list.SetCount(list.Capacity);
      list.Clear();
      return 0;
    };

    Thunk forloop = () => {
      var mylist = list;
      var num = mylist.Capacity;
      for (int i = 0; i < num; ++i) mylist.Add(default(Vector3));
      list.Clear();
      return 0;
    };

    SpeedTester(setcount, forloop, 10, "vs forloop");
  }

  private void TestConvertHelperGeneric<T>() {
    List<T> list = new List<T>(30);
    for (int i=0; i<10; ++i) { list.Add(default(T)); }
    Assert.AreEqual(list.Count, 10);
    Assert.AreEqual(list.Capacity, 30);

    var pub = ConvertHelper<List<T>, ListExtensions.PublicList<T>>.Convert(list);
    Assert.IsTrue(pub != null);
    Assert.IsTrue(ReferenceEquals(list.GetBackingArray(), pub.BackingArray));
    Assert.AreEqual(pub.Count, list.Count);
    Assert.AreEqual(pub.Capacity, list.Capacity);
  }

  [Test]
  public void TestConvertHelper() {
    TestConvertHelperGeneric<int>();
    TestConvertHelperGeneric<Vector3>();
  }

}  // class TestListExtensions
}  // namespace TiltBrush
