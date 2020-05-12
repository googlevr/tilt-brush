// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using UnityEngine;

#if TILT_BRUSH
using BrushDescriptor = TiltBrush.BrushDescriptor;
#endif

namespace TiltBrushToolkit {

public class GltfMaterialConverter {
  private static readonly Regex kTiltBrushMaterialRegex = new Regex(
      @".*([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$");
  // Matches
  //    http://...<guid>/shadername.glsl
  //    <some local file>/.../<guid>-<version>.glsl
  private static readonly Regex kTiltBrushShaderRegex = new Regex(
      @".*([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})[/-]");

  /// <summary>
  /// Information about a Unity material corresponding to a Gltf node.
  /// </summary>
  public struct UnityMaterial {
    /// <summary>
    /// The material to be used in place of the GltfMaterial
    /// </summary>
    public Material material;
    /// <summary>
    /// The material that "material" is based on. This might be the same as
    /// "material", if no customizations were needed.
    /// </summary>
    public Material template;
#if TILT_BRUSH
    /// An exportable representation of "template".
    /// null means that this material is not exportable; but currently
    /// this will never be null.
    /// Not needed in TBT because TBT doesn't care about export.
    public BrushDescriptor exportableMaterial;
#endif
  }

  /// <summary>
  /// List of NEW Unity materials we have created.
  /// </summary>
  private List<Material> m_newMaterials = new List<Material>();

  /// <summary>
  /// For memoizing GetMaterial()
  /// </summary>
  private Dictionary<GltfMaterialBase, UnityMaterial> m_getMaterialMemo =
      new Dictionary<GltfMaterialBase, UnityMaterial>();

  private static bool IsTiltBrushHostedUri(string uri) {
    // Will always look like "https://www.tiltbrush.com/shaders/..."
    if (uri.Contains("://")) { return true; }
    return false;
  }

  /// <summary>
  /// Enumerates those Textures associated with local materials, as distinguished
  /// from well-known, global materials like BlocksPaper and Tilt Brush Light.
  /// Textures associated with those latter materials will not be enumerated.
  ///
  /// These are the textures that need UnityEngine.Textures created for them.
  /// </summary>
  public static IEnumerable<GltfTextureBase> NecessaryTextures(GltfRootBase root) {
    foreach (var mat in root.Materials) {
      if (! IsGlobalMaterial(mat)) {
        foreach (var tex in mat.ReferencedTextures) {
          yield return tex;
        }
      }
    }
  }

  /// <summary>
  /// Converts "Necessary" textures textures found in the gltf file.
  /// Coroutine must be fully consumed before generating materials.
  /// </summary>
  /// <seealso cref="GltfMaterialConverter.NecessaryTextures" />
  /// <param name="root">The root of the GLTF file.</param>
  /// <param name="loader">The loader to use to load resources (textures, etc).</param>
  /// <param name="loaded">Mutated to add any textures that were loaded.</param>
  public static IEnumerable LoadTexturesCoroutine(
      GltfRootBase root, IUriLoader loader, List<Texture2D> loaded) {
    foreach (GltfTextureBase gltfTexture in NecessaryTextures(root)) {
      if (IsTiltBrushHostedUri(gltfTexture.SourcePtr.uri)) {
        Debug.LogWarningFormat("Texture {0} uri {1} was considered necessary",
                               gltfTexture.GltfId, gltfTexture.SourcePtr.uri);
        continue;
      }
      foreach (var unused in ConvertTextureCoroutine(gltfTexture, loader)) {
        yield return null;
      }
      if (gltfTexture.unityTexture != null) {
        loaded.Add(gltfTexture.unityTexture);
      }
    }

    // After textures are converted, we don't need the cached RawImage data any more.
    // "Deallocate" it.
    foreach (GltfImageBase image in root.Images) {
      image.data = null;
    }
  }

  /// <summary>
  /// Gets (or creates) the Unity material corresponding to the given glTF material.
  /// </summary>
  /// <param name="gltfMaterial">The glTF material.</param>
  /// <returns>The Unity material that correpsonds to the given GLTF2 material.</returns>
  public UnityMaterial? GetMaterial(GltfMaterialBase gltfMaterial) {
    if (m_getMaterialMemo.TryGetValue(gltfMaterial, out UnityMaterial memo)) {
      return memo;
    }

    if (LookUpGlobalMaterial(gltfMaterial) is UnityMaterial global) {
      Debug.Assert(global.material == global.template);
      m_getMaterialMemo[gltfMaterial] = global;
      return global;
    }

    if (ConvertGltfMaterial(gltfMaterial) is UnityMaterial created) {
      Debug.Assert(created.material != created.template);
      m_newMaterials.Add(created.material);
      m_getMaterialMemo[gltfMaterial] = created;
      return created;
    }

    Debug.LogErrorFormat("Failed to convert material {0}", gltfMaterial.name);
    return null;
  }

  /// <summary>
  /// Returns a list of new materials that were created as part of the material
  /// conversion process.
  /// </summary>
  public List<Material> GetGeneratedMaterials() {
    return new List<Material>(m_newMaterials);
  }

  /// <returns>true if there is a global material corresponding to the given glTF material,
  /// false if a material needs to be created for this material.</returns>
  private static bool IsGlobalMaterial(GltfMaterialBase gltfMaterial) {
    // Simple implementation for now
    return LookUpGlobalMaterial(gltfMaterial) != null;
  }

  /// <summary>
  /// Looks up a built-in global material that corresponds to the given GLTF material.
  /// This will NOT create new materials; it will only look up global ones.
  /// </summary>
  /// <param name="gltfMaterial">The material to look up.</param>
  /// <param name="materialGuid">The guid parsed from the material name, or Guid.None</param>
  /// <returns>The global material that corresponds to the given GLTF material,
  /// if found. If not found, null.</returns>
  private static UnityMaterial? LookUpGlobalMaterial(GltfMaterialBase gltfMaterial) {
#if TILT_BRUSH
    // Is this a Gltf1 blocks material?
    if (gltfMaterial.TechniqueExtras != null) {
      string surfaceShader = null;
      gltfMaterial.TechniqueExtras.TryGetValue("gvrss", out surfaceShader);

      if (surfaceShader != null) {
        // Blocks material. Look up the mapping in TbtSettings.
        if (TbtSettings.Instance.LookupSurfaceShaderMaterial(surfaceShader) is UnityMaterial um) {
          return um;
        } else {
          Debug.LogWarningFormat("Unknown gvrss surface shader {0}", surfaceShader);
        }
      }
    }

    // This method of building Guid from a name is flimsy, and proven so by b/109698832.
    // As a patch fix, look specifically for Blocks material names.

    // Is this a Gltf2 Blocks material?
    Gltf2Material gltf2 = gltfMaterial as Gltf2Material;
    if (gltf2 != null) {
      if (gltfMaterial.name != null) {
        string url = "https://vr.google.com/shaders/w/gvrss/";
        if (gltfMaterial.name.Equals("BlocksGem")) {
          return TbtSettings.Instance.LookupSurfaceShaderMaterial(url + "gem.json");
        }
        if (gltfMaterial.name.Equals("BlocksGlass")) {
          return TbtSettings.Instance.LookupSurfaceShaderMaterial(url + "glass.json");
        }
        if (gltfMaterial.name.Equals("BlocksPaper")) {
          return TbtSettings.Instance.LookupSurfaceShaderMaterial(url + "paper.json");
        }
      }
    }
#endif

    // Check if it's a Tilt Brush material.
    Guid guid = ParseGuidFromMaterial(gltfMaterial);
    if (guid != Guid.Empty) {
      // Tilt Brush global material. PBR materials will use unrecognized guids;
      // these will be handled by the caller.
      BrushDescriptor desc;
      if (TbtSettings.Instance.TryGetBrush(guid, out desc)) {
        return new UnityMaterial {
            material = desc.Material,
#if TILT_BRUSH
            exportableMaterial = desc,
#endif
            template = desc.Material
        };
      }
    }
    return null;
  }

  private UnityMaterial? ConvertGltfMaterial(GltfMaterialBase gltfMat) {
    if (gltfMat is Gltf1Material) {
      return ConvertGltf1Material((Gltf1Material)gltfMat);
    } else if (gltfMat is Gltf2Material) {
      return ConvertGltf2Material((Gltf2Material)gltfMat);
    } else {
      Debug.LogErrorFormat("Unexpected type: {0}", gltfMat.GetType());
      return null;
    }
  }

  /// <summary>
  /// Converts the given glTF1 material to a new Unity material.
  /// This is only possible if the passed material is a Tilt Brush "PBR" material
  /// squeezed into glTF1.
  /// </summary>
  /// <param name="gltfMat">The glTF1 material to convert.</param>
  /// <returns>The result of the conversion, or null on failure.</returns>
  private UnityMaterial? ConvertGltf1Material(Gltf1Material gltfMat) {
    // We know this guid doesn't map to a brush; if it did, LookupGlobalMaterial would
    // have succeeded and we wouldn't be trying to create an new material.
    Guid instanceGuid = ParseGuidFromMaterial(gltfMat);
    Guid templateGuid = ParseGuidFromShader(gltfMat);

    BrushDescriptor desc;
    if (!TbtSettings.Instance.TryGetBrush(templateGuid, out desc)) {
      // If they are the same, there is no template/instance relationship.
      if (instanceGuid != templateGuid) {
        Debug.LogErrorFormat("Unexpected: cannot find template material {0} for {1}",
                             templateGuid, instanceGuid);
      }
      return null;
    }

    TiltBrushGltf1PbrValues tbPbr = gltfMat.values;
    // The default values here are reasonable fallbacks if there is no tbPbr
    Gltf2Material.PbrMetallicRoughness pbr = new Gltf2Material.PbrMetallicRoughness();
    if (tbPbr != null) {
      if (tbPbr.BaseColorFactor != null) {
        pbr.baseColorFactor = tbPbr.BaseColorFactor.Value;
      }
      if (tbPbr.MetallicFactor != null) {
        pbr.metallicFactor = tbPbr.MetallicFactor.Value;
      }
      if (tbPbr.RoughnessFactor != null) {
        pbr.roughnessFactor = tbPbr.RoughnessFactor.Value;
      }
      if (tbPbr.BaseColorTexPtr != null) {
        pbr.baseColorTexture = new Gltf2Material.TextureInfo {
          index = -1,
          texCoord = 0,
          texture = tbPbr.BaseColorTexPtr
        };
      }
      // Tilt Brush doesn't support metallicRoughnessTexture (yet?)
    }
    var pbrInfo = new TbtSettings.PbrMaterialInfo {
#if TILT_BRUSH
        descriptor = desc,
#endif
        material = desc.Material
    };
    return CreateNewPbrMaterial(pbrInfo, gltfMat.name, pbr);
  }

  /// <summary>
  /// Converts the given GLTF 2 material to a Unity Material.
  /// This is "best effort": we only interpret SOME, but not all GLTF material parameters.
  /// We try to be robust, and will always try to return *some* material rather than fail,
  /// even if crucial fields are missing or can't be parsed.
  /// </summary>
  /// <param name="gltfMat">The GLTF 2 material to convert.</param>
  /// <returns>The result of the conversion</returns>
  private UnityMaterial? ConvertGltf2Material(Gltf2Material gltfMat) {
    TbtSettings.PbrMaterialInfo pbrInfo;

#if TILT_BRUSH
    if (!gltfMat.doubleSided) {
      // TBT supports importing single-sided materials, forcing TB to try.
      // TB will import them but can't export single-sided because it lacks single-sided BD.
      // Single-sided BD
      // TB's copy of TbtSettings uses our double-sided BrushDescriptor for these single-sided
      // materials, so they'll export as double-sided.
      // TODO: create single-sided BrushDescriptors, push out to TBT, PT, Poly, ...
      Debug.LogWarning($"Not fully supported: single-sided");
    }
#endif

    string alphaMode = gltfMat.alphaMode == null ? null : gltfMat.alphaMode.ToUpperInvariant();
    switch (alphaMode) {
      case null:
      case "":
      case Gltf2Material.kAlphaModeOpaque:
        pbrInfo = gltfMat.doubleSided
            ? TbtSettings.Instance.m_PbrOpaqueDoubleSided
            : TbtSettings.Instance.m_PbrOpaqueSingleSided;
        break;
      case Gltf2Material.kAlphaModeBlend:
        pbrInfo = gltfMat.doubleSided
            ? TbtSettings.Instance.m_PbrBlendDoubleSided
            : TbtSettings.Instance.m_PbrBlendSingleSided;
        break;
      default:
        Debug.LogWarning($"Not yet supported: alphaMode={alphaMode}");
        goto case Gltf2Material.kAlphaModeOpaque;
    }

    if (gltfMat.pbrMetallicRoughness == null) {
      var specGloss = gltfMat.extensions?.KHR_materials_pbrSpecularGlossiness;
      if (specGloss != null) {
        // Try and make the best of pbrSpecularGlossiness.
        // Maybe it would be better to support "extensionsRequired" and raise an error
        // if the asset requires pbrSpecularGlossiness.
        gltfMat.pbrMetallicRoughness = new Gltf2Material.PbrMetallicRoughness {
            baseColorFactor = specGloss.diffuseFactor,
            baseColorTexture = specGloss.diffuseTexture,
            roughnessFactor = 1f - specGloss.glossinessFactor
        };
      } else {
        Debug.LogWarningFormat("Material #{0} has no PBR info.", gltfMat.gltfIndex);
        return null;
      }
    }

    return CreateNewPbrMaterial(pbrInfo, gltfMat.name, gltfMat.pbrMetallicRoughness);
  }

  // Helper for ConvertGltf{1,2}Material
  private UnityMaterial CreateNewPbrMaterial(
      TbtSettings.PbrMaterialInfo pbrInfo, string gltfMatName,
      Gltf2Material.PbrMetallicRoughness pbr) {
    Material mat = UnityEngine.Object.Instantiate(pbrInfo.material);

    Texture tex = null;
    if (pbr.baseColorTexture != null) {
      tex = pbr.baseColorTexture.texture.unityTexture;
      mat.SetTexture("_BaseColorTex", tex);
    }

    if (gltfMatName != null) {
      // The gltf has a name it wants us to use
      mat.name = gltfMatName;
    } else {
      // No name in the gltf; make up something reasonable
      string matName = pbrInfo.material.name;
      if (matName.StartsWith("Base")) { matName = matName.Substring(4); }
      if (tex != null) {
        matName = string.Format("{0}_{1}", matName, tex.name);
      }
      mat.name = matName;
    }

    mat.SetColor("_BaseColorFactor", pbr.baseColorFactor);
    mat.SetFloat("_MetallicFactor", pbr.metallicFactor);
    mat.SetFloat("_RoughnessFactor", pbr.roughnessFactor);

    return new UnityMaterial {
        material = mat,
#if TILT_BRUSH
        exportableMaterial = pbrInfo.descriptor,
#endif
        template = pbrInfo.material
    };
  }

  private static string SanitizeName(string uri) {
    uri = System.IO.Path.ChangeExtension(uri, "");
    return Regex.Replace(uri, @"[^a-zA-Z0-9_-]+", "");
  }

  /// <summary>
  /// Fills in gltfTexture.unityTexture with a Texture2D.
  /// </summary>
  /// <param name="gltfTexture">The glTF texture to convert.</param>
  /// <param name="loader">The IUriLoader to use for loading image files.</param>
  /// <returns>On completion of the coroutine, gltfTexture.unityTexture will be non-null
  /// on success.</returns>
  private static IEnumerable ConvertTextureCoroutine(
      GltfTextureBase gltfTexture, IUriLoader loader) {
    if (gltfTexture.unityTexture != null) {
      throw new InvalidOperationException("Already converted");
    }

    if (gltfTexture.SourcePtr == null) {
      Debug.LogErrorFormat("No image for texture {0}", gltfTexture.GltfId);
      yield break;
    }

    Texture2D tex;
    if (gltfTexture.SourcePtr.data != null) {
      // This case is hit if the client code hooks up its own threaded
      // texture-loading mechanism.
      var data = gltfTexture.SourcePtr.data;
      tex = new Texture2D(data.colorWidth, data.colorHeight, data.format, true);
      yield return null;
      tex.SetPixels32(data.colorData);
      yield return null;
      tex.Apply();
      yield return null;
    } else {
#if UNITY_EDITOR && !TILT_BRUSH
      // Prefer to load the Asset rather than create a new Texture2D;
      // that lets the resulting prefab reference the texture rather than
      // embedding it inside the prefab.
      tex = loader.LoadAsAsset(gltfTexture.SourcePtr.uri);
#else
      tex = null;
#endif
      if (tex == null) {
        byte[] textureBytes;
        try {
          using (IBufferReader r = loader.Load(gltfTexture.SourcePtr.uri)) {
            textureBytes = new byte[r.GetContentLength()];
            r.Read(textureBytes, destinationOffset: 0, readStart: 0, readSize: textureBytes.Length);
          }
        } catch (IOException e) {
          Debug.LogWarning($"Cannot read uri {gltfTexture.SourcePtr.uri}: {e}");
          yield break;
        }
        tex = new Texture2D(1,1);
        tex.LoadImage(textureBytes, markNonReadable: false);
        yield return null;
      }
    }

    tex.name = SanitizeName(gltfTexture.SourcePtr.uri);
    gltfTexture.unityTexture = tex;
  }

  // Returns the guid that represents this material.
  // The guid may refer to a pre-existing material (like Blocks Paper, or Tilt Brush Light).
  // It may also refer to a dynamically-generated material, in which case the base material
  // can be found by using ParseGuidFromShader.
  private static Guid ParseGuidFromMaterial(GltfMaterialBase gltfMaterial) {
    if (Guid.TryParse((gltfMaterial as Gltf2Material)?.extensions?.GOOGLE_tilt_brush_material?.guid,
                      out Guid guid)) {
      return guid;
    }
    // Tilt Brush names its gltf materials like:
    //   material_Light-2241cd32-8ba2-48a5-9ee7-2caef7e9ed62

    // .net 3.5 doesn't have Guid.TryParse, and raising FormatException generates
    // tons of garbage for something that is done so often.
    if (!kTiltBrushMaterialRegex.IsMatch(gltfMaterial.name)) {
      return Guid.Empty;
    }
    int start = Mathf.Max(0, gltfMaterial.name.Length - 36);
    if (start < 0) { return Guid.Empty; }
    return new Guid(gltfMaterial.name.Substring(start));
  }

  // Returns the guid found on this material's vert or frag shader, or Empty on failure.
  // This Guid represents the template from which a pbr material was created.
  // For example, BasePbrOpaqueDoubleSided.
  private static Guid ParseGuidFromShader(Gltf1Material material) {
    var technique = material.techniquePtr;
    if (technique == null) { return Guid.Empty; }
    var program = technique.programPtr;
    if (program == null) { return Guid.Empty; }
    var shader = program.vertexShaderPtr ?? program.fragmentShaderPtr;
    if (shader == null) { return Guid.Empty; }
    var match = kTiltBrushShaderRegex.Match(shader.uri);
    if (match.Success) {
      return new Guid(match.Groups[1].Value);
    } else {
      return Guid.Empty;
    }
  }

  /// Returns a BrushDescriptor given a gltf material, or null if not found.
  /// If the material is an instance of a template, the descriptor for that
  /// will be returned.
  /// Note that gltf2 has pbr support, and Tilt Brush uses that instead of
  /// template "brushes".
  public static BrushDescriptor LookupBrushDescriptor(GltfMaterialBase gltfMaterial) {
    Guid guid = ParseGuidFromMaterial(gltfMaterial);
    if (guid == Guid.Empty) {
      return null;
    } else {
      BrushDescriptor desc;
      TbtSettings.Instance.TryGetBrush(
          guid, out desc);
      if (desc == null) {
        // Maybe it's templated from a pbr material; the template guid
        // can be found on the shader.
        Gltf1Material gltf1Material = gltfMaterial as Gltf1Material;
        if (gltf1Material == null) {
#if TILT_BRUSH
          Debug.LogErrorFormat("Unexpected: glTF2 Tilt Brush material");
#endif
          return null;
        }
        Guid templateGuid = ParseGuidFromShader((Gltf1Material)gltfMaterial);
        TbtSettings.Instance.TryGetBrush(
          templateGuid, out desc);
      }
      return desc;
    }
  }
}

}
