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
public class LightGizmo : MonoBehaviour {
  enum DragState {
    None,
    Hover,
    Drag
  }

  [SerializeField] private Renderer m_ColorIndicator;
  [SerializeField] private Collider m_BroadCollider;
  [SerializeField] private Collider m_Collider;
  [SerializeField] private GameObject m_LightMesh;
  [SerializeField] private Renderer[] m_TintableMeshes;
  [SerializeField] private Transform[] m_HighlightMeshXfs;
  [SerializeField] private float m_DampenDeadRadius = .025f;
  [SerializeField] private float m_DampenRadius = .2f;
  [SerializeField] private float m_DampenExp = 2.0f;
  private Mesh[] m_HighlightMeshes;
  private int m_LightID;
  private Light m_Light;
  private Quaternion m_Rotation_SS;
  private DragState m_State;
  private TrTransform m_BaseDragXf_LS;
  private LightsPanel m_ParentPanel;

  public Collider BroadCollider { get { return m_BroadCollider; } }
  public Collider Collider { get { return m_Collider; } }
  public bool Visible { set { m_LightMesh.SetActive(value); } }
  public bool IsBeingDragged { get { return m_State == DragState.Drag; } }
  public bool IsBeingHovered { get { return m_State == DragState.Hover; } }

  public void SetParentPanel(LightsPanel panel) {
    Debug.AssertFormat(m_ParentPanel == null, "Light Gizmo parent already set!");
    m_ParentPanel = panel;
    App.Scene.PoseChanged += (TrTransform prev, TrTransform current) => {
      transform.rotation = current.rotation * m_Rotation_SS;
      transform.position = m_ParentPanel.LightWidgetPosition(transform.rotation);
    };
  }

  void Awake() {
    if (m_HighlightMeshXfs != null) {
      m_HighlightMeshes = new Mesh[m_HighlightMeshXfs.Length];
      for (int i = 0; i < m_HighlightMeshXfs.Length; i++) {
        MeshFilter meshFilter = m_HighlightMeshXfs[i].GetComponent<MeshFilter>();
        if (meshFilter) {
          m_HighlightMeshes[i] = meshFilter.mesh;
        }
      }
    }
  }

  // effectively the initializer
  public Color SetLight(int ID) {
    m_Light = App.Scene.GetLight(ID);
    m_Light.enabled = true;
    m_LightID = ID;

    transform.rotation = m_Light.transform.rotation;
    m_Rotation_SS = App.Scene.AsScene[transform].rotation;
    m_Light.transform.position = transform.position;
    SetColor(m_Light.color);
    return m_Light.color;
  }

  public virtual void SetColor(Color color) {
    m_Light.color = color;
    m_ColorIndicator.material.SetColor("_ClampedColor",
        ColorPickerUtils.ClampColorIntensityToLdr(m_Light.color));
    m_ColorIndicator.material.SetColor("_TrueColor", color);
  }

  public Color GetColor() {
    return m_Light.color;
  }

  public void UpdateTint() {
    if (m_TintableMeshes != null) {
      Color rMatColor = GrabWidget.InactiveGrey;
      switch (m_State) {
      case DragState.Hover:
        rMatColor = Color.white;
        break;
      case DragState.Drag:
        rMatColor = PointerManager.m_Instance.MainPointer.GetCurrentColor();
        break;
      }
      for (int i = 0; i < m_TintableMeshes.Length; ++i) {
        m_TintableMeshes[i].material.color = rMatColor;
      }
    }
  }

  public bool UpdateDragState(bool hit, bool activate) {
    switch (m_State) {
    case DragState.None:
      if (hit) {
        m_State = DragState.Hover;
        UpdateTint();
        InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.03f);
      }
      break;
    case DragState.Hover:
      if (!hit) {
        m_State = DragState.None;
        UpdateTint();
        InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.02f);
      } else if (activate) {
        m_BaseDragXf_LS =
            Coords.AsGlobal[InputManager.Brush.Transform].inverse * Coords.AsGlobal[transform];
        m_State = DragState.Drag;
        UpdateTint();
      }
      break;
    case DragState.Drag:
      if (!activate) {
        m_State = DragState.Hover;
        SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ModifyLightCommand(
          (LightMode)m_LightID, m_Light.color, m_Light.transform.localRotation, final: true));
        UpdateTint();
      } else {
        LightsControlScript.m_Instance.DiscoMode = false;
        var controllerXf = Coords.AsGlobal[InputManager.Brush.Transform];
        var newXf = controllerXf * m_BaseDragXf_LS;

        // Dampen the rotation near the center as it changes too rapidly with small movements.
        float dist = (m_ParentPanel.PreviewCenter - newXf.translation).magnitude;
        Quaternion newRot =
          Quaternion.LookRotation(m_ParentPanel.PreviewCenter - newXf.translation);
        if (dist > m_DampenDeadRadius) {
          if (dist < m_DampenRadius) {
            float t = Mathf.Clamp01(Mathf.Pow(dist / m_DampenRadius, m_DampenExp));
            newRot = Quaternion.Slerp(transform.rotation, newRot, t);
          }
          SketchMemoryScript.m_Instance.PerformAndRecordCommand(
            new ModifyLightCommand((LightMode)m_LightID, m_Light.color,
            Quaternion.Inverse(App.Scene.Pose.rotation) * newRot));
        }
      }
      break;
    }
    return hit;
  }

  public void SetRotation(Quaternion rotation_SS) {
    m_Rotation_SS = rotation_SS;
    m_Light.transform.localRotation = rotation_SS;
    UpdateTransform();
  }

  public void UpdateTransform() {
    // This is effectively called from LightsPanel.Start, which is before the gizmos
    // are entirely initialized and m_Light is null.
    // This is a patch fix.
    if (m_Light != null && m_ParentPanel != null) {
      Quaternion rotation = m_Light.transform.rotation;
      transform.position = m_ParentPanel.LightWidgetPosition(rotation);
      transform.rotation =
        Quaternion.LookRotation(rotation * Vector3.forward,
          (ViewpointScript.Head.position - transform.position).normalized);
    }
  }
}
} // namespace TiltBrush
