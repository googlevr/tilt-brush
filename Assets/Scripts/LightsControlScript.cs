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
public class LightsControlScript : MonoBehaviour {
  static public LightsControlScript m_Instance;
  public const int kLightCount = 2;
  public const float kRotationChangeEpsilon = .0001f;
  public CustomEnvLight[] m_EnvLights;

  [SerializeField] private List<Color> m_Colors;
  [SerializeField] private float m_BeatThreshold;
  [SerializeField] private float m_DriftSpeed = 1f;
  [SerializeField] private float m_JumpSpeed = 4f;
  [SerializeField] private float m_JumpInterval = 0.5f;
  private Movement m_MoveMode;
  private bool m_Jumping;
  private float m_JumpTimer;
  private float m_MoveTimer;
  private DiscoCurve[] m_DiscoCurves;

  public struct DiscoCurve {
    public Quaternion start;
    public Quaternion target;
    public Color startColor;
    public Color targetColor;
    public Color lerpColor;
  }

  enum Movement {
    Still,
    Drift
  }

  public bool DiscoMode {
    get { return m_MoveMode != Movement.Still; }
    set {
      if (value) {
        // Initialize disco curves.
        int numSceneLights = App.Scene.GetNumLights();
        for (int i = 0; i < Mathf.Min(numSceneLights, m_DiscoCurves.Length); ++i) {
          m_DiscoCurves[i].start = App.Scene.GetLight(i).transform.rotation;
          m_DiscoCurves[i].target = Random.rotation;
          m_DiscoCurves[i].startColor = m_Colors[Random.Range(0, m_Colors.Count)];
          m_DiscoCurves[i].targetColor = m_Colors[Random.Range(0, m_Colors.Count)];
        }
        m_MoveTimer = 0.0f;

        // Easter egg notification.
        OutputWindowScript.m_Instance.CreateInfoCardAtController(
            InputManager.ControllerName.Brush,
            "Disco Mode Unlocked", fPopScalar: 0.5f);
      }
      m_MoveMode = value ? Movement.Drift : Movement.Still;
    }
  }

  public bool LightsChanged {
    get {
      var env = SceneSettings.m_Instance.CurrentEnvironment;
      if (env == null) { return false; }

      var shadow = App.Scene.GetLight((int)LightMode.Shadow);
      var noShadow = App.Scene.GetLight((int)LightMode.NoShadow);
      bool colorChanged =
        RenderSettings.ambientLight != env.m_RenderSettings.m_AmbientColor ||
        env.m_Lights[(int)LightMode.Shadow].Color != shadow.color ||
        env.m_Lights[(int)LightMode.NoShadow].Color != noShadow.color;

      return colorChanged ||
        !IsLightRotationCloseEnough(
        env.m_Lights[(int)LightMode.Shadow].m_Rotation,
          App.Scene.AsScene[shadow.transform].rotation) ||
        !IsLightRotationCloseEnough(
          env.m_Lights[(int)LightMode.NoShadow].m_Rotation,
          App.Scene.AsScene[noShadow.transform].rotation);
    }
  }

  public static bool IsLightRotationCloseEnough(Quaternion a, Quaternion b) {
    return (a * Vector3.forward - b * Vector3.forward).magnitude < kRotationChangeEpsilon;
  }

  public struct CustomEnvLight {
    public Color color;
    public Quaternion rotation;
  }

  public CustomLights CustomLights {
    get {
      if (!LightsChanged) { return null; }
      var shadow = App.Scene.GetLight((int)LightMode.Shadow);
      var noShadow = App.Scene.GetLight((int)LightMode.NoShadow);
      return new CustomLights() {
        Ambient = RenderSettings.ambientLight,
        Shadow = new CustomLights.DirectionalLight() {
          Orientation = App.Scene.AsScene[shadow.transform].rotation,
          Color = shadow.enabled ? shadow.color : Color.black
        },
        NoShadow = new CustomLights.DirectionalLight() {
          Orientation = App.Scene.AsScene[noShadow.transform].rotation,
          Color = noShadow.enabled ? noShadow.color : Color.black
        }
      };
    }
    set {
      m_EnvLights = new CustomEnvLight[3];
      // Ambient light data is always at index 0.
      m_EnvLights[0].color = value.Ambient;
      RenderSettings.ambientLight = value.Ambient;

      // Shadow is EnvLights[1], No shadow is EnvLights[2]
      for (int i = 0; i < (int)LightMode.NumLights; i++) {
        CustomLights.DirectionalLight data =
          (LightMode)i == LightMode.Shadow ? value.Shadow : value.NoShadow;
        m_EnvLights[i + 1].color = data.Color;
        m_EnvLights[i + 1].rotation = data.Orientation;
      }
      // Note that these lights may disagree with our calculated reflection intensity value.
      // We ignore that here and will only enforce it if the light intensities are changed with.
    }
  }

  void Awake() {
    m_Instance = this;
    // Add 1 because we want to include the ambient light as well.
    int numLights = (int)LightMode.NumLights + 1;
    m_DiscoCurves = new DiscoCurve[numLights];
  }

  public void AddColor(Color c) {
    m_Colors.RemoveAt(0);
    m_Colors.Add(c);
  }

  void AdvanceCurves() {
    for (int i = 0; i < m_DiscoCurves.Length; ++i) {
      m_DiscoCurves[i].start = m_DiscoCurves[i].target;
      m_DiscoCurves[i].target = Random.rotation;

      m_DiscoCurves[i].startColor = m_DiscoCurves[i].targetColor;
      m_DiscoCurves[i].targetColor = m_Colors[Random.Range(0, m_Colors.Count)];
    }
  }

  void Update() {
    switch (m_MoveMode) {
    case Movement.Still:
      break;
    case Movement.Drift:
      m_JumpTimer -= Time.deltaTime;

      // Move forward.
      float speed = m_Jumping ? m_JumpSpeed : m_DriftSpeed;
      m_MoveTimer += Time.deltaTime * speed;
      if (m_MoveTimer >= 1.0f) {
        // If we're passed our threshold, udpate our curves.
        AdvanceCurves();
        m_MoveTimer -= 1.0f;
        m_Jumping = false;
      }

      // TODO: Fix this.
      // This assumes there is only one panel of type Lights created.
      BasePanel basePanel = PanelManager.m_Instance.GetPanelByType(BasePanel.PanelType.Lights);
      LightsPanel lightsPanel = basePanel as LightsPanel;
      if (lightsPanel) {
        for (int i = 0; i < m_DiscoCurves.Length; ++i) {
          m_DiscoCurves[i].lerpColor =
              Color.Lerp(m_DiscoCurves[i].startColor, m_DiscoCurves[i].targetColor, m_MoveTimer);

          var lightGizmo = lightsPanel.GetLight((LightMode)i);
          if (lightGizmo != null) {
            lightGizmo.SetRotation(
                Quaternion.Slerp(m_DiscoCurves[i].start, m_DiscoCurves[i].target, m_MoveTimer));
          }
        }

        lightsPanel.SetDiscoLights(m_DiscoCurves[0].lerpColor, m_DiscoCurves[1].lerpColor,
            m_DiscoCurves[2].lerpColor, noRecord: true);
      }

      // Speed to the next checkpoint if we're listening to music and we hit a threshold.
      if (m_JumpTimer < 0.0f && App.Instance.RequestingAudioReactiveMode) {
        if (VisualizerManager.m_Instance.BeatOutput.z > m_BeatThreshold) {
          m_Jumping = true;

          // Prevent jumping from happening too quickly, back to back.
          m_JumpTimer = m_JumpInterval;
        }
      }
      break;
    }
  }
}
} // namespace TiltBrush
