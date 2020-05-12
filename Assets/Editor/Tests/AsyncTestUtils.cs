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
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace TiltBrush {

public static class AsyncTestUtils {
  /// Converts a block of async/await-using test code into something consumable
  /// from a [UnityTest]-style unit test.
  public static IEnumerator<Null> AsUnityTest(Func<Task> body) {
    return ToNullEnumerator(TaskRunOnUnity(body));
  }

  /// Converts a block of async/await-using test code into something consumable
  /// from a [UnityTest]-style unit test.
  public static IEnumerator<Null> AsUnityTest<T>(Func<Task<T>> body) {
    return ToNullEnumerator(TaskRunOnUnity(body));
  }

  /// Alternative to Task.Run()
  /// Ensures the task starts running on the Unity thread, as opposed to a thread pool.
  private static Task TaskRunOnUnity(Func<Task> body) {
    // Runs the start of the task now; hopefully we're on the Unity thread.
    // See https://devblogs.microsoft.com/pfxteam/task-run-vs-task-factory-startnew/
    // for another option.

    // Runs the intro portion of the task right now
    return body();
    // Returns immediately; runs the intro portion of the task from the event loop
    // return ((Func<Task>) (async () => { await Awaiters.NextFrame; await body();}))();
  }

  private static IEnumerator<Null> ToNullEnumerator(Task task) {
    while (!task.IsCompleted) {
      yield return null;
    }

    if (task.IsFaulted) {
      // Unwrap if unambiguous
      AggregateException outer = task.Exception;
      var inners = outer.InnerExceptions;
      Exception toThrow = (inners.Count == 1) ? inners[0] : outer;
      ExceptionDispatchInfo.Capture(toThrow).Throw();
    } else if (task.IsCanceled) {
      Assert.Fail("Task unexpectedly canceled");
    }
  }
}

}
