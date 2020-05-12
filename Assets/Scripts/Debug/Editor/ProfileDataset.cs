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
using System;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;

namespace TiltBrush {
  public class ProfileDataset {
    /// Data stored for a single sample from the profiler.
    public class FrameData {
      public float totalMilliseconds;
      public int sampleCount;
    }

    /// Data stored for a single function in a particular point in the call heirarchy.
    /// A function may get called several times from its parent, so a FrameData object is
    /// recorded once for each.
    public class SampledFunction {
      // Data from the profiler.
      public string name;

      // We hold a single FrameData for each frame that contains this function. Indexed by frame.
      public Dictionary<int, FrameData> frameData = new Dictionary<int, FrameData>();

      public int depth;
      public int id;

      // Calculated data.
      // Child functions are keyed by function name.
      public Dictionary<string, SampledFunction> children;

      public float mean;
      public float median;
      public float min;
      public float max;

      public float medianStdDev;

      // Median standard deviation as a percentage of the median.
      public float medianStdDevPc;

      // Sorted children and sort choices.
      public List<SampledFunction> sortedChildren;
      public int sortIndex = 0;
      public bool sortAscending = true;
    }

    private SampledFunction m_root;
    private List<SampledFunction> m_AllFunctions;

    public SampledFunction Root { get { return m_root; } }
    public List<SampledFunction> AllFunctions {  get { return m_AllFunctions; } }

    /// Load the profile dataset from the profiler, and analyse it.
    /// An optional callback can be called with the progress (0 - 1)
    public void AnalyzeProfilerData(Action<float> progressCallback) {
      IEnumerator<float> load = LoadDataFromProfiler();
      int count = 0;
      while (load.MoveNext()) {
        if (progressCallback != null && count++ > 10) {
          count = 0;
          progressCallback(load.Current * 0.75f);
        }
      }

      IEnumerator<float> calculate = CalculateMetrics();
      while (calculate.MoveNext()) {
        if (progressCallback != null && count++ > 10) {
          progressCallback(0.75f + calculate.Current * 0.25f);
          count = 0;
        }
      }
    }

    /// Gets the data from the profiler and stores it in our own tree structure.
    /// Uses the undocumented Unity Profiling classes.
    private IEnumerator<float> LoadDataFromProfiler() {
      m_AllFunctions = new List<SampledFunction>();

      int id = 0;
      m_root = new SampledFunction();
      m_root.depth = 0;
      m_root.id = id++;
      m_root.frameData = new Dictionary<int, FrameData>();
      m_root.name = "Frame";
      m_AllFunctions.Add(m_root);
      int first = ProfilerDriver.firstFrameIndex;
      int last = ProfilerDriver.lastFrameIndex;

      for (int frame = first; frame <= last; ++frame) {
        yield return Mathf.InverseLerp(first, last, frame);

        // The iterator is used to get profile data. It can be set to start on a particular thread
        // at a particular frame. When you call Next(bool enterchildren) it will move on to the
        // next sample. It contains the depth of the callstack for each function, which can
        // be used to work out the tree structure.
        var iterator = new ProfilerFrameDataIterator();
        iterator.SetRoot(frame, 0);
        m_root.frameData.Add(frame,
          new FrameData {totalMilliseconds = iterator.durationMS, sampleCount = 1});

        // A stack is used to keep a track of the functions further up the call stack.
        Stack<SampledFunction> stack = new Stack<SampledFunction>();
        SampledFunction current = m_root;
        stack.Push(m_root);
        while (iterator.Next(true)) {
          // If the new item has a greater depth, it must be the child of the current item, so
          // push it on the stack
          if (iterator.depth > current.depth) {
            stack.Push(current);
          }
          // If the new item has a lesser depth, we must spool off the stack until we match the
          // new depth.
          while (iterator.depth < current.depth) {
            current = stack.Pop();
          }

          if (stack.Peek().children == null) {
            stack.Peek().children = new Dictionary<string, SampledFunction>();
          }

          // We check to see if there is an existing item with the new name in the children of
          // the item at the top of the stack - if there is, we will just add to it.
          SampledFunction newFn;
          if (!stack.Peek().children.TryGetValue(iterator.name, out newFn)) {
            // Otherwise, we must create a new one and add it to the children of the itam at the
            // top of the stack.
            newFn = new SampledFunction();
            newFn.id = id++;
            newFn.name = iterator.name;
            newFn.depth = iterator.depth;

            stack.Peek().children.Add(newFn.name, newFn);
            // We also add it to the list of all items here.
            m_AllFunctions.Add(newFn);
          }
          current = newFn;

          // Get an existing framedata for this frame, or create a new one.
          FrameData frameData;
          if (!current.frameData.TryGetValue(frame, out frameData)) {
            frameData = new FrameData();
            current.frameData.Add(frame, frameData);
          }

          // Update the frame data.
          frameData.totalMilliseconds += iterator.durationMS;
          frameData.sampleCount++;
        }
      }
    }

    /// Calculates a bunch of metrics on the data. median is calculated as well as mean because mean
    /// isn't very useful if you have any large outlying values.
    /// Standard deviation is also taken from the median.
    private IEnumerator<float> CalculateMetrics() {
      int index = 0;
      m_root.sortedChildren = m_root.children.Values.ToList();
      foreach (var current in m_AllFunctions) {
        yield return Mathf.InverseLerp(0, m_AllFunctions.Count, index++);

        // The children in the dictionary are converted into a list for sorting.
        if (current.children != null) {
          current.sortedChildren = current.children.Values.ToList();
        } else {
          current.sortedChildren = new List<SampledFunction>();
        }

        if (current.frameData.Count == 0) {
          continue;
        }

        current.min = float.MaxValue;
        current.max = float.MinValue;
        current.mean = 0;

        // Sort the frames to get min, max, and median.
        FrameData[] sortedFrames =
          current.frameData.Values.OrderBy(x => x.totalMilliseconds).ToArray();
        current.min = sortedFrames[0].totalMilliseconds;
        current.max = sortedFrames[sortedFrames.Length - 1].totalMilliseconds;
        // The median is the average of two middle frames.
        current.median = sortedFrames[sortedFrames.Length / 2].totalMilliseconds;
        current.median += sortedFrames[(sortedFrames.Length - 1) / 2].totalMilliseconds;
        current.median *= 0.5f;
        current.mean = sortedFrames.Average(x => x.totalMilliseconds);

        // Standard deviation of median, calculated as follows:
        //                ______________________
        //               / n-1
        // stdDev =     /   Σ (t[i] - median)^2
        //             /   i=0
        //         _  /   ----------------------
        //          \/             n-1
        //
        current.medianStdDev = Mathf.Sqrt(
          sortedFrames.Average(x => Mathf.Pow(x.totalMilliseconds - current.median, 2f)));
        current.medianStdDevPc = (current.medianStdDev / current.median) * 100f;
      }
    }
  }
} // namespace TiltBrush
