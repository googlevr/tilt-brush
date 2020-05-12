// Copyright 2019 Google LLC
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

using UnityEngine;

#if TILT_BRUSH
using System.Collections.Generic;
using System.Linq;
using BrushDescriptor = TiltBrush.BrushDescriptor;
using UnityMaterial = TiltBrushToolkit.GltfMaterialConverter.UnityMaterial;
#endif

namespace TiltBrushToolkit {

public class TbtSettings : ScriptableObject {
  [Serializable]
  public struct PbrMaterialInfo {
    public Material material;
#if TILT_BRUSH
    public BrushDescriptor descriptor;
#endif
  }
  const string kAssetName = "TiltBrushToolkitSettings";

  private static TbtSettings sm_Instance;

  public static TbtSettings Instance {
    get {
      if (sm_Instance == null) {
#if TILT_BRUSH
        sm_Instance = TiltBrush.App.Config.m_TbtSettings;
#else
        sm_Instance = Resources.Load<TbtSettings>(kAssetName);
        if (sm_Instance == null) {
          throw new InvalidOperationException("Cannot find " + kAssetName + ".asset");
        }
#endif
      }
      return sm_Instance;
    }
  }

  public static Version TbtVersion {
    get { return new Version { major = 23, minor = 0 }; }
  }

#if !TILT_BRUSH
  public static BrushManifest BrushManifest {
    get { return sm_Instance.m_BrushManifest; }
  }

  [SerializeField] private BrushManifest m_BrushManifest = null;
#endif

  public PbrMaterialInfo m_PbrOpaqueSingleSided;
  public PbrMaterialInfo m_PbrOpaqueDoubleSided;
  public PbrMaterialInfo m_PbrBlendSingleSided;
  public PbrMaterialInfo m_PbrBlendDoubleSided;

  /// <returns>null if not found</returns>
  public bool TryGetBrush(Guid guid, out BrushDescriptor desc) {
#if TILT_BRUSH
    desc = TiltBrush.BrushCatalog.m_Instance.GetBrush(guid);
    return (desc != null);
#else
    return m_BrushManifest.BrushesByGuid.TryGetValue(guid, out desc);
#endif
  }

  //
  // Tilt Brush extensions for Blocks
  //

#if TILT_BRUSH
  // One or the other of material and descriptor should be set.
  // If both are set, they will be sanity-checked against each other.
  [Serializable]
  struct SurfaceShaderMaterial {
    public string shaderUrl;
    public Material material;
    public BrushDescriptor descriptor;

    internal Material Material {
      get {
#if UNITY_EDITOR
        if (material != null && descriptor != null) {
          if (material != descriptor.Material) {
            Debug.LogWarningFormat("{0} has conflicting materials", shaderUrl);
          }
        }
#endif
        if (material != null) {
          return material;
        } else if (descriptor != null) {
          return descriptor.Material;
        } else {
          return null;
        }
      }
    }
  }

  [SerializeField] private SurfaceShaderMaterial[] m_SurfaceShaderMaterials;

  private Dictionary<string, UnityMaterial> m_SurfaceShaderMaterialLookup;

  public static Version PtVersion => new Version { major = 0, minor = 5 };

  /// <returns>null if not found</returns>
  public UnityMaterial? LookupSurfaceShaderMaterial(string url) {
    if (m_SurfaceShaderMaterialLookup == null) {
      m_SurfaceShaderMaterialLookup = m_SurfaceShaderMaterials.ToDictionary(
          elt => elt.shaderUrl,
          elt => new UnityMaterial {
              material = elt.material,
              exportableMaterial = elt.descriptor,
              template = elt.material
          });
    }
    if (m_SurfaceShaderMaterialLookup.TryGetValue(url, out var ret)) {
      return ret;
    } else {
      return null;
    }
  }
#endif
}

}
