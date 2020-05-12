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
using UnityEngine;

namespace TiltBrush {

public class StrokePlaybackByTime : StrokePlayback {
  private LinkedListNode<Stroke> m_strokeNode;

  public LinkedListNode<Stroke> StrokeNode {
    get { return m_strokeNode; }
  }

  public void Init(LinkedListNode<Stroke> memoryObjectNode,
                   PointerScript pointer, CanvasScript canvas) {
    m_strokeNode = memoryObjectNode;
    BaseInit(memoryObjectNode.Value, pointer, canvas);
  }

  public override void ClearPlayback() {
    m_strokeNode = null;
    base.ClearPlayback();
  }

  protected override bool IsControlPointReady(PointerManager.ControlPoint controlPoint) {
    // TODO: API accepts time source function
    return (controlPoint.m_TimestampMs / 1000F) <= App.Instance.CurrentSketchTime;
  }
}

// Playback using stroke timestamps and supporting layering in time and timeline scrub.
//
// When moving forward we need strokes ordered by head timestamp so that we can schedule
// rendering, and when moving backward we need tail timestamp ordering so that we can
// delete the minimum set of affected strokes.  Our stroke accounting has them exist in
// one of three places:
//     1) an "unrendered" linked list (ordered by head timestamp)
//     2) assigned to a pointer for rendering
//     3) a "rendered" linked list (ordered by tail timestamp)
//
// For our use patterns, the insertions into the ordered linked lists are
// effectively O(1) complexity:
//     * strokes are added to rendered list in order after each is completed, so
//       insert will traverse at most num_pointers nodes
//     * strokes are added to unrendered list in order from the head of rendered list, so
//       insert will traverse at most num_overlapping_strokes nodes (i.e. number of
//       strokes overlapping in time with the inserted stroke)
public class ScenePlaybackByTimeLayered : IScenePlayback {
  // Array of pending stroke playbacks indexed by pointer.
  private StrokePlaybackByTime[] m_strokePlaybacks;
  private int m_lastTimeMs = 0;
  // List of unrendered strokes ordered by head timestamp, earliest first
  private SortedLinkedList<Stroke> m_unrenderedStrokes;
  // List of rendered strokes ordered by tail timestamp, latest first
  private SortedLinkedList<Stroke> m_renderedStrokes;
  private int m_strokeCount;
  private int m_maxPointerUnderrun = 0;
  private CanvasScript m_targetCanvas;

  public int MaxPointerUnderrun { get { return m_maxPointerUnderrun; } }
  public int MemoryObjectsDrawn { get { return 0; } } // unimplemented

  // Input strokes must be ordered by head timestamp
  public ScenePlaybackByTimeLayered(IEnumerable<Stroke> strokes) {
    m_targetCanvas = App.ActiveCanvas;
    m_unrenderedStrokes = new SortedLinkedList<Stroke>(
      (a, b) => (a.HeadTimestampMs < b.HeadTimestampMs),
      strokes);
    m_strokeCount = m_unrenderedStrokes.Count;
    m_renderedStrokes = new SortedLinkedList<Stroke>(
      (a, b) => (a.TailTimestampMs >= b.TailTimestampMs),
      new Stroke[] {});
    m_strokePlaybacks = new StrokePlaybackByTime[PointerManager.m_Instance.NumTransientPointers];
    for (int i = 0; i < m_strokePlaybacks.Length; ++i) {
      m_strokePlaybacks[i] = new StrokePlaybackByTime();
    }
  }

  // Continue drawing stroke for this frame, returning true if more rendering is pending.
  public bool Update() {
    int currentTimeMs = (int)(App.Instance.CurrentSketchTime * 1000);

    // Handle a jump back in time by resetting corresponding in-flight or completed strokes
    // to the undrawn state.
    if (currentTimeMs < m_lastTimeMs) {
      // any stroke in progress is implicated by rewind-- clear the stroke's playback
      foreach (var stroke in m_strokePlaybacks) {
        if (!stroke.IsDone()) {
          var pendingNode = stroke.StrokeNode;
          stroke.ClearPlayback();
          SketchMemoryScript.m_Instance.UnrenderStrokeMemoryObject(pendingNode.Value);
          m_unrenderedStrokes.Insert(pendingNode);
        }
      }
      // delete any stroke having final timestamp > new current time
      while (m_renderedStrokes.Count > 0 &&
             m_renderedStrokes.First.Value.TailTimestampMs > currentTimeMs) {
        var node = m_renderedStrokes.PopFirst();
        if (node.Value.IsVisibleForPlayback) {
          // TODO: remove SketchMemory cyclical dependency
          // TODO: sub-stroke unrender to eliminate needless geometry thrashing within a frame
          SketchMemoryScript.m_Instance.UnrenderStrokeMemoryObject(node.Value);
        }
        m_unrenderedStrokes.Insert(node);
      }
    }

    int pendingStrokes = 0;
    if (currentTimeMs != 0) {
      for (int i = 0; i < m_strokePlaybacks.Length; ++i) {
        var stroke = m_strokePlaybacks[i];
        // update any pending stroke from last frame
        stroke.Update();
        if (stroke.IsDone() && stroke.StrokeNode != null) {
          m_renderedStrokes.Insert(stroke.StrokeNode);
          stroke.ClearPlayback();
        }
        // grab and play available strokes, until one is left pending
        while (stroke.IsDone() && m_unrenderedStrokes.Count > 0 &&
               (m_unrenderedStrokes.First.Value.HeadTimestampMs <= currentTimeMs ||
                !m_unrenderedStrokes.First.Value.IsVisibleForPlayback)) {
          var node = m_unrenderedStrokes.PopFirst();
          if (node.Value.IsVisibleForPlayback) {
            stroke.Init(node, PointerManager.m_Instance.GetTransientPointer(i), m_targetCanvas);
            stroke.Update();
            if (stroke.IsDone()) {
              m_renderedStrokes.Insert(stroke.StrokeNode);
              stroke.ClearPlayback();
            }
          } else {
            m_renderedStrokes.Insert(node);
          }
        }
        if (!stroke.IsDone()) {
          ++pendingStrokes;
        }
      }

      // check for pointer underrun
      int underrun = 0;
      foreach (var obj in m_unrenderedStrokes) {
        if (!obj.IsVisibleForPlayback) {
          continue;
        }
        if (obj.HeadTimestampMs <= currentTimeMs) {
          ++underrun;
        } else {
          break;
        }
      }
      m_maxPointerUnderrun = Mathf.Max(m_maxPointerUnderrun, underrun);
    }

    Debug.Assert(
      m_renderedStrokes.Count + pendingStrokes + m_unrenderedStrokes.Count == m_strokeCount);
    m_lastTimeMs = currentTimeMs;
    return !(m_unrenderedStrokes.Count == 0 && pendingStrokes == 0);
  }

  public void AddStroke(Stroke stroke) {
    // We expect call when user has completed stroke, so add to rendered list.  List
    // is sorted by end time and we expect new node to land at the head.
    m_renderedStrokes.Insert(stroke.m_PlaybackNode);
    ++m_strokeCount;
  }

  public void RemoveStroke(Stroke stroke) {
    // Only allowed for strokes in rendered or unrendered list.  In current use from ClearRedo,
    // it will always be unrendered.
    Debug.Assert(stroke.m_PlaybackNode.List != null);
    stroke.m_PlaybackNode.List.Remove(stroke.m_PlaybackNode);  // O(1)
    --m_strokeCount;
  }

  public void QuickLoadRemaining() { App.Instance.CurrentSketchTime = float.MaxValue; }
}

}  // namespace TiltBrush
