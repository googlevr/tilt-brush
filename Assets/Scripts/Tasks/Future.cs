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
using System.Runtime.CompilerServices;
using System.Threading;

using UnityAsyncAwaitUtil;

namespace TiltBrush {

public class FutureFailed : Exception {
  public FutureFailed(string message) : base(message) {}
  public FutureFailed(string fmt, params System.Object[] args)
    : base(string.Format(fmt, args)) {}
  public FutureFailed(Exception inner, string fmt, params System.Object[] args)
    : base(string.Format(fmt, args), inner) {}
}

/// Future is a way of running some computation on another thread.
/// It may be awaited, but for new code consider using Task<>.
/// Awaiting unwraps the FutureFailed exception.
public class Future<T> {
  public enum State {
    Start,   // Before computation has been scheduled for execution
    Running, // Thread has started; computation is underway
    Done,    // Computation has finished successfully
    Error    // Computation (or cleanup!) threw an exception
  };

  // Public for "await" support only
  public struct Awaiter : INotifyCompletion {
    private readonly Future<T> m_future;

    public Awaiter(Future<T> future) {
      m_future = future;
    }

    public bool IsCompleted {
      get {
        if (m_future.m_closed) {
          // ObjectDisposed will be propagated when "await" tries to call GetResult()
          return true;
        }
        lock (m_future.m_lock) {
          return (m_future.m_state == State.Done || m_future.m_state == State.Error);
        }
      }
    }

    public T GetResult() {
      return m_future.GetResultForAwaiter();
    }

    void INotifyCompletion.OnCompleted(Action continuation) {
      // The names INotifyCompletion and OnCompleted are terrible. This method lets us know what
      // to do when the Future completes; it is not notification that the Future has completed.
      // This is called by "await" after it sees !IsCompleted and before suspending.
      var ctx = SynchronizationContext.Current ?? SyncContextUtil.UnitySynchronizationContext;
      lock (m_future.m_lock) {
        if (!IsCompleted) {  // common case
          if (m_future.m_continuations == null) {
            m_future.m_continuations = new List<(Action, SynchronizationContext)>();
          }
          m_future.m_continuations.Add((continuation, ctx));
        } else {  // racy uncommon case
          ctx.Post(_ => continuation(), null);
        }
      }
    }
  }

  protected State m_state = State.Start;
  private Object m_lock = new Object();
  private Exception m_error = null;
  private bool m_closed = false;
  private Action<T> m_cleanupFunction;
  private T m_result;
  private Thread m_thread = null;
  private List<(Action, SynchronizationContext)> m_continuations = null;

  /// computation -
  ///   Will be run to completion on another thread
  /// cleanupFunction -
  ///   If supplied, Close() will arrange to call this function
  ///   on the result of the computation.
  /// longRunning -
  ///   Pass "true" if the computation takes O(seconds) rather than O(milliseconds),
  ///   or if you need the ability to interrupt the task.
  public Future(Func<T> computation, Action<T> cleanupFunction=null, bool longRunning=false) {
    m_cleanupFunction = cleanupFunction;

    // This is a local function because it closes over local variable "computation"
    void ThreadFunction() {
      try {
        lock (m_lock) {
          m_state = State.Running;
        }
        T result = computation();
        bool needCleanup = false;
        lock (m_lock) {
          m_state = State.Done;
          if (m_closed) {
            needCleanup = true;
          } else {
            m_result = result;
          }
        }
        if (needCleanup && m_cleanupFunction != null) {
          m_cleanupFunction(result);
        }
      } catch (Exception e) {
        m_state = State.Error;
        m_error = e;
      }
      if (m_continuations != null) {
        List<(Action, SynchronizationContext)> continuations;
        lock (m_lock) {
          continuations = m_continuations;
          m_continuations = null;
        }
        foreach (var (continuation, ctx) in continuations) {
          ctx.Post(_ => continuation(), null);
        }
      }
    }

    if (longRunning) {
      m_thread = new Thread(ThreadFunction);
      m_thread.IsBackground = true;
      m_thread.Start();
    } else {
      m_thread = null;
      ThreadPool.QueueUserWorkItem(_ => ThreadFunction());
    }
  }

  /// Signal that the result of the Future is no longer wanted.
  /// If the Future is already computed, the cleanup function will be called
  /// on the current thread. Otherwise, the cleanup function will be
  /// called on the worker thread once the computation finishes.
  ///
  /// It is an error to call TryGetResult() after closing the Future.
  public void Close(bool interrupt=false) {
    bool wasDone;
    lock (m_lock) {
      if (m_closed) {
        return;
      }
      m_closed = true;
      wasDone = (m_state == State.Done);

      if (interrupt) {
        if (m_thread == null) {
          UnityEngine.Debug.LogError("Can only interrupt future if longRunning=true");
        } else {
          m_thread.Interrupt();
        }
      }
    }
    if (wasDone && m_cleanupFunction != null) {
      // Can use m_result because it will no longer change asynchronously
      m_cleanupFunction(m_result);
    }
  }

  /// Returns false and sets result = default(T) if computation is not done yet.
  /// Otherwise returns true and sets result = computation result
  ///
  /// Raises FutureFailed if the computation did not complete.
  /// Raises ObjectDisposedException if Close() has been called.
  public bool TryGetResult(out T result) {
    lock (m_lock) {
      if (m_closed) {
        throw new ObjectDisposedException("Future");
      }
      switch (m_state) {
      default:
      case State.Start:
      case State.Running:
        result = default(T);
        return false;
      case State.Done:
        result = m_result;
        return true;
      case State.Error:
        throw new FutureFailed(m_error, "Incomplete");
      }
    }
  }

  // Accessible for "await" support only
  public Awaiter GetAwaiter() {
    return new Awaiter(this);
  }

  private T GetResultForAwaiter() {
    lock (m_lock) {
      if (m_closed) {
        throw new ObjectDisposedException("Future");
      }
      switch (m_state) {
        default:
        case State.Start:
        case State.Running:
          throw new InvalidOperationException("Not IsCompleted");
        case State.Done:
          return m_result;
        case State.Error:
          System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(m_error).Throw();
          throw new Exception("internal error"); // not reached
      }
    }
  }
}

} // namespace TiltBrush
