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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;
using UnityAsyncAwaitUtil;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush {

public static class TbTaskExtensions {
  /// Converts a result-less Task to an IEnumerator<Null>, aka a "bare" coroutine.
  /// This method should be used mainly as scaffolding while converting from
  /// coroutines to async/await.
  /// Pass:
  ///   optToken - The token (if any) that the Task was given for cancellation.
  ///     This will be used to propagate coroutine cancellation into the Task.
  ///     If the Task doesn't support cancellation, don't pass a token.
  ///     However, if the caller cancels the coroutine you'll get error spam. In this
  ///     case you should either rewrite the caller (who shouldn't rely on being able
  ///     cancel an uncancelable task) or add cancellation support to the Task.
  public static IEnumerator<Null> AsIeNull(this Task task, CancellationToken? optToken=null) {
    try {
      while (!task.IsCompleted) {
        yield return null;
      }
      if (task.IsFaulted) {
        var inners = task.Exception.InnerExceptions;
        if (inners.Count > 1) {
          Debug.LogError("Unable to convert all Task exceptions to Coroutine exceptions");
          for (int i = 1; i < inners.Count; ++i) {
            Debug.LogException(inners[i]);
          }
        }
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(inners[0]).Throw();
      }
    } finally {
      if (!task.IsCompleted) {
        // Extracting the CancellationToken from the task doesn't work, since the task
        // may be a promise to unwrap a Task<Task>, and the promise doesn't have a
        // cancellation token. Thus we have to rely on the user passing in a token.
        if (! (optToken is CancellationToken token)) {
          Debug.LogError("Cannot propagate coroutine Dispose() without a token");
        } else if (token.IsCancellationRequested) {
          // Already done!
        } else if (token.CanBeCanceled) {
          // CT doesn't have API to cancel the token. You can only cancel from the CTS.
          // Since AsIeNull() is intended to be temp scaffolding, let's try to match what the
          // final async code will look like and do it with just the CT. Threading through a CT
          // is common in async code, but threading through a CTS is definitely not.
          var cts = token.UglyGetCancellationTokenSource();
          Debug.Assert(cts != null);  // Must be, because CanBeCanceled == true
          cts.Cancel();
        } else {
          // Maybe no token set, or the token's cts is not cancelable.
          Debug.LogError("Cannot propagate coroutine Dispose() with non-cancelable token");
        }
      }
    }
  }

  static void RunOnUnityScheduler([CanBeNull] Action action) {
    if (action == null) { return; }
    if (SynchronizationContext.Current == SyncContextUtil.UnitySynchronizationContext) {
      action();
    } else {
      SyncContextUtil.UnitySynchronizationContext.Post(_ => action(), null);
    }
  }

  static void RunOnUnityScheduler<T>([CanBeNull] Action<T> action, T state) {
    if (action == null) { return; }
    if (SynchronizationContext.Current == SyncContextUtil.UnitySynchronizationContext) {
      action(state);
    } else {
      SyncContextUtil.UnitySynchronizationContext.Post(obj => action((T)obj), state);
    }
  }

  /// A definitely hacky way of getting the CancellationTokenSource for a CancellationToken
  private static CancellationTokenSource UglyGetCancellationTokenSource(
      this CancellationToken token) {
    object cts = token.GetType()
        .GetField("m_source", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
        .GetValue(token);
    return (CancellationTokenSource)cts;
  }

  /// A somewhat hacky but legit way of getting the CancellationToken for a Task.
  private static CancellationToken UglyGetCancellationToken(this Task task) {
    return new TaskCanceledException(task).CancellationToken;
  }

  /// Converts "async Task" to "async void".
  /// "async Task" keeps exceptions in the Task for future awaiters to consume.
  /// "async void" (in Unity) handles the exceptions by logging them to the console.
  /// Use this to consume the task result from non-async code.
  public static async void AsAsyncVoid(this Task task) {
    await task;
  }

  /// See AsAsyncVoid(Task)
  /// Success and failure callbacks will be executed on the Unity thread.
  public static async void AsAsyncVoid(
      this Task task, [CanBeNull] Action success, [CanBeNull] Action failure) {
    try {
      await task;
      failure = null;
      RunOnUnityScheduler(success);
    } finally {
      RunOnUnityScheduler(failure);
    }
  }

  /// See AsAsyncVoid(Task)
  /// Success and failure callbacks will be executed on the Unity thread.
  public static async void AsAsyncVoid<T>(
      this Task<T> task, [CanBeNull] Action<T> success, [CanBeNull] Action failure) {
    try {
      var result = await task;
      failure = null;
      RunOnUnityScheduler(success, result);
    } finally {
      RunOnUnityScheduler(failure);
    }
  }

  private static IEnumerator CompleteAfterIsDone(
      IEnumeratorAwaitExtensions.SimpleCoroutineAwaiter awaiter,
      DownloadHandler dh) {
    while (! dh.isDone) { yield return null; }
    awaiter.Complete(null);
  }

  /// Allows you to await www.downloadHandler.
  public static IEnumeratorAwaitExtensions.SimpleCoroutineAwaiter GetAwaiter(
      this DownloadHandler dh) {
    var awaiter = new IEnumeratorAwaitExtensions.SimpleCoroutineAwaiter();
    RunOnUnityScheduler(() => AsyncCoroutineRunner.Instance.StartCoroutine(
                            CompleteAfterIsDone(awaiter, dh)));
    return awaiter;
  }
}

} // namespace TiltBrush
