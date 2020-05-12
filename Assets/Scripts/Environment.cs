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
using System.Collections.Generic;

namespace TiltBrush {

[CreateAssetMenu(fileName="Environment", menuName="Tilt Brush Environment")]
public class Environment : ScriptableObject {
  [Serializable]
  public struct Light {
    [SerializeField] private Color m_Color;
    [SerializeField] private float m_Intensity;
    public Vector3 m_Position;
    public Quaternion m_Rotation;
    public LightType m_Type;
    public float m_Range;
    public float m_SpotAngle;
    public bool m_ShadowsEnabled;

    // Returns the possibly HDR color of the light.
    // Assignment of a color to this field should be after it is multiplied with the intensity.
    public Color Color {
      get {
        var color = m_Color * m_Intensity;
        color.a = 1.0f;
        return color;
      }
      set {
        m_Color = value;
        m_Intensity = 1.0f;
      }
    }
  }

  // A subset of UnityEngine.RenderSettings; just the stuff we need
  [Serializable]
  public struct RenderSettingsLite {
    public bool m_FogEnabled;
    public Color m_FogColor;
    public float m_FogDensity;
    public float m_FogStartDistance;
    public float m_FogEndDistance;
    // Our Environment assets only use Exponential.
    // If you change this, also modify Editor/ShaderStripping.cs
    public FogMode m_FogMode {
      get { return FogMode.Exponential; }
      set {
        if (value != FogMode.Exponential) {
          Debug.LogWarningFormat("Ignoring RenderSettingsLite.FogMode = {0}", value);
        }
      }
    }
    public Color m_ClearColor;
    public Color m_AmbientColor;
    public float m_SkyboxExposure;
    public Color m_SkyboxTint;
    /// Name of a GameObject asset in Resources/EnvironmentPrefabs/
    /// This is _not_ a GameObject because we don't want its assets
    /// always loaded. Assets are loaded dynamically.
    public string m_EnvironmentPrefab;
    public string m_EnvironmentReverbZonePrefab;
    public Cubemap m_SkyboxCubemap;
    public Cubemap m_ReflectionCubemap;
    public float m_ReflectionIntensity;
  }

  public static void SetRenderSettings(RenderSettingsLite src) {
    RenderSettings.fog = src.m_FogEnabled;
    RenderSettings.fogColor = src.m_FogColor;
    RenderSettings.fogDensity = src.m_FogDensity;
    RenderSettings.fogStartDistance = src.m_FogStartDistance;
    RenderSettings.fogEndDistance = src.m_FogEndDistance;
    RenderSettings.fogMode = src.m_FogMode;
    // Ignore .m_ClearColor
    RenderSettings.ambientSkyColor = src.m_AmbientColor;
    // Ignore .m_SkyboxExposure
    // Ignore .m_SkyboxTint
    // Ignore .m_EnvironmentPrefab
    // Ignore .m_EnvironmentReverbZonePrefab
    // Ignore .m_SkyboxCubemap
    RenderSettings.customReflection = src.m_ReflectionCubemap;
    RenderSettings.reflectionIntensity = src.m_ReflectionIntensity;
  }

  public static RenderSettingsLite GetRenderSettings() {
    RenderSettingsLite dst = new RenderSettingsLite();
    dst.m_FogEnabled = RenderSettings.fog;
    dst.m_FogColor = RenderSettings.fogColor;
    dst.m_FogDensity = RenderSettings.fogDensity;
    dst.m_FogStartDistance = RenderSettings.fogStartDistance;
    dst.m_FogEndDistance = RenderSettings.fogEndDistance;
    dst.m_FogMode = RenderSettings.fogMode;
    // Ignore .m_ClearColor
    dst.m_AmbientColor = RenderSettings.ambientSkyColor;
    // Ignore .m_SkyboxExposure
    // Ignore .m_SkyboxTint
    // Ignore .m_EnvironmentPrefab
    // Ignore .m_EnvironmentReverbZonePrefab
    // Ignore .m_SkyboxCubemap
    dst.m_ReflectionCubemap = RenderSettings.customReflection;
    dst.m_ReflectionIntensity = RenderSettings.reflectionIntensity;

    return dst;
  }

  public SerializableGuid m_Guid;
  public string m_Description;
  public Texture2D m_IconTexture;
  public RenderSettingsLite m_RenderSettings;
  public List<Light> m_Lights;
  public float m_TeleportBoundsHalfWidth = 15.0f;
  public float m_ControllerXRayHeight = -999999999.0f;
  [Tooltip("Home position for snappable widgets")]
  public Vector3 m_WidgetHome = Vector3.zero;

  private Material m_DerivedSkyboxMaterial;
  /// The settings on this material mostly do not matter, and are left alone.
  /// When lerping between presets, _Tint, _Exposure, and _Tex are mutated
  /// based on values in the Environment.
  public Material m_SkyboxMaterial {
    get {
      if (m_DerivedSkyboxMaterial == null) {
        m_DerivedSkyboxMaterial = Instantiate(EnvironmentCatalog.m_Instance.m_SkyboxMaterial);
      }
      return m_DerivedSkyboxMaterial;
    }
  }

  public Color m_SkyboxColorA;
  public Color m_SkyboxColorB;
}

} // namespace TiltBrush
