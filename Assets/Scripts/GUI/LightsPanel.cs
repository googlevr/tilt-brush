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

namespace TiltBrush {

[Serializable]
public enum LightMode {
  Ambient = -1,
  Shadow,
  NoShadow,
  NumLights // This should *not* include Ambient light in the count.
}

public class LightsPanel : BasePanel {
  [SerializeField] private LightButton[] m_LightButtons;
  [SerializeField] private LightGizmo m_MainLightGizmoPrefab;
  [SerializeField] private LightGizmo m_SecondaryLightGizmoPrefab;
  [SerializeField] private float m_LightSize = 0.6f;
  [SerializeField] private Material m_PreviewSphereMaterial;
  [SerializeField] private Material m_LightButtonHDRMaterial;
  [SerializeField] private Transform m_PreviewSphereParent;
  [SerializeField] private float m_ColorPickerPopUpHeightOffset = 0.4f;

  [SerializeField] private Transform m_ShadowLightFake;
  [SerializeField] private Renderer m_ShadowLightColorIndicator;
  [SerializeField] private Transform m_NoShadowLightFake;
  [SerializeField] private Renderer m_NoShadowLightColorIndicator;
  [SerializeField] private Transform m_PreviewSphere;
  [SerializeField] private Transform m_PreviewSphereBase;

  // Avoid writing to the actual material, use an instance instead.
  // This also avoids exposing the instance to the editor.
  private Material m_PreviewSphereMaterialInstance;

  private LightGizmo m_LightGizmo_Shadow;
  private LightGizmo m_LightGizmo_NoShadow;
  private LightGizmo m_GizmoPointedAt;

  public Vector3 PreviewCenter {
    get {
      return m_PreviewSphere.transform.position;
    }
  }

  public Vector3 LightWidgetPosition(Quaternion lightRot) {
    var pos = PreviewCenter + lightRot * Vector3.back * m_LightSize *
      (1 + m_GazeActivePercent * (m_GazeHighlightScaleMultiplier - 1.0f));
    return pos;
  }

  public bool IsLightGizmoBeingDragged {
    get { return m_LightGizmo_Shadow.IsBeingDragged || m_LightGizmo_NoShadow.IsBeingDragged; }
  }

  public bool IsLightGizmoBeingHovered {
    get { return m_LightGizmo_Shadow.IsBeingHovered || m_LightGizmo_NoShadow.IsBeingHovered; }
  }

  public Vector3 ActiveLightGizmoPosition {
    get {
      Debug.Assert(IsLightGizmoBeingDragged || IsLightGizmoBeingHovered);
      return m_LightGizmo_Shadow.IsBeingHovered || m_LightGizmo_Shadow.IsBeingDragged ?
            m_LightGizmo_Shadow.transform.position : m_LightGizmo_NoShadow.transform.position;
    }
  }

  public override void OnPanelMoved() {
    m_LightGizmo_Shadow.UpdateTransform();
    m_LightGizmo_NoShadow.UpdateTransform();

    if (m_GazeActivePercent == 0) {
      m_ShadowLightFake.transform.position = m_LightGizmo_Shadow.transform.position;
      m_NoShadowLightFake.transform.position = m_LightGizmo_NoShadow.transform.position;
      float zOffset = m_PreviewSphere.transform.localPosition.z;
      m_ShadowLightFake.transform.localPosition = new Vector3(
        m_ShadowLightFake.transform.localPosition.x,
        m_ShadowLightFake.transform.localPosition.y,
        zOffset + (IsPositionCloserThanPreview(m_ShadowLightFake.transform.position) ? -.1f : .1f));
      m_NoShadowLightFake.transform.localPosition = new Vector3(
        m_NoShadowLightFake.transform.localPosition.x,
        m_NoShadowLightFake.transform.localPosition.y,
        zOffset + (IsPositionCloserThanPreview(m_NoShadowLightFake.transform.position) ? -.05f : .05f));
    }
  }

  bool IsPositionCloserThanPreview(Vector3 pos) {
    return (pos - ViewpointScript.Head.position).magnitude <
           (PreviewCenter - ViewpointScript.Head.position).magnitude;
  }

  void ActivateButtons(bool show) {
    for (int i = 0; i < m_LightButtons.Length; i++) {
      m_LightButtons[i].gameObject.SetActive(show);
    }
  }

  override public void OnWidgetShowAnimComplete() {
    OnPanelMoved();
    for (int i = -1; i < (int)LightMode.NumLights; i++) {
      m_LightButtons[i+1].SetDescriptionText(LightModeToString((LightMode)i),
        ColorTable.m_Instance.NearestColorTo(GetLightColor((LightMode)i)));
    }
    m_ShadowLightFake.gameObject.SetActive(true);
    m_NoShadowLightFake.gameObject.SetActive(true);
  }

  public override void OnWidgetShowAnimStart() {
    m_LightGizmo_Shadow.Visible = false;
    m_LightGizmo_NoShadow.Visible = false;
  }

  override protected void Awake() {
    base.Awake();
    // Avoid writing directly to the material, since that will save changes to disk when in editor.
    if (m_PreviewSphereMaterial) {
      m_PreviewSphereMaterialInstance = Instantiate(m_PreviewSphereMaterial);
    }
    m_PreviewSphere.GetComponent<MeshRenderer>().material = m_PreviewSphereMaterialInstance;
    m_PreviewSphereBase.GetComponent<MeshRenderer>().material = m_PreviewSphereMaterialInstance;
    m_ShadowLightColorIndicator.material.SetFloat("_FlattenAmount", 1);
    m_NoShadowLightColorIndicator.material.SetFloat("_FlattenAmount", 1);
  }

  void Start() {
    OnPanelMoved();
  }

  override protected void OnEnablePanel() {
    base.OnEnablePanel();
    if (m_LightGizmo_Shadow) {
      m_LightGizmo_Shadow.gameObject.SetActive(true);
    } else {
      m_LightGizmo_Shadow = Instantiate(m_MainLightGizmoPrefab);
      m_LightGizmo_Shadow.SetParentPanel(this);
    }
    m_LightGizmo_Shadow.UpdateTint();

    if (m_LightGizmo_NoShadow) {
      m_LightGizmo_NoShadow.gameObject.SetActive(true);
    } else {
      m_LightGizmo_NoShadow = Instantiate(m_SecondaryLightGizmoPrefab);
      m_LightGizmo_NoShadow.SetParentPanel(this);
    }
    m_LightGizmo_NoShadow.UpdateTint();

    m_ShadowLightFake.gameObject.SetActive(true);
    m_NoShadowLightFake.gameObject.SetActive(true);
    ActivateButtons(true);
  }

  override protected void OnDisablePanel() {
    base.OnDisablePanel();
    if (m_LightGizmo_Shadow) {
      m_LightGizmo_Shadow.gameObject.SetActive(false);
    }
    if (m_LightGizmo_NoShadow) {
      m_LightGizmo_NoShadow.gameObject.SetActive(false);
    }
    ActivateButtons(false);
    m_ShadowLightFake.gameObject.SetActive(false);
    m_NoShadowLightFake.gameObject.SetActive(false);
  }

  public Color GetLightColor(LightMode mode) {
    switch (mode) {
    case LightMode.Ambient:
      return RenderSettings.ambientLight;
    case LightMode.Shadow:
      return m_LightGizmo_Shadow.GetColor();
    case LightMode.NoShadow:
      return m_LightGizmo_NoShadow.GetColor();
    }
    return Color.black;
  }

  public void SetDiscoLights(Color ambient, Color shadow, Color noShadow, bool noRecord) {
    SetLightColor(LightMode.Ambient, ambient, noRecord);
    SetLightColor(LightMode.Shadow, shadow, noRecord);
    SetLightColor(LightMode.NoShadow, noShadow, noRecord);
    RefreshLightButtons();
  }

  void SetLightColor(LightMode mode, Color color, bool noRecord = false) {
    ModifyLightCommand command = null;
    switch (mode) {
    case LightMode.Ambient:
      command = new ModifyLightCommand(mode, color, Quaternion.identity);
      break;
    case LightMode.Shadow:
    case LightMode.NoShadow:
      command = new ModifyLightCommand(mode, color,
          App.Scene.GetLight((int)mode).transform.localRotation);
      break;
    }
    command.Redo();
    if (!noRecord) {
      SketchMemoryScript.m_Instance.RecordCommand(command);
    }
  }

  public LightGizmo GetLight(LightMode light) {
    switch (light) {
    case LightMode.Shadow:
      return m_LightGizmo_Shadow;
    case LightMode.NoShadow:
      return m_LightGizmo_NoShadow;
    }
    return null;
  }

  Action<Color> OnColorPicked(LightMode mode) {
    return delegate(Color c) {
      SetLightColor(mode, c);
      if (mode == LightMode.Shadow || mode == LightMode.NoShadow) {
        SceneSettings.m_Instance.UpdateReflectionIntensity();
      }
    };
  }

  void Update() {
    UpdateState();
    CalculateBounds();

    if (m_Fixed || SketchControlsScript.m_Instance.IsUserGrabbingWorld()) {
      OnPanelMoved();
    }

    if (m_PanelDescriptionState != DescriptionState.Closed) {
      m_PanelDescriptionTextSpring.Update(m_DescriptionSpringK, m_DescriptionSpringDampen);

      //orient text and swatches
      Quaternion qAngleOrient = Quaternion.Euler(0.0f, m_PanelDescriptionTextSpring.m_CurrentAngle, 0.0f);
      Quaternion qOrient = m_Mesh.transform.rotation * qAngleOrient;
      m_PanelDescriptionObject.transform.rotation = qOrient;

      //position text and swatches
      Vector3 vPanelDescriptionOffset = m_Bounds;
      vPanelDescriptionOffset.x *= m_PanelDescriptionOffset.x;
      vPanelDescriptionOffset.y *= m_PanelDescriptionOffset.y;
      Vector3 vTransformedDescOffset = m_Mesh.transform.rotation * vPanelDescriptionOffset;
      m_PanelDescriptionObject.transform.position = m_Mesh.transform.position + vTransformedDescOffset;

      //alpha text and swatches
      float fDistToClosed = Mathf.Abs(m_PanelDescriptionTextSpring.m_CurrentAngle - m_DescriptionClosedAngle);
      Color rItemColor = m_PanelDescriptionColor;
      float fRatio = fDistToClosed / m_DescriptionAlphaDistance;
      float fAlpha = Mathf.Min(fRatio * fRatio, 1.0f);
      rItemColor.a = fAlpha;
      if (m_PanelDescriptionTextMeshPro) {
        m_PanelDescriptionTextMeshPro.color = rItemColor;
      }

      if (m_PanelDescriptionState == DescriptionState.Closing) {
        float fToDesired = m_PanelDescriptionTextSpring.m_DesiredAngle - m_PanelDescriptionTextSpring.m_CurrentAngle;
        if (Mathf.Abs(fToDesired) <= m_CloseAngleThreshold) {
          m_PanelDescriptionRenderer.enabled = false;
          m_PreviewSphereMaterialInstance.SetFloat("_FlattenAmount", 1.0f);
          m_PanelDescriptionState = DescriptionState.Closed;
        }
      }
    }
    UpdateGazeBehavior();
    UpdateFixedTransition();
  }

  override public bool RaycastAgainstMeshCollider(Ray rRay, out RaycastHit rHitInfo, float fDist) {
    rHitInfo = new RaycastHit();

    // If we're dragging a gizmo, we're always colliding with this panel.
    if (m_LightGizmo_Shadow.IsBeingDragged) {
      rHitInfo.point = m_LightGizmo_Shadow.transform.position;
      rHitInfo.normal = (rHitInfo.point - rRay.origin).normalized;
      return true;
    } else if (m_LightGizmo_NoShadow.IsBeingDragged) {
      rHitInfo.point = m_LightGizmo_NoShadow.transform.position;
      rHitInfo.normal = (rHitInfo.point - rRay.origin).normalized;
      return true;
    }

    // Precise checks against gizmos.
    if (!m_ActivePopUp && m_GazeActive) {
      if (m_LightGizmo_Shadow.Collider.Raycast(rRay, out rHitInfo, fDist)) {
        rHitInfo.point = m_LightGizmo_Shadow.transform.position;
        rHitInfo.normal = (rHitInfo.point - rRay.origin).normalized;
        m_GizmoPointedAt = m_LightGizmo_Shadow;
        return true;
      } else if (m_LightGizmo_NoShadow.Collider.Raycast(rRay, out rHitInfo, fDist)) {
        rHitInfo.point = m_LightGizmo_NoShadow.transform.position;
        rHitInfo.normal = (rHitInfo.point - rRay.origin).normalized;
        m_GizmoPointedAt = m_LightGizmo_NoShadow;
        return true;
      } else {
        m_GizmoPointedAt = null;
      }

      // Gross checks against broad colliders for gizmos.  These checks ensure the user doesn't
      // lose focus on the panel when targeting a light gizmo that's angled in a way where pointing
      // at it would not satisfy the standard checks below.
      if (m_LightGizmo_Shadow.BroadCollider.Raycast(rRay, out rHitInfo, fDist) ||
          m_LightGizmo_NoShadow.BroadCollider.Raycast(rRay, out rHitInfo, fDist)) {
        // Spoof a collision along the plane of the mesh collider
        Plane meshPlane = new Plane(m_MeshCollider.transform.forward,
            m_MeshCollider.transform.position);
        float rayDist;
        meshPlane.Raycast(rRay, out rayDist);
        rHitInfo.point = rRay.GetPoint(rayDist);
        rHitInfo.normal = m_MeshCollider.transform.forward;
        return true;
      }
    }

    // Fall back on standard panel collider.
    return base.RaycastAgainstMeshCollider(rRay, out rHitInfo, fDist);
  }

  override public void OnUpdatePanel(Vector3 vToPanel, Vector3 vHitPoint) {
    base.OnUpdatePanel(vToPanel, vHitPoint);

    m_LightGizmo_Shadow.UpdateDragState(
      m_GizmoPointedAt == m_LightGizmo_Shadow, m_InputValid);
    m_LightGizmo_NoShadow.UpdateDragState(
      m_GizmoPointedAt == m_LightGizmo_NoShadow, m_InputValid);

    TutorialManager.m_Instance.UpdateLightGizmoHint();
  }

  override public void PanelGazeActive(bool bActive) {
    base.PanelGazeActive(bActive);
    m_LightGizmo_Shadow.UpdateDragState(false, false);
    m_LightGizmo_NoShadow.UpdateDragState(false, false);
    for (int i = 0; i < m_LightButtons.Length; ++i) {
      m_LightButtons[i].ResetState();
    }
    m_GizmoPointedAt = null;
    if (!bActive) {
      TutorialManager.m_Instance.ClearLightGizmoHint();
    }
  }

  public void Refresh() {
    var shadowColor = m_LightGizmo_Shadow.SetLight(0);
    var noShadowColor = m_LightGizmo_NoShadow.SetLight(1);

    m_ShadowLightColorIndicator.material.SetColor("_TrueColor", shadowColor);
    m_ShadowLightColorIndicator.material.SetColor("_ClampedColor",
        ColorPickerUtils.ClampColorIntensityToLdr(shadowColor));
    m_NoShadowLightColorIndicator.material.SetColor("_TrueColor", noShadowColor);
    m_NoShadowLightColorIndicator.material.SetColor("_ClampedColor",
        ColorPickerUtils.ClampColorIntensityToLdr(noShadowColor));

    // TODO : Patch this up.
    //if (m_PanelPopUpScript) {
    //  ColorPickerPopUpWindow picker = m_PanelPopUpScript as ColorPickerPopUpWindow;
    //  if (picker) {
    //    picker.RefreshCurrentColor();
    //  }
    //}

    RefreshLightButtons();
    OnPanelMoved();
  }

  void RefreshLightButtons() {
    Color panelColor = GetGazeColor();
    m_LightButtons[0].SetColor(panelColor);
    m_LightButtons[1].SetColor(panelColor);
    m_LightButtons[2].SetColor(panelColor);

    for (int i = -1; i < (int)LightMode.NumLights; i++) {
      m_LightButtons[i + 1].SetDescriptionText(LightModeToString((LightMode)i),
        ColorTable.m_Instance.NearestColorTo(GetLightColor((LightMode)i)));
    }

    m_LightButtonHDRMaterial.SetColor(
      "_ClampedColor", ColorPickerUtils.ClampColorIntensityToLdr(m_LightGizmo_Shadow.GetColor()));
  }

  override protected void OnUpdateActive() {
    if (m_GazeActive && m_CurrentState == PanelState.Available) {
      m_PanelDescriptionState = DescriptionState.Open;
      m_PanelDescriptionTextSpring.m_DesiredAngle = m_DescriptionOpenAngle;
      m_PanelDescriptionRenderer.enabled = true;
    } else {
      m_PanelDescriptionState = DescriptionState.Closing;
      m_PanelDescriptionTextSpring.m_DesiredAngle = m_DescriptionClosedAngle;
      ResetPanelFlair();
    }
  }

  string LightModeToString(LightMode mode) {
    switch (mode) {
    case LightMode.Ambient:
      return "Fill Light";
    case LightMode.Shadow:
      return "Main Light";
    case LightMode.NoShadow:
      return "Secondary Light";
    }
    return "";
  }

  public void ButtonPressed(LightMode mode) {
    m_LightGizmo_Shadow.Visible = false;
    m_LightGizmo_NoShadow.Visible = false;
    m_PreviewSphereParent.gameObject.SetActive(false);

    // Create the popup with callback.
    SketchControlsScript.GlobalCommands command =
        (mode == LightMode.Shadow || mode == LightMode.NoShadow) ?
        SketchControlsScript.GlobalCommands.LightingHdr :
        SketchControlsScript.GlobalCommands.LightingLdr;
    CreatePopUp(command, -1, -1, LightModeToString(mode), MakeOnPopUpClose(mode));

    // Init popup according to current light mode.
    var popup = (m_ActivePopUp as ColorPickerPopUpWindow);
    popup.transform.localPosition += new Vector3(0, m_ColorPickerPopUpHeightOffset, 0);
    ColorPickerUtils.SetLogVRangeForMode(mode);
    popup.ColorPicker.ColorPicked += OnColorPicked(mode);
    popup.ColorPicker.ColorPicked += delegate(Color c) {
      m_LightButtons[(int)mode + 1].SetDescriptionText(LightModeToString(mode),
        ColorTable.m_Instance.NearestColorTo(c));
      SetLightColor(mode, c);
    };

    // Init must be called after all popup.ColorPicked actions have been assigned.
    popup.ColorPicker.Controller.CurrentColor = GetLightColor(mode);
    popup.ColorPicker.ColorFinalized += MakeLightColorFinalized(mode);
    popup.CustomColorPalette.ColorPicked += MakeLightColorPickedAsFinal(mode);

    m_EatInput = true;
  }

  Action MakeOnPopUpClose(LightMode mode) {
    return delegate {
      m_LightGizmo_Shadow.Visible = true;
      m_LightGizmo_NoShadow.Visible = true;
      m_PreviewSphereParent.gameObject.SetActive(true);

      Color lightColor = GetLightColor(mode);
      m_LightButtons[(int)mode + 1].SetDescriptionText(
          LightModeToString(mode),
          ColorTable.m_Instance.NearestColorTo(lightColor));
    };
  }

  Action MakeLightColorFinalized(LightMode mode) {
    return delegate {
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(mode == LightMode.Ambient ?
        new ModifyLightCommand(mode, RenderSettings.ambientLight, Quaternion.identity, final: true) :
        new ModifyLightCommand(mode, App.Scene.GetLight((int)mode).color,
          App.Scene.GetLight((int)mode).transform.localRotation, final: true));
    };
  }

  Action<Color> MakeLightColorPickedAsFinal(LightMode mode) {
    return delegate(Color c) {
      SetLightColor(mode, c);
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(mode == LightMode.Ambient ?
        new ModifyLightCommand(mode, RenderSettings.ambientLight, Quaternion.identity, final: true) :
        new ModifyLightCommand(mode, App.Scene.GetLight((int)mode).color,
          App.Scene.GetLight((int)mode).transform.localRotation, final: true));
    };
  }

  protected override void UpdateGazeBehavior() {
    float fPrevPercent = m_GazeActivePercent;
    base.UpdateGazeBehavior();
    // When inactive, light gizmos' white outline will tint grey.
    m_NoShadowLightColorIndicator.material.SetColor("_Color", Color.gray);
    m_ShadowLightColorIndicator.material.SetColor("_Color", Color.gray);

    if (m_UseGazeRotation && m_Mesh) {
      if (fPrevPercent > 0.0f && fPrevPercent < 1.0f) {
        m_LightGizmo_Shadow.Visible = m_GazeActivePercent > 0;
        m_LightGizmo_NoShadow.Visible = m_GazeActivePercent > 0;
        float fScaleMult = m_GazeActivePercent * (m_GazeHighlightScaleMultiplier - 1.0f);
        Vector3 newScale = m_BaseScale * m_AdjustedScale * (1.0f + fScaleMult);
        m_LightGizmo_Shadow.transform.localScale = newScale * m_LightSize;
        m_LightGizmo_NoShadow.transform.localScale = newScale * m_LightSize;

        m_ShadowLightFake.gameObject.SetActive(m_GazeActivePercent == 0);
        m_NoShadowLightFake.gameObject.SetActive(m_GazeActivePercent == 0);

        OnPanelMoved();

        m_PreviewSphereMaterialInstance.SetFloat("_FlattenAmount", 1.0f - m_GazeActivePercent);
        m_PreviewSphereParent.localPosition = new Vector3(0, 0, -m_GazeActivePercent);
      }
    }
  }
}
}  // namespace TiltBrush
