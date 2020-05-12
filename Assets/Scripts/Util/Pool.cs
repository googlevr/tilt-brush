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

namespace TiltBrush {

/// Pooled objects can be in several states:
///   New:  Newly-allocated by new T()
///   Used: In use
///   Free: On the freelist
///   Garbage: like Free, except not on the freelist -- it will be reclaimed at some point.
public interface IPoolable {
  /// Called when an instance is returned to the pool.
  /// To help catch errors, implementations may want to make the instance unusable.
  void OnPoolPut();

  /// Called when an object is taken from the freelist.
  /// This should put the instance into a state identical to new.
  void OnPoolGet();
}

/// Simple pool allocator
public class Pool<T>
    where T : class, IPoolable, new() {
  private Stack<T> m_free = new Stack<T>();
  private int m_maxFree;

  /// If maxFree is negative, the pool will hold onto all free instances indefinitely.
  public Pool(int maxFree = -1) {
    m_maxFree = maxFree;
  }

  /// Returns an instance of T which has no current users.
  public T Get() {
    T instance;
    lock (m_free) {
      // no TryPop yet
      if (m_free.Count == 0) {
        instance = null;
      } else {
        instance = m_free.Pop();
      }
    }

    if (instance == null) {
      // Don't put the instance through an OnPoolPut() / OnPoolGet() pair, because
      // that would be a logical no-op and potentially expensive.
      return new T();
    } else {
      instance.OnPoolGet();
      return instance;
    }
  }

  // / A helper that calls Put() and nulls out the passed reference.
  public void PutAndClear(ref T instance) {
    T tmp = instance;
    instance = null;
    Put(tmp);
  }

  /// Pass an instance that is no longer used or referenced by anyone.
  public void Put(T instance) {
    instance.OnPoolPut();
    lock (m_free) {
      if (m_maxFree < 0 || m_free.Count < m_maxFree) {
        m_free.Push(instance);
      }
    }
  }
}
} // namespace TiltBrush
