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
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TiltBrush {

[System.Serializable()]
public class ExportFailedException : System.Exception {
  public ExportFailedException(string message)
    : base(message) { }
  public ExportFailedException(string fmt, params object[] args)
    : base(string.Format(fmt, args)) { }
}

static class ToolkitUtils {
  #region Settings and configurables

  // Probably c:\src\tbtools
  static readonly string kExportPathRootFull =
      Path.Combine(Application.dataPath, "../../tbtools/UnitySDK");

  // Scraped from TBT14 release, which is the last release that used
  // stateful/nondeterminstic methods when creating .meta files.
  // Verified that these are the same as the PT1.0 release
  static Dictionary<Guid, string> kLegacyBrushGuidToTbtAssetGuidMapping = new Dictionary<Guid, string>() {
    { new Guid("89d104cd-d012-426b-b5b3-bbaee63ac43c"), "cadc04d18daae874aa63168911a19ba6"}, // Bubbles
    { new Guid("0f0ff7b2-a677-45eb-a7d6-0cd7206f4816"), "58575cce2727fec449fd95b38f249b39"}, // ChromaticWave
    { new Guid("79168f10-6961-464a-8be1-57ed364c5600"), "07ab675df8bcbd148809e406fcd485fd"}, // CoarseBristles
    { new Guid("4391aaaa-df73-4396-9e33-31e4e4930b27"), "d4d57e30cdd022945b067d76b7ded1c7"}, // Disco
    { new Guid("d1d991f2-e7a0-4cf1-b328-f57e915e6260"), "89d06e0e0a68a1c488e9eb9bc2ca3abc"}, // DotMarker
    { new Guid("6a1cf9f9-032c-45ec-9b1d-a6680bee30f7"), "7f2dcdb5c7d9fc94db14b323331d5860"}, // Dots
    { new Guid("0d3889f3-3ede-470c-8af4-f44813306126"), "f4ed52160dfaf694ea666a6294e38f42"}, // DoubleTaperedFlat
    { new Guid("0d3889f3-3ede-470c-8af4-de4813306126"), "c2b9f7e0f4fb54b41b7dbeb23b117551"}, // DoubleTaperedMarker
    { new Guid("3ca16e2f-bdcd-4da2-8631-dcef342f40f1"), "6d014762200f4014fa8a87bcfaa038fe"}, // DuctTape
    { new Guid("f6e85de3-6dcc-4e7f-87fd-cee8c3d25d51"), "6e268406bbadd084a825dda73d24387a"}, // Electricity
    { new Guid("02ffb866-7fb2-4d15-b761-1012cefb1360"), "9f45a29bbed9af6479948c03267fe239"}, // Embers
    { new Guid("cb92b597-94ca-4255-b017-0e3f42f12f9e"), "3c7ec9f584dd60e4a9308abe95b9f22e"}, // Fire
    { new Guid("280c0a7a-aad8-416c-a7d2-df63d129ca70"), "a97674f9669261b48a61cfed32378345"}, // Flat
    { new Guid("55303bc4-c749-4a72-98d9-d23e68e76e18"), "969b71c814f75e9408602593d4e10e3e"}, // FlatDeprecated
    { new Guid("cf019139-d41c-4eb0-a1d0-5cf54b0a42f3"), "0cfb52fb947fb6e4786ed9f2bdc5328c"}, // Highlighter
    { new Guid("e8ef32b1-baa8-460a-9c2c-9cf8506794f5"), "3251442494b7f6c43aa51c2ede6c409e"}, // Hypercolor
    { new Guid("6a1cf9f9-032c-45ec-9b6e-a6680bee32e9"), "88ecf340d7ce92a4bada6eac937e887a"}, // HyperGrid
    { new Guid("c0012095-3ffd-4040-8ee1-fc180d346eaa"), "6c27caca67a732d4f9d59b028c4dd9a8"}, // Ink
    { new Guid("ea19de07-d0c0-4484-9198-18489a3c1487"), "362043fe9dab1704fa0f912c4f1e0d09"}, // Leaves
    { new Guid("2241cd32-8ba2-48a5-9ee7-2caef7e9ed62"), "54a30b55c2e961643a7701bedc663c88"}, // Light
    { new Guid("4391aaaa-df81-4396-9e33-31e4e4930b27"), "4f55fba4b64913b419cf4a17ba5d489b"}, // LightWire
    { new Guid("429ed64a-4e97-4466-84d3-145a861ef684"), "3fd505f4dc669fe4c86c80d009ec2efd"}, // Marker
    { new Guid("b2ffef01-eaaa-4ab5-aa64-95a2c4f5dbc6"), "71228caab923caf46a89c3275938564a"}, // NeonPulse
    { new Guid("c515dad7-4393-4681-81ad-162ef052241b"), "d7ada22e6a436e742910a7553f159550"}, // OilPaint
    { new Guid("759f1ebd-20cd-4720-8d41-234e0da63716"), "9b77a7cd7bb6c0244bca8ab8221af846"}, // Paper
    { new Guid("c33714d1-b2f9-412e-bd50-1884c9d46336"), "7e6a9b8a0ad82cf4da20e86f1a326040"}, // Plasma
    { new Guid("ad1ad437-76e2-450d-a23a-e17f8310b960"), "d75833fb286458e468c00cc5b0ac5f4d"}, // Rainbow
    { new Guid("70d79cca-b159-4f35-990c-f02193947fe8"), "84e07a49ce756cf44ab7ea2dbdc064c4"}, // Smoke
    { new Guid("d902ed8b-d0d1-476c-a8de-878a79e3a34c"), "7ccf5a61af1914e409b526a98d43354b"}, // Snow
    { new Guid("accb32f5-4509-454f-93f8-1df3fd31df1b"), "bf5c0e36bc63d7b418af3ac347cfdcfd"}, // SoftHighlighter
    { new Guid("7a1c8107-50c5-4b70-9a39-421576d6617e"), "402a078d5db9ad0468dda690396443bc"}, // Splatter
    { new Guid("0eb4db27-3f82-408d-b5a1-19ebd7d5b711"), "b1626af60ae7a31419a1c40afb39c5ed"}, // Stars
    { new Guid("44bb800a-fbc3-4592-8426-94ecb05ddec3"), "12b98790c761f47429559af03b6669d2"}, // Streamers
    { new Guid("0077f88c-d93a-42f3-b59b-b31c50cdb414"), "38f5be2edab11fa429b090756d26d000"}, // Taffy
    { new Guid("c8ccb53d-ae13-45ef-8afb-b730d81394eb"), "e7ec06c48c590ea4ebe400ce6a278efe"}, // TaperedFlat
    { new Guid("d90c6ad8-af0f-4b54-b422-e0f92abe1b3c"), "b39f584eb1440404289d9461a0973e35"}, // TaperedMarker
    { new Guid("1a26b8c0-8a07-4f8a-9fac-d2ef36e0cad0"), "26d7464425c0b84479a8a21a695b194c"}, // TaperedMarker_Flat
    { new Guid("fdf0326a-c0d1-4fed-b101-9db0ff6d071f"), "94cb84ebeec44c540bb15b32797e5a98"}, // ThickPaint
    { new Guid("4391385a-df73-4396-9e33-31e4e4930b27"), "b8a2349e67797ea4593639454ef74543"}, // Toon
    { new Guid("d229d335-c334-495a-a801-660ac8a87360"), "5398e778cce3fdf469bbfa388aa46d5d"}, // VelvetInk
    { new Guid("10201aa3-ebc2-42d8-84b7-2e63f6eeb8ab"), "ebe8a9d99afbd6b46909ea24a9896258"}, // Waveform
    { new Guid("4391385a-cf83-4396-9e33-31e4e4930b27"), "3a72bd3c1fe3d784ab8896ec9fdfd058"}, // Wire
    // Scraped from TBT14, but idential to deterministic-style meta files
    // { new Guid("232998f8-d357-47a2-993a-53415df9be10"), "71dc5ead67382b75789dd72c8058d553"}, // BlocksGem
    // { new Guid("3d813d82-5839-4450-8ddc-8e889ecd96c7"), "185ca6407d6d6095e95d6695d994a12b"}, // BlocksGlass
    // { new Guid("0e87b49c-6546-3a34-3a44-8a556d7d6c3e"), "d6f6de76308b4b05386f187491479d94"}, // BlocksPaper
  };

  const string kManifestAssetPath = "Assets/Manifest.asset";

#if UNITY_EDITOR_WIN
  const string kAbsoluteUri = "C:/";
#else
  const string kAbsoluteUri = "/";
#endif

  // Files that should not be copied
  static readonly HashSet<string> kIgnoredFiles = new HashSet<string> {
    "Assets/Shaders/Include/Hdr.cginc",
    "Assets/Shaders/Include/Ods.cginc"
  };

  // Files that should override their path and instead be copied to a specific folder
  // These are relative to the UnitySDK folder
  static Dictionary<string, string> kPatOverrides = new Dictionary<string, string> {
    { "Noise.cginc", "Assets/ThirdParty/Noise/Shaders/Noise.cginc" }
  };

  // Replace these regexps in the shaders
  static Dictionary<string, string> kShaderReplacements = new Dictionary<string, string> {
    // HDR

    { @".*pragma multi_compile __ HDR_EMULATED.*\n", "" },
    { @".*include.*Hdr\.cginc.*\n",                  "" },
    { @"encodeHdr\s*\((.*)\)",                        "float4($1, 1.0)" },

    // ODS

    { @".*include.*Ods\.cginc.*\n",                  "" },
    // TBT_LINEAR_TARGET and ODS_RENDER have no real connection with each other,
    // but it's convenient to be able to swap one for the other. Every TB brush
    // multi-compiles ODS, and every TBT brush needs to multi-compile TBT_LINEAR_TARGET
    // { ".*pragma multi_compile __ ODS_RENDER.*\\n",   "" },
    { @"multi_compile __ ODS_RENDER ODS_RENDER_CM",  "multi_compile __ TBT_LINEAR_TARGET" },
    { @".*PrepForOdsWorldSpace.*\n",                 "" },
    { @".*PrepForOds.*\n",                           "" },
    // Temporary fix for broken shaders
    //{ @"(.*)(v\.texcoord1|v\.tangent\.w)(.*)\r\n", "//$0${1}0.0${3} // Additional coordinates are unsupported for the time being\r\n"}

    // Toolkit

    // Lines tagged "// NOTOOLKIT" get removed, and lines taged "// TOOLKIT: ..." get uncommented
    { @".* // NOTOOLKIT.*\n", "" },
    { @"// TOOLKIT: (.*\n)", "$1"}
  };

  static string kLicenseText = string.Join("\n", new string[] {
    "// Copyright 2017 Google Inc.",
    "//",
    "// Licensed under the Apache License, Version 2.0 (the \"License\");",
    "// you may not use this file except in compliance with the License.",
    "// You may obtain a copy of the License at",
    "//",
    "//     http://www.apache.org/licenses/LICENSE-2.0",
    "//",
    "// Unless required by applicable law or agreed to in writing, software",
    "// distributed under the License is distributed on an \"AS IS\" BASIS,",
    "// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.",
    "// See the License for the specific language governing permissions and",
    "// limitations under the License.",
    ""
  });

  // Which file extensions need a license
  static HashSet<string> kLicensedFileExtensions = new HashSet<string> {
    ".shader", ".cs", ".cginc"
  };

  #endregion

  #region Menus

  [MenuItem("Tilt/Toolkit/Export FBX", true)]
  private static bool ExportBrushStrokesFbx_Enabled() {
    return Application.isPlaying;
  }

#if FBX_SUPPORTED
  [MenuItem("Tilt/Toolkit/Export FBX")]
  private static void ExportBrushStrokesFbx() {
    var current = SaveLoadScript.m_Instance.SceneFile;
    string basename = (current.Valid)
      ? Path.GetFileNameWithoutExtension(current.FullPath).Replace(" ", "_")
      : "Untitled";

    string directoryName = FileUtils.GenerateNonexistentFilename(
      App.UserExportPath(), basename, "");
    if (!FileUtils.InitializeDirectoryWithUserError(directoryName,
                                                    "Failed to export")) {
      return;
    }
    string fbxName = Path.Combine(directoryName, basename + ".fbx");
    ExportFbx.Export(fbxName, ExportFbx.kFbxAscii);
  }
#endif

  // Collects all brushes and their assets, and exports them into a folder that can be copied into into Tilt Brush Toolkit's Unity SDK
  [MenuItem("Tilt/Toolkit/Export Brushes for Toolkit")]
  static void CollectBrushes() {
    ExportToToolkit(
        kExportPathRootFull,
        kExportPathRootFull + "/Assets/TiltBrush",
        doBrushes:true);
  }

  // Collects all environments as scenes with the right preferences, and their assets, and exports them into a folder that can be copied into Tilt Brush Toolkit's Unity SDK
  // This is dead and unsupported code at the moment
  // [MenuItem("Tilt/Toolkit/Export Environments for Toolkit")]
  static void CollectEnvironments() {
    ExportToToolkit(
        kExportPathRootFull,
        kExportPathRootFull + "/Assets/TiltBrushExamples",
        doEnvironments:true);
  }

  #endregion

  static Vector2 m_ProgressRange = new Vector2(0, 1);
  static List<string> m_Warnings;

  static void MaybeDestroy(string directory) {
    if (! Directory.Exists(directory)) { return; }
    if (EditorUtility.DisplayDialog(string.Format("Delete {0}?", directory), "Yes", "No")) {
      Directory.Delete(directory, true);
    }
  }

  /// Iterate over enumerable, displaying a progress bar.
  static IEnumerable<T> WithProgress<T>(
      IEnumerable<T> enumerable,
      string heading,
      Func<T, string> nameFunc=null,
      bool allowCancel=false) {
    if (nameFunc == null) {
      nameFunc = (elt => elt.ToString());
    }
    List<T> items = enumerable.ToList();
    float lo = m_ProgressRange.x;
    float hi = m_ProgressRange.y;
    EditorUtility.DisplayProgressBar(heading, "", lo);
    for (int i = 0; i < items.Count; ++i) {
      yield return items[i];
      var name = nameFunc(items[i]);
      var description = string.Format("Processed {0}", name);
      var pct = lo + (hi - lo) * (i+1) / items.Count;
      if (allowCancel) {
        if (EditorUtility.DisplayCancelableProgressBar(heading, description, pct)) {
          throw new ExportFailedException("Cancelled at {0}", name);
        }
      } else {
        EditorUtility.DisplayProgressBar(heading, description, pct);
      }
    }
  }

  static void Warning(string fmt, params object[] args) {
    if (m_Warnings == null) {
      m_Warnings = new List<string>();
    }
    m_Warnings.Add(string.Format(fmt, args));
  }
  static void Error(string fmt, params object[] args) {
    Warning("ERROR: " + fmt, args);
  }

  static IEnumerable<BrushDescriptor> GetBrushesToExport(TiltBrushManifest manifest) {
    HashSet<BrushDescriptor> all = new HashSet<BrushDescriptor>();
    foreach (var brush in manifest.Brushes.Concat(manifest.CompatibilityBrushes)) {
      for (var current = brush; current != null; current = current.m_Supersedes) {
        all.Add(current);
      }
    }

    return all;
  }

  /// Pass:
  ///   unityProjectRoot A directory that contains an "Assets" folder
  ///   exportPath      Full path
  static void ExportToToolkit(
      string targetProjectRoot,
      string targetDirectory,
      bool doBrushes = false,
      bool doEnvironments = false) {
    m_Warnings = null;

    // c:\src\tb
    string sourceProjectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets/".Length);

    Directory.CreateDirectory(targetDirectory);
    AssetDatabase.CreateFolder("Assets", "Dynamic");

    try {
      var manifest = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>(kManifestAssetPath);
      if (manifest == null) {
        throw new ExportFailedException("Missing {0}", kManifestAssetPath);
      }

      HashSet<string> collectedAssets = new HashSet<string>();

      // Brushes

      m_ProgressRange = new Vector2(0, .5f);
      if (doBrushes) {
        // store paths to descriptors (direct references get lost)
        // XXX: what does that mean?
        var brushPaths = GetBrushesToExport(manifest)
            .OrderBy(desc => desc.DurableName)
            .Select(desc => AssetDatabase.GetAssetPath(desc))
            .ToList();
        foreach (var brushPath in WithProgress(brushPaths, "Brushes", Path.GetFileName, true)) {
          var descriptor = AssetDatabase.LoadAssetAtPath<BrushDescriptor>(brushPath);
          ExportToToolkit_Brush(descriptor, targetDirectory, collectedAssets);
        }
      }

      // Environments

      m_ProgressRange = new Vector2(m_ProgressRange.y, .85f);
      if (doEnvironments) {
        var envPaths = manifest.Environments
            .Select(env => AssetDatabase.GetAssetPath(env))
            .ToList();

        foreach (var envPath in WithProgress(envPaths, "Environments", Path.GetFileName, true)) {
          var environment = AssetDatabase.LoadAssetAtPath<Environment>(envPath);
          ExportToToolkit_Environment(environment, targetDirectory, collectedAssets);
        }
      }

      // Copy shared assets

      m_ProgressRange = new Vector2(m_ProgressRange.y, .95f);
      {
        HashSet<string> copied = new HashSet<string>();
        HashSet<string> toCopy = collectedAssets;
        int i = 0;
        while (toCopy.Count > 0) {
          if (++i > 20) {
            Debug.LogError("Too many iterations");
            break;
          }
          HashSet<string> extra = new HashSet<string>();
          foreach (string asset in WithProgress(toCopy.OrderBy(x => x), "Copying")) {
            CopyAssetAndIncludes(sourceProjectRoot, targetProjectRoot, targetDirectory, asset, extra);
          }
          copied.UnionWith(toCopy);

          toCopy = extra;
          toCopy.ExceptWith(copied);
        }
      }

      //EditorUtility.RevealInFinder(targetDirectory);  this seems to hang unity sometimes
      Debug.LogFormat("Assets exported to {0}", targetDirectory);
    } finally {
      AssetDatabase.DeleteAsset("Assets/Dynamic");
      EditorUtility.ClearProgressBar();
      AssetDatabase.Refresh();
      if (m_Warnings != null && m_Warnings.Count > 0) {
        string[] uniqueWarnings = new HashSet<string>(m_Warnings).OrderBy(elt => elt).ToArray();
        Debug.LogWarning(string.Join("\n", uniqueWarnings));
      }
    }
  }

  // Returns the name used for TBT assets that are created per-brush.
  // Ideally, this name should be both determinsitic and non-colliding.
  //
  // Note that even if 2 TB brushes reference the same .mat, TBT contains
  // 1 .mat per brush.
  static string GetCreatedAssetNameForBrush(BrushDescriptor desc) {
    // New behavior for TBT15 and above: To handle the double-sided/single-sided fiasco,
    // use .asset/.mat names guaranteed not to conflict with previous brush versions' names.
    // Older brushes use the same old (rubbish) names.
    float creationVersion;
    if (! float.TryParse(desc.m_CreationVersion, out creationVersion)) {
      throw new ExportFailedException(
          "{0}: TBT brushes must have a valid creationVersion", desc.name);
    } else {
      int creationMajorVersion = (int)creationVersion;
      if (creationMajorVersion >= 15 || desc.name.EndsWith("SingleSided")) {
        return string.Format("{0}_{1}", desc.m_DurableName, creationMajorVersion);
      } else {
        // Legacy. Would be better if this were something that couldn't accidentally
        // change (eg, desc.m_DurableName); but that would change the legacy behavior
        // since desc.m_DurableName has already fallen out of sync with desc.name.
        // But a future motivated person could bake desc.name into some new field
        // eg BrushDescriptor.m_TbtExportBaseName.
        return desc.name;
      }
    }
  }

  static string GetSubdirForBrush(BrushDescriptor desc) {
    var descPath = AssetDatabase.GetAssetPath(desc);
    // The naming convention is
    //   Brushes/<high-level grouping>/<brush dir>
    // for example
    //   Brushes/Basic/Light/Light.asset
    var match = Regex.Match(Path.GetDirectoryName(descPath), @"Resources/Brushes/(.*)/");
    string groupName;
    if (match.Success) {
      groupName = match.Groups[1].Value;
    } else {
      Debug.LogWarningFormat("Unexpected descriptor asset path {0}", descPath);
      groupName = "Basic";
    }
    return string.Format("Assets/Brushes/{0}/{1}", groupName, desc.name);
  }

  // Directly creates some assets in the target tree, and appends other assets
  // to collectedAssets for later copying.
  //
  // Assets which are directly created in the target:
  //               --- asset ---                            --- .meta guid ---
  //   <target>/Assets/Brushes/Basic/<name>/<name>.mat      desc.m_Guid.ToString("N")
  //   <target>/Assets/Brushes/Basic/<name>/<name>.asset    Uuid5(desc.m_Guid, 'tbt-asset'), unity-style
  //
  // - .mat guids are serialized as .ToString("N")
  // - Pre-M14 .asset guids are random and generated by Unity (RFC 4122 type 4)
  // - M14+ .asset guids are generated deterministically (RFC 4122 type 5), and
  //   we copy Unity's wacky serialization format so it's more-feasible to determine
  //   which is which, should we ever need to.  But maybe we should bite the bullet
  //   and make them all type 5? (which may annoy a small number of people trying
  //   to upgrade TBT in a pre-existing project because of the .meta change)
  static void ExportToToolkit_Brush(
      BrushDescriptor descriptor,
      string targetDirectory,
      HashSet<string> collectedAssets) {
    if (descriptor.name.StartsWith("Pbr")) {
      Debug.LogFormat("Skipping {0}: we don't know how to handle pbr in TBT yet", descriptor.name);
      return;
    }
    if (descriptor.name.StartsWith("EnvironmentDiffuse")) {
      Debug.LogFormat("Skipping {0}: we don't need these EnvironmentDiffuse things in TBT", descriptor.name);
      return;
    }

    string containerDirectory = Path.Combine(targetDirectory, GetSubdirForBrush(descriptor));
    string assetName = GetCreatedAssetNameForBrush(descriptor);

    // Create material and store its dependencies
    string desiredFilename = assetName + ".mat";
    string materialFinalPath = containerDirectory + "/" + desiredFilename;

    string materialAssetPath = AssetDatabase.GetAssetPath(descriptor.Material);
    collectedAssets.UnionWith(GetDependencies(materialAssetPath, includeRoot:false));

    // Steal the brush's TB guid to use as a Unity guid.
    Guid materialGuid = descriptor.m_Guid;
    CopyAsset(materialAssetPath, materialFinalPath, adjustName: true);
    SetFileGuid_Incorrect(materialFinalPath, descriptor.m_Guid);

    // Create a brush descriptor
    string targetPath = Path.Combine(containerDirectory, assetName + ".asset");
    string meta;
    string yaml = ToolkitBrushDescriptor.CreateAndSerialize(
        descriptor, materialGuid, assetName, out meta);
    File.WriteAllText(targetPath, yaml);

    string targetMetaPath = targetPath + ".meta";
    if (!File.Exists(targetMetaPath)) {
      Warning("New brush {0}: Uncomment and run BrushManifest.MenuItem_UpdateManifest() in TBT",
              descriptor.m_DurableName);
    } else {
      // Revert spurious timestamp changes
      var match = Regex.Match(File.ReadAllText(targetMetaPath), @"timeCreated: \d+");
      if (match.Success) {
        meta = Regex.Replace(meta, @"timeCreated: \d+", match.Groups[0].Value);
      }
    }

    File.WriteAllText(targetMetaPath, meta);
    if (kLegacyBrushGuidToTbtAssetGuidMapping.ContainsKey(descriptor.m_Guid)) {
      // Legacy: for pre-M14 brushes that were created with random .meta guids
      SetFileGuid_String(targetPath, kLegacyBrushGuidToTbtAssetGuidMapping[descriptor.m_Guid]);
    } else {
      Guid brushAssetGuid = GuidUtils.Uuid5(descriptor.m_Guid, "tbt-asset");
      SetFileGuid_Correct(targetPath, brushAssetGuid);
    }
  }

  // Appends to collectedAssets
  static void ExportToToolkit_Environment(
      Environment environment,
      string targetDirectory,
      HashSet<string> collectedAssets) {
    // make an environment
    var name = environment.name;
    var settings = environment.m_RenderSettings;
    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
        "Assets/Resources/" + settings.m_EnvironmentPrefab + ".prefab");
    PrefabUtility.InstantiatePrefab(prefab);

    RenderSettings.ambientSkyColor = settings.m_AmbientColor;
    RenderSettings.fogColor = settings.m_FogColor;
    RenderSettings.reflectionIntensity = settings.m_ReflectionIntensity;
    RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;

    RenderSettings.fog = settings.m_FogEnabled;
    RenderSettings.fogMode = settings.m_FogMode;
    RenderSettings.fogDensity = settings.m_FogDensity;
    RenderSettings.fogStartDistance = settings.m_FogStartDistance;
    RenderSettings.fogEndDistance = settings.m_FogEndDistance;

    // Lights
    Object.DestroyImmediate(Object.FindObjectOfType<Light>());
    for (int li = 0; li < environment.m_Lights.Count; li++) {
      var lsettings = environment.m_Lights[li];
      var light = new GameObject("Light " + li, typeof(Light)).GetComponent<Light>();
      light.transform.position = lsettings.m_Position;
      light.transform.rotation = lsettings.m_Rotation;
      light.color = lsettings.Color;
      light.type = lsettings.m_Type;
      light.range = lsettings.m_Range;
      light.spotAngle = lsettings.m_SpotAngle;
      light.intensity = 1.0f;
      light.shadows = lsettings.m_ShadowsEnabled ? LightShadows.Hard : LightShadows.None;
    }

    // Camera
    var cam = Object.FindObjectOfType<Camera>();
    cam.transform.position = new Vector3(0, 15, 0);
    cam.nearClipPlane = 0.5f;
    cam.farClipPlane = 10000.0f;
    cam.fieldOfView = 60;
    cam.clearFlags = CameraClearFlags.Skybox;
    cam.backgroundColor = settings.m_ClearColor;


    RenderSettings.customReflection = settings.m_ReflectionCubemap;
    if (settings.m_SkyboxCubemap) {
      Error("These guid shenanigans don't work yet: {0}", name);
      Material skyboxMaterialTmp = new Material(Shader.Find("Custom/Skybox"));
      Guid skyboxMaterialGuid = GuidUtils.Uuid5(environment.m_Guid, "skyboxMaterial");
      Material skyboxMaterial = CreateAssetWithGuid_Incorrect(
          skyboxMaterialTmp, name + "_Skybox.mat", skyboxMaterialGuid);
      string sbAssetPath = AssetDatabase.GetAssetPath(skyboxMaterial);

      collectedAssets.UnionWith(GetDependencies(sbAssetPath, includeRoot:false));

      string sbFinalPath = targetDirectory + "/" + sbAssetPath;
      CopyAsset(sbAssetPath, sbFinalPath);

      RenderSettings.skybox = skyboxMaterial;
      RenderSettings.skybox.SetColor("_Tint", settings.m_SkyboxTint);
      RenderSettings.skybox.SetFloat("_Exposure", settings.m_SkyboxExposure);
      RenderSettings.skybox.SetTexture("_Tex", settings.m_SkyboxCubemap);
    } else {
      RenderSettings.skybox = null;
    }

    Lightmapping.realtimeGI = false;
    Lightmapping.bakedGI = false;

    // Store scene
    var sceneAssetPath = "Assets/Dynamic/" + name + ".unity";
    var sceneFinalPath = targetDirectory + "/Environments/" + Path.GetFileName(sceneAssetPath);
    EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), sceneAssetPath);
    AssetDatabase.ImportAsset(sceneAssetPath, ImportAssetOptions.ForceSynchronousImport);

    CopyAsset(sceneAssetPath, sceneFinalPath);
    collectedAssets.UnionWith(GetDependencies(sceneAssetPath, includeRoot:false));
    SetFileGuid_Incorrect(sceneFinalPath, environment.m_Guid);

    AssetDatabase.DeleteAsset(sceneAssetPath);

    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
  }

  // Copies sourceProjectRoot / asset to targetDirectory / asset
  // targetDirectory must be a subdirectory of targetProjectRoot
  // If any includes are found, they're added to moreAssets.
  //
  // Pass:
  //   sourceProjectRoot  - the Unity project that the asset comes from
  //   targetProjectRoot  - the Unity project that the asset is going to
  //   targetDirectory    - either targetProjectRoot, or a subdirectory
  //   asset              - name of an asset, relative to the project, eg "Assets/foo.png"
  //
  // Note that if targetDirectory != targetProjectRoot,
  // from oldRoot to targetDirectory
  // May also find new assets to copy; these are added to moreAssets
  static void CopyAssetAndIncludes(
      string sourceProjectRoot,
      string targetProjectRoot,
      string targetDirectory,
      string asset,
      HashSet<string> moreAssets) {
    var sourcePath = sourceProjectRoot + "/" + asset;

    string targetPath;
    {
      targetPath = targetDirectory + "/" + asset;
      if (targetPath.Contains("/Resources")) {
        targetPath = targetPath.Replace("/Resources", "");
      }
      var basename = Path.GetFileName(asset);
      if (kPatOverrides.Keys.Contains(basename)) {
        targetPath = targetProjectRoot + "/" + kPatOverrides[basename];
      }
    }

    CopyAsset(sourcePath, targetPath);

    var obj = AssetDatabase.LoadAssetAtPath<Object>(asset);

    // Collect includes

    if (obj is Shader || Path.GetExtension(asset) == ".cginc") {
      var str = File.ReadAllText(targetPath);
      var originalstr = str;
      var pattern = @"#\s*include ""(.+?\.cginc)""";
      str = Regex.Replace(str, pattern, (m) => {
          string includeAsset = m.Groups[1].Value;
          string includePath = sourceProjectRoot + "/" + includeAsset;
          // if include file is inside the Library, collect it and
          // replace its path to be local to the Shaders folder
          if (IsUnwanted(includeAsset)) {
            return "// " + m.Groups[0].Value;
          }
          if (File.Exists(includePath)) {
            moreAssets.UnionWith(GetDependencies(includeAsset, includeRoot:true));

            // Make sure files in the static list use the full path
            string path;
            if (kPatOverrides.Keys.Contains(Path.GetFileName(includeAsset))) {
              path = kPatOverrides[Path.GetFileName(includeAsset)];
            } else {
              Uri uri1 = new Uri(kAbsoluteUri + targetPath);
              Uri uri2 = new Uri(kAbsoluteUri + targetDirectory + "/" + includeAsset);
              path = uri1.MakeRelativeUri(uri2).ToString();
            }

            return "#include \"" + path + "\"";
          }
          return m.Value;
        });
      // "Singleline" makes "." match all characters, even newline
      str = Regex.Replace(str, @"// NOTOOLKIT {.*?// } NOTOOLKIT\r?\n", "",
                          RegexOptions.Singleline);
      foreach (var r in kShaderReplacements.Keys) {
        str = Regex.Replace(str, r, kShaderReplacements[r]);
      }
      if (str != originalstr) {
        File.WriteAllText(targetPath, str);
      }
    }
  }

  /// If adjustName=true, modify the m_Name: field to match the asset name.
  private static void CopyAsset(string source, string dest, bool copyMetaFiles = true,
                                bool adjustName = false) {
    if (!File.Exists(source)) {
      Error("Could not copy {0}; source file not found", source);
      return;
    }
    if (kIgnoredFiles.Contains(Path.GetFileName(source))) {
      Warning("{0} is in the ignore list so it was skipped.", source);
      return;
    }
    if (!Directory.Exists(Path.GetDirectoryName(dest))) {
      Directory.CreateDirectory(Path.GetDirectoryName(dest));
    }
    File.Copy(source, dest, true);

    if (adjustName) {
      string srcName = string.Format("  m_Name: {0}", Path.GetFileNameWithoutExtension(source));
      string dstName = string.Format("  m_Name: {0}", Path.GetFileNameWithoutExtension(dest));
      string srcBody = File.ReadAllText(dest);
      string dstBody = srcBody.Replace(srcName, dstName);
      if (dstBody != srcBody) {
        File.WriteAllText(dest, dstBody);
      }
    }

    if (copyMetaFiles) {
      string sourceMeta = source + ".meta";
      string destMeta = dest + ".meta";
      if (File.Exists(sourceMeta)) {
        string sourceContents = File.ReadAllText(sourceMeta);
        // The time change is spurious, and hides actually-interesting diffs
        if (File.Exists(destMeta)) {
          var match = Regex.Match(File.ReadAllText(destMeta), @"timeCreated: \d+");
          if (match.Success) {
            sourceContents = Regex.Replace(
                sourceContents, @"timeCreated: \d+", match.Groups[0].Value);
          }
        }
        File.WriteAllText(destMeta, sourceContents);
      } else {
        Error("{0} has no meta file.", source);
      }
    }

    // Check license
    if (!dest.Contains("ThirdParty") && kLicensedFileExtensions.Contains(Path.GetExtension(dest))) {
      string withoutLicense = File.ReadAllText(dest).Replace("\r\n", "\n");
      string withLicense = string.Format("{0}\n{1}", kLicenseText, withoutLicense);
      File.WriteAllText(dest, withLicense.Replace("\n", System.Environment.NewLine));
    }
  }

  private static T CreateAssetWithGuid_Incorrect<T>(T obj, string filename, Guid guid) where T:Object {
    // Create once with the wrong guid; then fix up the guid
    string tempAssetPath1 = string.Format("Assets/Dynamic/x{0}", filename);
    string tempAssetPath2 = string.Format("Assets/Dynamic/{0}", filename);
    AssetDatabase.CreateAsset(obj, tempAssetPath1);
    File.Copy(tempAssetPath1, tempAssetPath2, true);
    File.Copy(tempAssetPath1+".meta", tempAssetPath2+".meta", true);
    SetFileGuid_Incorrect(tempAssetPath2, guid);
    AssetDatabase.ImportAsset(tempAssetPath2, ImportAssetOptions.ForceSynchronousImport);
    return AssetDatabase.LoadAssetAtPath<T>(tempAssetPath2);
  }

  static bool IsUnwanted(string asset) {
    if (asset.Contains("Paid")) { return true; }
    if (kIgnoredFiles.Contains(asset)) { return true; }
    return false;
  }

  /// Returns all dependencies of asset, except for asset.
  /// Traversal does not go through "unwanted" assets.
  /// If asset is itself "unwanted", return value is empty.
  static HashSet<string> GetDependencies(string rootAsset, bool includeRoot) {
    var seen = new HashSet<string>();
    if (IsUnwanted(rootAsset)) {
      Error("Asking for dependencies of unwanted {0}", rootAsset);
      return seen;
    }

    // Invariant: open contains no paid assets
    var open = new HashSet<string>() { rootAsset };
    while (open.Count > 0) {
      var current = open.First();
      open.Remove(current);
      if (! seen.Add(current)) {
        // already processed
        continue;
      }

      foreach (string child in AssetDatabase.GetDependencies(current, false)) {
        if (! IsUnwanted(child)) {
          open.Add(child);
        } else {
          Warning("Skipping dependencies from unwanted {0} -> {1}", current, child);
        }
      }
    }
    if (!includeRoot) {
      seen.Remove(rootAsset);
    }
    return seen;
  }

  // This is the wrong way of turning GUID into a unity string.
  // The actual implementation is mind-boggling -- see GuidUtils.SerializeToUnity
  public static string GuidToUnityString_Incorrect(System.Guid guid) {
    return guid.ToString("N");
  }

  /// <summary>
  /// Assign a stable GUID to keep references intact by writing it to the meta file.
  /// </summary>
  static void SetFileGuid_Incorrect(string filePath, System.Guid guid) {
    if (File.Exists(filePath) && File.Exists(filePath + ".meta")) {
      var str = File.ReadAllText(filePath + ".meta");
      str = Regex.Replace(str, @"guid\: .+?\n", "guid: " + GuidToUnityString_Incorrect(guid) + "\n");
      File.WriteAllText(filePath + ".meta", str);
    } else {
      Error("Cannot find file or meta file {0}", filePath);
    }
  }

  /// <summary>
  /// Assign a stable GUID to keep references intact by writing it to the meta file.
  /// Uses Unity-style serialization of guids
  /// </summary>
  static void SetFileGuid_Correct(string filePath, System.Guid guid) {
    if (File.Exists(filePath) && File.Exists(filePath + ".meta")) {
      var str = File.ReadAllText(filePath + ".meta");
      str = Regex.Replace(str, @"guid\: .+?\n", "guid: " + GuidUtils.SerializeToUnity(guid) + "\n");
      File.WriteAllText(filePath + ".meta", str);
    } else {
      Error("Cannot find file or meta file {0}", filePath);
    }
  }

  static void SetFileGuid_String(string filePath, string guid) {
    if (File.Exists(filePath) && File.Exists(filePath + ".meta")) {
      var str = File.ReadAllText(filePath + ".meta");
      str = Regex.Replace(str, @"guid\: .+?\n", "guid: " + guid + "\n");
      File.WriteAllText(filePath + ".meta", str);
    } else {
      Error("Cannot find file or meta file {0}", filePath);
    }
  }

  static string GetFileGuid_String(string filePath) {
    var str = File.ReadAllText(filePath + ".meta");
    return Regex.Match(str, @"guid\: (.+?)\n").Groups[1].Value;
  }
}
}
