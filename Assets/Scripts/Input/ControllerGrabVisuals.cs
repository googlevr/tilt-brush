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

public class ControllerGrabVisuals : MonoBehaviour {
  public enum VisualState {
    Off,
    WorldWandGrip,
    WorldBrushGrip,
    WorldDoubleGrip,
    WidgetWandGrip,
    WidgetBrushGrip,
  }

  [SerializeField] private Transform m_Line;
  [SerializeField] private Transform m_LineOutline;
  [SerializeField] private float m_LineHorizontalOffset = 0.75f;
  [SerializeField] private float m_LineOutlineWidth = 0.1f;
  [SerializeField] private float m_LineBaseWidth = 0.025f;
  [SerializeField] private float m_HintIntensity = 0.75f;
  [SerializeField] private float m_DrawInDuration = 0.3f;

  [SerializeField] private Transform m_AnimalRulerAnchor;
  [SerializeField] private Renderer m_AnimalRuler;

  // Configurables to tell us where the ruler sits in the texture, and how big it is.
  // U of the logscale==0 point; it's not exactly in the middle!
  [SerializeField] private float m_AnimalRulerUZeroPoint;
  // Amount of u per unit of log-scale user size
  [SerializeField] private float m_AnimalRulerUExtent;

  [SerializeField] private Vector2 m_AnimalRulerSquishRange;
  // Typically, ShrinkRange.y should == SquishRange.x
  [SerializeField] private Vector2 m_AnimalRulerShrinkRange;
  [SerializeField] private float m_AnimalRulerScaleSpeed = 8.0f;

  private float m_AnimalRulerTextureRatio;  // Aspect ratio, that is

  private Renderer m_LineRenderer;
  private Renderer m_LineOutlineRenderer;
  private Renderer[] m_AnimalRulerRenderers;
  private VisualState m_CurrentVisualState = VisualState.Off;
  private VisualState m_DesiredVisualState = VisualState.Off;

  private float m_Intensity = 1;

  private float m_LineDrawInTime = 0.0f;
  private float m_LineT;

  private bool m_AnimalRulerRequestVisible;

  private Transform m_HeldWidget;
  private bool m_WandInWidgetRange;
  private bool m_BrushInWidgetRange;

  public bool WandInWidgetRange {
    set {
      m_WandInWidgetRange = value;
      UpdateWandControllerGripState();
    }
  }
  public bool BrushInWidgetRange {
    set {
      m_BrushInWidgetRange = value;
      UpdateBrushControllerGripState();
    }
  }

  void Start() {
    m_LineRenderer = m_Line.GetComponent<Renderer>();
    m_LineOutlineRenderer = m_LineOutline.GetComponent<Renderer>();
    m_LineRenderer.enabled = false;
    m_LineOutlineRenderer.enabled = false;

    m_AnimalRulerAnchor.gameObject.SetActive(false);
    m_AnimalRulerRenderers = m_AnimalRulerAnchor.GetComponentsInChildren<Renderer>();
    SetAnimalRulerScale(0);
    m_AnimalRulerRequestVisible = false;

    m_WandInWidgetRange = false;
    m_BrushInWidgetRange = false;

    Texture animals = m_AnimalRuler.material.mainTexture;
    m_AnimalRulerTextureRatio = ((float)animals.height) / animals.width;
  }

  public void SetDesiredVisualState(VisualState state) {
    m_DesiredVisualState = state;
  }

  public void SetHeldWidget(Transform xfWidget) {
    m_HeldWidget = xfWidget;
  }

  void Update() {
    if (m_CurrentVisualState != m_DesiredVisualState) {
      SwitchState();
    }

    switch (m_CurrentVisualState) {
    case VisualState.Off: break;
    case VisualState.WorldWandGrip:
      // If the brush (other) controller dropped out, request exit out of this state.
      if (!InputManager.Brush.IsTrackedObjectValid) {
        SetDesiredVisualState(VisualState.Off);
      } else {
        UpdateLineT();
        UpdateVisuals();
      }
      break;
    case VisualState.WorldBrushGrip:
      // If the wand (other) controller dropped out, request exit out of this state.
      if (!InputManager.Wand.IsTrackedObjectValid) {
        SetDesiredVisualState(VisualState.Off);
      } else {
        UpdateLineT();
        UpdateVisuals();
      }
      break;
    case VisualState.WidgetWandGrip:
    case VisualState.WidgetBrushGrip:
      UpdateLineT();
      UpdateVisuals();
      break;
    case VisualState.WorldDoubleGrip:
      UpdateVisuals();
      break;
    }
  }

  void UpdateLineT() {
    if (m_LineT < 1.0f) {
      m_LineT = Mathf.SmoothStep(0.0f, 1.0f,
          Mathf.Clamp(m_LineDrawInTime / m_DrawInDuration, 0.0f, 1.0f));
      m_LineDrawInTime += Time.deltaTime;
    }
  }

  Vector3 GetControllerAttachPos(InputManager.ControllerName controllerName) {
    return InputManager.Controllers[(int)controllerName].Geometry.GripAttachPoint.position;
  }

  void UpdateVisuals() {
    Vector3 brush_pos = GetControllerAttachPos(InputManager.ControllerName.Brush);
    Vector3 wand_pos = GetControllerAttachPos(InputManager.ControllerName.Wand);

    // If we're holding a widget, make our line length zero so it doesn't show.
    if (m_HeldWidget != null) {
      Debug.Assert(m_CurrentVisualState == VisualState.WidgetBrushGrip ||
          m_CurrentVisualState == VisualState.WidgetWandGrip);
      wand_pos = brush_pos;
    } else {
      if (m_CurrentVisualState == VisualState.WorldBrushGrip) {
        wand_pos = Vector3.Lerp(brush_pos, wand_pos, m_LineT);
      } else if (m_CurrentVisualState == VisualState.WorldWandGrip) {
        brush_pos = Vector3.Lerp(wand_pos, brush_pos, m_LineT);
      }
    }

    float line_length = (brush_pos - wand_pos).magnitude - m_LineHorizontalOffset;
    if (line_length > 0.0f) {
      Vector3 brush_to_wand = (brush_pos - wand_pos).normalized;
      Vector3 centerpoint = brush_pos - (brush_pos - wand_pos) / 2.0f;
      transform.position = centerpoint;
      m_Line.position = centerpoint;
      m_Line.up = brush_to_wand;
      m_LineOutline.position = centerpoint;
      m_LineOutline.up = brush_to_wand;
      Vector3 temp = Vector3.one * m_LineBaseWidth * m_Intensity;
      temp.y = line_length / 2.0f;
      m_Line.localScale = temp;
      temp.y = line_length / 2.0f + m_LineOutlineWidth * Mathf.Min(1.0f, 1.0f / line_length) * m_Intensity;
      temp.x += m_LineOutlineWidth;
      temp.z += m_LineOutlineWidth;
      m_LineOutline.localScale = temp;
    } else {
      // Short term disable of line
      m_Line.localScale = Vector3.zero;
      m_LineOutline.localScale = Vector3.zero;
      m_AnimalRulerAnchor.gameObject.SetActive(false);
    }

    m_LineRenderer.material.SetColor("_Color",
        SketchControlsScript.m_Instance.m_GrabHighlightActiveColor);
    if (m_AnimalRulerRequestVisible) {
      UpdateAnimalRuler();
    }
  }

  void SwitchState() {
    InputManager.ControllerName wand = InputManager.ControllerName.Wand;
    InputManager.ControllerName brush = InputManager.ControllerName.Brush;
    BaseControllerBehavior wandBehavior = InputManager.m_Instance.GetControllerBehavior(wand);
    BaseControllerBehavior brushBehavior = InputManager.m_Instance.GetControllerBehavior(brush);

    // Short circuit out of certain states if all the pieces don't fit.
    if (m_DesiredVisualState == VisualState.WorldBrushGrip) {
      if (!InputManager.Wand.IsTrackedObjectValid) {
        m_DesiredVisualState = VisualState.Off;
      }
    } else if (m_DesiredVisualState == VisualState.WorldWandGrip) {
      if (!InputManager.Brush.IsTrackedObjectValid) {
        m_DesiredVisualState = VisualState.Off;
      }
    }

    switch (m_DesiredVisualState) {
      case VisualState.Off:
        m_LineDrawInTime = 0.0f;
        m_LineT = 0.0f;
        m_LineRenderer.enabled = false;
        m_LineOutlineRenderer.enabled = false;
        UpdateWandControllerGripState();
        UpdateBrushControllerGripState();
        m_AnimalRulerAnchor.gameObject.SetActive(false);
        SetAnimalRulerScale(0);
        m_AnimalRulerRequestVisible = false;
        break;
      case VisualState.WorldWandGrip:
      case VisualState.WidgetWandGrip:
        m_LineRenderer.material.SetFloat("_Intensity", m_HintIntensity);
        m_Intensity = m_HintIntensity;
        m_LineRenderer.enabled = true;
        m_LineOutlineRenderer.enabled = true;
        wandBehavior.EnableTransformVisuals(true, 0.5f);
        wandBehavior.SetGripState(BaseControllerBehavior.GripState.Gripped);
        UpdateBrushControllerGripState();
        m_AnimalRulerAnchor.gameObject.SetActive(false);
        m_AnimalRulerRequestVisible = false;
        break;
      case VisualState.WorldBrushGrip:
      case VisualState.WidgetBrushGrip:
        m_LineRenderer.material.SetFloat("_Intensity", m_HintIntensity);
        m_Intensity = m_HintIntensity;
        m_LineRenderer.enabled = true;
        m_LineOutlineRenderer.enabled = true;
        brushBehavior.EnableTransformVisuals(true, 0.5f);
        brushBehavior.SetGripState(BaseControllerBehavior.GripState.Gripped);
        UpdateWandControllerGripState();
        m_AnimalRulerAnchor.gameObject.SetActive(false);
        m_AnimalRulerRequestVisible = false;
        break;
      case VisualState.WorldDoubleGrip:
        m_LineT = 1.0f;
        m_Intensity = 1.0f;
        m_LineRenderer.enabled = true;
        m_LineOutlineRenderer.enabled = true;
        m_LineRenderer.material.SetFloat("_Intensity", 1.0f);
        wandBehavior.EnableTransformVisuals(true, 1.0f);
        wandBehavior.SetGripState(BaseControllerBehavior.GripState.Gripped);
        brushBehavior.EnableTransformVisuals(true, 1.0f);
        brushBehavior.SetGripState(BaseControllerBehavior.GripState.Gripped);
        m_AnimalRulerAnchor.gameObject.SetActive(true);
        m_AnimalRulerRequestVisible = true;
        break;
      }

    m_CurrentVisualState = m_DesiredVisualState;
  }

  void UpdateWandControllerGripState() {
    BaseControllerBehavior behavior =
        InputManager.m_Instance.GetControllerBehavior(InputManager.ControllerName.Wand);
    bool gripReady = m_WandInWidgetRange || (m_DesiredVisualState == VisualState.WorldBrushGrip);
    float visualAmount = m_DesiredVisualState == VisualState.WidgetWandGrip ? 1.0f : 0.5f;
    behavior.EnableTransformVisuals(m_WandInWidgetRange, visualAmount);
    behavior.SetGripState(gripReady ?
        BaseControllerBehavior.GripState.ReadyToGrip :
        BaseControllerBehavior.GripState.Standard);
  }

  void UpdateBrushControllerGripState() {
    BaseControllerBehavior behavior =
        InputManager.m_Instance.GetControllerBehavior(InputManager.ControllerName.Brush);
    bool gripReady = m_BrushInWidgetRange || (m_DesiredVisualState == VisualState.WorldWandGrip);
    float visualAmount = m_DesiredVisualState == VisualState.WidgetBrushGrip ? 1.0f : 0.5f;
    behavior.EnableTransformVisuals(m_BrushInWidgetRange, visualAmount);
    behavior.SetGripState(gripReady ?
        BaseControllerBehavior.GripState.ReadyToGrip :
        BaseControllerBehavior.GripState.Standard);
  }

  void UpdateAnimalRuler() {
    Transform headXf = ViewpointScript.Head;
    Vector3 vBrushPos = GetControllerAttachPos(InputManager.ControllerName.Brush);
    Vector3 vWandPos = GetControllerAttachPos(InputManager.ControllerName.Wand);
    Vector3 vHeadToBrush = vBrushPos - headXf.position;
    Vector3 vHeadToWand = vWandPos - headXf.position;
    Vector3 vHeadToBrushTransformed = headXf.InverseTransformDirection(vHeadToBrush);
    Vector3 vHeadToWandTransformed = headXf.InverseTransformDirection(vHeadToWand);
    Vector3 vControllerSpan = Vector3.zero;
    if (vHeadToBrushTransformed.x < vHeadToWandTransformed.x) {
      vControllerSpan = vWandPos - vBrushPos;
    } else {
      vControllerSpan = vBrushPos - vWandPos;
    }

    // If the controllers are too close together, don't show the ruler.
    float fControllerSpanMag = vControllerSpan.magnitude - m_LineHorizontalOffset;
    if (fControllerSpanMag < m_AnimalRulerShrinkRange.x) {
      m_AnimalRulerAnchor.gameObject.SetActive(false);
    } else {
      m_AnimalRulerAnchor.gameObject.SetActive(m_AnimalRulerRequestVisible);
      m_AnimalRulerAnchor.rotation = Quaternion.LookRotation(-vControllerSpan.normalized,
          ViewpointScript.Head.up);

      // Squish quad and update UVs to reflect.
      float fQuadWidth = Mathf.Clamp(fControllerSpanMag, m_AnimalRulerSquishRange.x,
          m_AnimalRulerSquishRange.y);
      float quadWidthU = fQuadWidth * m_AnimalRulerTextureRatio;
      m_AnimalRuler.transform.localScale = new Vector3(fQuadWidth, 1.0f, 1.0f);
      m_AnimalRuler.material.SetTextureScale("_MainTex", new Vector2(quadWidthU, 1.0f));

      // Scene size is 1/(user size); thus the negative.
      float logUserSize = -Mathf.Log(App.Scene.Pose.scale, 10.0f);  // currently -1 to 1
      float quadLeftU = m_AnimalRulerUZeroPoint + (logUserSize * m_AnimalRulerUExtent)
          - (quadWidthU * 0.5f);
      m_AnimalRuler.material.SetTextureOffset("_MainTex", new Vector2(quadLeftU, 0));

      // Chase scale.
      float rulerScale = (fControllerSpanMag > m_AnimalRulerShrinkRange.y) ? 1.0f :
          ((fControllerSpanMag - m_AnimalRulerShrinkRange.x) /
           (m_AnimalRulerShrinkRange.y - m_AnimalRulerShrinkRange.x));

      float fCurrentScale = m_AnimalRulerAnchor.localScale.x;
      float fScaleDiff = rulerScale - fCurrentScale;

      // Protect against scaling every frame.
      if (Mathf.Abs(fScaleDiff) > 0.0f) {
        float fScaleStep = m_AnimalRulerScaleSpeed * Time.deltaTime;
        if (fCurrentScale < rulerScale) {
          fCurrentScale = Mathf.Min(fCurrentScale + fScaleStep, rulerScale);
        } else if (fCurrentScale > rulerScale) {
          fCurrentScale = Mathf.Max(fCurrentScale - fScaleStep, rulerScale);
        }
        SetAnimalRulerScale(fCurrentScale);
      }
    }

    // Update colors
    for (int i=0; i< m_AnimalRulerRenderers.Length; i++) {
      m_AnimalRulerRenderers[i].material.SetColor(
          "_Color", SketchControlsScript.m_Instance.m_GrabHighlightActiveColor);
    }
  }

  void SetAnimalRulerScale(float fScale) {
    m_AnimalRulerAnchor.localScale = Vector3.one * fScale;
  }
}
}  // namespace TiltBrush
