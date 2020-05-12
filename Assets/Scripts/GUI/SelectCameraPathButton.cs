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
using UnityEngine;
using TMPro;

namespace TiltBrush {

public class SelectCameraPathButton : OptionButton {
  // This value is for the relative position in the UI.  It does not map directly
  // to the index of the path.
  [SerializeField] protected int m_PathNumber;
  [SerializeField] protected GameObject m_AddPathMesh;
  [SerializeField] protected TextMeshPro m_PathNumberText;

  private CameraPathWidget m_PathWidget;
  private int m_NumActivePaths;

  override protected void Awake() {
    base.Awake();

    m_PathWidget = WidgetManager.m_Instance.GetNthActiveCameraPath(m_PathNumber);

    // We need to set m_CommandParam so we're properly highlighted in
    // SketchControlsScript.IsCommandActive.
    if (m_PathWidget != null) {
      int? index = WidgetManager.m_Instance.GetIndexOfCameraPath(m_PathWidget);
      if (index == null) {
        throw new ArgumentException("SelectCameraPathButton m_PathWidget index invalid.");
      }
      m_CommandParam = index.Value;
    } else {
      m_CommandParam = -1;
    }

    // Count the number of active camera paths at the time we were created.
    // Note that this caching method is ok in this instance because these buttons are transient.
    var datas = WidgetManager.m_Instance.CameraPathWidgets;
    m_NumActivePaths = 0;
    foreach (TypedWidgetData<CameraPathWidget> data in datas) {
      ++m_NumActivePaths;
    }
  }

  override public void UpdateVisuals() {
    base.UpdateVisuals();

    // Show the path icon for active path, the + icon for 'add a path', and hide
    // the button if we're beyond the 'add a path' index.
    GetComponent<Renderer>().enabled = m_PathWidget != null && m_PathWidget.gameObject.activeSelf;
    m_AddPathMesh.SetActive(m_NumActivePaths == m_PathNumber);
    gameObject.SetActive(m_NumActivePaths >= m_PathNumber);
    m_PathNumberText.gameObject.SetActive(m_PathNumber < m_NumActivePaths);
  }

  override protected void OnButtonPressed() {
    // Create a new path if we pressed the + icon.
    if (m_NumActivePaths == m_PathNumber) {
      m_PathWidget = WidgetManager.m_Instance.CreatePathWidget();
      SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.CameraPathTool);
      App.Switchboard.TriggerCameraPathModeChanged(CameraPathTool.Mode.AddPositionKnot);
    }

    WidgetManager.m_Instance.CameraPathsVisible = true;
    WidgetManager.m_Instance.SetCurrentCameraPath(m_PathWidget);
    SketchControlsScript.m_Instance.EatGazeObjectInput();
  }

  override public void HasFocus(RaycastHit rHitInfo) {
    if (m_PathWidget != null && WidgetManager.m_Instance.CameraPathsVisible) {
      m_PathWidget.HighlightEntirePath();
    }
  }
}

} // namespace TiltBrush