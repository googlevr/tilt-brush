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
using UnityEngine.Serialization;

namespace TiltBrush {

// Basic controller behaviors shared between the Wand and the Brush, both of which directly derrive
// directly from BaseControllerBehavior.
public class BaseControllerBehavior : MonoBehaviour {

  public enum GripState {
    Standard,
    ReadyToGrip,
    Gripped
  }

  // -------------------------------------------------------------------------------------------- //
  // Inspector Data
  // -------------------------------------------------------------------------------------------- //
  [SerializeField] private InputManager.ControllerName m_ControllerName;
  [SerializeField] private ControllerGeometry m_ControllerGeometryPrefab;
  [FormerlySerializedAs("m_Offset")] [SerializeField] private Vector3 m_GeometryOffset;
  [FormerlySerializedAs("m_Rotation")] [SerializeField] private Quaternion m_GeometryRotation
      = Quaternion.identity;

  // -------------------------------------------------------------------------------------------- //
  // Private Fields
  // -------------------------------------------------------------------------------------------- //
  private Color m_Tint;
  private float m_BaseIntensity;
  private float m_GlowIntensity;

  private GripState m_CurrentGripState;
  private ControllerGeometry m_ControllerGeometry;

  // -------------------------------------------------------------------------------------------- //
  // Public Properties
  // -------------------------------------------------------------------------------------------- //
  public InputManager.ControllerName ControllerName {
    get { return m_ControllerName; }
  }

  private GameObject TransformVisuals {
    get { return ControllerGeometry.TransformVisualsRenderer.gameObject; }
  }

  public ControllerGeometry ControllerGeometry {
    get {
      if (m_ControllerGeometry == null) {
        InstantiateControllerGeometryFromPrefab(null);
      }
      return m_ControllerGeometry;
    }
  }

  /// Returns null if the ControllerName is invalid, or the requested controller does not exist.
  public ControllerInfo ControllerInfo {
    get {
      int name = (int)ControllerName;
      var controllers = InputManager.Controllers;
      // This handles the ControllerName.None case, too.
      if (name >= 0 && name < controllers.Length) {
        return controllers[name];
      } else {
        return null;
      }
    }
  }

  // Override the controller geometry with an instance of the passed in prefab unless we're passed
  // null. In that case, use the default controller geometry prefab.
  public void InstantiateControllerGeometryFromPrefab(ControllerGeometry prefab) {
    bool changedController = false;
    if (m_ControllerGeometry != null) {
      Destroy(m_ControllerGeometry.gameObject);
      changedController = true;
    }

    if (prefab == null) {
      prefab = m_ControllerGeometryPrefab;
    }
    SetGeometry(Instantiate(prefab));

    if (changedController) {
      InputManager.ControllersHaveChanged();
    }
  }

  public Transform PointerAttachAnchor { get { return ControllerGeometry.PointerAttachAnchor; } }
  public Transform PointerAttachPoint { get { return ControllerGeometry.PointerAttachPoint; } }
  public Transform ToolAttachAnchor { get { return ControllerGeometry.ToolAttachAnchor; } }
  public Transform PinCushionSpawn { get { return ControllerGeometry.PinCushionSpawn; } }

  // -------------------------------------------------------------------------------------------- //
  // Unity Events
  // -------------------------------------------------------------------------------------------- //

  void Awake() {
    m_CurrentGripState = GripState.Standard;
  }

  void Update() {
    // This is a proxy for "tutorial mode".
    SketchControlsScript.m_Instance.AssignControllerMaterials(m_ControllerName);

    // Skip the tint and animation update if in intro sketch because
    // - nothing but the default has been assigned
    // - the user is not able to change the tint color
    // - we do not want to animate the buttons/pads
    if (!PanelManager.m_Instance.IntroSketchbookMode) {
      // Send a signal to the controller that the materials have been assigned.
      ControllerGeometry.OnMaterialsAssigned(GetTintColor());
    }

    if (ControllerGeometry.XRayVisuals) {
      float XRayHeight_ss = (App.Scene.Pose.translation.y
                          + App.Scene.Pose.scale * SceneSettings.m_Instance.ControllerXRayHeight);
      bool bControllerUnderground = transform.position.y < XRayHeight_ss;
      bool bHMDUnderground = ViewpointScript.Head.position.y < XRayHeight_ss;
      ControllerGeometry.XRayVisuals.SetActive(bControllerUnderground != bHMDUnderground);
    }

    if (ControllerGeometry.TriggerAnchor != null) {
      // This is hooked up for Wmr, Vive.
      // This is not hooked up for the Quest, Rift, Knuckles controller geometry;
      // they work using Animators and AnimateOculusTouchSteam.cs
      Vector2 range = m_ControllerGeometry.TriggerRotation;
      ControllerGeometry.TriggerAnchor.localRotation = Quaternion.AngleAxis(
          Mathf.Lerp(range.x, range.y, ControllerInfo.GetTriggerRatio()),
          Vector3.right);
    }

    //
    // If the transform visuals are active and the user is interacting with a widget, add the
    // transform visuals to the highlight queue. Eventually, we may:
    //
    // (a) only have the post process higlight in which case these transform visuals will not need
    //     a renderer/material or
    // (b) modify the highlight queue to a dynamic list that retains state across frames in which
    //     case this logic can be moved into EnableTransformVisuals().
    //
    if (TransformVisuals.activeSelf
        && SketchControlsScript.m_Instance.IsUserAbleToInteractWithAnyWidget()) {
      App.Instance.SelectionEffect.RegisterMesh(TransformVisuals.GetComponent<MeshFilter>());

      switch (ControllerGeometry.Style) {
      case ControllerStyle.OculusTouch:
      case ControllerStyle.Knuckles:
        App.Instance.SelectionEffect.RegisterMesh(
            ControllerGeometry.JoystickPad.GetComponent<MeshFilter>());
        break;
      case ControllerStyle.Vive:
        App.Instance.SelectionEffect.RegisterMesh(
            ControllerGeometry.PadMesh.GetComponent<MeshFilter>());
        break;
      case ControllerStyle.Wmr:
        // TODO What should be here?  Joystick or pad?
        break;
      }
    }

    OnUpdate();
  }

  // -------------------------------------------------------------------------------------------- //
  // Virtual API
  // -------------------------------------------------------------------------------------------- //

  virtual protected void OnUpdate() { }

  virtual public void ActivateHint(bool bActivate) { }

  // Displays the swap effect on the controller. This may be overridden in sublcasses
  // if specific controllers have different implementations.
  virtual public void DisplayControllerSwapAnimation() {
    if (!App.Instance.ShowControllers) { return; }
    var highlightEffectPrefab = ControllerGeometry.HighlightEffectPrefab;
    GameObject rEffect = Instantiate(highlightEffectPrefab,
                                     transform.position,
                                     transform.rotation) as GameObject;
    rEffect.transform.parent = m_ControllerGeometry.transform;
    rEffect.transform.localPosition = highlightEffectPrefab.transform.localPosition;
    rEffect.transform.localRotation = highlightEffectPrefab.transform.localRotation;
  }

  // -------------------------------------------------------------------------------------------- //
  // Public API
  // -------------------------------------------------------------------------------------------- //

  /// <summary>
  /// Used to notify the user to look at their controllers.
  /// </summary>
  /// <param name="buzzLength">Duration of haptics in seconds</param>
  /// <param name="numPulses">Number of haptic pulses</param>
  /// <param name="interval">Duration between haptic pulses in seconds</param>
  public void BuzzAndGlow(float buzzLength, int numPulses, float interval) {
    //jiggle controller
    if (buzzLength > 0) {
      InputManager.m_Instance.TriggerHapticsPulse(
          m_ControllerName, numPulses, interval, buzzLength);
    }

    //play glow effect
    var activateEffectPrefab = m_ControllerGeometry.ActivateEffectPrefab;
    if (activateEffectPrefab) {
      GameObject rGlow = Instantiate(activateEffectPrefab, transform.position, transform.rotation) as GameObject;
      rGlow.transform.parent = m_ControllerGeometry.transform;
      rGlow.transform.localPosition = activateEffectPrefab.transform.localPosition;
      rGlow.transform.localRotation = activateEffectPrefab.transform.localRotation;
    }
  }

  // Helper for SwapBehaviors. Swaps the poses of the two transforms.
  private static void SwapPoses(Transform a, Transform b) {
    // Since the parents are the same, we can take a shortcut and swap the local transform
    Debug.Assert(a.parent == b.parent);
    Vector3    tmpPosition = a.localPosition;
    Quaternion tmpRotation = a.localRotation;
    Vector3    tmpScale    = a.localScale;
    a.localPosition = b.localPosition;
    a.localRotation = b.localRotation;
    a.localScale    = b.localScale;
    b.localPosition = tmpPosition;
    b.localRotation = tmpRotation;
    b.localScale    = tmpScale;
  }

  // Helper for SwapGeometry.
  // Sets up pointers from this -> geom, and from geom -> this.
  private void SetGeometry(ControllerGeometry geom) {
    m_ControllerGeometry = geom;

    // The back-pointers is implicit; it's geometry.transform.parent.
    // worldPositionStays: false because we're about to overwrite it anyway
    m_ControllerGeometry.transform.SetParent(this.transform, worldPositionStays: false);
    Quaternion rot = m_GeometryRotation.IsInitialized() ? m_GeometryRotation : Quaternion.identity;
    Coords.AsLocal[m_ControllerGeometry.transform] = TrTransform.TRS(m_GeometryOffset, rot, 1);
    m_ControllerGeometry.OnBehaviorChanged();
  }

  /// Swaps the behaviors associated with the controller geometries.
  /// The geometries themselves do not move.
  public static void SwapBehaviors(BaseControllerBehavior a, BaseControllerBehavior b) {
    // Well, this is a bit roundabout, because the behavior is the parent of the geometry
    // rather than vice versa. The geometries swap positions twice: once when they swap parents,
    // and again when their new parents swap places.
    SwapPoses(a.transform, b.transform);
    // Force instantiation using ControllerGeometry accessor
    var tmp = a.ControllerGeometry;
    a.SetGeometry(b.ControllerGeometry);
    b.SetGeometry(tmp);
  }

  public void SetTouchLocatorActive(bool active) {
    if (ControllerGeometry.PadTouchLocator != null) {
      ControllerGeometry.PadTouchLocator.gameObject.SetActive(active);
    }
  }

  public void SetTouchLocatorPosition(Vector2 loc) {
    if (ControllerGeometry.PadTouchLocator != null) {
      // Ensure the locator doesn't go beyond the edges of the pad face.
      // This value assumes loc is normalized to the range [-1,1].
      Vector2 offset = new Vector2(loc.x * m_ControllerGeometry.TouchLocatorTranslateScale,
          loc.y * m_ControllerGeometry.TouchLocatorTranslateScale);
      if (offset.magnitude > m_ControllerGeometry.TouchLocatorTranslateClamp) {
        offset = offset.normalized * m_ControllerGeometry.TouchLocatorTranslateClamp;
      }

      Vector3 pos =
          new Vector3(offset.x, ControllerGeometry.PadTouchLocator.localPosition.y, offset.y);
      ControllerGeometry.PadTouchLocator.localPosition = pos;
    }
  }

  public void SetTint(Color rTintColor, float fBaseIntensity, float fGlowIntensity) {
    m_Tint = rTintColor;
    m_BaseIntensity = fBaseIntensity;
    m_GlowIntensity = fGlowIntensity;

    Color rTintedColor = GetTintColor();
    ControllerGeometry.MainMesh.material.SetColor("_EmissionColor", rTintedColor);
    ControllerGeometry.TriggerMesh.material.SetColor("_EmissionColor", rTintedColor);
    for (int i = 0; i < ControllerGeometry.OtherMeshes.Length; ++i) {
      ControllerGeometry.OtherMeshes[i].material.SetColor("_EmissionColor", rTintedColor);
    }
    ControllerGeometry.TransformVisualsRenderer.material.SetColor("_Color", rTintColor);

    if (ControllerGeometry.GuideLine) {
      ControllerGeometry.GuideLine.material.SetColor("_EmissionColor", m_Tint * m_BaseIntensity);
    }
  }

  private Color GetTintColor() {
    return m_Tint * (m_BaseIntensity + m_GlowIntensity);
  }

  public void EnableTransformVisuals(bool bEnable, float fIntensity) {
    TransformVisuals.SetActive(bEnable && App.Instance.ShowControllers);
    ControllerGeometry.TransformVisualsRenderer.material.SetFloat("_Intensity", fIntensity);
  }

  public void SetGripState(GripState state) {
    if (m_CurrentGripState != state) {
      ControllerStyle style = ControllerGeometry.Style;
      if (style != ControllerStyle.InitializingSteamVR &&
          style != ControllerStyle.None &&
          style != ControllerStyle.Unset) {

        bool manuallyAnimateGrips = (style == ControllerStyle.Vive ||
                                     style == ControllerStyle.Wmr);

        switch (state) {
        case GripState.Standard:
          if (manuallyAnimateGrips) {
            ControllerGeometry.LeftGripMesh.transform.localPosition = Vector3.zero;
            ControllerGeometry.RightGripMesh.transform.localPosition = Vector3.zero;
          }
          ControllerGeometry.LeftGripMesh.material = ControllerGeometry.BaseGrippedMaterial;
          ControllerGeometry.RightGripMesh.material = ControllerGeometry.BaseGrippedMaterial;
          break;
        case GripState.ReadyToGrip:
          if (manuallyAnimateGrips) {
            ControllerGeometry.LeftGripMesh.transform.localPosition =
                m_ControllerGeometry.LeftGripPopOutVector;
            Vector3 vRightPopOut = m_ControllerGeometry.LeftGripPopOutVector;
            vRightPopOut.x *= -1.0f;
            ControllerGeometry.RightGripMesh.transform.localPosition = vRightPopOut;
          }
          ControllerGeometry.LeftGripMesh.material = m_ControllerGeometry.GripReadyMaterial;
          ControllerGeometry.RightGripMesh.material = m_ControllerGeometry.GripReadyMaterial;
          ControllerGeometry.LeftGripMesh.material.SetColor("_Color", m_Tint);
          ControllerGeometry.RightGripMesh.material.SetColor("_Color", m_Tint);
          break;
        case GripState.Gripped:
          if (manuallyAnimateGrips) {
            ControllerGeometry.LeftGripMesh.transform.localPosition =
                m_ControllerGeometry.LeftGripPopInVector;
            Vector3 vRightPopIn = m_ControllerGeometry.LeftGripPopInVector;
            vRightPopIn.x *= -1.0f;
            ControllerGeometry.RightGripMesh.transform.localPosition = vRightPopIn;
          }
          ControllerGeometry.LeftGripMesh.material = m_ControllerGeometry.GrippedMaterial;
          ControllerGeometry.RightGripMesh.material = m_ControllerGeometry.GrippedMaterial;
          ControllerGeometry.LeftGripMesh.material.SetColor("_Color", m_Tint);
          ControllerGeometry.RightGripMesh.material.SetColor("_Color", m_Tint);
          break;
        }
      }
    }
    m_CurrentGripState = state;
  }
}
}  // namespace TiltBrush
