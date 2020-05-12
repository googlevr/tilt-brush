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

namespace TiltBrush {

// A generic ring buffer <T> container with some additional performance considerations.
//
// Capacity indicates the maximum number of elements that can be held and Count indicates how many
// elements the buffer is actively holding. When adding elements, one can optionally overwrite the
// oldest value when the buffer is full.
//
// Note that reference types allocated in the RingBuffer will not be released until they are
// overwritten or the RingBuffer itself goes out of scope. Dequeue does not guarantee the reference
// will be released.
//
// Implements IEnumerable for fast element scanning and accessors (GetRange/PopValues) to fill
// caller owned memory, which allows for value extraction without memory allocation.
//
public class RingBuffer<T> : System.Collections.Generic.IEnumerable<T> {
  private T[] m_buffer;

  // The first valid value
  private int m_head;

  // The next location into which a value will be written
  private int m_next;

  // -------------------------------------------------------------------------------------------- //
  // Constructor
  // -------------------------------------------------------------------------------------------- //

  public RingBuffer(int capacity) {
    m_buffer = new T[capacity + 1];
  }

  // -------------------------------------------------------------------------------------------- //
  // Properties
  // -------------------------------------------------------------------------------------------- //

  // Indicates that the buffer is holding no elements.
  public bool IsEmpty {
    get { return m_head == m_next; }
  }

  // Indicates that the buffer is holding the maximum number of elements (i.e. Count == Capacity).
  public bool IsFull {
    get { return (m_next + 1) % CapacityInternal == m_head; }
  }

  // Maximum number of elements that this container can hold
  public int Capacity {
    get { return m_buffer.Length - 1; }
  }

  // The number of elements in the buffer.
  public int Count {
    get { return ComputeLength(m_head, m_next); }
  }

  // Internal capacity is Capacity + 1, to account for sentinel.
  private int CapacityInternal {
    get { return m_buffer.Length; }
  }

  // -------------------------------------------------------------------------------------------- //
  // Public API
  // -------------------------------------------------------------------------------------------- //

  // Add an item to the end of the buffer, returns false if the buffer is already at capacity, true
  // if the value is inserted.
  public bool Enqueue(T item) {
    return Enqueue(item, false);
  }

  // When overwriteIfFull is true and the buffer is full, the oldest value will be overwritten and
  // Add() will always return true.
  public bool Enqueue(T item, bool overwriteIfFull) {
    if (IsFull) {
      if (!overwriteIfFull) {
        return false;
      }

      // Eject the oldest value.
      m_head = (m_head + 1) % CapacityInternal;
    }

    m_buffer[m_next++] = item;
    m_next %= CapacityInternal;

    return true;
  }

  // Remove and discard n items from the front of the buffer. If fewer than n items remain in the
  // buffer, an exception is thrown.
  public void Dequeue(int n) {
    if (n > Count) {
      throw new IndexOutOfRangeException();
    }
    m_head = (m_head + n) % CapacityInternal;
  }

  // Removes and returns the first element in the buffer.
  public T Dequeue() {
    if (IsEmpty) {
      throw new System.IndexOutOfRangeException();
    }
    T value = m_buffer[m_head];
    m_head = (m_head + 1) % CapacityInternal;
    return value;
  }

  // Removes rangeOut.Length elements from the front of the buffer, placing them into rangeOut.
  public int Dequeue(ref T[] rangeOut) {
    int len = System.Math.Min(rangeOut.Length, Count);
    for (int i = 0; i < len; i++) {
      rangeOut[i] = m_buffer[m_head];
      m_head = (m_head + 1) % CapacityInternal;
    }
    return len;
  }

  // Clear all values from the buffer.
  // Note that capacity is unchanged.
  public void Clear() {
    m_next = m_head = 0;
  }

  // Copies the source buffer, including exact capacity and contents.
  public void CopyFrom(RingBuffer<T> src) {
    if (src.CapacityInternal != CapacityInternal) {
      m_buffer = new T[src.CapacityInternal];
    }
    m_next = src.m_next;
    m_head = src.m_head;
    src.m_buffer.CopyTo(m_buffer, 0);
    Array.Copy(src.m_buffer, m_buffer, CapacityInternal);
  }

  // Fetch a single value. Throws an exception if the index is not in the range [0, Count).
  public T this[int index] {
    get { return m_buffer[ComputeCheckedIndex(index)]; }
    set { m_buffer[ComputeCheckedIndex(index)] = value; }
  }

  // Populates ret with the first ret.Length values from the buffer.
  public void GetRange(ref T[] ret) {
    GetRange(ref ret, 0, Math.Min(Count, ret.Length));
  }

  // Populates ret with the first count values from the buffer starting at ringIndex.
  public void GetRange(ref T[] ret, int ringIndex, int count) {
    System.Diagnostics.Debug.Assert(ret.Length <= count);
    System.Diagnostics.Debug.Assert(Count <= count);

    int start = ComputeCheckedIndex(ringIndex);
    int end = ComputeCheckedIndex(ringIndex + count - 1);
    if (start < end) {
      // Copies bytes, so get the element byte count
      int eltSize = Buffer.ByteLength(m_buffer) / m_buffer.Length;
      Buffer.BlockCopy(m_buffer, eltSize*start, ret, 0, eltSize * count);
    } else {
      for (int i = 0; i < count; i++) {
        ret[i] = m_buffer[(start + i) % CapacityInternal];
      }
    }
  }

  // Returns all values as an enumerator.
  System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
    return new Enumerator(this);
  }

  // Returns all values as an enumerator<T>.
  public System.Collections.Generic.IEnumerator<T> GetEnumerator() {
    return new Enumerator(this);
  }

  // Returns the specified range of values as an enumerator<T>.
  public System.Collections.Generic.IEnumerator<T> GetEnumerator(int start, int length) {
    return new Enumerator(this, start, length);
  }

  // -------------------------------------------------------------------------------------------- //
  // Private Helpers
  // -------------------------------------------------------------------------------------------- //

  // Computes an index into the ring buffer, taking into account the valid range within the buffer.
  // If an index is outside of the valid range, an IndexOutOfRangeException is thrown.
  // Note that the index returned is wrapped (e.g. modded by the capactiy).
  private int ComputeCheckedIndex(int index) {
    int safeIndex = (index + m_head) % CapacityInternal;
    if (safeIndex >= m_next && safeIndex < m_head) {
      throw new System.IndexOutOfRangeException();
    }
    return safeIndex;
  }

  private int ComputeIndex(int index) {
    return (index + m_head) % CapacityInternal;
  }

  // Computes the length of a ring buffer given the start and end index.
  // Note that these indices are expected to be wrapped (e.g. modded by the capacity).
  private int ComputeLength(int startIndex, int endIndex) {
    // If the end has wrapped around, it will be less than m_start.
    int end = (endIndex < startIndex) ? endIndex + CapacityInternal : endIndex;
    return end - startIndex;
  }

  // Enumerator implementation.
  private class Enumerator : System.Collections.Generic.IEnumerator<T> {
    private RingBuffer<T> m_owner;
    private int m_index;
    private int m_start;
    private int m_end;

    private Enumerator() { }

    public Enumerator(RingBuffer<T> owner) {
      m_owner = owner;
      m_index = m_owner.m_head;
      m_start = m_index;
      m_end = m_owner.m_next;
    }

    public Enumerator(RingBuffer<T> owner, int start, int length) {
      m_owner = owner;
      m_index = m_owner.ComputeCheckedIndex(start);
      m_start = m_index;
      // We may point off the end of the array, so we can't use ComputeCheckedIndex.
      m_end = m_owner.ComputeIndex(start + length);
    }

    public int Length {
      get { return m_owner.ComputeLength(m_start, m_end); }
    }

    public T Current {
      get { return m_owner.m_buffer[m_index]; }
    }

    object System.Collections.IEnumerator.Current {
      get { return m_owner.m_buffer[m_index]; }
    }

    public bool MoveNext() {
      m_index = (m_index + 1) % m_owner.CapacityInternal;
      int i = (m_index < m_start) ? m_index + m_owner.CapacityInternal : m_index;
      int end = (m_end < m_start) ? m_end + m_owner.CapacityInternal : m_end;
      return i < end;
    }

    public void Reset() {
      m_index = m_start;
    }

    public void Dispose() {
      m_owner = null;
    }
  }

}

} // namespace TiltBrush
