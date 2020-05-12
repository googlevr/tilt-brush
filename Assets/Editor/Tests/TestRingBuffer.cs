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

using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush {

internal class TestRingBuffer : MathTestUtils {

  [Test]
  public void TestDefaults() {
    RingBuffer<float> rbf = new RingBuffer<float>(10);
    Assert.AreEqual(10, rbf.Capacity);
    Assert.AreEqual(0, rbf.Count);
    Assert.IsTrue(rbf.IsEmpty);
    Assert.IsFalse(rbf.IsFull);
  }

  [Test]
  public void TestEnqueue() {
    RingBuffer<float> rbf = new RingBuffer<float>(3);

    Assert.AreEqual(0, rbf.Count);

    Assert.IsTrue(rbf.IsEmpty);
    Assert.IsFalse(rbf.IsFull);
    Assert.IsTrue(rbf.Enqueue(1.5f));
    Assert.AreEqual(1, rbf.Count);

    Assert.IsFalse(rbf.IsEmpty);
    Assert.IsFalse(rbf.IsFull);
    Assert.IsTrue(rbf.Enqueue(2.5f));
    Assert.AreEqual(2, rbf.Count);

    Assert.IsFalse(rbf.IsEmpty);
    Assert.IsFalse(rbf.IsFull);
    Assert.IsTrue(rbf.Enqueue(3.5f));
    Assert.AreEqual(3, rbf.Count);

    // Test Add() returns false if full.
    Assert.IsFalse(rbf.IsEmpty);
    Assert.IsTrue(rbf.IsFull);
    Assert.IsFalse(rbf.Enqueue(4.5f));

    Assert.AreEqual(1.5, rbf[0]);
    Assert.AreEqual(2.5, rbf[1]);
    Assert.AreEqual(3.5, rbf[2]);

    // Test Add() overwirte oldest value if full.
    Assert.IsFalse(rbf.IsEmpty);
    Assert.IsTrue(rbf.IsFull);
    Assert.IsTrue(rbf.Enqueue(4.5f, true));
    Assert.IsFalse(rbf.IsEmpty);
    Assert.IsTrue(rbf.IsFull);

    Assert.AreEqual(2.5, rbf[0]);
    Assert.AreEqual(3.5, rbf[1]);
    Assert.AreEqual(4.5, rbf[2]);

    Assert.IsTrue(rbf.Enqueue(5.5f, true));
    Assert.AreEqual(3.5, rbf[0]);
    Assert.AreEqual(4.5, rbf[1]);
    Assert.AreEqual(5.5, rbf[2]);

    Assert.IsTrue(rbf.Enqueue(6.5f, true));
    Assert.AreEqual(4.5, rbf[0]);
    Assert.AreEqual(5.5, rbf[1]);
    Assert.AreEqual(6.5, rbf[2]);

    Assert.IsTrue(rbf.Enqueue(7.5f, true));
    Assert.AreEqual(5.5, rbf[0]);
    Assert.AreEqual(6.5, rbf[1]);
    Assert.AreEqual(7.5, rbf[2]);
  }

  [Test]
  public void TestClear() {
    RingBuffer<float> rbf = new RingBuffer<float>(3);
    Assert.IsTrue(rbf.IsEmpty);
    Assert.IsFalse(rbf.IsFull);

    Assert.IsTrue(rbf.Enqueue(1.5f));
    Assert.IsTrue(rbf.Enqueue(2.5f));
    Assert.IsTrue(rbf.Enqueue(3.5f));

    Assert.IsFalse(rbf.IsEmpty);
    Assert.IsTrue(rbf.IsFull);
    Assert.AreEqual(3, rbf.Count);

    rbf.Clear();

    Assert.IsTrue(rbf.IsEmpty);
    Assert.IsFalse(rbf.IsFull);
    Assert.AreEqual(0, rbf.Count);
  }

  [Test]
  public void TestCopyFrom() {
    RingBuffer<float> r1 = new RingBuffer<float>(3);
    RingBuffer<float> r2 = new RingBuffer<float>(10);
    RingBuffer<float> r3 = new RingBuffer<float>(1);

    Assert.IsTrue(r1.Enqueue(1.5f));
    Assert.IsTrue(r1.Enqueue(2.5f));
    Assert.IsTrue(r1.Enqueue(3.5f));

    r2.CopyFrom(r1);
    r3.CopyFrom(r2);

    Assert.AreEqual(r1.IsFull, r2.IsFull);
    Assert.AreEqual(r1.IsEmpty, r2.IsEmpty);
    Assert.AreEqual(r1[0], r2[0]);
    Assert.AreEqual(r1[1], r2[1]);
    Assert.AreEqual(r1[2], r2[2]);

    Assert.AreEqual(r3.IsFull, r2.IsFull);
    Assert.AreEqual(r3.IsEmpty, r2.IsEmpty);
    Assert.AreEqual(r3[0], r2[0]);
    Assert.AreEqual(r3[1], r2[1]);
    Assert.AreEqual(r3[2], r2[2]);

    r2.Clear();

    Assert.AreEqual(r3.IsFull, r1.IsFull);
    Assert.AreEqual(r3.IsEmpty, r1.IsEmpty);
    Assert.AreEqual(r3[0], r1[0]);
    Assert.AreEqual(r3[1], r1[1]);
    Assert.AreEqual(r3[2], r1[2]);
  }

  [Test]
  public void TestCopyFromWrapped() {
    RingBuffer<float> r1 = new RingBuffer<float>(3);
    RingBuffer<float> r2 = new RingBuffer<float>(10);
    RingBuffer<float> r3 = new RingBuffer<float>(1);

    Assert.IsTrue(r1.Enqueue(1.5f, true));
    Assert.IsTrue(r1.Enqueue(2.5f, true));
    Assert.IsTrue(r1.Enqueue(3.5f, true));
    Assert.IsTrue(r1.Enqueue(4.5f, true));
    Assert.IsTrue(r1.Enqueue(5.5f, true));
    Assert.IsTrue(r1.Enqueue(6.5f, true));
    Assert.IsTrue(r1.Enqueue(7.5f, true));
    Assert.IsTrue(r1.Enqueue(8.5f, true));

    r2.CopyFrom(r1);
    r3.CopyFrom(r2);

    Assert.AreEqual(r1.IsFull, r2.IsFull);
    Assert.AreEqual(r1.IsEmpty, r2.IsEmpty);
    Assert.AreEqual(r1[0], r2[0]);
    Assert.AreEqual(r1[1], r2[1]);
    Assert.AreEqual(r1[2], r2[2]);

    Assert.AreEqual(r3.IsFull, r2.IsFull);
    Assert.AreEqual(r3.IsEmpty, r2.IsEmpty);
    Assert.AreEqual(r3[0], r2[0]);
    Assert.AreEqual(r3[1], r2[1]);
    Assert.AreEqual(r3[2], r2[2]);

    r2.Clear();

    Assert.AreEqual(r3.IsFull, r1.IsFull);
    Assert.AreEqual(r3.IsEmpty, r1.IsEmpty);
    Assert.AreEqual(r3[0], r1[0]);
    Assert.AreEqual(r3[1], r1[1]);
    Assert.AreEqual(r3[2], r1[2]);
  }

  [Test]
  public void TestDequeue() {
    RingBuffer<float> r1 = new RingBuffer<float>(3);

    Assert.IsTrue(r1.Enqueue(1.5f));
    Assert.IsTrue(r1.Enqueue(2.5f));
    Assert.IsTrue(r1.Enqueue(3.5f));

    Assert.IsTrue(r1.IsFull);
    Assert.IsFalse(r1.IsEmpty);

    float[] v = new float[3];
    Assert.AreEqual(3, r1.Dequeue(ref v));

    Assert.AreEqual(0, r1.Count);
    Assert.IsFalse(r1.IsFull);
    Assert.IsTrue(r1.IsEmpty);

    Assert.AreEqual(1.5f, v[0]);
    Assert.AreEqual(2.5f, v[1]);
    Assert.AreEqual(3.5f, v[2]);

    Assert.IsTrue(r1.Enqueue(4.5f));
    Assert.IsTrue(r1.Enqueue(5.5f));
    Assert.IsTrue(r1.Enqueue(6.5f));

    Assert.IsTrue(r1.IsFull);
    Assert.IsFalse(r1.IsEmpty);

    float[] v1 = new float[1];
    Assert.AreEqual(1, r1.Dequeue(ref v1));

    Assert.AreEqual(2, r1.Count);
    Assert.AreEqual(4.5f, v1[0]);

    Assert.AreEqual(1, r1.Dequeue(ref v1));
    Assert.AreEqual(1, r1.Count);
    Assert.AreEqual(5.5f, v1[0]);

    Assert.AreEqual(1, r1.Dequeue(ref v1));
    Assert.AreEqual(0, r1.Count);
    Assert.AreEqual(6.5f, v1[0]);

    Assert.IsFalse(r1.IsFull);
    Assert.IsTrue(r1.IsEmpty);

    Assert.AreEqual(0, r1.Dequeue(ref v1));
    Assert.AreEqual(0, r1.Count);
    Assert.AreEqual(6.5f, v1[0]);

    Assert.IsTrue(r1.Enqueue(7.5f, true));
    Assert.IsTrue(r1.Enqueue(8.5f, true));
    Assert.IsTrue(r1.Enqueue(9.5f, true));
    Assert.IsTrue(r1.Enqueue(10.5f, true));

    Assert.AreEqual(3, r1.Dequeue(ref v));
    Assert.AreEqual(8.5f, v[0]);
    Assert.AreEqual(9.5f, v[1]);
    Assert.AreEqual(10.5f, v[2]);

  }

  [Test]
  public void TestGetRange() {
    RingBuffer<float> r1 = new RingBuffer<float>(3);

    Assert.IsTrue(r1.Enqueue(4.5f));
    Assert.IsTrue(r1.Enqueue(1.5f));
    Assert.IsTrue(r1.Enqueue(2.5f));
    Assert.IsTrue(r1.Enqueue(3.5f, true));

    float[] v1 = new float[1];
    r1.GetRange(ref v1);
    Assert.AreEqual(1.5f, v1[0]);

    float[] v2 = new float[2];
    r1.GetRange(ref v2);
    Assert.AreEqual(1.5f, v2[0]);
    Assert.AreEqual(2.5f, v2[1]);

    float[] v3 = new float[3];
    r1.GetRange(ref v3);
    Assert.AreEqual(1.5f, v3[0]);
    Assert.AreEqual(2.5f, v3[1]);
    Assert.AreEqual(3.5f, v3[2]);

    r1.GetRange(ref v2, 1, 2);
    Assert.AreEqual(2.5f, v2[0]);
    Assert.AreEqual(3.5f, v2[1]);

    r1.GetRange(ref v1, 1, 1);
    Assert.AreEqual(2.5f, v1[0]);

    r1.GetRange(ref v1, 2, 1);
    Assert.AreEqual(3.5f, v1[0]);

    r1.GetRange(ref v1, 0, 1);
    Assert.AreEqual(1.5f, v1[0]);
  }

  [Test]
  public void TestEnumerators() {
    RingBuffer<float> r1 = new RingBuffer<float>(3);

    Assert.IsTrue(r1.Enqueue(4.5f));
    Assert.IsTrue(r1.Enqueue(1.5f));
    Assert.IsTrue(r1.Enqueue(2.5f));
    Assert.IsTrue(r1.Enqueue(3.5f, true));

    using (var e1 = r1.GetEnumerator()) {
      Assert.AreEqual(1.5, e1.Current);
      Assert.IsTrue(e1.MoveNext());
      Assert.AreEqual(2.5, e1.Current);
      Assert.IsTrue(e1.MoveNext());
      Assert.AreEqual(3.5, e1.Current);
      Assert.IsFalse(e1.MoveNext());
      e1.Reset();
      Assert.AreEqual(1.5, e1.Current);
      Assert.IsTrue(e1.MoveNext());
      Assert.AreEqual(2.5, e1.Current);
      Assert.IsTrue(e1.MoveNext());
      Assert.AreEqual(3.5, e1.Current);
      Assert.IsFalse(e1.MoveNext());
    }

    using (var e1 = r1.GetEnumerator(0, 3)) {
      Assert.AreEqual(1.5, e1.Current);
      Assert.IsTrue(e1.MoveNext());
      Assert.AreEqual(2.5, e1.Current);
      Assert.IsTrue(e1.MoveNext());
      Assert.AreEqual(3.5, e1.Current);
      Assert.IsFalse(e1.MoveNext());
      e1.Reset();
      Assert.AreEqual(1.5, e1.Current);
      Assert.IsTrue(e1.MoveNext());
      Assert.AreEqual(2.5, e1.Current);
      Assert.IsTrue(e1.MoveNext());
      Assert.AreEqual(3.5, e1.Current);
      Assert.IsFalse(e1.MoveNext());
    }

    using (var e1 = r1.GetEnumerator(1, 2)) {
      Assert.AreEqual(2.5, e1.Current);
      Assert.IsTrue(e1.MoveNext());
      Assert.AreEqual(3.5, e1.Current);
      Assert.IsFalse(e1.MoveNext());
      e1.Reset();
      Assert.AreEqual(2.5, e1.Current);
      Assert.IsTrue(e1.MoveNext());
      Assert.AreEqual(3.5, e1.Current);
      Assert.IsFalse(e1.MoveNext());
    }

    using (var e1 = r1.GetEnumerator(2, 1)) {
      Assert.AreEqual(3.5, e1.Current);
      Assert.IsFalse(e1.MoveNext());
      e1.Reset();
      Assert.AreEqual(3.5, e1.Current);
      Assert.IsFalse(e1.MoveNext());
    }

    using (var e1 = r1.GetEnumerator(1, 1)) {
      Assert.AreEqual(2.5, e1.Current);
      Assert.IsFalse(e1.MoveNext());
      e1.Reset();
      Assert.AreEqual(2.5, e1.Current);
      Assert.IsFalse(e1.MoveNext());
    }

    using (var e1 = r1.GetEnumerator(0, 1)) {
      Assert.AreEqual(1.5, e1.Current);
      Assert.IsFalse(e1.MoveNext());
      e1.Reset();
      Assert.AreEqual(1.5, e1.Current);
      Assert.IsFalse(e1.MoveNext());
    }
  }
}

}
