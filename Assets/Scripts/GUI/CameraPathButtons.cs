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
using TMPro;

namespace TiltBrush {

public class CameraPathButtons : UIComponent {
  [SerializeField] private GameObject m_PathObjects;
  [SerializeField] private GameObject m_NoPathObjects;
  [SerializeField] private TextMeshPro m_NoPathText;
  [SerializeField] private string m_FirstPathMessage;
  [SerializeField] private string m_NonFirstPathMessage;
  [SerializeField] private OptionButton m_RecordButton;

  private UIComponentManager m_UIComponentManager;

  override protected void Awake() {
    base.Awake();
    m_UIComponentManager = GetComponent<UIComponentManager>();
    App.Switchboard.CameraPathCreated += RefreshButtonsVisibility;
    App.Switchboard.CameraPathDeleted += RefreshButtonsVisibility;
    App.Switchboard.AllWidgetsDestroyed += RefreshButtonsVisibility;
    App.Switchboard.CameraPathKnotChanged += RefreshButtonsVisibility;
    App.Switchboard.CurrentCameraPathChanged += RefreshButtonsVisibility;
    App.Switchboard.ToolChanged += RefreshButtonsVisibility;
    App.Switchboard.VideoRecordingStopped += RefreshButtonsVisibility;
  }

  override protected void Start() {
    RefreshButtonsVisibility();
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    App.Switchboard.CameraPathCreated -= RefreshButtonsVisibility;
    App.Switchboard.CameraPathDeleted -= RefreshButtonsVisibility;
    App.Switchboard.AllWidgetsDestroyed -= RefreshButtonsVisibility;
    App.Switchboard.CameraPathKnotChanged -= RefreshButtonsVisibility;
    App.Switchboard.CurrentCameraPathChanged -= RefreshButtonsVisibility;
    App.Switchboard.ToolChanged -= RefreshButtonsVisibility;
    App.Switchboard.VideoRecordingStopped -= RefreshButtonsVisibility;
  }

  override public void SetColor(Color color) {
    base.SetColor(color);
    m_UIComponentManager.SetColor(color);
    m_NoPathText.color = color;
  }

  override public void UpdateVisuals() {
    base.UpdateVisuals();
    m_UIComponentManager.UpdateVisuals();
  }

  override public bool UpdateStateWithInput(bool inputValid, Ray inputRay,
        GameObject parentActiveObject, Collider parentCollider) {
    if (base.UpdateStateWithInput(inputValid, inputRay, parentActiveObject, parentCollider)) {
      if (parentActiveObject == null || parentActiveObject == gameObject) {
        if (BasePanel.DoesRayHitCollider(inputRay, GetCollider())) {
          m_UIComponentManager.UpdateUIComponents(inputRay, inputValid, parentCollider);
          return true;
        }
      }
    }
    return false;
  }

  override public void ManagerLostFocus() {
    base.ManagerLostFocus();
    m_UIComponentManager.ManagerLostFocus();
  }

  override public void ResetState() {
    base.ResetState();
    m_UIComponentManager.Deactivate();
  }

  void RefreshButtonsVisibility() {
    // Path Objects should be shown if our current path is a "full path", meaning it has
    // 2 or more position knots.
    // If the current path is null, but we have paths, show Path Objects.  This case is
    // most commonly after a sketch load.
    // If we don't have paths, or our current path isn't full, don't show Path Objects.
    bool anyPathsActive = WidgetManager.m_Instance.AnyCameraPathWidgetsActive;
    var currentCameraPath = WidgetManager.m_Instance.GetCurrentCameraPath();
    bool currentCameraPathIsAFullPath = (currentCameraPath != null) &&
        (currentCameraPath.WidgetScript.Path.NumPositionKnots > 1);
    bool showPathButtons = anyPathsActive &&
        (currentCameraPath == null || currentCameraPathIsAFullPath);
    m_PathObjects.SetActive(showPathButtons);
    m_NoPathObjects.SetActive(!showPathButtons);

    // No Path Text should reflect whether we're creating our first path, or building out
    // another path.
    m_NoPathText.text = anyPathsActive ||
        SketchSurfacePanel.m_Instance.GetCurrentToolType() == BaseTool.ToolType.CameraPathTool ?
        m_NonFirstPathMessage : m_FirstPathMessage;

    m_RecordButton.UpdateVisuals();
  }
}
}  // namespace TiltBrush
