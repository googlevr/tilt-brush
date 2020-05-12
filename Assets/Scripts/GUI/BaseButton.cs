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

public class BaseButton : UIComponent {
    protected enum ButtonState {
    Unavailable,
    Untouched,
    Hover,
    Held,
    Pressed
  }

  [SerializeField] protected Texture2D m_ButtonTexture;
  [SerializeField] protected bool m_AtlasTexture = true;
  [SerializeField] protected bool m_ToggleButton = false;
  [SerializeField] protected bool m_LongPressReleaseButton = false;
  [SerializeField] protected bool m_ButtonHasPressedAudio = true;
  [SerializeField] protected float m_ZAdjustHover = -0.02f;
  [SerializeField] protected float m_ZAdjustClick = 0.05f;
  [SerializeField] protected float m_HoverScale = 1.1f;
  [SerializeField] protected float m_HoverBoxColliderGrow = 0.2f;

  [Header("This checkbox makes the button experimental-only")]
  [SerializeField] private bool m_AddOverlay = false;

  protected Renderer m_ButtonRenderer;
  protected bool m_ToggleActive;
  protected bool m_ButtonSelected;

  private Texture2D m_LastTextureAtlased;
  private bool m_AtlasFlag_Activated;
  private bool m_AtlasFlag_Focus;
  private List<Vector2> m_MeshUvsForAtlasing;

  protected Texture2D m_CurrentButtonTexture;
  // TODO : See if we can remove the concept of button states.  It feels redundant
  // with the new UIComponent state machine and is leading to edge case bugs.
  protected ButtonState m_CurrentButtonState;
  protected float m_ZAdjustBase;
  protected Vector3 m_ScaleBase;
  protected Vector3? m_BoxColliderSizeBase;
  protected bool m_UseScale;
  protected bool m_AllowBypassHover;

  public Vector3 ScaleBase { get { return m_ScaleBase; } }

  public virtual bool IsAvailable() { return m_CurrentButtonState != ButtonState.Unavailable; }
  public bool IsPressed() { return m_CurrentButtonState == ButtonState.Pressed; }
  public bool IsHover() { return m_CurrentButtonState == ButtonState.Hover; }

  protected void RefreshAtlasedMaterial() {
    m_ButtonRenderer.material =
        SketchControlsScript.m_Instance.IconTextureAtlas.GetAppropriateMaterial(
          m_AtlasFlag_Activated, m_AtlasFlag_Focus);
  }

  protected bool AssignAtlasedTexture(Texture2D tex) {
    // Early out if we're trying to set our texture to our current texture.
    if (tex != null && m_LastTextureAtlased != null &&
        tex.GetHashCode() == m_LastTextureAtlased.GetHashCode()) {
      return true;
    }

    Rect r;
    if (SketchControlsScript.m_Instance.IconTextureAtlas.GetTextureUVs(tex, out r)) {
      RefreshAtlasedMaterial();

      // Lazy initialize the mesh uv backup list.
      if (m_MeshUvsForAtlasing == null) {
        m_MeshUvsForAtlasing = new List<Vector2>();
        GetComponent<MeshFilter>().mesh.GetUVs(0, m_MeshUvsForAtlasing);
      }

      // Scale Uvs appropriately and set on the mesh.
      List<Vector2> uvs = IconTextureAtlas.ScaleUvsWithAtlasRect(r, m_MeshUvsForAtlasing);
      GetComponent<MeshFilter>().mesh.SetUVs(0, uvs);

      m_LastTextureAtlased = tex;
      return true;
    }
    return false;
  }

  override protected void Awake() {
    base.Awake();

    m_ButtonSelected = false;
    m_CurrentButtonTexture = m_ButtonTexture;
    m_ButtonRenderer = GetComponent<Renderer>();

    m_AtlasFlag_Activated = false;
    // Default focus to on so the button is bright and cheery by default.  Panels will
    // control focus and dim buttons if necessary.
    m_AtlasFlag_Focus = true;
    ConfigureTextureAtlas();

    UpdateUVsForAspect(-1);

    m_CurrentButtonState = ButtonState.Untouched;
    m_ZAdjustBase = transform.localPosition.z;
    m_ScaleBase = transform.localScale;
    BoxCollider bc = m_Collider != null ? m_Collider as BoxCollider : null;
    if (bc != null) {
      m_BoxColliderSizeBase = bc.size;
    }
    m_UseScale = Mathf.Abs(m_HoverScale - 1.0f) > 0.001f;
    m_HoldFocus = !m_LongPressReleaseButton;
    m_StealFocus = m_LongPressReleaseButton;

    // Experimental buttons should self-destruct.
    // This is hackable, but it's okay to do this at runtime since the functionality
    // behind the button should also be behind #ifdefs. It can't be done at build time
    // since some buttons are in prefabs rather than in the scene.
    bool selfDestruct = m_AddOverlay;

#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (m_AddOverlay && Config.IsExperimental) {
      GameObject overlay = Instantiate(App.Config.m_LabsButtonOverlayPrefab);
      overlay.transform.SetParent(transform, false);
      selfDestruct = false;
    }
#endif

    if (selfDestruct) {
      gameObject.SetActive(false);
      Destroy(this);
    }
  }

  virtual protected void ConfigureTextureAtlas() {
    bool useBackupTexture = true;
    if (m_AtlasTexture && SketchControlsScript.m_Instance.AtlasIconTextures) {
      useBackupTexture = !AssignAtlasedTexture(m_ButtonTexture);
    }
    // If we shouldn't atlas this texture, or we can't find it, fall back to default.
    if (useBackupTexture) {
      if (m_CurrentButtonTexture != null) {
        m_ButtonRenderer.material.mainTexture = m_CurrentButtonTexture;
      }
      m_AtlasTexture = false;
    }
  }

  override protected void OnRegisterComponent() {
    m_ScaleBase = transform.localScale;
    m_ZAdjustBase = transform.localPosition.z;
  }

  public void SetButtonTexture(Texture2D rTexture, float aspect=-1) {
    if (m_AtlasTexture) {
      if (!AssignAtlasedTexture(rTexture)) {
        string textureName = rTexture == null ? "null" : rTexture.name;
#if UNITY_EDITOR
        string assetName = UnityEditor.AssetDatabase.GetAssetPath(rTexture);
        if (assetName != null) {
          textureName = assetName;
        }
#endif
        if (this is BrushTypeButton) {
          textureName = string.Format("{0} for {1}", textureName, ((BrushTypeButton)this).m_Brush.DurableName);
        }
        Debug.LogErrorFormat(this, "Button texture is not in atlas: {0}.", textureName);
      }
    } else {
      m_CurrentButtonTexture = rTexture;
      m_ButtonRenderer.material.mainTexture = rTexture;
      UpdateUVsForAspect(aspect);
    }
  }

  protected virtual void SetMaterialFloat(string name, float value) {
    if (!m_AtlasTexture) {
      m_ButtonRenderer.material.SetFloat(name, value);
    }
  }

  virtual public void SetButtonGrayscale(bool bGrayscale) {
    if (!m_AtlasTexture) {
      if (bGrayscale) {
        SetMaterialFloat("_Grayscale", 1);
      } else {
        SetMaterialFloat("_Grayscale", 0);
      }
    }
  }

  protected void SetButtonActivated(bool bActivated) {
    // If we're atlasing, switch our atlased material instead of tapping material floats.
    if (m_AtlasTexture) {
      m_AtlasFlag_Activated = bActivated;
      RefreshAtlasedMaterial();
    } else {
      if (bActivated) {
        SetMaterialFloat("_Activated", 1.0f);
      } else {
        SetMaterialFloat("_Activated", 0.0f);
      }
    }
  }

  virtual public void SetButtonSelected(bool bSelected) {
    m_ButtonSelected = bSelected;
    SetButtonActivated(m_ButtonSelected);
  }

  public void SetButtonAvailable(bool bAvailable) {
    SetButtonSelected(false);
    if (!bAvailable) {
      m_CurrentButtonState = ButtonState.Unavailable;
      ResetScale();
    } else {
      if (m_CurrentButtonState == ButtonState.Unavailable) {
        m_CurrentButtonState = ButtonState.Untouched;
      }
    }
  }

  protected virtual void SetMaterialColor(Color rColor) {
    if (m_AtlasTexture) {
      m_AtlasFlag_Focus = (rColor == Color.white);
      RefreshAtlasedMaterial();
    } else {
      m_ButtonRenderer.material.SetColor("_Color", rColor);
    }
  }

  override public void SetColor(Color rColor) {
    if (!IsAvailable()) {
      float alpha = rColor.a;
      rColor *= kUnavailableTintAmount;
      rColor.a = alpha;
    }
    SetMaterialColor(rColor);
  }

  public void SetHDRButtonColor(Color color, Color secondaryColor) {
    if (!IsAvailable()) {
      float alpha = color.a;
      float alpha2 = secondaryColor.a;
      color *= kUnavailableTintAmount;
      secondaryColor *= kUnavailableTintAmount;
      color.a = alpha;
      secondaryColor.a = alpha2;
    }
    if (!m_AtlasTexture) {
      m_ButtonRenderer.material.SetColor("_Color", color);
      m_ButtonRenderer.material.SetColor("_SecondaryColor", secondaryColor);
    }
  }

  override public void ForceDescriptionDeactivate() {
    base.ForceDescriptionDeactivate();
    ResetScale();
  }

  public override bool UpdateStateWithInput(bool inputValid, Ray inputRay,
        GameObject parentActiveObject, Collider parentCollider) {
    if (base.UpdateStateWithInput(inputValid, inputRay, parentActiveObject, parentCollider)) {
      UpdateButtonState(inputValid);
      return true;
    }
    return false;
  }

  public virtual void UpdateButtonState(bool bActivateInputValid) { }

  override public void ButtonPressed(RaycastHit rHitInfo) {
    // Long press buttons don't trigger press until release.
    if (m_LongPressReleaseButton) {
      bool available = IsAvailable();
      SetButtonActivated(available);
      SetDescriptionVisualsAvailable(available);
      SetDescriptionActive(true);
    } else {
      if (IsAvailable()) {
        // AdjustButtonPositionAndScale should be before OnButtonPressed.
        // Buttons that trigger popups reset their scale, which is stomped by
        // this if OnButtonPressed is called first.
        AdjustButtonPositionAndScale(m_ZAdjustClick, m_HoverScale, m_HoverBoxColliderGrow);
        OnButtonPressed();
        m_CurrentButtonState = ButtonState.Pressed;
        if (m_ButtonHasPressedAudio) {
          AudioManager.m_Instance.ItemSelect(transform.position);
        }
      } else {
        AudioManager.m_Instance.DisabledItemSelect(transform.position);
      }
    }

  }

  virtual protected void OnButtonPressed() { }

  override public void ButtonReleased() {
    if (m_LongPressReleaseButton && IsAvailable()) {
      OnButtonPressed();
    }
    if (m_CurrentButtonState != ButtonState.Untouched && IsAvailable()) {
      ResetScale();
      m_CurrentButtonState = ButtonState.Untouched;
    }
  }

  override public void GainFocus() {
    if (!m_LongPressReleaseButton) {
      if (IsAvailable()) {
        AdjustButtonPositionAndScale(m_ZAdjustHover, m_HoverScale, m_HoverBoxColliderGrow);
        if (m_CurrentButtonState != ButtonState.Pressed) {
          AudioManager.m_Instance.ItemHover(transform.position);
        }

        m_CurrentButtonState = ButtonState.Hover;
        SetDescriptionActive(true);
        SetDescriptionVisualsAvailable(true);
      } else {
        SetDescriptionActive(true);
        SetDescriptionVisualsAvailable(false);
      }
    }
  }

  override public void ResetState() {
    base.ResetState();

    if (m_LongPressReleaseButton) {
      SetButtonActivated(false);
      ResetScale();
    }

    //we don't need to set the position if we're already in this state
    if (m_CurrentButtonState != ButtonState.Untouched && IsAvailable()) {
      ResetScale();
      m_CurrentButtonState = ButtonState.Untouched;
    }
    SetDescriptionActive(false);
  }

  protected virtual void AdjustButtonPositionAndScale(
      float posAmount, float scaleAmount, float boxColliderGrow) {
    Vector3 vLocalPos = transform.localPosition;
    vLocalPos.z = m_ZAdjustBase + posAmount;
    transform.localPosition = vLocalPos;
    if (m_UseScale) {
      transform.localScale = m_ScaleBase * scaleAmount;
      AdjustDescriptionScale();
    }
    // Do this after we adjust scale so we have the proper scale in our calculation.
    if (m_BoxColliderSizeBase.HasValue) {
      BoxCollider bc = m_Collider as BoxCollider;
      Vector3 scale = transform.localScale;
      Vector3 size = m_BoxColliderSizeBase.Value;
      size.x += boxColliderGrow / scale.x;
      size.y += boxColliderGrow / scale.y;
      bc.size = size;
    }
  }

  public void ResetScale() {
    AdjustButtonPositionAndScale(0.0f, 1.0f, 0.0f);
  }

  // Set UVs to avoid distorting the texture. Choose truncation over letterboxing.
  // aspect is the aspect ratio of the texture, or -1 to calculate based on the
  // texture width/height.
  void UpdateUVsForAspect(float aspect) {
    if (!m_AtlasTexture) {
      if (m_CurrentButtonTexture != null && aspect <= 0) {
        aspect = m_CurrentButtonTexture.width / m_CurrentButtonTexture.height;
      }
      m_ButtonRenderer.material.SetFloat("_Aspect", aspect);
    }
  }
}
} // namespace TiltBrush
