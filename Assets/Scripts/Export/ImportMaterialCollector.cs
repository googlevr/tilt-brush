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

#if FBX_SUPPORTED
using Autodesk.Fbx;
#endif
using UnityEngine;

using TiltBrushToolkit;

namespace TiltBrush {

/// This class is responsible for collecting import material information and returning an
/// IExportableMaterial that corresponds to a given Material instance. This lets us capture
/// information at import time that allows us to re-export any of the imported materials.
///
/// Currently, the only import material supported is a Gltf2 Material and the only export
/// material supported is a PBR material.
public class ImportMaterialCollector : IImportMaterialCollector {
  private readonly string m_AssetLocation;
  private readonly Guid m_guidNamespace;
  private Dictionary<Material, IExportableMaterial> m_MaterialToIem =
      new Dictionary<Material, IExportableMaterial>();
  private int m_numAdded = 0;

  // Pass:
  //   randomSeed -
  //     A seed string that helps uniquify the generated material names.
  //     This must be different for each imported model that generates materials.
  public ImportMaterialCollector(string assetLocation, string uniqueSeed) {
    m_guidNamespace = GuidUtils.Uuid5(Guid.Empty, uniqueSeed);
    m_AssetLocation = assetLocation;
  }

#if FBX_SUPPORTED
  // Used for FBX imports
  public void Add(
      Material unityMaterial,
      bool transparent, string baseColorUri, FbxSurfaceLambert fbxMaterial) {
    if (baseColorUri != null) {
      Debug.Assert(File.Exists(Path.Combine(m_AssetLocation, baseColorUri)));
    }

    TbtSettings.PbrMaterialInfo pbrInfo = transparent
        ? TbtSettings.Instance.m_PbrBlendDoubleSided
        : TbtSettings.Instance.m_PbrOpaqueDoubleSided;

    m_MaterialToIem.Add(
        unityMaterial,
        new DynamicExportableMaterial(
            parent: pbrInfo.descriptor,
            durableName: fbxMaterial.GetName(),
            uniqueName: MakeDeterministicUniqueName(m_numAdded++, fbxMaterial.GetName()),
            uriBase: m_AssetLocation) {
                BaseColorFactor = unityMaterial.GetColor("_Color"),
                BaseColorTex = baseColorUri,
            });
  }
#endif

  // Used for gltf imports
  public void Add(GltfMaterialConverter.UnityMaterial um,
                  GltfMaterialBase gltfMaterial) {
    if (m_MaterialToIem.ContainsKey(um.material)) {
      return;
    }
    if (um.exportableMaterial == null) {
      Debug.LogWarning($"gltf imported a non-exportable material {um.material.name}");
      return;
    }

    IExportableMaterial iem;
    if (um.material == um.template) {
      // No customizations made, so we can use the IEM we're given instead of making a child IEM.
      // This must be a "global" material (ie, Blocks, or Tilt Brush brush)
      iem = um.exportableMaterial;
    } else if (gltfMaterial is Gltf2Material gltf2Material) {
      // It's hard to introspect UnityEngine.Materials to see how um.material was customized from
      // um.template, so instead we make guesses based on what we find in the gltf.
      var pbr = gltf2Material.pbrMetallicRoughness;
      if (pbr == null) {
        Debug.LogWarning($"{um.material.name} looks like a generated material, but has no pbr");
        iem = null;
      } else {
        iem = new DynamicExportableMaterial(
            parent: um.exportableMaterial,
            durableName: gltf2Material.name,
            uniqueName: MakeDeterministicUniqueName(m_numAdded++, gltf2Material.name),
            uriBase: m_AssetLocation) {
                BaseColorFactor = pbr.baseColorFactor,
                BaseColorTex = pbr.baseColorTexture?.texture?.SourcePtr?.uri,
                MetallicFactor = pbr.metallicFactor,
                RoughnessFactor = pbr.roughnessFactor
            };
      }
    } else {
      // The only source of gltf1 is TB-on-poly, or the first revision of Quest export-to-glb.
      // Neither is worth the effort of supporting?
      Debug.LogWarning($"{um.material.name}: no support for non-global materials in gltf1");
      iem = null;
    }

    if (iem != null) {
      m_MaterialToIem.Add(um.material, iem);
    }
  }

  [JetBrains.Annotations.Pure]
  public Guid MakeDeterministicUniqueName(int data, string data2) {
    return GuidUtils.Uuid5(m_guidNamespace, string.Format("{0}_{1}", data, data2));
  }

  /// Returns the unique IExportableMaterial that corresponds to the passed Material,
  /// or null if there is none.
  public IExportableMaterial GetExportableMaterial(Material unityMaterial) {
    if (m_MaterialToIem.TryGetValue(unityMaterial, out var ret)) {
      return ret;
    } else {
      return null;
    }
  }
}

}  // namespace TiltBrush
