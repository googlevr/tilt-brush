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

namespace TiltBrush {

public class ColorPicker : UIComponent {
  public event Action<Color> ColorPicked;
  public event Action ColorFinalized;

  [SerializeField] private ColorPickerSelector m_ColorPickerSelector;
  [SerializeField] private ColorPickerSlider m_ColorPickerSlider;
  [SerializeField] private Renderer m_ColorPickerSelectorBorderCube;
  [SerializeField] private Renderer m_ColorPickerSelectorBorderCylinder;
  [SerializeField] private GameObject m_CircleBack;

  private ColorController m_ColorController;
  private GameObject m_ActiveInputObject;

  // TODO : Temporary for refactors.
  public ColorController Controller { get { return m_ColorController; } }

  override protected void Awake() {
    base.Awake();
    CustomColorPaletteStorage.m_Instance.ModeChanged += OnModeChanged;

    // Default to the brush color controller.
    m_ColorController = App.BrushColor;

    // Look for a different color controller on our manager.
    for (var manager = m_Manager; manager != null; manager = manager.ParentManager) {
      ColorController colorController = manager.GetComponent<ColorController>();
      if (colorController != null) {
        m_ColorController = colorController;
        break;
      }
    }
    m_ColorController.CurrentColorSet += OnCurrentColorSet;
  }

  override protected void Start() {
    base.Start();
    // Poke our mode to refresh everything after it's all initialized.
    CustomColorPaletteStorage.m_Instance.Mode = CustomColorPaletteStorage.m_Instance.Mode;
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    CustomColorPaletteStorage.m_Instance.ModeChanged -= OnModeChanged;
    m_ColorController.CurrentColorSet -= OnCurrentColorSet;
  }

  override public void SetColor(Color color) {
    base.SetColor(color);
    m_ColorPickerSelector.SetTintColor(color);
    m_ColorPickerSlider.SetTintColor(color);
    if (m_ColorPickerSelectorBorderCube != null) {
      m_ColorPickerSelectorBorderCube.material.SetColor("_Color", color);
    }
    if (m_ColorPickerSelectorBorderCylinder != null) {
      m_ColorPickerSelectorBorderCylinder.material.SetColor("_Color", color);
    }
  }

  override public bool UpdateStateWithInput(bool inputValid, Ray inputRay,
        GameObject parentActiveObject, Collider parentCollider) {
    if (base.UpdateStateWithInput(inputValid, inputRay, parentActiveObject, parentCollider)) {
      UpdateColorSelectorAndSlider(inputValid, inputRay, parentCollider);
      return true;
    }
    return false;
  }

  void UpdateColorSelectorAndSlider(bool inputValid, Ray inputRay, Collider parentCollider) {
    // Reset our input object if we're not holding the trigger.
    if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate)) {
      ResetActiveInputObject();
    }

    // Color limits if we're tied to the brush color.
    float luminanceMin = 0.0f;
    float saturationMax = 1.0f;
    BrushColorController brushController = m_ColorController as BrushColorController;
    if (brushController != null) {
      luminanceMin = brushController.BrushLuminanceMin;
      saturationMax = brushController.BrushSaturationMax;
    }

    // Cache mode cause we use it a bunch.
    ColorPickerMode mode = ColorPickerUtils.GetActiveMode(m_ColorController.IsHdr);

    // Check for collision against our color slider first.
    RaycastHit hitInfo;
    if (m_ActiveInputObject == null || m_ActiveInputObject == m_ColorPickerSlider.gameObject) {
      bool validCollision = BasePanel.DoesRayHitCollider(inputRay,
          m_ColorPickerSlider.GetCollider(), out hitInfo);

      // TODO : ColorPickerSlider should be a UIComponent that handles this stuff
      // on its own.
      // If we're not colliding with the slider, but we were before, get our collision with
      // our parent collider.
      if (!validCollision && m_ActiveInputObject == m_ColorPickerSlider.gameObject) {
        validCollision = BasePanel.DoesRayHitCollider(inputRay, parentCollider, out hitInfo);
      }

      if (validCollision) {
        // Over slider, check for mouse down.
        if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate)) {
          float value = ColorPickerUtils.ApplySliderConstraint(mode,
              m_ColorPickerSlider.GetValueFromHit(hitInfo),
              luminanceMin, saturationMax);
          UpdateSelectorSlider(value);
          UpdateSliderPosition();
          Color newColor;
          if (ColorPickerUtils.RawValueToColor(mode,
              m_ColorPickerSelector.RawValue, out newColor)) {
            m_ColorController.SetCurrentColorSilently(newColor);
            TriggerColorPicked(newColor);
          } else {
            // Indicates some logic fault: the user isn't modifying the color plane,
            // so why is the color plane's value outside the valid range?
            Debug.LogErrorFormat("Unexpected bad RawValue. mode:{0} val:{1}", mode,
                m_ColorPickerSelector.RawValue);
          }

          SketchSurfacePanel.m_Instance.VerifyValidToolWithColorUpdate();
          m_ActiveInputObject = m_ColorPickerSlider.gameObject;
        }
      }
    }

    if (m_ActiveInputObject == null || m_ActiveInputObject == m_ColorPickerSelector.gameObject) {
      if (BasePanel.DoesRayHitCollider(inputRay, m_ColorPickerSelector.GetCollider(),
          out hitInfo)) {
        // Over color picker, check for input.
        if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate)) {
          Vector3 value = ColorPickerUtils.ApplyPlanarConstraint(
              m_ColorPickerSelector.GetValueFromHit(hitInfo),
              mode, luminanceMin, saturationMax);
          Color color;
          if (ColorPickerUtils.RawValueToColor(mode, value, out color)) {
            m_ColorPickerSelector.RawValue = value;
            m_ColorController.SetCurrentColorSilently(color);
            TriggerColorPicked(color);
            m_ColorPickerSlider.OnColorChanged(mode, value);

            SketchSurfacePanel.m_Instance.VerifyValidToolWithColorUpdate();
            m_ActiveInputObject = m_ColorPickerSelector.gameObject;
          }
        }
      }
    }
  }

  override public bool CalculateReticleCollision(
      Ray castRay, ref Vector3 pos, ref Vector3 forward) {
    //see if our cast direction hits the selector
    RaycastHit selectorHitInfo;
    RaycastHit sliderHitInfo;

    bool selectorValid = BasePanel.DoesRayHitCollider(castRay,
        m_ColorPickerSelector.GetCollider(), out selectorHitInfo);
    bool sliderValid = BasePanel.DoesRayHitCollider(castRay,
        m_ColorPickerSlider.GetCollider(), out sliderHitInfo);

    if (selectorValid && sliderValid) {
      // Find the one that's closest and disable the other.
      if ((selectorHitInfo.point - castRay.origin).sqrMagnitude <
          (sliderHitInfo.point - castRay.origin).sqrMagnitude) {
        sliderValid = false;
      } else {
        selectorValid = false;
      }
    }

    // Custom transforms for colliding with an object.
    if (selectorValid) {
      pos = selectorHitInfo.point;
      forward = -m_ColorPickerSelector.transform.forward;
      return true;
    } else if (sliderValid) {
      pos = sliderHitInfo.point;
      forward = -m_ColorPickerSlider.transform.forward;
      return true;
    }

    return false;
  }

  override public void ResetState() {
    base.ResetState();
    ResetActiveInputObject();
  }

  void TriggerColorPicked(Color color) {
    if (ColorPicked != null) {
      ColorPicked(color);
    }
  }

  void ResetActiveInputObject() {
    if (ColorFinalized != null) {
      // If we were picking a color, but now we're not, send a finalize event.
      if (m_ActiveInputObject == m_ColorPickerSlider.gameObject ||
          m_ActiveInputObject == m_ColorPickerSelector.gameObject) {
        ColorFinalized();
      }
    }
    m_ActiveInputObject = null;
  }

  void UpdateSelectorSlider(float value) {
    Vector3 newValue = m_ColorPickerSelector.RawValue;
    newValue.z = value;
    m_ColorPickerSelector.RawValue = newValue;
  }

  void UpdateSliderPosition() {
    m_ColorPickerSlider.RawValue = m_ColorPickerSelector.RawValue.z;
  }

  void OnModeChanged() {
    ColorPickerMode mode = ColorPickerUtils.GetActiveMode(m_ColorController.IsHdr);
    ColorPickerInfo info = ColorPickerUtils.GetInfoForMode(mode);

    if (m_ColorPickerSelectorBorderCube != null && m_ColorPickerSelectorBorderCylinder != null) {
      m_ColorPickerSelectorBorderCube.enabled = false;
      m_ColorPickerSelectorBorderCylinder.enabled = false;
      if (info.cylindrical) {
        m_ColorPickerSelectorBorderCylinder.enabled = true;
      } else {
        m_ColorPickerSelectorBorderCube.enabled = true;
      }
    }

    if (m_CircleBack != null) {
      m_CircleBack.SetActive(info.cylindrical);
    }

    m_ColorPickerSelector.SetLocalMode(mode);
    m_ColorPickerSlider.SetLocalMode(mode);

    m_ColorController.CurrentColor = m_ColorController.CurrentColor;
  }

  void OnCurrentColorSet(ColorPickerMode mode, Vector3 rawColor) {
    m_ColorPickerSelector.RawValue = rawColor;
    m_ColorPickerSlider.RawValue = m_ColorPickerSelector.RawValue.z;
    m_ColorPickerSlider.OnColorChanged(mode, rawColor);
  }
}

} // namespace TiltBrush
