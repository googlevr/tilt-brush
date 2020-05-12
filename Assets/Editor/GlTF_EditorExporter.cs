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
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;

using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace TiltBrush {

// Provides menu item under Tilt/ for exporting brush strokes.
public static class GlTFEditorExporter {
  /// For b/37499109: downsample textures which have a dimension >= this
  const int kLargeTextureThreshold = 1024;

  /// A list of requests to export textures, where "export" means "copy+downsample"
  [Serializable]
  public class ExportRequests {
    public List<ExportRequest> exports = new List<ExportRequest>();
  }

  [Serializable]
  [UsedImplicitly(ImplicitUseTargetFlags.Members)]
  public class ExportRequest {
    public string source;
    public string destination;
    public float desiredWidth;
    public float desiredHeight;
    public bool isBump;
  }

  class ExportException : Exception {
    public ExportException(string message)
      : base(message) {}
    public ExportException(string fmt, params object[] args)
      : base(string.Format(fmt, args)) {}
  }

  private static string GetExportBaseName() {
    var current = SaveLoadScript.m_Instance.SceneFile;
    string basename = (current.Valid)
        ? Path.GetFileNameWithoutExtension(current.FullPath).Replace(" ", "_")
        : "Untitled";

    string directoryName = FileUtils.GenerateNonexistentFilename(
        App.UserExportPath(), basename, "");
    if (!FileUtils.InitializeDirectoryWithUserError(directoryName, "Failed to export")) {
      throw new InvalidOperationException("Directory full (?!)");
    }
    return Path.Combine(directoryName, basename);
  }

  [MenuItem("Tilt/glTF/Sync Export Materials", false, 1)]
  private static void SyncExportMaterials() {
    string projectPath = Path.GetDirectoryName(Application.dataPath);
    string manifestPath = Path.Combine(projectPath, "Support/exportManifest.json");
    string exportRoot = Path.Combine(projectPath, ExportUtils.kProjectRelativeBrushExportRoot);
    var exportRequests = WriteManifest(manifestPath, exportRoot);
    ExportTextures(exportRequests);
    GenerateShaders(manifestPath, exportRoot);
  }

  [MenuItem("Tilt/glTF/Export Brush Strokes to glTF v1", false, 2)]
  private static void ExportBrushStrokes_gltf1() {
    new ExportGlTF().ExportBrushStrokes(
        GetExportBaseName() + ".gltf",
        AxisConvention.kGltfAccordingToPoly,
        binary: false,
        doExtras: true,
        gltfVersion: 1,
        includeLocalMediaContent: false);
  }

  [MenuItem("Tilt/glTF/Export Brush Strokes to glb v1", false, 3)]
  private static void ExportBrushStrokes_glb1() {
    new ExportGlTF().ExportBrushStrokes(
        GetExportBaseName() + ".glb1",
        AxisConvention.kGltf2,
        binary: true,
        doExtras: false,
        gltfVersion: 1,
        includeLocalMediaContent: true);
  }

  [MenuItem("Tilt/glTF/Export Brush Strokes to glTF v1", true)]
  [MenuItem("Tilt/glTF/Export Brush Strokes to glb v1", true)]
  private static bool ExportBrushStrokes_Enabled() {
    return Application.isPlaying;
  }

  [MenuItem("Tilt/glTF/Export Environments to glTF", false, 4)]
  private static void ExportEnvironments() {
#if !GAMEOBJ_EXPORT_TO_GLTF
    Debug.LogError("Enable the define and fix up the code");
#else
    // Save the original RenderSettings
    Environment.RenderSettingsLite originalRenderSettings = Environment.GetRenderSettings();

    // Clear out the existing environments directory to do a clean export
    string projectPath = Path.GetDirectoryName(Application.dataPath);
    string environmentExportPath = Path.Combine(projectPath,
                                                ExportUtils.kProjectRelativeEnvironmentExportRoot);
    try {
      Directory.Delete(environmentExportPath, recursive: true);
    } catch (DirectoryNotFoundException) {
      // It's okay if this directory doesn't exist yet as it will be created later.
    }

    // Clear out the existing textures directory to do a clean export
    string textureExportPath = Path.Combine(projectPath,
                                            ExportUtils.kProjectRelativeTextureExportRoot);
    try {
      Directory.Delete(textureExportPath, recursive: true);
    } catch (DirectoryNotFoundException) {
      // It's okay if this directory doesn't exist yet as it will be created later.
    }
    if (!FileUtils.InitializeDirectoryWithUserError(
        textureExportPath, "Failed to export, can't create texture export directory")) {
      return;
    }

    // Get the environment
    TiltBrushManifest manifest = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>("Assets/Manifest.asset");
    foreach (Environment env in manifest.Environments) {
      // Copy over the RenderSettings
      Environment.SetRenderSettings(env.m_RenderSettings);

      // Set up the environment
      string envGuid = env.m_Guid.ToString("D");
      Debug.LogFormat("Exporting environment: {0}", env.m_RenderSettings.m_EnvironmentPrefab);
      GameObject envPrefab = Resources.Load<GameObject>(env.m_RenderSettings.m_EnvironmentPrefab);
      GameObject envGameObject = UObject.Instantiate(envPrefab);
      envGameObject.name = envGuid;

      // Hide game objects that don't get exported to Poly.
      foreach (Transform child in envGameObject.transform) {
        if (SceneSettings.ExcludeFromPolyExport(child)) {
          child.gameObject.SetActive(false);
        }
      }

      // Set up the environment export directory
      string directoryName = Path.Combine(environmentExportPath, envGuid);
      if (!FileUtils.InitializeDirectoryWithUserError(
          directoryName, "Failed to export, can't create environment export directory")) {
        return;
      }

      string basename = FileUtils.SanitizeFilename(envGameObject.name);
      string gltfName = Path.Combine(directoryName, basename + ".gltf");

      var exporter = new ExportGlTF();
      exporter.ExportGameObject(envGameObject, gltfName, env);

      // DestroyImmediate is required because editor mode never runs object garbage collection.
      UObject.DestroyImmediate(envGameObject);
    }

    // Restore the original RenderSettings
    Environment.SetRenderSettings(originalRenderSettings);
#endif
  }

  private static Dictionary<Guid, BrushDescriptor> GetBrushes() {
    var cat = new Dictionary<Guid, BrushDescriptor>();
    // We don't export experimental brushes brushes in the live prod build.
    TiltBrushManifest productionManifest = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>(
        "Assets/Manifest.asset");

    foreach (BrushDescriptor desc in productionManifest.UniqueBrushes()) {
      cat.Add(desc.m_Guid, desc);
    }

    return cat;
  }

  // Tries to clean up identifiers for downstream clients.
  private static string SanitizeName(string name) {
    return name.TrimStart("_".ToCharArray());
  }

  /// Returns exit code
  private static int RunCommand(string command, params string[] commandArgs) {
    System.Diagnostics.Process proc = new System.Diagnostics.Process();
    proc.StartInfo = new System.Diagnostics.ProcessStartInfo(
        command, string.Join(" ", commandArgs));
    proc.StartInfo.RedirectStandardOutput = true;
    proc.StartInfo.RedirectStandardError = true;
    proc.StartInfo.UseShellExecute = false;
    proc.StartInfo.CreateNoWindow = true;

    // This receives output with the trailing \n stripped (which seems like a .net bug?)
    // Windows Python sends output with the \r intact
    StringBuilder stdout = new StringBuilder();
    proc.OutputDataReceived += (sender, args) => {
      if (! string.IsNullOrEmpty(args.Data)) {
        stdout.AppendLine(args.Data.Replace("\r", ""));
      }
    };

    StringBuilder stderr = new StringBuilder();
    proc.ErrorDataReceived += (sender, args) => {
      if (! string.IsNullOrEmpty(args.Data)) {
        stderr.AppendLine(args.Data.Replace("\r", ""));
      }
    };

    proc.Start();
    proc.BeginErrorReadLine();
    proc.BeginOutputReadLine();
    proc.WaitForExit();

    string s = stdout.ToString().Trim();
    if (!string.IsNullOrEmpty(s)) {
      Debug.Log(s);
    }

    string err = stderr.ToString().Trim();
    if (!string.IsNullOrEmpty(err)) {
      Debug.LogError(err);
    }

    if (proc.ExitCode != 0) {
      EditorUtility.DisplayDialog(
          "Command Failed", string.Format("Command: {1} {2}\n\nExit code: {0}\n\nOutput: {3}",
          proc.ExitCode, command, string.Join(" ", commandArgs), err), "OK");
    }
    return proc.ExitCode;
  }

  // Input: the exportRequests
  // Output: downsampled textures, to subdirectories of exportRoot
  private static void ExportTextures(ExportRequests exportRequests) {
    string projectPath = Path.GetDirectoryName(Application.dataPath);
    string requestsJson = Path.Combine(projectPath, "Temp/ExportRequests.json");
    var serializer = new JsonSerializer();
    serializer.ContractResolver = new CustomJsonContractResolver();
    using (var writer = new CustomJsonWriter(new StreamWriter(requestsJson))) {
      writer.Formatting = Formatting.Indented;
      serializer.Serialize(writer, exportRequests);
    }

    string scriptPath = Path.Combine(projectPath, "Support/bin/gltf_export_textures.py");
    RunCommand("python", scriptPath, requestsJson);
  }

  // Input:
  //   ExportManifest in manifestPath
  // Ouptut:
  //   Shaders, to subdirectories of exportRoot
  private static void GenerateShaders(string manifestPath, string exportRoot) {
    string projectPath = Path.GetDirectoryName(Application.dataPath);
    string scriptPath = Path.Combine(projectPath, "Support/bin/gltf_export_shaders.py");
    RunCommand("python", scriptPath, manifestPath, exportRoot);
  }

  /// Fetches the shaderlab value for the specified tag, either "cull" or "gltfcull".
  /// Raises ExportException if there are zero or multiple values.
  private static bool? GetBackfaceCullValue(string filename, string tag) {
    if (!File.Exists(filename)) {
      throw new ExportException("Missing {0}", filename);
    }
    string shaderText = File.ReadAllText(filename);
    var matches = Regex.Matches(shaderText, @"\b"+tag+@" (\w+)", RegexOptions.IgnoreCase);
    var values = (from Match match in matches
                  select match.Groups[1].Value.ToLower()).ToList();
    values = new HashSet<string>(values).ToList();

    // Indeterminate cases
    if (values.Count == 0) {
      return null;
    } else if (values.Count > 1) {
      throw new ExportException(
          "{0}: Too many cull modes: {1}", filename, values);
    }

    string value = values[0];
    if (value == "off") {
      return false;
    } else if (value == "back") {
      return true;
    } else {
      throw new ExportException(
          "{0}: Unknown cull mode {1} is unsupported", filename, value);
    }
  }

  /// Returns true iff the gltf shader requires backface culling
  /// Raises ExportException if it can't be determined.
  private static bool GetEnableCull(BrushDescriptor descriptor) {
    string projectPath = Path.GetDirectoryName(Application.dataPath);
    string shaderPath = AssetDatabase.GetAssetPath(descriptor.Material.shader);
    if (shaderPath == null) {
      throw new ArgumentException("Cannot find Unity shader for brush {0}", descriptor.name);
    }
    shaderPath = Path.Combine(projectPath, shaderPath);
    bool? value = GetBackfaceCullValue(shaderPath, "gltfcull");
    if (value == null) {
      value = GetBackfaceCullValue(shaderPath, "cull");
    }
    if (value == null) {
      throw new ExportException(
          "Cannot find Cull or GltfCull in {0}", shaderPath);
    }
    return value.Value;
  }

  /// Add an export request to exportRequests.
  /// A follow-up process will actually do the export, which involves
  /// copying, downsampling, and/or filtering.
  static void AddExportTextureRequest(
      ExportGlTF.ExportedBrush exp,
      ExportRequests exportRequests,
      Texture tex, string texName, string exportRoot) {

    // Even if it weren't for b/37499109, we'd still want to do a downsample
    // for robustness; tex.width/height is not necessarily the size of the
    // source png.
    int desiredWidth = tex.width;
    int desiredHeight = tex.height;
    if (desiredWidth  >= kLargeTextureThreshold ||
        desiredHeight >= kLargeTextureThreshold) {
      desiredWidth = Mathf.Max(desiredWidth >> 1, 1);
      desiredHeight = Mathf.Max(desiredHeight >> 1, 1);
    }

    string projectRoot = Path.GetDirectoryName(Application.dataPath);
    string src = Path.Combine(projectRoot, UnityEditor.AssetDatabase.GetAssetPath(tex));
    string dstName = String.Format(
        "{0}-v{1}-{2}{3}",
        exp.folderName, exp.shaderVersion, texName, Path.GetExtension(src));
    exp.textures.Add(texName, dstName);
    exp.textureSizes.Add(texName, new Vector2(desiredWidth, desiredHeight));

    string dstDir = Path.Combine(exportRoot, exp.folderName);

    bool isBump; {
      string assetPath = AssetDatabase.GetAssetPath(tex);
      TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
      isBump = importer.convertToNormalmap;
    }
    exportRequests.exports.Add(
        new ExportRequest {
          source = src,
          destination = Path.Combine(dstDir, dstName),
          desiredWidth = desiredWidth,
          desiredHeight = desiredHeight,
          isBump = isBump
        });
  }

  /// Returns an ExportedBrush, and appends export requests to exportRequests.
  public static ExportGlTF.ExportedBrush ExportBrush(
      ExportRequests exportRequests,
      BrushDescriptor descriptor, string exportRoot) {
    if (string.IsNullOrEmpty(descriptor.m_DurableName)) {
      throw new ApplicationException(
          string.Format("Brush {0} has no DurableName", descriptor.name));
    }
    string brushFolderNameFmt = "{0}-{1}";
    string shaderFmt = "{0}-v{1}-{2}.glsl";

    var exp = new ExportGlTF.ExportedBrush();
    exp.name = descriptor.m_DurableName;
    exp.guid = descriptor.m_Guid;
    exp.folderName = string.Format(brushFolderNameFmt, exp.name, exp.guid.ToString("D"));
    exp.shaderVersion = descriptor.m_ShaderVersion;
    exp.vertexShader   = string.Format(shaderFmt, exp.folderName, exp.shaderVersion, "vertex");
    exp.fragmentShader = string.Format(shaderFmt, exp.folderName, exp.shaderVersion, "fragment");
    exp.enableCull = GetEnableCull(descriptor);
    exp.blendMode = descriptor.m_BlendMode;

    // And the material
    {
      Material material = descriptor.Material;
      Shader shader = material.shader;
      for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++) {
        string internalName = ShaderUtil.GetPropertyName(shader, i);
        int propId = Shader.PropertyToID(internalName);
        string propName = SanitizeName(internalName);

        switch (ShaderUtil.GetPropertyType(shader, i)) {
        case ShaderUtil.ShaderPropertyType.TexEnv:
          if (material.HasProperty(internalName) && material.GetTexture(propId) is Texture2D) {
            AddExportTextureRequest(
                exp, exportRequests, material.GetTexture(propId), propName, exportRoot);
          }
          break;
        case ShaderUtil.ShaderPropertyType.Color:
          exp.colorParams.Add(propName, material.GetColor(propId));
          break;
        case ShaderUtil.ShaderPropertyType.Range:
        case ShaderUtil.ShaderPropertyType.Float:
          float value = material.GetFloat(propId);
          if (propName == "ScrollJitterIntensity") {
            value *= App.UNITS_TO_METERS;
          }
          exp.floatParams.Add(propName, value);
          break;
        case ShaderUtil.ShaderPropertyType.Vector:
          Vector4 vec = material.GetVector(propId);
          if (propName == "ScrollDistance") {
            vec *= App.UNITS_TO_METERS;
            vec.z *= -1;
          }
          exp.vectorParams.Add(propName, vec);
          break;
        default:
          break;
        }
      }
    }

    // A bit of sanity-checking.
    bool expectCutoff = (exp.blendMode == ExportableMaterialBlendMode.AlphaMask);
    bool hasCutoff = exp.floatParams.ContainsKey("Cutoff");
    if (expectCutoff != hasCutoff) {
      if (expectCutoff) {
        Debug.LogWarning($"{descriptor.m_DurableName}: missing cutoff (or shouldn't be AlphaMask)",
                         descriptor);
      } else {
        // Some of these warnings are caused by materials which are AdditiveBlend but also have
        // alpha cutoff; but exp.blendMode can only represent one or the other. Unclear how
        // we can get this into a gltf material.
        // Some are caused by materials which don't use blending at all but still use a
        // shader that has cutoff math in it (eg hull, wire, icing). This is a bit of wasted
        // work at shading time.
        // Debug.LogWarning($"{descriptor.m_DurableName}: {exp.blendMode} but has cutoff",
        //                  descriptor);
      }
    }

    return exp;
  }

  // Exports all non-experimental brushes along with their material parameters
  // into a directory structure suitable for submission to
  // google3/googledata/html/external_content/tiltbrush.com/shaders/brushes
  //
  // Input:
  //   Non-experimental brushes (from Assets/Manifest.asset)
  //   Their materials and shaders
  //
  // Output:
  //   An ExportManifest, writen to manifestPath
  //   An ExportRequests instance
  //
  // The manifest is consumed by Tilt Brush at export time, and should not
  // be uploaded to Poly or be served by it.
  //
  // The ExportRequests is processed by a texture downsampler; see DownsampleTextures()
  //
  // Shaders are not generated here; see GenerateShaders()
  //
  private static ExportRequests WriteManifest(
      string manifestPath, string exportRoot) {
    ExportRequests exportRequests = new ExportRequests();
    Debug.LogFormat("Exporting to: {0}", manifestPath);

    ExportGlTF.ExportManifest manifest = new ExportGlTF.ExportManifest();
    Dictionary<Guid, BrushDescriptor> brushes;
    using (var unused = new BuildTiltBrush.TempHookUpSingletons()) {
      manifest.tiltBrushVersion = App.Config.m_VersionNumber;
      manifest.tiltBrushBuildStamp = App.Config.m_BuildStamp;
      brushes = GetBrushes();
    }

    foreach (KeyValuePair<Guid, BrushDescriptor> kvp in brushes) try {
      BrushDescriptor descriptor = kvp.Value;
      var exp = ExportBrush(exportRequests, descriptor, exportRoot);
      manifest.brushes.Add(exp.guid, exp);

      // While we're at it, maybe a sanity check of the descriptor is in order
      if (descriptor.m_RenderBackfaces && !exp.enableCull) {
        Debug.LogWarning(
            $"{descriptor.m_DurableName}: generates backface geometry, but disables culling",
            descriptor);
      }
    } catch (ExportException e) {
      Debug.LogException(e);
    } // foreach Brush + try

    var serializer = new JsonSerializer();
    serializer.ContractResolver = new CustomJsonContractResolver();
    using (var writer = new CustomJsonWriter(new StreamWriter(manifestPath))) {
      writer.Formatting = Formatting.Indented;
      serializer.Serialize(writer, manifest);
    }

    return exportRequests;
  }
}

}
