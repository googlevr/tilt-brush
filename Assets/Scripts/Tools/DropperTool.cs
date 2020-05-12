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

namespace TiltBrush {

public class DropperTool : BaseStrokeIntersectionTool {
  [SerializeField] private Transform m_DropperTransform;
  [SerializeField] private Renderer m_DropperConeRenderer;
  [SerializeField] private Renderer m_DropperRenderer;
  [SerializeField] private Transform m_DropperDescription;
  [SerializeField] private Transform m_DropperBrushDescription;
  [SerializeField] private TextMesh m_DropperBrushDescriptionText;
  [SerializeField] private Transform m_DropperColorDescription;
  [SerializeField] private Transform m_DropperColorDescriptionSwatch;
  [SerializeField] private float m_DropperBrushSelectRadius;
  private Renderer m_DropperColorDescriptionSwatchRenderer;
  private Renderer[] m_DescriptionRenderers;

  private bool m_ValidBrushFoundThisFrame;
  private bool m_SelectionValid;
  private Color m_SelectionColor;
  private BrushDescriptor m_SelectionBrush;
  private Stroke m_SelectionStroke;

  private enum State {
    Enter,
    Standard,
    Exit,
    Off
  }
  private State m_CurrentState;
  private float m_EnterAmount;
  [SerializeField] private float m_EnterSpeed = 16.0f;
  [SerializeField] private float m_ReferenceImageCollisionDepth;
  [SerializeField] private Transform m_OffsetTransform;
  private Vector3 m_OffsetTransformBaseScale;

  public void DisableRequestExit_HackForSceneSurgeon() { m_RequestExit = false; }

  override public void Init() {
    base.Init();

    m_DescriptionRenderers = m_DropperDescription.GetComponentsInChildren<Renderer>();

    m_DropperColorDescriptionSwatchRenderer =
      m_DropperColorDescriptionSwatch.GetComponent<Renderer>();

    m_OffsetTransformBaseScale = m_OffsetTransform.localScale;
    SetState(State.Off);
    m_EnterAmount = 0.0f;
    UpdateScale();
  }

  override public void HideTool(bool bHide) {
    base.HideTool(bHide);

    if (bHide) {
      SetState(State.Exit);
    }
    ResetDetection();
    m_DropperRenderer.enabled = !bHide;
    m_DropperConeRenderer.enabled = !bHide;
  }

  override public void EnableTool(bool bEnable) {
    base.EnableTool(bEnable);
    ResetDetection();
    m_SelectionValid = false;

    if (bEnable) {
      EatInput();
    } else {
      SetState(State.Off);
      m_EnterAmount = 0.0f;
      UpdateScale();
    }
    SnapIntersectionObjectToController();
  }

  void Update() {
    //update animations
    switch (m_CurrentState) {
    case State.Enter:
      m_EnterAmount += (m_EnterSpeed * Time.deltaTime);
      if (m_EnterAmount >= 1.0f) {
        m_EnterAmount = 1.0f;
        m_CurrentState = State.Standard;
      }
      UpdateScale();
      break;
    case State.Exit:
      m_EnterAmount -= (m_EnterSpeed * Time.deltaTime);
      if (m_EnterAmount <= 0.0f) {
        m_EnterAmount = 0.0f;
        for (int i = 0; i < m_DescriptionRenderers.Length; ++i) {
          m_DescriptionRenderers[i].enabled = false;
        }
        m_CurrentState = State.Off;
      }
      UpdateScale();
      break;
    }
  }

  void SetState(State rDesiredState) {
    switch (rDesiredState) {
    case State.Enter:
      for (int i = 0; i < m_DescriptionRenderers.Length; ++i) {
        m_DescriptionRenderers[i].enabled = true;
      }
      break;
    }
    m_CurrentState = rDesiredState;
  }

  override public void UpdateTool() {
    base.UpdateTool();

    //keep description locked to controller
    Transform attach = InputManager.Brush.Geometry.DropperDescAttachPoint;
    m_DropperDescription.transform.position = attach.position;
    m_DropperDescription.transform.rotation = attach.rotation;
    SnapIntersectionObjectToController();

    // Check for reference widget intersection first because it's cheap.
    Vector3 dropperCoords = Vector3.zero; // x and y valid from [-0.5, 0.5]
    ImageWidget image = WidgetManager.m_Instance.GetNearestImage(
      m_DropperTransform.position, m_ReferenceImageCollisionDepth, ref dropperCoords);
    if (image != null) {
      // Treat intersection with widget as if it were an intersection with a stroke.
      Color pixelColor;
      bool success = image.GetPixel(dropperCoords.x + 0.5f,
                                    dropperCoords.y + 0.5f,
                                    out pixelColor);
      if (success) {
        m_SelectionColor = pixelColor;
        m_DropperRenderer.material.color = m_SelectionColor;
        m_DropperColorDescriptionSwatchRenderer.material.color = m_SelectionColor;
        SetState(State.Enter);
        m_DropperColorDescription.gameObject.SetActive(true);
        m_DropperBrushDescription.gameObject.SetActive(false);
        ResetDetection();

        // Set the brush on activate.
        if (!m_EatInput && !m_ToolHidden &&
            InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate)) {
          PanelManager.m_Instance.SetCurrentColorOnAllColorPickers(m_SelectionColor);

          AudioManager.m_Instance.PlayDropperPickSound(m_DropperRenderer.transform.position);

          //select and get out
          m_RequestExit = true;
        }

        // Exit out early because we don't need to do actual stroke detection.
        return;
      }
    }

    //select brush and color
    if (!m_EatInput && m_SelectionValid && !m_ToolHidden &&
        InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate)) {
      //brush must be set *before* color so the pointer is primed to interpret the color correctly
      PointerManager.m_Instance.SetBrushForAllPointers(m_SelectionBrush);
      PointerManager.m_Instance.ExplicitlySetAllPointersBrushSize(
          m_SelectionStroke.SizeInRoomSpace);
      PointerManager.m_Instance.MarkAllBrushSizeUsed();

      PanelManager.m_Instance.SetCurrentColorOnAllColorPickers(m_SelectionColor);

      AudioManager.m_Instance.PlayDropperPickSound(m_DropperRenderer.transform.position);

      //select and get out
      m_RequestExit = true;

      if (m_SelectionStroke != null) {
        BrushController.m_Instance.TriggerStrokeSelected(m_SelectionStroke);
      }
      // Let go so it can be GC'd
      m_SelectionStroke = null;
    }

    //always default to resetting detection
    m_ResetDetection = true;
    m_ValidBrushFoundThisFrame = false;

    if (App.Config.m_UseBatchedBrushes) {
      UpdateBatchedBrushDetection(m_DropperTransform.position);
    } else {
      UpdateSolitaryBrushDetection(m_DropperTransform.position);
    }

    if (m_ResetDetection) {
      if (m_ValidBrushFoundThisFrame) {
        SetState(State.Enter);
        m_DropperBrushDescription.gameObject.SetActive(true);
        m_DropperColorDescription.gameObject.SetActive(false);
      } else {
        SetDescriptionInfo(Color.white, 1.0f, null);
        SetState(State.Exit);
      }
      ResetDetection();
    }
  }

  override protected void SnapIntersectionObjectToController() {
    Vector3 vPos = InputManager.Brush.Geometry.ToolAttachPoint.position +
          InputManager.Brush.Geometry.ToolAttachPoint.forward * m_PointerForwardOffset;
    m_DropperTransform.position = vPos;
    m_DropperTransform.rotation = InputManager.Brush.Geometry.ToolAttachPoint.rotation;
  }

  override protected void HandleIntersection(Stroke stroke) {
    var desc = BrushCatalog.m_Instance.GetBrush(stroke.m_BrushGuid);
    SetDescriptionInfo(stroke.m_Color, stroke.m_BrushSize, desc, stroke);
    m_ValidBrushFoundThisFrame = true;
  }

  void UpdateScale() {
    Vector3 vScale = m_OffsetTransformBaseScale;
    vScale.x *= m_EnterAmount;
    m_OffsetTransform.localScale = vScale;
  }

  override public float GetSize() {
    return m_DropperBrushSelectRadius;
  }

  // TODO: just pass the stroke and nothing else?
  void SetDescriptionInfo(Color rColor, float fSize, BrushDescriptor rBrush, Stroke stroke=null) {
    m_DropperRenderer.material.color = rColor;

    bool bSelectionValid = false;
    string sBrushDescription = "";
    if (rBrush != null) {
      sBrushDescription = rBrush.m_Description;
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
      if (Config.IsExperimental && !string.IsNullOrEmpty(rBrush.m_DescriptionExtra)) {
        sBrushDescription = string.Format(
            "{0} ({1})", rBrush.m_Description, rBrush.m_DescriptionExtra);
      }
#endif
      m_SelectionColor = rColor;
      m_SelectionBrush = rBrush;
      m_SelectionStroke = stroke;
      bSelectionValid = true;
    }

    if (m_SelectionValid != bSelectionValid) {
      AudioManager.m_Instance.PlayDropperIntersectionSound(m_DropperRenderer.transform.position);
    }

    m_SelectionValid = bSelectionValid;
    m_DropperBrushDescriptionText.text = sBrushDescription;
  }
}
}  // namespace TiltBrush
