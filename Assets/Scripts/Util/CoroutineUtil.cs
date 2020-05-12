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

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace TiltBrush {

public static class CoroutineUtil {

  /// This is a method to poll multiple coroutines in parallel and until they're all finished.
  /// The method can handle the case in which a subcoroutine somewhere inside delegates to another
  /// subcoroutine using the 'yield return Subcoroutine(...);' form.
  /// Note this does not support any YieldInstruction-based coroutines.
  public static IEnumerator<Null> CompleteAllCoroutines(IReadOnlyList<IEnumerator> coroutines) {
    // The coroutines are stored in stacks so that if a coroutine returns a new subcoroutine the
    // child coroutine gets finished first.
    int numCoroutines = coroutines.Count;
    var coroutineStacks = new List<Stack<IEnumerator>>(numCoroutines);
    for (int i = 0; i < numCoroutines; ++i) {
      var stack = new Stack<IEnumerator>();
      stack.Push(coroutines[i]);
      coroutineStacks.Add(stack);
    }

    while (true) {
      bool stillGoing = false;
      for (int i = 0; i < numCoroutines; ++i) {
        var stack = coroutineStacks[i];
        if (stack.Count == 0) {
          continue;
        }
        stillGoing = true;
        var cr = stack.Peek();
        // pop completed coroutines from their stack
        if (!cr.MoveNext()) {
          stack.Pop();
        }
        Debug.Assert(!(cr.Current is YieldInstruction),
            "CoroutineUtil.CompleteAllCoroutines() does not support YieldInstructions.");
        // returned coroutines should be pushed onto their stack.
        if (cr.Current is IEnumerator) {
          stack.Push(cr.Current as IEnumerator);
        }
      }
      if (!stillGoing) {
        break;
      }
      yield return null;
    }
  }

  /// Returns a coroutine that runs A to completion, then runs B to completion.
  public static IEnumerator<T> Sequence<T>(IEnumerator<T> a, IEnumerator<T> b) {
    while (a.MoveNext()) { yield return a.Current; }
    while (b.MoveNext()) { yield return b.Current; }
  }
}


/// This class is used to document that an IEnumerator is a "bare"
/// coroutine.  This is one which only yields null, as opposed to
/// Unity-style coroutines which can yield non-null values of type
/// AsyncOperation, IEnumerable, or IEnumerator.
public class Null {
#if false
  // Illustrates best practices for using Null.
  public static IEnumerator<Null> MyCoroutine() {
    yield return null;

    // Bare coroutines can only call other bare coroutines.
    // This can be checked at compile time:
    var someCoroutine = ...;
    while (someCoroutine.MoveNext()) {
      yield return someCoroutine.Current;
    }
  }
#endif

  protected Null() {}
}


/// This class is used to indicate a bare coroutine that yields only
/// because it wants to be cooperatively time-sliced rather than because it
/// wants time to pass, physics to step, frame to advance, etc. Timesliced
/// coroutines should only call other timesliced coroutines.
///
/// It's not a hard distinction. The intent is that if a coroutine is OK
/// with being forcibly made synchronous (eg, by polling it in a tight
/// loop), it can document that by using Timeslice.
public class Timeslice : Null {
  protected Timeslice() {}
}

} // namespace TiltBrush