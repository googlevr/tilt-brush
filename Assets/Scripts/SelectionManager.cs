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
using UnityEngine;

namespace TiltBrush {

/// A class for managing a set of selected strokes.
///
/// Selection tools call to this class to select or deselect specific strokes.
/// This class also manages a grabwidget that makes the strokes grabbable.
public class SelectionManager : MonoBehaviour {
  public static SelectionManager m_Instance;

  [SerializeField] private SelectionWidget m_SelectionWidget;

  // These are up-to-date mappings from groups to strokes and widgets in the scene. Ie., strokes
  // and widgets that have been disabled will still be in these mappings. These mappings do not
  // include things that are in the group SketchGroupTag.None (ie., ungrouped things).
  private Dictionary<SketchGroupTag, HashSet<Stroke>> m_GroupToStrokes =
      new Dictionary<SketchGroupTag, HashSet<Stroke>>();
  private Dictionary<SketchGroupTag, HashSet<GrabWidget>> m_GroupToWidgets =
      new Dictionary<SketchGroupTag, HashSet<GrabWidget>>();

  // These are up-to-date mappings from groups to strokes and widgets that are currently selected.
  // These also include the group SketchGroupTag.None (ie., selected things that are not grouped).
  // Groups that become empty are removed from these dictionaries so that the count of entries
  // indicate how many different groups (including ungrouped things in SketchGroupTag.None) are
  // selected.
  private Dictionary<SketchGroupTag, HashSet<Stroke>> m_GroupToSelectedStrokes =
      new Dictionary<SketchGroupTag, HashSet<Stroke>>();
  private Dictionary<SketchGroupTag, HashSet<GrabWidget>> m_GroupToSelectedWidgets =
      new Dictionary<SketchGroupTag, HashSet<GrabWidget>>();

  // A reference to the SelectionTool, cached on startup.
  private SelectionTool m_SelectionTool;

  // The list of strokes currently selected.
  // This contains no deleted strokes because conceptually, deletion removes strokes from a
  // selection and re-adds them upon undo.
  // As an internal implementation detail, the selection _canvas_ is allowed to contain
  // strokes that are not selected (likely because they're deleted); so do not conflate this
  // set with the set of strokes in the selection canvas.
  private HashSet<Stroke> m_SelectedStrokes;
  private HashSet<Stroke> m_SelectedStrokesCopyWhileGrabbingGroup;

  // The list of widgets currently selected.
  private HashSet<GrabWidget> m_SelectedWidgets;
  private HashSet<GrabWidget> m_SelectedWidgetsCopyWhileGrabbingGroup;

  private bool m_IsAnimatingTossFromGrabbingGroup;
  private bool m_IsGrabbingGroup;
  private BaseTool.ToolType m_ToolTypeBeforeGrabbingGroup;

  // As opposed to 'add to selection'.  When this is true, strokes picked up
  // by the selection tool will be removed from selected strokes.  When false, they'll be added
  // to the list of selected strokes.
  private bool m_RemoveFromSelection;

  private bool m_bSelectionWidgetNeedsUpdate;

  /// Returns true when SelectedStrokes is not empty.
  public bool HasSelection {
    get {
      return m_SelectedStrokes.Count > 0 || m_SelectedWidgets.Count > 0;
    }
  }

  /// Returns true when cached selection tool is hot.
  public bool SelectionToolIsHot {
    get {
      return (m_SelectionTool != null) && m_SelectionTool.IsHot;
    }
  }

  /// Returns true when the current selection can be grouped.
  public bool SelectionCanBeGrouped {
    get {
      return HasSelection;
    }
  }

  /// Returns true when the current selection is all in a single group.
  public bool SelectionIsInOneGroup {
    get {
      if (m_GroupToSelectedStrokes.Count > 1 || m_GroupToSelectedWidgets.Count > 1 ) {
        // There's more than one group.
        return false;
      }
      if (m_GroupToSelectedStrokes.Count == 0 && m_GroupToSelectedWidgets.Count == 0 ) {
        // There are no groups. Ie., there is no selection.
        return false;
      }
      if (m_GroupToSelectedStrokes.Count == 1) {
        if (m_GroupToSelectedWidgets.Count == 1 ) {
          // There's one group present in both strokes and widgets.
          return m_GroupToSelectedStrokes.Keys.First() != SketchGroupTag.None &&
                 m_GroupToSelectedWidgets.Keys.First() != SketchGroupTag.None &&
                 m_GroupToSelectedStrokes.Keys.First() == m_GroupToSelectedWidgets.Keys.First();
        } else {
          // There's one group present in only strokes.
          return m_GroupToSelectedStrokes.Keys.First() != SketchGroupTag.None;
        }
      }
      // There's one group present in only widgets.
      return m_GroupToSelectedWidgets.Keys.First() != SketchGroupTag.None;
    }
  }

  /// Returns the number of strokes in the current selection.
  public int SelectedStrokeCount {
    get {
      return m_SelectedStrokes.Count;
    }
  }

  /// Returns the vert count for the current selection.
  public int NumVertsInSelection {
    get {
      int count = 0;
      foreach (var stroke in m_SelectedStrokes) {
        count += stroke.m_BatchSubset.m_VertLength;
      }
      foreach (var widget in m_SelectedWidgets) {
        ModelWidget mw = widget as ModelWidget;
        if (mw != null) {
          count += mw.GetNumVertsInModel();
        }
      }
      return count;
    }
  }

  /// The collection of currently selected strokes.
  /// Returns the strokes in no particular order.
  public IEnumerable<Stroke> SelectedStrokes {
    get {
      return m_SelectedStrokes;
    }
  }

  /// The collection of currently selected widgets.
  /// Returns the widgets in no particular order.
  public IEnumerable<GrabWidget> SelectedWidgets {
    get {
      return m_SelectedWidgets;
    }
  }

  public bool SelectionWasTransformed {
    get {
      // The reason we check for approximate equality between selection and active canvas's poses
      // is that the selection canvas pose might be set to what's intended to be its initial
      // pose by the means of a transformation -- which introduces imprecision.
      //
      // As an example, the SelectionWidget, when moved, sets the selection transform to whatever
      // the widget box's global delta transform is within canvas space. This is not lossless.
      // When the user moves the widget box, then undoes the move, the widget box moves back to its
      // start, but the operations performed to get turn its global position into a canvas-space
      // transformation sometimes introduce non-identiy values.
      return !TrTransform.Approximately(App.ActiveCanvas.Pose, App.Scene.SelectionCanvas.Pose);
    }
  }

  public void RemoveFromSelection(bool remove) {
    bool bRemoveFromSelectionPrev = m_RemoveFromSelection;

    if (HasSelection) {
      m_RemoveFromSelection = remove;
    } else {
      // Only allow deselection if we have a selection.
      m_RemoveFromSelection = false;
    }

    // Hide the main canvas if and only if we are in deselect mode.
    App.Scene.MainCanvas.gameObject.SetActive(!m_RemoveFromSelection);

    // Start the animation to toggle the tool. At the end of the animation, the visual
    // representation of the tool will match the mode.
    if (bRemoveFromSelectionPrev != remove) {
      m_SelectionTool.StartToggleAnimation();
    }
  }

  public bool ShouldRemoveFromSelection() {
    return m_RemoveFromSelection;
  }

  private bool ShouldShowSelectedStrokes {
    get {
      return !m_SelectionTool.IsHot || ShouldRemoveFromSelection();
    }
  }

  /// Selection transformation relative to the scene.
  public TrTransform SelectionTransform {
    get {
      return App.ActiveCanvas.Pose.inverse * App.Scene.SelectionCanvas.Pose;
    }
    set {
      App.Scene.SelectionCanvas.Pose = App.ActiveCanvas.Pose * value;
    }
  }

  public bool IsAnimatingTossFromGrabbingGroup => m_IsAnimatingTossFromGrabbingGroup;

  /// Returns the active strokes in the given group.
  public IEnumerable<Stroke> StrokesInGroup(SketchGroupTag group) {
    if (m_GroupToStrokes.ContainsKey(group)) {
      foreach (var stroke in m_GroupToStrokes[group]) {
        if (stroke.IsGeometryEnabled) {
          yield return stroke;
        }
      }
    }
  }

  public IEnumerable<GrabWidget> WidgetsInGroup(SketchGroupTag group) {
    if (m_GroupToWidgets.ContainsKey(group)) {
      foreach (var widget in m_GroupToWidgets[group]) {
        if (widget.IsAvailable()) {
          yield return widget;
        }
      }
    }
  }

  public void OnStrokeRemovedFromGroup(Stroke stroke, SketchGroupTag oldGroup) {
    // If the stroke is in the selection, then we need to update our group to selected strokes
    // mapping.
    if (m_SelectedStrokes.Contains(stroke)) {
      RemoveFromGroupToSelectedStrokes(oldGroup, stroke);
    }

    // Remove the stroke from the old group to strokes mapping.
    if (oldGroup != SketchGroupTag.None) {
      // Remove this stroke from the dictionary entry for the old group.
      m_GroupToStrokes[oldGroup].Remove(stroke);
    }
  }

  public void OnWidgetRemovedFromGroup(GrabWidget widget, SketchGroupTag oldGroup) {
    // If the widget is in the selection, then we need to update our group to selected widget
    // mapping.
    if (m_SelectedWidgets.Contains(widget)) {
      RemoveFromGroupToSelectedWidgets(oldGroup, widget);
    }

    // Remove the widget from the old group to widgets mapping.
    if (oldGroup != SketchGroupTag.None) {
      // Remove this widget from the dictionary entry for the old group.
      m_GroupToWidgets[oldGroup].Remove(widget);
    }
  }

  public void OnStrokeAddedToGroup(Stroke stroke) {
    SketchGroupTag newGroup = stroke.Group;

    // If the stroke is in the selection, then we need to update our group to selected strokes
    // mapping.
    if (m_SelectedStrokes.Contains(stroke)) {
      AddToGroupToSelectedStrokes(stroke.Group, stroke);
    }

    // Add the stroke to the new group to strokes mapping.
    if (newGroup != SketchGroupTag.None) {
      // Add this stroke to the dictionary entry for the new group.
      if (!m_GroupToStrokes.TryGetValue(newGroup, out var newGroupStrokes)) {
        newGroupStrokes = m_GroupToStrokes[newGroup] = new HashSet<Stroke>();
      }
      newGroupStrokes.Add(stroke);
    }
  }

  public void OnWidgetAddedToGroup(GrabWidget widget) {
    SketchGroupTag newGroup = widget.Group;

    // If the widget is in the selection, then we need to update our group to selected widget
    // mapping.
    if (m_SelectedWidgets.Contains(widget)) {
      AddToGroupToSelectedWidgets(widget.Group, widget);
    }

    // Add the widget to the new group to widgets mapping.
    if (newGroup != SketchGroupTag.None) {
      // Add this widget to the dictionary entry for the new group.
      if (!m_GroupToWidgets.TryGetValue(newGroup, out var newGroupWidgets)) {
        newGroupWidgets = m_GroupToWidgets[newGroup] = new HashSet<GrabWidget>();
      }
      newGroupWidgets.Add(widget);
    }
  }

  /// Reset the selection manager.
  public void OnFinishReset() {
    m_GroupToStrokes.Clear();
    m_GroupToSelectedStrokes.Clear();
    m_GroupToSelectedWidgets.Clear();
  }

  void Awake() {
    m_Instance = this;
    m_SelectedStrokes = new HashSet<Stroke>();
    m_SelectedWidgets = new HashSet<GrabWidget>();
  }

  public void CacheSelectionTool(SelectionTool tool) {
    m_SelectionTool = tool;
  }

  void Start() {
    m_SelectionWidget.SelectionTransformed += OnSelectionTransformed;
  }

  void Update() {
    if (HasSelection) {
      // TODO: if !HasSelection, batches that UpdateSelectionVisibility has hidden
      // will remain hidden; is that OK?
      UpdateSelectionVisibility();
      RegisterHighlights();
    }
  }

  // Potentially suppresses rendering of the selection itself.
  // eg, if the selection tool is active, rendering should be suppressed so the user
  // can better see things that remain unselected.
  void UpdateSelectionVisibility() {
    bool showSelection = ShouldShowSelectedStrokes;

#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      // Strokes of type BrushStroke currently only exist in experimental builds.
      // The list of selected strokes might be quite long, so we want to avoid iterating it.
      foreach (Stroke stroke in m_SelectedStrokes) {
        if (stroke.m_Type == Stroke.Type.BrushStroke) {
          stroke.m_Object.SetActive(showSelection);
        }
      }
    }
#endif
    App.Scene.SelectionCanvas.BatchManager.SetVisibility(showSelection);

    m_SelectionWidget.gameObject.SetActive(showSelection);

    foreach (GrabWidget widget in m_SelectedWidgets) {
      widget.gameObject.SetActive(showSelection && widget.IsAvailable());
    }
  }

  // Register highlights for all selected objects
  void RegisterHighlights() {
    bool showHighlight =
        !SketchControlsScript.m_Instance.IsUserAbleToInteractWithAnyWidget() ||
        SketchControlsScript.m_Instance.IsUserIntersectingWithSelectionWidget() ||
        SketchControlsScript.m_Instance.IsUserInteractingWithSelectionWidget();
    if (showHighlight) {
      foreach (GrabWidget widget in m_SelectedWidgets) {
        widget.RegisterHighlight();
      }
#if !UNITY_ANDROID
      App.Scene.SelectionCanvas.RegisterHighlight();
#endif
    }
  }

  void LateUpdate() {
    ResolveChanges();
  }

  /// Select a group that a widget belongs to and then return the corresponding selection widget.
  public SelectionWidget StartGrabbingGroupWithWidget(GrabWidget grabWidget) {
    m_IsGrabbingGroup = true;

    // Save off the current tool and selection.
    m_ToolTypeBeforeGrabbingGroup = SketchSurfacePanel.m_Instance.ActiveToolType;
    m_SelectedStrokesCopyWhileGrabbingGroup = new HashSet<Stroke>(m_SelectedStrokes);
    m_SelectedWidgetsCopyWhileGrabbingGroup = new HashSet<GrabWidget>(m_SelectedWidgets);

    // Select the group that the widget belongs to.
    ClearActiveSelection();
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new SelectCommand(null, new [] { grabWidget },
                          SelectionTransform,
                          deselect: false, initial: true, isGrabbingGroup: true));
    UpdateSelectionWidget();
    ResolveChanges();

    SketchSurfacePanel.m_Instance.ActiveTool.HideTool(true);
    return m_SelectionWidget;
  }

  public void EndGrabbingGroupWithWidget() {
    if (m_SelectionWidget.IsTossed()) {
      m_IsAnimatingTossFromGrabbingGroup = true;
    } else {
      // Restore the original selection and tool.
      SketchSurfacePanel.m_Instance.ActiveTool.HideTool(false);
      ClearActiveSelection();
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(
          new SelectCommand(m_SelectedStrokesCopyWhileGrabbingGroup,
                            m_SelectedWidgetsCopyWhileGrabbingGroup,
                            SelectionTransform,
                            isGrabbingGroup: true, isEndGrabbingGroup: true));
      m_SelectedStrokesCopyWhileGrabbingGroup = null;
      m_SelectedWidgetsCopyWhileGrabbingGroup = null;
      SketchSurfacePanel.m_Instance.EnableSpecificTool(m_ToolTypeBeforeGrabbingGroup);

      m_IsGrabbingGroup = false;
    }
  }

  public void ResolveChanges() {
    if (m_bSelectionWidgetNeedsUpdate) {
      m_SelectionWidget.SelectionTransform = SelectionTransform;
      if (HasSelection) {
        Bounds selectionBounds;
        // If we don't have strokes selected, use the bounds of our widgets, only.
        if (m_SelectedStrokes.Count == 0) {
          selectionBounds = GetBoundsOfSelectedWidgets_SelectionCanvasSpace();
        } else if (m_SelectedWidgets.Count == 0) {
          // If we don't have widgets, use the bounds of our strokes, only.
          selectionBounds = App.Scene.SelectionCanvas.GetCanvasBoundingBox(onlyActive: true);
        } else {
          selectionBounds = App.Scene.SelectionCanvas.GetCanvasBoundingBox(onlyActive: true);
          selectionBounds.Encapsulate(GetBoundsOfSelectedWidgets_SelectionCanvasSpace());
        }

        m_SelectionWidget.SetSelectionBounds(selectionBounds);

        bool selectionPinned = false;
        m_SelectionWidget.ResetSizeRange();
        foreach (GrabWidget widget in m_SelectedWidgets) {
          float widgetToSelectionScale =
              Mathf.Abs(m_SelectionWidget.GetSignedWidgetSize() / widget.GetSignedWidgetSize());
          // Updates the size range of the selection widget, which will get shrunk to the smallest
          // maximum size of all the widgets in the selection.
          m_SelectionWidget.UpdateSizeRange(widget.GetWidgetSizeRange() * widgetToSelectionScale);
          if (widget.Pinned) {
            selectionPinned = true;
            break;
          }
        }
        m_SelectionWidget.PreventSelectionFromMoving(selectionPinned);
        AudioManager.m_Instance.SelectionHighlightLoop(true);
      } else {
        m_SelectionWidget.PreventSelectionFromMoving(false);
        m_SelectionWidget.SelectionCleared();
        AudioManager.m_Instance.SelectionHighlightLoop(false);
      }
      m_bSelectionWidgetNeedsUpdate = false;
    }
  }

  public void ClearActiveSelection() {
    // Make sure we don't have a selection active.
    if (HasSelection) {
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(
          CreateEndSelectionCommand());
      App.Scene.MainCanvas.gameObject.SetActive(true);
    }
  }

  /// Creates a command that deselects all of the strokes currently selected.
  public BaseCommand CreateEndSelectionCommand() {
    return new SelectCommand(m_SelectedStrokes, m_SelectedWidgets, SelectionTransform,
        deselect: true, checkForClearedSelection: true,
        isGrabbingGroup: m_IsGrabbingGroup);
  }

  ///  Delete all currently selected strokes.
  public void DeleteSelection() {
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new DeleteSelectionCommand(m_SelectedStrokes, m_SelectedWidgets));
    RemoveFromSelection(false);
    if (m_IsAnimatingTossFromGrabbingGroup) {
      m_IsAnimatingTossFromGrabbingGroup = false;
      EndGrabbingGroupWithWidget();
    }
  }

  public DeleteSelectionCommand CurrentDeleteSelectionCommand() {
    return new DeleteSelectionCommand(m_SelectedStrokes, m_SelectedWidgets);
  }

  /// Ends the selection without moving strokes back to the main canvas.
  /// This should only be called when we're clearing the scene and need to quickly
  /// forget our selection, knowing that the selection canvas will soon be cleared
  /// anyway.
  public void ForgetStrokesInSelectionCanvas() {
    m_SelectedStrokes.Clear();
    m_SelectedWidgets.Clear();
    SelectionTransform = TrTransform.identity;
    UpdateSelectionWidget();
  }

  public void SelectStrokes(IEnumerable<Stroke> strokes) {
    foreach (var stroke in strokes) {
      if (IsStrokeSelected(stroke)) {
        Debug.LogWarning("Attempted to select stroke that is already selected.");
        continue;
      }

      stroke.SetParentKeepWorldPosition(App.Scene.SelectionCanvas, SelectionTransform.inverse);
      m_SelectedStrokes.Add(stroke);

      if (!m_GroupToSelectedStrokes.TryGetValue(stroke.Group, out var groupStrokes)) {
        groupStrokes = m_GroupToSelectedStrokes[stroke.Group] = new HashSet<Stroke>();
      }
      Debug.Assert(!groupStrokes.Contains(stroke));
      groupStrokes.Add(stroke);
    }

    // If the manager is tasked to select strokes, make sure the SelectionTool is active.
    // b/64029485 In the event that the user does not have the SelectionTool active and presses
    // undo causing strokes to be highlighted, force the user to have the SelectionTool.
    SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.SelectionTool);
  }

  public void DeselectStrokes(IEnumerable<Stroke> strokes) {
    foreach (var stroke in strokes) {
      if (!IsStrokeSelected(stroke)) {
        Debug.LogWarning("Attempted to deselect stroke that is not selected.");
        continue;
      }

      stroke.SetParentKeepWorldPosition(App.ActiveCanvas, SelectionTransform);
      m_SelectedStrokes.Remove(stroke);

      var groupStrokes = m_GroupToSelectedStrokes[stroke.Group];
      groupStrokes.Remove(stroke);
      if (groupStrokes.Count == 0) {
        m_GroupToSelectedStrokes.Remove(stroke.Group);
      }
    }

    if (!HasSelection) {
      SelectionTransform = TrTransform.identity;
    }
  }

  public void SelectWidgets(IEnumerable<GrabWidget> widgets) {
    foreach (var widget in widgets) {
      if (IsWidgetSelected(widget)) {
        Debug.LogWarning("Attempted to select widget that is already selected.");
        continue;
      }

      widget.SetCanvas(App.Scene.SelectionCanvas);
      HierarchyUtils.RecursivelySetLayer(widget.transform,
          App.Scene.SelectionCanvas.gameObject.layer);
      m_SelectedWidgets.Add(widget);

      if (!m_GroupToSelectedWidgets.TryGetValue(widget.Group, out var groupWidgets)) {
        groupWidgets = m_GroupToSelectedWidgets[widget.Group] = new HashSet<GrabWidget>();
      }
      Debug.Assert(!groupWidgets.Contains(widget));
      groupWidgets.Add(widget);
    }

    // If the manager is tasked to select something, make sure the SelectionTool is active.
    // b/64029485 In the event that the user does not have the SelectionTool active and presses
    // undo causing something to be highlighted, force the user to have the SelectionTool.
    SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.SelectionTool);
  }

  public void DeselectWidgets(IEnumerable<GrabWidget> widgets) {
    foreach (var widget in widgets) {
      if (!IsWidgetSelected(widget)) {
        Debug.LogWarning("Attempted to deselect widget that is not selected.");
        continue;
      }

      widget.SetCanvas(App.ActiveCanvas);
      widget.RestoreGameObjectLayer(App.ActiveCanvas.gameObject.layer);
      widget.gameObject.SetActive(true);
      m_SelectedWidgets.Remove(widget);

      var groupWidgets = m_GroupToSelectedWidgets[widget.Group];
      groupWidgets.Remove(widget);
      if (groupWidgets.Count == 0) {
        m_GroupToSelectedWidgets.Remove(widget.Group);
      }
    }

    if (!HasSelection) {
      SelectionTransform = TrTransform.identity;
    }
  }

  public void RegisterStrokesInSelectionCanvas(ICollection<Stroke> strokes) {
    foreach (var stroke in strokes) {
      m_SelectedStrokes.Add(stroke);
      AddToGroupToSelectedStrokes(stroke.Group, stroke);
    }
    UpdateSelectionWidget();
  }

  public void DeregisterStrokesInSelectionCanvas(ICollection<Stroke> strokes) {
    foreach (var stroke in strokes) {
      m_SelectedStrokes.Remove(stroke);
      RemoveFromGroupToSelectedStrokes(stroke.Group, stroke);
    }
    UpdateSelectionWidget();
  }

  public void RegisterWidgetsInSelectionCanvas(ICollection<GrabWidget> widgets) {
    foreach (var widget in widgets) {
      m_SelectedWidgets.Add(widget);
      AddToGroupToSelectedWidgets(widget.Group, widget);
    }
    UpdateSelectionWidget();
  }

  public void DeregisterWidgetsInSelectionCanvas(ICollection<GrabWidget> widgets) {
    foreach (var widget in widgets) {
      m_SelectedWidgets.Remove(widget);
      RemoveFromGroupToSelectedWidgets(widget.Group, widget);
    }
    UpdateSelectionWidget();
  }

  public void InvertSelection() {
    // Build a list of all the strokes in the main canvas.
    List<Stroke> unselectedStrokes =
        SketchMemoryScript.m_Instance.GetAllUnselectedActiveStrokes();

    // Build a list of all the unpinned widgets in the main canvas.
    List<GrabWidget> unselectedWidgets =
        WidgetManager.m_Instance.GetAllUnselectedActiveWidgets();

    // Select everything that was in the main canvas.
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new InvertSelectionCommand(unselectedStrokes, m_SelectedStrokes,
          unselectedWidgets, m_SelectedWidgets));
  }

  public void FlipSelection() {
    // Flip the selection.
    TrTransform selectionFromWorldSpace =
        TrTransform.FromTransform(App.Scene.SelectionCanvas.transform).inverse;

    Plane flipPlaneInSelectionSpace = new Plane(
        selectionFromWorldSpace * m_SelectionWidget.transform.position,
        selectionFromWorldSpace * ViewpointScript.Head.position,
        selectionFromWorldSpace * (ViewpointScript.Head.position + Vector3.up));
#if false
    // useful for precise testing
    if (PointerManager.m_Instance.SymmetryPlane_RS is Plane plane_RS) {
      flipPlaneInSelectionSpace = App.Scene.SelectionCanvas.Pose.inverse * plane_RS;
    }
#endif

    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new FlipSelectionCommand(m_SelectedStrokes, m_SelectedWidgets, flipPlaneInSelectionSpace));
  }

  public void SelectAll() {
    // Build a list of all the strokes in the main canvas.
    List<Stroke> unselectedStrokes =
        SketchMemoryScript.m_Instance.GetAllUnselectedActiveStrokes();

    // Build a list of all the unpinned widgets in the main canvas.
    List<GrabWidget> unselectedWidgets =
        WidgetManager.m_Instance.GetAllUnselectedActiveWidgets();

    // Select em all.
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new SelectCommand(unselectedStrokes, unselectedWidgets,
                          SelectionManager.m_Instance.SelectionTransform,
                          deselect: false, initial: false));
  }

  /// Groups all the selected strokes into a single new group unless they are already in a single
  /// group. In that case, ungroup them all.
  public void ToggleGroupSelectedStrokesAndWidgets() {
    if (!SelectionCanBeGrouped) {
      return;
    }

    // If all the selected strokes are in one group, ungroup by setting the new group to None.
    // Otherwise, create a new group by setting the target group parameter to null.
    bool selectionIsInOneGroup = SelectionIsInOneGroup;
    SketchGroupTag? targetGroup =
        selectionIsInOneGroup ? SketchGroupTag.None : (SketchGroupTag?) null;
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new GroupStrokesAndWidgetsCommand(m_SelectedStrokes, m_SelectedWidgets, targetGroup: targetGroup));

    OutputWindowScript.m_Instance.CreateInfoCardAtController(
        InputManager.ControllerName.Brush, selectionIsInOneGroup ? "Ungrouped!" : "Grouped!");
    var pos = InputManager.m_Instance.GetControllerPosition(InputManager.ControllerName.Brush);
    AudioManager.m_Instance.PlayGroupedSound(pos);
  }

  /// Consumers who call SelectStroke(s) or DeselectStroke(s) must call this once they're
  /// done making a series of selections or deselections.
  public void UpdateSelectionWidget() {
    m_bSelectionWidgetNeedsUpdate = true;
  }

  public bool IsStrokeSelected(Stroke stroke) {
    return m_SelectedStrokes.Contains(stroke);
  }

  public bool IsWidgetSelected(GrabWidget widget) {
    return m_SelectedWidgets.Contains(widget);
  }

  private void RemoveFromGroupToSelectedStrokes(SketchGroupTag group, Stroke stroke) {
    var groupStrokes = m_GroupToSelectedStrokes[group];
    groupStrokes.Remove(stroke);
    if (groupStrokes.Count == 0) {
      m_GroupToSelectedStrokes.Remove(group);
    }

    App.Switchboard.TriggerSelectionChanged();
  }

  private void RemoveFromGroupToSelectedWidgets(SketchGroupTag group, GrabWidget widget) {
    var groupWidgets = m_GroupToSelectedWidgets[group];
    groupWidgets.Remove(widget);
    if (groupWidgets.Count == 0) {
      m_GroupToSelectedWidgets.Remove(group);
    }

    App.Switchboard.TriggerSelectionChanged();
  }

  private void AddToGroupToSelectedStrokes(SketchGroupTag group, Stroke stroke) {
    if (!m_GroupToSelectedStrokes.TryGetValue(group, out var groupStrokes)) {
      groupStrokes = m_GroupToSelectedStrokes[group] = new HashSet<Stroke>();
    }
    Debug.Assert(!groupStrokes.Contains(stroke));
    groupStrokes.Add(stroke);

    App.Switchboard.TriggerSelectionChanged();
  }

  private void AddToGroupToSelectedWidgets(SketchGroupTag group, GrabWidget widget) {
    if (!m_GroupToSelectedWidgets.TryGetValue(group, out var groupWidgets)) {
      groupWidgets = m_GroupToSelectedWidgets[group] = new HashSet<GrabWidget>();
    }
    Debug.Assert(!groupWidgets.Contains(widget));
    groupWidgets.Add(widget);

    App.Switchboard.TriggerSelectionChanged();
  }

  private void OnSelectionTransformed(TrTransform xf_SS) {
    SelectionTransform = xf_SS;
  }

  Bounds GetBoundsOfSelectedWidgets_SelectionCanvasSpace() {
    Bounds totalBounds_CS = new Bounds();
    bool boundsInitialized = false;
    foreach (GrabWidget widget in m_SelectedWidgets) {
      Bounds widgetBounds_CS = widget.GetBounds_SelectionCanvasSpace();
      if (!boundsInitialized) {
        // If this is the first widget we're looking at, initialize the bounds with the first
        // widget's bounds.
        totalBounds_CS = widgetBounds_CS;
        boundsInitialized = true;
      } else {
        totalBounds_CS.Encapsulate(widgetBounds_CS);
      }
    }

    return totalBounds_CS;
  }
}

}  // namespace TiltBrush
