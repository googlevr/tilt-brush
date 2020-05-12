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
using System.Linq;
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Profiling;
using UnityEditor.IMGUI.Controls;

namespace TiltBrush {
  /// Gets Unity profile data from a set of frames and calculates some averaged data over several
  /// frames from it.
  [Serializable]
  public class ProfileAnalyzer : EditorWindow {

    /// Used to calculate and display a hsitogram of frame times.
    /// The histogram buckets have been chosen so that there are three buckets every 1.11111ms.
    /// This number has been chosen because 11.1111ms = 90fps, 13.3333ms = 75fps, 16.6666ms = 60fps.
    private class Histogram {
      private int[] m_FrametimeHistogram;
      private int m_HistogramMax;
      private Color[] m_HistogramColors;
      private readonly int m_NumBuckets = 33;
      private readonly float m_BucketSize = 10f / 27f;
      private float m_90fps;
      private float m_75fps;
      private float m_60fps;
      private float m_under60fps;

      public Histogram() {
        m_HistogramColors = new Color[m_NumBuckets];
        for (int i = 0; i < m_NumBuckets; ++i) {
          Color color = new Color32(0xdb, 0x44, 0x37, 0xff);  // default red.
          if (i <= 5) {
            color = new Color32(0x42, 0x85, 0xf4, 0xff); // blue for 90fps +.
          } else if (i <= 11) {
            color = new Color32(0x0F, 0x9D, 0x58, 0xff); // green for 75fps +.
          } else if (i <= 20) {
            color = new Color32(0xf4, 0xb4, 0x00, 0xff); // yellow for 60fps +.
          }
          m_HistogramColors[i] = color;
        }
      }

      // Counts the number of frames that hit each frame time bucket, and works out
      // the percentages that hit 90,75,60, and worse fps.
      public void AnalyseData(ProfileDataset.SampledFunction root) {
        m_FrametimeHistogram = new int[m_NumBuckets];
        float[] limits =
          Enumerable.Range(0, m_NumBuckets).Select(x => 10f + x * m_BucketSize).ToArray();
        limits[m_NumBuckets - 1] = float.MaxValue;
        foreach (var frame in root.frameData.Values) {
          for (int i = 0; i < m_NumBuckets; ++i) {
            if (frame.totalMilliseconds <= limits[i]) {
              m_FrametimeHistogram[i]++;
              break;
            }
          }
        }
        m_HistogramMax = m_FrametimeHistogram.Max();

        float percentScale = 100.0f / root.frameData.Count;
        m_90fps = m_FrametimeHistogram.Take(6).Sum() * percentScale;
        m_75fps = m_FrametimeHistogram.Take(12).Sum() * percentScale;
        m_60fps = m_FrametimeHistogram.Take(21).Sum() * percentScale;
        m_under60fps = m_FrametimeHistogram.Skip(21).Sum() * percentScale;
      }

      public void Render(int histoHeight) {
        // Drawing the histogram without the layout tools, so grab some space from GUILayout here.
        // We also work out the size of a Label so we can add some at the bottom.
        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.Height(histoHeight));
        Rect textRect = GUILayoutUtility.GetRect(new GUIContent("13.33"), GUI.skin.label);
        GUILayout.EndHorizontal();
        Rect wholeRect = GUILayoutUtility.GetLastRect();

        Color oldColor = GUI.color;
        GUIContent blankContent = new GUIContent();
        GUIStyle box = new GUIStyle(GUI.skin.box);
        box.normal.background = EditorGUIUtility.whiteTexture;
        float width = wholeRect.width / m_NumBuckets;

        // ==== Histogram bars ====
        Rect exampleBlock = new Rect(wholeRect);
        exampleBlock.width = width - 2;
        exampleBlock.height -= textRect.height * 2;
        for (int i = 0; i < m_NumBuckets; ++i) {
          Rect block = new Rect(exampleBlock);
          block.x += width * i;
          block.height = Mathf.Max(4, block.height * m_FrametimeHistogram[i] / (float)m_HistogramMax);
          block.y += exampleBlock.height - block.height;
          GUI.color = m_HistogramColors[i];
          GUI.Box(block, blankContent, box);
        }

        // ==== Frame time labels ====
        GUI.color = oldColor;
        textRect.y = exampleBlock.y + exampleBlock.height;
        textRect.width = width * 3;
        GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
        labelStyle.alignment = TextAnchor.UpperCenter;
        for (int i = 0; i < m_NumBuckets - 1; i++) {
          Rect text = new Rect(textRect);
          text.x += text.width * (i + 0.5f);
          GUI.Label(text, (i * m_BucketSize * 3f + 10f).ToString("F2"), labelStyle);
        }

        // ==== FPS percentages ====
        GUIStyle fpsStyle = new GUIStyle(EditorStyles.textField);
        fpsStyle.alignment = TextAnchor.MiddleCenter;
        Rect fps = new Rect(textRect);
        fps.y += fps.height;
        fps.width = width * 6;
        GUI.Label(fps, string.Format(">= 90fps: {0:00}%", m_90fps), fpsStyle);
        fps.x += fps.width;
        GUI.Label(fps, string.Format(">= 75fps: {0:00}% ({1:00}%)", m_75fps, m_75fps - m_90fps), fpsStyle);
        fps.x += fps.width;
        fps.width = 9 * width;
        GUI.Label(fps, string.Format(">= 60fps: {0:00}% ({1:00}%)", m_60fps, m_60fps - m_75fps), fpsStyle);
        fps.x += fps.width;
        fps.width = width * 15;
        GUI.Label(fps, string.Format("< 60fps: {0:00}%", m_under60fps), fpsStyle);
      }
    }

    private ProfileDataset m_Data;
    private Vector2 m_Scroll;
    private HashSet<int> m_Expanded = new HashSet<int>();
    private bool m_HistogramVisible = true;
    [SerializeField] private int m_sortColumn = 4;
    [SerializeField] private bool m_sortAscending = false;
    [SerializeField] private string m_Source = "";
    private string m_SourceTrimmed = "";

    private Histogram m_Histogram;

    [MenuItem("Tilt/Open New Profile Analyzer")]
    public static void OpenProfileAnalyzer() {
      ProfileAnalyzer analyzer = CreateInstance<ProfileAnalyzer>();
      analyzer.titleContent = new GUIContent("Profile Analyzer");
      analyzer.Show();
    }

    private void Awake() {
      if(string.IsNullOrEmpty(m_SourceTrimmed)) {
        if (string.IsNullOrEmpty(m_Source)) {
          m_SourceTrimmed = "No Data.";
        } else {
          if (File.Exists(m_Source)) {
            m_SourceTrimmed = System.IO.Path.GetFileName(m_Source);
          } else {
            m_Source = "";
          }
        }
      }
    }

    private void OnGUI() {
      RenderMenuBar();
      RenderFrameHistogram();
      RenderData();
    }

    private void RenderMenuBar() {
      GUILayout.BeginHorizontal();
      GUILayout.Label(m_SourceTrimmed);

      // Add the option to reload the current data
      GUI.enabled = (m_Data == null && !string.IsNullOrEmpty(m_Source));
      if (GUILayout.Button("Reload")) {
        Profiler.AddFramesFromFile(m_Source);
        m_SourceTrimmed = string.Format("Loaded from {0}", System.IO.Path.GetFileName(m_Source));
        AnalyzeProfile();
      }
      GUI.enabled = true;
      if (GUILayout.Button("Load New")) {
        string source = LoadProfile();
        if (!string.IsNullOrEmpty(source)) {
          m_Source = source;
          m_SourceTrimmed = string.Format("Loaded from {0}", System.IO.Path.GetFileName(m_Source));
        }
      }
      if (GUILayout.Button("Analyze")) {
        AnalyzeProfile();
        m_Source = "";
        m_SourceTrimmed = string.Format("Taken from Editor at {0}", DateTime.Now.ToString("t"));
      }
      GUILayout.EndHorizontal();
    }

    private void RenderFrameHistogram() {
      if (m_Histogram == null || m_Data == null) { return; }
      m_HistogramVisible = EditorGUILayout.Foldout(m_HistogramVisible, "Frame Time Histogram");
      if (!m_HistogramVisible) { return; }
      m_Histogram.Render(200);
    }

    private string LoadProfile() {
      string filename = EditorUtility.OpenFilePanelWithFilters(
        "Open profile data file", "", new string[] { "Profile files", "data", "All files", "*" });
      if (string.IsNullOrEmpty(filename) || !File.Exists(filename)) {
        return null;
      }
      Profiler.AddFramesFromFile(filename);
      AnalyzeProfile();
      return filename;
    }

    /// Kick off loading and analysing the profile, showing the progress on a progress bar.
    private void AnalyzeProfile() {
      m_Data = new ProfileDataset();
      Action <float> progressBar = delegate(float f) {
        string message = (f < 0.75f) ? "Reading profile data" : "Analyzing profile data";
        EditorUtility.DisplayProgressBar("Loading Profile", message, f);
      };
      try {
        m_Data.AnalyzeProfilerData(progressBar);
        m_Histogram = new Histogram();
        m_Histogram.AnalyseData(m_Data.Root);
        m_Expanded = new HashSet<int>();
        m_Scroll = Vector2.zero;
      } finally {
        EditorUtility.ClearProgressBar();
      }
    }

    /// Renders the data in a table.
    /// Sorting is triggered just-in time; each visible list of samples is asked to make sure
    /// they're sorted whenever they are displayed.
    private void RenderData() {
      // Draw the header.
      int[] intWidths = {50, 400, 100, 100, 100, 100, 100, 120, 120};
      string[] names = {
        "id",
        "Name",
        "Frame Count",
        "Average ms",
        "Median ms",
        "Min ms",
        "Max ms",
        "Median StdDev",
        "Median StdDev %",
      };
      GUILayoutOption[] widths = intWidths.Select(x => GUILayout.Width(x)).ToArray();
      if (m_Data == null || m_Data.Root == null || m_Data.Root.children == null) { return; }
      GUILayout.BeginVertical();
      Vector2 headerScroll = m_Scroll;
      headerScroll.y = 0;
      GUILayout.BeginScrollView(headerScroll, GUIStyle.none, GUIStyle.none);
      GUILayout.BeginHorizontal();
      for (int i = 0; i < names.Length; ++i) {
        // Creates a header titles, with the a marker on the sorting index.
        string marker = (i == m_sortColumn) ? (m_sortAscending ? " ^" : " v") : "";
        string headerTitle = names[i] + marker;
        // Change sorting parameters.
        if (GUILayout.Button(headerTitle, widths[i])) {
          if (i == m_sortColumn) {
            m_sortAscending = !m_sortAscending;
          } else {
            m_sortColumn = i;
          }
        }
      }
      GUILayout.EndHorizontal();
      GUILayout.EndScrollView();

      // Draw the rest of the data.
      m_Scroll = GUILayout.BeginScrollView(m_Scroll);

      // Styles for the name section - clipping has to be turned off.
      GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
      foldoutStyle.clipping = TextClipping.Clip;
      GUIStyle labelStyle = new GUIStyle(foldoutStyle);
      labelStyle.imagePosition = ImagePosition.TextOnly;
      labelStyle.normal.background = EditorStyles.label.normal.background;

      // Alternate background styles are used for each line to make the table easier to read.
      GUIStyle[] lineStyles = new GUIStyle[2];
      lineStyles[0] = new GUIStyle(TreeView.DefaultStyles.backgroundEven);
      lineStyles[1] = new GUIStyle(TreeView.DefaultStyles.backgroundOdd);
      lineStyles[0].border = new RectOffset(3,3,3,3);
      lineStyles[1].border = new RectOffset(3, 3, 3, 3);
      lineStyles[0].margin = new RectOffset(3, 3, 3, 3);
      lineStyles[1].margin = new RectOffset(3, 3, 3, 3);

      // Make sure the children of the root are sorted.
      EnsureSorted(m_Data.Root);

      // Iterate through all children, and their open children - uses a stack instead of recursion.
      var stack = new Stack<IEnumerator<ProfileDataset.SampledFunction>>();
      stack.Push(m_Data.Root.sortedChildren.GetEnumerator());
      // used to work out which color line background to use.
      int lineIndex = 0;
      while(true) {
        // Get the next child item, or grab the next enumerator off the stack.
        ProfileDataset.SampledFunction child = null;
        while (true) {
          if (stack.Peek().MoveNext()) {
            child = stack.Peek().Current;
            break;
          }
          stack.Pop();
          if (!stack.Any()) {
            break;
          }
        }
        if (child == null) { break; }

        // Make sure the children are sorted, just-in-time
        EnsureSorted(child);

        GUILayout.BeginHorizontal(lineStyles[lineIndex++ % 2]);
        int columnIndex = 0;
        GUILayout.Label(child.id.ToString(), widths[columnIndex++]);
        int indent = (child.depth - 1) * 10;
        foldoutStyle.fixedWidth = intWidths[columnIndex] - indent;
        labelStyle.fixedWidth = foldoutStyle.fixedWidth;
        GUILayout.Space(indent);
        if (child.sortedChildren.Any()) {
          // A hashset is used to store whether an item with children is expanded.
          bool wasOpen = m_Expanded.Contains(child.id);
          bool toggle = GUILayout.Button(child.name, foldoutStyle);
          if (toggle) {
            if (wasOpen) {
              m_Expanded.Remove(child.id);
            } else {
              m_Expanded.Add(child.id);
            }
          }
          if (wasOpen) {
            stack.Push(child.sortedChildren.GetEnumerator());
          }
        } else {
          GUILayout.Label(child.name, labelStyle);
        }
        columnIndex++;
        GUILayout.Label(child.frameData.Count.ToString(), widths[columnIndex++]);
        GUILayout.Label(child.mean.ToString("F3"), widths[columnIndex++]);
        GUILayout.Label(child.median.ToString("F3"), widths[columnIndex++]);
        GUILayout.Label(child.min.ToString("F3"), widths[columnIndex++]);
        GUILayout.Label(child.max.ToString("F3"), widths[columnIndex++]);
        GUILayout.Label(child.medianStdDev.ToString("F3"), widths[columnIndex++]);
        GUILayout.Label(child.medianStdDevPc.ToString("F0"), widths[columnIndex++]);
        GUILayout.EndHorizontal();
      }
      GUILayout.EndScrollView();

      GUILayout.EndVertical();
    }

    /// Sorts the children of a function, depending on the sorting settings.
    private void EnsureSorted(ProfileDataset.SampledFunction fn) {
      // If this function is already sorted correctly, early out.
      if (fn.sortIndex == m_sortColumn && fn.sortAscending == m_sortAscending) {
        return;
      }

      switch (m_sortColumn) {
      case 0:
        fn.sortedChildren = fn.sortedChildren.OrderBy(x => x.id).ToList();
        break;
      case 1:
        fn.sortedChildren = fn.sortedChildren.OrderBy(x => x.name).ToList();
        break;
      case 2:
        fn.sortedChildren = fn.sortedChildren.OrderBy(x => x.frameData.Count).ToList();
        break;
      case 3:
        fn.sortedChildren = fn.sortedChildren.OrderBy(x => x.mean).ToList();
        break;
      case 4:
        fn.sortedChildren = fn.sortedChildren.OrderBy(x => x.median).ToList();
        break;
      case 5:
        fn.sortedChildren = fn.sortedChildren.OrderBy(x => x.min).ToList();
        break;
      case 6:
        fn.sortedChildren = fn.sortedChildren.OrderBy(x => x.max).ToList();
        break;
      case 7:
        fn.sortedChildren = fn.sortedChildren.OrderBy(x => x.medianStdDev).ToList();
        break;
      case 8:
        fn.sortedChildren = fn.sortedChildren.OrderBy(x => x.medianStdDevPc).ToList();
        break;
      }
      if (!m_sortAscending) {
        fn.sortedChildren.Reverse();
      }
    }
  }
} // namespace TiltBrush
