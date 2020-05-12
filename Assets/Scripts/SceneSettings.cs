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
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush {

public class SceneSettings : MonoBehaviour {
  // A using() object that requests instant scene switches
  public class RequestInstantSceneSwitch : IDisposable {
    public RequestInstantSceneSwitch() {
      m_Instance.m_RequestInstantSceneSwitch += 1;
    }
    public void Dispose() {
      m_Instance.m_RequestInstantSceneSwitch -= 1;
    }
  }

  public static SceneSettings m_Instance;

  public event Action FogDensityChanged;
  public event Action FogColorChanged;
  public event Action GradientActiveChanged;
  public event Action SkyboxChanged;

  private enum TransitionState {
    FadingToBlack,
    FadingToScene,
    Scene
  }

  private class LightTransition {
    public Environment.Light m_CurrentValues = new Environment.Light();
    public Environment.Light m_InterimValues;
    public Environment.Light m_DesiredValues;

    public void LerpCurrentToOff(float fValue) {
      m_InterimValues.Color = Color.Lerp(m_CurrentValues.Color, Color.black, fValue);
      m_InterimValues.m_Range = Mathf.Lerp(m_CurrentValues.m_Range, 0.0f, fValue);
      m_InterimValues.m_SpotAngle = Mathf.Lerp(m_CurrentValues.m_SpotAngle, 1.0f, fValue);
    }

    public void LerpCurrentToDesired(float fValue) {
      m_InterimValues.Color = Color.Lerp(m_CurrentValues.Color, m_DesiredValues.Color, fValue);
      m_InterimValues.m_Range = Mathf.Lerp(m_CurrentValues.m_Range, m_DesiredValues.m_Range, fValue);
      m_InterimValues.m_SpotAngle = Mathf.Lerp(m_CurrentValues.m_SpotAngle, m_DesiredValues.m_SpotAngle, fValue);
    }
  }

  [SerializeField] private float m_TransitionSpeed;
  // TODO: make private when MakeValidScenePose moves into SceneSettings
  [SerializeField] public float m_HardBoundsRadiusMeters_SS;
  [SerializeField] private float m_ReflectionIntensityFallOff = 0.25f;
  [SerializeField] private Material m_SkyboxMaterial;

  public System.Action FadingToDesiredEnvironment;

  private string m_RoomGeometryName;
  private GameObject m_RoomGeometry;
  private GameObject m_RoomReverbZone;
  private List<Camera> m_Cameras;
  private float m_TeleportBoundsHalfWidth;
  private float m_ControllerXRayHeight;

  private TransitionState m_CurrentState;

  private Environment m_DesiredEnvironment;
  private Environment m_CurrentEnvironment;
  private bool m_SkipFade;
  private float m_TransitionValue;
  private Environment.RenderSettingsLite m_CurrentValues;
  private Environment.RenderSettingsLite m_InterimValues;
  private List<GameObject> m_EnvironmentObjectsLowLOD;
  private List<GameObject> m_EnvironmentObjectsHighLOD;

  private bool m_InhibitSceneReset;
  private bool m_HasCustomLights;

  private bool m_LoadingCustomEnvironment;
  private Color m_CustomFogColor;
  private float m_CustomFogDensity;
  private float m_CustomReflectionIntensity;
  private Color m_SkyColorA;
  private Color m_SkyColorB;
  private Quaternion m_GradientSkew = Quaternion.identity;

  private bool m_FadingOutGradient;
  private Color m_FadingSkyColorA;
  private Color m_FadingSkyColorB;

  private bool m_InGradient;

  private List<LightTransition> m_TransitionLights;
  private int m_RequestInstantSceneSwitch;

  public float HardBoundsRadiusMeters_SS {
    get {
      return App.UserConfig.Flags.UnlockScale ? m_HardBoundsRadiusMeters_SS * 10.0f :
          m_HardBoundsRadiusMeters_SS;
    }
  }

  public float TeleportBoundsHalfWidth {
    get { return m_TeleportBoundsHalfWidth * App.METERS_TO_UNITS; }
  }

  public float ControllerXRayHeight {
    get { return m_ControllerXRayHeight * App.METERS_TO_UNITS; }
  }

  public bool IsTransitioning {
    get { return m_CurrentState != TransitionState.Scene;  }
  }

  public Color SkyColorA {
    get { return m_SkyColorA; }
    set {
      m_SkyColorA = value;
      RenderSettings.skybox.SetColor("_ColorA", value);
      TriggerSkyboxChanged();
    }
  }

  public Color SkyColorB {
    get { return m_SkyColorB; }
    set {
      m_SkyColorB = value;
      RenderSettings.skybox.SetColor("_ColorB", value);
      TriggerSkyboxChanged();
    }
  }

  public Color FogColor {
    get { return (Color)m_CustomFogColor; }
    set {
      m_CustomFogColor = value;
      RenderSettings.fogColor = value;
      TriggerFogColorChanged();
    }
  }

  public float FogDensity {
    get { return m_CurrentValues.m_FogDensity; }
    set {
      m_CurrentValues.m_FogDensity = value;
      RenderSettings.fogDensity = value / App.Scene.Pose.scale;
      TriggerFogDensityChanged();
    }
  }

  public bool InGradient {
    get { return RenderSettings.skybox != null && m_InGradient; }
    set {
      if (value == InGradient) { return; }
      if (value) {
        TransitionToGradient();
      } else {
        RenderSettings.skybox = CurrentEnvironment.m_SkyboxMaterial;
        RenderSettings.fog = CurrentEnvironment.m_RenderSettings.m_FogEnabled;
      }
      m_InGradient = value;
      if (GradientActiveChanged != null) {
        GradientActiveChanged();
      }
    }
  }

  public Quaternion GradientOrientation {
    get { return m_GradientSkew; }
    set {
      m_GradientSkew = value;
      if (InGradient) {
        RenderSettings.skybox.SetVector("_GradientDirection",
           App.Scene.Pose.rotation * m_GradientSkew * Vector3.up);
      }
      TriggerSkyboxChanged();
    }
  }

  public float DefaultReflectionIntensity {
    get { return m_CurrentEnvironment.m_RenderSettings.m_ReflectionIntensity; }
  }

  public Environment CurrentEnvironment {
    get { return m_CurrentEnvironment; }
  }

  public bool EnvironmentChanged {
    get {
      if (m_CurrentEnvironment == null) { return false; }
      bool skyboxChanged = (m_InGradient && m_CurrentEnvironment.m_RenderSettings.m_SkyboxCubemap != null) ||
        m_CurrentEnvironment.m_SkyboxColorA != m_SkyColorA ||
        m_CurrentEnvironment.m_SkyboxColorB != m_SkyColorB ||
        m_GradientSkew != Quaternion.identity;
      return skyboxChanged ||
        m_CurrentEnvironment.m_RenderSettings.m_FogColor != RenderSettings.fogColor ||
        m_CurrentEnvironment.m_RenderSettings.m_FogDensity != FogDensity ||
        m_CurrentEnvironment.m_RenderSettings.m_ReflectionIntensity != RenderSettings.reflectionIntensity;
    }
  }

  public static bool IsLowLod(Transform xf) {
    var tags = xf.GetComponent<GeometryTags>();
    return tags ? tags.IsLowLod : false;
  }

  public static bool IsHighLod(Transform xf) {
    var tags = xf.GetComponent<GeometryTags>();
    return tags ? tags.IsHighLod : false;
  }

  public static bool ExcludeFromPolyExport(Transform xf) {
    var tags = xf.GetComponent<GeometryTags>();
    return tags ? tags.ExcludeFromPolyExport : false;
  }

  void Awake() {
    m_Instance = this;
    m_CurrentState = TransitionState.Scene;
    m_TransitionLights = new List<LightTransition>();
    m_RoomGeometryName = "";
    m_RoomGeometry = null;
    m_RoomReverbZone = null;
  }

  void Start() {
    if (m_Cameras == null) {
      m_Cameras = new List<Camera>();
    }

    Camera [] aSceneCameras = UnityEngine.Object.FindObjectsOfType<Camera>();
    for (int i = 0; i < aSceneCameras.Length; ++i) {
      if (aSceneCameras[i].tag != "Ignore") {
        m_Cameras.Add(aSceneCameras[i]);
      }
    }
  }

  public void SetCustomEnvironment(CustomEnvironment custom, Environment env) {
    m_LoadingCustomEnvironment = true;
    m_CustomFogColor = (Color)custom.FogColor;
    m_CustomFogDensity = custom.FogDensity;
    m_CustomReflectionIntensity = custom.ReflectionIntensity;

    bool hasCustomGradient = custom.GradientColors != null;
    if (hasCustomGradient) {
      m_SkyColorA = custom.GradientColors[0];
      m_SkyColorB = custom.GradientColors[1];
      m_GradientSkew = custom.GradientSkew;
    } else {
      m_SkyColorA = env.m_SkyboxColorA;
      m_SkyColorB = env.m_SkyboxColorB;
    }

    // Set InGradient after the colors have been defined.  This call sends a message to all those
    // registered to listen for gradient changes.
    InGradient = hasCustomGradient;
  }

  public void UpdateReflectionIntensity() {
    float h, s, v1, v2;
    Color.RGBToHSV(App.Scene.GetLight(0).color, out h, out s, out v1);
    if (v1 < m_ReflectionIntensityFallOff) {
      Color.RGBToHSV(App.Scene.GetLight(1).color, out h, out s, out v2);
      if (v2 < m_ReflectionIntensityFallOff) {
        // Both lights are dimmer than the fall off point.
        // Calculate reflection intensity based on the brighter of the two.
        float v = Mathf.Max(v1, v2);
        RenderSettings.reflectionIntensity =
          Mathf.Log(1 + v / m_ReflectionIntensityFallOff) *
          SceneSettings.m_Instance.DefaultReflectionIntensity;
        return;
      }
    }
    RenderSettings.reflectionIntensity = SceneSettings.m_Instance.DefaultReflectionIntensity;
  }

  void Update_FadingToBlack() {
    if (m_SkipFade) {
      m_TransitionValue = 1.0f;
    } else {
      m_TransitionValue += (m_TransitionSpeed * Time.deltaTime);
      m_TransitionValue = Mathf.Min(m_TransitionValue, 1.0f);
    }

    //fade out scene
    m_InterimValues.m_ClearColor = Color.Lerp(m_CurrentValues.m_ClearColor, Color.black, m_TransitionValue);
    m_InterimValues.m_AmbientColor = Color.Lerp(m_CurrentValues.m_AmbientColor, Color.black, m_TransitionValue);
    m_InterimValues.m_FogColor = Color.Lerp(m_CurrentValues.m_FogColor, Color.black, m_TransitionValue);
    m_InterimValues.m_ReflectionIntensity = Mathf.Lerp(m_CurrentValues.m_ReflectionIntensity, 0.0f, m_TransitionValue);
    m_InterimValues.m_SkyboxTint = Color.Lerp(m_CurrentValues.m_SkyboxTint, Color.black, m_TransitionValue);

    // fade out skybox
    if (RenderSettings.skybox) {
      RenderSettings.skybox.SetColor("_Tint", m_InterimValues.m_SkyboxTint);
      RenderSettings.skybox.SetColor("_ColorA", Color.Lerp(m_FadingSkyColorA, Color.black, m_TransitionValue));
      RenderSettings.skybox.SetColor("_ColorB", Color.Lerp(m_FadingSkyColorB, Color.black, m_TransitionValue));
    }

    //fade out lights
    for (int i = 0; i < m_TransitionLights.Count; ++i) {
      m_TransitionLights[i].LerpCurrentToOff(m_TransitionValue);
    }

    //fade out custom shader values
    Shader.SetGlobalFloat("_SceneFadeAmount", 1.0f - m_TransitionValue);

    if (m_TransitionValue >= 1.0f) {
      Transition_FadingBlackToFadingScene();
    }
  }

  void Transition_FadingBlackToFadingScene() {
    Environment.RenderSettingsLite rDesired = m_DesiredEnvironment.m_RenderSettings;

    m_CurrentValues = m_InterimValues;
    m_InterimValues.m_FogEnabled = rDesired.m_FogEnabled;
    m_InterimValues.m_FogMode = rDesired.m_FogMode;
    FogDensity = m_LoadingCustomEnvironment ? m_CustomFogDensity :
      rDesired.m_FogDensity;
    RenderSettings.fogStartDistance = rDesired.m_FogStartDistance;
    RenderSettings.fogEndDistance = rDesired.m_FogEndDistance;
    m_TransitionValue = 0.0f;

    //create as many lights as will be in the scene and prep them to fade in
    m_TransitionLights.Clear();
    List<Environment.Light> aLights = m_DesiredEnvironment.m_Lights;
    for (int i = 0; i < aLights.Count; ++i) {
      LightTransition rNewTransition = new LightTransition();
      rNewTransition.m_DesiredValues = aLights[i];
      rNewTransition.m_InterimValues.m_Type = aLights[i].m_Type;
      rNewTransition.m_InterimValues.m_ShadowsEnabled = aLights[i].m_ShadowsEnabled;
      rNewTransition.m_InterimValues.m_Position = aLights[i].m_Position;
      rNewTransition.m_InterimValues.m_Rotation = aLights[i].m_Rotation;
      if (m_HasCustomLights) {
        rNewTransition.m_DesiredValues.Color = LightsControlScript.m_Instance.m_EnvLights[i + 1].color;
        rNewTransition.m_DesiredValues.m_Rotation = LightsControlScript.m_Instance.m_EnvLights[i + 1].rotation;
        rNewTransition.m_InterimValues.m_Rotation = LightsControlScript.m_Instance.m_EnvLights[i + 1].rotation;
      }
      m_TransitionLights.Add(rNewTransition);
    }

    // Create geometry and reverb zone for new environment.
    CreateEnvironment(rDesired);

    // Set the reflection cubemap
    RenderSettings.customReflection = rDesired.m_ReflectionCubemap;
    if (!m_LoadingCustomEnvironment) {
      if (rDesired.m_SkyboxCubemap) {
        RenderSettings.skybox = m_DesiredEnvironment.m_SkyboxMaterial;
        RenderSettings.skybox.SetColor("_Tint", Color.black);
        RenderSettings.skybox.SetFloat("_Exposure", rDesired.m_SkyboxExposure);
        RenderSettings.skybox.SetTexture("_Tex", rDesired.m_SkyboxCubemap);
      } else {
        InGradient = true;
        if (!m_FadingOutGradient) {
          RenderSettings.skybox = Instantiate(m_SkyboxMaterial);
        }
        if (RenderSettings.skybox != null) {
          RenderSettings.skybox.SetVector("_GradientDirection", Vector3.up);
        }
      }
    } else {
      if (!m_InGradient) {
        RenderSettings.skybox = m_DesiredEnvironment.m_SkyboxMaterial;
        RenderSettings.skybox.SetColor("_Tint", Color.black);
        RenderSettings.skybox.SetFloat("_Exposure", rDesired.m_SkyboxExposure);
        RenderSettings.skybox.SetTexture("_Tex", rDesired.m_SkyboxCubemap);
      } else if (!m_FadingOutGradient) {
        InGradient = true;
      }
    }

    // Fire off messages that say 'everything changed!'
    TriggerFogDensityChanged();
    TriggerFogColorChanged();
    TriggerSkyboxChanged();

    m_TeleportBoundsHalfWidth = m_DesiredEnvironment.m_TeleportBoundsHalfWidth;
    m_ControllerXRayHeight = m_DesiredEnvironment.m_ControllerXRayHeight;

    if (!m_InhibitSceneReset) {
      // Ugh. Can we do something better than this?
      // Maybe ask the teleport tool to enforce proper bounds (since it knows how)
      // instead of teleporting back to the origin? Then we could kill the hacky
      // m_InhibitSceneReset API.
      App.Scene.Pose = TrTransform.identity;
    }

    WidgetManager.m_Instance.SetHomePosition(m_DesiredEnvironment.m_WidgetHome);

    m_FadingOutGradient = false;

    m_CurrentState = TransitionState.FadingToScene;
  }

  void Update_FadingToScene() {
    Environment.RenderSettingsLite rDesired = m_DesiredEnvironment.m_RenderSettings;

    if (m_HasCustomLights) {
      rDesired.m_AmbientColor = LightsControlScript.m_Instance.m_EnvLights[0].color;
    }

    if (m_SkipFade) {
      m_TransitionValue = 1.0f;
    } else {
      m_TransitionValue += (m_TransitionSpeed * Time.deltaTime);
      m_TransitionValue = Mathf.Min(m_TransitionValue, 1.0f);
    }

    //fade in scene
    m_InterimValues.m_ClearColor = Color.Lerp(m_CurrentValues.m_ClearColor, rDesired.m_ClearColor, m_TransitionValue);
    m_InterimValues.m_AmbientColor = Color.Lerp(m_CurrentValues.m_AmbientColor, rDesired.m_AmbientColor, m_TransitionValue);
    m_InterimValues.m_FogColor = Color.Lerp(m_CurrentValues.m_FogColor,
      m_LoadingCustomEnvironment ? m_CustomFogColor : rDesired.m_FogColor, m_TransitionValue);
    m_InterimValues.m_ReflectionIntensity = Mathf.Lerp(m_CurrentValues.m_ReflectionIntensity,
      m_LoadingCustomEnvironment ? m_CustomReflectionIntensity : rDesired.m_ReflectionIntensity, m_TransitionValue);
    m_InterimValues.m_SkyboxTint = Color.Lerp(m_CurrentValues.m_SkyboxTint, rDesired.m_SkyboxTint, m_TransitionValue);

    //fade in lights
    for (int i = 0; i < m_TransitionLights.Count; ++i) {
      m_TransitionLights[i].LerpCurrentToDesired(m_TransitionValue);
    }

    //fade in custom shader values
    Shader.SetGlobalFloat("_SceneFadeAmount", m_TransitionValue);

    //tint the skybox if it's valid
    if (RenderSettings.skybox) {
      RenderSettings.skybox.SetColor("_Tint", m_InterimValues.m_SkyboxTint);
      RenderSettings.skybox.SetColor("_ColorA", Color.Lerp(Color.black, m_SkyColorA, m_TransitionValue));
      RenderSettings.skybox.SetColor("_ColorB", Color.Lerp(Color.black, m_SkyColorB, m_TransitionValue));
    }

    if (m_TransitionValue >= 1.0f) {
      m_CurrentState = TransitionState.Scene;
      m_CurrentValues = rDesired;
      FogDensity = m_LoadingCustomEnvironment ? m_CustomFogDensity : rDesired.m_FogDensity;
      if (RenderSettings.skybox) {
        RenderSettings.skybox.SetColor("_ColorA", m_SkyColorA);
        RenderSettings.skybox.SetColor("_ColorB", m_SkyColorB);
      }
      m_LoadingCustomEnvironment = false;
      m_CurrentEnvironment = m_DesiredEnvironment;
    }
  }

  void Update() {
    var origState = m_CurrentState;

    switch (origState) {
    case TransitionState.FadingToBlack:
      Update_FadingToBlack();
      break;
    case TransitionState.FadingToScene:
      Update_FadingToScene();
      break;
    case TransitionState.Scene:
      m_HasCustomLights = false;
      break;
    }

    if (origState == TransitionState.FadingToBlack ||
        origState == TransitionState.FadingToScene) {
      // Set our current settings in to the scene
      for (int i = 0; i < m_Cameras.Count; ++i) {
        if (m_Cameras[i].gameObject.activeSelf) {
          m_Cameras[i].backgroundColor = m_InterimValues.m_ClearColor;
        }
      }

      RenderSettings.ambientSkyColor = m_InterimValues.m_AmbientColor;
      RenderSettings.fog = m_InterimValues.m_FogEnabled;
      RenderSettings.fogColor = m_InterimValues.m_FogColor;
      RenderSettings.fogMode = m_InterimValues.m_FogMode;
      RenderSettings.reflectionIntensity = m_InterimValues.m_ReflectionIntensity;
      RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;

      // Set our lights
      for (int i = 0; i < m_TransitionLights.Count; ++i) {
        Light rLight = App.Scene.GetLight(i);
        rLight.enabled = true;
        rLight.type = m_TransitionLights[i].m_InterimValues.m_Type;
        rLight.transform.localPosition = m_TransitionLights[i].m_InterimValues.m_Position;
        rLight.transform.localRotation = m_TransitionLights[i].m_InterimValues.m_Rotation;
        rLight.color = m_TransitionLights[i].m_InterimValues.Color;
        rLight.range = m_TransitionLights[i].m_InterimValues.m_Range;
        rLight.spotAngle = m_TransitionLights[i].m_InterimValues.m_SpotAngle;
        if (m_TransitionLights[i].m_InterimValues.m_ShadowsEnabled) {
          rLight.shadows = LightShadows.Hard;
        } else {
          rLight.shadows = LightShadows.None;
        }
      }

      if (m_CurrentState == TransitionState.Scene) {
        // Update the state of lights and environment upon completion of fade to scene.
        PanelManager.m_Instance.ExecuteOnPanel<LightsPanel>(x => x.Refresh());
      }
    } else {
      // Refresh fog density based on scale.
      FogDensity = FogDensity;
    }

    UpdateEnvironment();
  }

  void UpdateEnvironment() {
    // Check to see if we're in the environment bounds.
    Vector3 headPosition_GS = ViewpointScript.Head.position;
    Vector3 headPosition_SS = App.Scene.Pose.inverse * headPosition_GS;
    bool bHeadInsideEnvironment = headPosition_SS.magnitude < TeleportBoundsHalfWidth &&
        headPosition_SS.y > 0;

    if (bHeadInsideEnvironment) {
      SetActiveList(m_EnvironmentObjectsLowLOD, false);
      SetActiveList(m_EnvironmentObjectsHighLOD, true);
    } else {
      SetActiveList(m_EnvironmentObjectsLowLOD, true);
      SetActiveList(m_EnvironmentObjectsHighLOD, false);
    }
  }

  void SetActiveList(List<GameObject> list, bool bActive) {
    if (list == null) {
      return;
    }

    foreach (GameObject obj in list) {
      obj.SetActive(bActive);
    }
  }

  void CreateEnvironment(Environment.RenderSettingsLite env) {
    //change lightning environment if requested is different from current
    if (m_RoomGeometryName != env.m_EnvironmentPrefab) {
      Destroy(m_RoomGeometry);
      Destroy(m_RoomReverbZone);
      GameObject rNewEnvironment = Resources.Load<GameObject>(env.m_EnvironmentPrefab);
      if (rNewEnvironment != null) {
        m_RoomGeometry = Instantiate(rNewEnvironment);
        Transform t = m_RoomGeometry.transform;
        Debug.Assert(TrTransform.FromTransform(t) == TrTransform.identity);
        t.SetParent(App.Instance.m_EnvironmentTransform, false);

        // Create reverb zone but do not set its parent.  It should live in room space.
        GameObject rNewReverbZone = Resources.Load<GameObject>(env.m_EnvironmentReverbZonePrefab);
        m_RoomReverbZone = Instantiate(rNewReverbZone);

        // Construct lists of objects that are only in low or only in high LOD.
        m_EnvironmentObjectsLowLOD = new List<GameObject>();
        m_EnvironmentObjectsHighLOD = new List<GameObject>();
        foreach (Transform child in m_RoomGeometry.transform) {
          if (IsLowLod(child)) {
            m_EnvironmentObjectsLowLOD.Add(child.gameObject);
          } else if (IsHighLod(child)) {
            m_EnvironmentObjectsHighLOD.Add(child.gameObject);
          }
        }
      } else {
        // Clear LOD lists and room geo reference
        m_RoomGeometry = null;
        m_RoomReverbZone = null;
        m_EnvironmentObjectsLowLOD = new List<GameObject>();
        m_EnvironmentObjectsHighLOD = new List<GameObject>();
      }
      // for b/37256058: crash seems to happen between this and the intro sketch load
      System.Console.WriteLine("Setenv: Unload 1");
      Resources.UnloadUnusedAssets();
      System.Console.WriteLine("Setenv: Unload 2");
      m_RoomGeometryName = env.m_EnvironmentPrefab;
    }
  }

  public void RecordSkyColorsForFading() {
    m_FadingOutGradient = m_InGradient;
    m_FadingSkyColorA = m_SkyColorA;
    m_FadingSkyColorB = m_SkyColorB;
  }

  /// If forceTransition, perform the fade-to-black-and-back even if the
  /// requested environment is the same.
  /// If keepTransforms, don't reset the canvas or scene transforms.
  public void SetDesiredPreset(
      Environment env,
      bool forceTransition=false,
      bool keepSceneTransform=false,
      bool hasCustomLights=false,
      bool skipFade=false) {
    m_SkipFade = skipFade;
    if (m_RequestInstantSceneSwitch > 0) {
      m_SkipFade = true;
    }
    bool bEnvironmentModified =
      LightsControlScript.m_Instance.LightsChanged || SceneSettings.m_Instance.EnvironmentChanged;
    if (env == null) {
      Debug.Log("null environment");
    } else if (env == m_DesiredEnvironment && !bEnvironmentModified &&
               !hasCustomLights && !m_LoadingCustomEnvironment && !forceTransition) {
      // same environment and lights not changed; but make sure we inhibit scene reset if requested
      m_InhibitSceneReset = keepSceneTransform;
    } else {
      m_HasCustomLights = hasCustomLights;

      if (!m_LoadingCustomEnvironment) {
        InGradient = (CurrentEnvironment == null) ? false :
            (env.m_RenderSettings.m_SkyboxCubemap == null);
        m_CustomFogColor = env.m_RenderSettings.m_FogColor;
        m_SkyColorA = env.m_SkyboxColorA;
        m_SkyColorB = env.m_SkyboxColorB;
        m_GradientSkew = Quaternion.identity;
      }

      m_DesiredEnvironment = env;
      m_CurrentValues = m_InterimValues;
      m_TransitionValue = 0.0f;
      m_CurrentState = TransitionState.FadingToBlack;
      m_InhibitSceneReset = keepSceneTransform;

      if (FadingToDesiredEnvironment != null) {
        FadingToDesiredEnvironment();
      }
    }
  }

  public Environment GetDesiredPreset() {
    return m_DesiredEnvironment;
  }

  // Register a new camera with scene settings
  public void RegisterCamera(Camera rCamera) {
    if (m_DesiredEnvironment != null) {
      rCamera.backgroundColor = m_DesiredEnvironment.m_RenderSettings.m_ClearColor;
    }

    if (m_Cameras != null) {
      m_Cameras.Add(rCamera);
    }
  }

  public Color GetContrastColor() {
    Color environmentColor = m_DesiredEnvironment.m_RenderSettings.m_ClearColor;
    return environmentColor.grayscale > 0.5f ? Color.black : Color.white;
  }

  public void TransitionToGradient() {
    if (!m_FadingOutGradient) {
      RenderSettings.skybox = Instantiate(m_SkyboxMaterial);
    }
    if (m_CurrentState == TransitionState.Scene) {
      RenderSettings.skybox.SetColor("_ColorA", m_SkyColorA);
      RenderSettings.skybox.SetColor("_ColorB", m_SkyColorB);
    }
    RenderSettings.skybox.SetVector("_GradientDirection", Vector3.up);
    RenderSettings.fog = true;
  }

  public CustomEnvironment CustomEnvironment {
    get {
      return !EnvironmentChanged ? null :
        new CustomEnvironment {
          GradientColors = m_InGradient ?
            new [] { (Color32)m_SkyColorA, (Color32)m_SkyColorB } : null,
          GradientSkew = m_GradientSkew,
          FogColor = (Color32)RenderSettings.fogColor,
          FogDensity = SceneSettings.m_Instance.FogDensity,
          ReflectionIntensity = RenderSettings.reflectionIntensity
        };
    }
  }

  public Color GetColor(BackdropButton.ColorMode mode) {
    switch (mode) {
    case BackdropButton.ColorMode.SkyColorA:
      return m_SkyColorA;
    case BackdropButton.ColorMode.SkyColorB:
      return m_SkyColorB;
    case BackdropButton.ColorMode.Fog:
      return m_CustomFogColor;
    }
    // Should never reach this
    throw new System.ArgumentException("Invalid color mode");
  }

  void TriggerFogDensityChanged() {
    if (FogDensityChanged != null) {
      FogDensityChanged();
    }
  }

  void TriggerFogColorChanged() {
    if (FogColorChanged != null) {
      FogColorChanged();
    }
  }

  void TriggerSkyboxChanged() {
    if (SkyboxChanged != null) {
      SkyboxChanged();
    }
  }
}
}  // namespace TiltBrush
