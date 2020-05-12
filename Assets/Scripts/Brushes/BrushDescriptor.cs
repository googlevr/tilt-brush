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
using System.IO;
using UnityEngine;

namespace TiltBrush {

/// Authored data shared by all brushes.
public class BrushDescriptor : ScriptableObject, IExportableMaterial {
  const string EXPORT_TEXTURE_DIR = "Support/ExportTextures";
  const string EXPORT_TEXTURE_EXTENSION = ".png";

  /// Use this attribute on fields that are stored as a string guid, but
  /// which want user-friendly UI that looks like a BrushDescriptor.
  /// One reason to use a string rather than a direct reference is
  /// to keep Unity from forcing that asset to be included in a build.
  public class AsStringGuidAttribute : PropertyAttribute { }

  static private ExportGlTF.ExportManifest sm_gltfManifest;
  static public ExportGlTF.ExportManifest GltfManifest {
    get {
      if (sm_gltfManifest == null) {

        var deserializer = new Newtonsoft.Json.JsonSerializer();
        deserializer.ContractResolver = new CustomJsonContractResolver();
        string manifestPath = Path.Combine(App.SupportPath(), "exportManifest.json");
        using (var reader = new Newtonsoft.Json.JsonTextReader(new StreamReader(manifestPath))) {
          sm_gltfManifest = deserializer.Deserialize<ExportGlTF.ExportManifest>(reader);
        }
      }
      return sm_gltfManifest;
    }
  }

  [Header("Identity")]
  [DisabledProperty]
  public SerializableGuid m_Guid;

  [DisabledProperty]
  [Tooltip("A human readable name that cannot change, but is not guaranteed to be unique.")]
  public string m_DurableName;

  // TODO: change this to m_FirstReleasedVersion
  [DisabledProperty]
  public string m_CreationVersion;

  [DisabledProperty]
  [Tooltip("Set to the current version of Tilt Brush when making non-compatible changes")]
  public string m_ShaderVersion = "10.0";

  [DisabledProperty]
  public GameObject m_BrushPrefab;
  [Tooltip("Set to true if brush should not be checked for save/load determinism")]
  public bool m_Nondeterministic;

  [Tooltip("When upgrading a brush, populate this field with the prior version")]
  public BrushDescriptor m_Supersedes;
  // The reverse link to m_Supersedes; filled in on startup.
  [NonSerialized]
  public BrushDescriptor m_SupersededBy;

  [Tooltip("True if this brush looks identical to the version it supersedes. Causes brush to be silently-upgraded on load, and silently-downgraded to the maximally-compatible version on save")]
  public bool m_LooksIdentical = false;

  [Header("GUI")]
  public Texture2D m_ButtonTexture;
  [Tooltip("Name of the brush, in the UI and elsewhere")]
  public string m_Description;
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
  [Tooltip("Optional, experimental-only information about the brush")]
  public string m_DescriptionExtra;
#endif
  [System.NonSerialized] public bool m_HiddenInGui = false;

  [Header("Audio")]
  public AudioClip[] m_BrushAudioLayers;
  public float m_BrushAudioBasePitch;
  public float m_BrushAudioMaxPitchShift = 0.05f;
  public float m_BrushAudioMaxVolume;
  public float m_BrushVolumeUpSpeed = 4f;
  public float m_BrushVolumeDownSpeed = 4f;
  public float m_VolumeVelocityRangeMultiplier = 1f;
  public bool m_AudioReactive;  // whether we should show the audio reactive icon on the brush page
  public AudioClip m_ButtonAudio;

  [Header("Material")]
  [SerializeField] private Material m_Material;
  // Number of atlas textures in the V direction
  public int m_TextureAtlasV;
  public float m_TileRate;
  public bool m_UseBloomSwatchOnColorPicker;

  [Header("Size")]
  [DisabledProperty]
  [Vec2AsRange(LowerBound=0, Slider=false)]
  public Vector2 m_BrushSizeRange;
  [DisabledProperty]
  [Vec2AsRange(LowerBound=0, Slider=false, HideMax=true)]
  [SerializeField]
  private Vector2 m_PressureSizeRange = new Vector2(.1f, 1f);
  public float m_SizeVariance; // Used by particle and spray brushes.
  [Range(.001f, 1)]
  public float m_PreviewPressureSizeMin = .001f;

  [Header("Color")]
  public float m_Opacity;
  [Vec2AsRange(LowerBound=0, UpperBound=1)]
  public Vector2 m_PressureOpacityRange;
  [Range(0, 1)] public float m_ColorLuminanceMin;
  [Range(0, 1)] public float m_ColorSaturationMax;

  [Header("Particle")]
  public float m_ParticleSpeed;
  public float m_ParticleRate;
  public float m_ParticleInitialRotationRange;
  public bool m_RandomizeAlpha;

  // To be removed!
  [Header("QuadBatch")]
  public float m_SprayRateMultiplier;
  public float m_RotationVariance;
  public float m_PositionVariance;
  public Vector2 m_SizeRatio;

  [Header("Geometry Brush")]
  public bool m_M11Compatibility;

  [Header("Tube")]
  // Want to add this to brush description but not obvious how to do it
  //public int m_VertsInClosedCircle = 9;
  // This is defined in pointer space
  public float m_SolidMinLengthMeters_PS =  0.002f;
  // Store radius in z component of uv0
  public bool m_TubeStoreRadiusInTexcoord0Z;

  [Header("Misc")]
  public bool m_RenderBackfaces;  // whether we should submit backfaces to renderer
  public bool m_BackIsInvisible;  // whether the backside is visible to the user
  public float m_BackfaceHueShift;
  public float m_BoundsPadding;  // amount to pad bounding box by in canvas space in meters

  [Tooltip("For particularly expensive geometry generation: do not incrementally play back the stroke.")]
  public bool m_PlayBackAtStrokeGranularity;

  [Header("Export Settings")]
  public ExportableMaterialBlendMode m_BlendMode;
  public float m_EmissiveFactor;
  public bool m_AllowExport = true;

  [Header("Simplification Settings")]
  public bool m_SupportsSimplification = true;
  public int m_HeadMinPoints = 1;
  public int m_HeadPointStep = 1;
  public int m_TailMinPoints = 1;
  public int m_TailPointStep = 1;
  public int m_MiddlePointStep = 0;

  // ===============================================================================================
  // BEGIN IExportableMaterial interface
  // ===============================================================================================

  public Guid UniqueName { get { return m_Guid; } }

  public string DurableName { get { return m_DurableName; } }

  public ExportableMaterialBlendMode BlendMode { get { return m_BlendMode; } }

  public float EmissiveFactor { get { return m_EmissiveFactor; } }

  public GeometryPool.VertexLayout VertexLayout {
    get {
      BaseBrushScript brush = m_BrushPrefab.GetComponent<BaseBrushScript>();
      if (brush == null) {
        throw new ApplicationException("BaseBrushScript not found for brush prefab");
      }
      return brush.GetVertexLayout(this);
    }
  }

  public bool HasExportTexture() {
    if (m_Material != null) {
      return m_Material.HasProperty("_MainTex") &&
          m_Material.mainTexture is Texture2D;
    }
    return false;
  }

  public string GetExportTextureFilename() {
#if UNITY_EDITOR
    return GetExportTextureFilenameEditor();
#else
    return GetExportTextureFilenameStandalone();
#endif
  }

  public bool SupportsDetailedMaterialInfo {
    get {
      return GltfManifest.brushes.ContainsKey(m_Guid);
    }
  }

  public string VertShaderUri {
    get {
      string brushName = ExportedBrush.folderName;
      return ComputeHostedUri(brushName, ExportedBrush.vertexShader);
    }
  }

  public string FragShaderUri {
    get {
      string brushName = ExportedBrush.folderName;
      return ComputeHostedUri(brushName, ExportedBrush.fragmentShader);
    }
  }

  public bool EnableCull {
    get { return ExportedBrush.enableCull; }
  }

  public string UriBase { get { return null; } }

  public Dictionary<string, string> TextureUris {
    get {
      string brushName = ExportedBrush.folderName;
      Dictionary<string, string> textureUris = new Dictionary<string, string>();
      foreach (var kvp in ExportedBrush.textures) {
        string textureUri = ComputeHostedUri(brushName, kvp.Value);
        textureUris.Add(kvp.Key, textureUri);
      }
      return textureUris;
    }
  }

  public Dictionary<string, Vector2> TextureSizes {
    get { return ExportedBrush.textureSizes; }
  }

  public Dictionary<string, float> FloatParams {
    get { return ExportedBrush.floatParams; }
  }

  public Dictionary<string, Vector3> VectorParams {
    get { return ExportedBrush.vectorParams; }
  }

  public Dictionary<string, Color> ColorParams {
    get { return ExportedBrush.colorParams; }
  }

  // Not part of the interface, but a helper used by our implementation
  // Can return null for experimental brushes
  private ExportGlTF.ExportedBrush ExportedBrush {
    get {
      ExportGlTF.ExportedBrush ret;
      if (! GltfManifest.brushes.TryGetValue(m_Guid, out ret)) {
        throw new InvalidOperationException("No detailed material info");
      }
      return ret;
    }
  }

  // ===============================================================================================
  // END IExportableMaterial interface
  // ===============================================================================================

  public bool NeedsStraightEdgeProxy {
    get {
      // Why is this a virtual API instead of data on the descriptor?
      BaseBrushScript brush = m_BrushPrefab.GetComponent<BaseBrushScript>();
      if (brush == null) {
        throw new ApplicationException("BaseBrushScript not found for brush prefab");
      }
      return brush.NeedsStraightEdgeProxy();
    }
  }

  /// Return non-instantiated material
  public Material Material {
    get {
      return m_Material;
    }
  }

  public override string ToString() {
    return string.Format("BrushDescriptor<{0} {1} {2}>", this.name, m_Description, m_Guid);
  }

  /// Forwarding property to ease Poly Toolkit code compat issues
  public GeometryPool.Semantic m_uv0Semantic {
    get {
      return VertexLayout.texcoord0.semantic;
    }
  }

  /// Forwarding property to ease Poly Toolkit code compat issues
  public GeometryPool.Semantic m_uv1Semantic {
    get {
      return VertexLayout.texcoord1.semantic;
    }
  }

  /// Forwarding property to ease Poly Toolkit code compat issues
  public bool m_bFbxExportNormalAsTexcoord1 {
    get {
      return VertexLayout.bFbxExportNormalAsTexcoord1;
    }
  }

  private const string kDefaultShaderPrefix = "Default-none";

  public float PressureSizeMin(bool previewMode) {
    return (previewMode ? m_PreviewPressureSizeMin : m_PressureSizeRange.x);
  }

  // Returns the full URI at which the given asset filename can be referenced.
  // The corresponding brushName must be passed in.
  // TODO: This should generalize somehow for IExportableMaterial.
  public static string ComputeHostedUri(string brushName, string filename) {
    const string kHostingPrefix = "https://www.tiltbrush.com/shaders/brushes/";
    if (filename.StartsWith(kDefaultShaderPrefix + "-")) {
      return kHostingPrefix + kDefaultShaderPrefix + "/" + filename;
    }
    if (!filename.StartsWith(brushName)) {
      throw new ApplicationException("Invalid asset format: " + filename);
    }
    return kHostingPrefix + brushName + "/" + filename;
  }

  // Crazy code that we need because we want these .png files to be included
  // as loose files in the build -- and also because in editor these loose
  // files are in the Assets/ directory, not in the Support/ directory.

  private string GetExportTextureFilenameStandalone() {
    // Can return anything here as long as it's unique.
    // Returning m_Guid.ToString() is safest, but also super ugly.
    // Returning m_Description is prettier, but not guaranteed to be unique.
    return Path.Combine(EXPORT_TEXTURE_DIR, m_Guid.ToString("D") + EXPORT_TEXTURE_EXTENSION);
  }

#if UNITY_EDITOR
  private string GetExportTextureFilenameEditor() {
    Debug.Assert(m_Material != null);
    Texture2D mainTex = (Texture2D)m_Material.mainTexture;
    if (mainTex != null) {
      // Kind of junky... this is because we hardcode this extension
      // it in GetExportTextureFilename
      var path = UnityEditor.AssetDatabase.GetAssetPath(mainTex);
      if (!path.EndsWith(EXPORT_TEXTURE_EXTENSION)) {
        throw new InvalidOperationException(string.Format(
           "{0} texture filetype ({1}) should be a '{2}'.",
           m_Description, path,EXPORT_TEXTURE_EXTENSION));
      }
      return path;
    } else {
      return null;
    }
  }

  internal IEnumerable<CopyRequest> CopyRequests {
    get {
      if (HasExportTexture()) {
        yield return new CopyRequest {
          source = GetExportTextureFilenameEditor(),
          dest = GetExportTextureFilenameStandalone(),
          // No export support on Android
          omitForAndroid = true
        };
      }
    }
  }
#endif
}

}  // namespace TiltBrush
