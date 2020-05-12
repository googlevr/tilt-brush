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
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush {

// Rough equivalent of C# 4.0 HasFlag (actually more efficient since types must match).
public static class EnumExtensions {
  public static bool HasFlag<T>(this T flags, T flag) where T : struct, IConvertible
  {
    var iFlags = Convert.ToUInt64(flags);
    var iFlag = Convert.ToUInt64(flag);
    return ((iFlags & iFlag) == iFlag);
  }
}

public class SketchMemoryScript : MonoBehaviour {
  public static  SketchMemoryScript m_Instance;

  public event Action OperationStackChanged;

  public GameObject m_UndoBatchMeshPrefab;
  public GameObject m_UndoBatchMesh;
  public bool m_SanityCheckStrokes = false;

  private int m_LastCheckedVertCount;
  private int m_MemoryWarningVertCount;

  [Flags]
  public enum StrokeFlags {
    None = 0,
    Deprecated1 = 1 << 0,
    /// This stroke continues a group that is considered a single entity with respect to undo and
    /// redo. To support timestamp ordering, only strokes having identical timestamps on the initial
    /// control point may be grouped (e.g. mirrored strokes). Currently, these strokes must also be
    /// added to SketchMemoryScript at the same time.
    ///
    /// This is distinct from Stroke.Group, which is a collection of strokes (of possibly differing
    /// timestamps) that are selected together.
    IsGroupContinue = 1 << 1,
  }

  // NOTE: making this generic is way more trouble than it's worth
  public static void SetFlag(ref SketchMemoryScript.StrokeFlags flags,
                             SketchMemoryScript.StrokeFlags flag, bool flagValue) {
    if (flagValue) {
      flags |= flag;
    } else {
      flags &= ~flag;
    }
  }

  // stack of sketch operations this session
  private Stack<BaseCommand> m_OperationStack;
  // stack of undone operations available for redo
  private Stack<BaseCommand> m_RedoStack;

  // Memory list by timestamp of initial control point.  The nodes of this list are
  // embedded in MemoryObject.  Notable properties:
  //    * only MemoryObjects to be serialized (i.e. strokes) are kept in the list
  //    * objects remain in this list despite being in "undone" state (i.e. on redo stack)
  //    * edit-ordering is preserved among strokes having the same timestamp-- implying that
  //      by-time order matches edit order for sketches with no timestamps, and moreover that
  //      stroke grouping is preserved
  //
  // Why we can get away with linked list performance for sequence time ordering:
  //    * load case:  sort once at init to populate sequence-time list.  Since we save in
  //      timestamp order, this should be O(N).
  //    * playback case:  simply walk sequence-time list for normal case.  For timeline scrub,
  //      hopefully skip interval is small (say 10 seconds) so the number of items to traverse
  //      is reasonable.
  //    * edit case:  update current position in sequence-time list every frame (same as playback)
  //      so we're always ready to insert new strokes
  private LinkedList<Stroke> m_MemoryList = new LinkedList<Stroke>();
  // Used as a starting point for any search by time.  Either null or a node contained in
  // m_MemoryList.
  // TODO: Have Update() advance this position to match current sketch time so that we
  // amortize list traversal in timeline edit mode.
  private LinkedListNode<Stroke> m_CurrentNodeByTime;

  //for loading .sketches
  public enum PlaybackMode {
    Distance,
    Timestamps,
  }
  private IScenePlayback m_ScenePlayback;

  /// discern between initial and edit-time playback in timeline edit mode
  private bool m_IsInitialPlay;
  private PlaybackMode m_PlaybackMode;
  private float m_DistancePerSecond;  // in units. for PlaybackMode.Distance

  // operation stack size as of last load (always 0) or save
  private int m_LastOperationStackCount;
  private bool m_HasVisibleObjects;
  private bool m_MemoryExceeded;
  private bool m_MemoryWarningAccepted;

  // Strokes that should be deleted; processed and cleared each frame.
  private HashSet<Stroke> m_DeleteStrokes = new HashSet<Stroke>(new ReferenceComparer<Stroke>());
  // Non-null if there are strokes that should be repainted this frame.
  // TODO: give this the same treatment as m_DeleteStrokes?
  private BaseCommand m_RepaintStrokeParent;

  private TrTransform m_xfSketchInitial_RS;

  private Coroutine m_RepaintCoroutine;
  private float m_RepaintProgress;

  public float RepaintProgress {
    get { return m_RepaintCoroutine == null ? 1f : m_RepaintProgress; }
  }

  public TrTransform InitialSketchTransform {
    get { return m_xfSketchInitial_RS; }
    set { m_xfSketchInitial_RS = value; }
  }

  public bool IsPlayingBack { get { return m_ScenePlayback != null; } }

  public float PlaybackMetersPerSecond {
    get {
      return m_DistancePerSecond * App.UNITS_TO_METERS;
    }
  }

  public float GetDrawnPercent() {
    return (float) m_ScenePlayback.MemoryObjectsDrawn / (m_MemoryList.Count - 1);
  }

  // Note this value is cached and only updated once per frame.
  public bool HasVisibleObjects() { return m_HasVisibleObjects; }

  public bool MemoryExceeded {
    get { return m_MemoryExceeded; }
  }
  public bool MemoryWarningAccepted {
    get { return m_MemoryWarningAccepted; }
    set {
      m_MemoryWarningAccepted = value;
      App.Switchboard.TriggerMemoryWarningAcceptedChanged();
    }
  }
  public float MemoryExceededRatio {
    get { return (float)m_LastCheckedVertCount / (float)m_MemoryWarningVertCount; }
  }

  public void SetLastOperationStackCount() {
    m_LastOperationStackCount = m_OperationStack.Count;
  }

  public bool WillVertCountPutUsOverTheMemoryLimit(int numVerts) {
    if (!m_MemoryExceeded && !m_MemoryWarningAccepted) {
      int vertCount = numVerts +
          App.Scene.MainCanvas.BatchManager.CountAllBatchVertices() +
          App.Scene.SelectionCanvas.BatchManager.CountAllBatchVertices() +
          WidgetManager.m_Instance.WidgetsVertCount;
      return vertCount > m_MemoryWarningVertCount;
    }
    return false;
  }

  void CheckSketchForOverMemoryLimit() {
    if (!m_MemoryExceeded) {
      // Only do the memory check in the AppState.Standard.  The AppState.MemoryWarning exits to
      // AppState.Standard, so interrupting any other state would have bad consequences. 
      if (App.CurrentState == App.AppState.Standard) {
        m_LastCheckedVertCount = 
            App.Scene.MainCanvas.BatchManager.CountAllBatchVertices() +
            App.Scene.SelectionCanvas.BatchManager.CountAllBatchVertices() +
            WidgetManager.m_Instance.WidgetsVertCount;
        if (m_LastCheckedVertCount > m_MemoryWarningVertCount) {
          if (!m_MemoryWarningAccepted) {
            App.Instance.SetDesiredState(App.AppState.MemoryExceeded);
          }
          m_MemoryExceeded = true;
          App.Switchboard.TriggerMemoryExceededChanged();
        }
      }
    }
  }

  // True if strokes have been modified since last load or save (approximately)
  public bool IsMemoryDirty() {
    if (m_OperationStack.Count != m_LastOperationStackCount) {
      IEnumerable<BaseCommand> newCommands = null;
      if (m_OperationStack.Count > m_LastOperationStackCount) {
        newCommands = m_OperationStack.Take(m_OperationStack.Count - m_LastOperationStackCount);
      } else {
        newCommands = m_RedoStack.Take(m_LastOperationStackCount - m_OperationStack.Count);
      }
      return newCommands.Any(e => e.NeedsSave);
    }
    return false;
  }

  public bool CanUndo() { return m_OperationStack.Count > 0; }
  public bool CanRedo() { return m_RedoStack.Count > 0; }

  void Awake() {
    m_OperationStack = new Stack<BaseCommand>();
    m_LastOperationStackCount = 0;
    m_RedoStack = new Stack<BaseCommand>();
    m_HasVisibleObjects = false;
    m_MemoryExceeded = false;
    m_MemoryWarningAccepted = false;
    m_LastCheckedVertCount = 0;
    m_UndoBatchMesh = GameObject.Instantiate(m_UndoBatchMeshPrefab);
    m_Instance = this;
    m_xfSketchInitial_RS = TrTransform.identity;

    m_MemoryWarningVertCount = App.PlatformConfig.MemoryWarningVertCount;
  }

  void Update() {
    // Brute force, but this should short circuit early.
    m_HasVisibleObjects = m_MemoryList.Any(obj => obj.IsGeometryEnabled);

    // This may be unnecessary to do every frame.  We may, instead, want to do it after
    // specific operations.
    CheckSketchForOverMemoryLimit();

    if (m_DeleteStrokes.Count > 0 || m_RepaintStrokeParent != null) {
      ClearRedo();
      if (m_DeleteStrokes.Count > 0) {
        var parent = new BaseCommand();
        // TODO: should this be done in deterministic order?
        foreach (Stroke stroke in m_DeleteStrokes) {
          new DeleteStrokeCommand(stroke, parent);

          switch (stroke.m_Type) {
          case Stroke.Type.BrushStroke:
            InitUndoObject(stroke.m_Object.GetComponent<BaseBrushScript>());
            break;
          case Stroke.Type.BatchedBrushStroke:
            InitUndoObject(stroke.m_BatchSubset);
            break;
          case Stroke.Type.NotCreated:
            Debug.LogError("Unexpected: MemorizeDeleteSelection NotCreated stroke");
            break;
          }
        }

        PerformAndRecordCommand(parent);
        m_DeleteStrokes.Clear();
      }
      if (m_RepaintStrokeParent != null) {
        PerformAndRecordCommand(m_RepaintStrokeParent);
        m_RepaintStrokeParent = null;
      }
      OperationStackChanged();
    }
  }

  /// Duplicates a stroke. Duplicated strokes have a timestamp that corresponds to the current time.
  public Stroke DuplicateStroke(Stroke srcStroke, CanvasScript canvas, TrTransform? transform) {
    Stroke duplicate = new Stroke(srcStroke);
    if (srcStroke.m_Type == Stroke.Type.BatchedBrushStroke) {
      if (transform == null) {
        duplicate.CopyGeometry(canvas, srcStroke);
      } else {
        // If this fires, consider adding transform support to CreateGeometryByCopying
        Debug.LogWarning("Unexpected: Taking slow DuplicateStroke path");
        duplicate.Recreate(transform, canvas);
      }
    } else {
      duplicate.Recreate(transform, canvas);
    }
    UpdateTimestampsToCurrentSketchTime(duplicate);
    MemoryListAdd(duplicate);
    return duplicate;
  }

  public void PerformAndRecordCommand(BaseCommand command, bool discardIfNotMerged = false) {
    bool discardCommand = discardIfNotMerged;
    BaseCommand delta = command;
    ClearRedo();
    while (m_OperationStack.Any()) {
      BaseCommand top = m_OperationStack.Pop();
      if (!top.Merge(command)) {
        m_OperationStack.Push(top);
        break;
      }
      discardCommand = false;
      command = top;
    }
    if (discardCommand) {
      command.Dispose();
      return;
    }
    delta.Redo();
    m_OperationStack.Push(command);
    OperationStackChanged();
  }

  // TODO: deprecate in favor of PerformAndRecordCommand
  // Used by BrushStrokeCommand and ModifyLightCommmand while in Disco mode
  public void RecordCommand(BaseCommand command) {
    ClearRedo();
    while (m_OperationStack.Any()) {
      BaseCommand top = m_OperationStack.Pop();
      if (!top.Merge(command)) {
        m_OperationStack.Push(top);
        break;
      }
      command = top;
    }
    m_OperationStack.Push(command);
    OperationStackChanged();
  }

  /// Returns approximate latest timestamp from the stroke list (including deleted strokes).
  /// If there are no strokes, raise System.InvalidOperationException
  public double GetApproximateLatestTimestamp() {
    if (m_MemoryList.Count > 0) {
      var obj = m_MemoryList.Last.Value;
      return obj.TailTimestampMs / 1000f;
    }
    throw new InvalidOperationException();
  }

  /// Returns the earliest timestamp from the stroke list (including deleted strokes).
  /// If there are no strokes, raise System.InvalidOperationException
  public double GetEarliestTimestamp() {
    if (m_MemoryList.Count > 0) {
      var obj = m_MemoryList.First.Value;
      return obj.HeadTimestampMs / 1000f;
    }
    throw new InvalidOperationException();
  }

  private static bool StrokeTimeLT(Stroke a, Stroke b) {
    return (a.HeadTimestampMs < b.HeadTimestampMs);
  }

  private static bool StrokeTimeLTE(Stroke a, Stroke b) {
    return !StrokeTimeLT(b, a);
  }

  // The memory list by time is updated-- control points are expected to be initialized
  // and immutable. This includes the timestamps on the control points.
  public void MemoryListAdd(Stroke stroke) {
    Debug.Assert(stroke.m_Type == Stroke.Type.NotCreated ||
                 stroke.m_Type == Stroke.Type.BrushStroke ||
                 stroke.m_Type == Stroke.Type.BatchedBrushStroke);
    if (stroke.m_ControlPoints.Length == 0) {
      Debug.LogWarning("Unexpected zero-length stroke");
      return;
    }

    // add to sequence-time list
    // We add to furthest position possible in the list (i.e. following all strokes
    // with lead control point timestamp LTE the new one).  This ensures that grouped
    // strokes are not divided, given that such strokes must have identical timestamps.
    // NOTE: O(1) given expected timestamp order of strokes in the .tilt file.
    var node = stroke.m_NodeByTime;
    if (m_MemoryList.Count == 0 || StrokeTimeLT(stroke, m_MemoryList.First.Value)) {
      m_MemoryList.AddFirst(node);
    } else {
      // find insert position-- most efficient for "not too far ahead of current position" case
      var addAfter = m_CurrentNodeByTime;
      if (addAfter == null || StrokeTimeLT(stroke, addAfter.Value)) {
        addAfter = m_MemoryList.First;
      }
      while (addAfter.Next != null && StrokeTimeLTE(addAfter.Next.Value, stroke)) {
        addAfter = addAfter.Next;
      }
      m_MemoryList.AddAfter(addAfter, node);
    }
    m_CurrentNodeByTime = node;

    // add to scene playback
    if (m_ScenePlayback != null) {
      m_ScenePlayback.AddStroke(stroke);
    }
  }

  public void MemorizeBatchedBrushStroke(
      BatchSubset subset, Color rColor, Guid brushGuid,
      float fBrushSize, float brushScale,
      List<PointerManager.ControlPoint> rControlPoints, StrokeFlags strokeFlags,
      StencilWidget stencil, float lineLength, int seed) {
    // NOTE: PointerScript calls ClearRedo() in batch case

    Stroke rNewStroke = new Stroke();
    rNewStroke.m_Type = Stroke.Type.BatchedBrushStroke;
    rNewStroke.m_BatchSubset = subset;
    rNewStroke.m_ControlPoints = rControlPoints.ToArray();
    rNewStroke.m_ControlPointsToDrop = new bool[rNewStroke.m_ControlPoints.Length];
    rNewStroke.m_Color = rColor;
    rNewStroke.m_BrushGuid = brushGuid;
    rNewStroke.m_BrushSize = fBrushSize;
    rNewStroke.m_BrushScale = brushScale;
    rNewStroke.m_Flags = strokeFlags;
    rNewStroke.m_Seed = seed;
    subset.m_Stroke = rNewStroke;

    SketchMemoryScript.m_Instance.RecordCommand(
      new BrushStrokeCommand(rNewStroke, stencil, lineLength));

    if (m_SanityCheckStrokes) {
      SanityCheckGeometryGeneration(rNewStroke);
      //SanityCheckVersusReplacementBrush(rNewObject);
    }
    MemoryListAdd(rNewStroke);

    TiltMeterScript.m_Instance.AdjustMeter(rNewStroke, up: true);
  }

  public void MemorizeBrushStroke(
      BaseBrushScript brushScript, Color rColor, Guid brushGuid,
      float fBrushSize, float brushScale,
      List<PointerManager.ControlPoint> rControlPoints,
      StrokeFlags strokeFlags,
      StencilWidget stencil, float lineLength) {
    ClearRedo();

    Stroke rNewStroke = new Stroke();
    rNewStroke.m_Type = Stroke.Type.BrushStroke;
    rNewStroke.m_Object = brushScript.gameObject;
    rNewStroke.m_ControlPoints = rControlPoints.ToArray();
    rNewStroke.m_ControlPointsToDrop = new bool[rNewStroke.m_ControlPoints.Length];
    rNewStroke.m_Color = rColor;
    rNewStroke.m_BrushGuid = brushGuid;
    rNewStroke.m_BrushSize = fBrushSize;
    rNewStroke.m_BrushScale = brushScale;
    rNewStroke.m_Flags = strokeFlags;
    brushScript.Stroke = rNewStroke;

    SketchMemoryScript.m_Instance.RecordCommand(
      new BrushStrokeCommand(rNewStroke, stencil, lineLength));

    MemoryListAdd(rNewStroke);

    TiltMeterScript.m_Instance.AdjustMeter(rNewStroke, up: true);
  }

  /// Queues a stroke for deletion later in the frame. Because deletion is deferred,
  /// the stroke will _not_ look deleted when this function returns.
  ///
  /// It's an error to pass an already deleted stroke. Calling this
  /// multiple times on the same stroke is otherwise allowed.
  public void MemorizeDeleteSelection(Stroke strokeObj) {
    // Note for edit-during-playback:  erase selection only works on fully rendered
    // strokes so a guard against deletion of in-progress strokes isn't needed here.

    // It's only possible to detect double-delete if the stroke is batched, but
    // thankfully that's the common case.
    Debug.Assert(
        strokeObj.m_BatchSubset == null || strokeObj.m_BatchSubset.m_Active,
        "Deleting deleted stroke");

    if (!m_DeleteStrokes.Add(strokeObj)) {
      // The API says this is ok; but it (currently) should never happen either.
      Debug.LogWarningFormat(
          "Unexpected: enqueuing same stroke twice @ {0}",
          Time.frameCount);
    }
  }

  public void MemorizeDeleteSelection(GameObject rObject) {
    var brush = rObject.GetComponent<BaseBrushScript>();
    if (brush) {
      MemorizeDeleteSelection(brush.Stroke);
    }
  }

  public bool MemorizeStrokeRepaint(Stroke stroke, bool recolor, bool rebrush) {
    Guid brushGuid = PointerManager.m_Instance
        .GetPointer(InputManager.ControllerName.Brush).CurrentBrush.m_Guid;
    if ((recolor && stroke.m_Color != PointerManager.m_Instance.PointerColor) ||
        (rebrush && stroke.m_BrushGuid != brushGuid)) {
      if (m_RepaintStrokeParent == null) {
        m_RepaintStrokeParent = new BaseCommand();
      }

      Color newColor = recolor ? PointerManager.m_Instance.PointerColor : stroke.m_Color;
      Guid newGuid = rebrush ? brushGuid : stroke.m_BrushGuid;
      new RepaintStrokeCommand(stroke, newColor, newGuid, m_RepaintStrokeParent);
      return true;
    }
    return false;
  }

  public bool MemorizeStrokeRepaint(GameObject rObject, bool recolor, bool rebrush) {
    var brush = rObject.GetComponent<BaseBrushScript>();
    if (brush) {
      MemorizeStrokeRepaint(brush.Stroke, recolor, rebrush);
      return true;
    }
    return false;
  }

  /// Removes stroke from our list only.
  /// It's the caller's responsibility to destroy if (if desired).
  public void RemoveMemoryObject(Stroke stroke) {
    var nodeByTime = stroke.m_NodeByTime;
    if (nodeByTime.List != null) {  // implies stroke object
      if (m_CurrentNodeByTime == nodeByTime) {
        m_CurrentNodeByTime = nodeByTime.Previous;
      }
      m_MemoryList.Remove(nodeByTime);  // O(1)
      if (m_ScenePlayback != null) {
          m_ScenePlayback.RemoveStroke(stroke);
      }
    }
  }

  // This function is a patch fix for bad data making its way in to save files.  Bad strokes
  // can't be detected until the geometry generation has run over the control points, so we
  // need to parse the memory list after load.  PointerScript.DetachLine() has an early discard
  // for strokes with bad data, so the empty geometry won't be added to the scene, but we'll
  // still have the entry in the memory list.  This function clears those out.
  public void SanitizeMemoryList() {
    LinkedListNode<Stroke> node = m_MemoryList.First;
    while (node != null) {
      LinkedListNode<Stroke> nextNode = node.Next;
      if (node.Value.m_Type == Stroke.Type.BatchedBrushStroke &&
          node.Value.m_BatchSubset.m_VertLength == 0) {
        m_MemoryList.Remove(node);
      }
      node = nextNode;
    }
  }

  public List<Stroke> GetAllUnselectedActiveStrokes() {
    return m_MemoryList.Where(
        s => s.IsGeometryEnabled && s.Canvas == App.Scene.MainCanvas &&
             (s.m_Type != Stroke.Type.BatchedBrushStroke ||
              s.m_BatchSubset.m_VertLength > 0)).ToList();
  }

  public void ClearRedo() {
    foreach (var command in m_RedoStack) {
      command.Dispose();
    }
    m_RedoStack.Clear();
  }

  public void ClearMemory() {
    if (m_ScenePlayback != null) {
      // Ensure scene playback completes so that geometry is in state expected by rest of system.
      // TODO: Lift this restriction.  Artifact observed is a ghost stroke from previously deleted
      // scene appearing briefly.
      App.Instance.CurrentSketchTime = float.MaxValue;
      m_ScenePlayback.Update();
      m_ScenePlayback = null;
    }
    SelectionManager.m_Instance.ForgetStrokesInSelectionCanvas();
    ClearRedo();
    foreach (var item in m_MemoryList) {
      //skip batched strokes here because they'll all get dumped in ResetPools()
      if (item.m_Type != Stroke.Type.BatchedBrushStroke) {
        item.DestroyStroke();
      }
    }
    m_OperationStack.Clear();
    if (OperationStackChanged != null) { OperationStackChanged(); }
    m_LastOperationStackCount = 0;
    m_MemoryList.Clear();
    App.GroupManager.ResetGroups();
    SelectionManager.m_Instance.OnFinishReset();
    m_CurrentNodeByTime = null;
    foreach (var canvas in App.Scene.AllCanvases) {
      canvas.BatchManager.ResetPools();
    }
    TiltMeterScript.m_Instance.ResetMeter();
    App.Instance.CurrentSketchTime = 0;
    App.Instance.AutosaveRestoreFileExists = false;
    m_HasVisibleObjects = false;
    m_MemoryExceeded = false;
    m_LastCheckedVertCount = 0;
    MemoryWarningAccepted = false;
    App.Switchboard.TriggerMemoryExceededChanged();
    SaveLoadScript.m_Instance.MarkAsAutosaveDone();
    SaveLoadScript.m_Instance.NewAutosaveFile();
    m_xfSketchInitial_RS = TrTransform.identity;
    Resources.UnloadUnusedAssets();
  }

  public IEnumerator<float> RepaintCoroutine() {
    int numStrokes = m_MemoryList.Count;
    int strokesRepainted = 0;
    int remainingStrokesPerSlice = 0;
    int totalPrevVerts = 0;
    int totalVerts = 0;

    // First, gather a bunch of info about our strokes.
    bool [] batchEnabled = new bool[m_MemoryList.Count];
    int batchEnabledIndex = 0;
    for (var node = m_MemoryList.First; node != null; node = node.Next) {
      // TODO: Should we skip this stroke if it's particles?
      var stroke = node.Value;
      if (stroke.m_Type == Stroke.Type.BatchedBrushStroke) {
        totalPrevVerts += stroke.m_BatchSubset.m_VertLength;
        batchEnabled[batchEnabledIndex] = stroke.IsGeometryEnabled;
      }
      Array.Clear(stroke.m_ControlPointsToDrop, 0, stroke.m_ControlPointsToDrop.Length);
      stroke.Uncreate();
      ++batchEnabledIndex;
    }

    // Clear pools.
    foreach (var canvas in App.Scene.AllCanvases) {
      canvas.BatchManager.ResetPools();
    }

    // Recreate strokes.
    batchEnabledIndex = 0;
    for (var node = m_MemoryList.First; node != null; node = node.Next) {
      // TODO: Should we skip this stroke if it's particles?
      var stroke = node.Value;
      stroke.Recreate();
      if (stroke.m_Type == Stroke.Type.BatchedBrushStroke) {
        totalVerts += stroke.m_BatchSubset.m_VertLength;
        if (!batchEnabled[batchEnabledIndex]) {
          stroke.m_BatchSubset.m_ParentBatch.DisableSubset(stroke.m_BatchSubset);
        }
      }
      strokesRepainted++;
      remainingStrokesPerSlice--;
      if (remainingStrokesPerSlice < 0) {
        m_RepaintProgress = Mathf.Clamp01(strokesRepainted / (float)numStrokes);
        yield return m_RepaintProgress;
        remainingStrokesPerSlice = numStrokes / 100;
      }

      ++batchEnabledIndex;
    }

    // Report change to user.
    if (totalPrevVerts < totalVerts) {
      float vertChange = (float)totalVerts / (float)totalPrevVerts;
      float increasePercent = (vertChange - 1.0f) * 100.0f;
      int increaseReadable = (int)Mathf.Max(1.0f, Mathf.Floor(increasePercent));
      string report = "Sketch is " + increaseReadable.ToString() + "% larger.";
      OutputWindowScript.m_Instance.CreateInfoCardAtController(
          InputManager.ControllerName.Wand,
          report);
    } else if (totalPrevVerts > totalVerts) {
      float vertChange = (float)totalVerts / (float)totalPrevVerts;
      float decreasePercent = 100.0f - (vertChange * 100.0f);
      int decreaseReadable = (int)Mathf.Max(1.0f, Mathf.Floor(decreasePercent));
      string report = "Sketch is " + decreaseReadable.ToString() + "% smaller.";
      OutputWindowScript.m_Instance.CreateInfoCardAtController(
          InputManager.ControllerName.Wand,
          report);
    } else {
      OutputWindowScript.m_Instance.CreateInfoCardAtController(
          InputManager.ControllerName.Wand,
          "No change in sketch size.");
    }
    ControllerConsoleScript.m_Instance.AddNewLine("Sketch rebuilt! Vertex count: " +
        totalPrevVerts.ToString() + " -> " + totalVerts.ToString());

    m_RepaintCoroutine = null;
  }

  public void StepBack() {
    var comm = m_OperationStack.Pop();
    comm.Undo();
    m_RedoStack.Push(comm);
    OperationStackChanged();
  }

  public void StepForward() {
    var comm = m_RedoStack.Pop();
    comm.Redo();
    m_OperationStack.Push(comm);
    OperationStackChanged();
  }

  public static IEnumerable<Stroke> AllStrokes() {
    return m_Instance.m_MemoryList;
  }

  public static int AllStrokesCount() {
    return m_Instance.m_MemoryList.Count();
  }

  public static void InitUndoObject(BaseBrushScript rBrushScript) {
    rBrushScript.CloneAsUndoObject();
  }

  public static void InitUndoObject(BatchSubset subset) {
    // TODO: Finish moving control of prefab into BatchManager
    subset.Canvas.BatchManager.CloneAsUndoObject(
      subset, m_Instance.m_UndoBatchMesh);
  }

  /// Restore stroke to its unrendered state, deleting underlying geometry.
  /// This is used by rewind functionality of ScenePlayback.
  /// TODO: Rather than destroying geometry for rewind, we should employ the hiding
  /// mechanism of the stroke erase / undo operations.
  public void UnrenderStrokeMemoryObject(Stroke stroke) {
    TiltMeterScript.m_Instance.AdjustMeter(stroke, up: false);
    stroke.Uncreate();
  }

  public void SetPlaybackMode(PlaybackMode mode, float distancePerSecond) {
    m_PlaybackMode = mode;
    m_DistancePerSecond = distancePerSecond;
  }

  public void SpeedUpMemoryDrawingSpeed() {
    m_DistancePerSecond *= 1.25f;
  }

  public void QuickLoadDrawingMemory() {
    if (m_ScenePlayback != null) {
      m_ScenePlayback.QuickLoadRemaining();
    }
  }

  /// timeline edit mode: if forEdit is true, play audio countdown and keep user pointers enabled
  public void BeginDrawingFromMemory(bool bDrawFromStart, bool forEdit = false) {
    if (bDrawFromStart) {
      switch (m_PlaybackMode) {
      case PlaybackMode.Distance:
      default:
        m_ScenePlayback = new ScenePlaybackByStrokeDistance(m_MemoryList);
        PointerManager.m_Instance.SetPointersAudioForPlayback();
        break;
      case PlaybackMode.Timestamps:
        App.Instance.CurrentSketchTime = GetEarliestTimestamp();
        m_ScenePlayback = new ScenePlaybackByTimeLayered(m_MemoryList);
        break;
      }
      m_IsInitialPlay = true;
    }

    if (!forEdit) {
      PointerManager.m_Instance.SetInPlaybackMode(true);
      PointerManager.m_Instance.RequestPointerRendering(false);
    }
  }

  /// returns true if there is more to draw
  public bool ContinueDrawingFromMemory() {
    if (m_ScenePlayback != null && !m_ScenePlayback.Update()) {
      if (m_IsInitialPlay) {
        m_IsInitialPlay = false;
        if (m_ScenePlayback.MaxPointerUnderrun > 0) {
          Debug.LogFormat("Parallel pointer underrun during playback: deficient {0} pointers",
                          m_ScenePlayback.MaxPointerUnderrun);
        }
        // we're done-- however in timeline edit mode we keep playback alive to allow scrubbing
        PointerManager.m_Instance.SetInPlaybackMode(false);
        PointerManager.m_Instance.RequestPointerRendering(true);
        m_ScenePlayback = null;
      }
      return false;
    }
    return true;
  }

  private void SortMemoryList() {
    var sorted = m_MemoryList.OrderBy(obj => obj.HeadTimestampMs).ToArray();
    m_MemoryList.Clear();
    foreach (var obj in sorted) {
      m_MemoryList.AddLast(obj.m_NodeByTime);
    }
  }

  // Redraw scene instantly and optionally re-sort by timestamp-- will drop frames and thrash mem.
  // For use by internal tools which mutate MemoryObjects.
  public void Redraw(bool doSort) {
    ClearRedo();
    if (m_ScenePlayback == null) {
      foreach (var obj in m_MemoryList) {
        SketchMemoryScript.m_Instance.UnrenderStrokeMemoryObject(obj);
      }
      if (doSort) { SortMemoryList(); }
      m_ScenePlayback = new ScenePlaybackByStrokeDistance(m_MemoryList);
      m_ScenePlayback.QuickLoadRemaining();
      m_ScenePlayback.Update();
      m_ScenePlayback = null;
    } else {  // timeline edit mode
      var savedSketchTime = App.Instance.CurrentSketchTime;
      App.Instance.CurrentSketchTime = 0;
      m_ScenePlayback.Update();
      if (doSort) { SortMemoryList(); }
      m_ScenePlayback = new ScenePlaybackByTimeLayered(m_MemoryList);
      App.Instance.CurrentSketchTime = savedSketchTime;
      m_ScenePlayback.Update();
    }
  }

  //
  // Sanity-checking geometry generation
  //

  // Return a non-null error message if the mesh data is not identical
  private static string Compare(
      Vector3[] oldVerts, int iOldVert0, int iOldVert1,
      int[]     oldTris,  int iOldTri0,  int iOldTri1,
      Vector2[] oldUv0s,
      BaseBrushScript newBrush) {

    Vector3[] newVerts; int nNewVerts;
    Vector2[] newUv0s;
    int[] newTris; int nNewTris;
    newBrush.DebugGetGeometry(out newVerts, out nNewVerts, out newUv0s, out newTris, out nNewTris);

    if (nNewVerts != iOldVert1 - iOldVert0) {
      return "vert count mismatch";
    }

    for (int i = 0; i < nNewVerts; ++i) {
      Vector3 vo = oldVerts[iOldVert0 + i];
      Vector3 vn = newVerts[i];
      if (vo != vn) {
        return string.Format("vert mismatch @ {0}/{1}", i, nNewVerts);
      }
    }

    // Before enabling, we need a way of skipping this for brushes that
    // legitimately want randomized UVs. There is also a known nondeterminism
    // for QuadStripBrushStretchUV, though.
#if false
    if (oldUv0s != null) {
      if (newUv0s == null) {
        return "uv0 mismatch (loaded mesh has no UVs)";
      }
      for (int i = 0; i < nNewVerts; ++i) {
        Vector2 uvo = oldUv0s[iOldVert0 + i];
        Vector2 uvn = newUv0s[i];
        if (uvo != uvn) {
          return string.Format("uv mismatch @ {0}/{1}", i, nNewVerts);
        }
      }
    }
#endif

    if (nNewTris != iOldTri1 - iOldTri0) {
      return "tri count mismatch";
    }

    // Try to account for vert numbering
    {
      int triOffset = newTris[0] - oldTris[iOldTri0];
      for (int i = 0; i < nNewTris; ++i) {
        int to = oldTris[iOldTri0 + i];
        int tn = newTris[i];
        if (to + triOffset != tn) {
          return string.Format("tri mismatch @ {0}/{1}", i, nNewTris);
        }
      }
    }
    return null;
  }

  private static void SanityCheckVersusReplacementBrush(Stroke oldStroke) {
    BrushDescriptor desc = BrushCatalog.m_Instance.GetBrush(oldStroke.m_BrushGuid);
    BrushDescriptor replacementDesc = desc.m_Supersedes;
    if (replacementDesc == null) {
      return;
    }

    // Make a copy, since Begin/EndLineFromMemory mutate little bits of MemoryBrushStroke
    Stroke newStroke = new Stroke {
      m_BrushGuid     = replacementDesc.m_Guid,
      m_IntendedCanvas = oldStroke.Canvas,
      m_ControlPoints = oldStroke.m_ControlPoints,
      m_BrushScale    = oldStroke.m_BrushScale,
      m_BrushSize     = oldStroke.m_BrushSize,
      m_Color         = oldStroke.m_Color,
      m_Seed          = oldStroke.m_Seed
    };
    Array.Copy(oldStroke.m_ControlPointsToDrop, newStroke.m_ControlPointsToDrop,
               oldStroke.m_ControlPointsToDrop.Length);

    newStroke.Recreate(TrTransform.T(new Vector3(0.5f, 0, 0)));
  }

  private static string Compare(Mesh oldMesh, BaseBrushScript newBrush) {
    Vector3[] verts = oldMesh.vertices;
    int[] tris = oldMesh.triangles;
    // There is no way of querying Unity what the format of uv0 is, so don't check it
    return Compare(verts, 0, verts.Length, tris, 0, tris.Length, null, newBrush);
  }

  // Generate geometry from stroke as if it were being played back.
  // If played-back geometry is different, complain and make the new stroke visible.
  // TODO: do more kinds of brushes
  private static void SanityCheckGeometryGeneration(Stroke oldStroke) {
    if (oldStroke.m_Type == Stroke.Type.BrushStroke) {
      if (oldStroke.m_Object.GetComponent<MeshFilter>() == null) {
        // Stroke can't be checked
        return;
      }
    }

    {
      BrushDescriptor desc = BrushCatalog.m_Instance.GetBrush(oldStroke.m_BrushGuid);
      if (desc == null || desc.m_Nondeterministic) {
        // Stroke doesn't want to be checked
        return;
      }
    }

    // Re-create geometry. PointerManager's pointer management is a complete mess.
    // "5" is the most-likely to be unused.
    var pointer = PointerManager.m_Instance.GetTransientPointer(5);

    // Make a copy, since Begin/EndLineFromMemory mutate little bits of MemoryBrushStroke
    Stroke newStroke = new Stroke();
    newStroke.m_BrushGuid     = oldStroke.m_BrushGuid;
    newStroke.m_ControlPoints = oldStroke.m_ControlPoints;
    newStroke.m_BrushScale    = oldStroke.m_BrushScale;
    newStroke.m_BrushSize     = oldStroke.m_BrushSize;
    newStroke.m_Color         = oldStroke.m_Color;
    newStroke.m_Seed          = oldStroke.m_Seed;

    // Now swap r and b
    newStroke.m_Color.r = oldStroke.m_Color.b;
    newStroke.m_Color.b = oldStroke.m_Color.r;

    Array.Copy(oldStroke.m_ControlPointsToDrop, newStroke.m_ControlPointsToDrop,
               oldStroke.m_ControlPointsToDrop.Length);

    newStroke.m_Object = pointer.BeginLineFromMemory(newStroke, oldStroke.Canvas);
    pointer.UpdateLineFromStroke(newStroke);

    // Compare geometry
    string err;
    var newBrush = pointer.CurrentBrushScript;
    if (oldStroke.m_Type == Stroke.Type.BrushStroke) {
      Mesh oldMesh = oldStroke.m_Object.GetComponent<MeshFilter>().sharedMesh;
      err = Compare(oldMesh, newBrush);
    } else {
      BatchSubset oldMesh = oldStroke.m_BatchSubset;
      GeometryPool allGeom = oldMesh.m_ParentBatch.Geometry;

      Vector3[] verts = allGeom.m_Vertices.GetBackingArray();
      int[] tris = allGeom.m_Tris.GetBackingArray();

      // To avoid super complications, only check in the common case of
      // uv0 == Vector2[]
      Vector2[] uv0;
      if (allGeom.Layout.texcoord0.size == 2) {
        uv0 = allGeom.m_Texcoord0.v2.GetBackingArray();
      } else {
        uv0 = null;
      }

      err = Compare(
          verts, oldMesh.m_StartVertIndex, oldMesh.m_StartVertIndex + oldMesh.m_VertLength,
          tris, oldMesh.m_iTriIndex, oldMesh.m_iTriIndex + oldMesh.m_nTriIndex,
          uv0, newBrush);
    }

    if (err != null) {
      // Use assert so it shows up in ExceptionRenderScript
      UnityEngine.Debug.Assert(false, string.Format("Sanity check stroke: {0}", err));
      newBrush.ApplyChangesToVisuals();

      // Turn off batching for this stroke because our batching system currently
      // can't handle strokes that aren't on the undo stack, in sketch memory, etc
      bool prevValue = App.Config.m_UseBatchedBrushes;
      App.Config.m_UseBatchedBrushes = false;
      pointer.EndLineFromMemory(newStroke, discard: false);
      App.Config.m_UseBatchedBrushes = prevValue;
    } else {
      pointer.EndLineFromMemory(newStroke, discard: true);
    }
  }

  // Modify a stroke in a way that tries to satisfy:
  // - No control points should be before CurrentSketchTime.
  //   (because they might overlap with previous points)
  // - Few control points should be after CurrentSketchTime
  //   (because they might overlap with to-be-drawn strokes)
  private static void UpdateTimestampsToCurrentSketchTime(Stroke stroke) {
    uint nowMs = (uint)(App.Instance.CurrentSketchTime * 1000);
    uint offsetMs = 0;
    uint nCp = (uint)stroke.m_ControlPoints.Length;
    for (uint iCp = 0; iCp < nCp; ++iCp) {
      stroke.m_ControlPoints[iCp].m_TimestampMs = nowMs + offsetMs++;
    }
  }
}
} // namespace TiltBrush
