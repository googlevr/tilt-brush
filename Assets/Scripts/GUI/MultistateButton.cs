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
using System.Collections.Generic;

namespace TiltBrush {

// A button that has n sides for n possible values.
// Calls the m_Command command with the selected index as the first param.
// Gets its initial value by checking IsCommandActive with each possible index.
public class MultistateButton : BaseButton {
  [System.Serializable]
  public struct Option {
    public string m_Description;
    public Texture m_Texture;
  }

  [SerializeField] private bool m_ShowRotation = true;
  [SerializeField] private float m_RotationSpeedMultiplier = 14.3f;
  [SerializeField] private SketchControlsScript.GlobalCommands m_Command;
  [SerializeField] protected Option[] m_Options;

  private int m_CurrentOptionIdx;
  private GameObject m_OptionContainer;
  private GameObject[] m_Sides;
  private Renderer[] m_SideRenderers;
  private Color? m_MaterialTint;
  private Dictionary<string, float> m_MaterialFloats;
  private float m_CurrentRotation; // Degrees
  private float m_TargetRotation; // Degrees
  private bool m_IsRotating;

  private int NumOptions {
    get {
      return m_Options.Length;
    }
  }

  private Option CurrentOption {
    get {
      return m_Options[m_CurrentOptionIdx];
    }
  }

  // Degrees between each option icon.
  private float OptionAngleDeltaDegrees {
    get {
      return 360.0f / NumOptions;
    }
  }

  // Radians between each option icon.
  private float OptionAngleDeltaRadians {
    get {
      if (NumOptions == 0) { return 2 * Mathf.PI; }
      return 2 * Mathf.PI / NumOptions;
    }
  }

  // Distance each option icon should be from the center of the carousel.
  // Units are in terms of button widths.
  private float OptionSideDistance {
    get {
      return 1 / (2 * Mathf.Tan(OptionAngleDeltaRadians / 2));
    }
  }

  override protected void Awake() {
    base.Awake();
    m_CurrentOptionIdx = -1;
    m_MaterialFloats = new Dictionary<string, float>();
  }

  override protected void Start() {
    base.Start();
    GetComponent<MeshRenderer>().enabled = false;
    OnStart();
  }

  protected virtual void OnStart() {
    CreateOptionSides();

    // Find if a current option is already active
    for (var i = 0; i < NumOptions; i++) {
      if (SketchControlsScript.m_Instance.IsCommandActive(m_Command, i)) {
        ForceSelectedOption(i);
        break;
      }
    }
  }

  override public void UpdateVisuals() {
    base.UpdateVisuals();
    if (m_IsRotating) {
      if (Mathf.Abs(m_TargetRotation - m_CurrentRotation) < 1 || !m_ShowRotation) {
        m_CurrentRotation = m_TargetRotation;
        m_IsRotating = false;
        HideOtherSides();
      } else {
        float degreesRemaining = m_CurrentRotation - m_TargetRotation;
        while (degreesRemaining < 0) { degreesRemaining += 360;}
        m_CurrentRotation -= (degreesRemaining * Time.deltaTime * m_RotationSpeedMultiplier);
        while (m_CurrentRotation < 0) { m_CurrentRotation += 360;}
      }

      m_OptionContainer.transform.localRotation = Quaternion.Euler(0, m_CurrentRotation, 0);
    }
  }

  private void HideOtherSides() {
    for (int i = 0; i < m_Sides.Length; i++) {
      if (i != m_CurrentOptionIdx) {
        m_Sides[i].SetActive(false);
      }
    }
  }

  private void ShowAllSides() {
    for (int i = 0; i < m_Sides.Length; i++) {
      m_Sides[i].SetActive(true);
    }
  }

  protected void ForceSelectedOption(int index) {
    SetSelectedOption(index);
    m_CurrentRotation = m_TargetRotation;
    m_IsRotating = false;
    HideOtherSides();
    m_OptionContainer.transform.localRotation = Quaternion.Euler(0, m_CurrentRotation, 0);
  }

  protected void SetSelectedOption(int index) {
    if (m_Sides == null) {
      // Button has not called CreateOptionSides() yet.
      return;
    }
    if (m_CurrentOptionIdx == index) { return;}

    ShowAllSides();
    m_IsRotating = true;
    m_CurrentOptionIdx = index;
    m_TargetRotation = OptionAngleDeltaDegrees * (NumOptions - m_CurrentOptionIdx - 1);
    SetExtraDescriptionText(CurrentOption.m_Description);
  }

  override protected void OnButtonPressed() {
    SetSelectedOption((m_CurrentOptionIdx + 1) % NumOptions);
    SketchControlsScript.m_Instance.IssueGlobalCommand(m_Command, m_CurrentOptionIdx, -1);
  }

  protected void CreateOptionSides() {
    m_Sides = new GameObject[NumOptions];
    m_SideRenderers = new Renderer[NumOptions];

    m_OptionContainer = new GameObject();
    m_OptionContainer.transform.parent = transform;
    m_OptionContainer.transform.localPosition = new Vector3(0, 0, OptionSideDistance);
    m_OptionContainer.transform.localRotation = Quaternion.identity;
    m_OptionContainer.transform.localScale = Vector3.one;

    for (int i = 0; i < NumOptions; i++) {
      Option option = m_Options[i];
      GameObject side = GameObject.CreatePrimitive(PrimitiveType.Quad);
      m_Sides[i] = side;

      side.GetComponent<Collider>().enabled = false;
      side.transform.parent = m_OptionContainer.transform;
      side.transform.localRotation = Quaternion.Euler(0, OptionAngleDeltaDegrees * (i + 1), 0);
      side.transform.localPosition = side.transform.localRotation * new Vector3(0, 0, -OptionSideDistance);
      side.transform.localScale = Vector3.one;

      Renderer renderer = side.GetComponent<Renderer>();
      m_SideRenderers[i] = renderer;
      renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      renderer.receiveShadows = false;
      renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
      renderer.materials = GetComponent<Renderer>().materials;

      Material material = renderer.material;
      material.mainTexture = option.m_Texture;
      if (m_MaterialTint.HasValue) {
        material.SetColor("_Color", m_MaterialTint.Value);
      }
      foreach (string key in m_MaterialFloats.Keys) {
        material.SetFloat(key, m_MaterialFloats[key]);
      }
    }

    // Keep all these new objects in the same layer as the button.
    HierarchyUtils.RecursivelySetLayer(m_OptionContainer.transform, gameObject.layer);
  }

  override protected void SetMaterialColor(Color rColor) {
    base.SetMaterialColor(rColor);
    m_MaterialTint = rColor;

    if (m_Sides != null) {
      for (var i = 0; i < m_Sides.Length; i++) {
        m_SideRenderers[i].material.SetColor("_Color", rColor);
      }
    }
  }

  override protected void SetMaterialFloat(string name, float value) {
    base.SetMaterialFloat(name, value);
    m_MaterialFloats[name] = value;

    if (m_Sides != null) {
      for (var i = 0; i < m_Sides.Length; i++) {
        m_SideRenderers[i].material.SetFloat(name, value);
      }
    }
  }
}

}  // namespace TiltBrush
