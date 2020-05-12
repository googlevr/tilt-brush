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
  /// <summary>
  ///  The workhorse for updating any UIComponent children.
  ///  This component must be updated by another component to provide fine control over when
  ///  the objects are ticked.
  /// </summary>
  public class UIComponentManager : MonoBehaviour {
    private List<UIComponent> m_UIComponents;
    private GameObject m_ActiveInputObject;
    private UIComponent m_ActiveInputUIComponent;
    private BasePanel m_PopUpPanel;

    // This is an accessor for ideal future purposes.  Currently, we have portions of the UI
    // that derive from UIComponent and are managed by a UIComponentManager.  We also have portions
    // that are managed directly by panels and popups.  The ideal scenario is that everything is
    // managed by a UIComponentManager and the set method does not exist.
    // TODO
    public GameObject ActiveInputObject {
      get { return m_ActiveInputObject; }
      set { m_ActiveInputObject = value; }
    }
    public UIComponent ActiveInputUIComponent { get { return m_ActiveInputUIComponent; } }
    public UIComponentManager ParentManager {
      get { return transform.parent ?
          transform.parent.GetComponentInParent<UIComponentManager>() : null; }
    }

    void Awake() {
      m_UIComponents = new List<UIComponent>();
    }

    public void RegisterUIComponent(UIComponent comp) {
      // Assert this is a unique object.
      Debug.AssertFormat(!m_UIComponents.Contains(comp), "Duplicate in RegisterUIComponent");
      m_UIComponents.Add(comp);
    }

    public void UnregisterUIComponent(UIComponent comp) {
      bool wasRemoved = m_UIComponents.Remove(comp);
      Debug.Assert(wasRemoved, "Attempted to unregister a UIComponent that wasn't registered!");
    }

    public BasePanel GetPanelForPopUps() {
      // If m_PopUpPanel hasn't been set, recursively walk up and look for a panel.
      if (m_PopUpPanel == null) {
        UIComponentManager manager = this;
        while (manager != null) {
          m_PopUpPanel = GetPanelFromManager(manager);
          if (m_PopUpPanel == null) {
            manager = manager.ParentManager;
          } else {
            break;
          }
        }
      }

      if (m_PopUpPanel == null) {
        Debug.LogWarning("UIComponentManager requested panel for popups and nothing was found.");
      }
      return m_PopUpPanel;
    }

    BasePanel GetPanelFromManager(UIComponentManager manager) {
      BasePanel panel = manager.GetComponent<BasePanel>();
      if (panel == null) {
        PopUpWindow popup = manager.GetComponent<PopUpWindow>();
        if (popup != null) {
          return popup.GetParentPanel();
        }
      }
      return panel;
    }

    public void SetColor(Color col) {
      for (int i = 0; i < m_UIComponents.Count; ++i) {
        m_UIComponents[i].SetColor(col);
      }
    }

    public void UpdateVisuals() {
      for (int i = 0; i < m_UIComponents.Count; ++i) {
        m_UIComponents[i].UpdateVisuals();
      }
    }

    public void ManagerLostFocus() {
      for (int i = 0; i < m_UIComponents.Count; ++i) {
        m_UIComponents[i].ManagerLostFocus();
      }
    }

    public void Deactivate() {
      for (int i = 0; i < m_UIComponents.Count; ++i) {
        m_UIComponents[i].ResetState();
        m_UIComponents[i].ForceDescriptionDeactivate();
      }
    }

    public void GazeRatioChanged(float gazeRatio) {
      for (int i = 0; i < m_UIComponents.Count; ++i) {
        m_UIComponents[i].GazeRatioChanged(gazeRatio);
      }
    }

    public bool BrushPadAnimatesOnAnyHover() {
      for (int i = 0; i < m_UIComponents.Count; ++i) {
        if (m_UIComponents[i].BrushPadAnimatesOnHover()) {
          return true;
        }
      }
      return false;
    }

    public void AssignControllerMaterials(InputManager.ControllerName controller) {
      // There's an order of operations problem here.  In practice, I don't think it's an issue
      // right now, but this will need to be rethought if multiple UIComponents expect to assign
      // controller materials and play nicely.
      for (int i = 0; i < m_UIComponents.Count; ++i) {
        m_UIComponents[i].AssignControllerMaterials(controller);
      }
    }

    public float GetControllerPadShaderRatio(InputManager.ControllerName controller) {
      float shaderRatio = 0.0f;
      for (int i = 0; i < m_UIComponents.Count; ++i) {
        // I guess we'll just take the max for all UIComponents?
        shaderRatio =
            Mathf.Max(m_UIComponents[i].GetControllerPadShaderRatio(controller), shaderRatio);
      }
      return shaderRatio;
    }

    public void ResetInput() {
      m_ActiveInputObject = null;
      m_ActiveInputUIComponent = null;
    }

    public void UpdateUIComponents(Ray selectionRay, bool inputValid, Collider parentCollider) {
      // If we don't have controller input, reset our active input object.  Note that this is
      // different from the inputValid param.  inputValid takes game state in to account,
      // including eatInput flags.
      if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate)) {
        ResetInput();
      }

      for (int i = 0; i < m_UIComponents.Count; ++i) {
        UIComponent uiComponent = m_UIComponents[i];

        if (uiComponent.UpdateStateWithInput(inputValid, selectionRay, m_ActiveInputObject,
            parentCollider)) {
          m_ActiveInputObject = uiComponent.gameObject;
          m_ActiveInputUIComponent = uiComponent;
        } else {
          // Reset state of component because we're not messing with it.
          uiComponent.ResetState();
        }
      }

      // If we clicked, but didn't hit a UIComponent, set our active object to ourself.
      // This will prevent "off-clicking and dragging" to initiate a press.
      if (inputValid && m_ActiveInputObject == null) {
        m_ActiveInputObject = gameObject;
      }
    }

    public void SendMessageToComponents(UIComponent.ComponentMessage type, int param) {
      // TODO : Remove the direct calls to popups and panels.  They shouldn't
      // be required to store page state information.
      PopUpWindow parentPopUp = GetComponent<PopUpWindow>();
      if (parentPopUp) {
        switch (type) {
        case UIComponent.ComponentMessage.NextPage: parentPopUp.AdvancePage(1); break;
        case UIComponent.ComponentMessage.PrevPage: parentPopUp.AdvancePage(-1); break;
        case UIComponent.ComponentMessage.GotoPage: parentPopUp.GotoPage(param); break;
        case UIComponent.ComponentMessage.ClosePopup:
            parentPopUp.RequestClose(bForceClose: true); break;
        }
      } else {
        BasePanel parentPanel = GetComponent<BasePanel>();
        if (parentPanel) {
          switch (type) {
          case UIComponent.ComponentMessage.NextPage: parentPanel.AdvancePage(1); break;
          case UIComponent.ComponentMessage.PrevPage: parentPanel.AdvancePage(-1); break;
          case UIComponent.ComponentMessage.GotoPage: parentPanel.GotoPage(param); break;
          }
        } else {
          UIComponent uiComponent = GetComponent<UIComponent>();
          if (uiComponent) {
            uiComponent.ReceiveMessage(type, param);
          }
        }
      }

      for (int i = 0; i < m_UIComponents.Count; ++i) {
        m_UIComponents[i].ReceiveMessage(type, param);
      }
    }

    public bool RaycastAgainstCustomColliders(Ray ray, out RaycastHit hitInfo, float dist) {
      hitInfo = new RaycastHit();
      for (int i = 0; i < m_UIComponents.Count; ++i) {
        // There's an order of operations issue here.  In practice, UIComponents don't have
        // overlapping colliders, so it shouldn't matter.
        if (m_UIComponents[i].RaycastAgainstCustomCollider(ray, out hitInfo, dist)) {
          return true;
        }
      }
      return false;
    }

    public void CalculateReticleCollision(Ray castRay, ref Vector3 pos, ref Vector3 forward) {
      for (int i = 0; i < m_UIComponents.Count; ++i) {
        // There's an order of operations issue here.  In practice, UIComponents don't have
        // overlapping colliders, so it shouldn't matter.
        if (m_UIComponents[i].CalculateReticleCollision(castRay, ref pos, ref forward)) {
          return;
        }
      }
    }
  }
} // namespace TiltBrush
