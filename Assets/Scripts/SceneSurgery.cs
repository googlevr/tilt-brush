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
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UObject = UnityEngine.Object;

namespace TiltBrush {

#if UNITY_EDITOR
public class SceneSurgery : EditorWindow {
  [MenuItem("Window/Scene Surgery")]
  static void OpenWindow() {
    EditorWindow.GetWindow<SceneSurgery>().Show();
  }

  // GUI state
  static public GUIContent NAME_Color = new GUIContent("Color", "Brush color");
  bool m_DoColor = false;
  Color m_Color = Color.white;

  static public GUIContent NAME_Brush = new GUIContent("Brush", "Brush guid");
  bool m_DoBrush = false;
  string m_Brush = "<enter guid here>";

  static GUIContent NAME_TimeOffset = new GUIContent(
      "Time offset (ms)",
      "Shift time of all control points by the specified amount");
  bool m_DoTimeOffset = false;
  int m_TimeOffset = 0;

  static GUIContent NAME_TimeStart = new GUIContent(
      "Start time (ms, absolute)",
      "Absolute time of first stroke's first control point; other points are shifted/stretched to match");
  bool m_DoTimeStart = false;
  int m_TimeStart = 0;

  static GUIContent NAME_TimeEnd = new GUIContent(
      "End time (ms, absolute)",
      "Absolute time of last stroke's last control point; other points are shifted/stretched to match");
  bool m_DoTimeEnd = false;
  int m_TimeEnd = 1;

  static GUIContent NAME_CpOffset = new GUIContent(
      "Control point offset",
      "Push control points up to this far along their normal. Exact value is random per stroke.");
  bool m_DoCpOffset = false;
  float m_CpOffset = 2e-3f;

  static GUIContent NAME_ResequenceStrokes = new GUIContent(
      "Resequence (X axis)",
      "Adjust timestamps to make strokes sequential, ordered by head control point along X axis.");
  bool m_ResequenceStrokes = false;

  static GUIContent NAME_CpLiftIncrement = new GUIContent(
      "Control point increment",
      "Push control points this far along their normal, multiplied by distance along stroke");
  bool m_DoCpLiftIncrement = false;
  float m_CpLiftIncrement = 1e-4f;

  bool m_ApplyOnSelect = false;

  // Internal state
  private bool m_haveSelection = false;

  // Better than Update() because it's only called at 10Hz
  private bool m_startCalled = false;
  void OnInspectorUpdate() {
    // Emulate GameObject.Start() functionality.
    if (! EditorApplication.isPlaying) {
      m_startCalled = false;
    } else {
      if (!m_startCalled && App.CurrentState == App.AppState.Standard) {
        m_startCalled = true;
        Start();
      }
    }
  }

  // Called sort-of when GameObject Start() is called
  void Start() {
    m_ApplyOnSelect = false;
    if (BrushController.m_Instance != null) {
      BrushController.m_Instance.StrokeSelected += OnStrokeSelected;
      BrushController.m_Instance.BrushChanged += brushDescriptor => {
        m_Brush = brushDescriptor.m_Guid.ToString("D");
        Repaint();
      };
    }
    if (PanelManager.m_Instance != null) {
      // TODO : This path has been deprecated.  Repair this.
      //List<PanelManager.PanelData> allPanels = PanelManager.m_Instance.GetAllPanels();
      //for (int i = 0; i < allPanels.Count; ++i) {
      //  ColorPickerPanel colorsPanel = allPanels[i].m_Panel as ColorPickerPanel;
      //  if (colorsPanel != null) {
      //    colorsPanel.ColorUpdated += color => {
      //      m_Color = (Color)color;
      //      Repaint();
      //    };
      //  }
      //}
    }
  }

  void OnStrokeSelected(Stroke stroke) {
    // XXX: should fix the Dropper's event to pass along the sender object
    var sender = App.Config.m_Dropper;
    if (m_ApplyOnSelect && stroke != null) {
      Apply(new [] { stroke });
      sender.DisableRequestExit_HackForSceneSurgeon();
    }
  }

  /// Apply selected mutations to strokes associated with the passed transforms
  public void ApplyToTransforms(IEnumerable<Transform> transforms) {
    Apply(StrokesForTransforms(transforms).ToArray());
  }

  /// Applies selected mutations to all strokes
  public void ApplyToAll() {
    Apply(SketchMemoryScript.AllStrokes().ToArray());
  }

  /// Pushes non-identity transforms from GameObjects into strokes.
  /// Zeroes out the GameObject transforms as a side effect.
  public void BakeTransforms() {
    foreach (var mo in SketchMemoryScript.AllStrokes()) {
      BakeGameObjTransform(mo);
    }

    // Zeroing can't happen during baking, because a single transform may affect
    // multiple strokes. Also, we should take care to zero transforms for batches
    // that may not currently have any strokes.
    foreach (var t in AllStrokeTransforms()) {
      t.position = Vector3.zero;
      t.rotation = Quaternion.identity;
      t.localScale = Vector3.one;
    }

    SketchMemoryScript.m_Instance.Redraw(doSort: false);
  }

  public void RemoveOriginFromMeshBounds() {
    foreach (var t in AllStrokeTransforms()) {
      var mf = t.GetComponent<MeshFilter>();
      if (mf != null) {
        mf.sharedMesh.bounds = BoundsWithoutOrigin(mf.sharedMesh.vertices);
      }
    }
  }

  //
  // Helpers
  //

  static IEnumerable<Transform> AllStrokeTransforms() {
    var transforms = new HashSet<Transform>(new ReferenceComparer<Transform>());
    foreach (var canvas in App.Scene.AllCanvases) {
      transforms.UnionWith(from batch in canvas.BatchManager.AllBatches()
                           select batch.transform);
    }
    transforms.UnionWith(from mo in SketchMemoryScript.AllStrokes()
                         select TransformForStroke(mo));
    return transforms;
  }

  public static IEnumerable<Stroke> StrokesForTransforms(IEnumerable<Transform> transforms) {
    foreach (var t in transforms) {
      Batch batch = t.GetComponent<Batch>();
      if (batch != null) {
        foreach (var group in batch.m_Groups) {
          yield return group.m_Stroke;
        }
      }

      BaseBrushScript brush = t.GetComponent<BaseBrushScript>();
      if (brush != null) {
        yield return brush.Stroke;
      }
    }
  }

  private static Transform TransformForStroke(Stroke stroke) {
    if (stroke.m_BatchSubset != null) {
      Batch batch = stroke.m_BatchSubset.m_ParentBatch;
      return batch.transform;
    } else if (stroke != null) {
      return stroke.m_Object.transform;
    } else {
      return null;
    }
  }

  static Bounds BoundsWithoutOrigin(Vector3[] vs) {
    Bounds b = new Bounds();
    bool seenFirst = false;
    foreach (var v in vs) {
      if (v != Vector3.zero) {
        if (!seenFirst) {
          b = new Bounds(v, Vector3.zero);
          seenFirst = true;
        } else {
          b.Encapsulate(v);
        }
      }
    }
    return b;
  }

  /// Raises InvalidOperationException if no timestamps.
  public static void GetMinMaxTimes(
      Stroke[] strokes,
      out uint min, out uint max) {
    try {
      min = strokes.Select(stroke => stroke.m_ControlPoints[0].m_TimestampMs).Min();
      max = strokes.Select(stroke => stroke.m_ControlPoints.Last().m_TimestampMs).Max();
    } catch (NullReferenceException e) {
      // Strange that Linq throws NullReferenceException
      // Change to InvalidOperationException
      throw new InvalidOperationException(e.ToString());
    }
  }

  //
  // Mutators
  //

  private void BakeGameObjTransform(Stroke stroke) {
    TrTransform xf_CS = Coords.AsCanvas[TransformForStroke(stroke)];
    if (xf_CS == TrTransform.identity) { return; }

    var cps = stroke.m_ControlPoints;
    for (int i = 0; i < cps.Length; ++i) {
      var cp = xf_CS * TrTransform.TR(cps[i].m_Pos, cps[i].m_Orient);
      cps[i].m_Pos = cp.translation;
      cps[i].m_Orient = cp.rotation;
    }
    stroke.m_BrushScale *= xf_CS.scale;
  }

  private void Apply(Stroke[] strokes) {
    BrushDescriptor brush = null;
    if (m_DoBrush && !String.IsNullOrEmpty(m_Brush)) {
      try {
        var guid = new Guid(m_Brush);
        brush = BrushCatalog.m_Instance.GetBrush(guid);
        if (brush == null) {
          Debug.LogFormat("No Brush {0}", guid);
        }
      } catch (Exception e) {
        Debug.LogFormat("Invalid guid {0}: {1}", m_Brush, e);
        brush = null;
      }
    }

    bool needsTimeAdjust = true;
    uint new0 = 0, new1 = 1;    // invariant: new0 <= new1
    uint old0 = 0, old1 = 1;    // invariant: old0 < old1
    try {
      GetMinMaxTimes(strokes, out old0, out old1);
      if (old0 == old1) {
        // our choice here determines whether the timestamp goes to new0 or new1
        old1 = old0 + 1;
      }
    } catch (InvalidOperationException) {
      needsTimeAdjust = false;
    }

    if (m_DoTimeOffset) {
      new0 = (uint)(old0 + m_TimeOffset);
      new1 = (uint)(old1 + m_TimeOffset);
    } else if (m_DoTimeStart && m_DoTimeEnd) {
      new0 = (uint) m_TimeStart;
      new1 = (uint) m_TimeEnd;
    } else if (m_DoTimeStart) {
      new0 = (uint) m_TimeStart;
      new1 = (uint) (new0 + (old1 - old0));
    } else if (m_DoTimeEnd) {
      new1 = (uint) m_TimeEnd;
      new0 = (uint) (new1 - (old1 - old0));
    } else {
      new0 = old0;
      new1 = old1;
      needsTimeAdjust = needsTimeAdjust && m_ResequenceStrokes;
    }

    if (new0 > new1) {
      Debug.LogFormat("Invalid retime {0} {1}", new0, new1);
      needsTimeAdjust = false;
    }

    uint pointIndex = 0;
    int pointCount = strokes.Select(obj => obj.m_ControlPoints.Length).Sum();
    float durationPerPointMs = (float)(new1 - new0) / pointCount;
    if (m_ResequenceStrokes) {
      // TODO: axis options
      strokes = strokes.OrderBy(obj => obj.m_ControlPoints[0].m_Pos.x).ToArray();
    }

    foreach (var stroke in strokes) {
      var cps = stroke.m_ControlPoints;

      if (cps.Length > 0 && needsTimeAdjust) {
        for (int i = 0; i < cps.Length; ++i) {
          if (m_ResequenceStrokes) {
            cps[i].m_TimestampMs = new0 + (uint)(pointIndex * durationPerPointMs);
            ++pointIndex;
          } else {
            // Use long to avoid overflow
            long ts = cps[i].m_TimestampMs;
            ts = (ts - old0) * (new1 - new0) / (old1 - old0) + new0;
            cps[i].m_TimestampMs = (uint)ts;
          }
        }
      }

      if (m_DoColor) {
        stroke.m_Color = m_Color;
      }

      if (brush != null) {
        stroke.m_BrushGuid = brush.m_Guid;
      }

      if (m_DoCpOffset || m_DoCpLiftIncrement) {
        float offset = m_DoCpOffset ? UnityEngine.Random.Range(0, m_CpOffset) : 0;
        float increment = m_DoCpLiftIncrement ? m_CpLiftIncrement : 0;
        for (int i = 0; i < cps.Length; ++i) {
          Vector3 norm = -(cps[i].m_Orient * Vector3.forward);
          cps[i].m_Pos += norm * (offset + i * increment);
        }
      }

    }

    SketchMemoryScript.m_Instance.Redraw(doSort: needsTimeAdjust);
  }

  //
  // GUI code
  //

  void OnSelectionChange() {
    var transforms = Selection.transforms;
    m_haveSelection = (transforms.Length > 0);
    if (m_haveSelection) {
      var strokes = StrokesForTransforms(transforms).ToArray();
      uint min, max;
      try {
        GetMinMaxTimes(strokes, out min, out max);
      } catch (InvalidOperationException) {
        return;
      }
      m_TimeStart = (int)min;
      m_TimeEnd = (int)max;
      Repaint();
    }
  }

  public void ToggledVector(ref bool toggle, ref Vector3 val, GUIContent name) {
    EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField(name, GUILayout.Width(150), GUILayout.MaxWidth(150));
      toggle = EditorGUILayout.Toggle(toggle, GUILayout.ExpandWidth(false));
      GUI.enabled = toggle;
      val = EditorGUILayout.Vector3Field("", val, GUILayout.ExpandWidth(true));
      GUI.enabled = true;
    EditorGUILayout.EndHorizontal();
  }

  public void ToggledColor(ref bool toggle, ref Color val, GUIContent name) {
    EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField(name, GUILayout.Width(150), GUILayout.MaxWidth(150));
      toggle = EditorGUILayout.Toggle(toggle, GUILayout.ExpandWidth(false));
      val = EditorGUILayout.ColorField("", val, GUILayout.ExpandWidth(true));
    EditorGUILayout.EndHorizontal();
  }

  public void ToggledInt(ref bool toggle, ref int val, GUIContent name) {
    EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField(name, GUILayout.Width(150), GUILayout.MaxWidth(150));
      toggle = EditorGUILayout.Toggle(toggle, GUILayout.ExpandWidth(false));
      val = EditorGUILayout.IntField("", val, GUILayout.ExpandWidth(true));
    EditorGUILayout.EndHorizontal();
  }

  static public void Toggled(ref bool toggle, ref float val, GUIContent name) {
    EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField(name, GUILayout.Width(150), GUILayout.MaxWidth(150));
      toggle = EditorGUILayout.Toggle(toggle, GUILayout.ExpandWidth(false));
      val = EditorGUILayout.FloatField("", val, GUILayout.ExpandWidth(true));
    EditorGUILayout.EndHorizontal();
  }

  public void ToggledGuid(ref bool toggle, ref string val, GUIContent name) {
    EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField(name, GUILayout.Width(150), GUILayout.MaxWidth(150));
      toggle = EditorGUILayout.Toggle(toggle, GUILayout.ExpandWidth(false));
      GUI.enabled = toggle;
      string newval = EditorGUILayout.TextField(val, GUILayout.ExpandWidth(true));
      if (newval != val) {
        try {
          var guid = new Guid(newval);
          val = guid.ToString("D");
        } catch (Exception) {
          val = newval;
        }
      }
      GUI.enabled = true;
    EditorGUILayout.EndHorizontal();
  }

  void FloodSelectByColor() {
    var targetColor =
      StrokesForTransforms(Selection.transforms).First().m_Color;
    var selection = new HashSet<GameObject>(new ReferenceComparer<GameObject>());
    selection.UnionWith(from stroke in SketchMemoryScript.AllStrokes()
                        where stroke.m_Color == targetColor
                        select TransformForStroke(stroke).gameObject);
    Selection.objects = selection.Cast<UObject>().ToArray();
  }

  void OnGUI() {
    EditorGUILayout.Space();

    // Easier to use unity manipulations then "bake transforms"

    ToggledColor(ref m_DoColor, ref m_Color, SceneSurgery.NAME_Color);
    ToggledGuid(ref m_DoBrush, ref m_Brush, SceneSurgery.NAME_Brush);
    ToggledInt(ref m_DoTimeOffset, ref m_TimeOffset, SceneSurgery.NAME_TimeOffset);
    ToggledInt(ref m_DoTimeStart, ref m_TimeStart, SceneSurgery.NAME_TimeStart);
    ToggledInt(ref m_DoTimeEnd, ref m_TimeEnd, SceneSurgery.NAME_TimeEnd);
    m_ResequenceStrokes = EditorGUILayout.Toggle(NAME_ResequenceStrokes, m_ResequenceStrokes);
    Toggled(ref m_DoCpOffset, ref m_CpOffset, SceneSurgery.NAME_CpOffset);
    Toggled(ref m_DoCpLiftIncrement, ref m_CpLiftIncrement, SceneSurgery.NAME_CpLiftIncrement);
    EditorGUILayout.Space();
    m_ApplyOnSelect = EditorGUILayout.Toggle("Use dropper to apply", m_ApplyOnSelect);

    EditorGUILayout.BeginHorizontal(); {
      GUI.enabled = m_haveSelection;
      if (GUILayout.Button("Apply (editor selection)")) {
        ApplyToTransforms(UnityEditor.Selection.transforms);
      }
      GUI.enabled = true;

      if (GUILayout.Button("Apply (all)")) {
        ApplyToAll();
      }

    } EditorGUILayout.EndHorizontal();
    if (GUILayout.Button("Bake transforms")) {
      BakeTransforms();
    }
    if (GUILayout.Button("Remove origin from mesh bounds")) {
      RemoveOriginFromMeshBounds();
    }
    if (GUILayout.Button("Flood-select by color")) {
      RemoveOriginFromMeshBounds();
      FloodSelectByColor();
    }
    if (GUILayout.Button("Filter non-strokes from selection")) {
      // Batch or BaseBrushScript
      Selection.objects = (
          from t in Selection.transforms
          where (t.GetComponent<Batch>() != null ||
                 t.GetComponent<BaseBrushScript>() != null)
          select t.gameObject).Cast<UObject>().ToArray();
    }
  }
}
#endif
}  // namespace TiltBrush
