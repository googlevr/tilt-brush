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

// Linked list with ordered values, allowing duplicates.
public class SortedLinkedList<T> {
  private LinkedList<T> m_list;
  System.Func<T, T, bool> m_lessThan;

  public SortedLinkedList(System.Func<T, T, bool> lessThan, IEnumerable<T> orderedInitialValues)  {
    m_list = new LinkedList<T>(orderedInitialValues);
    m_lessThan = lessThan;
  }

  public int Count { get { return m_list.Count; } }

  public LinkedList<T>.Enumerator GetEnumerator() { return m_list.GetEnumerator(); }

  public LinkedListNode<T> First { get { return m_list.First; } }

  public LinkedListNode<T> PopFirst() {
    var node = m_list.First;
    m_list.Remove(node);
    return node;
  }

  /// Insert new node.  In the case of nodes with identical ordering value, the new node
  /// is placed nearest to the head of the list (First).
  public void Insert(LinkedListNode<T> newNode) {
    System.Diagnostics.Debug.Assert(newNode.List == null);
    var node = m_list.First;
    while (node != null && m_lessThan(node.Value, newNode.Value)) {
      node = node.Next;
    }
    if (node == null) {
      m_list.AddLast(newNode);
    } else {
      m_list.AddBefore(node, newNode);
    }
  }
}

} // namespace TiltBrush