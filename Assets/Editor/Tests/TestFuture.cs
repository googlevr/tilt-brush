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
using System.Collections;
using System.Collections.Generic;
using System.Threading;

using NUnit.Framework;
using UnityEngine.TestTools;

using static TiltBrush.AsyncTestUtils;

namespace TiltBrush {

// Subclass so we can inspect the "done" state
internal class MyFuture : Future<List<int>> {
  public bool IsDone { get { return m_state == State.Done; } }
  public MyFuture(Func<List<int>> computation, Action<List<int>> cleanupFunction = null)
    : base(computation, cleanupFunction) {}
}

internal class TestFuture {
  const int NUM_ITERATIONS = 1;
  const int SLEEP_TIME = 10;

  static List<int> FunctionSuccess() {
    Thread.Sleep(SLEEP_TIME);
    return new List<int> { 3 };
  }

  static List<int> FunctionFailure() {
    throw new InvalidOperationException("I failed");
  }

  static T Force<T>(Future<T> f) where T : class {
    T value;
    while (! f.TryGetResult(out value)) {}
    return value;
  }

  [Test]
  public void TestFutureSuccess() {
    var f = new MyFuture(FunctionSuccess);
    Assert.AreEqual(Force(f)[0], 3);
  }

  [Test]
  public void TestFutureFailure() {
    var f = new MyFuture(FunctionFailure);
    Assert.Throws<FutureFailed>(() => Force(f));
  }

  [Test]
  public void TestFutureDisposalException() {
    var f = new MyFuture(FunctionSuccess);
    f.Close();
    Assert.Throws<ObjectDisposedException>(() => {
        f.TryGetResult(out _);
      });
  }

  [Test]
  public void TestFutureCleanupBefore() {
    object monitor = new object();
    for (int i = 0; i < NUM_ITERATIONS; ++i) {
      bool cleaned = false;
      int cleanupSum = 0;

      Action<List<int>> cleanup = intList => {
        lock (monitor) {
          cleaned = true;
          cleanupSum += intList[0];
          Monitor.PulseAll(monitor);
        }
      };

      // Close f before the computation finishes

      var f = new MyFuture(FunctionSuccess, cleanup);
      f.Close();

      lock (monitor) {
        while (! cleaned) {
          Assert.IsTrue(Monitor.Wait(monitor, SLEEP_TIME * 3), "Future not cleaning");
        }
      }

      Assert.IsTrue(f.IsDone);
      Assert.AreEqual(3, cleanupSum);
    }
  }

  [Test]
  public void TestFutureCleanupAfter() {
    for (int i = 0; i < NUM_ITERATIONS; ++i) {
      int cleanupSum = 0;

      // Close f after the computation finishes

      Action<List<int>> cleanup = intList => {
        cleanupSum += intList[0];
      };

      var f = new MyFuture(FunctionSuccess, cleanup);
      Force(f);
      Assert.IsTrue(f.IsDone);

      f.Close();
      Assert.AreEqual(3, cleanupSum);
    }
  }

  // Checks that Future.Awaiter returns the proper value whether or not
  // the Future has completed at the time of the await.
  [UnityTest]
  public IEnumerator TestAwaitFutureSuccess() => AsUnityTest(async () => {

    const int kValue = 3;
    var slowFuture = new Future<int>(() => { Thread.Sleep(20); return kValue; });
    Assert.AreEqual(kValue, await slowFuture);
    var fastFuture = new Future<int>(() => kValue);
    Thread.Sleep(50);  // plenty of time for fastFuture to complete
    Assert.AreEqual(kValue, await fastFuture);

  });

  class CustomException : Exception {}
  // Checks that Future.Awaiter throws the proper exception whether or not
  // the Future has completed at the time of the await.
  [UnityTest]
  public IEnumerator TestAwaitFutureFailure() => AsUnityTest(async () => {

    var failSlow = new Future<int>(() => {
      Thread.Sleep(20);
      throw new CustomException();
    });
    try {
      Assert.Fail($"Got {await failSlow} instead of exception");
    } catch (CustomException) { }

    var failFast = new Future<int>(() => throw new CustomException());
    try {
      Thread.Sleep(50);  // Give it time to complete
      Assert.Fail($"Got {await failFast} instead of exception");
    } catch (CustomException) { }

  });

}

}
