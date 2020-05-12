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
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.TestTools;

using static TiltBrush.AsyncTestUtils;

namespace TiltBrush {
internal class TestTbTaskExtensions {
  class MyException : Exception {}

  [UnityTest]
  public IEnumerator TestAsIeNullRethrows() {
    async Task WaitThenThrow() {
      await Awaiters.SecondsRealtime(.01f);
      throw new MyException();
    }
    using (var co = WaitThenThrow().AsIeNull()) {
      while (true) {
        try {
          if (!co.MoveNext()) {
            Assert.Fail("Coroutine finished without error");
          }
        } catch (MyException) {
          break;
        }
        yield return null;
      }
    }
  }

  private static async Task CancellableTask(CancellationToken ct) {
    for (int i = 0; i < 30; ++i) {
      ct.ThrowIfCancellationRequested();
      await Awaiters.NextFrame;
    }
    throw new TimeoutException();
  }

  [UnityTest]
  public IEnumerator TestAsIeNullDisposalSuccess() => AsUnityTest(async () => {
    var cts = new CancellationTokenSource();
    var ct = cts.Token;
    Assert.IsTrue(ct.CanBeCanceled);
    var task = CancellableTask(ct);
    // Check that AsIeNull successfully propagates the Dispose coming out of the using()
    using (var co = task.AsIeNull(ct)) {
      co.MoveNext();
      await Awaiters.NextFrame;
    }
    try {
      await task;
    } catch (OperationCanceledException) {
      return;
    }
    Assert.Fail("Task was not canceled in time (or at all)");
  });

  [UnityTest]
  public IEnumerator TestAsIeNullDisposalFailure() {
    // AsIeNull will try to propagate the Dispose, but that will fail.
    // Check that the failure detection works.
    LogAssert.Expect(LogType.Error, "Cannot propagate coroutine Dispose() without a token");
    Task longishTask = Task.Run(() => Thread.Sleep(100));
    using (var co = longishTask.AsIeNull()) {
      co.MoveNext();
      yield return null;
    }
  }

  [UnityTest]
  public IEnumerator TestAsAsyncVoidRunsSuccessCallback() {
    bool? success = null;
    Task.Run(() => {
      Thread.Sleep(25);
    }).AsAsyncVoid(success: () => success = true, failure: () => success = false);
    for (int i = 0; i < 100; ++i) {
      Thread.Sleep(1);
      yield return null;
      if (success is bool value) {
        Assert.AreEqual(true, value);
        yield break;
      }
    }
    Assert.Fail("Timeout");
  }

  [UnityTest]
  public IEnumerator TestAsAsyncVoidTRunsSuccessCallback() {
    const double expected = 1.0;
    double? result = null;
    Task.Run(() => {
      Thread.Sleep(25);
      return expected;
    }).AsAsyncVoid(success: dbl => result = dbl, failure: () => result = -1);
    for (int i = 0; i < 100; ++i) {
      Thread.Sleep(1);
      yield return null;
      if (result is double value) {
        Assert.AreEqual(expected, value);
        yield break;
      }
    }
    Assert.Fail("Timeout");
  }

  // This test works, but it produces an unavoidable LogException which causes
  // any concurrently-running tests to fail.
  // [UnityTest]
  public IEnumerator TestAsAsyncVoidRunsFailureCallback() {
    bool? success = null;
    Task.Run(() => {
      throw new MyException();
    }).AsAsyncVoid(success: () => { success = true; },
                   failure: () => { success = false; });
    for (int i = 0; i < 100; ++i) {
      Thread.Sleep(10);
      yield return null;
      if (success is bool value) {
        Assert.AreEqual(false, value);
        yield break;
      }
    }
    Assert.Fail("Success/Failure callbacks didn't fire");
  }
}
}
