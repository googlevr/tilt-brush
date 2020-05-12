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

public class LoadSketchButton : BaseButton {
  [System.Serializable]
  public struct MenuButton {
    public SketchControlsScript.GlobalCommands m_Command;
    public Texture m_Icon;
  }

  [SerializeField] private InspectSketchButton m_MenuButton;
  [SerializeField] private GameObject m_Warning;
  [SerializeField] private Material m_WarningMaterial;
  [SerializeField] private Material m_ErrorMaterial;

  private bool m_ThumbnailLoaded = false;
  private bool m_SizeOk = true;
  private Vector2 m_DynamicUvScale;
  private Vector2 m_DynamicUvOffset;
  private float m_DynamicUvTransitionSpeed = 12.0f;
  private float m_DynamicUvTransitionValue;
  private int m_SketchIndex;
  private SketchSet m_SketchSet;
  private UIComponentManager m_UIComponentManager;

  public int SketchIndex {
    get { return m_SketchIndex; }
    set {
      m_SketchIndex = value;
      RefreshDetails();
    }
  }

  public SketchSet SketchSet {
    get { return m_SketchSet; }
    set {
      m_SketchSet = value;
      RefreshDetails();
    }
  }

  public bool ThumbnailLoaded {
    get { return m_ThumbnailLoaded; }
    set { m_ThumbnailLoaded = value; }
  }

  public float FadeIn {
    set { SetMaterialFloat("_FadeIn", value); }
  }

  public bool WarningVisible {
    set {
      if (m_Warning != null) {
        m_Warning.SetActive(value);
      }
    }
    get { return m_Warning != null && m_Warning.activeSelf; }
  }

  void RefreshDetails() {
    m_MenuButton.SetSketchDetails(m_SketchIndex, SketchSet.Type);

    m_SizeOk = true;
    if (m_SketchSet.Type == SketchSetType.Liked) {
      if (m_SketchSet.IsSketchIndexValid(m_SketchIndex)) {
        SceneFileInfo sfi = m_SketchSet.GetSketchSceneFileInfo(m_SketchIndex);
        if (sfi.TriangleCount is int triCount) {
          m_SizeOk = triCount <
              QualityControls.m_Instance.AppQualityLevels.MaxPolySketchTriangles;
        }
      }
    }
    if (m_Warning != null) {
      m_Warning.GetComponent<Renderer>().material = m_SizeOk ? m_WarningMaterial : m_ErrorMaterial;
    }
  }

  public void UpdateUvOffsetAndScale(Vector2 offset, Vector2 scale) {
    // Keep the dynamic uv up to date, but don't set it if we currently have focus.
    m_DynamicUvOffset = offset;
    m_DynamicUvScale = scale;
    if (!m_HadFocus) {
      m_ButtonRenderer.material.SetTextureOffset("_MainTex", m_DynamicUvOffset);
      m_ButtonRenderer.material.SetTextureScale("_MainTex", m_DynamicUvScale);
    }
  }

  override protected void Awake() {
    base.Awake();
    m_UIComponentManager = GetComponent<UIComponentManager>();
    WarningVisible = false;
    m_DynamicUvScale = Vector2.one;
    m_DynamicUvOffset = Vector2.zero;
    m_DynamicUvTransitionValue = 0.0f;
  }

  override public void SetColor(Color color) {
    // Darken the button if we're not able to load it.
    Color backupColor = color;
    if (!m_SizeOk) {
      float alpha = color.a;
      color *= kUnavailableTintAmount;
      color.a = alpha;
    }

    base.SetColor(color);
    if (m_Warning != null) {
      m_Warning.GetComponent<Renderer>().material.SetColor("_Color", backupColor);
    }
    m_UIComponentManager.SetColor(color);
  }

  override protected void OnButtonPressed() {
    if (!m_SketchSet.GetSketchSceneFileInfo(m_SketchIndex).Available &&
      m_SketchSet.Type != SketchSetType.Drive) {
      return;
    }

    // Sequence on load is:
    // LoadConfirmUnsaved -> LoadWaitOnDownload -> LoadConfirmComplex -> LoadComplexHigh ->  Load
    SketchControlsScript.m_Instance.IssueGlobalCommand(
        SketchControlsScript.GlobalCommands.LoadConfirmUnsaved,
        m_SketchIndex, (int)m_SketchSet.Type);
    ResetState();
  }

  override public bool UpdateStateWithInput(bool inputValid, Ray inputRay,
        GameObject parentActiveObject, Collider parentCollider) {
    // TODO : This is a bit backwards.  We need to pipe input to the bottom of a
    // UIComponent stack, and then invalidate input as we come back up.  This logic is a
    // deliberate version of that, but consider this a TODO for making the system work
    // more generally in this way.
    if (parentActiveObject == null || parentActiveObject == gameObject) {
      bool resetState = false;
      m_UIComponentManager.UpdateUIComponents(inputRay, inputValid, parentCollider);
      if (m_UIComponentManager.ActiveInputObject != null &&
          m_UIComponentManager.ActiveInputObject != gameObject) {
        resetState = inputValid;
        inputValid = false;
      }

      if (base.UpdateStateWithInput(inputValid, inputRay, parentActiveObject, parentCollider)) {
        // If a child component received input, we want this component to reset its state,
        // so we return false to tell our parent we don't have focus anymore.
        return !resetState;
      }
    }
    return false;
  }

  override public void GainFocus() {
    base.GainFocus();
    m_DynamicUvTransitionValue = 0.0f;
    m_MenuButton.gameObject.SetActive(m_SketchSet.Type == SketchSetType.User);
    if (!m_SizeOk) {
      SetDescriptionVisualsAvailable(false);
    }
  }

  override public void HasFocus(RaycastHit rHitInfo) {
    m_DynamicUvTransitionValue += Mathf.Min(m_DynamicUvTransitionSpeed * Time.deltaTime, 1.0f);

    Vector2 scale = Vector2.Lerp(m_DynamicUvScale, Vector2.one, m_DynamicUvTransitionValue);
    m_ButtonRenderer.material.SetTextureScale("_MainTex", scale);

    Vector2 offset = Vector2.Lerp(m_DynamicUvOffset, Vector2.zero, m_DynamicUvTransitionValue);
    m_ButtonRenderer.material.SetTextureOffset("_MainTex", offset);
  }

  override public void LostFocus() {
    base.LostFocus();
    m_MenuButton.gameObject.SetActive(false);
  }

  public override void ResetState() {
    base.ResetState();
    m_UIComponentManager.ResetInput();
    m_MenuButton.gameObject.SetActive(false);
  }
}

}  // namespace TiltBrush
