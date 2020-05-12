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

namespace TiltBrush {

public class SketchSurfacePanel : BasePanel {
  static public SketchSurfacePanel m_Instance;

  private bool m_BacksideIsActive;
  private bool m_FreePaintMode = false;

  private BaseTool[] m_Tools;
  private int m_ActiveToolIndex;
  private float m_ToolSelectionAggregateValue;
  private float m_ToolSelectionThreshold = 0.2f;
  private List<BaseTool> m_ToolsToMonitor;

  protected bool m_ToolHideRequested;

  private bool m_SurfaceIsDrawable = true;

  [NonSerialized] public bool m_UpdatedToolThisFrame = false;

  public bool IsSurfaceDrawable() { return m_SurfaceIsDrawable; }
  public bool IsSketchSurfaceToolActive() {
    return GetCurrentToolType() == BaseTool.ToolType.SketchSurface;
  }

  public void SetInFreePaintMode(bool bFreePaint) { m_FreePaintMode = bFreePaint; }
  public bool IsInFreePaintMode() { return m_FreePaintMode; }

  public BaseTool ActiveTool {
    get { return m_Tools[m_ActiveToolIndex]; }
  }

  public BaseTool.ToolType ActiveToolType {
    get { return m_Tools[m_ActiveToolIndex].m_Type; }
  }

  BaseTool.ToolType DefaultToolType() {
    if (m_FreePaintMode) {
      return BaseTool.ToolType.FreePaintTool;
    }
    return BaseTool.ToolType.SketchSurface;
  }

  override protected void Awake() {
    base.Awake();
    m_Instance = this;

    m_ToolsToMonitor = new List<BaseTool>();
    m_PanelDescriptionState = DescriptionState.Closed;
    m_PanelFlairState = DescriptionState.Closed;
  }

  void Start() {
    //get all tools from our children
    m_Tools = GetComponentsInChildren<BaseTool>(true);

    m_ActiveToolIndex = 0;
    m_ToolSelectionAggregateValue = 0.0f;

    //init and then turn them all off
    for (int i = 0; i < m_Tools.Length; ++i) {
      m_Tools[i].Init();
      m_Tools[i].gameObject.SetActive(false);
    }

    m_ToolHideRequested = false;
  }

  void OnDisable() {
    m_AddedNewPoseListener = false;
    App.VrSdk.NewControllerPosesApplied -= OnNewPoses;
  }

  bool m_AddedNewPoseListener = false;
  void Update() {
    // Do this here instead of OnEnable() to ensure that our event handler gets
    // called last, after all the controllers. Hacky, and this will probably break
    // if we dynamically create controllers. Alternatively, we can modify SteamVR_Render.cs
    // and add a "post_new_poses" event
    if (! m_AddedNewPoseListener) {
      m_AddedNewPoseListener = true;
      App.VrSdk.NewControllerPosesApplied += OnNewPoses;
    }

    BaseUpdate();

    //update tool rendering according to state
    if (!m_FreePaintMode || InputManager.Brush.IsTrackedObjectValid) {
      //show tool according to requested visibility
      if (m_ToolHideRequested && !ActiveTool.ToolHidden()) {
        ActiveTool.HideTool(true);
      } else if (!m_ToolHideRequested && ActiveTool.ToolHidden()) {
        ActiveTool.HideTool(false);
      }
    } else if (!ActiveTool.ToolHidden()) {
      //turn off tool
      ActiveTool.HideTool(true);
    }

    // Monitor requested tools.
    for (int i = 0; i < m_ToolsToMonitor.Count; ++i) {
      m_ToolsToMonitor[i].Monitor();
    }
  }

  public void BeginMonitoringTool(BaseTool tool) {
    // Add unique.
    bool bAdd = true;
    for (int i = 0; i < m_ToolsToMonitor.Count; ++i) {
      if (m_ToolsToMonitor[i].m_Type == tool.m_Type) {
        bAdd = false;
        break;
      }
    }
    if (bAdd) {
      m_ToolsToMonitor.Add(tool);
    }
  }

  public void StopMonitoringTool(BaseTool tool) {
    for (int i = 0; i < m_ToolsToMonitor.Count; ++i) {
      if (m_ToolsToMonitor[i].m_Type == tool.m_Type) {
        m_ToolsToMonitor.RemoveAt(i);
        break;
      }
    }
  }

  public void SetBacksideActive(Vector3 vCamPosition) {
    Vector3 vToUs = transform.position - vCamPosition;
    vToUs.Normalize();

    float fDot = Vector3.Dot(transform.forward, vToUs);
    if (!m_BacksideIsActive && (fDot < -0.05f)) {
      m_BacksideIsActive = true;
    } else if (m_BacksideIsActive && (fDot > 0.05f)) {
      m_BacksideIsActive = false;
    }
    ActiveTool.BacksideActive(m_BacksideIsActive);
  }

  override public void GetReticleTransform(
      out Vector3 vPos, out Vector3 vForward, bool bGazeAndTap) {
    // If the pointer is in an invalid draw location this set this to false
    m_SurfaceIsDrawable = true;

    // Standard math for computing pointer transform
    Vector3 vTransformedOffset = transform.rotation * m_ReticleOffset;
    vPos = transform.position + vTransformedOffset;
    vForward = -transform.forward;
  }

  public void EnableSpecificTool(BaseTool.ToolType rType) {
    if (ActiveTool.m_Type == rType) {
      return;
    }

    for (int i = 0; i < m_Tools.Length; ++i) {
      if (m_Tools[i].m_Type == rType) {
        ActiveTool.EnableTool(false);

        m_ActiveToolIndex = i;
        ActiveTool.EnableTool(true);
        m_ToolSelectionAggregateValue = 0.0f;
        App.Switchboard.TriggerToolChanged();
        return;
      }
    }
  }

  public void DisableSpecificTool(BaseTool.ToolType rType) {
    //only disable the tool if it's the one we've got selected
    if (ActiveTool.m_Type == rType) {
      EnableDefaultTool();
    }
  }

  public void EnableDefaultTool() {
    EnableSpecificTool(DefaultToolType());
  }

  public bool IsDefaultToolEnabled() {
    return ActiveTool.m_Type == DefaultToolType();
  }

  public BaseTool GetToolOfType(BaseTool.ToolType type) {
    for (int i = 0; i < m_Tools.Length; ++i) {
      if (m_Tools[i].m_Type == type) {
        return m_Tools[i];
      }
    }
    return null;
  }

  public void VerifyValidToolWithColorUpdate() {
    if (ActiveTool.m_Type != BaseTool.ToolType.RepaintTool &&
        ActiveTool.m_Type != BaseTool.ToolType.RecolorTool) {
      EnableDefaultTool();
    }
  }

  // If the user has a tool reserved for advanced mode, revert back to default tool.
  public void EnsureUserHasBasicToolEnabled() {
    BaseTool.ToolType activeToolType = ActiveTool.m_Type;
    if (activeToolType == BaseTool.ToolType.Selection ||
        activeToolType == BaseTool.ToolType.ColorPicker ||
        activeToolType == BaseTool.ToolType.DropperTool ||
        activeToolType == BaseTool.ToolType.BrushPicker ||
        activeToolType == BaseTool.ToolType.BrushAndColorPicker ||
        activeToolType == BaseTool.ToolType.RepaintTool ||
        activeToolType == BaseTool.ToolType.RecolorTool ||
        activeToolType == BaseTool.ToolType.RebrushTool ||
        activeToolType == BaseTool.ToolType.PinTool ||
        activeToolType == BaseTool.ToolType.SelectionTool) {
      EnableDefaultTool();
    }
  }

  public void RequestHideActiveTool(bool bHide) {
    m_ToolHideRequested = bHide;
  }

  bool IsScrollableTool(BaseTool.ToolType rType) {
    return (rType == BaseTool.ToolType.SketchSurface) ||
            (rType == BaseTool.ToolType.ColorPicker) ||
            (rType == BaseTool.ToolType.BrushPicker) ||
            (rType == BaseTool.ToolType.BrushAndColorPicker) ||
            (rType == BaseTool.ToolType.Selection) ||
            (rType == BaseTool.ToolType.EraserTool);
  }

  public void CheckForToolSelection() {
    //don't allow us to switch tools if we're in SteamVR mode
    if (!m_FreePaintMode) {
      int iPrevIndex = m_ActiveToolIndex;
      m_ToolSelectionAggregateValue += InputManager.m_Instance.GetToolSelection();
      if (m_ToolSelectionAggregateValue > m_ToolSelectionThreshold) {
        do {
          ++m_ActiveToolIndex;
          m_ActiveToolIndex %= m_Tools.Length;
          InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Wand, 0.1f);
        }
        while (!IsScrollableTool(m_Tools[m_ActiveToolIndex].m_Type));

        while (m_ToolSelectionAggregateValue > m_ToolSelectionThreshold) {
          m_ToolSelectionAggregateValue -= m_ToolSelectionThreshold;
        }
      } else if (m_ToolSelectionAggregateValue < -m_ToolSelectionThreshold) {
        do {
          --m_ActiveToolIndex;
          while (m_ActiveToolIndex < 0) {
            m_ActiveToolIndex += m_Tools.Length;
          }
          InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Wand, 0.1f);
        }
        while (!IsScrollableTool(m_Tools[m_ActiveToolIndex].m_Type));

        while (m_ToolSelectionAggregateValue < -m_ToolSelectionThreshold) {
          m_ToolSelectionAggregateValue += m_ToolSelectionThreshold;
        }
      }

      if (iPrevIndex != m_ActiveToolIndex) {
        m_Tools[iPrevIndex].EnableTool(false);
        m_Tools[m_ActiveToolIndex].EnableTool(true);
      }
    }
  }

  public void NextTool() {
    int iPrevIndex = m_ActiveToolIndex;
    do {
      ++m_ActiveToolIndex;
      m_ActiveToolIndex %= m_Tools.Length;
      InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Wand, 0.1f);
    }
    while (!IsScrollableTool(m_Tools[m_ActiveToolIndex].m_Type));

    if (iPrevIndex != m_ActiveToolIndex) {
      m_Tools[iPrevIndex].EnableTool(false);
      m_Tools[m_ActiveToolIndex].EnableTool(true);
    }
  }

  public void PreviousTool() {
    int iPrevIndex = m_ActiveToolIndex;
    do {
      --m_ActiveToolIndex;
      while (m_ActiveToolIndex < 0) {
        m_ActiveToolIndex += m_Tools.Length;
      }
      InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Wand, 0.1f);
    }
    while (!IsScrollableTool(m_Tools[m_ActiveToolIndex].m_Type));

    if (iPrevIndex != m_ActiveToolIndex) {
      m_Tools[iPrevIndex].EnableTool(false);
      m_Tools[m_ActiveToolIndex].EnableTool(true);
    }
  }

  public void UpdateCurrentTool() {
    UnityEngine.Profiling.Profiler.BeginSample("SketchSurfacePanel.UpdateCurrentTool");
    m_UpdatedToolThisFrame = true;
    ActiveTool.UpdateTool();
    if (ActiveTool.ExitRequested()) {
      // Requesting a tool exit puts us back at our default tool.
      EnableDefaultTool();
      ActiveTool.EatInput();

      // If we're currently loading, hide our default tool.
      if (App.Instance.IsLoading()) {
        PointerManager.m_Instance.RequestPointerRendering(false);
      }
    }
    UnityEngine.Profiling.Profiler.EndSample();
  }

  // This gets called late in the frame, just before rendering;
  // it should do minimal work.
  public void OnNewPoses() {
    if (m_UpdatedToolThisFrame) {
      ActiveTool.LateUpdateTool();
    }
  }

  public bool DoesCurrentToolAllowWidgetManipulation() {
    return ActiveTool.AllowsWidgetManipulation();
  }

  public void UpdateToolSize(float fAdjustAmount) {
    ActiveTool.UpdateSize(fAdjustAmount);
  }

  public float ToolSize01() {
    return ActiveTool.GetSize01();
  }

  public void SetCurrentToolColor(Color rColor) {
    ActiveTool.SetColor(rColor);
  }

  public void UpdateCurrentToolExtraText(string sText) {
    ActiveTool.SetExtraText(sText);
  }

  public void UpdateCurrentToolProgress(float fProgress) {
    ActiveTool.SetToolProgress(fProgress);
  }

  public bool ShouldShowTransformGizmo() {
    return ActiveTool.m_ShowTransformGizmo;
  }

  public bool ShouldShowPointer() {
    return ActiveTool.ShouldShowPointer();
  }

  public void EatToolsInput() {
    ActiveTool.EatInput();
  }

  public void AllowDrawing(bool bAllow) {
    ActiveTool.AllowDrawing(bAllow);
  }

  public bool CanAdjustToolSize() {
    return ActiveTool.CanAdjustSize();
  }

  override public void AssignControllerMaterials(InputManager.ControllerName controller) {
    ActiveTool.AssignControllerMaterials(controller);
  }

  public float GetCurrentToolSizeRatio(
      InputManager.ControllerName controller, VrInput input) {
    return ActiveTool.GetSizeRatio(controller, input);
  }

  public BaseTool.ToolType GetCurrentToolType() {
    return ActiveTool.m_Type;
  }

  public void EnableRenderer(bool bEnable) {
    //disable all tools
    for (int i = 0; i < m_Tools.Length; ++i) {
      m_Tools[i].EnableRenderer(bEnable && (m_ActiveToolIndex == i));
    }
  }

}
}  // namespace TiltBrush
