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

public class BrushTypeButton : BaseButton {
  [SerializeField] private Texture2D m_PreviewBGTexture;
  [SerializeField] private GameObject m_AudioReactiveIcon;
  [SerializeField] private GameObject m_ExperimentalIcon;

  [NonSerialized] public BrushDescriptor m_Brush;
  [NonSerialized] public Vector3 m_OriginPosition;

  protected PreviewCubeScript m_PreviewCubeScript;
  private Renderer m_AudioReactiveIconRenderer;
  private Renderer m_ExperimentalIconRenderer;
  private Vector3 m_AudioReactiveIconBaseLocalPos;
  private Vector3 m_ExperimentalIconBaseLocalPos;
  private Texture2D m_BrushIconTexture;

  override protected void Awake() {
    base.Awake();
    m_AudioReactiveIconRenderer = m_AudioReactiveIcon.GetComponent<Renderer>();
    m_ExperimentalIconRenderer = m_ExperimentalIcon.GetComponent<Renderer>();
    m_OriginPosition = transform.localPosition;
  }

  override protected void OnDescriptionChanged() {
    m_PreviewCubeScript = m_Description.GetComponent<PreviewCubeScript>();
    base.OnDescriptionChanged();
  }

  override protected void OnRegisterComponent() {
    base.OnRegisterComponent();
    m_AudioReactiveIconBaseLocalPos = m_AudioReactiveIcon.transform.localPosition;
    m_ExperimentalIconBaseLocalPos = m_ExperimentalIcon.transform.localPosition;
  }

  override protected void ConfigureTextureAtlas() {
    if (SketchControlsScript.m_Instance.AtlasIconTextures) {
      // Brush icons are assigned by the panel later.  We want atlasing on all our
      // buttons, so just set it to the default for now.
      RefreshAtlasedMaterial();
    } else {
      base.ConfigureTextureAtlas();
    }
  }

  public void SetButtonProperties(BrushDescriptor rBrush) {
    m_Brush = rBrush;

    Texture2D buttonTexture = rBrush.m_ButtonTexture;
    if (buttonTexture == null) {
      Debug.LogWarningFormat(
          rBrush,
          "Button Texture not set for {0}, {1}", rBrush.DurableName, rBrush.m_Guid);
      buttonTexture = BrushCatalog.m_Instance.DefaultBrush.m_ButtonTexture;
    }
    m_BrushIconTexture = buttonTexture;
    m_PreviewCubeScript.SetSampleQuadTexture(buttonTexture);
    SetButtonTexture(buttonTexture);

#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      SetDescriptionText(rBrush.m_Description, rBrush.m_DescriptionExtra);
    } else {
      SetDescriptionText(rBrush.m_Description);
    }
#else
    SetDescriptionText(rBrush.m_Description);
#endif
    m_AudioReactiveIcon.SetActive(rBrush.m_AudioReactive &&
        VisualizerManager.m_Instance.VisualsRequested);
    // Play standard click sound if brush doesn't have a custom button sound
    m_ButtonHasPressedAudio = (rBrush.m_ButtonAudio == null);
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    m_ExperimentalIcon.SetActive(App.Instance.IsBrushExperimental(rBrush));
#endif
  }

  override protected void OnDescriptionActivated() {
    if (m_AtlasTexture) {
      SetButtonTexture(m_PreviewBGTexture);
    } else {
      m_ButtonRenderer.material.mainTexture = m_PreviewBGTexture;
    }
  }

  override protected void OnDescriptionDeactivated() {
    SetButtonSelected(m_ButtonSelected);
  }

  override protected void OnButtonPressed() {
    BrushController.m_Instance.SetActiveBrush(m_Brush);
  }

  override public void ResetState() {
    base.ResetState();
    PlaceIconsOnPreviewCube(0.0f);
  }

  override public void SetButtonSelected(bool bSelected) {
    base.SetButtonSelected(bSelected);
    if (bSelected) {
      m_AudioReactiveIconRenderer.material.SetFloat("_Activated", 1.0f);
      m_ExperimentalIconRenderer.material.SetFloat("_Activated", 1.0f);
    } else {
      m_AudioReactiveIconRenderer.material.SetFloat("_Activated", 0.0f);
      m_ExperimentalIconRenderer.material.SetFloat("_Activated", 0.0f);
    }
    //only set our texture if we're deactivated-- otherwise we'll get it when we turn off the preview
    if (m_DescriptionState == DescriptionState.Deactivated) {
      if (m_AtlasTexture) {
        SetButtonTexture(m_BrushIconTexture);
      } else {
        m_ButtonRenderer.material.mainTexture = m_CurrentButtonTexture;
      }
    }
  }

  override public void UpdateVisuals() {
    base.UpdateVisuals();
    if (m_PreviewCubeScript != null) {
      //keep the preview cube texture up to date.
      // LEGACY.
      // TODO
      // This IF statement and its content can be removed once all the button descriptions
      // have been replaced on brush buttons
      if (m_DescriptionState != DescriptionState.Deactivated) {
        m_PreviewCubeScript.SetSelected(m_ButtonSelected);
      }

      PlaceIconsOnPreviewCube(m_DescriptionActivateTimer);
    }
  }

  void PlaceIconsOnPreviewCube(float offsetPercent) {
    // Two times the depth of the parent (because the PreviewCube will be of size 1 when full).
    float offset = offsetPercent * transform.localScale.z * 2.0f;
    // Subtract off original offset to fit clean on the extended cube.
    offset += m_AudioReactiveIconBaseLocalPos.z * offsetPercent;

    Vector3 localPos = m_AudioReactiveIconBaseLocalPos;
    localPos.z -= offset;
    m_AudioReactiveIcon.transform.localPosition = localPos;

    localPos = m_ExperimentalIconBaseLocalPos;
    localPos.z -= offset;
    m_ExperimentalIcon.transform.localPosition = localPos;
  }

  override public void SetColor(Color rColor) {
    base.SetColor(rColor);
    m_AudioReactiveIconRenderer.material.SetColor("_Color", rColor);
    m_ExperimentalIconRenderer.material.SetColor("_Color", rColor);
  }
}
}  // namespace TiltBrush
